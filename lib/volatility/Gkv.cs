using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// GKV: Garman-Klass Volatility
/// An efficient estimator of volatility that uses open, high, low,
/// and close prices to capture intraday price movements.
/// </summary>
/// <remarks>
/// The GKV calculation process:
/// 1. Calculate components using OHLC prices
/// 2. Combine components using optimal weights
/// 3. Take rolling average over period
/// 4. Annualize and convert to percentage
///
/// Key characteristics:
/// - More efficient than close-to-close volatility
/// - Uses full OHLC price information
/// - Default period is 20 days
/// - Annualized by default
/// - Expressed as a percentage
///
/// Formula:
/// u = ln(High/Low)²/2
/// c = ln(Close/Open)²
/// GKV = sqrt(sum((0.5*u - (2*ln(2)-1)*c) / period) * 252) * 100
///
/// Market Applications:
/// - Volatility estimation
/// - Risk measurement
/// - Option pricing
/// - Trading strategy development
/// - Market analysis
///
/// Sources:
///     Garman and Klass (1980)
///     Journal of Business 53(1): 67-78
///
/// Note: Returns annualized volatility as a percentage
/// </remarks>

[SkipLocalsInit]
public sealed class Gkv : AbstractBase
{
    private readonly int _period;
    private readonly bool _annualize;
    private readonly CircularBuffer _components;
    private readonly double _ln2;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Gkv(int period = 20, bool annualize = true)
    {
        _period = period;
        _annualize = annualize;
        WarmupPeriod = period;
        Name = $"GKV({_period})";
        _components = new CircularBuffer(period);
        _ln2 = Math.Log(2);
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Gkv(object source, int period = 20, bool annualize = true) : this(period, annualize)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _components.Clear();
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

        // Calculate components
        double u = Math.Log(BarInput.High / BarInput.Low);
        u = u * u / 2;

        double c = Math.Log(BarInput.Close / BarInput.Open);
        c = c * c;

        // Combine components with optimal weights
        double component = 0.5 * u - (2 * _ln2 - 1) * c;
        _components.Add(component);

        // Need enough values for calculation
        if (_index <= _period)
        {
            return 0;
        }

        // Calculate average component
        double avgComponent = _components.Average();

        // Calculate volatility
        double volatility = Math.Sqrt(avgComponent);

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
