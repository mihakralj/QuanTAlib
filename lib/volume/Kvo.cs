using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// KVO: Klinger Volume Oscillator
/// A volume-based technical indicator that compares volume to price movement to identify
/// long-term trends and potential reversals. It helps determine the long-term money flow
/// while remaining sensitive to short-term fluctuations.
/// </summary>
/// <remarks>
/// The KVO calculation process:
/// 1. Calculate Trend:
///    Trend = Current DM > Previous DM ? +1 : -1
/// 2. Calculate Volume Force (VF):
///    VF = Volume * abs(ROC) * Trend * 100
/// 3. Calculate two EMAs of VF and their difference:
///    Signal = EMA(VF, shortPeriod) - EMA(VF, longPeriod)
///
/// Key characteristics:
/// - Volume-weighted measure
/// - Oscillates around zero
/// - Uses two different time periods
/// - Default periods are 34 and 55 days
/// - Shows volume force and price direction
///
/// Formula:
/// DM = (H + L + C) / 3
/// Trend = DM > Previous DM ? +1 : -1
/// VF = Volume * abs(ROC) * Trend * 100
/// KVO = EMA(VF, shortPeriod) - EMA(VF, longPeriod)
///
/// Market Applications:
/// - Trend confirmation
/// - Divergence analysis
/// - Volume/price relationship
/// - Support/resistance levels
/// - Market reversals
///
/// Sources:
///     Stephen Klinger - Original development
///     https://www.investopedia.com/terms/k/klingeroscillator.asp
///
/// Note: Positive values indicate buying pressure, while negative values indicate selling pressure
/// </remarks>

[SkipLocalsInit]
public sealed class Kvo : AbstractBase
{
    private readonly int _longPeriod;
    private double _prevDm;
    private double _shortEma;
    private double _longEma;
    private readonly double _shortAlpha;
    private readonly double _longAlpha;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Kvo(int shortPeriod = 34, int longPeriod = 55)
    {
        _longPeriod = longPeriod;
        WarmupPeriod = longPeriod + 1;  // Need one extra period for previous DM
        Name = $"KVO({shortPeriod},{_longPeriod})";
        _shortAlpha = 2.0 / (shortPeriod + 1);
        _longAlpha = 2.0 / (longPeriod + 1);
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Kvo(object source, int shortPeriod = 34, int longPeriod = 55) : this(shortPeriod, longPeriod)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _prevDm = 0;
        _shortEma = 0;
        _longEma = 0;
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

        // Calculate Daily Mean
        double dm = (BarInput.High + BarInput.Low + BarInput.Close) / 3;

        // Skip first period to establish previous DM
        if (_index == 1)
        {
            _prevDm = dm;
            return 0;
        }

        // Calculate Trend
        int trend = dm > _prevDm ? 1 : -1;

        // Calculate Rate of Change
        double roc = Math.Abs(dm - _prevDm) / _prevDm;

        // Calculate Volume Force
        double vf = BarInput.Volume * roc * trend * 100;

        // Calculate EMAs
        if (_index <= _longPeriod)
        {
            // Initialize EMAs
            _shortEma = vf;
            _longEma = vf;
        }
        else
        {
            // Update EMAs
            _shortEma = (_shortAlpha * vf) + ((1 - _shortAlpha) * _shortEma);
            _longEma = (_longAlpha * vf) + ((1 - _longAlpha) * _longEma);
        }

        // Store current DM for next calculation
        _prevDm = dm;

        // Calculate KVO
        double kvo = _shortEma - _longEma;

        IsHot = _index >= WarmupPeriod;
        return kvo;
    }
}
