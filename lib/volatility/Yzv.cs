using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// YZV: Yang-Zhang Volatility
/// A volatility estimator that combines overnight and trading volatilities,
/// providing a more complete picture of price variation while being drift-independent.
/// </summary>
/// <remarks>
/// The YZV calculation process:
/// 1. Calculate overnight (close-to-open) volatility
/// 2. Calculate open-to-close volatility
/// 3. Calculate Rogers-Satchell volatility
/// 4. Combine components with optimal weights
///
/// Key characteristics:
/// - Drift independence
/// - Minimum variance
/// - Handles overnight gaps
/// - Uses all HLOC prices
/// - Optimal weighting
///
/// Formula:
/// YZV = sqrt(Vo + k*Vc + (1-k)*Vrs)
/// where:
/// Vo = overnight volatility
/// Vc = open-to-close volatility
/// Vrs = Rogers-Satchell volatility
/// k ≈ 0.34 (optimal weight)
///
/// Market Applications:
/// - Option pricing
/// - Risk measurement
/// - Trading systems
/// - Portfolio management
/// - Market analysis
///
/// Note: Most efficient unbiased estimator among drift-independent estimators
/// </remarks>
[SkipLocalsInit]
public sealed class Yzv : AbstractBase
{
    private readonly Sma _maCo; // Close-to-Open
    private readonly Sma _maOc; // Open-to-Close
    private readonly Sma _maRs; // Rogers-Satchell
    private double _prevClose;
    private double _prevValue;
    private const double K = 0.34; // Optimal weight
    private const int DefaultPeriod = 20;

    /// <param name="period">The number of periods for volatility calculation (default 20).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Yzv(int period = DefaultPeriod)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));

        _maCo = new(period);
        _maOc = new(period);
        _maRs = new(period);
        WarmupPeriod = period;
        Name = $"YZV({period})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of periods for volatility calculation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Yzv(object source, int period = DefaultPeriod) : this(period)
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

        // Calculate overnight volatility (close-to-open)
        double co = Math.Log(BarInput.Open / _prevClose);
        double vo = _maCo.Calc(co * co, true);

        // Calculate open-to-close volatility
        double oc = Math.Log(BarInput.Close / BarInput.Open);
        double vc = _maOc.Calc(oc * oc, true);

        // Calculate Rogers-Satchell volatility component
        double lnHC = Math.Log(BarInput.High / BarInput.Close);
        double lnHO = Math.Log(BarInput.High / BarInput.Open);
        double lnLC = Math.Log(BarInput.Low / BarInput.Close);
        double lnLO = Math.Log(BarInput.Low / BarInput.Open);
        double rs = lnHC * lnHO + lnLC * lnLO;
        double vrs = _maRs.Calc(rs, true);

        // Combine components with optimal weights
        _prevValue = Math.Sqrt(vo + K * vc + (1.0 - K) * vrs);
        return _prevValue;
    }
}
