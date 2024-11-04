using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// SV: Stochastic Volatility
/// A volatility measure that models price volatility as a random process,
/// capturing both the magnitude and the rate of change in price movements.
/// </summary>
/// <remarks>
/// The SV calculation process:
/// 1. Calculate log returns
/// 2. Compute exponentially weighted variance
/// 3. Apply smoothing to variance estimate
/// 4. Take square root for volatility
///
/// Key characteristics:
/// - Time-varying volatility
/// - Mean-reverting process
/// - Captures volatility clustering
/// - Handles leverage effects
/// - Accounts for fat tails
///
/// Formula:
/// Returns = ln(Close/PrevClose)
/// Variance = λ * PrevVariance + (1-λ) * Returns²
/// SV = sqrt(Variance)
/// where λ is the decay factor
///
/// Market Applications:
/// - Option pricing
/// - Risk management
/// - Trading strategies
/// - Portfolio optimization
/// - Market regime detection
///
/// Note: More sophisticated than simple volatility measures, better captures market dynamics
/// </remarks>
[SkipLocalsInit]
public sealed class Sv : AbstractBase
{
    private readonly double _lambda;
    private readonly Sma _ma;
    private double _prevClose;
    private double _prevVariance;
    private double _prevValue;
    private const int DefaultPeriod = 20;
    private const double DefaultLambda = 0.94;

    /// <param name="period">The number of periods for smoothing (default 20).</param>
    /// <param name="lambda">The decay factor for variance calculation (default 0.94).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1 or lambda is not between 0 and 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Sv(int period = DefaultPeriod, double lambda = DefaultLambda)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (lambda <= 0 || lambda >= 1)
            throw new ArgumentOutOfRangeException(nameof(lambda));

        _lambda = lambda;
        _ma = new(period);
        WarmupPeriod = period;
        Name = $"SV({period},{lambda:F2})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of periods for smoothing.</param>
    /// <param name="lambda">The decay factor for variance calculation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Sv(object source, int period = DefaultPeriod, double lambda = DefaultLambda) : this(period, lambda)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _prevClose = BarInput.Close;
            _index++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        if (!BarInput.IsNew)
            return _prevValue;

        ManageState(true);

        // Calculate log return
        double logReturn = Math.Log(BarInput.Close / _prevClose);
        double squaredReturn = logReturn * logReturn;

        // Update variance estimate
        _prevVariance = _lambda * _prevVariance + (1.0 - _lambda) * squaredReturn;

        // Apply smoothing and take square root
        _prevValue = Math.Sqrt(_ma.Calc(_prevVariance, true));
        return _prevValue;
    }
}
