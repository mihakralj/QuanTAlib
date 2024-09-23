
using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// ALMA: Arnaud Legoux Moving Average
/// Uses the curve of the Normal (Gauss) distribution. This moving average reduces lag
/// of the data in conjunction with smoothing to reduce noise.
/// </summary>
/// <remarks>
/// Smoothness:     ★★★★☆ (4/5)
/// Sensitivity:    ★★★★☆ (4/5)
/// Overshooting:   ★★★★☆ (4/5)
/// Lag:            ★★★★★ (5/5)
///
/// Validation:
///    Skender.Stock.Indicators
/// </remarks>

public class Alma : AbstractBase
{
    private readonly int _period;
    private readonly double _offset;
    private readonly double _sigma;
    private CircularBuffer? _buffer;
    private CircularBuffer? _weight;
    private double _norm;

    /// <param name="period">The number of data points used in the ALMA calculation.</param>
    /// <param name="offset">Controls the smoothness and high-frequency filtering. Default is 0.85.</param>
    /// <param name="sigma">Controls the shape of the Gaussian distribution. Default is 6.</param>
    /// <exception cref="ArgumentException">Thrown when period is less than 1.</exception>
    public Alma(int period, double offset = 0.85, double sigma = 6) : base()
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be greater than or equal to 1.", nameof(period));
        }
        _period = period;
        _offset = offset;
        _sigma = sigma;
        WarmupPeriod = period;
        Name = "Alma";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="period">The number of data points used in the ALMA calculation.</param>
    /// <param name="offset">Controls the smoothness and high-frequency filtering. Default is 0.85.</param>
    /// <param name="sigma">Controls the shape of the Gaussian distribution. Default is 6.</param>
    public Alma(object source, int period, double offset = 0.85, double sigma = 6) : this(period, offset, sigma)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    public override void Init()
    {
        base.Init();
        _buffer = new CircularBuffer(_period);
        _weight = new CircularBuffer(_period);
        _norm = 0;
    }

    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _index++;
        }
    }

    /// <summary>
    /// Performs the core ALMA calculation. Called from parent abstractBase Calc()
    /// </summary>
    /// <returns>The calculated ALMA value.</returns>
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        _buffer!.Add(Input.Value, Input.IsNew);
        if (_weight!.Count < _buffer.Count)
        {
            for (var i = 0; i < _buffer.Count - _weight.Count; i++)
            {
                _weight.Add(0.0);
            }
        }

        if (_buffer.Count <= _period)
        {
            UpdateWeights();
        }

        double weightedSum = 0;
        for (var i = 0; i < _buffer.Count; i++)
        {
            weightedSum += _weight[i] * _buffer[i];
        }

        double result = weightedSum / _norm;

        IsHot = _index >= WarmupPeriod;
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateWeights()
    {
        int len = _buffer!.Count;
        _norm = 0;
        double m = _offset * (len - 1);
        double s = len / _sigma;
        for (int i = 0; i < len; i++)
        {
            double wt = Math.Exp(-((i - m) * (i - m)) / (2 * s * s));
            _weight![i] = wt;
            _norm += wt;
        }
    }
}