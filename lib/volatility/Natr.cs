using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// NATR: Normalized Average True Range
/// A volatility indicator that expresses ATR as a percentage of closing price,
/// making it more comparable across different price levels.
/// </summary>
/// <remarks>
/// The NATR calculation process:
/// 1. Calculate True Range (TR):
///    TR = max(high-low, abs(high-prevClose), abs(low-prevClose))
/// 2. Calculate ATR using SMA of TR
/// 3. Normalize by dividing ATR by close price and multiply by 100
/// 4. Updates with each new price bar
///
/// Key characteristics:
/// - Normalized volatility measure
/// - Period-based average
/// - Trend independent
/// - Percentage-based measure
/// - Comparable across instruments
///
/// Formula:
/// TR = max(high-low, abs(high-prevClose), abs(low-prevClose))
/// ATR = SMA(TR, period)
/// NATR = (ATR / Close) * 100
///
/// Market Applications:
/// - Cross-market comparison
/// - Position sizing
/// - Volatility analysis
/// - Risk assessment
/// - Market regime identification
///
/// Note: More suitable for comparing volatility across different instruments than ATR
/// </remarks>
[SkipLocalsInit]
public sealed class Natr : AbstractBase
{
    private readonly Sma _ma;
    private double _prevClose;
    private const int DefaultPeriod = 14;

    /// <param name="period">The number of periods for NATR calculation (default 14).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Natr(int period = DefaultPeriod)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));

        _ma = new(period);
        WarmupPeriod = period;
        Name = $"NATR({period})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of periods for NATR calculation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Natr(object source, int period = DefaultPeriod) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _prevClose = BarInput.Close;
            _index++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(BarInput.IsNew);

        // Calculate True Range
        double hl = BarInput.High - BarInput.Low;
        double hc = Math.Abs(BarInput.High - _prevClose);
        double lc = Math.Abs(BarInput.Low - _prevClose);
        double tr = Math.Max(hl, Math.Max(hc, lc));

        // Calculate ATR
        double atr = _ma.Calc(tr, BarInput.IsNew);

        // Normalize ATR
        return (atr / BarInput.Close) * 100.0;
    }
}
