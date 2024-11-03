using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// NVI: Negative Volume Index
/// A cumulative indicator that focuses on days when volume decreases from the previous day.
/// It is based on the premise that smart money is active on days with lower volume.
/// </summary>
/// <remarks>
/// The NVI calculation process:
/// 1. Compare current volume with previous volume
/// 2. If current volume is less than previous volume:
///    NVI = Previous NVI + (((Close - Previous Close) / Previous Close) * Previous NVI)
/// 3. If current volume is greater than or equal to previous volume:
///    NVI = Previous NVI
///
/// Key characteristics:
/// - Cumulative indicator
/// - Only updates on lower volume days
/// - Starts at base value of 1000
/// - Focuses on smart money activity
/// - Volume-driven measure
///
/// Formula:
/// If Volume < Previous Volume:
///    NVI = Previous NVI + (Price % Change * Previous NVI)
/// Else:
///    NVI = Previous NVI
///
/// Market Applications:
/// - Smart money tracking
/// - Trend identification
/// - Market timing
/// - Volume analysis
/// - Price confirmation
///
/// Sources:
///     Paul Dysart - Original development (1930s)
///     Norman Fosback - Further development
///     https://www.investopedia.com/terms/n/nvi.asp
///
/// Note: Rising NVI suggests smart money is buying, while falling NVI suggests smart money is selling
/// </remarks>
[SkipLocalsInit]
public sealed class Nvi : AbstractBase
{
    private double _prevClose;
    private double _prevVolume;
    private double _prevNvi;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Nvi()
    {
        WarmupPeriod = 2;  // Need previous volume and close
        Name = "NVI";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Nvi(object source) : this()
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
        _prevNvi = 1000;  // Standard starting value
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
            return _prevNvi;
        }

        // Calculate NVI
        if (BarInput.Volume < _prevVolume)
        {
            double priceChange = ((BarInput.Close - _prevClose) / _prevClose);
            _prevNvi += priceChange * _prevNvi;
        }

        // Store current values for next calculation
        _prevClose = BarInput.Close;
        _prevVolume = BarInput.Volume;

        IsHot = _index >= WarmupPeriod;
        return _prevNvi;
    }
}
