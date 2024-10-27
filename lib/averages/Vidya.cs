using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// VIDYA: Variable Index Dynamic Average
/// An adaptive moving average that adjusts its smoothing based on the ratio of
/// short-term to long-term volatility. This allows the average to become more
/// responsive during volatile periods and more stable during quiet periods.
/// </summary>
/// <remarks>
/// The VIDYA calculation process:
/// 1. Calculates standard deviation for short and long periods
/// 2. Uses ratio of short/long volatility to determine smoothing
/// 3. Applies variable smoothing factor to price data
/// 4. Adapts automatically to changing market conditions
///
/// Key characteristics:
/// - Adaptive smoothing based on volatility
/// - More responsive during volatile periods
/// - More stable during quiet periods
/// - Uses standard deviation for volatility measurement
/// - Combines short and long-term market analysis
///
/// Sources:
///     Tushar Chande - "Beyond Technical Analysis"
///     https://www.investopedia.com/terms/v/vidya.asp
/// </remarks>

public class Vidya : AbstractBase
{
    private readonly int _longPeriod;
    private readonly double _alpha;
    private readonly CircularBuffer _shortBuffer;
    private readonly CircularBuffer _longBuffer;
    private double _lastVIDYA, _p_lastVIDYA;

    /// <param name="shortPeriod">The number of periods for short-term volatility calculation.</param>
    /// <param name="longPeriod">The number of periods for long-term volatility calculation (default is 4x shortPeriod).</param>
    /// <param name="alpha">The alpha parameter controlling the base smoothing factor (default 0.2).</param>
    /// <exception cref="ArgumentException">Thrown when shortPeriod is less than 1.</exception>
    public Vidya(int shortPeriod, int longPeriod = 0, double alpha = 0.2)
    {
        if (shortPeriod < 1)
        {
            throw new System.ArgumentException("Short period must be greater than or equal to 1.", nameof(shortPeriod));
        }
        _longPeriod = (longPeriod == 0) ? shortPeriod * 4 : longPeriod;
        _alpha = alpha;
        _shortBuffer = new CircularBuffer(shortPeriod);
        _longBuffer = new CircularBuffer(_longPeriod);
        WarmupPeriod = _longPeriod;
        Name = $"Vidya({shortPeriod},{_longPeriod})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="shortPeriod">The number of periods for short-term volatility calculation.</param>
    /// <param name="longPeriod">The number of periods for long-term volatility calculation (default is 4x shortPeriod).</param>
    /// <param name="alpha">The alpha parameter controlling the base smoothing factor (default 0.2).</param>
    public Vidya(object source, int shortPeriod, int longPeriod = 0, double alpha = 0.2)
        : this(shortPeriod, longPeriod, alpha)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _lastVIDYA = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Input.Value;
            _index++;
            _p_lastVIDYA = _lastVIDYA;
        }
        else
        {
            _lastVIDYA = _p_lastVIDYA;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double CalculateStdDev(CircularBuffer buffer)
    {
        double mean = buffer.Average();
        double sumSquaredDiff = 0;
        var span = buffer.GetSpan();

        for (int i = 0; i < buffer.Count; i++)
        {
            double diff = span[i] - mean;
            sumSquaredDiff += diff * diff;
        }

        return System.Math.Sqrt(sumSquaredDiff / buffer.Count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateVidya(double shortStdDev, double longStdDev)
    {
        double s = _alpha * (shortStdDev / longStdDev);
        return (s * Input.Value) + ((1.0 - s) * _lastVIDYA);
    }

    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        _shortBuffer.Add(Input.Value, Input.IsNew);
        _longBuffer.Add(Input.Value, Input.IsNew);

        double vidya;
        if (_index <= _longPeriod)
        {
            vidya = _shortBuffer.Average();
        }
        else
        {
            double shortStdDev = CalculateStdDev(_shortBuffer);
            double longStdDev = CalculateStdDev(_longBuffer);
            vidya = CalculateVidya(shortStdDev, longStdDev);
        }

        _lastVIDYA = vidya;
        IsHot = _index >= WarmupPeriod;

        return vidya;
    }
}
