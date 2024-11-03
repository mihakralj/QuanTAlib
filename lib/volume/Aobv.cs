using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// AOBV: Archer On-Balance Volume
/// A modified version of the traditional On-Balance Volume (OBV) indicator that uses a more
/// sophisticated method to determine buying and selling pressure. It considers both the
/// closing price and the price range to provide a more nuanced view of volume flow.
/// </summary>
/// <remarks>
/// The AOBV calculation process:
/// 1. Determine price position within the day's range
/// 2. Apply volume based on price position:
///    - If close is in upper 1/3 of range: Add full volume
///    - If close is in middle 1/3 of range: Add/subtract half volume
///    - If close is in lower 1/3 of range: Subtract full volume
///
/// Key characteristics:
/// - Volume-weighted measure
/// - Cumulative indicator
/// - No upper/lower bounds
/// - More nuanced than traditional OBV
/// - Considers price position in range
///
/// Formula:
/// Range = High - Low
/// UpperThird = High - (Range / 3)
/// LowerThird = Low + (Range / 3)
/// If Close >= UpperThird:
///     AOBV = Previous AOBV + Volume
/// Else if Close <= LowerThird:
///     AOBV = Previous AOBV - Volume
/// Else:
///     If Close > Previous Close:
///         AOBV = Previous AOBV + (Volume / 2)
///     Else:
///         AOBV = Previous AOBV - (Volume / 2)
///
/// Market Applications:
/// - Trend confirmation
/// - Volume analysis
/// - Price/volume divergence
/// - Support/resistance levels
/// - Market participation
///
/// Sources:
///     Steve Archer - Original development
///     Technical Analysis of Stock Trends (Edwards, Magee)
///
/// Note: Provides a more detailed analysis of volume flow than traditional OBV
/// </remarks>
[SkipLocalsInit]
public sealed class Aobv : AbstractBase
{
    private double _cumulativeAobv;
    private double _prevClose;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Aobv()
    {
        WarmupPeriod = 1;
        Name = "AOBV";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Aobv(object source) : this()
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _cumulativeAobv = 0;
        _prevClose = 0;
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

        double range = BarInput.High - BarInput.Low;
        if (range > 0)
        {
            double upperThird = BarInput.High - (range / 3);
            double lowerThird = BarInput.Low + (range / 3);

            // Determine volume flow based on price position
            if (BarInput.Close >= upperThird)
            {
                _cumulativeAobv += BarInput.Volume;
            }
            else if (BarInput.Close <= lowerThird)
            {
                _cumulativeAobv -= BarInput.Volume;
            }
            else
            {
                // In middle third, use half volume based on close comparison
                _cumulativeAobv += (BarInput.Close > _prevClose) ?
                    (BarInput.Volume / 2) : -(BarInput.Volume / 2);
            }
        }

        _prevClose = BarInput.Close;

        IsHot = _index >= WarmupPeriod;
        return _cumulativeAobv;
    }
}
