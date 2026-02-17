// ZTEST: One-Sample t-Test Statistic
// Computes t = (x̄ - μ₀) / (s / √n) using sample standard deviation (N-1 Bessel correction)
// Formula: t = (mean - mu0) / standardError, where standardError = sampleStdDev / sqrt(n)

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// ZTEST: One-Sample t-Test — computes the t-statistic measuring how many
/// standard errors the rolling sample mean deviates from a hypothesized mean μ₀.
/// </summary>
/// <remarks>
/// Key properties:
/// - Uses sample standard deviation (N-1 denominator, Bessel correction)
/// - Output is unbounded; values beyond ±2.04 (period=30) suggest 95% significance
/// - When standard error is negligible (&lt; 1e-10), returns 0.0
/// - Period must be >= 2
/// - Despite the name "ZTEST" (per PineScript convention), this computes a t-statistic
/// </remarks>
/// <seealso href="ztest.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Ztest : AbstractBase
{
    private readonly int _period;
    private readonly double _mu0;
    private readonly RingBuffer _buffer;
    private readonly TValuePublishedHandler _handler;
    private double _lastValidValue;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double LastValidTStat, double LastValidValue);
    private State _s, _ps;

    public override bool IsHot => _buffer.Count >= _period;

    /// <param name="period">Lookback period (default 30, must be >= 2)</param>
    /// <param name="mu0">Hypothesized population mean (default 0.0)</param>
    public Ztest(int period = 30, double mu0 = 0.0)
    {
        if (period < 2)
        {
            throw new ArgumentException("Period must be >= 2 for t-test calculation.", nameof(period));
        }

        _period = period;
        _mu0 = mu0;
        _buffer = new RingBuffer(period);
        Name = $"Ztest({period},{mu0:G})";
        WarmupPeriod = period;
        _s = new State(0.0, 0.0);
        _ps = _s;
        _handler = Handle;
    }

    /// <param name="source">Source indicator for event-based chaining</param>
    /// <param name="period">Lookback period (default 30)</param>
    /// <param name="mu0">Hypothesized population mean (default 0.0)</param>
    public Ztest(ITValuePublisher source, int period = 30, double mu0 = 0.0) : this(period, mu0)
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
            // Population variance first: E[X²] - (E[X])²
            double popVariance = (sumSq / n) - (mean * mean);

            if (popVariance < 0.0)
            {
                popVariance = 0.0;
            }

            // Bessel correction: sample variance = popVariance * n / (n - 1)
            double sampleStdDev = Math.Sqrt(popVariance * n / (n - 1));
            double standardError = sampleStdDev / Math.Sqrt(n);

            if (standardError > 1e-10)
            {
                result = (mean - _mu0) / standardError;
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

    public static TSeries Batch(TSeries source, int period = 30, double mu0 = 0.0)
    {
        var indicator = new Ztest(period, mu0);
        return indicator.Update(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period = 30, double mu0 = 0.0)
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

                // Bessel correction: sample variance = popVariance * n / (n - 1)
                double sampleStdDev = Math.Sqrt(popVariance * n / (n - 1));
                double standardError = sampleStdDev / Math.Sqrt(n);

                if (standardError > 1e-10)
                {
                    output[i] = (mean - mu0) / standardError;
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

    public static (TSeries Results, Ztest Indicator) Calculate(TSeries source, int period = 30, double mu0 = 0.0)
    {
        var indicator = new Ztest(period, mu0);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}
