using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// VP: Volume Profile
/// A volume-based indicator that analyzes volume distribution across price levels.
/// It helps identify significant price levels where most trading activity occurs.
/// </summary>
/// <remarks>
/// The VP calculation process:
/// 1. Track volume at each price level within a period
/// 2. Calculate Point of Control (POC) - price with highest volume
/// 3. Calculate Value Area (70% of total volume)
///
/// Key characteristics:
/// - Price level analysis
/// - Volume distribution
/// - Support/resistance identification
/// - Trading activity concentration
/// - Market structure analysis
///
/// Formula:
/// VP = Σ Volume at each price level
/// POC = Price level with max volume
/// Value Area = Price range containing 70% of volume
///
/// Market Applications:
/// - Support/resistance levels
/// - Market structure analysis
/// - Trading activity patterns
/// - Price level significance
/// - Volume concentration
///
/// Note: Returns Point of Control (price level with highest volume)
/// </remarks>
[SkipLocalsInit]
public sealed class Vp : AbstractBase
{
    private readonly CircularBuffer _volumes;
    private readonly CircularBuffer _prices;
    private const int DefaultPeriod = 14;

    /// <param name="period">The number of periods to analyze volume distribution (default 14).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vp(int period = DefaultPeriod)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));

        _volumes = new(period);
        _prices = new(period);
        WarmupPeriod = period;
        Name = $"VP({period})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of periods to analyze volume distribution.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vp(object source, int period = DefaultPeriod) : this(period)
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
    private static int FindMaxVolumeIndex(CircularBuffer volumes)
    {
        int maxIndex = 0;
        double maxVolume = volumes[0];

        for (int i = 1; i < volumes.Count; i++)
        {
            if (volumes[i] > maxVolume)
            {
                maxVolume = volumes[i];
                maxIndex = i;
            }
        }

        return maxIndex;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(BarInput.IsNew);

        // Store volume and price
        _volumes.Add(BarInput.Volume, BarInput.IsNew);
        _prices.Add(BarInput.Close, BarInput.IsNew);

        // Find price level with highest volume (Point of Control)
        int pocIndex = FindMaxVolumeIndex(_volumes);
        return _prices[pocIndex];
    }
}
