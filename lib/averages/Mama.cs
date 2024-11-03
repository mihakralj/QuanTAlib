using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// MAMA: MESA Adaptive Moving Average
/// A highly sophisticated adaptive moving average that uses the MESA (Maximum Entropy
/// Spectral Analysis) algorithm to detect market cycles and adjust its smoothing
/// accordingly. MAMA provides both a faster (MAMA) and slower (FAMA) moving average.
/// </summary>
/// <remarks>
/// The MAMA calculation process:
/// 1. Uses Hilbert Transform to decompose price into phase and amplitude
/// 2. Calculates the dominant cycle period using phase analysis
/// 3. Determines phase position and rate of change
/// 4. Adapts smoothing based on phase changes
/// 5. Generates both MAMA and FAMA (Following Adaptive Moving Average)
///
/// Key characteristics:
/// - Highly adaptive to market conditions
/// - Provides two synchronized moving averages
/// - Uses cycle analysis for adaptation
/// - Excellent at identifying trend changes
/// - Combines multiple signal processing techniques
///
/// Sources:
///     John Ehlers - "MESA Adaptive Moving Averages"
///     https://www.mesasoftware.com/papers/MAMA.pdf
/// </remarks>
public class Mama : AbstractBase
{
    private readonly double _fastLimit, _slowLimit;
    private readonly CircularBuffer _pr, _sm, _dt, _i1, _q1, _i2, _q2, _re, _im, _pd, _ph;
    private readonly double _twoPi = 2.0 * System.Math.PI;
    private readonly double _radToDeg = 180.0 / System.Math.PI;
    private readonly double _alpha02 = 0.2;
    private readonly double _alpha08 = 0.8;
    private readonly double _famaAlpha = 0.5;

    private double _mama, _fama;
    private double _prevMama, _prevFama, _sumPr;
    private double _p_prevMama, _p_prevFama, _p_sumPr;

    /// <summary>
    /// Gets the Following Adaptive Moving Average (FAMA) value.
    /// </summary>
    public TValue Fama { get; private set; }

    public Mama(double fastLimit = 0.5, double slowLimit = 0.05)
    {
        Fama = new TValue();
        _fastLimit = fastLimit;
        _slowLimit = slowLimit;
        _pr = new(7);
        _sm = new(7);
        _dt = new(7);
        _q1 = new(7);
        _i1 = new(7);
        _i2 = new(2);
        _q2 = new(2);
        _re = new(2);
        _im = new(2);
        _pd = new(2);
        _ph = new(2);
        Name = $"Mama({_fastLimit:F2}, {_slowLimit:F2})";
        Init();
    }

    public Mama(object source, double fastLimit = 0.5, double slowLimit = 0.05) : this(fastLimit, slowLimit)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        Fama = new TValue();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _p_prevMama = _prevMama;
            _p_prevFama = _prevFama;
            _p_sumPr = _sumPr;
            _lastValidValue = Input.Value;
            _index++;
        }
        else
        {
            _prevMama = _p_prevMama;
            _prevFama = _p_prevFama;
            _sumPr = _p_sumPr;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateSmooth()
    {
        return ((4.0 * _pr[^1]) + (3.0 * _pr[^2]) + (2.0 * _pr[^3]) + _pr[^4]) * 0.1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double CalculateHilbertTransform(CircularBuffer buffer, double adj)
    {
        return ((0.0962 * (buffer[^1] - buffer[^7])) + (0.5769 * (buffer[^3] - buffer[^5]))) * adj;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculatePeriod(double im, double re)
    {
        if (im == 0 || re == 0) return _pd[^2];
        return _twoPi / System.Math.Atan(im / re);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double AdjustPeriod(double period)
    {
        period = System.Math.Clamp(period, 0.67 * _pd[^2], 1.5 * _pd[^2]);
        period = System.Math.Clamp(period, 6.0, 50.0);
        return (_alpha02 * period) + (_alpha08 * _pd[^2]);
    }

    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        _pr.Add(Input.Value, Input.IsNew);

        if (_index > 6)
        {
            double adj = (0.075 * _pd[^1]) + 0.54;

            // Smooth and Detrender
            _sm.Add(CalculateSmooth(), Input.IsNew);
            _dt.Add(CalculateHilbertTransform(_sm, adj), Input.IsNew);

            // In-phase and quadrature
            _q1.Add(CalculateHilbertTransform(_dt, adj), Input.IsNew);
            _i1.Add(_dt[^4], Input.IsNew);

            // Advance phases
            double jI = CalculateHilbertTransform(_i1, adj);
            double jQ = CalculateHilbertTransform(_q1, adj);

            // Phasor addition
            double i2 = _i1[^1] - jQ;
            double q2 = _q1[^1] + jI;
            _i2.Add(i2, Input.IsNew);
            _q2.Add(q2, Input.IsNew);
            _i2[^1] = (_alpha02 * _i2[^1]) + (_alpha08 * _i2[^2]);
            _q2[^1] = (_alpha02 * _q2[^1]) + (_alpha08 * _q2[^2]);

            // Homodyne discriminator
            double re = (_i2[^1] * _i2[^2]) + (_q2[^1] * _q2[^2]);
            double im = (_i2[^1] * _q2[^2]) - (_q2[^1] * _i2[^2]);
            _re.Add(re, Input.IsNew);
            _im.Add(im, Input.IsNew);
            _re[^1] = (_alpha02 * _re[^1]) + (_alpha08 * _re[^2]);
            _im[^1] = (_alpha02 * _im[^1]) + (_alpha08 * _im[^2]);

            // Calculate and adjust period
            double period = CalculatePeriod(_im[^1], _re[^1]);
            _pd.Add(period, Input.IsNew);
            _pd[^1] = AdjustPeriod(_pd[^1]);

            // Phase calculation
            double phase = _i1[^1] != 0 ? System.Math.Atan(_q1[^1] / _i1[^1]) * _radToDeg : _ph[^2];
            _ph.Add(phase, Input.IsNew);

            // Adaptive alpha
            double delta = System.Math.Max(_ph[^2] - _ph[^1], 1.0);
            double alpha = System.Math.Clamp(_fastLimit / delta, _slowLimit, _fastLimit);

            // Final indicators
            _mama = (alpha * (_pr[^1] - _prevMama)) + _prevMama;
            _fama = (_famaAlpha * alpha * (_mama - _prevFama)) + _prevFama;

            _prevMama = _mama;
            _prevFama = _fama;
        }
        else
        {
            InitializeBuffers();
            _sumPr += Input.Value;
            _mama = _fama = _prevMama = _prevFama = _sumPr / _index;
        }

        Fama = new TValue(Time: Input.Time, Value: _fama, IsNew: Input.IsNew);
        IsHot = _index >= 6;

        return _mama;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InitializeBuffers()
    {
        _pd.Add(0, Input.IsNew);
        _sm.Add(0, Input.IsNew);
        _dt.Add(0, Input.IsNew);
        _i1.Add(0, Input.IsNew);
        _q1.Add(0, Input.IsNew);
        _i2.Add(0, Input.IsNew);
        _q2.Add(0, Input.IsNew);
        _re.Add(0, Input.IsNew);
        _im.Add(0, Input.IsNew);
        _ph.Add(0, Input.IsNew);
    }
}
