using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// TRIMA: Triangular Moving Average
/// </summary>
/// <remarks>
/// TRIMA applies triangular weighting to data points, emphasizing the middle of the window.
/// Equivalent to a double SMA: SMA(SMA(period1), period2).
///
/// Calculation:
/// p1 = period / 2 + 1
/// p2 = (period + 1) / 2
/// TRIMA = SMA(SMA(input, p1), p2)
///
/// O(1) update:
/// Uses two SMA instances, each with O(1) update complexity.
///
/// IsHot:
/// Becomes true when the buffer is full (period samples processed).
/// </remarks>
[SkipLocalsInit]
public sealed class Trima : ITValuePublisher
{
    private readonly int _period;
    private readonly int _p1;
    private readonly int _p2;
    private readonly RingBuffer _buffer1;
    private readonly RingBuffer _buffer2;

    private record struct State(
        double Sum1, double LastInput1, double LastValidValue1, int TickCount1, double NextRemoved1,
        double Sum2, double LastInput2, int TickCount2, double NextRemoved2,
        int SampleCount
    );
    private State _state;
    private State _p_state;

    private const int ResyncInterval = 1000;

    public string Name { get; }
    public TValue Last { get; private set; }
    public bool IsHot => _state.SampleCount >= _period;
    public event Action<TValue>? Pub;

    public Trima(int period)
    {
        if (period <= 0) throw new ArgumentException("Period must be greater than 0", nameof(period));

        _period = period;
        _p1 = period / 2 + 1;
        _p2 = (period + 1) / 2;
        
        _buffer1 = new RingBuffer(_p1);
        _buffer2 = new RingBuffer(_p2);
        
        Name = $"Trima({period})";
    }

    public Trima(ITValuePublisher source, int period) : this(period)
    {
        source.Pub += (item) => Update(item);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValidValue(double input)
    {
        if (double.IsFinite(input))
        {
            _state.LastValidValue1 = input;
            return input;
        }
        return _state.LastValidValue1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
            _state.SampleCount++;
        }
        else
        {
            _state = _p_state;
        }

        // SMA 1
        double val1 = GetValidValue(input.Value);
        
        if (isNew)
        {
            double removed1 = _buffer1.Count == _buffer1.Capacity ? _buffer1.Oldest : 0.0;
            _state.Sum1 = _state.Sum1 - removed1 + val1;
            _buffer1.Add(val1);
            
            // Store NextRemoved1 for next step
            _state.NextRemoved1 = _buffer1.Count == _buffer1.Capacity ? _buffer1.Oldest : 0.0;

            _state.TickCount1++;
            if (_buffer1.IsFull && _state.TickCount1 >= ResyncInterval)
            {
                _state.TickCount1 = 0;
                _state.Sum1 = _buffer1.Sum();
            }
        }
        else
        {
            // Use NextRemoved1 from _p_state
            double removed1 = _p_state.NextRemoved1;
            _state.Sum1 = _p_state.Sum1 - removed1 + val1;
            _buffer1.UpdateNewest(val1);
        }
        
        _state.LastInput1 = val1;
        double sma1Result = _state.Sum1 / _buffer1.Count;

        // SMA 2
        if (isNew)
        {
            double removed2 = _buffer2.Count == _buffer2.Capacity ? _buffer2.Oldest : 0.0;
            _state.Sum2 = _state.Sum2 - removed2 + sma1Result;
            _buffer2.Add(sma1Result);
            
            // Store NextRemoved2 for next step
            _state.NextRemoved2 = _buffer2.Count == _buffer2.Capacity ? _buffer2.Oldest : 0.0;

            _state.TickCount2++;
            if (_buffer2.IsFull && _state.TickCount2 >= ResyncInterval)
            {
                _state.TickCount2 = 0;
                _state.Sum2 = _buffer2.Sum();
            }
        }
        else
        {
            // Use NextRemoved2 from _p_state
            double removed2 = _p_state.NextRemoved2;
            _state.Sum2 = _p_state.Sum2 - removed2 + sma1Result;
            _buffer2.UpdateNewest(sma1Result);
        }

        _state.LastInput2 = sma1Result;

        Last = new TValue(input.Time, _state.Sum2 / _buffer2.Count);
        Pub?.Invoke(Last);
        return Last;
    }

    public TSeries Update(TSeries source)
    {
        if (source.Count == 0) return [];

        int len = source.Count;
        List<long> t = new(len);
        List<double> v = new(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);
        
        Calculate(source.Values, vSpan, _period);
        source.Times.CopyTo(tSpan);

        // Restore state
        int lookback = _p1 + _p2 - 2;
        int startIndex = Math.Max(0, len - lookback);
        Reset();

        for (int i = startIndex; i < len; i++)
        {
            Update(new TValue(source.Times[i], source.Values[i]), isNew: true);
        }

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    public static TSeries Calculate(TSeries source, int period)
    {
        var trima = new Trima(period);
        return trima.Update(source);
    }

    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length");
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        int p1 = period / 2 + 1;
        int p2 = (period + 1) / 2;

        double[] tempArray = ArrayPool<double>.Shared.Rent(source.Length);
        Span<double> tempSpan = tempArray.AsSpan(0, source.Length);

        try
        {
            Sma.Calculate(source, tempSpan, p1);
            Sma.Calculate(tempSpan, output, p2);
        }
        finally
        {
            ArrayPool<double>.Shared.Return(tempArray);
        }
    }
    
    public void Reset()
    {
        _buffer1.Clear();
        _buffer2.Clear();
        _state = default;
        _p_state = default;
        Last = default;
    }
}
