using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// Mom: Momentum
/// A basic momentum indicator that measures the change in price over a specified
/// period, helping identify the strength and speed of price movements.
/// </summary>
/// <remarks>
/// The Momentum calculation process:
/// 1. Store historical prices in a circular buffer
/// 2. Calculate absolute difference between current and historical price
/// 3. No scaling factor applied to maintain raw price difference
///
/// Key characteristics:
/// - Basic momentum measurement
/// - Shows absolute price changes
/// - Zero line crossovers signal trend changes
/// - Foundation for other momentum indicators
///
/// Formula:
/// Mom = Price - PriceN
/// where PriceN is the price N periods ago
///
/// Sources:
///     Technical Analysis of Financial Markets by John J. Murphy
///     Technical Analysis Using Multiple Timeframes by Brian Shannon
/// </remarks>
[SkipLocalsInit]
public sealed class Mom : AbstractBase
{
    private readonly CircularBuffer _priceBuffer;
    private const int DefaultPeriod = 10;

    /// <param name="period">The lookback period for momentum calculation (default 10).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Mom(int period = DefaultPeriod)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));

        _priceBuffer = new(period + 1);
        WarmupPeriod = period;
        Name = $"MOM({period})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The lookback period for momentum calculation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Mom(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
            _priceBuffer.Add(Input.Value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        if (_priceBuffer.Count < _priceBuffer.Capacity)
            return 0.0;

        return Input.Value - _priceBuffer[0];
    }
}
