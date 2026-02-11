using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Median: Rolling Median
/// </summary>
/// <remarks>
/// The Median is the middle value of a sorted dataset. It is a robust measure of central tendency,
/// less affected by outliers than the Mean (SMA).
///
/// Calculation:
/// 1. Maintain a sorted list of the last 'Period' values.
/// 2. If Period is odd, Median = Middle Value.
/// 3. If Period is even, Median = Average of the two Middle Values.
///
/// Complexity:
/// Update: O(N) due to maintaining sorted structure (BinarySearch + Array.Copy).
/// This is significantly faster than O(N log N) full sort for each update.
/// </remarks>
[SkipLocalsInit]
public sealed class Median : AbstractBase
{
    private readonly int _period;
    private readonly RingBuffer _buffer;
    private readonly double[] _sortedBuffer;
    private readonly double[] _p_sortedBuffer;
    private readonly TValuePublishedHandler _handler;
    private readonly ITValuePublisher? _source;
    private bool _disposed;

    /// <summary>
    /// Creates a Median indicator with the specified period.
    /// </summary>
    /// <param name="period">The size of the rolling window (must be > 0).</param>
    public Median(int period)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _period = period;
        _buffer = new RingBuffer(period);
        _sortedBuffer = new double[period];
        _p_sortedBuffer = new double[period];
        Name = $"Median({period})";
        WarmupPeriod = period;
        _handler = Handle;
    }

    public Median(ITValuePublisher source, int period) : this(period)
    {
        _source = source;
        source.Pub += _handler;
    }

    public Median(TSeries source, int period) : this(period)
    {
        Prime(source.Values);
        if (source.Count > 0)
        {
            Last = new TValue(source.LastTime, Last.Value);
        }
        _source = source;
        source.Pub += _handler;
    }

    /// <summary>
    /// True if the buffer is full.
    /// </summary>
    public override bool IsHot => _buffer.IsFull;

    /// <summary>
    /// Initializes the indicator state using the provided history.
    /// </summary>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        if (source.Length == 0)
        {
            return;
        }

        _buffer.Clear();
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
        if (isNew)
        {
            // Save sorted buffer state for potential rollback
            Array.Copy(_sortedBuffer, _p_sortedBuffer, _buffer.Count);

            if (_buffer.IsFull)
            {
                double old = _buffer.Oldest;
                RemoveFromSorted(old);
            }
            _buffer.Add(input.Value);
            AddToSorted(input.Value);
        }
        else
        {
            // Restore sorted buffer from backup before mutation
            int prevCount = _buffer.Count;
            if (prevCount > 0)
            {
                Array.Copy(_p_sortedBuffer, _sortedBuffer, prevCount);
            }

            if (_buffer.Count > 0)
            {
                double current = _buffer.Newest;
                RemoveFromSorted(current);         // Logically reduces sorted count by 1
                _buffer.UpdateNewest(input.Value); // Count unchanged
                AddToSorted(input.Value);          // Searches reduced space, re-expands to Count
            }
            else
            {
                _buffer.Add(input.Value);
                AddToSorted(input.Value);
            }
        }

        double median;
        int count = _buffer.Count;
        if (count == 0)
        {
            median = double.NaN;
        }
        else
        {
            int mid = count / 2;
            median = (count % 2 != 0)
                ? _sortedBuffer[mid]
                : (_sortedBuffer[mid - 1] + _sortedBuffer[mid]) * 0.5;
        }

        Last = new TValue(input.Time, median);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddToSorted(double value)
    {
        // Invariant: _buffer has already added the new value
        // validCount = elements in sortedBuffer BEFORE insertion
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

    /// <summary>
    /// Removes a value from the sorted buffer.
    /// Note: For duplicate values, an arbitrary instance is removed.
    /// This is acceptable because duplicates are interchangeable for median calculation.
    /// </summary>
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

    /// <summary>
    /// Calculates Median for the entire series using a new instance.
    /// </summary>
    public static TSeries Batch(TSeries source, int period)
    {
        var median = new Median(period);
        return median.Update(source);
    }

    /// <summary>
    /// Calculates Median in-place.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        // Always use ArrayPool to avoid CS8353 stackalloc escape issues
        double[] rentedSorted = ArrayPool<double>.Shared.Rent(period);
        double[] rentedWindow = ArrayPool<double>.Shared.Rent(period);
        try
        {
            Span<double> sortedBuffer = rentedSorted.AsSpan(0, period);
            Span<double> window = rentedWindow.AsSpan(0, period);

            int windowIdx = 0;
            int count = 0;

            for (int i = 0; i < len; i++)
            {
                double val = source[i];

                if (count == period)
                {
                    double old = window[windowIdx];
                    int oldIndex = BinarySearchSpan(sortedBuffer, count, old);

                    // Only remove if value was found (should always be true in correct operation)
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

                int mid = count / 2;
                double median = (count % 2 != 0)
                    ? sortedBuffer[mid]
                    : (sortedBuffer[mid - 1] + sortedBuffer[mid]) * 0.5;

                output[i] = median;
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(rentedSorted);
            ArrayPool<double>.Shared.Return(rentedWindow);
        }
    }

    public static (TSeries Results, Median Indicator) Calculate(TSeries source, int period)
    {
        var indicator = new Median(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
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

    /// <summary>
    /// Resets the indicator state.
    /// </summary>
    public override void Reset()
    {
        _buffer.Clear();
        Array.Clear(_sortedBuffer);
        Array.Clear(_p_sortedBuffer);
        Last = default;
    }

    /// <summary>
    /// Disposes the indicator and unsubscribes from the source.
    /// </summary>
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