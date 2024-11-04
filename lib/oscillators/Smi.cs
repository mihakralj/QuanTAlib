using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// SMI: Stochastic Momentum Index
/// A double-smoothed momentum indicator that shows where the close is relative
/// to the midpoint of the recent high/low range. It helps identify overbought
/// and oversold conditions with higher accuracy than traditional stochastics.
/// </summary>
/// <remarks>
/// The SMI calculation process:
/// 1. Calculate median price distance (Close - (High + Low)/2)
/// 2. Calculate highest high and lowest low over period
/// 3. First smoothing of median distance and range
/// 4. Second smoothing of first smoothed values
/// 5. Scale to percentage (-100 to +100)
///
/// Key characteristics:
/// - Oscillates between -100 and +100
/// - Double smoothing reduces noise
/// - Traditional overbought level at +40
/// - Traditional oversold level at -40
/// - Centerline crossovers signal trend changes
///
/// Formula:
/// D = Close - (High + Low)/2
/// HL = Highest High - Lowest Low
/// First smoothing:
///   SD = EMA(EMA(D, period1), period2)
///   SHL = EMA(EMA(HL, period1), period2)
/// SMI = 100 * (SD / (SHL/2))
///
/// Sources:
///     William Blau - "Momentum, Direction, and Divergence" (1995)
///     https://www.tradingview.com/scripts/stochasticmomentumindex/
///
/// Note: Default periods (10,3,3) are commonly used values
/// </remarks>
[SkipLocalsInit]
public sealed class Smi : AbstractBase
{
    private readonly CircularBuffer _highs;
    private readonly CircularBuffer _lows;
    private readonly Ema _dEma1;
    private readonly Ema _dEma2;
    private readonly Ema _hlEma1;
    private readonly Ema _hlEma2;
    private const int DefaultPeriod = 10;
    private const int DefaultSmooth1 = 3;
    private const int DefaultSmooth2 = 3;
    private const double ScalingFactor = 100.0;

    /// <param name="period">The lookback period (default 10).</param>
    /// <param name="smooth1">First smoothing period (default 3).</param>
    /// <param name="smooth2">Second smoothing period (default 3).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Smi(int period = DefaultPeriod, int smooth1 = DefaultSmooth1, int smooth2 = DefaultSmooth2)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than 0");
        if (smooth1 < 1)
            throw new ArgumentOutOfRangeException(nameof(smooth1), "Smooth1 must be greater than 0");
        if (smooth2 < 1)
            throw new ArgumentOutOfRangeException(nameof(smooth2), "Smooth2 must be greater than 0");

        _highs = new(period);
        _lows = new(period);
        _dEma1 = new(smooth1);
        _dEma2 = new(smooth2);
        _hlEma1 = new(smooth1);
        _hlEma2 = new(smooth2);
        WarmupPeriod = period + smooth1 + smooth2;
        Name = $"SMI({period},{smooth1},{smooth2})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The lookback period.</param>
    /// <param name="smooth1">First smoothing period.</param>
    /// <param name="smooth2">Second smoothing period.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Smi(object source, int period = DefaultPeriod, int smooth1 = DefaultSmooth1, int smooth2 = DefaultSmooth2)
        : this(period, smooth1, smooth2)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _highs.Add(BarInput.High);
            _lows.Add(BarInput.Low);
            _index++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(BarInput.IsNew);

        // Calculate median price distance and range
        double midpoint = (BarInput.High + BarInput.Low) / 2.0;
        double distance = BarInput.Close - midpoint;
        double range = _highs.Max() - _lows.Min();

        // First smoothing
        double smoothD1 = _dEma1.Calc(new TValue(BarInput.Time, distance, BarInput.IsNew));
        double smoothHL1 = _hlEma1.Calc(new TValue(BarInput.Time, range, BarInput.IsNew));

        // Second smoothing
        double smoothD2 = _dEma2.Calc(new TValue(BarInput.Time, smoothD1, BarInput.IsNew));
        double smoothHL2 = _hlEma2.Calc(new TValue(BarInput.Time, smoothHL1, BarInput.IsNew));

        // Calculate SMI
        return smoothHL2 >= double.Epsilon ? ScalingFactor * (smoothD2 / (smoothHL2 / 2.0)) : 0;
    }
}
