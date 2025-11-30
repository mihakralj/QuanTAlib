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
    private readonly int _capacity;
    private int _head;      // Next write position (also start position when full)
    private int _count;     // Current number of elements
    private double _sum;    // Running sum of all elements

    /// <summary>
    /// Creates a new RingBuffer with the specified capacity.
    /// Uses pinned memory for SIMD compatibility.
    /// </summary>
    /// <param name="capacity">Maximum number of elements (must be > 0)</param>
    public RingBuffer(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentException("Capacity must be greater than 0", nameof(capacity));

        _capacity = capacity;
        _buffer = GC.AllocateArray<double>(capacity, pinned: true);
        _head = 0;
        _count = 0;
        _sum = 0;
    }

    /// <summary>
    /// Maximum number of elements the buffer can hold.
    /// </summary>
    public int Capacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _capacity;
    }

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
        get => _count == _capacity;
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
    /// </summary>
    public double Newest
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (_count == 0) return 0;
            int idx = (_head - 1 + _capacity) % _capacity;
            return _buffer[idx];
        }
    }

    /// <summary>
    /// Gets the oldest value in the buffer.
    /// </summary>
    public double Oldest
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (_count == 0) return 0;
            // When full, _head points to oldest; otherwise start is 0
            int start = _count == _capacity ? _head : 0;
            return _buffer[start];
        }
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

        if (_count == _capacity)
        {
            // Buffer is full: remove oldest value from sum
            removed = _buffer[_head];
            _sum -= removed;
        }
        else
        {
            _count++;
        }

        _buffer[_head] = value;
        _sum += value;
        _head = (_head + 1) % _capacity;

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
        if (_count == 0) return;

        int idx = (_head - 1 + _capacity) % _capacity;
        double oldValue = _buffer[idx];
        _sum -= oldValue;
        _sum += value;
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
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)actualIndex, (uint)_count);

        int start = _count == _capacity ? _head : 0;
        int bufferIdx = (start + actualIndex) % _capacity;
        return _buffer[bufferIdx];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetAt(Index index, double value)
    {
        int actualIndex = index.IsFromEnd ? _count - index.Value : index.Value;
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)actualIndex, (uint)_count);

        int start = _count == _capacity ? _head : 0;
        int bufferIdx = (start + actualIndex) % _capacity;

        _sum -= _buffer[bufferIdx];
        _sum += value;
        _buffer[bufferIdx] = value;
    }

    /// <summary>
    /// Returns a span over the buffer contents.
    /// If buffer is contiguous, returns direct span (SIMD-friendly).
    /// If wrapped, returns span over a copy.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<double> GetSpan()
    {
        if (_count == 0) return ReadOnlySpan<double>.Empty;

        int start = _count == _capacity ? _head : 0;

        // Check if contiguous (no wrap)
        if (start + _count <= _capacity)
        {
            return new ReadOnlySpan<double>(_buffer, start, _count);
        }

        // Wrapped - need to copy
        return new ReadOnlySpan<double>(ToArray());
    }

    /// <summary>
    /// Returns a span over the entire internal buffer (for advanced SIMD use).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<double> GetInternalSpan() => _buffer.AsSpan();

    /// <summary>
    /// Returns the maximum value in the buffer using SIMD acceleration.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Max()
    {
        if (_count == 0) return double.NaN;
        return MaxSimd();
    }

    /// <summary>
    /// Returns the minimum value in the buffer using SIMD acceleration.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Min()
    {
        if (_count == 0) return double.NaN;
        return MinSimd();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private double MaxSimd()
    {
        var span = GetSpan();
        var vectorSize = Vector<double>.Count;
        var maxVector = new Vector<double>(double.MinValue);

        int i = 0;
        ref double spanRef = ref MemoryMarshal.GetReference(span);

        for (; i <= span.Length - vectorSize; i += vectorSize)
        {
            maxVector = Vector.Max(maxVector, Unsafe.As<double, Vector<double>>(ref Unsafe.Add(ref spanRef, i)));
        }

        double max = double.MinValue;
        for (int j = 0; j < vectorSize; j++)
        {
            max = Math.Max(max, maxVector[j]);
        }

        for (; i < span.Length; i++)
        {
            max = Math.Max(max, span[i]);
        }

        return max;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private double MinSimd()
    {
        var span = GetSpan();
        var vectorSize = Vector<double>.Count;
        var minVector = new Vector<double>(double.MaxValue);

        int i = 0;
        ref double spanRef = ref MemoryMarshal.GetReference(span);

        for (; i <= span.Length - vectorSize; i += vectorSize)
        {
            minVector = Vector.Min(minVector, Unsafe.As<double, Vector<double>>(ref Unsafe.Add(ref spanRef, i)));
        }

        double min = double.MaxValue;
        for (int j = 0; j < vectorSize; j++)
        {
            min = Math.Min(min, minVector[j]);
        }

        for (; i < span.Length; i++)
        {
            min = Math.Min(min, span[i]);
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
        if (_count == 0) return Array.Empty<double>();

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
        if (_count == 0) return;

        int start = _count == _capacity ? _head : 0;

        if (start + _count <= _capacity)
        {
            Array.Copy(_buffer, start, destination, destinationIndex, _count);
        }
        else
        {
            int firstPartLength = _capacity - start;
            Array.Copy(_buffer, start, destination, destinationIndex, firstPartLength);
            Array.Copy(_buffer, 0, destination, destinationIndex + firstPartLength, _count - firstPartLength);
        }
    }

    /// <summary>
    /// Creates a copy of the current state for bar correction support.
    /// </summary>
    public RingBuffer Clone()
    {
        var clone = new RingBuffer(_capacity);
        Array.Copy(_buffer, clone._buffer, _capacity);
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
        if (source._capacity != _capacity)
            throw new ArgumentException("Source buffer must have same capacity", nameof(source));

        Array.Copy(source._buffer, _buffer, _capacity);
        _head = source._head;
        _count = source._count;
        _sum = source._sum;
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
    public struct Enumerator : IEnumerator<double>
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
            _start = buffer._count == buffer._capacity ? buffer._head : 0;
            _index = -1;
            _current = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (_index + 1 >= _count)
                return false;

            _index++;
            int bufferIdx = (_start + _index) % _buffer._capacity;
            _current = _buffer._buffer[bufferIdx];
            return true;
        }

        public double Current => _current;
        object IEnumerator.Current => Current;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            _index = -1;
            _current = default;
        }

        public void Dispose() { }
    }
}
