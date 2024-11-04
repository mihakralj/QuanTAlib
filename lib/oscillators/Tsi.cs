using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// TSI: True Strength Index
/// A momentum oscillator that shows both trend direction and overbought/oversold conditions.
/// Uses two EMAs of price change momentum to help identify short-term trends and reversals.
/// </summary>
/// <remarks>
/// The TSI calculation process:
/// 1. Calculate price change (PC): Current close - Previous close
/// 2. Calculate absolute price change (APC): Absolute value of PC
/// 3. First smoothing: EMA1 of PC and EMA1 of APC
/// 4. Second smoothing: EMA2 of EMA1(PC) and EMA2 of EMA1(APC)
/// 5. TSI = 100 * (Double smoothed PC / Double smoothed APC)
///
/// Key characteristics:
/// - Oscillates around zero
/// - Shows momentum and trend direction
/// - Identifies overbought/oversold conditions
/// - Generates signals through centerline/signal line crossovers
/// - Shows momentum divergence with price
///
/// Formula:
/// TSI = 100 * (EMA2(EMA1(PC)) / EMA2(EMA1(APC)))
/// where:
/// PC = Current Price - Previous Price
/// APC = |PC|
/// Default periods: First EMA = 25, Second EMA = 13
///
/// Sources:
///     William Blau - "Momentum, Direction, and Divergence" (1995)
///     https://www.investopedia.com/terms/t/tsi.asp
///
/// Note: Default periods (25,13) were recommended by Blau
/// </remarks>
[SkipLocalsInit]
public sealed class Tsi : AbstractBase
{
    private readonly Ema _pcEma1;
    private readonly Ema _pcEma2;
    private readonly Ema _apcEma1;
    private readonly Ema _apcEma2;
    private double _prevPrice;
    private const int DefaultFirstPeriod = 25;
    private const int DefaultSecondPeriod = 13;
    private const double ScalingFactor = 100.0;

    /// <param name="firstPeriod">The first EMA smoothing period (default 25).</param>
    /// <param name="secondPeriod">The second EMA smoothing period (default 13).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Tsi(int firstPeriod = DefaultFirstPeriod, int secondPeriod = DefaultSecondPeriod)
    {
        if (firstPeriod < 1 || secondPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(firstPeriod), "All periods must be greater than 0");

        _pcEma1 = new(firstPeriod);
        _pcEma2 = new(secondPeriod);
        _apcEma1 = new(firstPeriod);
        _apcEma2 = new(secondPeriod);
        WarmupPeriod = firstPeriod + secondPeriod;
        Name = $"TSI({firstPeriod},{secondPeriod})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="firstPeriod">The first EMA smoothing period.</param>
    /// <param name="secondPeriod">The second EMA smoothing period.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Tsi(object source, int firstPeriod = DefaultFirstPeriod, int secondPeriod = DefaultSecondPeriod)
        : this(firstPeriod, secondPeriod)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            if (_index == 0)
                _prevPrice = Input.Value;
            _index++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        // Calculate price changes
        double priceChange = Input.Value - _prevPrice;
        double absPriceChange = Math.Abs(priceChange);

        if (Input.IsNew)
            _prevPrice = Input.Value;

        // First smoothing
        double smoothPc = _pcEma1.Calc(new TValue(Input.Time, priceChange, Input.IsNew));
        double smoothApc = _apcEma1.Calc(new TValue(Input.Time, absPriceChange, Input.IsNew));

        // Second smoothing
        double doubleSmoothedPc = _pcEma2.Calc(new TValue(Input.Time, smoothPc, Input.IsNew));
        double doubleSmoothedApc = _apcEma2.Calc(new TValue(Input.Time, smoothApc, Input.IsNew));

        // Calculate TSI
        return doubleSmoothedApc >= double.Epsilon ? ScalingFactor * (doubleSmoothedPc / doubleSmoothedApc) : 0;
    }
}
