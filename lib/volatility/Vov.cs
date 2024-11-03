using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// VOV: Volatility of Volatility
/// A technical indicator that measures the volatility of volatility itself,
/// providing insight into the stability of market volatility.
/// </summary>
/// <remarks>
/// The VOV calculation process:
/// 1. Calculate primary volatility (e.g., using True Range)
/// 2. Calculate standard deviation of primary volatility
/// 3. Normalize result for comparison
///
/// Key characteristics:
/// - Second-order volatility measure
/// - Default period is 20 days
/// - Always positive
/// - No upper bound
/// - Measures volatility stability
///
/// Formula:
/// Primary Volatility = TR (True Range)
/// VOV = StdDev(Primary Volatility, period) / Average(Primary Volatility, period)
///
/// Market Applications:
/// - Risk of risk assessment
/// - Volatility regime changes
/// - Market stability analysis
/// - Trading strategy adaptation
/// - Risk management
///
/// Note: Higher values indicate more unstable volatility conditions
/// </remarks>
[SkipLocalsInit]
public sealed class Vov : AbstractBase
{
    private readonly int _period;
    private readonly CircularBuffer _volatilities;
    private double _prevClose;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vov(int period = 20)
    {
        _period = period;
        WarmupPeriod = period + 1;  // Need extra period for TR calculation
        Name = $"VOV({_period})";
        _volatilities = new CircularBuffer(period);
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vov(object source, int period = 20) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _prevClose = 0;
        _volatilities.Clear();
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
    private double CalculateVariance(CircularBuffer buffer)
    {
        if (buffer.Count == 0) return 0;
        double mean = buffer.Average();
        double sumSquaredDiff = 0;
        for (int i = 0; i < buffer.Count; i++)
        {
            double diff = buffer[i] - mean;
            sumSquaredDiff += diff * diff;
        }
        return sumSquaredDiff / buffer.Count;
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

        // Calculate True Range as primary volatility measure
        double tr = Math.Max(BarInput.High - BarInput.Low,
                   Math.Max(Math.Abs(BarInput.High - _prevClose),
                          Math.Abs(BarInput.Low - _prevClose)));

        // Store current close for next calculation
        _prevClose = BarInput.Close;

        // Add volatility to buffer
        _volatilities.Add(tr);

        // Need enough volatilities for VOV calculation
        if (_index <= _period)
        {
            return 0;
        }

        // Calculate mean volatility
        double meanVol = _volatilities.Average();

        // Calculate VOV (normalized standard deviation)
        double vov = meanVol > double.Epsilon ? Math.Sqrt(CalculateVariance(_volatilities)) / meanVol : 0;

        IsHot = _index >= WarmupPeriod;
        return vov;
    }
}
