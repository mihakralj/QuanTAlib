using System.Collections;
using System.Runtime.CompilerServices;
using System.Numerics;

namespace QuanTAlib;

/// <summary>
/// Represents a circular buffer of double values with fixed capacity.
/// </summary>
/// <remarks>
/// This class provides efficient operations for adding, accessing, and manipulating
/// a fixed-size buffer of double values. It uses SIMD operations for improved performance
/// on supported hardware.
/// </remarks>
public class CircularBuffer : IEnumerable<double>
{
    private readonly double[] _buffer;
    private int _start = 0;
    private int _size = 0;

    /// <summary>
    /// Gets the maximum number of elements that can be contained in the buffer.
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// Gets the number of elements currently contained in the buffer.
    /// </summary>
    public int Count => _size;

    /// <summary>
    /// Initializes a new instance of the CircularBuffer class with the specified capacity.
    /// </summary>
    /// <param name="capacity">The maximum number of elements the buffer can hold.</param>
    public CircularBuffer(int capacity)
    {
        Capacity = capacity;
        _buffer = GC.AllocateArray<double>(capacity, pinned: true);
    }

    /// <summary>
    /// Adds an item to the buffer.
    /// </summary>
    /// <param name="item">The item to add to the buffer.</param>
    /// <param name="isNew">Indicates whether the item is a new value or an update to the last added value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(double item, bool isNew = true)
    {
        if (_size == 0 || isNew)
        {
            if (_size < Capacity)
            {
                _buffer[(_start + _size) % Capacity] = item;
                _size++;
            }
            else
            {
                _buffer[_start] = item;
                _start = (_start + 1) % Capacity;
            }
        }
        else
        {
            _buffer[(_start + _size - 1) % Capacity] = item;
        }
    }

    /// <summary>
    /// Gets or sets the element at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the element to get or set.</param>
    /// <returns>The element at the specified index.</returns>
    public double this[Index index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            int actualIndex = index.IsFromEnd ? _size - index.Value : index.Value;
            actualIndex = Math.Clamp(actualIndex, 0, _size - 1);
            return _buffer[(_start + actualIndex) % Capacity];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            int actualIndex = index.IsFromEnd ? _size - index.Value : index.Value;
            actualIndex = Math.Clamp(actualIndex, 0, _size - 1);
            _buffer[(_start + actualIndex) % Capacity] = value;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowArgumentOutOfRangeException(string paramName)
    {
        throw new ArgumentOutOfRangeException(paramName, "Index is out of range.");
    }

    /// <summary>
    /// Gets the newest (most recently added) element in the buffer.
    /// </summary>
    /// <returns>The newest element in the buffer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Newest()
    {
        if (_size == 0)
            return 0;
        return _buffer[(_start + _size - 1) % Capacity];
    }

    /// <summary>
    /// Gets the oldest element in the buffer.
    /// </summary>
    /// <returns>The oldest element in the buffer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Oldest()
    {
        if (_size == 0)
            ThrowInvalidOperationException();
        return _buffer[_start];
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowInvalidOperationException()
    {
        throw new InvalidOperationException("Buffer is empty.");
    }

    /// <summary>
    /// Returns an enumerator that iterates through the buffer.
    /// </summary>
    /// <returns>An enumerator for the buffer.</returns>
    public Enumerator GetEnumerator() => new(this);
    IEnumerator<double> IEnumerable<double>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Represents an enumerator for the CircularBuffer.
    /// </summary>
    public struct Enumerator : IEnumerator<double>
    {
        private readonly CircularBuffer _buffer;
        private int _index;
        private double _current;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(CircularBuffer buffer)
        {
            _buffer = buffer;
            _index = -1;
            _current = default;
        }

        /// <summary>
        /// Advances the enumerator to the next element of the buffer.
        /// </summary>
        /// <returns>true if the enumerator was successfully advanced to the next element; false if the enumerator has passed the end of the collection.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (_index + 1 >= _buffer._size)
                return false;

            _index++;
            _current = _buffer[_index];
            return true;
        }

        /// <summary>
        /// Gets the element in the buffer at the current position of the enumerator.
        /// </summary>
        public double Current => _current;
        object IEnumerator.Current => Current;

        /// <summary>
        /// Sets the enumerator to its initial position, which is before the first element in the buffer.
        /// </summary>
        public void Reset()
        {
            _index = -1;
            _current = default;
        }

        /// <summary>
        /// Disposes the enumerator.
        /// </summary>
        public void Dispose() { }
    }

    /// <summary>
    /// Copies the elements of the buffer to an array, starting at a particular array index.
    /// </summary>
    /// <param name="destination">The one-dimensional array that is the destination of the elements copied from the buffer.</param>
    /// <param name="destinationIndex">The zero-based index in array at which copying begins.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(double[] destination, int destinationIndex)
    {
        if (_size == 0)
            return;

        if (_start + _size <= Capacity)
        {
            Array.Copy(_buffer, _start, destination, destinationIndex, _size);
        }
        else
        {
            int firstPartLength = Capacity - _start;
            Array.Copy(_buffer, _start, destination, destinationIndex, firstPartLength);
            Array.Copy(_buffer, 0, destination, destinationIndex + firstPartLength, _size - firstPartLength);
        }
    }

    /// <summary>
    /// Returns a read-only span over the contents of the buffer.
    /// </summary>
    /// <returns>A read-only span over the buffer contents.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<double> GetSpan()
    {
        if (_size == 0)
            return ReadOnlySpan<double>.Empty;

        if (_start + _size <= Capacity)
        {
            return new ReadOnlySpan<double>(_buffer, _start, _size);
        }

        return new ReadOnlySpan<double>(ToArray());
    }

    /// <summary>
    /// Gets the internal buffer array.
    /// </summary>
    public double[] InternalBuffer => _buffer;

    /// <summary>
    /// Returns a read-only span over the entire internal buffer.
    /// </summary>
    /// <returns>A read-only span over the entire internal buffer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<double> GetInternalSpan() => _buffer.AsSpan();

    /// <summary>
    /// Removes all elements from the buffer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        Array.Clear(_buffer, 0, _buffer.Length);
        _start = 0;
        _size = 0;
    }

    /// <summary>
    /// Returns the maximum value in the buffer.
    /// </summary>
    /// <returns>The maximum value in the buffer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Max()
    {
        if (_size == 0)
            ThrowInvalidOperationException();

        return MaxSimd();
    }

    /// <summary>
    /// Returns the minimum value in the buffer.
    /// </summary>
    /// <returns>The minimum value in the buffer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Min()
    {
        if (_size == 0)
            ThrowInvalidOperationException();

        return MinSimd();
    }

    /// <summary>
    /// Computes the sum of all values in the buffer.
    /// </summary>
    /// <returns>The sum of all values in the buffer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Sum()
    {
        return SumSimd();
    }

    /// <summary>
    /// Computes the average of all values in the buffer.
    /// </summary>
    /// <returns>The average of all values in the buffer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Average()
    {
        if (_size == 0)
            ThrowInvalidOperationException();

        return SumSimd() / _size;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double MaxSimd()
    {
        var span = GetSpan();
        var vectorSize = Vector<double>.Count;
        var maxVector = new Vector<double>(double.MinValue);

        int i = 0;
        for (; i <= span.Length - vectorSize; i += vectorSize)
        {
            maxVector = Vector.Max(maxVector, new Vector<double>(span.Slice(i, vectorSize)));
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double MinSimd()
    {
        var span = GetSpan();
        var vectorSize = Vector<double>.Count;
        var minVector = new Vector<double>(double.MaxValue);

        int i = 0;
        for (; i <= span.Length - vectorSize; i += vectorSize)
        {
            minVector = Vector.Min(minVector, new Vector<double>(span.Slice(i, vectorSize)));
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double SumSimd()
    {
        var span = GetSpan();
        var vectorSize = Vector<double>.Count;
        var sumVector = Vector<double>.Zero;

        int i = 0;
        for (; i <= span.Length - vectorSize; i += vectorSize)
        {
            sumVector += new Vector<double>(span.Slice(i, vectorSize));
        }

        double sum = 0;
        for (int j = 0; j < vectorSize; j++)
        {
            sum += sumVector[j];
        }

        for (; i < span.Length; i++)
        {
            sum += span[i];
        }

        return sum;
    }

    /// <summary>
    /// Copies the buffer elements to a new array.
    /// </summary>
    /// <returns>An array containing copies of the buffer elements.</returns>
    public double[] ToArray()
    {
        double[] array = new double[_size];
        CopyTo(array, 0);
        return array;
    }

    /// <summary>
    /// Performs a parallel operation on the buffer elements.
    /// </summary>
    /// <param name="operation">The operation to perform on each partition of the buffer.</param>
    public void ParallelOperation(Func<double[], int, int, double> operation)
    {
        const int MinimumPartitionSize = 1024;

        if (_size < MinimumPartitionSize)
        {
            var span = GetSpan();
            var array = span.ToArray();
            operation(array, 0, array.Length);
            return;
        }

        int partitionCount = Environment.ProcessorCount;
        int partitionSize = _size / partitionCount;

        if (partitionSize < MinimumPartitionSize)
        {
            partitionCount = Math.Max(1, _size / MinimumPartitionSize);
            partitionSize = _size / partitionCount;
        }

        var buffer = ToArray();
        var results = new double[partitionCount];

        Parallel.For(0, partitionCount, i =>
        {
            int start = i * partitionSize;
            int length = (i == partitionCount - 1) ? _size - start : partitionSize;
            results[i] = operation(buffer, start, length);
        });
    }
}