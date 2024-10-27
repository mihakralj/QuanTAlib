using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// RSX: Relative Strength eXtended
/// An enhanced version of RSI developed by Mark Jurik that applies JMA (Jurik Moving
/// Average) smoothing to the RSI calculation. RSX provides smoother signals with
/// less noise while maintaining responsiveness to significant price movements.
/// </summary>
/// <remarks>
/// The RSX calculation process:
/// 1. Calculates traditional RSI values
/// 2. Applies JMA smoothing to RSI output
/// 3. Uses optimized parameters for noise reduction
/// 4. Maintains RSI's 0-100 scale
///
/// Key characteristics:
/// - Smoother than traditional RSI
/// - Better noise reduction
/// - Maintains responsiveness to significant moves
/// - Same interpretation as RSI (0-100 scale)
/// - Fewer false signals than RSI
///
/// Formula:
/// RSX = JMA(RSI(price))
/// where:
/// RSI = standard Relative Strength Index
/// JMA = Jurik Moving Average with optimized parameters
///
/// Sources:
///     Mark Jurik - "The Jurik RSX"
///     https://www.jurikresearch.com/
///
/// Note: Proprietary enhancement of RSI using JMA technology
/// </remarks>

[SkipLocalsInit]
public sealed class Rsx : AbstractBase
{
    private readonly Rma _avgGain;
    private readonly Rma _avgLoss;
    private readonly Jma _rsx;
    private double _prevValue, _p_prevValue;
    private const double ScalingFactor = 100.0;
    private const int DefaultPeriod = 14;
    private const int DefaultPhase = 0;
    private const double DefaultFactor = 0.55;
    private const int JmaPeriod = 8;
    private const int JmaPower = 100;
    private const double JmaPhase = 0.25;
    private const int JmaExtra = 3;

    /// <param name="period">The number of periods for RSI calculation (default 14).</param>
    /// <param name="phase">The phase parameter for JMA smoothing (default 0).</param>
    /// <param name="factor">The factor parameter for smoothing control (default 0.55).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rsx(int period = DefaultPeriod, int phase = DefaultPhase, double factor = DefaultFactor)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        _avgGain = new(period);
        _avgLoss = new(period);
        _rsx = new(JmaPeriod, JmaPower, JmaPhase, JmaExtra);
        _index = 0;
        WarmupPeriod = period + 1;
        Name = $"RSX({period})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of periods for RSI calculation.</param>
    /// <param name="phase">The phase parameter for JMA smoothing.</param>
    /// <param name="factor">The factor parameter for smoothing control.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rsx(object source, int period, int phase = DefaultPhase, double factor = DefaultFactor) : this(period, phase, factor)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _index++;
            _p_prevValue = _prevValue;
        }
        else
        {
            _prevValue = _p_prevValue;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static (double gain, double loss) CalculateGainLoss(double change)
    {
        return (Math.Max(change, 0), Math.Max(-change, 0));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double CalculateRsi(double avgGain, double avgLoss)
    {
        return avgLoss > 0 ? ScalingFactor - (ScalingFactor / (1 + (avgGain / avgLoss))) : ScalingFactor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        if (_index == 1)
        {
            _prevValue = Input.Value;
        }

        // Calculate RSI components
        double change = Input.Value - _prevValue;
        var (gain, loss) = CalculateGainLoss(change);
        _prevValue = Input.Value;

        // Calculate RSI
        _avgGain.Calc(gain, Input.IsNew);
        _avgLoss.Calc(loss, Input.IsNew);
        double rsi = CalculateRsi(_avgGain.Value, _avgLoss.Value);

        // Apply JMA smoothing
        _rsx.Calc(rsi, Input.IsNew);

        return _rsx.Value;
    }
}
