using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Multi-Period Weighted Moving Average (WMA) - O(1) optimized per period.
/// Calculates multiple WMAs with different periods for the same input series.
/// Uses dual running sums for O(1) complexity per update per period.
/// Uses last-value substitution for invalid inputs (NaN/Infinity).
/// </summary>
[SkipLocalsInit]
public class WmaVector
{
    private readonly int[] _periods;
    private readonly double[] _divisors;
    private readonly RingBuffer[] _buffers;
    private readonly double[] _sums;      // Simple sums for each period
    private readonly double[] _wsums;     // Weighted sums for each period
    private readonly double[] _p_sums;    // Saved simple sums for bar correction
    private readonly double[] _p_wsums;   // Saved weighted sums for bar correction
    private readonly double[] _p_lastInputs;  // Last inputs for bar correction
    private readonly int _count;
    private double _lastValidValue;
    private double _p_lastValidValue;

    /// <summary>
    /// Current WMA values for all periods.
    /// </summary>
    public ReadOnlySpan<TValue> Values => _values;

    private readonly TValue[] _values;

    /// <summary>
    /// Initializes WmaVector with specified periods.
    /// </summary>
    /// <param name="periods">Array of periods (each must be > 0)</param>
    public WmaVector(int[] periods)
    {
        _count = periods.Length;
        _periods = new int[_count];
        _divisors = new double[_count];
        _buffers = new RingBuffer[_count];
        _sums = new double[_count];
        _wsums = new double[_count];
        _p_sums = new double[_count];
        _p_wsums = new double[_count];
        _p_lastInputs = new double[_count];
        _values = new TValue[_count];

        for (int i = 0; i < _count; i++)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(periods[i], 0);
            _periods[i] = periods[i];
            _divisors[i] = periods[i] * (periods[i] + 1) * 0.5;
            _buffers[i] = new RingBuffer(periods[i]);
        }
    }

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
    /// Resets all WMA states.
    /// </summary>
    public void Reset()
    {
        for (int i = 0; i < _count; i++)
        {
            _buffers[i].Clear();
            _sums[i] = 0;
            _wsums[i] = 0;
            _p_sums[i] = 0;
            _p_wsums[i] = 0;
            _p_lastInputs[i] = 0;
        }
        _lastValidValue = 0;
        _p_lastValidValue = 0;
        Array.Clear(_values);
    }

    /// <summary>
    /// Updates WMAs with the given value.
    /// Uses last-value substitution: invalid inputs (NaN/Infinity) are replaced with
    /// the last known good value, providing continuity in the output series.
    /// O(1) complexity per period using dual running sums.
    /// </summary>
    /// <param name="input">Input value</param>
    /// <param name="isNew">True for new bar, false for update to current bar (default: true)</param>
    /// <returns>Array of WMA values</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue[] Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            // Get valid value (this may update _lastValidValue)
            double val = GetValidValue(input.Value);

            for (int i = 0; i < _count; i++)
            {
                int period = _periods[i];
                var buffer = _buffers[i];

                if (buffer.IsFull)
                {
                    // Buffer is full: O(1) update using dual running sums
                    double oldSum = _sums[i];
                    double oldest = buffer.Oldest;
                    _sums[i] = _sums[i] - oldest + val;
                    _wsums[i] = _wsums[i] - oldSum + (period * val);
                }
                else
                {
                    // Warmup phase: incrementally build sums
                    int count = buffer.Count + 1;
                    _sums[i] += val;
                    _wsums[i] += count * val;
                }

                buffer.Add(val);

                // Save state AFTER this update for potential future corrections
                _p_sums[i] = _sums[i];
                _p_wsums[i] = _wsums[i];
                _p_lastInputs[i] = val;

                // Calculate WMA
                double currentDivisor = buffer.IsFull ? _divisors[i] : buffer.Count * (buffer.Count + 1) * 0.5;
                _values[i] = new TValue(input.Time, _wsums[i] / currentDivisor);
            }

            _p_lastValidValue = _lastValidValue;
        }
        else
        {
            // Bar correction: restore to state AFTER last isNew=true, then swap last value
            _lastValidValue = _p_lastValidValue;
            double val = GetValidValue(input.Value);

            for (int i = 0; i < _count; i++)
            {
                int period = _periods[i];
                var buffer = _buffers[i];

                // Restore sums to state after last isNew=true
                _sums[i] = _p_sums[i];
                _wsums[i] = _p_wsums[i];

                // Correction: replace _p_lastInputs[i] with val
                int weight = buffer.IsFull ? period : buffer.Count;
                _sums[i] = _sums[i] - _p_lastInputs[i] + val;
                _wsums[i] += weight * (val - _p_lastInputs[i]);

                buffer.UpdateNewest(val);

                // Calculate WMA
                double currentDivisor = buffer.IsFull ? _divisors[i] : buffer.Count * (buffer.Count + 1) * 0.5;
                _values[i] = new TValue(input.Time, _wsums[i] / currentDivisor);
            }
        }

        return _values;
    }

    /// <summary>
    /// Calculates WMAs for the entire series.
    /// </summary>
    /// <param name="source">Input series</param>
    /// <returns>Array of WMA series</returns>
    public TSeries[] Calculate(TSeries source)
    {
        int len = source.Count;
        var resultSeries = new TSeries[_count];

        // Reset state for fresh calculation
        Reset();

        // Pre-allocate lists
        var tLists = new List<long>[_count];
        var vLists = new List<double>[_count];

        for (int i = 0; i < _count; i++)
        {
            tLists[i] = new List<long>(len);
            vLists[i] = new List<double>(len);
            CollectionsMarshal.SetCount(tLists[i], len);
            CollectionsMarshal.SetCount(vLists[i], len);
        }

        var sourceValues = source.Values;
        var sourceTimes = source.Times;

        for (int t = 0; t < len; t++)
        {
            double val = sourceValues[t];
            long time = sourceTimes[t];

            // Last-value substitution: replace non-finite inputs with last valid value
            val = GetValidValue(val);

            for (int i = 0; i < _count; i++)
            {
                int period = _periods[i];
                var buffer = _buffers[i];

                if (buffer.IsFull)
                {
                    // Buffer is full: O(1) update
                    double oldSum = _sums[i];
                    double oldest = buffer.Oldest;
                    _sums[i] = _sums[i] - oldest + val;
                    _wsums[i] = _wsums[i] - oldSum + (period * val);
                }
                else
                {
                    // Warmup phase
                    int count = buffer.Count + 1;
                    _sums[i] += val;
                    _wsums[i] += count * val;
                }

                buffer.Add(val);

                CollectionsMarshal.AsSpan(tLists[i])[t] = time;
                double currentDivisor = buffer.IsFull ? _divisors[i] : buffer.Count * (buffer.Count + 1) * 0.5;
                CollectionsMarshal.AsSpan(vLists[i])[t] = _wsums[i] / currentDivisor;
            }
        }

        // Create TSeries and update Values
        for (int i = 0; i < _count; i++)
        {
            resultSeries[i] = new TSeries(tLists[i], vLists[i]);
            var lastT = CollectionsMarshal.AsSpan(tLists[i])[len - 1];
            var lastV = CollectionsMarshal.AsSpan(vLists[i])[len - 1];
            _values[i] = new TValue(lastT, lastV);
        }

        return resultSeries;
    }

    /// <summary>
    /// Calculates WMAs for the entire series using specified periods.
    /// </summary>
    /// <param name="source">Input series</param>
    /// <param name="periods">Array of periods</param>
    /// <returns>Array of WMA series</returns>
    public static TSeries[] Calculate(TSeries source, int[] periods)
    {
        var wmaVector = new WmaVector(periods);
        return wmaVector.Calculate(source);
    }
}
