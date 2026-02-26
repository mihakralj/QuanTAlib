using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Trim: Rolling Trimmed Mean Moving Average
/// </summary>
/// <remarks>
/// Sorts the lookback window, discards the lowest and highest trimPct% of values,
/// and returns the arithmetic mean of the remaining middle portion.
/// trimPct=0 → SMA, trimPct approaches 50 → Median.
///
/// Complexity per bar: O(N log N) sort + O(N) sum — unavoidable for exact order statistics.
/// Sorted buffer maintained incrementally via BinarySearch + Array.Copy to avoid full re-sort.
/// </remarks>
[SkipLocalsInit]
public sealed class Trim : AbstractBase
{
    private readonly int _period;
    private readonly double _trimPct;
    private readonly RingBuffer _buffer;
    private readonly double[] _sortedBuffer;
    private readonly double[] _p_sortedBuffer;
    private readonly TValuePublishedHandler _handler;
    private readonly ITValuePublisher? _source;
    private double _lastValidValue;
    private int _p_sortedCount;
    private bool _disposed;

    public override bool IsHot => _buffer.IsFull;

    /// <summary>
    /// Creates a Trim indicator with the specified period and trim percentage.
    /// </summary>
    /// <param name="period">The size of the rolling window (must be >= 3).</param>
    /// <param name="trimPct">Percentage of values to trim from each tail (0–49). Default 10.</param>
    public Trim(int period, double trimPct = 10.0)
    {
        if (period < 3)
        {
            throw new ArgumentException("Period must be >= 3", nameof(period));
        }

        if (trimPct < 0 || trimPct >= 50)
        {
            throw new ArgumentException("TrimPct must be in [0, 49]", nameof(trimPct));
        }

        _period = period;
        _trimPct = trimPct;
        _buffer = new RingBuffer(period);
        _sortedBuffer = new double[period];
        _p_sortedBuffer = new double[period];
        Name = $"Trim({period},{trimPct})";
        WarmupPeriod = period;
        _handler = Handle;
    }

    /// <summary>Creates a chained Trim indicator.</summary>
    public Trim(ITValuePublisher source, int period, double trimPct = 10.0) : this(period, trimPct)
    {
        _source = source;
        source.Pub += _handler;
    }

    /// <summary>Creates a Trim indicator primed from a TSeries source.</summary>
    public Trim(TSeries source, int period, double trimPct = 10.0) : this(period, trimPct)
    {
        Prime(source.Values);
        if (source.Count > 0)
        {
            Last = new TValue(source.LastTime, Last.Value);
        }

        _source = source;
        source.Pub += _handler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs args) => Update(args.Value, args.IsNew);

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
        }

        double result = ComputeTrimmedMean(_sortedBuffer, _buffer.Count, _trimPct);
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

        Batch(source.Values, vSpan, _period, _trimPct);
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
        Last = default;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        if (source.Length == 0)
        {
            return;
        }

        _buffer.Clear();
        Array.Clear(_sortedBuffer);
        int warmupLength = Math.Min(source.Length, WarmupPeriod);
        int startIndex = source.Length - warmupLength;

        for (int i = startIndex; i < source.Length; i++)
        {
            Update(new TValue(DateTime.MinValue, source[i]));
        }
    }

    /// <summary>Calculates Trim for the entire series using a new instance.</summary>
    public static TSeries Batch(TSeries source, int period, double trimPct = 10.0)
    {
        var trim = new Trim(period, trimPct);
        return trim.Update(source);
    }

    /// <summary>Calculates Trim in-place using spans.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period, double trimPct = 10.0)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (period < 3)
        {
            throw new ArgumentException("Period must be >= 3", nameof(period));
        }

        if (trimPct < 0 || trimPct >= 50)
        {
            throw new ArgumentException("TrimPct must be in [0, 49]", nameof(trimPct));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        const int StackallocThreshold = 256;
        double[]? rentedSorted = null;
        double[]? rentedWindow = null;
        scoped Span<double> sortedBuffer;
        scoped Span<double> window;

        if (period <= StackallocThreshold)
        {
            sortedBuffer = stackalloc double[period];
            window = stackalloc double[period];
        }
        else
        {
            rentedSorted = ArrayPool<double>.Shared.Rent(period);
            rentedWindow = ArrayPool<double>.Shared.Rent(period);
            sortedBuffer = rentedSorted.AsSpan(0, period);
            window = rentedWindow.AsSpan(0, period);
        }

        sortedBuffer.Clear();
        window.Clear();

        try
        {
            int windowIdx = 0;
            int count = 0;

            for (int i = 0; i < len; i++)
            {
                double val = source[i];

                if (count == period)
                {
                    double old = window[windowIdx];
                    int oldIndex = BinarySearchSpan(sortedBuffer, count, old);
                    if (oldIndex >= 0)
                    {
                        if (oldIndex < count - 1)
                        {
                            sortedBuffer.Slice(oldIndex + 1, count - 1 - oldIndex).CopyTo(sortedBuffer.Slice(oldIndex));
                        }

                        count--;
                    }
                }

                window[windowIdx] = val;
                windowIdx = (windowIdx + 1) % period;

                int newIndex = BinarySearchSpan(sortedBuffer, count, val);
                if (newIndex < 0)
                {
                    newIndex = ~newIndex;
                }

                if (newIndex < count)
                {
                    sortedBuffer.Slice(newIndex, count - newIndex).CopyTo(sortedBuffer.Slice(newIndex + 1));
                }

                sortedBuffer[newIndex] = val;
                count++;

                output[i] = ComputeTrimmedMeanSpan(sortedBuffer, count, trimPct);
            }
        }
        finally
        {
            if (rentedSorted != null)
            {
                ArrayPool<double>.Shared.Return(rentedSorted);
            }

            if (rentedWindow != null)
            {
                ArrayPool<double>.Shared.Return(rentedWindow);
            }
        }
    }

    public static (TSeries Results, Trim Indicator) Calculate(TSeries source, int period, double trimPct = 10.0)
    {
        var indicator = new Trim(period, trimPct);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ComputeTrimmedMean(double[] sorted, int count, double trimPct)
    {
        if (count == 0)
        {
            return double.NaN;
        }

        int trimCount = (int)(count * trimPct / 100.0);
        int keepCount = count - 2 * trimCount;

        if (keepCount < 1)
        {
            keepCount = 1;
            trimCount = (count - 1) / 2;
        }

        double sum = 0.0;
        int end = trimCount + keepCount;
        for (int i = trimCount; i < end; i++)
        {
            sum += sorted[i];
        }

        return sum / keepCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ComputeTrimmedMeanSpan(Span<double> sorted, int count, double trimPct)
    {
        if (count == 0)
        {
            return double.NaN;
        }

        int trimCount = (int)(count * trimPct / 100.0);
        int keepCount = count - 2 * trimCount;

        if (keepCount < 1)
        {
            keepCount = 1;
            trimCount = (count - 1) / 2;
        }

        double sum = 0.0;
        int end = trimCount + keepCount;
        for (int i = trimCount; i < end; i++)
        {
            sum += sorted[i];
        }

        return sum / keepCount;
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
