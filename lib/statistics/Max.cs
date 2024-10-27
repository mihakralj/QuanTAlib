using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// MAX: Maximum Value with Decay
/// A statistical measure that tracks the highest value over a specified period,
/// with an optional decay factor to gradually reduce the influence of older peaks.
/// This adaptive approach allows the indicator to respond to changing market conditions.
/// </summary>
/// <remarks>
/// The MAX calculation process:
/// 1. Tracks highest value in current period
/// 2. Applies exponential decay to old peaks
/// 3. Adjusts decay based on time since last peak
/// 4. Caps result at current period's maximum
///
/// Key characteristics:
/// - Tracks absolute highest values
/// - Optional decay for adaptivity
/// - Maintains historical context
/// - Smooth transitions with decay
/// - Period-based windowing
///
/// Formula:
/// decay = 1 - e^(-halfLife * timeSinceMax / period)
/// max = max - decay * (max - periodAverage)
/// max = min(max, periodMaximum)
///
/// Market Applications:
/// - Identify resistance levels
/// - Track price peaks
/// - Implement trailing stops
/// - Monitor price extremes
/// - Adaptive trend following
///
/// Sources:
///     Technical Analysis of Financial Markets
///     https://www.investopedia.com/terms/r/resistance.asp
///
/// Note: Decay factor allows for adaptive peak tracking
/// </remarks>

[SkipLocalsInit]
public sealed class Max : AbstractBase
{
    private readonly int Period;
    private readonly CircularBuffer _buffer;
    private readonly double _halfLife;
    private double _currentMax;
    private double _p_currentMax;
    private int _timeSinceNewMax;
    private int _p_timeSinceNewMax;
    private const double DefaultDecay = 0.0;
    private const double DecayScaleFactor = 0.1;
    private const double Epsilon = 1e-10;

    /// <param name="period">The number of points to consider for maximum calculation.</param>
    /// <param name="decay">Half-life decay factor (0 for no decay, higher for faster forgetting).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1 or decay is negative.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Max(int period, double decay = DefaultDecay)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period),
                "Period must be greater than or equal to 1.");
        }
        if (decay < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(decay),
                "Half-life must be non-negative.");
        }
        Period = period;
        WarmupPeriod = 0;
        _buffer = new CircularBuffer(period);
        _halfLife = decay * DecayScaleFactor;
        Name = $"Max(period={period}, halfLife={decay:F2})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points to consider for maximum calculation.</param>
    /// <param name="decay">Half-life decay factor (default 0).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Max(object source, int period, double decay = DefaultDecay) : this(period, decay)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _currentMax = double.MinValue;
        _timeSinceNewMax = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _p_currentMax = _currentMax;
            _lastValidValue = Input.Value;
            _index++;
            _timeSinceNewMax++;
            _p_timeSinceNewMax = _timeSinceNewMax;
        }
        else
        {
            _currentMax = _p_currentMax;
            _timeSinceNewMax = _p_timeSinceNewMax;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private double CalculateDecayRate()
    {
        return 1 - Math.Exp(-_halfLife * _timeSinceNewMax / Period);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double FindMaxValue(ReadOnlySpan<double> values)
    {
        double max = double.MinValue;
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] > max)
            {
                max = values[i];
            }
        }
        return max;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);
        _buffer.Add(Input.Value, Input.IsNew);

        // Update maximum if new value is higher
        if (Input.Value >= _currentMax)
        {
            _currentMax = Input.Value;
            _timeSinceNewMax = 0;
        }

        // Apply decay based on time since last maximum
        double decayRate = CalculateDecayRate();
        _currentMax -= decayRate * (_currentMax - _buffer.Average());

        // Ensure maximum doesn't exceed current period's highest value
        ReadOnlySpan<double> values = _buffer.GetSpan();
        _currentMax = Math.Min(_currentMax, FindMaxValue(values));

        IsHot = true;
        return _currentMax;
    }
}
