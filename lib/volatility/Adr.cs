using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// ADR: Average Daily Range
/// A volatility indicator that measures the average range of price movement over
/// a specified period. It helps identify normal trading ranges and potential
/// breakout levels.
/// </summary>
/// <remarks>
/// The ADR calculation process:
/// 1. Calculate daily range (High - Low)
/// 2. Apply SMA to daily ranges
/// 3. Updates with each new price bar
///
/// Key characteristics:
/// - Simple volatility measure
/// - Period-based average
/// - Trend independent
/// - Absolute price measure
/// - Support/resistance aid
///
/// Formula:
/// Daily Range = High - Low
/// ADR = SMA(Daily Range, period)
///
/// Market Applications:
/// - Position sizing
/// - Volatility analysis
/// - Support/resistance levels
/// - Breakout identification
/// - Risk assessment
///
/// Note: Simpler alternative to ATR, doesn't consider gaps
/// </remarks>

[SkipLocalsInit]
public sealed class Adr : AbstractBase
{
    private readonly Sma _ma;
    private const int DefaultPeriod = 14;

    /// <param name="period">The number of periods for ADR calculation (default 14).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Adr(int period = DefaultPeriod)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));

        _ma = new(period);
        WarmupPeriod = period;
        Name = $"ADR({period})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of periods for ADR calculation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Adr(object source, int period = DefaultPeriod) : this(period)
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

        // Calculate daily range
        double range = BarInput.High - BarInput.Low;

        // Apply SMA smoothing
        return _ma.Calc(range, BarInput.IsNew);
    }
}
