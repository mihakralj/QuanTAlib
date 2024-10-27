using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// REMA: Regularized Exponential Moving Average
/// A modified exponential moving average that includes a regularization term to reduce
/// noise and improve trend following. The regularization helps to smooth the output
/// while maintaining responsiveness to significant price movements.
/// </summary>
/// <remarks>
/// The REMA calculation process:
/// 1. Uses standard EMA smoothing with adaptive alpha
/// 2. Adds regularization term based on previous values
/// 3. Balances new and regularized terms using lambda parameter
/// 4. Provides smoother output than standard EMA
///
/// Key characteristics:
/// - Improved noise reduction through regularization
/// - Better trend following than standard EMA
/// - Adjustable regularization via lambda parameter
/// - Adaptive alpha based on period
/// - Reduced whipsaws in choppy markets
///
/// Sources:
///     https://user42.tuxfamily.org/chart/manual/Regularized-Exponential-Moving-Average.html
/// </remarks>

public class Rema : AbstractBase
{
    private readonly int _period;
    private readonly double _lambda;
    private readonly double _lambdaPlus1Recip;  // 1/(1 + lambda)
    private double _lastRema, _prevRema;
    private double _savedLastRema, _savedPrevRema;

    /// <summary>
    /// Gets the period used in the REMA calculation.
    /// </summary>
    public int Period => _period;

    /// <summary>
    /// Gets the lambda (regularization) parameter value.
    /// </summary>
    public double Lambda => _lambda;

    /// <param name="period">The number of periods used in the REMA calculation.</param>
    /// <param name="lambda">The regularization parameter (default 0.5). Higher values increase smoothing.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1 or lambda is negative.</exception>
    public Rema(int period, double lambda = 0.5)
    {
        if (period < 1)
            throw new System.ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        if (lambda < 0)
            throw new System.ArgumentOutOfRangeException(nameof(lambda), "Lambda must be non-negative.");

        _period = period;
        _lambda = lambda;
        _lambdaPlus1Recip = 1.0 / (1.0 + lambda);
        Name = $"REMA({period},{lambda:F2})";
        WarmupPeriod = period;
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of periods used in the REMA calculation.</param>
    /// <param name="lambda">The regularization parameter (default 0.5).</param>
    public Rema(object source, int period, double lambda = 0.5) : this(period, lambda)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _lastRema = 0;
        _prevRema = 0;
        _savedLastRema = 0;
        _savedPrevRema = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _savedLastRema = _lastRema;
            _savedPrevRema = _prevRema;
            _index++;
        }
        else
        {
            _lastRema = _savedLastRema;
            _prevRema = _savedPrevRema;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateAlpha()
    {
        return 2.0 / (System.Math.Min(_period, _index) + 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateRema(double alpha, double input)
    {
        double standardTerm = _lastRema + alpha * (input - _lastRema);
        double regularizationTerm = _lastRema + (_lastRema - _prevRema);
        return (standardTerm + _lambda * regularizationTerm) * _lambdaPlus1Recip;
    }

    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        if (_index > 2)
        {
            double alpha = CalculateAlpha();
            double rema = CalculateRema(alpha, Input.Value);
            _prevRema = _lastRema;
            _lastRema = rema;
        }
        else if (_index == 2)
        {
            _prevRema = _lastRema;
            _lastRema = Input.Value;
        }
        else
        {
            _lastRema = Input.Value;
        }

        IsHot = _index >= WarmupPeriod;
        return _lastRema;
    }
}
