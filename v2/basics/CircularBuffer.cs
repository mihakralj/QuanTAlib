using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public class CircularBuffer: IEnumerable<double>
{
    private double[] _buffer = null!;
    private int _start;
    private int _size;

    public int Capacity => _buffer.Length;
    public int Count => _size;

    public CircularBuffer(int capacity)
    {

        _buffer = new double[capacity];
        _start = 0;
        _size = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(double item, bool isNew = true) {
        if (_size == 0 || isNew) {
            // If buffer is empty or isNew is true, add new item
            if (_size < Capacity) {
                _buffer[(_start + _size) % Capacity] = item;
                _size++;
            } else {
                _buffer[_start] = item;
                _start = (_start + 1) % Capacity;
            }
        } else {
            // If isNew is false, just update the last item
            if (_size > 0) {
                _buffer[(_start + _size - 1) % Capacity] = item;
            } else {
                _buffer[_start] = item;
                _size = 1;
            }
        }
    }

    public double this[int index] {
        get {
            if (index < 0 || index >= _size)
                throw new IndexOutOfRangeException();
            return _buffer[(_start + index) % Capacity];
        } set {
            if (index < 0 || index >= _size)
                throw new IndexOutOfRangeException();
            _buffer[(_start + index) % Capacity] = value;
        }
    }

    public Enumerator GetEnumerator() => new Enumerator(this);
    IEnumerator<double> IEnumerable<double>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public struct Enumerator : IEnumerator<double> {
        private readonly CircularBuffer _buffer;
        private int _index;
        private double _current;

        internal Enumerator(CircularBuffer buffer) {
            _buffer = buffer;
            _index = -1;
            _current = default;
        }

        public bool MoveNext() {
            if (_index + 1 >= _buffer._size)
                return false;

            _index++;
            _current = _buffer[_index];
            return true;
        }

        public double Current => _current;
        object IEnumerator.Current => Current;

        public void Reset() {
            _index = -1;
            _current = default;
        }

        public void Dispose() { }
    }
}