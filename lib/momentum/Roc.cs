using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// ROC: Rate of Change
/// A momentum indicator that measures the percentage change in price over a specified
/// period, helping identify the speed and strength of price movements.
/// </summary>
/// <remarks>
/// The ROC calculation process:
/// 1. Store historical prices in a circular buffer
/// 2. Calculate percentage change between current and historical price
/// 3. Multiply by scaling factor for better visualization
///
/// Key characteristics:
/// - Pure momentum indicator
/// - Oscillates around zero line
/// - Helps identify overbought/oversold conditions
/// - Useful for divergence analysis
///
/// Formula:
/// ROC = ((Price - PriceN) / PriceN) * 100
/// where PriceN is the price N periods ago
///
/// Sources:
///     Technical Analysis of Financial Markets by John J. Murphy
///     Technical Analysis of Stock Trends by Robert D. Edwards and John Magee
/// </remarks>

[SkipLocalsInit]
public sealed class Roc : AbstractBase
{
    private readonly CircularBuffer _priceBuffer;
    private const double ScalingFactor = 100.0;
    private const int DefaultPeriod = 12;

    /// <param name="period">The lookback period for ROC calculation (default 12).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Roc(int period = DefaultPeriod)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));

        _priceBuffer = new(period + 1);
        WarmupPeriod = period;
        Name = $"ROC({period})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The lookback period for ROC calculation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Roc(object source, int period) : this(period)
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

        double oldPrice = _priceBuffer[0];
        if (oldPrice <= double.Epsilon)
            return 0.0;

        return ((Input.Value - oldPrice) / oldPrice) * ScalingFactor;
    }
}
