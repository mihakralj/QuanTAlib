using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Theil: Theil's T Index (generalized entropy measure of inequality)
/// </summary>
/// <remarks>
/// Measures the inequality or concentration of values within a sliding window.
/// Based on information theory, the Theil T Index quantifies how far a distribution
/// deviates from perfect equality. Values must be positive.
///
/// Calculation:
///   T = (1/n) × Σ (xᵢ/μ) × ln(xᵢ/μ)
///
/// where μ = mean of all values in the window, n = count of valid positive values.
///
/// Properties:
/// - T = 0 indicates perfect equality (all values identical)
/// - Higher T indicates greater inequality/concentration
/// - Decomposable: total inequality = between-group + within-group
/// - Scale-invariant: multiplying all values by a constant doesn't change T
/// </remarks>
[SkipLocalsInit]
public sealed class Theil : AbstractBase
{
    private readonly int _period;
    private readonly RingBuffer _buffer;
    private double _lastValidValue;
    private readonly TValuePublishedHandler _handler;

    public override bool IsHot => _buffer.IsFull;

    /// <summary>
    /// Creates a new Theil T Index indicator.
    /// </summary>
    /// <param name="period">The lookback period (must be >= 2).</param>
    public Theil(int period)
    {
        if (period < 2)
        {
            throw new ArgumentException("Period must be greater than or equal to 2", nameof(period));
        }
        _period = period;
        _buffer = new RingBuffer(period);
        Name = $"Theil({period})";
        WarmupPeriod = period;
        _handler = Handle;
    }

    /// <summary>
    /// Creates a chaining constructor that subscribes to a source indicator.
    /// </summary>
    public Theil(ITValuePublisher src, int period) : this(period)
    {
        src.Pub += _handler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        double value = input.Value;

        // NaN/Infinity guard: substitute last valid value
        if (!double.IsFinite(value) || value <= 0)
        {
            value = _lastValidValue;
        }
        else
        {
            _lastValidValue = value;
        }

        if (isNew)
        {
            _buffer.Add(value);
        }
        else
        {
            _buffer.UpdateNewest(value);
        }

        double theil = ComputeTheil(_buffer.GetSpan());

        Last = new TValue(input.Time, theil);
        PubEvent(Last, isNew);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, _period);
        source.Times.CopyTo(tSpan);

        // Reset running state before priming
        _buffer.Clear();
        _lastValidValue = 0;

        // Prime the state
        int primeStart = Math.Max(0, len - _period);
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
        Last = default;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        DateTime ts = DateTime.MinValue;
        foreach (double value in source)
        {
            Update(new TValue(ts, value));
            if (step.HasValue)
            {
                ts = ts.Add(step.Value);
            }
        }
    }

    public static TSeries Batch(TSeries source, int period)
    {
        var theil = new Theil(period);
        return theil.Update(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (period < 2)
        {
            throw new ArgumentException("Period must be greater than or equal to 2", nameof(period));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        CalculateScalarCore(source, output, period);
    }

    public static (TSeries Results, Theil Indicator) Calculate(TSeries source, int period)
    {
        var indicator = new Theil(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateScalarCore(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        int len = source.Length;
        const int StackallocThreshold = 256;

        double[]? rentedWindow = null;
        scoped Span<double> windowBuf;
        if (period <= StackallocThreshold)
        {
            windowBuf = stackalloc double[period];
        }
        else
        {
            rentedWindow = ArrayPool<double>.Shared.Rent(period);
            windowBuf = rentedWindow.AsSpan(0, period);
        }

        try
        {
            // Persistent lastValidValue across all iterations — matches streaming Update behavior
            double lastValidValue = 0;
            for (int i = 0; i < len; i++)
            {
                int windowStart = Math.Max(0, i - period + 1);
                int windowLen = i - windowStart + 1;

                // Copy window values with NaN/non-positive substitution using persistent lastValidValue
                double windowLastValid = lastValidValue;
                for (int j = 0; j < windowLen; j++)
                {
                    double wv = source[windowStart + j];
                    if (!double.IsFinite(wv) || wv <= 0)
                    {
                        wv = windowLastValid;
                    }
                    else
                    {
                        windowLastValid = wv;
                    }
                    windowBuf[j] = wv;
                }

                // Update the persistent value with the last valid seen in this window
                if (windowLastValid > 0)
                {
                    lastValidValue = windowLastValid;
                }

                output[i] = ComputeTheil(windowBuf[..windowLen]);
            }
        }
        finally
        {
            if (rentedWindow is not null)
            {
                ArrayPool<double>.Shared.Return(rentedWindow);
            }
        }
    }

    /// <summary>
    /// Computes Theil's T Index from a span of positive values.
    /// T = (1/n) × Σ (xᵢ/μ) × ln(xᵢ/μ)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ComputeTheil(ReadOnlySpan<double> values)
    {
        int n = values.Length;
        if (n < 2)
        {
            return 0;
        }

        // Compute mean of positive values
        double sum = 0;
        int validCount = 0;
        for (int i = 0; i < n; i++)
        {
            double v = values[i];
            if (v > 0)
            {
                sum += v;
                validCount++;
            }
        }

        if (validCount == 0 || sum <= 0)
        {
            return double.NaN;
        }

        double mean = sum / validCount;
        double invMean = 1.0 / mean;

        // Compute Theil T: (1/n) × Σ (xᵢ/μ) × ln(xᵢ/μ)
        double theilSum = 0;
        for (int i = 0; i < n; i++)
        {
            double v = values[i];
            if (v > 0)
            {
                double ratio = v * invMean;
                theilSum += ratio * Math.Log(ratio);
            }
        }

        return theilSum / validCount;
    }
}
