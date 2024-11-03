using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// VR: Volatility Ratio
/// A technical indicator that compares volatility across different time periods
/// to identify changes in market conditions.
/// </summary>
/// <remarks>
/// The VR calculation process:
/// 1. Calculate short-term volatility
/// 2. Calculate long-term volatility
/// 3. Calculate ratio between them
///
/// Key characteristics:
/// - Relative volatility measure
/// - Default periods are 10 and 20 days
/// - Values above 1 indicate increasing volatility
/// - Values below 1 indicate decreasing volatility
/// - Normalized comparison
///
/// Formula:
/// Short Volatility = StdDev(Returns, shortPeriod)
/// Long Volatility = StdDev(Returns, longPeriod)
/// VR = Short Volatility / Long Volatility
///
/// Market Applications:
/// - Volatility regime changes
/// - Market condition analysis
/// - Risk assessment
/// - Trading strategy adaptation
/// - Trend confirmation
///
/// Note: Values significantly different from 1 indicate changing market conditions
/// </remarks>
[SkipLocalsInit]
public sealed class Vr : AbstractBase
{
    private readonly int _longPeriod;
    private readonly CircularBuffer _shortReturns;
    private readonly CircularBuffer _longReturns;
    private double _prevClose;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vr(int shortPeriod = 10, int longPeriod = 20)
    {
        _longPeriod = longPeriod;
        WarmupPeriod = longPeriod + 1;  // Need one extra period for returns
        Name = $"VR({shortPeriod},{_longPeriod})";
        _shortReturns = new CircularBuffer(shortPeriod);
        _longReturns = new CircularBuffer(longPeriod);
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vr(object source, int shortPeriod = 10, int longPeriod = 20) : this(shortPeriod, longPeriod)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new BarSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _prevClose = 0;
        _shortReturns.Clear();
        _longReturns.Clear();
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

        // Calculate return
        double ret = _prevClose > double.Epsilon ? Math.Log(BarInput.Close / _prevClose) : 0;

        // Add return to buffers
        _shortReturns.Add(ret);
        _longReturns.Add(ret);

        // Store current close for next calculation
        _prevClose = BarInput.Close;

        // Need enough returns for both periods
        if (_index <= _longPeriod)
        {
            return 0;
        }

        // Calculate volatilities
        double shortVol = Math.Sqrt(CalculateVariance(_shortReturns));
        double longVol = Math.Sqrt(CalculateVariance(_longReturns));

        // Calculate ratio
        double vr = longVol > double.Epsilon ? shortVol / longVol : 1;

        IsHot = _index >= WarmupPeriod;
        return vr;
    }
}
