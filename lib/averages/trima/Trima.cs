using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// TRIMA: Triangular Moving Average
/// </summary>
/// <remarks>
/// TRIMA is a weighted moving average where the weights increase linearly to the middle
/// of the period and then decrease linearly. It places the most weight on the middle
/// portion of the data series.
///
/// Calculation:
/// TRIMA(period) = SMA(SMA(period1), period2)
/// where:
/// period1 = period / 2 + 1
/// period2 = (period + 1) / 2
///
/// This implementation uses a flattened structure with two internal SMA buffers
/// to ensure correct handling of warmup periods and bar corrections without
/// the overhead of composed objects.
///
/// Key characteristics:
/// - Smoother than SMA
/// - Double smoothing (lag is higher than SMA)
/// - Weights form a triangle
/// - O(1) time complexity
/// - O(period) space complexity
///
/// Sources:
/// - https://www.investopedia.com/terms/t/triangularaverage.asp
/// </remarks>
[SkipLocalsInit]
public sealed class Trima
{
    private readonly int _period;
    private readonly int _p1;
    private readonly int _p2;
    private readonly RingBuffer _buffer1;
    private readonly RingBuffer _buffer2;

    // SMA1 State
    private double _sum1;
    private double _p_sum1;
    private double _p_lastInput1;
    private double _lastValidValue1;
    private double _p_lastValidValue1;
    private int _tickCount1;

    // SMA2 State
    private double _sum2;
    private double _p_sum2;
    private double _p_lastInput2;
    private int _tickCount2;

    private int _sampleCount;
    private const int ResyncInterval = 1000;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Creates TRIMA with specified period.
    /// </summary>
    /// <param name="period">Number of values to average (must be > 0)</param>
    public Trima(int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        _period = period;
        _p1 = period / 2 + 1;
        _p2 = (period + 1) / 2;
        
        _buffer1 = new RingBuffer(_p1);
        _buffer2 = new RingBuffer(_p2);
        
        Name = $"Trima({period})";
    }

    /// <summary>
    /// Current TRIMA value.
    /// </summary>
    public TValue Value { get; private set; }

    /// <summary>
    /// True if the TRIMA has enough data to produce valid results.
    /// </summary>
    public bool IsHot => _sampleCount >= _period;

    /// <summary>
    /// Gets a valid input value, using last-value substitution for non-finite inputs.
    /// </summary>
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

    /// <summary>
    /// Updates TRIMA with the given value.
    /// </summary>
    /// <param name="input">Input value</param>
    /// <param name="isNew">True for new bar, false for update to current bar (default: true)</param>
    /// <returns>Current TRIMA value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _sampleCount++;

            // SMA 1 Update
            double val1 = GetValidValue(input.Value);
            double removed1 = _buffer1.Count == _buffer1.Capacity ? _buffer1.Oldest : 0.0;
            _sum1 = _sum1 - removed1 + val1;
            _buffer1.Add(val1);

            // Resync SMA1
            _tickCount1++;
            if (_buffer1.IsFull && _tickCount1 >= ResyncInterval)
            {
                _tickCount1 = 0;
                _sum1 = _buffer1.Sum();
            }

            // Save SMA1 state
            _p_sum1 = _sum1;
            _p_lastInput1 = val1;
            _p_lastValidValue1 = _lastValidValue1;

            // SMA 1 Result
            double sma1Result = _sum1 / _buffer1.Count;

            // SMA 2 Update (Input is sma1Result)
            // Note: sma1Result is always finite if input stream has at least one finite value
            double removed2 = _buffer2.Count == _buffer2.Capacity ? _buffer2.Oldest : 0.0;
            _sum2 = _sum2 - removed2 + sma1Result;
            _buffer2.Add(sma1Result);

            // Resync SMA2
            _tickCount2++;
            if (_buffer2.IsFull && _tickCount2 >= ResyncInterval)
            {
                _tickCount2 = 0;
                _sum2 = _buffer2.Sum();
            }

            // Save SMA2 state
            _p_sum2 = _sum2;
            _p_lastInput2 = sma1Result;

            // Final Result
            double trimaResult = _sum2 / _buffer2.Count;
            Value = new TValue(input.Time, trimaResult);
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

            double trimaResult = _sum2 / _buffer2.Count;
            Value = new TValue(input.Time, trimaResult);
        }

        return Value;
    }

    /// <summary>
    /// Updates TRIMA with the entire series.
    /// </summary>
    /// <param name="source">Input series</param>
    /// <returns>TRIMA series</returns>
    public TSeries Update(TSeries source)
    {
        if (source.Count == 0) return new TSeries(new List<long>(), new List<double>());

        // Use the static Calculate method for performance
        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);
        var sourceValues = source.Values;
        var sourceTimes = source.Times;

        Calculate(sourceValues, vSpan, _period);
        sourceTimes.CopyTo(tSpan);

        // Restore state by replaying the last part
        // We need to replay enough to fill both SMAs
        int lookback = _p1 + _p2;
        int startIndex = Math.Max(0, len - lookback);

        // Reset internal state
        Reset();

        // Replay
        for (int i = startIndex; i < len; i++)
        {
            Update(new TValue(sourceTimes[i], sourceValues[i]), isNew: true);
        }

        Value = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    /// <summary>
    /// Calculates TRIMA for the entire series using a new instance.
    /// </summary>
    public static TSeries Calculate(TSeries source, int period)
    {
        var trima = new Trima(period);
        return trima.Update(source);
    }

    /// <summary>
    /// Calculates TRIMA in-place.
    /// Uses ArrayPool to allocate temporary buffer and chains optimized SMA calculations.
    /// </summary>
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length");
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        int p1 = period / 2 + 1;
        int p2 = (period + 1) / 2;

        // Rent a temporary buffer for the intermediate SMA
        double[] tempArray = ArrayPool<double>.Shared.Rent(source.Length);
        Span<double> tempSpan = tempArray.AsSpan(0, source.Length);

        try
        {
            // SMA 1
            Sma.Calculate(source, tempSpan, p1);
            
            // SMA 2 (TRIMA)
            Sma.Calculate(tempSpan, output, p2);
        }
        finally
        {
            ArrayPool<double>.Shared.Return(tempArray);
        }
    }
    
    /// <summary>
    /// Resets the TRIMA state.
    /// </summary>
    public void Reset()
    {
        _buffer1.Clear();
        _buffer2.Clear();
        
        _sum1 = 0;
        _p_sum1 = 0;
        _p_lastInput1 = 0;
        _lastValidValue1 = 0;
        _p_lastValidValue1 = 0;
        _tickCount1 = 0;

        _sum2 = 0;
        _p_sum2 = 0;
        _p_lastInput2 = 0;
        _tickCount2 = 0;

        _sampleCount = 0;
        Value = default;
    }
}
