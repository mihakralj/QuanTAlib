using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// PVR: Price Volume Rank
/// A technical indicator that ranks price and volume movements to identify
/// significant market moves based on their combined strength.
/// </summary>
/// <remarks>
/// The PVR calculation process:
/// 1. Calculate price change percentage:
///    Price Change = ((Close - Previous Close) / Previous Close) * 100
/// 2. Calculate volume ratio:
///    Volume Ratio = Current Volume / Previous Volume
/// 3. Calculate PVR:
///    PVR = Price Change * Volume Ratio
///
/// Key characteristics:
/// - Combines price and volume analysis
/// - No specific boundaries
/// - Measures movement significance
/// - Volume-weighted price change
/// - Identifies strong moves
///
/// Formula:
/// Price Change = ((Close - Previous Close) / Previous Close) * 100
/// Volume Ratio = Volume / Previous Volume
/// PVR = Price Change * Volume Ratio
///
/// Market Applications:
/// - Significant move identification
/// - Volume-supported moves
/// - Trend strength analysis
/// - Breakout confirmation
/// - Market momentum measurement
///
/// Note: Higher absolute values indicate more significant price moves with volume support
/// </remarks>

[SkipLocalsInit]
public sealed class Pvr : AbstractBase
{
    private double _prevClose;
    private double _prevVolume;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Pvr()
    {
        WarmupPeriod = 2;  // Need previous close and volume
        Name = "PVR";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Pvr(object source) : this()
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _prevClose = 0;
        _prevVolume = 0;
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

        // Skip first period to establish previous values
        if (_index == 1)
        {
            _prevClose = BarInput.Close;
            _prevVolume = BarInput.Volume;
            return 0;
        }

        // Calculate price change percentage
        double priceChange = (Math.Abs(_prevClose) > double.Epsilon) ? ((BarInput.Close - _prevClose) / _prevClose) * 100 : 0;

        // Calculate volume ratio
        double volumeRatio = (Math.Abs(_prevVolume) > double.Epsilon) ? BarInput.Volume / _prevVolume : 1;

        // Store current values for next calculation
        _prevClose = BarInput.Close;
        _prevVolume = BarInput.Volume;

        // Calculate PVR
        double pvr = priceChange * volumeRatio;

        IsHot = _index >= WarmupPeriod;
        return pvr;
    }
}
