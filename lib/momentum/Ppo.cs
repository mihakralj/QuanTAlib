using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// PPO: Percentage Price Oscillator
/// A momentum indicator that shows the percentage difference between two moving averages
/// of different periods, helping identify price momentum and potential trend changes.
/// </summary>
/// <remarks>
/// The PPO calculation process:
/// 1. Calculate fast EMA of closing prices
/// 2. Calculate slow EMA of closing prices
/// 3. Calculate the percentage difference between fast and slow EMAs
/// 4. Multiply by a scaling factor for better visualization
///
/// Key characteristics:
/// - Measures momentum through percentage differences
/// - Normalized for comparison across different price levels
/// - Zero line crossovers signal trend changes
/// - Similar to MACD but expressed as a percentage
///
/// Formula:
/// FastMA = EMA(Close, FastPeriod)
/// SlowMA = EMA(Close, SlowPeriod)
/// PPO = ((FastMA - SlowMA) / SlowMA) * 100
///
/// Sources:
///     Technical Analysis of Financial Markets by John J. Murphy
///     StockCharts.com Technical Indicators
/// </remarks>
[SkipLocalsInit]
public sealed class Ppo : AbstractBase
{
    private readonly Ema _fastEma;
    private readonly Ema _slowEma;
    private const double ScalingFactor = 100.0;
    private const int DefaultFastPeriod = 12;
    private const int DefaultSlowPeriod = 26;

    /// <param name="fastPeriod">The fast EMA period (default 12).</param>
    /// <param name="slowPeriod">The slow EMA period (default 26).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Ppo(int fastPeriod = DefaultFastPeriod, int slowPeriod = DefaultSlowPeriod)
    {
        if (fastPeriod < 1 || slowPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(fastPeriod));
        if (fastPeriod >= slowPeriod)
            throw new ArgumentException("Fast period must be less than slow period");

        _fastEma = new(fastPeriod);
        _slowEma = new(slowPeriod);
        WarmupPeriod = slowPeriod;
        Name = $"PPO({fastPeriod},{slowPeriod})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="fastPeriod">The fast EMA period.</param>
    /// <param name="slowPeriod">The slow EMA period.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Ppo(object source, int fastPeriod, int slowPeriod) : this(fastPeriod, slowPeriod)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        // No state management needed for this indicator
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        double fastEma = _fastEma.Calc(Input.Value, Input.IsNew);
        double slowEma = _slowEma.Calc(Input.Value, Input.IsNew);

        if (Math.Abs(slowEma) <= double.Epsilon)
            return 0.0;

        return ((fastEma - slowEma) / slowEma) * ScalingFactor;
    }
}
