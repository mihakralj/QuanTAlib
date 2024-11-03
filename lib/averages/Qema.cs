using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// QEMA: Quadruple Exponential Moving Average
/// A sophisticated moving average that applies four exponential moving averages in sequence
/// and combines them using a specific formula to reduce lag while maintaining smoothness.
/// The final combination is: 4*EMA1 - 6*EMA2 + 4*EMA3 - EMA4
/// </summary>
/// <remarks>
/// The QEMA calculation process:
/// 1. Applies first EMA to price data
/// 2. Applies second EMA to result of first EMA
/// 3. Applies third EMA to result of second EMA
/// 4. Applies fourth EMA to result of third EMA
/// 5. Combines results using the formula: 4*EMA1 - 6*EMA2 + 4*EMA3 - EMA4
///
/// Key characteristics:
/// - Multiple EMA smoothing stages
/// - Reduced lag through combination formula
/// - Customizable smoothing factors for each EMA
/// - Better noise reduction than single EMA
/// - Maintains responsiveness to significant moves
///
/// Implementation:
///     Based on quadruple exponential smoothing principles
///     with optimized combination formula
/// </remarks>

public class Qema : AbstractBase
{
    private readonly Ema _ema1, _ema2, _ema3, _ema4;
    private double _lastQema, _p_lastQema;

    /// <param name="k1">Smoothing factor for first EMA (default 0.2).</param>
    /// <param name="k2">Smoothing factor for second EMA (default 0.2).</param>
    /// <param name="k3">Smoothing factor for third EMA (default 0.2).</param>
    /// <param name="k4">Smoothing factor for fourth EMA (default 0.2).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any k value is less than or equal to 0.</exception>
    public Qema(double k1 = 0.2, double k2 = 0.2, double k3 = 0.2, double k4 = 0.2)
    {
        if (k1 <= 0 || k2 <= 0 || k3 <= 0 || k4 <= 0)
        {
            throw new System.ArgumentOutOfRangeException(nameof(k1), "All k values must be in the range (0, 1].");
        }

        _ema1 = new Ema(k1);
        _ema2 = new Ema(k2);
        _ema3 = new Ema(k3);
        _ema4 = new Ema(k4);

        Name = $"QEMA ({k1:F2},{k2:F2},{k3:F2},{k4:F2})";
        double smK = System.Math.Min(System.Math.Min(k1, k2), System.Math.Min(k3, k4));
        WarmupPeriod = (int)((2 - smK) / smK);
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="k1">Smoothing factor for first EMA.</param>
    /// <param name="k2">Smoothing factor for second EMA.</param>
    /// <param name="k3">Smoothing factor for third EMA.</param>
    /// <param name="k4">Smoothing factor for fourth EMA.</param>
    public Qema(object source, double k1, double k2, double k3, double k4)
        : this(k1, k2, k3, k4)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _lastQema = 0;
        _p_lastQema = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _p_lastQema = _lastQema;
            _index++;
        }
        else
        {
            _lastQema = _p_lastQema;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateEma(Ema ema, double value)
    {
        var tempValue = new TValue(Input.Time, value, Input.IsNew);
        return ema.Calc(tempValue).Value;
    }

    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        // Calculate EMAs in sequence
        double ema1 = CalculateEma(_ema1, Input.Value);
        double ema2 = CalculateEma(_ema2, ema1);
        double ema3 = CalculateEma(_ema3, ema2);
        double ema4 = CalculateEma(_ema4, ema3);

        // Combine EMAs using optimized formula
        _lastQema = (4.0 * (ema1 + ema3)) - ((6.0 * ema2) + ema4);

        IsHot = _index >= WarmupPeriod;
        return _lastQema;
    }
}
