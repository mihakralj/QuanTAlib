using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// PVOL: Price-Volume
/// A technical indicator that measures the relationship between price and volume changes,
/// helping to identify the strength of price movements.
/// </summary>
/// <remarks>
/// The PVOL calculation process:
/// 1. Calculate price change:
///    Price Change = (Close - Previous Close) / Previous Close
/// 2. Calculate volume change:
///    Volume Change = (Volume - Previous Volume) / Previous Volume
/// 3. Calculate PVOL:
///    PVOL = Price Change * Volume Change * 100
///
/// Key characteristics:
/// - Measures price-volume relationship
/// - Oscillates around zero
/// - Shows momentum strength
/// - Identifies volume-supported moves
/// - No specific boundaries
///
/// Formula:
/// Price Change = (Close - Previous Close) / Previous Close
/// Volume Change = (Volume - Previous Volume) / Previous Volume
/// PVOL = Price Change * Volume Change * 100
///
/// Market Applications:
/// - Price movement confirmation
/// - Volume analysis
/// - Trend strength assessment
/// - Divergence identification
/// - Market momentum analysis
///
/// Note: High positive values indicate strong upward momentum with volume support,
/// while high negative values indicate strong downward momentum with volume support
/// </remarks>

[SkipLocalsInit]
public sealed class Pvol : AbstractBase
{
    private double _prevClose;
    private double _prevVolume;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Pvol()
    {
        WarmupPeriod = 2;  // Need previous close and volume
        Name = "PVOL";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Pvol(object source) : this()
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

        // Calculate price and volume changes
        double priceChange = (Math.Abs(_prevClose) >= double.Epsilon) ? (BarInput.Close - _prevClose) / _prevClose : 0;
        double volumeChange = (Math.Abs(_prevVolume) >= double.Epsilon) ? (BarInput.Volume - _prevVolume) / _prevVolume : 0;

        // Store current values for next calculation
        _prevClose = BarInput.Close;
        _prevVolume = BarInput.Volume;

        // Calculate PVOL
        double pvol = priceChange * volumeChange * 100;

        IsHot = _index >= WarmupPeriod;
        return pvol;
    }
}
