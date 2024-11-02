using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// VWAP: Volume Weighted Average Price
/// A trading benchmark that shows the ratio of the value traded to total volume
/// traded over a specific period. VWAP equals the dollar value of all trading
/// periods divided by the total trading volume for the current day.
/// </summary>
/// <remarks>
/// The VWAP calculation process:
/// 1. Calculate typical price for each period
/// 2. Multiply typical price by volume
/// 3. Calculate cumulative values
/// 4. Divide cumulative (price * volume) by cumulative volume
///
/// Key characteristics:
/// - Intraday trading benchmark
/// - Volume-weighted measure
/// - Institutional trading reference
/// - Price momentum indicator
/// - Trading efficiency measure
///
/// Formula:
/// VWAP = Σ(Price * Volume) / ΣVolume
/// where Price = (High + Low + Close)/3
///
/// Market Applications:
/// - Best execution analysis
/// - Trading algorithms
/// - Price momentum
/// - Market impact analysis
/// - Order timing
///
/// Sources:
///     https://www.investopedia.com/terms/v/vwap.asp
///
/// Note: Commonly used by institutional traders
/// </remarks>

[SkipLocalsInit]
public sealed class Vwap : AbstractBase
{
    private double _cumulativeTPV; // Cumulative (Typical Price * Volume)
    private double _cumulativeVolume;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vwap()
    {
        WarmupPeriod = 1;
        Name = "VWAP";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vwap(object source) : this()
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _cumulativeTPV = 0;
        _cumulativeVolume = 0;
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

        // Update cumulative values only for new bars
        if (BarInput.IsNew)
        {
            _cumulativeTPV += BarInput.HLC3 * BarInput.Volume;
            _cumulativeVolume += BarInput.Volume;
        }

        // Calculate VWAP
        return _cumulativeVolume > 0 ? _cumulativeTPV / _cumulativeVolume : BarInput.HLC3;
    }
}
