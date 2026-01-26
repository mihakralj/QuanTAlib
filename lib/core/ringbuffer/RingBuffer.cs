using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// A high-performance circular buffer for double values optimized for SIMD operations.
/// Uses pinned memory and maintains running sum for O(1) average calculations.
/// </summary>
/// <remarks>
/// Key characteristics:
/// - Fixed capacity set at construction
/// - Pinned memory for SIMD compatibility
/// - O(1) Add and Sum operations via running sum
/// - SIMD-accelerated Min/Max operations
/// - Direct span access when buffer is contiguous
/// - Thread-unsafe for maximum performance
/// </remarks>
[SkipLocalsInit]
public sealed class RingBuffer : IEnumerable<double>
{
    private readonly double[] _buffer;
    private int _head;
    private int _count;
    private double _sum;

    private int _savedHead;
    private int _savedCount;
    private double _savedSum;
    private double _savedValue;

    /// <summary>
    /// Immutable snapshot token for multi-buffer scenarios.
    /// Allows capturing and restoring buffer state without using the built-in single snapshot.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly record struct SnapshotToken(int Head, int Count, double Sum, double Value);

    /// <summary>
    /// Creates a new RingBuffer with the specified capacity.
    /// Uses pinned memory for SIMD compatibility.
    /// </summary>
    /// <param name="capacity">Maximum number of elements (must be > 0)</param>
    public RingBuffer(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentException("Capacity must be greater than 0", nameof(capacity));
        }

        Capacity = capacity;
        _buffer = GC.AllocateArray<double>(capacity, pinned: true);
        _head = 0;
        _count = 0;
        _sum = 0;
    }

    /// <summary>
    /// Maximum number of elements the buffer can hold.
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// Current number of elements in the buffer.
    /// </summary>
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count;
    }

    /// <summary>
    /// True if the buffer is full (Count == Capacity).
    /// </summary>
    public bool IsFull
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count == Capacity;
    }

    /// <summary>
    /// Running sum of all elements in the buffer.
    /// O(1) operation using maintained running sum.
    /// </summary>
    public double Sum
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _sum;
    }

    /// <summary>
    /// Recalculates the sum by iterating over all elements.
    /// Useful for correcting floating-point drift after many updates.
    /// Uses GetSequencedSpans to avoid allocation when buffer wraps.
    /// </summary>
    public double RecalculateSum()
    {
        double sum = 0;
        GetSequencedSpans(out var first, out var second);

        for (int i = 0; i < first.Length; i++)
        {
            sum += first[i];
        }
        for (int i = 0; i < second.Length; i++)
        {
            sum += second[i];
        }

        _sum = sum;
        return sum;
    }

    /// <summary>
    /// Average of all elements in the buffer.
    /// Returns 0 if buffer is empty.
    /// </summary>
    public double Average
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count > 0 ? _sum / _count : 0;
    }

    /// <summary>
    /// Gets the newest (most recently added) value.
    /// Returns double.NaN if buffer is empty.
    /// </summary>
    public double Newest
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (_count == 0)
            {
                return double.NaN;
            }

            int idx = (_head - 1 + Capacity) % Capacity;
            return _buffer[idx];
        }
    }

    /// <summary>
    /// Gets the oldest value in the buffer.
    /// Returns double.NaN if buffer is empty.
    /// </summary>
    public double Oldest
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (_count == 0)
            {
                return double.NaN;
            }

            int start = _count == Capacity ? _head : 0;
            return _buffer[start];
        }
    }

    /// <summary>
    /// Gets the index in the internal buffer where the oldest element is located.
    /// </summary>
    public int StartIndex
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count == Capacity ? _head : 0;
    }

    /// <summary>
    /// Gets a read-only span over the internal buffer array for direct SIMD access.
    /// </summary>
    public ReadOnlySpan<double> InternalBuffer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _buffer.AsSpan();
    }

    /// <summary>
    /// Adds a value to the buffer.
    /// If full, the oldest value is overwritten and its value is subtracted from the sum.
    /// Returns the value that was removed (0 if buffer was not full).
    /// </summary>
    /// <param name="value">Value to add</param>
    /// <returns>The removed oldest value, or 0 if buffer was not full</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Add(double value)
    {
        double removed = 0;

        if (_count == Capacity)
        {
            removed = _buffer[_head];
            _sum = Math.FusedMultiplyAdd(-1.0, removed, _sum + value);
        }
        else
        {
            _count++;
            _sum += value;
        }

        _buffer[_head] = value;
        _head = (_head + 1) % Capacity;

        return removed;
    }

    /// <summary>
    /// Adds a value with support for bar correction semantics.
    /// </summary>
    /// <param name="value">Value to add</param>
    /// <param name="isNew">True for new bar, false for update to current bar</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(double value, bool isNew)
    {
        if (isNew || _count == 0)
        {
            Add(value);
        }
        else
        {
            UpdateNewest(value);
        }
    }

    /// <summary>
    /// Updates the newest (most recently added) value.
    /// This is used for bar correction (isNew=false semantics).
    /// </summary>
    /// <param name="value">New value to replace the newest</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UpdateNewest(double value)
    {
        if (_count == 0)
        {
            return;
        }

        int idx = (_head - 1 + Capacity) % Capacity;
        double oldValue = _buffer[idx];
        _sum = Math.FusedMultiplyAdd(-1.0, oldValue, _sum + value);
        _buffer[idx] = value;
    }

    /// <summary>
    /// Gets or sets element at the specified index (0 = oldest, Count-1 = newest).
    /// Supports negative indexing via Index type.
    /// </summary>
    public double this[Index index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => GetAt(index);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => SetAt(index, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetAt(Index index)
    {
        int actualIndex = index.IsFromEnd ? _count - index.Value : index.Value;
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)actualIndex, (uint)_count, nameof(index));

        int start = _count == Capacity ? _head : 0;
        int bufferIdx = (start + actualIndex) % Capacity;
        return _buffer[bufferIdx];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetAt(Index index, double value)
    {
        int actualIndex = index.IsFromEnd ? _count - index.Value : index.Value;
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)actualIndex, (uint)_count, nameof(index));

        int start = _count == Capacity ? _head : 0;
        int bufferIdx = (start + actualIndex) % Capacity;

        double oldValue = _buffer[bufferIdx];
        _sum = Math.FusedMultiplyAdd(-1.0, oldValue, _sum + value);
        _buffer[bufferIdx] = value;
    }

    /// <summary>
    /// Returns a span over the buffer contents.
    /// If buffer is contiguous, returns direct span (SIMD-friendly).
    /// If wrapped, returns span over a copy.
    /// </summary>
    /// <remarks>
    /// <para><b>Ãƒâ€šÃ‚Â  Allocation Warning:</b> When the buffer wraps around (i.e., when data spans
    /// from the end of the internal array back to the beginning), this method allocates a new
    /// array via <see cref="ToArray"/> to return contiguous data. For allocation-free iteration
    /// over wrapped buffers, use <see cref="GetSequencedSpans"/> instead.</para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<double> GetSpan()
    {
        if (_count == 0)
        {
            return ReadOnlySpan<double>.Empty;
        }

        int start = _count == Capacity ? _head : 0;

        if (start + _count <= Capacity)
        {
            return new ReadOnlySpan<double>(_buffer, start, _count);
        }

        return new ReadOnlySpan<double>(ToArray());
    }

    /// <summary>
    /// Returns a span over the entire internal buffer (for advanced SIMD use).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<double> GetInternalSpan() => _buffer.AsSpan();

    /// <summary>
    /// Gets the two sequential spans that make up the buffer contents in chronological order (Oldest to Newest).
    /// <param name="first">The first segment of data.</param>
    /// <param name="second">The second segment of data (empty if buffer is contiguous).</param>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetSequencedSpans(out ReadOnlySpan<double> first, out ReadOnlySpan<double> second)
    {
        if (_count == 0)
        {
            first = default;
            second = default;
            return;
        }

        int start = _count == Capacity ? _head : 0;
        int firstLen = Math.Min(_count, Capacity - start);

        first = new ReadOnlySpan<double>(_buffer, start, firstLen);

        second = _count > firstLen
            ? new ReadOnlySpan<double>(_buffer, 0, _count - firstLen)
            : default;
    }

    /// <summary>
    /// Returns the maximum value in the buffer using SIMD acceleration.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Max()
    {
        if (_count == 0)
        {
            return double.NaN;
        }

        return MaxSimd();
    }

    /// <summary>
    /// Returns the minimum value in the buffer using SIMD acceleration.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Min()
    {
        if (_count == 0)
        {
            return double.NaN;
        }

        return MinSimd();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private double MaxSimd()
    {
        GetSequencedSpans(out var first, out var second);
        var vectorSize = Vector<double>.Count;
        var maxVector = new Vector<double>(double.MinValue);
        double max = double.MinValue;

        // Process first span with SIMD
        int i = 0;
        if (first.Length >= vectorSize)
        {
            ref double firstRef = ref MemoryMarshal.GetReference(first);
            for (; i <= first.Length - vectorSize; i += vectorSize)
            {
                maxVector = Vector.Max(maxVector, Unsafe.As<double, Vector<double>>(ref Unsafe.Add(ref firstRef, i)));
            }
        }
        // Scalar remainder of first span
        for (; i < first.Length; i++)
        {
            max = Math.Max(max, first[i]);
        }

        // Process second span with SIMD (if wrapped)
        i = 0;
        if (second.Length >= vectorSize)
        {
            ref double secondRef = ref MemoryMarshal.GetReference(second);
            for (; i <= second.Length - vectorSize; i += vectorSize)
            {
                maxVector = Vector.Max(maxVector, Unsafe.As<double, Vector<double>>(ref Unsafe.Add(ref secondRef, i)));
            }
        }
        // Scalar remainder of second span
        for (; i < second.Length; i++)
        {
            max = Math.Max(max, second[i]);
        }

        // Reduce vector to scalar
        for (int j = 0; j < vectorSize; j++)
        {
            max = Math.Max(max, maxVector[j]);
        }

        return max;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private double MinSimd()
    {
        GetSequencedSpans(out var first, out var second);
        var vectorSize = Vector<double>.Count;
        var minVector = new Vector<double>(double.MaxValue);
        double min = double.MaxValue;

        // Process first span with SIMD
        int i = 0;
        if (first.Length >= vectorSize)
        {
            ref double firstRef = ref MemoryMarshal.GetReference(first);
            for (; i <= first.Length - vectorSize; i += vectorSize)
            {
                minVector = Vector.Min(minVector, Unsafe.As<double, Vector<double>>(ref Unsafe.Add(ref firstRef, i)));
            }
        }
        // Scalar remainder of first span
        for (; i < first.Length; i++)
        {
            min = Math.Min(min, first[i]);
        }

        // Process second span with SIMD (if wrapped)
        i = 0;
        if (second.Length >= vectorSize)
        {
            ref double secondRef = ref MemoryMarshal.GetReference(second);
            for (; i <= second.Length - vectorSize; i += vectorSize)
            {
                minVector = Vector.Min(minVector, Unsafe.As<double, Vector<double>>(ref Unsafe.Add(ref secondRef, i)));
            }
        }
        // Scalar remainder of second span
        for (; i < second.Length; i++)
        {
            min = Math.Min(min, second[i]);
        }

        // Reduce vector to scalar
        for (int j = 0; j < vectorSize; j++)
        {
            min = Math.Min(min, minVector[j]);
        }

        return min;
    }

    /// <summary>
    /// Clears all elements from the buffer.
    /// </summary>
    public void Clear()
    {
        Array.Clear(_buffer, 0, _buffer.Length);
        _head = 0;
        _count = 0;
        _sum = 0;
    }

    /// <summary>
    /// Copies the buffer elements to a new array in chronological order.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double[] ToArray()
    {
        if (_count == 0)
        {
            return Array.Empty<double>();
        }

        double[] array = new double[_count];
        CopyTo(array, 0);
        return array;
    }

    /// <summary>
    /// Copies elements to destination array starting at destinationIndex.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(double[] destination, int destinationIndex)
    {
        if (_count == 0)
        {
            return;
        }

        int start = _count == Capacity ? _head : 0;

        if (start + _count <= Capacity)
        {
            Array.Copy(_buffer, start, destination, destinationIndex, _count);
        }
        else
        {
            int firstPartLength = Capacity - start;
            Array.Copy(_buffer, start, destination, destinationIndex, firstPartLength);
            Array.Copy(_buffer, 0, destination, destinationIndex + firstPartLength, _count - firstPartLength);
        }
    }

    /// <summary>
    /// Copies elements to a destination span in chronological order.
    /// Destination must have at least Count elements.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(Span<double> destination)
    {
        if (_count == 0)
        {
            return;
        }

        int start = _count == Capacity ? _head : 0;

        if (start + _count <= Capacity)
        {
            _buffer.AsSpan(start, _count).CopyTo(destination);
        }
        else
        {
            int firstPartLength = Capacity - start;
            _buffer.AsSpan(start, firstPartLength).CopyTo(destination);
            _buffer.AsSpan(0, _count - firstPartLength).CopyTo(destination.Slice(firstPartLength));
        }
    }

    /// <summary>
    /// Creates a copy of the current state for bar correction support.
    /// </summary>
    public RingBuffer Clone()
    {
        var clone = new RingBuffer(Capacity);
        Array.Copy(_buffer, clone._buffer, Capacity);
        clone._head = _head;
        clone._count = _count;
        clone._sum = _sum;
        return clone;
    }

    /// <summary>
    /// Copies state from another RingBuffer.
    /// Both buffers must have the same capacity.
    /// </summary>
    public void CopyFrom(RingBuffer source)
    {
        if (source.Capacity != Capacity)
        {
            throw new ArgumentException("Source buffer must have same capacity", nameof(source));
        }

        Array.Copy(source._buffer, _buffer, Capacity);
        _head = source._head;
        _count = source._count;
        _sum = source._sum;
    }

    /// <summary>
    /// Captures the current state of the buffer.
    /// Must be called BEFORE adding a new value if you intend to Restore later.
    /// Saves the value at _head position (which will be overwritten by the next Add).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Snapshot()
    {
        _savedHead = _head;
        _savedCount = _count;
        _savedSum = _sum;
        // Save the value that will be overwritten by the next Add()
        // When buffer is full, Add() will overwrite _buffer[_head] (the oldest value)
        // When buffer is not full, _buffer[_head] is undefined but we save it anyway
        _savedValue = _buffer[_head];
    }

    /// <summary>
    /// Restores the buffer to the state captured by Snapshot.
    /// This restores the buffer to its state before the last Add() operation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Restore()
    {
        _head = _savedHead;
        _count = _savedCount;
        _sum = _savedSum;
        // Restore the value at _head position that was saved before the Add()
        _buffer[_head] = _savedValue;
    }

    /// <summary>
    /// Creates a snapshot token that captures the current buffer state.
    /// Use this for multi-buffer scenarios where you need to snapshot multiple buffers atomically.
    /// Must be called BEFORE adding a new value if you intend to restore later.
    /// </summary>
    /// <returns>An immutable token containing the buffer state.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SnapshotToken GetSnapshot()
    {
        return new SnapshotToken(_head, _count, _sum, _buffer[_head]);
    }

    /// <summary>
    /// Restores the buffer to the state captured in the provided snapshot token.
    /// Use this for multi-buffer scenarios where you need to restore multiple buffers atomically.
    /// </summary>
    /// <param name="token">The snapshot token to restore from.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RestoreSnapshot(SnapshotToken token)
    {
        _head = token.Head;
        _count = token.Count;
        _sum = token.Sum;
        _buffer[_head] = token.Value;
    }

    /// <summary>
    /// Returns an enumerator that iterates through the buffer in chronological order.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(this);

    IEnumerator<double> IEnumerable<double>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// High-performance enumerator for the RingBuffer.
    /// </summary>
    public struct Enumerator : IEnumerator<double>, IEquatable<Enumerator>
    {
        private readonly RingBuffer _buffer;
        private readonly int _start;
        private readonly int _count;
        private int _index;
        private double _current;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(RingBuffer buffer)
        {
            _buffer = buffer;
            _count = buffer._count;
            _start = buffer._count == buffer.Capacity ? buffer._head : 0;
            _index = -1;
            _current = 0.0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (_index + 1 >= _count)
            {
                return false;
            }

            _index++;
            int bufferIdx = (_start + _index) % _buffer.Capacity;
            _current = _buffer._buffer[bufferIdx];
            return true;
        }

        public readonly double Current => _current;
        readonly object IEnumerator.Current => Current;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            _index = -1;
            _current = 0.0;
        }

        public readonly void Dispose() { }

        public readonly bool Equals(Enumerator other) =>
            ReferenceEquals(_buffer, other._buffer) &&
            _start == other._start &&
            _count == other._count &&
            _index == other._index &&
            _current.Equals(other._current);

        public readonly override bool Equals(object? obj) =>
            obj is Enumerator other && Equals(other);

        public readonly override int GetHashCode() =>
            HashCode.Combine(RuntimeHelpers.GetHashCode(_buffer), _start, _count, _index, _current);

        public static bool operator ==(Enumerator left, Enumerator right) => left.Equals(right);
        public static bool operator !=(Enumerator left, Enumerator right) => !left.Equals(right);
    }
}
