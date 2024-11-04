using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// RSV: Rogers-Satchell Volatility
/// A volatility measure that accounts for drift in the price process and
/// is independent of the mean return level.
/// </summary>
/// <remarks>
/// The RSV calculation process:
/// 1. Calculate log differences between prices
/// 2. Combine log differences in specific way
/// 3. Average over specified period
/// 4. Take square root for final volatility
///
/// Key characteristics:
/// - Drift-independent
/// - Uses all price data (HLOC)
/// - More efficient estimator
/// - Handles trending markets
/// - Non-zero mean returns
///
/// Formula:
/// RSV = sqrt(mean(ln(H/C) * ln(H/O) + ln(L/C) * ln(L/O)))
/// where H=High, L=Low, O=Open, C=Close
///
/// Market Applications:
/// - Volatility estimation
/// - Risk measurement
/// - Option pricing
/// - Trading system development
/// - Market regime identification
///
/// Note: More robust than simple volatility measures in trending markets
/// </remarks>
[SkipLocalsInit]
public sealed class Rsv : AbstractBase
{
    private readonly Sma _ma;
    private const int DefaultPeriod = 10;
    private double _prevValue;

    /// <param name="period">The number of periods for RSV calculation (default 10).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rsv(int period = DefaultPeriod)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));

        _ma = new(period);
        WarmupPeriod = period;
        Name = $"RSV({period})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of periods for RSV calculation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rsv(object source, int period = DefaultPeriod) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
            _index++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        if (!BarInput.IsNew)
            return _prevValue;

        ManageState(true);

        // Calculate log ratios
        double lnHC = Math.Log(BarInput.High / BarInput.Close);
        double lnHO = Math.Log(BarInput.High / BarInput.Open);
        double lnLC = Math.Log(BarInput.Low / BarInput.Close);
        double lnLO = Math.Log(BarInput.Low / BarInput.Open);

        // Calculate Rogers-Satchell term
        double rs = (lnHC * lnHO) + (lnLC * lnLO);

        // Apply moving average and take square root
        _prevValue = Math.Sqrt(_ma.Calc(rs, true));
        return _prevValue;
    }
}
