using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// WMA: Weighted Moving Average
/// </summary>
/// <remarks>
/// WMA applies linear weighting to data points, giving more weight to recent values.
/// Uses dual running sums for O(1) complexity per update.
///
/// Key characteristics:
/// - Linear weighting: newest value has weight n, oldest has weight 1
/// - More responsive than SMA due to emphasis on recent data
/// - Less lag than SMA, but more than EMA
/// - O(1) time complexity for both update and bar correction
/// - O(1) space complexity for state save/restore (scalars only)
///
/// Calculation method:
/// WMA = (n*P_n + (n-1)*P_(n-1) + ... + 2*P_2 + 1*P_1) / (n*(n+1)/2)
///
/// O(1) update formula:
/// S_new = S - oldest + newest
/// W_new = W - S_old + n*newest
/// WMA = W_new / divisor
///
/// Bar correction (isNew=false):
/// - Restores to state after last isNew=true
/// - Then replaces the last value with new correction value
/// - All O(1) using scalar state
///
/// Sources:
/// - https://www.investopedia.com/terms/w/weightedaverage.asp
/// - https://school.stockcharts.com/doku.php?id=technical_indicators:weighted_moving_average
/// </remarks>
[SkipLocalsInit]
public sealed class Wma
{
    private readonly int _period;
    private readonly double _divisor;
    private readonly RingBuffer _buffer;

    // Dual running sums for O(1) WMA calculation
    private double _sum;      // Simple sum of values in window
    private double _wsum;     // Weighted sum of values in window
    private double _p_sum;    // Sum AFTER last isNew=true (for correction restore)
    private double _p_wsum;   // Weighted sum AFTER last isNew=true
    private double _p_lastInput;    // Input that was added on last isNew=true
    private double _lastValidValue;
    private double _p_lastValidValue;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Number of data points needed for the indicator to become "hot".
    /// </summary>
    public int WarmupPeriod { get; }

    /// <summary>
    /// Creates WMA with specified period.
    /// </summary>
    /// <param name="period">Number of values to average (must be > 0)</param>
    public Wma(int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        _period = period;
        _divisor = period * (period + 1) * 0.5;
        _buffer = new RingBuffer(period);
        Name = $"Wma({period})";
        WarmupPeriod = period;
    }

    /// <summary>
    /// Current WMA value.
    /// </summary>
    public TValue Value { get; private set; }

    /// <summary>
    /// True if the WMA has enough data to produce valid results.
    /// WMA is "hot" when the buffer is full (has received at least 'period' values).
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
    /// Updates WMA with the given value.
    /// O(1) for both isNew=true and isNew=false.
    /// </summary>
    /// <param name="input">Input value</param>
    /// <param name="isNew">True for new bar, false for update to current bar (default: true)</param>
    /// <returns>Current WMA value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            // Get valid value (this may update _lastValidValue)
            double val = GetValidValue(input.Value);

            if (_buffer.IsFull)
            {
                // Buffer is full: O(1) update using dual running sums
                double oldSum = _sum;  // Capture before update
                double oldest = _buffer.Oldest;
                _sum = _sum - oldest + val;
                _wsum = _wsum - oldSum + (_period * val);
            }
            else
            {
                // Warmup phase: incrementally build sums
                int count = _buffer.Count + 1;
                _sum += val;
                _wsum += count * val;
            }

            // Update buffer
            _buffer.Add(val);

            // Save state AFTER this update for potential future corrections
            _p_sum = _sum;
            _p_wsum = _wsum;
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

            // Restore sums to state after last isNew=true
            _sum = _p_sum;
            _wsum = _p_wsum;

            // Correction: replace _p_lastInput with val
            // S_corrected = S - lastInput + val
            // W_corrected = W + weight*(val - lastInput), where weight = period (if full) or count (if warmup)
            int weight = _buffer.IsFull ? _period : _buffer.Count;
            _sum = _sum - _p_lastInput + val;
            _wsum += weight * (val - _p_lastInput);

            // Update buffer's newest value
            _buffer.UpdateNewest(val);
        }

        // Calculate WMA using current divisor (handles warmup)
        double currentDivisor = _buffer.IsFull ? _divisor : _buffer.Count * (_buffer.Count + 1) * 0.5;
        double result = _wsum / currentDivisor;
        Value = new TValue(input.Time, result);
        return Value;
    }

    /// <summary>
    /// Updates WMA with the entire series.
    /// </summary>
    /// <param name="source">Input series</param>
    /// <returns>WMA series</returns>
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

        // Use local state for batch processing
        var localBuffer = new RingBuffer(_period);
        double localSum = 0;
        double localWsum = 0;

        for (int i = 0; i < len; i++)
        {
            // Last-value substitution: replace non-finite inputs with last valid value
            double val = GetValidValue(sourceValues[i]);

            if (localBuffer.IsFull)
            {
                // Buffer is full: O(1) update
                double oldSum = localSum;
                double oldest = localBuffer.Oldest;
                localSum = localSum - oldest + val;
                localWsum = localWsum - oldSum + (_period * val);
            }
            else
            {
                // Warmup phase
                int count = localBuffer.Count + 1;
                localSum += val;
                localWsum += count * val;
            }

            localBuffer.Add(val);

            tSpan[i] = sourceTimes[i];
            double currentDivisor = localBuffer.IsFull ? _divisor : localBuffer.Count * (localBuffer.Count + 1) * 0.5;
            vSpan[i] = localWsum / currentDivisor;
        }

        // Update instance state to the final state
        _buffer.CopyFrom(localBuffer);
        _sum = localSum;
        _wsum = localWsum;
        _p_sum = localSum;
        _p_wsum = localWsum;
        _p_lastInput = sourceValues[len - 1];

        Value = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    /// <summary>
    /// Calculates WMA for the entire series using a new instance.
    /// </summary>
    /// <param name="source">Input series</param>
    /// <param name="period">WMA period</param>
    /// <returns>WMA series</returns>
    public static TSeries Calculate(TSeries source, int period)
    {
        var wma = new Wma(period);
        return wma.Update(source);
    }

    /// <summary>
    /// Resets the WMA state.
    /// </summary>
    public void Reset()
    {
        _buffer.Clear();
        _sum = 0;
        _wsum = 0;
        _p_sum = 0;
        _p_wsum = 0;
        _p_lastInput = 0;
        _lastValidValue = 0;
        _p_lastValidValue = 0;
        Value = default;
    }
}
