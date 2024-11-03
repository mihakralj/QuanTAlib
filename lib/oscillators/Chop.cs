using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// CHOP: Choppiness Index
/// A technical indicator that measures the market's trendiness versus choppiness.
/// It helps determine if the market is trending or moving sideways by comparing
/// the total movement to the net directional movement over a period.
/// </summary>
/// <remarks>
/// The CHOP calculation process:
/// 1. Calculate ATR sum over period
/// 2. Calculate total price range over period
/// 3. Scale result to oscillate between 0 and 100
///
/// Key characteristics:
/// - Oscillates between 0 and 100
/// - Values above 61.8 indicate choppy market
/// - Values below 38.2 indicate trending market
/// - Based on ATR and price range
/// - Higher values = more choppy/sideways
/// - Lower values = more trending
///
/// Formula:
/// CHOP = 100 * LOG10(SUM(ATR,n)/(HIGH(n)-LOW(n))) / LOG10(n)
/// where:
/// n = period
/// ATR = Average True Range
/// HIGH(n) = Highest high over period n
/// LOW(n) = Lowest low over period n
///
/// Sources:
///     E.W. Dreiss
///     https://www.tradingview.com/support/solutions/43000501980-choppiness-index/
///
/// Note: Default period is 14
/// </remarks>
[SkipLocalsInit]
public sealed class Chop : AbstractBase
{
    private readonly Atr _atr;
    private readonly CircularBuffer _highs;
    private readonly CircularBuffer _lows;
    private readonly CircularBuffer _atrValues;
    private readonly double _logPeriod;
    private const int DefaultPeriod = 14;
    private const double ScalingFactor = 100.0;

    /// <param name="period">The number of periods used in the CHOP calculation (default 14).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Chop(int period = DefaultPeriod)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));

        _atr = new(period);
        _highs = new(period);
        _lows = new(period);
        _atrValues = new(period);
        _logPeriod = Math.Log10(period);
        WarmupPeriod = period;
        Name = $"CHOP({period})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of periods used in the CHOP calculation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Chop(object source, int period = DefaultPeriod) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
            _index++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(BarInput.IsNew);

        // Calculate ATR and store it
        double atr = _atr.Calc(BarInput);
        _atrValues.Add(atr, BarInput.IsNew);

        // Store high and low prices
        _highs.Add(BarInput.High, BarInput.IsNew);
        _lows.Add(BarInput.Low, BarInput.IsNew);

        // Calculate highest high and lowest low over period
        double highestHigh = _highs.Max();
        double lowestLow = _lows.Min();
        double range = highestHigh - lowestLow;

        // Calculate sum of ATR values
        double atrSum = _atrValues.Sum();

        // Avoid division by zero
        if (range < double.Epsilon || _logPeriod < double.Epsilon)
            return 0.0;

        // Calculate CHOP
        return ScalingFactor * Math.Log10(atrSum / range) / _logPeriod;
    }
}
