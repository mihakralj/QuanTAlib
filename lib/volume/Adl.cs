using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// ADL: Accumulation Distribution Line (Chaikin)
/// A volume-based indicator that measures the cumulative flow of money into and out
/// of a security. It assesses the relationship between price and volume to determine
/// buying/selling pressure.
/// </summary>
/// <remarks>
/// The ADL calculation process:
/// 1. Calculates Money Flow Multiplier (MFM):
///    MFM = ((Close - Low) - (High - Close)) / (High - Low)
/// 2. Calculates Money Flow Volume (MFV):
///    MFV = MFM × Volume
/// 3. ADL is cumulative sum of MFV values
///
/// Key characteristics:
/// - Volume-weighted measure
/// - Cumulative indicator
/// - No upper/lower bounds
/// - Trend confirmation tool
/// - Divergence indicator
///
/// Formula:
/// MFM = ((Close - Low) - (High - Close)) / (High - Low)
/// MFV = MFM × Volume
/// ADL = Previous ADL + MFV
///
/// Market Applications:
/// - Trend confirmation
/// - Volume analysis
/// - Price/volume divergence
/// - Support/resistance levels
/// - Market participation
///
/// Sources:
///     Marc Chaikin - Original development
///     https://www.investopedia.com/terms/a/accumulationdistribution.asp
///
/// Note: Focuses on the relationship between price and volume
/// </remarks>

[SkipLocalsInit]
public sealed class Adl : AbstractBase
{
    private double _cumulativeAdl;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Adl()
    {
        WarmupPeriod = 1;
        Name = "ADL";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Adl(object source) : this()
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _cumulativeAdl = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Value;
            _index++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double CalculateMoneyFlowMultiplier(double close, double high, double low)
    {
        double range = high - low;
        if (range > 0)
        {
            return ((close - low) - (high - close)) / range;
        }
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(BarInput.IsNew);

        // Calculate Money Flow Multiplier
        double mfm = CalculateMoneyFlowMultiplier(BarInput.Close, BarInput.High, BarInput.Low);

        // Calculate Money Flow Volume
        double mfv = mfm * BarInput.Volume;

        // Update cumulative ADL only for new bars
        if (BarInput.IsNew)
        {
            _cumulativeAdl += mfv;
        }

        IsHot = _index >= WarmupPeriod;
        return _cumulativeAdl;
    }
}
