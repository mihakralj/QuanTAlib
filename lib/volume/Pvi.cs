using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// PVI: Positive Volume Index
/// A cumulative indicator that focuses on days when volume increases from the previous day.
/// It is based on the premise that the public is active on days with higher volume.
/// </summary>
/// <remarks>
/// The PVI calculation process:
/// 1. Compare current volume with previous volume
/// 2. If current volume is greater than previous volume:
///    PVI = Previous PVI + (((Close - Previous Close) / Previous Close) * Previous PVI)
/// 3. If current volume is less than or equal to previous volume:
///    PVI = Previous PVI
///
/// Key characteristics:
/// - Cumulative indicator
/// - Only updates on higher volume days
/// - Starts at base value of 1000
/// - Focuses on public activity
/// - Volume-driven measure
///
/// Formula:
/// If Volume > Previous Volume:
///    PVI = Previous PVI + (Price % Change * Previous PVI)
/// Else:
///    PVI = Previous PVI
///
/// Market Applications:
/// - Public participation tracking
/// - Trend identification
/// - Market timing
/// - Volume analysis
/// - Price confirmation
///
/// Sources:
///     Norman Fosback - Original development
///     https://www.investopedia.com/terms/p/pvi.asp
///
/// Note: Rising PVI suggests public buying pressure, while falling PVI suggests public selling pressure
/// </remarks>
[SkipLocalsInit]
public sealed class Pvi : AbstractBase
{
    private double _prevClose;
    private double _prevVolume;
    private double _prevPvi;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Pvi()
    {
        WarmupPeriod = 2;  // Need previous volume and close
        Name = "PVI";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Pvi(object source) : this()
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
        _prevPvi = 1000;  // Standard starting value
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
            return _prevPvi;
        }

        // Calculate PVI
        if (BarInput.Volume > _prevVolume)
        {
            double priceChange = ((BarInput.Close - _prevClose) / _prevClose);
            _prevPvi += priceChange * _prevPvi;
        }

        // Store current values for next calculation
        _prevClose = BarInput.Close;
        _prevVolume = BarInput.Volume;

        IsHot = _index >= WarmupPeriod;
        return _prevPvi;
    }
}
