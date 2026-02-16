using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// IQR: Interquartile Range
/// </summary>
/// <remarks>
/// The Interquartile Range measures the spread of the middle 50% of data in a rolling window.
/// It equals Q3 (75th percentile) minus Q1 (25th percentile), providing a robust measure
/// of statistical dispersion that is resistant to outliers.
///
/// Calculation:
///   1. Maintain a sorted window of the last 'Period' values.
///   2. Compute Q1 (25th percentile) and Q3 (75th percentile) via linear interpolation.
///   3. IQR = Q3 - Q1.
///
/// Percentile interpolation (matching Pine/Excel PERCENTILE.INC):
///   rank = (p / 100) * (n - 1)
///   result = value[floor(rank)] + frac(rank) * (value[ceil(rank)] - value[floor(rank)])
///
/// Complexity:
///   Update: O(N) due to sorted buffer maintenance (BinarySearch + Array.Copy).
/// </remarks>
[SkipLocalsInit]
public sealed class Iqr : AbstractBase
{
    private readonly int _period;
    private readonly RingBuffer _buffer;
    private readonly double[] _sortedBuffer;
    private readonly double[] _p_sortedBuffer;
    private readonly TValuePublishedHandler _handler;
    private readonly ITValuePublisher? _source;
    private double _lastValidValue;
    private double _p_lastValidValue;
    private bool _disposed;

    /// <summary>Initializes a new IQR indicator with the specified period.</summary>
    /// <param name="period">The size of the rolling window (must be >= 2).</param>
    public Iqr(int period)
    {
        if (period < 2)
        {
            throw new ArgumentException("Period must be at least 2.", nameof(period));
        }

        _period = period;
        _buffer = new RingBuffer(period);
        _sortedBuffer = new double[period];
        _p_sortedBuffer = new double[period];
        Name = $"Iqr({period})";
        WarmupPeriod = period;
        _handler = Handle;
    }

    public Iqr(ITValuePublisher source, int period) : this(period)
    {
        _source = source;
        source.Pub += _handler;
    }

    public Iqr(TSeries source, int period) : this(period)
    {
        Prime(source.Values);
        if (source.Count > 0)
        {
            Last = new TValue(source.LastTime, Last.Value);
        }
        _source = source;
        source.Pub += _handler;
    }

    /// <summary>True when the buffer has reached full period length.</summary>
    public override bool IsHot => _buffer.IsFull;

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        if (source.Length == 0)
        {
            return;
        }

        _buffer.Clear();
        Array.Clear(_sortedBuffer);
        Array.Clear(_p_sortedBuffer);
        _lastValidValue = 0;
        _p_lastValidValue = 0;

        int warmupLength = Math.Min(source.Length, WarmupPeriod);
        int startIndex = source.Length - warmupLength;

        for (int i = startIndex; i < source.Length; i++)
        {
            Update(new TValue(DateTime.MinValue, source[i]));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs args) => Update(args.Value, args.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        double value = input.Value;

        // NaN/Infinity guard — substitute last valid
        if (!double.IsFinite(value))
        {
            value = _lastValidValue;
        }
        else
        {
            if (isNew)
            {
                _p_lastValidValue = _lastValidValue;
            }
            _lastValidValue = value;
        }

        if (isNew)
        {
            // Save sorted buffer state for rollback
            Array.Copy(_sortedBuffer, _p_sortedBuffer, _buffer.Count);

            if (_buffer.IsFull)
            {
                double old = _buffer.Oldest;
                RemoveFromSorted(old);
            }
            _buffer.Add(value);
            AddToSorted(value);
        }
        else
        {
            // Restore sorted buffer from backup before mutation
            _lastValidValue = _p_lastValidValue;
            int prevCount = _buffer.Count;
            if (prevCount > 0)
            {
                Array.Copy(_p_sortedBuffer, _sortedBuffer, prevCount);
            }

            if (_buffer.Count > 0)
            {
                double current = _buffer.Newest;
                RemoveFromSorted(current);
                _buffer.UpdateNewest(value);
                AddToSorted(value);
            }
            else
            {
                _buffer.Add(value);
                AddToSorted(value);
            }

            // Re-apply NaN guard for corrected value
            if (double.IsFinite(input.Value))
            {
                _lastValidValue = input.Value;
            }
        }

        int count = _buffer.Count;
        double iqr;

        if (count < 2)
        {
            iqr = 0.0;
        }
        else
        {
            double q1 = ComputePercentile(_sortedBuffer, count, 25.0);
            double q3 = ComputePercentile(_sortedBuffer, count, 75.0);
            iqr = q3 - q1;
        }

        Last = new TValue(input.Time, iqr);
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

        Prime(source.Values);

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    public override void Reset()
    {
        _buffer.Clear();
        Array.Clear(_sortedBuffer);
        Array.Clear(_p_sortedBuffer);
        _lastValidValue = 0;
        _p_lastValidValue = 0;
        Last = default;
    }

    /// <summary>Computes percentile via linear interpolation on a sorted span.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ComputePercentile(double[] sorted, int count, double p)
    {
        if (count == 1)
        {
            return sorted[0];
        }

        double rank = (p / 100.0) * (count - 1);
        int lo = (int)rank;
        int hi = lo + 1;

        if (hi >= count)
        {
            return sorted[count - 1];
        }

        double frac = rank - lo;
        // skipcq: CS-R1140 — FMA for interpolation precision
        return Math.FusedMultiplyAdd(frac, sorted[hi] - sorted[lo], sorted[lo]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddToSorted(double value)
    {
        int validCount = _buffer.Count - 1;
        int index = Array.BinarySearch(_sortedBuffer, 0, validCount, value);
        if (index < 0)
        {
            index = ~index;
        }

        if (index < validCount)
        {
            Array.Copy(_sortedBuffer, index, _sortedBuffer, index + 1, validCount - index);
        }
        _sortedBuffer[index] = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RemoveFromSorted(double value)
    {
        int validCount = _buffer.Count;
        int index = Array.BinarySearch(_sortedBuffer, 0, validCount, value);
        if (index < 0)
        {
            return;
        }

        if (index < validCount - 1)
        {
            Array.Copy(_sortedBuffer, index + 1, _sortedBuffer, index, validCount - 1 - index);
        }
    }

    /// <summary>Creates a batch IQR series from source.</summary>
    public static TSeries Batch(TSeries source, int period)
    {
        var iqr = new Iqr(period);
        return iqr.Update(source);
    }

    /// <summary>Computes IQR in-place over a span.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length.", nameof(output));
        }
        if (period < 2)
        {
            throw new ArgumentException("Period must be at least 2.", nameof(period));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        double[] rentedSorted = ArrayPool<double>.Shared.Rent(period);
        double[] rentedWindow = ArrayPool<double>.Shared.Rent(period);
        try
        {
            Span<double> sortedBuf = rentedSorted.AsSpan(0, period);
            Span<double> window = rentedWindow.AsSpan(0, period);
            sortedBuf.Clear();
            window.Clear();

            int windowIdx = 0;
            int count = 0;

            double lastValidValue = 0.0;

            for (int i = 0; i < len; i++)
            {
                double val = source[i];

                // NaN/Infinity guard — substitute last valid value
                if (!double.IsFinite(val))
                {
                    val = lastValidValue;
                }
                else
                {
                    lastValidValue = val;
                }

                if (count == period)
                {
                    double old = window[windowIdx];
                    int oldIndex = BinarySearchSpan(sortedBuf, count, old);
                    if (oldIndex >= 0)
                    {
                        if (oldIndex < count - 1)
                        {
                            sortedBuf.Slice(oldIndex + 1, count - 1 - oldIndex).CopyTo(sortedBuf.Slice(oldIndex));
                        }
                        count--;
                    }
                }

                window[windowIdx] = val;
                windowIdx = (windowIdx + 1) % period;

                int newIndex = BinarySearchSpan(sortedBuf, count, val);
                if (newIndex < 0)
                {
                    newIndex = ~newIndex;
                }

                if (newIndex < count)
                {
                    sortedBuf.Slice(newIndex, count - newIndex).CopyTo(sortedBuf.Slice(newIndex + 1));
                }
                sortedBuf[newIndex] = val;
                count++;

                if (count < 2)
                {
                    output[i] = 0.0;
                }
                else
                {
                    double q1 = ComputePercentileSpan(sortedBuf, count, 25.0);
                    double q3 = ComputePercentileSpan(sortedBuf, count, 75.0);
                    output[i] = q3 - q1;
                }
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(rentedSorted);
            ArrayPool<double>.Shared.Return(rentedWindow);
        }
    }

    public static (TSeries Results, Iqr Indicator) Calculate(TSeries source, int period)
    {
        var indicator = new Iqr(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ComputePercentileSpan(Span<double> sorted, int count, double p)
    {
        if (count == 1)
        {
            return sorted[0];
        }

        double rank = (p / 100.0) * (count - 1);
        int lo = (int)rank;
        int hi = lo + 1;

        if (hi >= count)
        {
            return sorted[count - 1];
        }

        double frac = rank - lo;
        return Math.FusedMultiplyAdd(frac, sorted[hi] - sorted[lo], sorted[lo]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int BinarySearchSpan(Span<double> span, int length, double value)
    {
        int lo = 0;
        int hi = length - 1;
        while (lo <= hi)
        {
            int mid = lo + ((hi - lo) >> 1);
            int cmp = span[mid].CompareTo(value);
            if (cmp == 0)
            {
                return mid;
            }

            if (cmp < 0)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }
        return ~lo;
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing && _source != null)
            {
                _source.Pub -= _handler;
            }
            _disposed = true;
        }
        base.Dispose(disposing);
    }
}
