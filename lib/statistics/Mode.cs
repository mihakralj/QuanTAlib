using System.Collections.Generic;
using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// MODE: Most Frequent Value Measure
/// A statistical measure that identifies the most frequently occurring value(s)
/// in a dataset. When multiple values share the highest frequency, it returns
/// their average to provide a representative central value.
/// </summary>
/// <remarks>
/// The Mode calculation process:
/// 1. Groups values by frequency
/// 2. Identifies highest frequency group(s)
/// 3. Averages multiple modes if present
/// 4. Uses mean until period filled
///
/// Key characteristics:
/// - Identifies most common values
/// - Handles multiple modes
/// - Robust to distribution shape
/// - Useful for discrete data
/// - Returns actual data points
///
/// Formula:
/// mode = value with highest frequency count
/// if multiple modes: average of mode values
///
/// Market Applications:
/// - Identify common price levels
/// - Detect support/resistance zones
/// - Analyze volume clusters
/// - Find price congestion areas
/// - Pattern recognition
///
/// Sources:
///     https://en.wikipedia.org/wiki/Mode_(statistics)
///     "Statistical Analysis in Financial Markets"
///
/// Note: Particularly useful for price level analysis
/// </remarks>
[SkipLocalsInit]
public sealed class Mode : AbstractBase
{
    private readonly int Period;
    private readonly CircularBuffer _buffer;
    private readonly Dictionary<double, int> _frequencies;
    private readonly List<double> _modes;
    private const double Epsilon = 1e-10;

    /// <param name="period">The number of points to consider for mode calculation.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Mode(int period)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        Period = period;
        WarmupPeriod = period;
        _buffer = new CircularBuffer(period);
        _frequencies = new Dictionary<double, int>();
        _modes = new List<double>();
        Name = $"Mode(period={period})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points to consider for mode calculation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Mode(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _buffer.Clear();
        _frequencies.Clear();
        _modes.Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Input.Value;
            _index++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void CountFrequencies(ReadOnlySpan<double> values)
    {
        _frequencies.Clear();
        for (int i = 0; i < values.Length; i++)
        {
            _frequencies[values[i]] = _frequencies.TryGetValue(values[i], out int count) ? count + 1 : 1;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void FindModes()
    {
        _modes.Clear();
        int maxCount = 0;

        foreach (var kvp in _frequencies)
        {
            if (kvp.Value > maxCount)
            {
                maxCount = kvp.Value;
                _modes.Clear();
                _modes.Add(kvp.Key);
            }
            else if (kvp.Value == maxCount)
            {
                _modes.Add(kvp.Key);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private double CalculateAverageMode()
    {
        double sum = 0;
        for (int i = 0; i < _modes.Count; i++)
        {
            sum += _modes[i];
        }
        return sum / _modes.Count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);
        _buffer.Add(Input.Value, Input.IsNew);

        double mode;
        if (_index >= Period)
        {
            ReadOnlySpan<double> values = _buffer.GetSpan();
            CountFrequencies(values);
            FindModes();
            mode = CalculateAverageMode();
        }
        else
        {
            // Use average until we have enough data points
            mode = _buffer.Average();
        }

        IsHot = _index >= WarmupPeriod;
        return mode;
    }
}
