using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// EWMA: Exponential Weighted Moving Average Volatility
/// A volatility measure that gives more weight to recent observations,
/// calculated using squared returns and exponential weighting.
/// </summary>
/// <remarks>
/// The EWMA calculation process:
/// 1. Calculate returns: (Close[t] - Close[t-1])/Close[t-1]
/// 2. Square returns
/// 3. Apply exponential weighting to squared returns
/// 4. Take square root and annualize
///
/// Key characteristics:
/// - More responsive to recent volatility changes
/// - Default decay factor (lambda) is 0.94
/// - Default period is 20 days
/// - Annualized by default (multiply by sqrt(252))
/// - Expressed as a percentage
///
/// Formula:
/// Returns[t] = (Close[t] - Close[t-1])/Close[t-1]
/// EWMA[t] = λ * EWMA[t-1] + (1-λ) * Returns[t]²
/// Volatility = sqrt(EWMA) * sqrt(252) * 100
///
/// Where:
/// λ (lambda) = decay factor (typically 0.94)
///
/// Market Applications:
/// - Risk measurement
/// - Option pricing
/// - Value at Risk (VaR)
/// - Portfolio optimization
/// - Volatility forecasting
///
/// Sources:
///     RiskMetrics™ Technical Document (1996)
///     https://www.msci.com/documents/10199/5915b101-4206-4ba0-aee2-3449d5c7e95a
///
/// Note: Returns annualized volatility as a percentage
/// </remarks>

[SkipLocalsInit]
public sealed class Ewma : AbstractBase
{
    private readonly int _period;
    private readonly double _lambda;
    private readonly bool _annualize;
    private double _prevClose;
    private double _ewma;
    private bool _isInitialized;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Ewma(int period = 20, double lambda = 0.94, bool annualize = true)
    {
        _period = period;
        _lambda = lambda;
        _annualize = annualize;
        WarmupPeriod = period + 1;  // Need one extra period for returns
        Name = $"EWMA({_period},{_lambda})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Ewma(object source, int period = 20, double lambda = 0.94, bool annualize = true) : this(period, lambda, annualize)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _prevClose = 0;
        _ewma = 0;
        _isInitialized = false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Value;
            _index++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(BarInput.IsNew);

        // Skip first period to establish previous close
        if (_index == 1)
        {
            _prevClose = BarInput.Close;
            return 0;
        }

        // Calculate return
        double return_ = (BarInput.Close - _prevClose) / _prevClose;
        double squaredReturn = return_ * return_;
        _prevClose = BarInput.Close;

        // Initialize EWMA if not done
        if (!_isInitialized && _index > _period)
        {
            _ewma = squaredReturn;
            _isInitialized = true;
        }

        // Need enough values for calculation
        if (_index <= _period)
        {
            return 0;
        }

        // Update EWMA
        _ewma = _lambda * _ewma + (1 - _lambda) * squaredReturn;

        // Calculate volatility
        double volatility = Math.Sqrt(_ewma);

        // Annualize if requested
        if (_annualize)
        {
            volatility *= Math.Sqrt(252);
        }

        // Convert to percentage
        volatility *= 100;

        IsHot = _index >= WarmupPeriod;
        return volatility;
    }
}
