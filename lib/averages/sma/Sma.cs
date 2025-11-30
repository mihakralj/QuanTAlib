using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// SMA: Simple Moving Average
/// </summary>
/// <remarks>
/// SMA calculates the arithmetic mean of the last N values.
/// Uses a RingBuffer for storage and manual running sum for O(1) operations.
///
/// Key characteristics:
/// - Equal weighting of all values in the period
/// - No lag bias - responds equally to all values in window
/// - Smooth output with good noise reduction
/// - O(1) time complexity for both update and bar correction
/// - O(1) space complexity for state save/restore (scalars only)
///
/// Calculation method:
/// SMA = Sum(values in period) / period
///
/// Bar correction (isNew=false):
/// - Restores to state after last isNew=true
/// - Then replaces the last value with new correction value
/// - All O(1) using scalar state
///
/// Sources:
/// - https://stockcharts.com/school/doku.php?id=chart_school:technical_indicators:moving_averages
/// - https://www.investopedia.com/terms/s/sma.asp
/// </remarks>
[SkipLocalsInit]
public sealed class Sma
{
    private readonly int _period;
    private readonly RingBuffer _buffer;

    // Running sum maintained separately for O(1) bar correction
    private double _sum;
    private double _p_sum;          // Sum AFTER last isNew=true (for correction restore)
    private double _p_lastInput;    // Input that was added on last isNew=true
    private double _lastValidValue;
    private double _p_lastValidValue;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Creates SMA with specified period.
    /// </summary>
    /// <param name="period">Number of values to average (must be > 0)</param>
    public Sma(int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        _period = period;
        _buffer = new RingBuffer(period);
        Name = $"Sma({period})";
    }

    /// <summary>
    /// Current SMA value.
    /// </summary>
    public TValue Value { get; private set; }

    /// <summary>
    /// True if the SMA has enough data to produce valid results.
    /// SMA is "hot" when the buffer is full (has received at least 'period' values).
    /// </summary>
    public bool IsHot => _buffer.IsFull;

    /// <summary>
    /// Gets a valid input value, using last-value substitution for non-finite inputs.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetValidValue(double input)
    {
        if (double.IsFinite(input))
        {
            _lastValidValue = input;
            return input;
        }
        return _lastValidValue;
    }

    /// <summary>
    /// Updates SMA with the given value.
    /// O(1) for both isNew=true and isNew=false.
    /// </summary>
    /// <param name="input">Input value</param>
    /// <param name="isNew">True for new bar, false for update to current bar (default: true)</param>
    /// <returns>Current SMA value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            // Get valid value (this may update _lastValidValue)
            double val = GetValidValue(input.Value);

            // Calculate what to remove from sum (oldest value if buffer full)
            double removedValue = _buffer.Count == _buffer.Capacity ? _buffer.Oldest : 0.0;

            // Update sum: remove oldest, add newest
            _sum = _sum - removedValue + val;

            // Update buffer
            _buffer.Add(val);

            // Save state AFTER this update for potential future corrections
            _p_sum = _sum;
            _p_lastInput = val;
            _p_lastValidValue = _lastValidValue;
        }
        else
        {
            // Bar correction: restore to state AFTER last isNew=true, then swap last value
            // Restore _lastValidValue BEFORE calling GetValidValue
            _lastValidValue = _p_lastValidValue;

            // Get valid value (this may update _lastValidValue)
            double val = GetValidValue(input.Value);

            // _p_sum is the sum AFTER the last isNew=true completed
            // _p_lastInput is the value that was added on last isNew=true
            // We want: new_sum = _p_sum - _p_lastInput + val
            _sum = _p_sum - _p_lastInput + val;

            // Update buffer's newest value
            _buffer.UpdateNewest(val);
        }

        double result = _sum / _buffer.Count;
        Value = new TValue(input.Time, result);
        return Value;
    }

    /// <summary>
    /// Updates SMA with the entire series.
    /// </summary>
    /// <param name="source">Input series</param>
    /// <returns>SMA series</returns>
    public TSeries Update(TSeries source)
    {
        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);
        var sourceValues = source.Values;
        var sourceTimes = source.Times;

        // Use local buffer and sum for batch processing
        var localBuffer = new RingBuffer(_period);
        double localSum = 0;

        for (int i = 0; i < len; i++)
        {
            // Last-value substitution: replace non-finite inputs with last valid value
            double val = GetValidValue(sourceValues[i]);

            // Remove oldest if buffer full
            double removedValue = localBuffer.Count == localBuffer.Capacity ? localBuffer.Oldest : 0.0;
            localSum = localSum - removedValue + val;

            localBuffer.Add(val);

            tSpan[i] = sourceTimes[i];
            vSpan[i] = localSum / localBuffer.Count;
        }

        // Update instance state to the final state
        // Copy buffer contents (needed for future streaming updates)
        _buffer.CopyFrom(localBuffer);
        _sum = localSum;
        _p_sum = localSum;
        _p_lastInput = sourceValues[len - 1];

        Value = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    /// <summary>
    /// Calculates SMA for the entire series using a new instance.
    /// </summary>
    /// <param name="source">Input series</param>
    /// <param name="period">SMA period</param>
    /// <returns>SMA series</returns>
    public static TSeries Calculate(TSeries source, int period)
    {
        var sma = new Sma(period);
        return sma.Update(source);
    }

    /// <summary>
    /// Calculates SMA in-place, writing results to pre-allocated output span.
    /// Zero-allocation method for maximum performance.
    /// </summary>
    /// <param name="source">Input values</param>
    /// <param name="output">Output span (must be same length as source)</param>
    /// <param name="period">SMA period (must be > 0)</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length");
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        int len = source.Length;
        double sum = 0;
        double lastValid = 0;

        for (int i = 0; i < len; i++)
        {
            double val = source[i];
            if (!double.IsFinite(val))
                val = lastValid;
            else
                lastValid = val;

            if (i >= period)
            {
                double oldVal = source[i - period];
                if (!double.IsFinite(oldVal))
                    oldVal = lastValid; // Approximate - for exact behavior use instance method
                sum -= oldVal;
            }
            sum += val;

            int count = Math.Min(i + 1, period);
            output[i] = sum / count;
        }
    }

    /// <summary>
    /// Resets the SMA state.
    /// </summary>
    public void Reset()
    {
        _buffer.Clear();
        _sum = 0;
        _p_sum = 0;
        _p_lastInput = 0;
        _lastValidValue = 0;
        _p_lastValidValue = 0;
        Value = default;
    }
}
