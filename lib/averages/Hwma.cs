using System;
namespace QuanTAlib;

/// <summary>
/// HWMA: Holt-Winters Moving Average
/// A triple exponential smoothing method that incorporates level (F), velocity (V), and
/// acceleration (A) components to create a responsive yet smooth moving average. This
/// implementation uses optimized smoothing factors for each component.
/// </summary>
/// <remarks>
/// The HWMA calculation process:
/// 1. Updates the level (F) component using alpha smoothing
/// 2. Updates the velocity (V) component using beta smoothing
/// 3. Updates the acceleration (A) component using gamma smoothing
/// 4. Combines all components for final value: F + V + 0.5A
///
/// Key characteristics:
/// - Adapts to both trends and acceleration in price movement
/// - Three separate smoothing factors for fine-tuned control
/// - More responsive to changes than simple moving averages
/// - Handles both linear and non-linear trends
///
/// Implementation:
///     Based on Holt-Winters triple exponential smoothing principles
///     with optimized default parameters:
///     - Alpha (nA) = 2/(period + 1)
///     - Beta (nB) = 1/period
///     - Gamma (nC) = 1/period
/// </remarks>

public class Hwma : AbstractBase
{
    private readonly int _period;
    private readonly double _nA, _nB, _nC;
    private double _pF, _pV, _pA;
    private double _ppF, _ppV, _ppA;

    /// <param name="period">The number of data points used in the HWMA calculation.</param>
    public Hwma(int period) : this(period, 2.0 / (1 + period), 1.0 / period, 1.0 / period)
    {
    }

    /// <param name="nA">Alpha smoothing factor for the level component.</param>
    /// <param name="nB">Beta smoothing factor for the velocity component.</param>
    /// <param name="nC">Gamma smoothing factor for the acceleration component.</param>
    public Hwma(double nA, double nB, double nC) : this((int)((2 - nA) / nA), nA, nB, nC)
    {
    }

    /// <param name="period">The number of data points used in the HWMA calculation.</param>
    /// <param name="nA">Alpha smoothing factor for the level component.</param>
    /// <param name="nB">Beta smoothing factor for the velocity component.</param>
    /// <param name="nC">Gamma smoothing factor for the acceleration component.</param>
    /// <exception cref="ArgumentException">Thrown when period is less than 1.</exception>
    public Hwma(int period, double nA, double nB, double nC)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be greater than or equal to 1.", nameof(period));
        }
        _period = period;
        _nA = nA;
        _nB = nB;
        _nC = nC;
        WarmupPeriod = period;
        Name = $"Hwma({_period})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of data points used in the HWMA calculation.</param>
    public Hwma(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    public override void Init()
    {
        base.Init();
        _pF = _pV = _pA = 0;
        _ppF = _ppV = _ppA = 0;
    }

    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Input.Value;
            _index++;
            _ppF = _pF;
            _ppV = _pV;
            _ppA = _pA;
        }
        else
        {
            _pF = _ppF;
            _pV = _ppV;
            _pA = _ppA;
        }
    }

    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        if (_index == 1)
        {
            _pF = Input.Value;
            _pA = _pV = 0;
        }

        double nA = _nA, nB = _nB, nC = _nC;
        if (_period == 1)
        {
            nA = 1;
            nB = 0;
            nC = 0;
        }

        double F = (1 - nA) * (_pF + _pV + 0.5 * _pA) + nA * Input.Value;
        double V = (1 - nB) * (_pV + _pA) + nB * (F - _pF);
        double A = (1 - nC) * _pA + nC * (V - _pV);

        double hwma = F + V + 0.5 * A;

        _pF = F;
        _pV = V;
        _pA = A;

        IsHot = _index >= WarmupPeriod;
        return hwma;
    }
}
