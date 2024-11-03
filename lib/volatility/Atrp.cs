using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// ATRP: Average True Range Percent
/// A volatility indicator that expresses ATR as a percentage of current price.
/// This normalization allows for comparison across different price levels and
/// instruments.
/// </summary>
/// <remarks>
/// The ATRP calculation process:
/// 1. Calculate ATR normally
/// 2. Divide by current price
/// 3. Multiply by 100 for percentage
///
/// Key characteristics:
/// - Normalized volatility measure
/// - Price-independent comparison
/// - Percentage output
/// - Cross-market analysis
/// - Relative volatility measure
///
/// Formula:
/// ATRP = (ATR / Close) * 100
///
/// Market Applications:
/// - Cross-market comparison
/// - Position sizing
/// - Volatility analysis
/// - Risk assessment
/// - Market comparison
///
/// Note: More suitable for comparing different instruments than raw ATR
/// </remarks>
[SkipLocalsInit]
public sealed class Atrp : AbstractBase
{
    private readonly Atr _atr;
    private const int DefaultPeriod = 14;
    private const double ScalingFactor = 100.0;

    /// <param name="period">The number of periods for ATR calculation (default 14).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Atrp(int period = DefaultPeriod)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));

        _atr = new(period);
        WarmupPeriod = period;
        Name = $"ATRP({period})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of periods for ATR calculation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Atrp(object source, int period = DefaultPeriod) : this(period)
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

        // Calculate ATR
        double atr = _atr.Calc(BarInput);

        // Convert to percentage of price
        return Math.Abs(BarInput.Close) > double.Epsilon
            ? (atr / BarInput.Close) * ScalingFactor
            : 0.0;
    }
}
