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
/// Uses Kahan compensated summation for numerical stability of the running sum-of-squares,
/// eliminating the need for periodic resynchronization.
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
    private double _sumSq;
    private double _p_sumSq;
    private double _sumSqComp;     // Kahan compensation for _sumSq
    private double _p_sumSqComp;

    public override bool IsHot => _buffer.Count >= _period;

    /// <summary>
    /// Initializes a rolling one-sample t-test indicator.
    /// </summary>
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
        _sumSq = 0.0;
        _p_sumSq = 0.0;
        _sumSqComp = 0.0;
        _p_sumSqComp = 0.0;
        _handler = Handle;
    }

    /// <summary>
    /// Initializes a rolling one-sample t-test indicator and subscribes it to a source publisher.
    /// </summary>
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
        double value = input.Value;

        if (!double.IsFinite(value))
        {
            value = _lastValidValue;
        }
        else
        {
            _lastValidValue = value;
        }

        if (isNew)
        {
            _p_sumSq = _sumSq;
            _p_sumSqComp = _sumSqComp;
            _buffer.Snapshot();
        }
        else
        {
            _sumSq = _p_sumSq;
            _sumSqComp = _p_sumSqComp;
            _buffer.Restore();
        }

        if (_buffer.IsFull)
        {
            double oldVal = _buffer.Oldest;
            // Kahan subtract old²
            double y = -(oldVal * oldVal) - _sumSqComp;
            double t = _sumSq + y;
            _sumSqComp = (t - _sumSq) - y;
            _sumSq = t;
        }

        _buffer.Add(value);

        // Kahan add new²
        {
            double y = (value * value) - _sumSqComp;
            double t = _sumSq + y;
            _sumSqComp = (t - _sumSq) - y;
            _sumSq = t;
        }

        double result;
        int n = _buffer.Count;

        if (n < 2)
        {
            result = 0.0;
        }
        else
        {
            double sum = _buffer.Sum;
            double mean = sum / n;

            double numerator = _sumSq - (sum * sum) / n;
            if (numerator < 0)
            {
                numerator = 0;
            }

            // Bessel correction: sample variance = popVariance * n / (n - 1)
            // which is numerator / (n - 1)
            double sampleVariance = numerator / (n - 1);
            double sampleStdDev = Math.Sqrt(sampleVariance);
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

        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return new TSeries();
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, _buffer.Capacity, _mu0);
        source.Times.CopyTo(tSpan);

        int primeStart = Math.Max(0, len - _buffer.Capacity);
        for (int i = primeStart; i < len; i++)
        {
            Update(source[i]);
        }

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs args) => Update(args.Value, args.IsNew);

    public override void Reset()
    {
        _buffer.Clear();
        _lastValidValue = 0;
        _sumSq = 0.0;
        _p_sumSq = 0.0;
        _sumSqComp = 0.0;
        _p_sumSqComp = 0.0;
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
            double sum = 0.0;
            double sumSq = 0.0;
            double sumComp = 0.0;      // Kahan compensation for sum
            double sumSqComp = 0.0;    // Kahan compensation for sumSq

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

                if (count == ringSize)
                {
                    double oldVal = ring[head];

                    // Kahan subtract old from sum
                    double ys = -oldVal - sumComp;
                    double ts = sum + ys;
                    sumComp = (ts - sum) - ys;
                    sum = ts;

                    // Kahan subtract old² from sumSq
                    double ysq = -(oldVal * oldVal) - sumSqComp;
                    double tsq = sumSq + ysq;
                    sumSqComp = (tsq - sumSq) - ysq;
                    sumSq = tsq;
                }
                else
                {
                    count++;
                }

                ring[head] = val;

                // Kahan add val to sum
                {
                    double ys = val - sumComp;
                    double ts = sum + ys;
                    sumComp = (ts - sum) - ys;
                    sum = ts;
                }

                // Kahan add val² to sumSq
                {
                    double ysq = (val * val) - sumSqComp;
                    double tsq = sumSq + ysq;
                    sumSqComp = (tsq - sumSq) - ysq;
                    sumSq = tsq;
                }

                head = (head + 1) % ringSize;

                if (count < 2)
                {
                    output[i] = 0.0;
                    continue;
                }

                int n = count;
                double mean = sum / n;

                double numerator = sumSq - (sum * sum) / n;
                if (numerator < 0)
                {
                    numerator = 0;
                }

                // Bessel correction: sample variance = popVariance * n / (n - 1)
                double sampleVariance = numerator / (n - 1);
                double sampleStdDev = Math.Sqrt(sampleVariance);
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
