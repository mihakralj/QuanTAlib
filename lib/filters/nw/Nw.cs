using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class Nw : AbstractBase
{
    private readonly int _period;
    private readonly double _bandwidth;
    private readonly double[] _weights;
    private readonly RingBuffer _buffer;
    private readonly ITValuePublisher? _publisher;
    private readonly TValuePublishedHandler? _handler;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double LastValid, int Count);
    private State _s;
    private State _ps;

    public int Period => _period;
    public double Bandwidth => _bandwidth;
    public override bool IsHot => _s.Count >= _period;

    public Nw(int period = 64, double bandwidth = 8.0)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be >= 1.");
        }
        if (bandwidth <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(bandwidth), "Bandwidth must be > 0.");
        }

        _period = period;
        _bandwidth = bandwidth;
        _buffer = new RingBuffer(period);
        WarmupPeriod = period;
        Name = $"Nw({period},{bandwidth:F1})";

        // Precompute Gaussian kernel weights: w[i] = exp(-i^2 / (2*h^2))
        _weights = new double[period];
        double h2x2 = 2.0 * bandwidth * bandwidth;
        for (int i = 0; i < period; i++)
        {
            _weights[i] = Math.Exp(-((double)i * i) / h2x2);
        }

        Init();
    }

    public Nw(ITValuePublisher source, int period = 64, double bandwidth = 8.0)
        : this(period, bandwidth)
    {
        _publisher = source;
        _handler = Sub;
        source.Pub += _handler;
    }

    private void Sub(object? source, in TValueEventArgs args)
    {
        Update(args.Value, args.IsNew);
    }

    public void Init()
    {
        _s = default;
        _ps = default;
        _buffer.Clear();
    }

    public override void Reset()
    {
        Init();
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (double value in source)
        {
            Update(new TValue(DateTime.MinValue, value));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        // skipcq: CS-R1140 - NW reads individual buffer positions; cannot use Snapshot/Restore
        if (isNew)
        {
            _ps = _s;
        }
        else
        {
            _s = _ps;
        }

        var s = _s;

        double val = input.Value;
        if (!double.IsFinite(val))
        {
            val = double.IsFinite(s.LastValid) ? s.LastValid : 0.0;
        }

        if (isNew)
        {
            _buffer.Add(val);
            s.Count++;
        }
        else
        {
            _buffer.UpdateNewest(val);
        }

        // Nadaraya-Watson: weighted average with Gaussian kernel
        // _buffer[0] = oldest, _buffer[Count-1] = newest
        // weights[0] = newest weight (1.0), weights[i] = i bars ago
        int bufCount = _buffer.Count;
        int bars = Math.Min(bufCount, _period);
        int newestIdx = bufCount - 1;
        double num = 0.0;
        double den = 0.0;

        for (int i = 0; i < bars; i++)
        {
            double w = _weights[i];
            double sample = _buffer[newestIdx - i];
            num = Math.FusedMultiplyAdd(w, sample, num);
            den += w;
        }

        double result = den > 0.0 ? num / den : val;

        if (double.IsFinite(val))
        {
            s.LastValid = val;
        }

        _s = s;

        TValue output = new(input.Time, result);
        Last = output;
        PubEvent(output, isNew);
        return output;
    }

    public override TSeries Update(TSeries source)
    {
        var tsResult = new TSeries();
        ReadOnlySpan<double> srcSpan = source.Values;
        double[] outArray = new double[srcSpan.Length];

        Batch(srcSpan, outArray.AsSpan(), _period, _bandwidth);

        for (int i = 0; i < outArray.Length; i++)
        {
            tsResult.Add(new TValue(source.Times[i], outArray[i]));
        }

        if (srcSpan.Length > 0)
        {
            int replayStart = Math.Max(0, srcSpan.Length - Math.Max(WarmupPeriod, 4));
            Reset();
            for (int i = replayStart; i < srcSpan.Length; i++)
            {
                Update(new TValue(source.Times[i], srcSpan[i]), isNew: true);
            }
        }

        return tsResult;
    }

    public static void Batch(ReadOnlySpan<double> source, Span<double> output,
        int period = 64, double bandwidth = 8.0)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output lengths must match.", nameof(output));
        }
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be >= 1.");
        }
        if (bandwidth <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(bandwidth), "Bandwidth must be > 0.");
        }

        // Precompute Gaussian weights
        const int StackallocThreshold = 256;
        double[]? rentedW = null;
        scoped Span<double> weights;
        if (period <= StackallocThreshold)
        {
            weights = stackalloc double[period];
        }
        else
        {
            rentedW = ArrayPool<double>.Shared.Rent(period);
            weights = rentedW.AsSpan(0, period);
        }

        try
        {
            double h2x2 = 2.0 * bandwidth * bandwidth;
            for (int i = 0; i < period; i++)
            {
                weights[i] = Math.Exp(-((double)i * i) / h2x2);
            }

            // Pre-clean source: replace NaN/Infinity with last-valid
            // (matches streaming behavior where buffer stores cleaned values)
            const int CleanThreshold = 256;
            double[]? rentedClean = null;
            scoped Span<double> clean;
            if (source.Length <= CleanThreshold)
            {
                clean = stackalloc double[source.Length];
            }
            else
            {
                rentedClean = ArrayPool<double>.Shared.Rent(source.Length);
                clean = rentedClean.AsSpan(0, source.Length);
            }

            try
            {
                double lastVal = 0.0;
                for (int t = 0; t < source.Length; t++)
                {
                    double val = source[t];
                    if (!double.IsFinite(val))
                    {
                        val = lastVal;
                    }
                    else
                    {
                        lastVal = val;
                    }
                    clean[t] = val;
                }

                for (int t = 0; t < source.Length; t++)
                {
                    int bars = Math.Min(t + 1, period);
                    double num = 0.0;
                    double den = 0.0;

                    for (int i = 0; i < bars; i++)
                    {
                        double w = weights[i];
                        double sample = clean[t - i];
                        num = Math.FusedMultiplyAdd(w, sample, num);
                        den += w;
                    }

                    output[t] = den > 0.0 ? num / den : clean[t];
                }
            }
            finally
            {
                if (rentedClean != null)
                {
                    ArrayPool<double>.Shared.Return(rentedClean);
                }
            }
        }
        finally
        {
            if (rentedW != null)
            {
                ArrayPool<double>.Shared.Return(rentedW);
            }
        }
    }

    public static TSeries Batch(TSeries source, int period = 64, double bandwidth = 8.0)
    {
        var result = new TSeries();
        ReadOnlySpan<double> srcSpan = source.Values;
        double[] outArray = new double[srcSpan.Length];

        Batch(srcSpan, outArray.AsSpan(), period, bandwidth);

        for (int i = 0; i < outArray.Length; i++)
        {
            result.Add(new TValue(source.Times[i], outArray[i]));
        }

        return result;
    }

    public static (TSeries Results, Nw Indicator) Calculate(TSeries source,
        int period = 64, double bandwidth = 8.0)
    {
        var indicator = new Nw(period, bandwidth);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _publisher != null && _handler != null)
        {
            _publisher.Pub -= _handler;
        }
        base.Dispose(disposing);
    }
}
