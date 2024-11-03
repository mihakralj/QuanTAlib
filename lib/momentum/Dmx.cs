using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// DMX: Enhanced Directional Movement Index using JMA smoothing
/// An improvement over the traditional DMI indicator that uses Jurik Moving Average (JMA)
/// for smoothing instead of Wilder's moving average. This enhancement provides better
/// noise reduction while maintaining responsiveness to significant price movements.
/// </summary>
/// <remarks>
/// The DMX calculation process:
/// 1. Calculate True Range (TR)
/// 2. Calculate +DM (Positive Directional Movement)
/// 3. Calculate -DM (Negative Directional Movement)
/// 4. Smooth TR, +DM, and -DM using JMA instead of Wilder's smoothing
/// 5. Calculate +DI and -DI as percentages
///
/// Key improvements over DMI:
/// - Uses JMA's adaptive volatility-based smoothing
/// - Better noise reduction in the directional movement signals
/// - Maintains responsiveness to significant price movements
/// - Reduced lag through JMA's phase-shifting
///
/// Formula:
/// TR = max(high-low, abs(high-prevClose), abs(low-prevClose))
/// +DM = if(high-prevHigh > prevLow-low) then max(high-prevHigh, 0) else 0
/// -DM = if(prevLow-low > high-prevHigh) then max(prevLow-low, 0) else 0
/// +DI = 100 * JMA(+DM) / JMA(TR)
/// -DI = 100 * JMA(-DM) / JMA(TR)
///
/// Sources:
///     Original DMI by J. Welles Wilder Jr. - "New Concepts in Technical Trading Systems" (1978)
///     Enhanced with JMA smoothing by Mark Jurik
/// </remarks>
[SkipLocalsInit]
public sealed class Dmx : AbstractBarBase
{
    private readonly Jma _smoothedTr;
    private readonly Jma _smoothedPlusDm;
    private readonly Jma _smoothedMinusDm;
    private double _prevHigh, _prevLow, _prevClose;
    private double _p_prevHigh, _p_prevLow, _p_prevClose;
    private double _plusDi, _minusDi;
    private const double ScalingFactor = 100.0;
    private const int DefaultPeriod = 10;
    private const int DefaultPhase = 100;
    private const double DefaultFactor = 0.25;

    /// <summary>
    /// Gets the most recent +DI value
    /// </summary>
    public double PlusDI => _plusDi;

    /// <summary>
    /// Gets the most recent -DI value
    /// </summary>
    public double MinusDI => _minusDi;

    /// <param name="period">The number of periods used in the DMX calculation (default 14).</param>
    /// <param name="phase">The phase for the JMA smoothing (default 0).</param>
    /// <param name="factor">The factor for the JMA smoothing (default 0.45).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Dmx(int period = DefaultPeriod, int phase = DefaultPhase, double factor = DefaultFactor)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        _smoothedTr = new(period, phase, factor);
        _smoothedPlusDm = new(period, phase, factor);
        _smoothedMinusDm = new(period, phase, factor);
        _index = 0;
        WarmupPeriod = period * 2;  // JMA needs more warmup periods than RMA
        Name = $"DMX({period})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of periods used in the DMX calculation.</param>
    /// <param name="phase">The phase for the JMA smoothing.</param>
    /// <param name="factor">The factor for the JMA smoothing.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Dmx(object source, int period, int phase = DefaultPhase, double factor = DefaultFactor) : this(period, phase, factor)
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

        // Smooth the indicators using JMA
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
