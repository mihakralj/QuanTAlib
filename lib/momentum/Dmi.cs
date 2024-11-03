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
/// +DM = if(high-prevHigh > prevLow-low) then max(high-prevHigh, 0) else 0
/// -DM = if(prevLow-low > high-prevHigh) then max(prevLow-low, 0) else 0
/// +DI = 100 * smoothed(+DM) / smoothed(TR)
/// -DI = 100 * smoothed(-DM) / smoothed(TR)
///
/// Sources:
///     J. Welles Wilder Jr. - "New Concepts in Technical Trading Systems" (1978)
///     https://www.investopedia.com/terms/d/dmi.asp
///
/// Note: Default period of 14 was recommended by Wilder
/// </remarks>
[SkipLocalsInit]
public sealed class Dmi : AbstractBarBase
{
    private readonly Rma _smoothedTr;
    private readonly Rma _smoothedPlusDm;
    private readonly Rma _smoothedMinusDm;
    private double _prevHigh, _prevLow, _prevClose;
    private double _p_prevHigh, _p_prevLow, _p_prevClose;
    private double _plusDi, _minusDi;
    private const double ScalingFactor = 100.0;
    private const int DefaultPeriod = 14;

    /// <summary>
    /// Gets the most recent +DI value
    /// </summary>
    public double PlusDI => _plusDi;

    /// <summary>
    /// Gets the most recent -DI value
    /// </summary>
    public double MinusDI => _minusDi;

    /// <param name="period">The number of periods used in the DMI calculation (default 14).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Dmi(int period = DefaultPeriod)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        _smoothedTr = new(period, useSma: true);
        _smoothedPlusDm = new(period, useSma: true);
        _smoothedMinusDm = new(period, useSma: true);
        _index = 0;
        WarmupPeriod = period + 1;
        Name = $"DMI({period})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of periods used in the DMI calculation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Dmi(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _index++;
            _p_prevHigh = _prevHigh;
            _p_prevLow = _prevLow;
            _p_prevClose = _prevClose;
        }
        else
        {
            _prevHigh = _p_prevHigh;
            _prevLow = _p_prevLow;
            _prevClose = _p_prevClose;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double CalculateTrueRange(double high, double low, double prevClose)
    {
        double hl = high - low;
        double hpc = Math.Abs(high - prevClose);
        double lpc = Math.Abs(low - prevClose);
        return Math.Max(hl, Math.Max(hpc, lpc));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static (double plusDm, double minusDm) CalculateDirectionalMovement(
        double high, double low, double prevHigh, double prevLow)
    {
        double upMove = high - prevHigh;
        double downMove = prevLow - low;

        double plusDm = 0.0;
        double minusDm = 0.0;

        if (upMove > downMove && upMove > 0)
            plusDm = upMove;
        else if (downMove > upMove && downMove > 0)
            minusDm = downMove;

        return (plusDm, minusDm);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        if (_index == 1)
        {
            _prevHigh = Input.High;
            _prevLow = Input.Low;
            _prevClose = Input.Close;
            return 0.0;
        }

        // Calculate True Range and Directional Movement
        double tr = CalculateTrueRange(Input.High, Input.Low, _prevClose);
        var (plusDm, minusDm) = CalculateDirectionalMovement(
            Input.High, Input.Low, _prevHigh, _prevLow);

        // Update previous values
        _prevHigh = Input.High;
        _prevLow = Input.Low;
        _prevClose = Input.Close;

        // Smooth the indicators using Wilder's method
        _smoothedTr.Calc(tr, Input.IsNew);
        _smoothedPlusDm.Calc(plusDm, Input.IsNew);
        _smoothedMinusDm.Calc(minusDm, Input.IsNew);

        // Calculate +DI and -DI
        double smoothedTr = _smoothedTr.Value;
        if (smoothedTr > 0)
        {
            _plusDi = ScalingFactor * _smoothedPlusDm.Value / smoothedTr;
            _minusDi = ScalingFactor * _smoothedMinusDm.Value / smoothedTr;
            return _plusDi - _minusDi;  // Return the difference as main value
        }

        _plusDi = 0.0;
        _minusDi = 0.0;
        return 0.0;
    }
}
