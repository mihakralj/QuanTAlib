using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// MACD: Moving Average Convergence Divergence
/// A trend-following momentum indicator that shows the relationship between two moving
/// averages of an asset's price. MACD is calculated by subtracting the longer-period
/// EMA from the shorter-period EMA. The result is then used to calculate a signal line
/// (EMA of MACD) and histogram (MACD - Signal).
/// </summary>
/// <remarks>
/// The MACD calculation process:
/// 1. Calculate the fast EMA (default 12 periods)
/// 2. Calculate the slow EMA (default 26 periods)
/// 3. MACD Line = Fast EMA - Slow EMA
/// 4. Signal Line = EMA of MACD Line (default 9 periods)
/// 5. MACD Histogram = MACD Line - Signal Line
///
/// Key characteristics:
/// - Centerline crossovers signal trend changes
/// - Signal line crossovers indicate trading opportunities
/// - Histogram shows momentum of price movement
/// - Divergences can signal potential reversals
///
/// Formula:
/// MACD Line = EMA(fast) - EMA(slow)
/// Signal Line = EMA(MACD Line, signal)
/// Histogram = MACD Line - Signal Line
///
/// Sources:
///     https://www.investopedia.com/terms/m/macd.asp
///     https://school.stockcharts.com/doku.php?id=technical_indicators:macd
/// </remarks>
[SkipLocalsInit]
public sealed class Macd : AbstractBase
{
    private readonly Ema _fastEma;
    private readonly Ema _slowEma;
    private readonly Ema _signalEma;
    private const int DefaultFastPeriod = 12;
    private const int DefaultSlowPeriod = 26;
    private const int DefaultSignalPeriod = 9;
    private double _macdLine;
    private double _signalLine;

    /// <summary>
    /// Gets the MACD line value (Fast EMA - Slow EMA)
    /// </summary>
    public double MacdLine => _macdLine;

    /// <summary>
    /// Gets the Signal line value (EMA of MACD line)
    /// </summary>
    public double SignalLine => _signalLine;

    /// <param name="fastPeriod">The number of periods for the fast EMA (default 12).</param>
    /// <param name="slowPeriod">The number of periods for the slow EMA (default 26).</param>
    /// <param name="signalPeriod">The number of periods for the signal line EMA (default 9).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Macd(int fastPeriod = DefaultFastPeriod, int slowPeriod = DefaultSlowPeriod, int signalPeriod = DefaultSignalPeriod)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(fastPeriod, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(slowPeriod, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(signalPeriod, 1);

        if (fastPeriod >= slowPeriod)
        {
            throw new ArgumentOutOfRangeException(nameof(fastPeriod), "Fast period must be less than slow period");
        }

        _fastEma = new(fastPeriod);
        _slowEma = new(slowPeriod);
        _signalEma = new(signalPeriod);
        WarmupPeriod = slowPeriod + signalPeriod;
        Name = $"MACD({fastPeriod},{slowPeriod},{signalPeriod})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="fastPeriod">The number of periods for the fast EMA.</param>
    /// <param name="slowPeriod">The number of periods for the slow EMA.</param>
    /// <param name="signalPeriod">The number of periods for the signal line EMA.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Macd(object source, int fastPeriod, int slowPeriod, int signalPeriod) : this(fastPeriod, slowPeriod, signalPeriod)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
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
        ManageState(Input.IsNew);

        // Calculate MACD line
        double fastEma = _fastEma.Calc(Input.Value, Input.IsNew);
        double slowEma = _slowEma.Calc(Input.Value, Input.IsNew);
        _macdLine = fastEma - slowEma;

        // Calculate Signal line
        _signalLine = _signalEma.Calc(_macdLine, Input.IsNew);

        // Return histogram
        return _macdLine - _signalLine;
    }
}
