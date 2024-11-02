using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// FRAMA: Fractal Adaptive Moving Average
/// An adaptive moving average that adjusts its smoothing factor based on the fractal dimension
/// of the price series. FRAMA automatically adapts to market conditions, becoming more responsive
/// during trends and more stable during sideways markets.
/// </summary>
/// <remarks>
/// The FRAMA algorithm works by:
/// 1. Calculating the fractal dimension of the price series
/// 2. Using this dimension to determine the optimal alpha (smoothing factor)
/// 3. Applying an EMA with the adaptive alpha
///
/// Key characteristics:
/// - Self-adaptive to market conditions
/// - Reduces lag during trending periods
/// - Increases smoothing during sideways markets
/// - Uses fractal geometry principles for market analysis
///
/// Sources:
///     John Ehlers - "FRAMA: A Trend-Following Indicator"
///     https://www.mesasoftware.com/papers/FRAMA.pdf
/// </remarks>

public class Frama : AbstractBase
{
    private readonly int _period;
    private readonly int _halfPeriod;
    private readonly double _periodRecip;
    private readonly double _halfPeriodRecip;
    private readonly double _log2 = System.Math.Log(2);
    private readonly double _epsilon = double.Epsilon;
    private readonly CircularBuffer _buffer;
    private double _lastFrama;
    private double _prevLastFrama;

    /// <param name="period">The number of periods used for fractal dimension calculation. Must be at least 2.</param>
    /// <exception cref="ArgumentException">Thrown when period is less than 2.</exception>
    public Frama(int period)
    {
        if (period < 2)
            throw new System.ArgumentException("Period must be at least 2", nameof(period));

        _period = period;
        _halfPeriod = period / 2;
        _periodRecip = 1.0 / period;
        _halfPeriodRecip = 1.0 / _halfPeriod;
        _buffer = new CircularBuffer(period);
        WarmupPeriod = period;
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of periods used for fractal dimension calculation.</param>
    public Frama(object source, int period) : this(period)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _buffer.Clear();
        _lastFrama = 0;
        _prevLastFrama = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _prevLastFrama = _lastFrama;
            _index++;
        }
        else
        {
            _lastFrama = _prevLastFrama;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateMinMax(double price, ref double high, ref double low)
    {
        high = System.Math.Max(high, price);
        low = System.Math.Min(low, price);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateAlpha(double dimension)
    {
        double alpha = System.Math.Exp(-4.6 * (dimension - 1));
        return System.Math.Clamp(alpha, 0.01, 1.0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override double GetLastValid()
    {
        return _lastFrama;
    }

    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        _buffer.Add(Input.Value, Input.IsNew);

        if (_buffer.Count < _period)
        {
            _lastFrama = _buffer.Average();
            return _lastFrama;
        }

        double hh = double.MinValue, ll = double.MaxValue;
        double hh1 = double.MinValue, ll1 = double.MaxValue;
        double hh2 = double.MinValue, ll2 = double.MaxValue;

        for (int i = 0; i < _period; i++)
        {
            double price = _buffer[i];
            UpdateMinMax(price, ref hh, ref ll);

            if (i < _halfPeriod)
            {
                UpdateMinMax(price, ref hh1, ref ll1);
            }
            else
            {
                UpdateMinMax(price, ref hh2, ref ll2);
            }
        }

        double n1 = (hh - ll) * _periodRecip;
        double n2 = (hh1 - ll1 + hh2 - ll2) * _halfPeriodRecip;

        double dimension = (System.Math.Log(n2 + _epsilon) - System.Math.Log(n1 + _epsilon)) / _log2;
        double alpha = CalculateAlpha(dimension);

        _lastFrama = alpha * (Input.Value - _lastFrama) + _lastFrama;

        IsHot = _index >= WarmupPeriod;
        return _lastFrama;
    }
}
