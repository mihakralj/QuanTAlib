using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// DSMA: Deviation Scaled Moving Average
/// Adaptive moving average that adjusts its smoothing factor based on the volatility of the input data.
/// It aims to be more responsive during trending periods and more stable during ranging periods.
/// </summary>
/// <remarks>
/// The DSMA uses a SuperSmoother filter to reduce noise and a dynamic alpha calculation based on the
/// scaled deviation of the input data. This allows it to adapt to changing market conditions.
///
/// The algorithm involves these main steps:
/// 1. Apply a SuperSmoother filter to the zero-mean input data.
/// 2. Calculate the Root Mean Square (RMS) of the filtered data.
/// 3. Scale the filtered data by the RMS to get a measure in terms of standard deviations.
/// 4. Use the scaled deviation to calculate an adaptive alpha for the moving average.
///
/// Source:
///    https://www.mesasoftware.com/papers/DEVIATION%20SCALED%20MOVING%20AVERAGE.pdf
/// </remarks>

public class Dsma : AbstractBase
{
    private readonly CircularBuffer _buffer;
    private readonly double _c2, _c3;
    private readonly double _scaleFactor;
    private readonly double _periodRecip;  // 1/_period
    private readonly double _scaleByPeriod; // 5/_period
    private readonly double _c1Half;  // _c1/2

    private double _lastDsma, _p_lastDsma;
    private double _filt, _filt1, _filt2, _zeros, _zeros1;
    private double _p_filt, _p_filt1, _p_filt2, _p_zeros, _p_zeros1;
    private bool _isInit, _p_isInit;

    /// <summary>
    /// Initializes a new instance of the <see cref="Dsma"/> class.
    /// </summary>
    /// <param name="period">The number of data points used in the DSMA calculation.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when period is less than 1.</exception>
    public Dsma(int period, double scaleFactor = 0.9)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        if (scaleFactor <= 0 || scaleFactor > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(scaleFactor), "Scale factor must be between 0 and 1 (exclusive).");
        }
        _periodRecip = 1.0 / period;
        _scaleFactor = scaleFactor;
        _buffer = new CircularBuffer(period);

        // SuperSmoother filter coefficients
        double halfPeriod = 0.5 * period;
        double a1 = System.Math.Exp(-1.414 * System.Math.PI / halfPeriod);
        double b1 = 2.0 * a1 * System.Math.Cos(1.414 * System.Math.PI / halfPeriod);

        _c2 = b1;
        _c3 = -a1 * a1;
        double _c1 = 1.0 - _c2 - _c3;
        _c1Half = _c1 * 0.5;
        _scaleByPeriod = 5.0 / period;

        Name = "Dsma";
        WarmupPeriod = (int)(period * 1.5); // A conservative estimate
        Init();
    }

    public Dsma(object source, int period, double scaleFactor = 0.9) : this(period, scaleFactor)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _lastDsma = 0;
        _filt = _filt1 = _filt2 = 0;
        _zeros = _zeros1 = 0;
        _isInit = false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _p_lastDsma = _lastDsma;
            _p_isInit = _isInit;
            _p_zeros = _zeros;
            _p_zeros1 = _zeros1;
            _p_filt = _filt;
            _p_filt1 = _filt1;
            _p_filt2 = _filt2;
            _index++;
        }
        else
        {
            _lastDsma = _p_lastDsma;
            _isInit = _p_isInit;
            _zeros = _p_zeros;
            _zeros1 = _p_zeros1;
            _filt = _p_filt;
            _filt1 = _p_filt1;
            _filt2 = _p_filt2;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateSuperSmootherFilter()
    {
        return _c1Half * (_zeros + _zeros1) + _c2 * _filt1 + _c3 * _filt2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateAdaptiveAlpha(double scaledFilt)
    {
        double alpha = _scaleFactor * System.Math.Abs(scaledFilt) * _scaleByPeriod;
        return System.Math.Clamp(alpha, 0.1, 1.0);
    }

    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        if (!_isInit)
        {
            _lastDsma = Input.Value;
            _isInit = true;
            return _lastDsma;
        }

        // Produce nominal zero mean
        _zeros = Input.Value - _lastDsma;

        // SuperSmoother Filter
        _filt = CalculateSuperSmootherFilter();

        // Update buffer for RMS calculation
        double filtSquared = _filt * _filt;
        _buffer.Add(filtSquared, Input.IsNew);

        // Compute RMS (Root Mean Square)
        double rms = System.Math.Sqrt(_buffer.Sum() * _periodRecip);

        // Rescale Filt in terms of Standard Deviations and calculate adaptive alpha
        double scaledFilt = rms > 0 ? _filt / rms : 0;
        double alpha = CalculateAdaptiveAlpha(scaledFilt);

        // DSMA calculation
        double dsma = alpha * Input.Value + (1 - alpha) * _lastDsma;

        // Update state variables
        _zeros1 = _zeros;
        _filt2 = _filt1;
        _filt1 = _filt;
        _lastDsma = dsma;

        IsHot = _index >= WarmupPeriod;
        return dsma;
    }
}
