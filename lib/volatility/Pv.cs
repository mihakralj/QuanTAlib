using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// PV: Parkinson Volatility
/// A volatility measure that uses the high and low prices to estimate
/// volatility, assuming continuous trading and log-normal price distribution.
/// </summary>
/// <remarks>
/// The PV calculation process:
/// 1. Calculate squared log range for each period
/// 2. Apply scaling factor (1/4ln2)
/// 3. Average over specified period
/// 4. Take square root for final volatility
///
/// Key characteristics:
/// - Range-based volatility
/// - More efficient than close-to-close
/// - Assumes continuous trading
/// - No gap consideration
/// - Log-normal distribution
///
/// Formula:
/// PV = sqrt(1/(4*ln(2)*n) * Σ(ln(High/Low))²)
/// where n is the number of periods
///
/// Market Applications:
/// - Volatility estimation
/// - Risk assessment
/// - Option pricing
/// - Trading system development
/// - Market regime identification
///
/// Note: More efficient than traditional volatility measures but sensitive to gaps
/// </remarks>
[SkipLocalsInit]
public sealed class Pv : AbstractBase
{
    private readonly Sma _ma;
    private readonly double _scaleFactor;
    private const int DefaultPeriod = 10;
    private double _prevValue;

    /// <param name="period">The number of periods for PV calculation (default 10).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Pv(int period = DefaultPeriod)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));

        _ma = new(period);
        _scaleFactor = 1.0 / (4.0 * Math.Log(2.0));
        WarmupPeriod = period;
        Name = $"PV({period})";
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of periods for PV calculation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Pv(object source, int period = DefaultPeriod) : this(period)
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

        // Calculate log range squared
        double logRange = Math.Log(BarInput.High / BarInput.Low);
        double logRangeSquared = logRange * logRange;

        // Apply moving average and scaling
        double meanLogRangeSquared = _ma.Calc(logRangeSquared, true);

        // Calculate final volatility
        _prevValue = Math.Sqrt(_scaleFactor * meanLogRangeSquared);
        return _prevValue;
    }
}
