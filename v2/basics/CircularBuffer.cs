namespace QuanTAlib;

public class CircularBuffer
{
    private double[] _buffer = null!;
    private int _start;
    private int _size;

    public CircularBuffer(int capacity)
    {
        _buffer = new double[capacity];
        _start = 0;
        _size = 0;
    }

    public int Capacity => _buffer.Length;
    public int Count => _size;

    public void Add(double item, bool isNew)
    {
        if (_size == 0 || isNew)
        {
            // If buffer is empty or isNew is true, add new item
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
            // If isNew is false, just update the last item
            _buffer[(_start + _size - 1) % Capacity] = item;
        }
    }

    public double this[int index]
    {
        get
        {
            if (index < 0 || index >= _size)
                throw new IndexOutOfRangeException();
            return _buffer[(_start + index) % Capacity];
        }
        set
        {
            if (index < 0 || index >= _size)
                throw new IndexOutOfRangeException();
            _buffer[(_start + index) % Capacity] = value;
        }
    }
}