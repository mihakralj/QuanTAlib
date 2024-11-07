using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// DMX: Enhanced Directional Movement Index using JMA smoothing
/// An improvement over the traditional DMI indicator that uses Jurik Moving Average (JMA)
/// for smoothing. This enhancement provides better noise reduction while maintaining
/// responsiveness to significant price movements.
/// </summary>
/// <remarks>
/// The DMX calculation process:
/// 1. Calculate DMI using the standard Dmi class
/// 2. Apply JMA smoothing to the +DI and -DI values
///
/// Key improvements over DMI:
/// - Uses JMA's adaptive volatility-based smoothing
/// - Better noise reduction in the directional movement signals
/// - Maintains responsiveness to significant price movements
/// - Reduced lag through JMA's phase-shifting
///
/// Formula:
/// DMI calculation as per standard DMI
/// DMX +DI = JMA(DMI +DI)
/// DMX -DI = JMA(DMI -DI)
///
/// Sources:
///     Original DMI by J. Welles Wilder Jr. - "New Concepts in Technical Trading Systems" (1978)
///     Enhanced with JMA smoothing by Mark Jurik
/// </remarks>
[SkipLocalsInit]
public sealed class Dmx : AbstractBarBase
{
    private readonly Dmi _dmi;
    private readonly Jma _smoothedPlusDi;
    private readonly Jma _smoothedMinusDi;
    private double _plusDi, _minusDi;
    private const int DefaultDmiPeriod = 14;
    private const int DefaultJmaPeriod = 7;
    private const int DefaultPhase = 100;
    private const double DefaultFactor = 0.25;

    /// <summary>
    /// Gets the most recent smoothed +DI value
    /// </summary>
    public double PlusDI => _plusDi;

    /// <summary>
    /// Gets the most recent smoothed -DI value
    /// </summary>
    public double MinusDI => _minusDi;

    /// <param name="dmiPeriod">The number of periods used in the DMI calculation (default 14).</param>
    /// <param name="jmaPeriod">The number of periods used in the JMA smoothing (default 10).</param>
    /// <param name="phase">The phase for the JMA smoothing (default 100).</param>
    /// <param name="factor">The factor for the JMA smoothing (default 0.25).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Dmx(int period = DefaultDmiPeriod, int jmaPeriod = DefaultJmaPeriod, int phase = DefaultPhase, double factor = DefaultFactor)
    {
        if (period < 1 || jmaPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(period), "Periods must be greater than or equal to 1.");
        _dmi = new(period);
        _smoothedPlusDi = new(jmaPeriod, phase, factor);
        _smoothedMinusDi = new(jmaPeriod, phase, factor);
        WarmupPeriod = period + jmaPeriod;
        Name = $"DMX({period},{jmaPeriod})";
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _index++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        // Calculate DMI
        _dmi.Calc(Input);

        // Smooth the DMI values using JMA
        _plusDi = _smoothedPlusDi.Calc(_dmi.PlusDI, Input.IsNew).Value;
        _minusDi = _smoothedMinusDi.Calc(_dmi.MinusDI, Input.IsNew).Value;

        return _plusDi - _minusDi;  // Return the difference as main value
    }
}
