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
public sealed class Trima
{
    private readonly int _period;
    private readonly int _p1;
    private readonly int _p2;
    private readonly RingBuffer _buffer1;
    private readonly RingBuffer _buffer2;

    private double _sum1, _p_sum1, _p_lastInput1, _lastValidValue1, _p_lastValidValue1;
    private int _tickCount1;

    private double _sum2, _p_sum2, _p_lastInput2;
    private int _tickCount2;

    private int _sampleCount;
    private const int ResyncInterval = 1000;

    public string Name { get; }
    public TValue Value { get; private set; }
    public bool IsHot => _sampleCount >= _period;

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValidValue(double input)
    {
        if (double.IsFinite(input))
        {
            _lastValidValue1 = input;
            return input;
        }
        return _lastValidValue1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _sampleCount++;

            // SMA 1
            double val1 = GetValidValue(input.Value);
            double removed1 = _buffer1.Count == _buffer1.Capacity ? _buffer1.Oldest : 0.0;
            _sum1 = _sum1 - removed1 + val1;
            _buffer1.Add(val1);

            _tickCount1++;
            if (_buffer1.IsFull && _tickCount1 >= ResyncInterval)
            {
                _tickCount1 = 0;
                _sum1 = _buffer1.Sum();
            }

            _p_sum1 = _sum1;
            _p_lastInput1 = val1;
            _p_lastValidValue1 = _lastValidValue1;

            double sma1Result = _sum1 / _buffer1.Count;

            // SMA 2
            double removed2 = _buffer2.Count == _buffer2.Capacity ? _buffer2.Oldest : 0.0;
            _sum2 = _sum2 - removed2 + sma1Result;
            _buffer2.Add(sma1Result);

            _tickCount2++;
            if (_buffer2.IsFull && _tickCount2 >= ResyncInterval)
            {
                _tickCount2 = 0;
                _sum2 = _buffer2.Sum();
            }

            _p_sum2 = _sum2;
            _p_lastInput2 = sma1Result;

            Value = new TValue(input.Time, _sum2 / _buffer2.Count);
        }
        else
        {
            // SMA 1 Correction
            _lastValidValue1 = _p_lastValidValue1;
            double val1 = GetValidValue(input.Value);
            _sum1 = _p_sum1 - _p_lastInput1 + val1;
            _buffer1.UpdateNewest(val1);
            
            double sma1Result = _sum1 / _buffer1.Count;

            // SMA 2 Correction
            _sum2 = _p_sum2 - _p_lastInput2 + sma1Result;
            _buffer2.UpdateNewest(sma1Result);

            Value = new TValue(input.Time, _sum2 / _buffer2.Count);
        }

        return Value;
    }

    public TSeries Update(TSeries source)
    {
        if (source.Count == 0) return new TSeries(new List<long>(), new List<double>());

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);
        
        Calculate(source.Values, vSpan, _period);
        source.Times.CopyTo(tSpan);

        // Restore state
        int lookback = _p1 + _p2;
        int startIndex = Math.Max(0, len - lookback);
        Reset();

        for (int i = startIndex; i < len; i++)
        {
            Update(new TValue(source.Times[i], source.Values[i]), isNew: true);
        }

        Value = new TValue(tSpan[len - 1], vSpan[len - 1]);
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
        
        _sum1 = _p_sum1 = _p_lastInput1 = _lastValidValue1 = _p_lastValidValue1 = 0;
        _tickCount1 = 0;

        _sum2 = _p_sum2 = _p_lastInput2 = 0;
        _tickCount2 = 0;

        _sampleCount = 0;
        Value = default;
    }
}
