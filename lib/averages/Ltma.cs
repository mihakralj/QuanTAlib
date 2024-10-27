using System;
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
            throw new ArgumentOutOfRangeException(nameof(gamma), "Gamma must be between 0 and 1.");
        _gamma = gamma;
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

    public override void Init()
    {
        base.Init();
        _prevL0 = _prevL1 = _prevL2 = _prevL3 = 0;
    }

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

    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        // Laguerre filter calculation
        double _l0 = (1 - _gamma) * Input.Value + _gamma * _prevL0;
        double _l1 = -_gamma * _l0 + _prevL0 + _gamma * _prevL1;
        double _l2 = -_gamma * _l1 + _prevL1 + _gamma * _prevL2;
        double _l3 = -_gamma * _l2 + _prevL2 + _gamma * _prevL3;
        _prevL0 = _l0;
        _prevL1 = _l1;
        _prevL2 = _l2;
        _prevL3 = _l3;

        double filteredValue = (_l0 + 2 * _l1 + 2 * _l2 + _l3) / 6;

        IsHot = _index >= WarmupPeriod;

        return filteredValue;
    }
}
