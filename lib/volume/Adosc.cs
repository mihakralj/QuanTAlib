using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// ADOSC: Chaikin Accumulation/Distribution Oscillator
/// A momentum indicator that measures the strength of accumulation/distribution by combining
/// price and volume with moving averages. It helps identify potential trend reversals and
/// buying/selling pressure.
/// </summary>
/// <remarks>
/// The ADOSC calculation process:
/// 1. Calculate ADL (Accumulation/Distribution Line)
///    a. Money Flow Multiplier = ((Close - Low) - (High - Close)) / (High - Low)
///    b. Money Flow Volume = MFM × Volume
///    c. ADL = Previous ADL + MFV
/// 2. Calculate two EMAs of ADL values
/// 3. Subtract longer EMA from shorter EMA
///
/// Key characteristics:
/// - Volume-weighted measure
/// - Oscillates around zero
/// - Uses two different time periods
/// - Default periods are 3 and 10 days
/// - Shows momentum of money flow
///
/// Formula:
/// MFM = ((Close - Low) - (High - Close)) / (High - Low)
/// MFV = MFM × Volume
/// ADL = Previous ADL + MFV
/// ADOSC = EMA(ADL, shortPeriod) - EMA(ADL, longPeriod)
///
/// Market Applications:
/// - Trend confirmation
/// - Divergence analysis
/// - Volume/price relationship
/// - Support/resistance levels
/// - Market reversals
///
/// Sources:
///     Marc Chaikin - Original development
///     https://www.investopedia.com/terms/c/chaikinoscillator.asp
///
/// Note: Positive values indicate buying pressure, while negative values indicate selling pressure
/// </remarks>
[SkipLocalsInit]
public sealed class Adosc : AbstractBase
{
    private readonly int _longPeriod;
    private double _cumulativeAdl;
    private double _shortEma;
    private double _longEma;
    private readonly double _shortAlpha;
    private readonly double _longAlpha;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Adosc(int shortPeriod = 3, int longPeriod = 10)
    {
        _longPeriod = longPeriod;
        WarmupPeriod = longPeriod;  // Need longer period for EMA calculation
        Name = $"ADOSC({shortPeriod},{_longPeriod})";
        _shortAlpha = 2.0 / (shortPeriod + 1);
        _longAlpha = 2.0 / (longPeriod + 1);
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Adosc(object source, int shortPeriod = 3, int longPeriod = 10) : this(shortPeriod, longPeriod)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _cumulativeAdl = 0;
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

        // Update cumulative ADL
        _cumulativeAdl += mfv;

        // Calculate EMAs
        if (_index <= _longPeriod)
        {
            // Initialize EMAs
            _shortEma = _cumulativeAdl;
            _longEma = _cumulativeAdl;
            return 0;
        }

        // Update EMAs
        _shortEma = (_shortAlpha * _cumulativeAdl) + ((1 - _shortAlpha) * _shortEma);
        _longEma = (_longAlpha * _cumulativeAdl) + ((1 - _longAlpha) * _longEma);

        // Calculate ADOSC
        double adosc = _shortEma - _longEma;

        IsHot = _index >= WarmupPeriod;
        return adosc;
    }
}
