using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Multi-Period Simple Moving Average (SMA) - SIMD optimized.
/// Calculates multiple SMAs with different periods for the same input series in parallel.
/// Uses last-value substitution for invalid inputs (NaN/Infinity).
/// </summary>
[SkipLocalsInit]
public class SmaVector
{
    private readonly RingBuffer[] _buffers;
    private readonly RingBuffer[] _p_buffers;  // Previous state for bar correction
    private readonly int _count;
    private double _lastValidValue;

    /// <summary>
    /// Current SMA values for all periods.
    /// </summary>
    public ReadOnlySpan<TValue> Values => _values;

    private readonly TValue[] _values;

    /// <summary>
    /// Initializes SmaVector with specified periods.
    /// </summary>
    /// <param name="periods">Array of periods (each must be > 0)</param>
    public SmaVector(int[] periods)
    {
        _count = periods.Length;
        _buffers = new RingBuffer[_count];
        _p_buffers = new RingBuffer[_count];
        _values = new TValue[_count];

        for (int i = 0; i < _count; i++)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(periods[i], 0);
            _buffers[i] = new RingBuffer(periods[i]);
            _p_buffers[i] = new RingBuffer(periods[i]);
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
    /// Resets all SMA states.
    /// </summary>
    public void Reset()
    {
        for (int i = 0; i < _count; i++)
        {
            _buffers[i].Clear();
            _p_buffers[i].Clear();
        }
        _lastValidValue = 0;
        Array.Clear(_values);
    }

    /// <summary>
    /// Updates SMAs with the given value.
    /// Uses last-value substitution: invalid inputs (NaN/Infinity) are replaced with
    /// the last known good value, providing continuity in the output series.
    /// </summary>
    /// <param name="input">Input value</param>
    /// <param name="isNew">True for new bar, false for update to current bar (default: true)</param>
    /// <returns>Array of SMA values</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue[] Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            // Save current state for potential bar correction
            for (int i = 0; i < _count; i++)
            {
                _p_buffers[i].CopyFrom(_buffers[i]);
            }
        }
        else
        {
            // Restore previous state for bar correction
            for (int i = 0; i < _count; i++)
            {
                _buffers[i].CopyFrom(_p_buffers[i]);
            }
        }

        // Last-value substitution: replace non-finite inputs with last valid value
        double val = GetValidValue(input.Value);

        // Update each buffer and calculate SMA
        for (int i = 0; i < _count; i++)
        {
            _buffers[i].Add(val);
            _values[i] = new TValue(input.Time, _buffers[i].Average);
        }

        return _values;
    }

    /// <summary>
    /// Calculates SMAs for the entire series.
    /// </summary>
    /// <param name="source">Input series</param>
    /// <returns>Array of SMA series</returns>
    public TSeries[] Calculate(TSeries source)
    {
        int len = source.Count;
        var resultSeries = new TSeries[_count];

        // Reset state for fresh calculation
        for (int i = 0; i < _count; i++)
        {
            _buffers[i].Clear();
        }
        _lastValidValue = 0;

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
                _buffers[i].Add(val);
                CollectionsMarshal.AsSpan(tLists[i])[t] = time;
                CollectionsMarshal.AsSpan(vLists[i])[t] = _buffers[i].Average;
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
    /// Calculates SMAs for the entire series using specified periods.
    /// </summary>
    /// <param name="source">Input series</param>
    /// <param name="periods">Array of periods</param>
    /// <returns>Array of SMA series</returns>
    public static TSeries[] Calculate(TSeries source, int[] periods)
    {
        var smaVector = new SmaVector(periods);
        return smaVector.Calculate(source);
    }
}
