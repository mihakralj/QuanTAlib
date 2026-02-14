using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Entropy: Normalized Shannon entropy of a time series over a sliding window.
/// </summary>
/// <remarks>
/// Measures the randomness/predictability of price data using histogram-based
/// probability estimation. Output is normalized to [0, 1] where 0 indicates
/// a perfectly predictable (constant) series and 1 indicates maximum randomness
/// (uniform distribution across bins).
///
/// Algorithm: values are binned into a histogram based on their position within
/// the window's [min, max] range. Shannon entropy H = -Σ(pᵢ·ln(pᵢ)) is computed
/// from bin frequencies and normalized by ln(bins).
///
/// Bins = min(max(count, 2), 100) matching PineScript reference implementation.
/// Complexity: O(period) per update — histogram must be rebuilt when min/max shift.
/// </remarks>
[SkipLocalsInit]
public sealed class Entropy : AbstractBase
{
    private readonly int _period;
    private readonly RingBuffer _buffer;
    private double _lastValidValue;
    private const int MaxBins = 100;
    private const double Epsilon = 1e-10;

    public override bool IsHot => _buffer.IsFull;

    /// <summary>
    /// Creates a new Entropy indicator.
    /// </summary>
    /// <param name="period">The lookback period (must be >= 2).</param>
    public Entropy(int period)
    {
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 2 for Entropy.");
        }
        _period = period;
        _buffer = new RingBuffer(period);
        Name = $"Entropy({period})";
        WarmupPeriod = period;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        double value = input.Value;

        // NaN/Infinity guard: substitute last valid value
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
            _buffer.Add(value);
        }
        else
        {
            _buffer.UpdateNewest(value);
        }

        double entropy = ComputeEntropy(_buffer.GetSpan());

        Last = new TValue(input.Time, entropy);
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
        var entropy = new Entropy(period);
        return entropy.Update(source);
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

    public static (TSeries Results, Entropy Indicator) Calculate(TSeries source, int period)
    {
        var indicator = new Entropy(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateScalarCore(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        int len = source.Length;
        const int StackallocThreshold = 256;

        // Use a temporary buffer for the current window
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

        // MaxBins (100) always fits on stack — no rental needed
        scoped Span<int> freqBuf = stackalloc int[MaxBins];

        try
        {
            for (int i = 0; i < len; i++)
            {
                // Determine window range
                int windowStart = Math.Max(0, i - period + 1);
                int windowLen = i - windowStart + 1;

                // Copy window values with NaN substitution
                double windowLastValid = 0;
                for (int j = 0; j < windowLen; j++)
                {
                    double wv = source[windowStart + j];
                    if (!double.IsFinite(wv))
                    {
                        wv = windowLastValid;
                    }
                    else
                    {
                        windowLastValid = wv;
                    }
                    windowBuf[j] = wv;
                }

                output[i] = ComputeEntropyFromSpan(windowBuf[..windowLen], freqBuf);
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
    /// Computes normalized Shannon entropy from a span of values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ComputeEntropy(ReadOnlySpan<double> values)
    {
        Span<int> freq = stackalloc int[MaxBins];
        return ComputeEntropyFromSpan(values, freq);
    }

    /// <summary>
    /// Core entropy computation with caller-supplied frequency buffer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ComputeEntropyFromSpan(ReadOnlySpan<double> values, Span<int> freq)
    {
        int count = values.Length;
        if (count < 2)
        {
            return 0;
        }

        // Find min/max
        double min = values[0];
        double max = values[0];
        for (int i = 1; i < count; i++)
        {
            double v = values[i];
            if (v < min)
            {
                min = v;
            }

            if (v > max)
            {
                max = v;
            }
        }

        double range = max - min;
        if (range <= Epsilon)
        {
            return 0; // All values are effectively equal — zero entropy
        }

        // Bin count: min(max(count, 2), 100)
        int bins = Math.Min(Math.Max(count, 2), MaxBins);

        // Clear frequency buffer
        freq[..bins].Clear();

        // Build histogram
        double invRange = 1.0 / range;
        for (int i = 0; i < count; i++)
        {
            double normVal = (values[i] - min) * invRange;
            // Clamp to [0, 1-ε] then scale to bin index
            int bucket = (int)(Math.Min(Math.Max(normVal, 0.0), 1.0 - Epsilon) * bins);
            // Safety clamp
            bucket = Math.Max(0, Math.Min(bucket, bins - 1));
            freq[bucket]++;
        }

        // Compute Shannon entropy: H = -Σ(pᵢ·ln(pᵢ))
        double invCount = 1.0 / count;
        double h = 0;
        for (int i = 0; i < bins; i++)
        {
            int f = freq[i];
            if (f > 0)
            {
                double p = f * invCount;
                h -= p * Math.Log(p);
            }
        }

        // Normalize by max entropy: ln(bins)
        double maxEntropy = Math.Log(bins);
        return maxEntropy > Epsilon ? h / maxEntropy : 0;
    }
}
