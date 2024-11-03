using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// EOM: Ease of Movement
/// A volume-based technical indicator that relates price change to volume, showing the
/// relationship between price change and volume. It emphasizes days where price changes
/// are accomplished with minimal volume and minimizes days where large volume generates
/// small price changes.
/// </summary>
/// <remarks>
/// The EOM calculation process:
/// 1. Calculate the distance moved:
///    Distance = ((High + Low)/2 - (Prior High + Prior Low)/2)
/// 2. Calculate the Box Ratio:
///    BoxRatio = Volume / (High - Low)
/// 3. Calculate single-period EMV:
///    EMV = Distance / BoxRatio
/// 4. Smooth EMV using simple moving average (optional)
///
/// Key characteristics:
/// - Volume-weighted measure
/// - Oscillates around zero
/// - Shows ease of price movement
/// - Default period is 14 days
///
/// Formula:
/// Distance = ((H + L)/2 - (pH + pL)/2)
/// BoxRatio = Volume / (High - Low)
/// EMV = Distance / BoxRatio
/// EOM = SMA(EMV, period)
///
/// Market Applications:
/// - Trend strength analysis
/// - Volume/price relationship
/// - Support/resistance breakouts
/// - Market momentum
/// - Divergence identification
///
/// Sources:
///     Richard W. Arms Jr. - Original development
///     https://www.investopedia.com/terms/e/easeofmovement.asp
///
/// Note: Positive values suggest prices are rising with light volume (bullish),
/// while negative values suggest prices are falling with light volume (bearish)
/// </remarks>
[SkipLocalsInit]
public sealed class Eom : AbstractBase
{
    private readonly int _period;
    private readonly double[] _emv;
    private int _position;
    private double _prevMidpoint;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Eom(int period = 14)
    {
        _period = period;
        WarmupPeriod = period + 1;  // Need one extra period for previous midpoint
        Name = $"EOM({_period})";
        _emv = new double[period];
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Eom(object source, int period = 14) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _position = 0;
        _prevMidpoint = 0;
        Array.Clear(_emv, 0, _emv.Length);
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

        double midpoint = (BarInput.High + BarInput.Low) / 2;
        double boxRatio = BarInput.Volume / (BarInput.High - BarInput.Low + double.Epsilon);  // Avoid division by zero

        // Skip first period to establish previous midpoint
        if (_index == 1)
        {
            _prevMidpoint = midpoint;
            return 0;
        }

        // Calculate distance moved
        double distance = midpoint - _prevMidpoint;

        // Calculate EMV for this period
        double emv = distance / boxRatio * 10000;  // Multiply by 10000 to make values more readable

        // Store in circular buffer
        _emv[_position] = emv;
        _position = (_position + 1) % _period;

        // Calculate EOM (simple moving average of EMV)
        double sum = 0;
        for (int i = 0; i < _period; i++)
        {
            sum += _emv[i];
        }
        double eom = sum / _period;

        // Store current midpoint for next calculation
        _prevMidpoint = midpoint;

        IsHot = _index >= WarmupPeriod;
        return eom;
    }
}
