using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// CV: Conditional Volatility (GARCH)
/// Implements the GARCH(1,1) model for estimating conditional volatility,
/// which captures volatility clustering and mean reversion in financial markets.
/// </summary>
/// <remarks>
/// The CV (GARCH) calculation process:
/// 1. Calculate returns: (Close[t] - Close[t-1])/Close[t-1]
/// 2. Update variance estimate using GARCH(1,1) formula:
///    σ²[t] = ω + α*r²[t-1] + β*σ²[t-1]
/// 3. Take square root to get volatility
///
/// Key characteristics:
/// - Captures volatility clustering
/// - Mean-reverting behavior
/// - Responds to market shocks
/// - Default period is 20 days
/// - Returns annualized volatility
///
/// Formula:
/// Returns[t] = (Close[t] - Close[t-1])/Close[t-1]
/// σ²[t] = ω + α*Returns²[t-1] + β*σ²[t-1]
/// CV[t] = sqrt(σ²[t]) * sqrt(252) * 100
///
/// Where:
/// ω (omega) = long-term variance * (1 - α - β)
/// α (alpha) = weight of recent squared return
/// β (beta) = weight of previous variance
///
/// Market Applications:
/// - Risk measurement
/// - Option pricing
/// - Value at Risk (VaR)
/// - Portfolio optimization
/// - Volatility forecasting
///
/// Sources:
///     Bollerslev (1986)
///     https://en.wikipedia.org/wiki/GARCH
///
/// Note: Returns annualized volatility as a percentage
/// </remarks>

[SkipLocalsInit]
public sealed class Cv : AbstractBase
{
    private readonly int _period;
    private readonly double _alpha;
    private readonly double _beta;
    private readonly double _omega;
    private double _prevClose;
    private double _prevVariance;
    private double _longTermVariance;
    private bool _isInitialized;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Cv(int period = 20, double alpha = 0.1, double beta = 0.8)
    {
        _period = period;
        _alpha = alpha;
        _beta = beta;
        _omega = 0.001 * (1 - alpha - beta);  // Initial estimate, will be updated with actual data
        WarmupPeriod = period + 1;  // Need one extra period for returns
        Name = $"CV({_period})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Cv(object source, int period = 20, double alpha = 0.1, double beta = 0.8) : this(period, alpha, beta)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _prevClose = 0;
        _prevVariance = 0;
        _longTermVariance = 0;
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

        // Initialize with first available data if not done
        if (!_isInitialized && _index > _period)
        {
            _longTermVariance = squaredReturn;  // Use current squared return as initial estimate
            _prevVariance = _longTermVariance;
            _isInitialized = true;
        }

        // Need enough values for calculation
        if (_index <= _period)
        {
            return 0;
        }

        // Update variance estimate using GARCH(1,1)
        double variance = _omega + _alpha * squaredReturn + _beta * _prevVariance;
        _prevVariance = variance;

        // Calculate annualized volatility as percentage
        double volatility = Math.Sqrt(variance) * Math.Sqrt(252) * 100;

        IsHot = _index >= WarmupPeriod;
        return volatility;
    }
}
