using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// MIN: Minimum Value with Decay
/// A statistical measure that tracks the lowest value over a specified period,
/// with an optional decay factor to gradually reduce the influence of older lows.
/// This adaptive approach allows the indicator to respond to changing market conditions.
/// </summary>
/// <remarks>
/// The MIN calculation process:
/// 1. Tracks lowest value in current period
/// 2. Applies exponential decay to old lows
/// 3. Adjusts decay based on time since last low
/// 4. Caps result at current period's minimum
///
/// Key characteristics:
/// - Tracks absolute lowest values
/// - Optional decay for adaptivity
/// - Maintains historical context
/// - Smooth transitions with decay
/// - Period-based windowing
///
/// Formula:
/// decay = 1 - e^(-halfLife * timeSinceMin / period)
/// min = min + decay * (periodAverage - min)
/// min = max(min, periodMinimum)
///
/// Market Applications:
/// - Identify support levels
/// - Track price troughs
/// - Implement trailing stops
/// - Monitor price extremes
/// - Adaptive trend following
///
/// Sources:
///     Technical Analysis of Financial Markets
///     https://www.investopedia.com/terms/s/support.asp
///
/// Note: Decay factor allows for adaptive low tracking
/// </remarks>
[SkipLocalsInit]
public sealed class Min : AbstractBase
{
    private readonly int Period;
    private readonly CircularBuffer _buffer;
    private readonly double _halfLife;
    private double _currentMin;
    private double _p_currentMin;
    private int _timeSinceNewMin;
    private int _p_timeSinceNewMin;
    private const double DefaultDecay = 0.0;
    private const double DecayScaleFactor = 0.1;
    private const double Epsilon = 1e-10;

    /// <param name="period">The number of points to consider for minimum calculation.</param>
    /// <param name="decay">Half-life decay factor (0 for no decay, higher for faster forgetting).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1 or decay is negative.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Min(int period, double decay = DefaultDecay)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        if (decay < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(decay), "Half-life must be non-negative.");
        }
        Period = period;
        WarmupPeriod = 0;
        _buffer = new CircularBuffer(period);
        _halfLife = decay * DecayScaleFactor;
        Name = $"Min(period={period}, halfLife={decay:F2})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of points to consider for minimum calculation.</param>
    /// <param name="decay">Half-life decay factor (default 0).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Min(object source, int period, double decay = DefaultDecay) : this(period, decay)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _currentMin = double.MaxValue;
        _timeSinceNewMin = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _p_currentMin = _currentMin;
            _lastValidValue = Input.Value;
            _index++;
            _timeSinceNewMin++;
            _p_timeSinceNewMin = _timeSinceNewMin;
        }
        else
        {
            _currentMin = _p_currentMin;
            _timeSinceNewMin = _p_timeSinceNewMin;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private double CalculateDecayRate()
    {
        return 1 - Math.Exp(-_halfLife * _timeSinceNewMin / Period);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double FindMinValue(ReadOnlySpan<double> values)
    {
        double min = double.MaxValue;
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] < min)
            {
                min = values[i];
            }
        }
        return min;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);
        _buffer.Add(Input.Value, Input.IsNew);

        // Update minimum if new value is lower
        if (Input.Value <= _currentMin)
        {
            _currentMin = Input.Value;
            _timeSinceNewMin = 0;
        }

        // Apply decay based on time since last minimum
        double decayRate = CalculateDecayRate();
        _currentMin += decayRate * (_buffer.Average() - _currentMin);

        // Ensure minimum doesn't fall below current period's lowest value
        ReadOnlySpan<double> values = _buffer.GetSpan();
        _currentMin = Math.Max(_currentMin, FindMinValue(values));

        IsHot = true;
        return _currentMin;
    }
}
