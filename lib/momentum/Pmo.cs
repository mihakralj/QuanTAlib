using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// PMO: Price Momentum Oscillator
/// A momentum indicator that uses exponential moving averages of ROC (Rate of Change)
/// to identify overbought and oversold conditions in price movements.
/// </summary>
/// <remarks>
/// The PMO calculation process:
/// 1. Calculate ROC (Rate of Change) of closing prices
/// 2. Apply a first smoothing EMA to the ROC values
/// 3. Apply a second smoothing EMA to the result
/// 4. Multiply by a scaling factor for better visualization
///
/// Key characteristics:
/// - Double-smoothed momentum indicator
/// - Helps identify overbought/oversold conditions
/// - Useful for trend confirmation and divergence analysis
/// - More responsive than traditional momentum oscillators
///
/// Formula:
/// ROC = (Close - PrevClose) / PrevClose
/// Signal1 = EMA(ROC, Period1)
/// PMO = EMA(Signal1, Period2) * ScalingFactor
///
/// Sources:
///     Developed by Carl Swenlin
///     Technical Analysis of Stocks and Commodities magazine
/// </remarks>
[SkipLocalsInit]
public sealed class Pmo : AbstractBase
{
    private readonly Ema _smoothing1;
    private readonly Ema _smoothing2;
    private double _prevClose;
    private double _p_prevClose;
    private const double ScalingFactor = 100.0;
    private const int DefaultPeriod1 = 35;
    private const int DefaultPeriod2 = 20;

    /// <param name="period1">The first smoothing period (default 35).</param>
    /// <param name="period2">The second smoothing period (default 20).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Pmo(int period1 = DefaultPeriod1, int period2 = DefaultPeriod2)
    {
        if (period1 < 1 || period2 < 1)
            throw new ArgumentOutOfRangeException(nameof(period1));

        _smoothing1 = new(period1);
        _smoothing2 = new(period2);
        _index = 0;
        WarmupPeriod = period1 + period2;
        Name = $"PMO({period1},{period2})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period1">The first smoothing period.</param>
    /// <param name="period2">The second smoothing period.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Pmo(object source, int period1, int period2) : this(period1, period2)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _index++;
            _p_prevClose = _prevClose;
        }
        else
        {
            _prevClose = _p_prevClose;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        if (_index == 1)
        {
            _prevClose = Input.Value;
            return 0.0;
        }

        // Calculate Rate of Change
        double roc = (Input.Value - _prevClose) / _prevClose;
        _prevClose = Input.Value;

        // Apply double smoothing
        double signal1 = _smoothing1.Calc(roc, Input.IsNew);
        return _smoothing2.Calc(signal1, Input.IsNew) * ScalingFactor;
    }
}
