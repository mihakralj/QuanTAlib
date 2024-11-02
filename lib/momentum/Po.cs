using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// PO: Price Oscillator
/// A momentum indicator that measures the difference between two moving averages
/// of different periods to identify price momentum and potential trend changes.
/// </summary>
/// <remarks>
/// The PO calculation process:
/// 1. Calculate fast EMA of closing prices
/// 2. Calculate slow EMA of closing prices
/// 3. Calculate the difference between fast and slow EMAs
/// 4. Multiply by a scaling factor for better visualization
///
/// Key characteristics:
/// - Measures momentum through moving average differences
/// - Helps identify trend direction and potential reversals
/// - Zero line crossovers signal trend changes
/// - Similar to MACD but more customizable periods
///
/// Formula:
/// FastMA = EMA(Close, FastPeriod)
/// SlowMA = EMA(Close, SlowPeriod)
/// PO = (FastMA - SlowMA) * ScalingFactor
///
/// Sources:
///     Technical Analysis of Financial Markets by John J. Murphy
/// </remarks>

[SkipLocalsInit]
public sealed class Po : AbstractBase
{
    private readonly Ema _fastEma;
    private readonly Ema _slowEma;
    private const double ScalingFactor = 1.0;
    private const int DefaultFastPeriod = 10;
    private const int DefaultSlowPeriod = 21;

    /// <param name="fastPeriod">The fast EMA period (default 10).</param>
    /// <param name="slowPeriod">The slow EMA period (default 21).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Po(int fastPeriod = DefaultFastPeriod, int slowPeriod = DefaultSlowPeriod)
    {
        if (fastPeriod < 1 || slowPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(fastPeriod));
        if (fastPeriod >= slowPeriod)
            throw new ArgumentException("Fast period must be less than slow period");

        _fastEma = new(fastPeriod);
        _slowEma = new(slowPeriod);
        WarmupPeriod = slowPeriod;
        Name = $"PO({fastPeriod},{slowPeriod})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="fastPeriod">The fast EMA period.</param>
    /// <param name="slowPeriod">The slow EMA period.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Po(object source, int fastPeriod, int slowPeriod) : this(fastPeriod, slowPeriod)
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
        return (fastEma - slowEma) * ScalingFactor;
    }
}
