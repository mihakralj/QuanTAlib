using System.Runtime.CompilerServices;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class Wiener : AbstractBase
{
    private readonly int _period;
    private readonly int _smoothPeriod;
    private readonly RingBuffer _buffer;

    public Wiener(int period, int smoothPeriod = 10)
    {
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 2.");
        }
        if (smoothPeriod < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(smoothPeriod), "Smooth period must be greater than or equal to 2.");
        }

        _period = period;
        _smoothPeriod = smoothPeriod;
        WarmupPeriod = Math.Max(_period, _smoothPeriod);
        Name = $"Wiener({_period},{_smoothPeriod})";
        _buffer = new RingBuffer(Math.Max(_period, _smoothPeriod));
    }

    public override bool IsHot => _buffer.Count >= WarmupPeriod;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Reset()
    {
        _buffer.Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (double.IsNaN(input.Value) || double.IsInfinity(input.Value))
        {
            // If we have a valid last value, return it, otherwise return input
            return isNew ? Last : new TValue(input.Time, Last.Value);
        }

        _buffer.Add(input.Value, isNew);

        // Not enough data?
        if (_buffer.Count < 2)
        {
            var res = new TValue(input.Time, input.Value);
            Last = res;
            PubEvent(res, isNew);
            return res;
        }

        double result = Calc();

        var ret = new TValue(input.Time, result);
        Last = ret;
        PubEvent(ret, isNew);
        return ret;
    }

    public override TSeries Update(TSeries source)
    {
        TSeries result = [];
        for (int i = 0; i < source.Count; i++)
        {
            result.Add(Update(source[i]));
        }
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double Calc()
    {
        // 1. Noise Variance
        // Pine:
        // for i = 0 to length - 2
        //    diff = src[i] - src[i + 1]
        //    diffs.push(diff * diff)
        // noise_var = sum(diffs) / (2 * num_diffs)

        int noiseLen = Math.Min(_buffer.Count, _period);
        double sumDiffs = 0;
        int numDiffs = 0;

        for (int i = 0; i < noiseLen - 1; i++)
        {
            // _buffer[^1] is newest (src[0])
            // _buffer[^ (i + 1)] -> src[i]
            double val1 = _buffer[^(i + 1)];
            double val2 = _buffer[^(i + 2)];
            double diff = val1 - val2;
            sumDiffs += diff * diff;
            numDiffs++;
        }

        double noiseVar = 0;
        if (numDiffs > 0)
        {
            noiseVar = sumDiffs / (2.0 * numDiffs);
        }

        // 2. Signal Variance (over smoothPeriod)
        // Pine: ta.sma(math.pow(src - ta.sma(src, smooth_len), 2.0), smooth_len)
        int signalLen = Math.Min(_buffer.Count, _smoothPeriod);

        // a. Calculate Mean of src over signalLen
        double sumSrc = 0;
        for (int i = 0; i < signalLen; i++)
        {
            sumSrc += _buffer[^(i + 1)];
        }
        double mean = sumSrc / signalLen;

        // b. Calculate Mean of Squared Deviations
        double sumSqDev = 0;
        for (int i = 0; i < signalLen; i++)
        {
            double val = _buffer[^(i + 1)];
            double dev = val - mean;
            sumSqDev += dev * dev;
        }
        double signalPlusNoise = sumSqDev / signalLen;

        // 3. Filter Logic
        double signalVar = Math.Max(signalPlusNoise - noiseVar, 0.0);
        double kp = 0;
        if (signalVar + noiseVar > 1e-10)
        {
            kp = signalVar / (signalVar + noiseVar);
        }

        // result = mean + k * (src - mean)
        double src0 = _buffer[^1];
        return mean + kp * (src0 - mean);
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        long initialTicks = DateTime.UtcNow.Ticks - source.Length * (step?.Ticks ?? TimeSpan.FromSeconds(1).Ticks);
        TimeSpan increment = step ?? TimeSpan.FromSeconds(1);

        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(initialTicks + i * increment.Ticks, source[i]));
        }
    }

    public static void Calculate(ReadOnlySpan<double> source, Span<double> destination, int period, int smoothPeriod = 10)
    {
        if (destination.Length < source.Length)
        {
            throw new ArgumentException("Destination span is shorter than source span.", nameof(destination));
        }

        var filter = new Wiener(period, smoothPeriod);
        for (int i = 0; i < source.Length; i++)
        {
            destination[i] = filter.Update(new TValue(0, source[i])).Value;
        }
    }
}
