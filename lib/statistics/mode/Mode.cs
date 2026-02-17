using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Mode: Rolling Statistical Mode
/// </summary>
/// <remarks>
/// The Mode is the most frequently occurring value in a dataset. It is the only measure
/// of central tendency that can be used with nominal (categorical) data.
///
/// Calculation:
/// 1. Maintain a sorted list of the last 'Period' values.
/// 2. Scan sorted list for the longest consecutive run of equal values.
/// 3. If no value appears more than once (and there are multiple distinct values), return NaN.
///
/// Complexity:
/// Update: O(N) due to maintaining sorted structure (BinarySearch + Array.Copy) + O(N) scan.
/// </remarks>
[SkipLocalsInit]
public sealed class Mode : AbstractBase
{
    private readonly int _period;
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
    /// Creates a Mode indicator with the specified period.
    /// </summary>
    /// <param name="period">The size of the rolling window (must be > 0).</param>
    public Mode(int period)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _period = period;
        _buffer = new RingBuffer(period);
        _sortedBuffer = new double[period];
        _p_sortedBuffer = new double[period];
        Name = $"Mode({period})";
        WarmupPeriod = period;
        _handler = Handle;
    }

    /// <summary>
    /// Creates a chained Mode indicator.
    /// </summary>
    public Mode(ITValuePublisher source, int period) : this(period)
    {
        _source = source;
        source.Pub += _handler;
    }

    /// <summary>
    /// Creates a Mode indicator primed from a TSeries source.
    /// </summary>
    public Mode(TSeries source, int period) : this(period)
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
        // NaN/Infinity guard: substitute last valid value
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
            // Save sorted buffer state for potential rollback
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

        double mode = FindModeFromSorted(_sortedBuffer, _buffer.Count);

        Last = new TValue(input.Time, mode);
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

    /// <summary>
    /// Calculates Mode for the entire series using a new instance.
    /// </summary>
    public static TSeries Batch(TSeries source, int period)
    {
        var mode = new Mode(period);
        return mode.Update(source);
    }

    /// <summary>
    /// Calculates Mode in-place using spans.
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

        double[] rentedSorted = ArrayPool<double>.Shared.Rent(period);
        double[] rentedWindow = ArrayPool<double>.Shared.Rent(period);
        try
        {
            Span<double> sortedBuffer = rentedSorted.AsSpan(0, period);
            Span<double> window = rentedWindow.AsSpan(0, period);
            sortedBuffer.Clear();
            window.Clear();

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

                output[i] = FindModeFromSortedSpan(sortedBuffer, count);
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(rentedSorted);
            ArrayPool<double>.Shared.Return(rentedWindow);
        }
    }

    public static (TSeries Results, Mode Indicator) Calculate(TSeries source, int period)
    {
        var indicator = new Mode(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    /// <summary>
    /// Finds the mode from a sorted array by scanning for the longest consecutive run.
    /// Returns NaN if no value appears more than once (and there are multiple distinct values).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double FindModeFromSorted(double[] sorted, int count)
    {
        if (count == 0)
        {
            return double.NaN;
        }

        if (count == 1)
        {
            return sorted[0];
        }

        double modeVal = sorted[0];
        int maxFreq = 1;
        int currentFreq = 1;
        int distinctCount = 1;

        for (int i = 1; i < count; i++)
        {
            // skipcq: CS-R1077 - Exact-equality required: mode detection counts identical values in a sorted array; epsilon would merge distinct prices
            if (sorted[i] == sorted[i - 1])
            {
                currentFreq++;
            }
            else
            {
                if (currentFreq > maxFreq)
                {
                    maxFreq = currentFreq;
                    modeVal = sorted[i - 1];
                }
                currentFreq = 1;
                distinctCount++;
            }
        }

        // Check the last run
        if (currentFreq > maxFreq)
        {
            maxFreq = currentFreq;
            modeVal = sorted[count - 1];
        }

        // No mode if all values unique and more than 1 distinct value
        if (maxFreq <= 1 && distinctCount > 1)
        {
            return double.NaN;
        }

        return modeVal;
    }

    /// <summary>
    /// Span-based mode finding for batch path.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double FindModeFromSortedSpan(Span<double> sorted, int count)
    {
        if (count == 0)
        {
            return double.NaN;
        }

        if (count == 1)
        {
            return sorted[0];
        }

        double modeVal = sorted[0];
        int maxFreq = 1;
        int currentFreq = 1;
        int distinctCount = 1;

        for (int i = 1; i < count; i++)
        {
            if (sorted[i] == sorted[i - 1])
            {
                currentFreq++;
            }
            else
            {
                if (currentFreq > maxFreq)
                {
                    maxFreq = currentFreq;
                    modeVal = sorted[i - 1];
                }
                currentFreq = 1;
                distinctCount++;
            }
        }

        if (currentFreq > maxFreq)
        {
            maxFreq = currentFreq;
            modeVal = sorted[count - 1];
        }

        if (maxFreq <= 1 && distinctCount > 1)
        {
            return double.NaN;
        }

        return modeVal;
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
