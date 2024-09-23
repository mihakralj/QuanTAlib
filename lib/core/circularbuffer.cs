using System.Collections;
using System.Runtime.CompilerServices;
using System.Numerics;

namespace QuanTAlib;

public class CircularBuffer : IEnumerable<double>
{
    private readonly double[] _buffer;
    private int _start = 0;
    private int _size = 0;

    public int Capacity { get; }
    public int Count => _size;

    public CircularBuffer(int capacity)
    {
        Capacity = capacity;
        _buffer = GC.AllocateArray<double>(capacity, pinned: true);
    }

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
    private static void ThrowArgumentOutOfRangeException()
    {
        throw new ArgumentOutOfRangeException("index", "Index is out of range.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Newest()
    {
        if (_size == 0)
            return 0;
        return _buffer[(_start + _size - 1) % Capacity];
    }

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

    public Enumerator GetEnumerator() => new(this);
    IEnumerator<double> IEnumerable<double>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (_index + 1 >= _buffer._size)
                return false;

            _index++;
            _current = _buffer[_index];
            return true;
        }

        public double Current => _current;
        object IEnumerator.Current => Current;

        public void Reset()
        {
            _index = -1;
            _current = default;
        }

        public void Dispose() { }
    }

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<double> GetSpan()
    {
        if (_size == 0)
            return ReadOnlySpan<double>.Empty;

        if (_start + _size <= Capacity)
        {
            return new ReadOnlySpan<double>(_buffer, _start, _size);
        }
        else
        {
            return new ReadOnlySpan<double>(ToArray());
        }
    }

    public double[] InternalBuffer => _buffer;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<double> GetInternalSpan() => _buffer.AsSpan();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        Array.Clear(_buffer, 0, _buffer.Length);
        _start = 0;
        _size = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Max()
    {
        if (_size == 0)
            ThrowInvalidOperationException();

        return MaxSimd();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Min()
    {
        if (_size == 0)
            ThrowInvalidOperationException();

        return MinSimd();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Sum()
    {
        return SumSimd();
    }

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

    public double[] ToArray()
    {
        double[] array = new double[_size];
        CopyTo(array, 0);
        return array;
    }

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