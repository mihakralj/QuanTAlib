using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// LTMA: Laguerre Time Moving Average
/// A sophisticated moving average that uses Laguerre polynomials to create a time-based
/// filter. This approach provides excellent noise reduction while maintaining
/// responsiveness to price changes.
/// </summary>
/// <remarks>
/// The LTMA calculation process:
/// 1. Applies a cascade of four Laguerre filters
/// 2. Each filter stage provides additional smoothing
/// 3. Combines the filtered outputs with optimal weights
/// 4. Produces a smooth output with minimal lag
///
/// Key characteristics:
/// - Time-based filtering using Laguerre polynomials
/// - Excellent noise reduction
/// - Maintains good responsiveness
/// - Single parameter (gamma) controls smoothing
/// - Computationally efficient
///
/// Sources:
///     John Ehlers - "Time Warp - Without Space Travel"
///     https://www.mesasoftware.com/papers/TimeWarp.pdf
/// </remarks>
public class Ltma : AbstractBase
{
    private readonly double _gamma;
    private readonly double _oneMinusGamma;
    private readonly double _invSix = 1.0 / 6.0;  // Precalculated constant for final averaging
    private double _prevL0, _prevL1, _prevL2, _prevL3;
    private double _p_prevL0, _p_prevL1, _p_prevL2, _p_prevL3;

    /// <summary>
    /// Gets the gamma parameter value used in the Laguerre filter.
    /// </summary>
    public double Gamma => _gamma;

    /// <param name="gamma">The damping factor (0 to 1) controlling the smoothing. Lower values provide more smoothing.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when gamma is not between 0 and 1.</exception>
    public Ltma(double gamma = 0.1)
    {
        if (gamma < 0 || gamma > 1)
            throw new System.ArgumentOutOfRangeException(nameof(gamma), "Gamma must be between 0 and 1.");
        _gamma = gamma;
        _oneMinusGamma = 1.0 - gamma;
        Name = $"Laguerre({gamma:F2})";
        WarmupPeriod = 4; // Minimum number of samples needed
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="gamma">The damping factor (0 to 1) controlling the smoothing.</param>
    public Ltma(object source, double gamma = 0.1) : this(gamma)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _prevL0 = _prevL1 = _prevL2 = _prevL3 = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _p_prevL0 = _prevL0;
            _p_prevL1 = _prevL1;
            _p_prevL2 = _prevL2;
            _p_prevL3 = _prevL3;
            _index++;
        }
        else
        {
            _prevL0 = _p_prevL0;
            _prevL1 = _p_prevL1;
            _prevL2 = _p_prevL2;
            _prevL3 = _p_prevL3;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateLaguerreStage(double input, double prev, double prevPrev)
    {
        return (-_gamma * input) + prev + (_gamma * prevPrev);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CombineOutputs(double l0, double l1, double l2, double l3)
    {
        return (l0 + (2.0 * (l1 + l2)) + l3) * _invSix;
    }

    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        // First stage
        double l0 = (_oneMinusGamma * Input.Value) + (_gamma * _prevL0);

        // Subsequent stages using helper method
        double l1 = CalculateLaguerreStage(l0, _prevL0, _prevL1);
        double l2 = CalculateLaguerreStage(l1, _prevL1, _prevL2);
        double l3 = CalculateLaguerreStage(l2, _prevL2, _prevL3);

        // Store values for next iteration
        _prevL0 = l0;
        _prevL1 = l1;
        _prevL2 = l2;
        _prevL3 = l3;

        IsHot = _index >= WarmupPeriod;
        return CombineOutputs(l0, l1, l2, l3);
    }
}
