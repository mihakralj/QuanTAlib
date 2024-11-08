using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// DMI: Directional Movement Index
/// A technical indicator that identifies the directional movement of price by
/// comparing successive highs and lows. DMI consists of two lines: +DI and -DI,
/// which help determine trend direction and strength.
/// </summary>
/// <remarks>
/// The DMI calculation process:
/// 1. Calculate True Range (TR)
/// 2. Calculate +DM (Positive Directional Movement)
/// 3. Calculate -DM (Negative Directional Movement)
/// 4. Smooth TR, +DM, and -DM using Wilder's smoothing
/// 5. Calculate +DI and -DI as percentages
///
/// Key characteristics:
/// - Both +DI and -DI oscillate between 0 and 100
/// - When +DI > -DI, uptrend is indicated
/// - When -DI > +DI, downtrend is indicated
/// - Crossovers of +DI and -DI signal potential trend changes
/// - Used in conjunction with ADX for trend trading
///
/// Formula:
/// TR = max(high-low, abs(high-prevClose), abs(low-prevClose))
/// +DM = if(high-prevHigh > prevLow-low && high-prevHigh > 0) then high-prevHigh else 0
/// -DM = if(prevLow-low > high-prevHigh && prevLow-low > 0) then prevLow-low else 0
/// Smoothed TR = Wilder's smoothing of TR (ATR)
/// Smoothed +DM = Wilder's smoothing of +DM
/// Smoothed -DM = Wilder's smoothing of -DM
/// +DI = 100 * Smoothed(+DM) / Smoothed(TR)
/// -DI = 100 * Smoothed(-DM) / Smoothed(TR)
///
/// Sources:
///     J. Welles Wilder Jr. - "New Concepts in Technical Trading Systems" (1978)
///     https://www.investopedia.com/terms/d/dmi.asp
///
/// Note: Default period of 14 was recommended by Wilder
/// </remarks>
[SkipLocalsInit]
public sealed class Dmi : AbstractBase
{
    private readonly Atr _atr;
    private readonly Rma _smoothedPlusDm;
    private readonly Rma _smoothedMinusDm;
    private double _prevHigh, _prevLow;
    private double _p_prevHigh, _p_prevLow;
    private double _plusDi, _minusDi;
    private const double ScalingFactor = 100.0;
    private const int DefaultPeriod = 14;

    public double PlusDI => _plusDi;
    public double MinusDI => _minusDi;

    public Dmi(int period = DefaultPeriod)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        _atr = new(period);
        _smoothedPlusDm = new(period);
        _smoothedMinusDm = new(period);
        WarmupPeriod = period + 1;
        Name = $"DMI({period})";
    }

    public override void Init()
    {
        base.Init();
        _atr.Init();
        _smoothedPlusDm.Init();
        _smoothedMinusDm.Init();
        _prevHigh = _prevLow = double.NaN;
        _p_prevHigh = _p_prevLow = double.NaN;
        _plusDi = _minusDi = 0;
        _index = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _index++;
            _p_prevHigh = _prevHigh;
            _p_prevLow = _prevLow;
        }
        else
        {
            _prevHigh = _p_prevHigh;
            _prevLow = _p_prevLow;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static (double plusDm, double minusDm) CalculateDirectionalMovement(
        double high, double low, double prevHigh, double prevLow)
    {
        double upMove = high - prevHigh;
        double downMove = prevLow - low;

        double plusDm = (upMove > downMove && upMove > 0) ? upMove : 0;
        double minusDm = (downMove > upMove && downMove > 0) ? downMove : 0;

        return (plusDm, minusDm);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(BarInput.IsNew);

        if (double.IsNaN(_prevHigh))
        {
            _prevHigh = BarInput.High;
            _prevLow = BarInput.Low;
            return 0.0;
        }

        // Calculate ATR
        double atr = _atr.Calc(BarInput).Value;

        // Calculate Directional Movement
        var (plusDm, minusDm) = CalculateDirectionalMovement(
            BarInput.High, BarInput.Low, _prevHigh, _prevLow);

        // Update previous values for next calculation
        _prevHigh = BarInput.High;
        _prevLow = BarInput.Low;

        // Smooth DM values using Wilder's method
        double smoothedPlusDm = _smoothedPlusDm.Calc(plusDm, BarInput.IsNew).Value;
        double smoothedMinusDm = _smoothedMinusDm.Calc(minusDm, BarInput.IsNew).Value;

        // Calculate DI values
        if (atr > 0)
        {
            _plusDi = ScalingFactor * smoothedPlusDm / atr;
            _minusDi = ScalingFactor * smoothedMinusDm / atr;
            return _plusDi - _minusDi;
        }

        _plusDi = 0.0;
        _minusDi = 0.0;
        return 0.0;
    }
}
