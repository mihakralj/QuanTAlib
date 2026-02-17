using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// PERCENTILE: Rolling Percentile
/// </summary>
/// <remarks>
/// Computes the value at a given percentile for a rolling window of data using
/// linear interpolation (PERCENTILE.INC / Excel method).
///
/// Calculation:
///   1. Maintain a sorted window of the last 'Period' values.
///   2. Compute rank = (p / 100) * (n - 1).
///   3. Interpolate between floor and ceil indices.
///
/// Properties:
///   - p=0 returns the minimum value in the window.
///   - p=50 returns the median (equivalent to Median indicator).
///   - p=100 returns the maximum value in the window.
///
/// Complexity:
///   Update: O(N) due to sorted buffer maintenance (BinarySearch + Array.Copy).
/// </remarks>
[SkipLocalsInit]
public sealed class Percentile : AbstractBase
{
    private readonly int _period;
    private readonly double _percent;
    private readonly RingBuffer _buffer;
    private readonly double[] _sortedBuffer;
    private readonly double[] _p_sortedBuffer;
    private readonly TValuePublishedHandler _handler;
    private readonly ITValuePublisher? _source;
    private double _lastValidValue;
    private double _p_lastValidValue;
    private int _p_sortedCount;
    private bool _disposed;

    /// <summary>Initializes a new Percentile indicator.</summary>
    /// <param name="period">The size of the rolling window (must be >= 1).</param>
    /// <param name="percent">The percentile to compute (0-100).</param>
    public Percentile(int period, double percent = 50.0)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be at least 1.", nameof(period));
        }
        if (percent < 0.0 || percent > 100.0)
        {
            throw new ArgumentException("Percent must be between 0 and 100.", nameof(percent));
        }

        _period = period;
        _percent = percent;
        _buffer = new RingBuffer(period);
        _sortedBuffer = new double[period];
        _p_sortedBuffer = new double[period];
        Name = $"Percentile({period},{percent})";
        WarmupPeriod = period;
        _handler = Handle;
    }

    public Percentile(ITValuePublisher source, int period, double percent = 50.0) : this(period, percent)
    {
        _source = source;
        source.Pub += _handler;
    }

    public Percentile(TSeries source, int period, double percent = 50.0) : this(period, percent)
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
            _p_sortedCount = _buffer.Count;
            Array.Copy(_sortedBuffer, _p_sortedBuffer, _p_sortedCount);

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
            // Restore sorted buffer from backup using saved count
            _lastValidValue = _p_lastValidValue;
            if (_p_sortedCount > 0)
            {
                Array.Copy(_p_sortedBuffer, _sortedBuffer, _p_sortedCount);
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
        double result = ComputePercentile(_sortedBuffer, count, _percent);

        Last = new TValue(input.Time, result);
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

        Batch(source.Values, vSpan, _period, _percent);
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

    /// <summary>Computes percentile via linear interpolation on a sorted span (PERCENTILE.INC method).</summary>
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

    /// <summary>Creates a batch Percentile series from source.</summary>
    public static TSeries Batch(TSeries source, int period, double percent = 50.0)
    {
        var indicator = new Percentile(period, percent);
        return indicator.Update(source);
    }

    /// <summary>Computes Percentile in-place over a span.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period, double percent = 50.0)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length.", nameof(output));
        }
        if (period < 1)
        {
            throw new ArgumentException("Period must be at least 1.", nameof(period));
        }
        if (percent < 0.0 || percent > 100.0)
        {
            throw new ArgumentException("Percent must be between 0 and 100.", nameof(percent));
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

                // NaN/Infinity guard
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

                output[i] = ComputePercentileSpan(sortedBuf, count, percent);
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(rentedSorted);
            ArrayPool<double>.Shared.Return(rentedWindow);
        }
    }

    public static (TSeries Results, Percentile Indicator) Calculate(TSeries source, int period, double percent = 50.0)
    {
        var indicator = new Percentile(period, percent);
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
