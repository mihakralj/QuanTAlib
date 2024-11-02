using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// VWMA: Volume Weighted Moving Average
/// A technical indicator that combines price and volume to show the average price
/// weighted by volume over a period. It gives more weight to prices with higher
/// volume, making it more responsive to high-volume price movements.
/// </summary>
/// <remarks>
/// The VWMA calculation process:
/// 1. Multiply price by volume for each period
/// 2. Sum (price * volume) over the period
/// 3. Sum volume over the period
/// 4. Divide sums to get weighted average
///
/// Key characteristics:
/// - Volume-sensitive average
/// - Trend indicator
/// - Support/resistance levels
/// - Price momentum
/// - Volume emphasis
///
/// Formula:
/// VWMA = Σ(Price * Volume) / ΣVolume
/// where sums are taken over the specified period
///
/// Market Applications:
/// - Trend identification
/// - Support/resistance levels
/// - Volume analysis
/// - Price momentum
/// - Trading signals
///
/// Note: More responsive to high-volume price movements
/// </remarks>

[SkipLocalsInit]
public sealed class Vwma : AbstractBase
{
    private readonly CircularBuffer _priceVolume;
    private readonly CircularBuffer _volume;
    private const int DefaultPeriod = 20;

    /// <param name="period">The number of periods for VWMA calculation (default 20).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vwma(int period = DefaultPeriod)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));

        _priceVolume = new(period);
        _volume = new(period);
        WarmupPeriod = period;
        Name = $"VWMA({period})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of periods for VWMA calculation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vwma(object source, int period = DefaultPeriod) : this(period)
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

        // Calculate and store price * volume
        double priceVolume = BarInput.Close * BarInput.Volume;
        _priceVolume.Add(priceVolume, BarInput.IsNew);
        _volume.Add(BarInput.Volume, BarInput.IsNew);

        // Calculate sums
        double sumPriceVolume = _priceVolume.Sum();
        double sumVolume = _volume.Sum();

        // Calculate VWMA
        return sumVolume > 0 ? sumPriceVolume / sumVolume : BarInput.Close;
    }
}
