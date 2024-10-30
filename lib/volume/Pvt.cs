using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// PVT: Price Volume Trend
/// A momentum indicator that combines price and volume to determine the strength of a trend.
/// Similar to OBV but uses percentage price changes in its calculation.
/// </summary>
/// <remarks>
/// The PVT calculation process:
/// 1. Calculate price change percentage:
///    Price Change = (Close - Previous Close) / Previous Close
/// 2. Calculate PVT:
///    PVT = Previous PVT + (Price Change * Volume)
///
/// Key characteristics:
/// - Cumulative indicator
/// - Volume-weighted price changes
/// - No upper or lower bounds
/// - Trend strength measure
/// - More sensitive than OBV
///
/// Formula:
/// Price Change = (Close - Previous Close) / Previous Close
/// PVT = Previous PVT + (Price Change * Volume)
///
/// Market Applications:
/// - Trend confirmation
/// - Divergence analysis
/// - Volume-price relationships
/// - Support/resistance levels
/// - Market momentum
///
/// Sources:
///     Norman Fosback - Original development
///     https://www.investopedia.com/terms/p/pvt.asp
///
/// Note: Rising PVT suggests buying pressure, while falling PVT suggests selling pressure
/// </remarks>

[SkipLocalsInit]
public sealed class Pvt : AbstractBase
{
    private double _prevClose;
    private double _prevPvt;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Pvt()
    {
        WarmupPeriod = 2;  // Need previous close
        Name = "PVT";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Pvt(object source) : this()
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _prevClose = 0;
        _prevPvt = 0;
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
    protected override double Calculation()
    {
        ManageState(BarInput.IsNew);

        // Skip first period to establish previous close
        if (_index == 1)
        {
            _prevClose = BarInput.Close;
            return 0;
        }

        // Calculate price change percentage
        double priceChange = (Math.Abs(_prevClose) > double.Epsilon) ? (BarInput.Close - _prevClose) / _prevClose : 0;

        // Calculate PVT
        _prevPvt += priceChange * BarInput.Volume;

        // Store current close for next calculation
        _prevClose = BarInput.Close;

        IsHot = _index >= WarmupPeriod;
        return _prevPvt;
    }
}
