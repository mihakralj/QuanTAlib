using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// Vel: Velocity
/// An enhanced momentum indicator that applies Jurik Moving Average (JMA) smoothing
/// to the basic momentum calculation, providing better noise reduction while
/// maintaining responsiveness to significant price movements.
/// </summary>
/// <remarks>
/// The Velocity calculation process:
/// 1. Calculate basic momentum (price difference)
/// 2. Apply JMA smoothing to the momentum values
/// 3. No scaling factor applied to maintain price-based units
///
/// Key characteristics:
/// - Enhanced momentum measurement with JMA smoothing
/// - Better noise reduction than basic momentum
/// - Maintains responsiveness to significant moves
/// - Reduced lag through JMA's phase-shifting
///
/// Formula:
/// Mom = Price - PriceN
/// Vel = JMA(Mom, period)
///
/// Sources:
///     Enhanced with JMA smoothing by Mark Jurik
///     Technical Analysis of Financial Markets by John J. Murphy
/// </remarks>
[SkipLocalsInit]
public sealed class Vel : AbstractBase
{
    private readonly CircularBuffer _priceBuffer;
    private readonly Jma _smoothing;
    private const int DefaultPeriod = 10;
    private const int DefaultPhase = 100;
    private const double DefaultFactor = 0.25;

    /// <param name="period">The lookback period for velocity calculation (default 10).</param>
    /// <param name="phase">The phase for the JMA smoothing (default 0).</param>
    /// <param name="power">The power factor for the JMA smoothing (default 2.0).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vel(int period = DefaultPeriod, int phase = DefaultPhase, double factor = DefaultFactor)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));

        _priceBuffer = new(period + 1);
        _smoothing = new(period, phase, factor);
        WarmupPeriod = period * 2;  // JMA needs more warmup periods
        Name = $"VEL({period})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The lookback period for velocity calculation.</param>
    /// <param name="phase">The phase for the JMA smoothing.</param>
    /// <param name="power">The power factor for the JMA smoothing.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vel(object source, int period, int phase = DefaultPhase, double power = DefaultFactor)
        : this(period, phase, power)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
            _priceBuffer.Add(Input.Value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        if (_priceBuffer.Count < _priceBuffer.Capacity)
            return 0.0;

        // Calculate basic momentum
        double momentum = Input.Value - _priceBuffer[0];

        // Apply JMA smoothing
        return _smoothing.Calc(momentum, Input.IsNew);
    }
}
