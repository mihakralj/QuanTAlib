using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// APO: Absolute Price Oscillator
/// A momentum indicator that measures the absolute difference between two moving
/// averages of different periods. APO helps identify trend direction and potential
/// reversals by showing the momentum of price movement.
/// </summary>
/// <remarks>
/// The APO calculation process:
/// 1. Calculate fast period moving average
/// 2. Calculate slow period moving average
/// 3. Calculate absolute difference between the two averages
///
/// Key characteristics:
/// - Oscillates above and below zero
/// - Positive values indicate upward price momentum
/// - Negative values indicate downward price momentum
/// - Zero line crossovers signal potential trend changes
/// - Similar to MACD but uses simple moving averages
///
/// Formula:
/// APO = Fast MA - Slow MA
/// where:
/// Fast MA = Moving average of shorter period
/// Slow MA = Moving average of longer period
///
/// Sources:
///     https://www.investopedia.com/terms/p/ppo.asp
///     https://school.stockcharts.com/doku.php?id=technical_indicators:price_oscillators_ppo
///
/// Note: Default periods are 12 and 26, similar to MACD
/// </remarks>

[SkipLocalsInit]
public sealed class Apo : AbstractBase
{
    private readonly Sma _fastMa;
    private readonly Sma _slowMa;
    private const int DefaultFastPeriod = 12;
    private const int DefaultSlowPeriod = 26;

    /// <param name="fastPeriod">The number of periods for the fast moving average (default 12).</param>
    /// <param name="slowPeriod">The number of periods for the slow moving average (default 26).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Apo(int fastPeriod = DefaultFastPeriod, int slowPeriod = DefaultSlowPeriod)
    {
        if (fastPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(fastPeriod));
        if (slowPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(slowPeriod));
        if (fastPeriod >= slowPeriod)
            throw new ArgumentException("Fast period must be less than slow period");

        _fastMa = new(fastPeriod);
        _slowMa = new(slowPeriod);
        WarmupPeriod = slowPeriod;
        Name = $"APO({fastPeriod},{slowPeriod})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="fastPeriod">The number of periods for the fast moving average.</param>
    /// <param name="slowPeriod">The number of periods for the slow moving average.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Apo(object source, int fastPeriod, int slowPeriod) : this(fastPeriod, slowPeriod)
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

        // Calculate both moving averages
        double fastMa = _fastMa.Calc(Input.Value, Input.IsNew);
        double slowMa = _slowMa.Calc(Input.Value, Input.IsNew);

        // Calculate absolute difference
        return fastMa - slowMa;
    }
}
