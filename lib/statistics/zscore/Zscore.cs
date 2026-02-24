// ZSCORE: Z-Score (Population Standard Score) — also known as STANDARDIZE
// Calculates z = (x - μ) / σ using population standard deviation (N denominator)
// Formula: z = (x - mean) / sqrt(Σ(xi - mean)² / N)

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// ZSCORE: Z-Score (also known as STANDARDIZE) — measures how many population
/// standard deviations a value lies from the rolling mean over a lookback window.
/// </summary>
/// <remarks>
/// Key properties:
/// - Uses population standard deviation (N denominator, no Bessel correction)
/// - Output is unbounded (typically -3 to +3 for normally distributed data)
/// - When σ = 0 (constant data), returns 0.0
/// - Period must be >= 2
/// </remarks>
/// <seealso href="zscore.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Zscore : AbstractBase
{
    private readonly int _period;
    private readonly RingBuffer _buffer;
    private readonly TValuePublishedHandler _handler;
    private double _lastValidValue;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double LastValidZScore, double LastValidValue);
    private State _s, _ps;

    public override bool IsHot => _buffer.Count >= _period;

    /// <param name="period">Lookback period (default 14, must be >= 2)</param>
    public Zscore(int period = 14)
    {
        if (period < 2)
        {
            throw new ArgumentException("Period must be >= 2 for standard deviation calculation.", nameof(period));
        }

        _period = period;
        _buffer = new RingBuffer(period);
        Name = $"Zscore({period})";
        WarmupPeriod = period;
        _s = new State(0.0, 0.0);
        _ps = _s;
        _handler = Handle;
    }

    /// <param name="source">Source indicator for event-based chaining</param>
    /// <param name="period">Lookback period (default 14)</param>
    public Zscore(ITValuePublisher source, int period = 14) : this(period)
    {
        source.Pub += _handler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _ps = _s;
        }
        else
        {
            _s = _ps;
            _lastValidValue = _s.LastValidValue;
        }

        double value = input.Value;

        if (!double.IsFinite(value))
        {
            value = _lastValidValue;
        }
        else
        {
            _lastValidValue = value;
        }

        _buffer.Add(value, isNew);

        double result;
        ReadOnlySpan<double> data = _buffer.GetSpan();
        int n = data.Length;

        if (n < 2)
        {
            result = 0.0;
        }
        else
        {
            double sum = 0.0;
            double sumSq = 0.0;

            for (int i = 0; i < n; i++)
            {
                double v = data[i];
                sum += v;
                sumSq += v * v;
            }

            double mean = sum / n;
            // Population variance: E[X²] - (E[X])²
            double popVariance = (sumSq / n) - (mean * mean);

            if (popVariance < 0.0)
            {
                popVariance = 0.0;
            }

            double stdDev = Math.Sqrt(popVariance);

            if (stdDev > 1e-10)
            {
                result = (value - mean) / stdDev;
            }
            else
            {
                result = 0.0;
            }
        }

        _s = new State(result, _lastValidValue);
        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        var result = new TSeries(source.Count);
        ReadOnlySpan<double> values = source.Values;
        ReadOnlySpan<long> times = source.Times;

        for (int i = 0; i < source.Count; i++)
        {
            var tv = Update(new TValue(new DateTime(times[i], DateTimeKind.Utc), values[i]), true);
            result.Add(tv, true);
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs args) => Update(args.Value, args.IsNew);

    public override void Reset()
    {
        _buffer.Clear();
        _lastValidValue = 0;
        _s = new State(0.0, 0.0);
        _ps = _s;
        Last = default;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        TimeSpan interval = step ?? TimeSpan.FromSeconds(1);
        DateTime time = DateTime.UtcNow - (interval * source.Length);

        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(time, source[i]), true);
            time += interval;
        }
    }

    public static TSeries Batch(TSeries source, int period = 14)
    {
        var indicator = new Zscore(period);
        return indicator.Update(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period = 14)
    {
        if (source.Length == 0)
        {
            throw new ArgumentException("Source span must not be empty.", nameof(source));
        }

        if (output.Length < source.Length)
        {
            throw new ArgumentException("Output span must be at least as long as source.", nameof(output));
        }

        if (period < 2)
        {
            throw new ArgumentException("Period must be >= 2.", nameof(period));
        }

        const int StackallocThreshold = 256;
        double[]? rented = null;
        int ringSize = period;

        scoped Span<double> ring;
        if (ringSize <= StackallocThreshold)
        {
            ring = stackalloc double[ringSize];
        }
        else
        {
            rented = ArrayPool<double>.Shared.Rent(ringSize);
            ring = rented.AsSpan(0, ringSize);
        }

        try
        {
            int head = 0;
            int count = 0;
            double lastValid = 0.0;

            for (int i = 0; i < source.Length; i++)
            {
                double val = source[i];

                if (!double.IsFinite(val))
                {
                    val = lastValid;
                }
                else
                {
                    lastValid = val;
                }

                if (count < ringSize)
                {
                    ring[count] = val;
                    count++;
                }
                else
                {
                    ring[head] = val;
                }

                head = (head + 1) % ringSize;

                if (count < 2)
                {
                    output[i] = 0.0;
                    continue;
                }

                double sum = 0.0;
                double sumSq = 0.0;
                int n = count;

                for (int j = 0; j < n; j++)
                {
                    double v = ring[j];
                    sum += v;
                    sumSq += v * v;
                }

                double mean = sum / n;
                double popVariance = (sumSq / n) - (mean * mean);

                if (popVariance < 0.0)
                {
                    popVariance = 0.0;
                }

                double stdDev = Math.Sqrt(popVariance);

                if (stdDev > 1e-10)
                {
                    output[i] = (val - mean) / stdDev;
                }
                else
                {
                    output[i] = 0.0;
                }
            }
        }
        finally
        {
            if (rented != null)
            {
                ArrayPool<double>.Shared.Return(rented);
            }
        }
    }

    public static (TSeries Results, Zscore Indicator) Calculate(TSeries source, int period = 14)
    {
        var indicator = new Zscore(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}
