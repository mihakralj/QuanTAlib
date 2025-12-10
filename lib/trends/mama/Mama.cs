using System;
using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// MESA Adaptive Moving Average (MAMA)
/// A trend-following indicator that adapts to the market's phase rate of change.
/// </summary>
[SkipLocalsInit]
public sealed class Mama : ITValuePublisher
{
    public TValue Last { get; private set; }
    public TValue Fama { get; private set; }
    public bool IsHot => _index > 6;
    public event Action<TValue>? Pub;

    private readonly double _fastLimit;
    private readonly double _slowLimit;

    private double _period, _p_period;
    private double _phase, _p_phase;
    private double _mama, _p_mama;
    private double _fama, _p_fama;
    private double _sumPr, _p_sumPr;
    private int _index;

    // State variables for IIR filters need to be preserved
    private double _i2, _p_i2;
    private double _q2, _p_q2;
    private double _re, _p_re;
    private double _im, _p_im;
    private double _lastValidPrice;

    private readonly RingBuffer _priceBuffer;
    private readonly RingBuffer _smoothBuffer;
    private readonly RingBuffer _detrender;
    private readonly RingBuffer _I1_buffer;
    private readonly RingBuffer _Q1_buffer;

    private const double c1 = 0.0962;
    private const double c2 = 0.5769;
    private const double TWOPI = 2.0 * Math.PI;
    private const double RadToDeg = 180.0 / Math.PI;

    public Mama(double fastLimit = 0.5, double slowLimit = 0.05)
    {
        if (fastLimit <= slowLimit || fastLimit <= 0 || slowLimit <= 0)
        {
            throw new ArgumentException("FastLimit must be > SlowLimit and > 0");
        }
        _fastLimit = fastLimit;
        _slowLimit = slowLimit;

        _priceBuffer = new RingBuffer(7);
        _smoothBuffer = new RingBuffer(7);
        _detrender = new RingBuffer(7);
        _I1_buffer = new RingBuffer(7);
        _Q1_buffer = new RingBuffer(7);

        Name = $"Mama({fastLimit:F2},{slowLimit:F2})";
        Init();
    }

    public Mama(ITValuePublisher source, double fastLimit = 0.5, double slowLimit = 0.05) : this(fastLimit, slowLimit)
    {
        source.Pub += (item) => Update(item);
    }

    public void Init()
    {
        _period = _p_period = 0.0;
        _phase = _p_phase = 0.0;
        _mama = _p_mama = double.NaN;
        _fama = _p_fama = double.NaN;
        _sumPr = _p_sumPr = 0.0;
        _index = 0;

        _i2 = _p_i2 = 0.0;
        _q2 = _p_q2 = 0.0;
        _re = _p_re = 0.0;
        _im = _p_im = 0.0;
        _lastValidPrice = 0.0;

        _priceBuffer.Clear();
        _smoothBuffer.Clear();
        _detrender.Clear();
        _I1_buffer.Clear();
        _Q1_buffer.Clear();

        Last = new TValue(DateTime.MinValue, double.NaN);
        Fama = new TValue(DateTime.MinValue, double.NaN);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_period = _period;
            _p_phase = _phase;
            _p_mama = _mama;
            _p_fama = _fama;
            _p_sumPr = _sumPr;
            _p_i2 = _i2;
            _p_q2 = _q2;
            _p_re = _re;
            _p_im = _im;
            _index++;
        }
        else
        {
            _period = _p_period;
            _phase = _p_phase;
            _mama = _p_mama;
            _fama = _p_fama;
            _sumPr = _p_sumPr;
            _i2 = _p_i2;
            _q2 = _p_q2;
            _re = _p_re;
            _im = _p_im;
        }

        double price = input.Value;
        if (!double.IsFinite(price))
        {
            price = _lastValidPrice;
        }
        else
        {
            _lastValidPrice = price;
        }

        _priceBuffer.Add(price, isNew);

        if (_index > 6)
        {
            double adj = (0.075 * _period) + 0.54;

            // Smooth
            double smooth = (4.0 * _priceBuffer[0] + 3.0 * _priceBuffer[1] + 2.0 * _priceBuffer[2] + _priceBuffer[3]) * 0.1;
            _smoothBuffer.Add(smooth, isNew);

            // Detrender
            double dt = (c1 * _smoothBuffer[0] + c2 * _smoothBuffer[2] - c2 * _smoothBuffer[4] - c1 * _smoothBuffer[6]) * adj;
            _detrender.Add(dt, isNew);

            // Q1
            double q1 = (c1 * dt + c2 * _detrender[2] - c2 * _detrender[4] - c1 * _detrender[6]) * adj;
            _Q1_buffer.Add(q1, isNew);

            // I1 = dt[3]
            double i1 = _detrender[3];
            _I1_buffer.Add(i1, isNew);

            // Advance phases
            // jI = CalculateHilbertTransform(_i1, adj)
            double jI = (c1 * i1 + c2 * _I1_buffer[2] - c2 * _I1_buffer[4] - c1 * _I1_buffer[6]) * adj;
            // jQ = CalculateHilbertTransform(_q1, adj)
            double jQ = (c1 * q1 + c2 * _Q1_buffer[2] - c2 * _Q1_buffer[4] - c1 * _Q1_buffer[6]) * adj;

            // Phasor addition
            double i2_val = i1 - jQ;
            double q2_val = q1 + jI;

            // Smooth i2, q2
            _i2 = 0.2 * i2_val + 0.8 * _p_i2;
            _q2 = 0.2 * q2_val + 0.8 * _p_q2;

            // Homodyne discriminator
            double re_val = (_i2 * _p_i2) + (_q2 * _p_q2);
            double im_val = (_i2 * _p_q2) - (_q2 * _p_i2);

            // Smooth re, im
            _re = 0.2 * re_val + 0.8 * _p_re;
            _im = 0.2 * im_val + 0.8 * _p_im;

            // Calculate Period
            double period = (Math.Abs(_im) > double.Epsilon && Math.Abs(_re) > double.Epsilon)
                ? TWOPI / Math.Atan(_im / _re)
                : 0.0;

            // Adjust Period
            period = period > 1.5 * _p_period ? 1.5 * _p_period : period;
            period = period < 0.67 * _p_period ? 0.67 * _p_period : period;
            period = period < 6.0 ? 6.0 : period;
            period = period > 50.0 ? 50.0 : period;

            // Smooth Period
            _period = 0.2 * period + 0.8 * _p_period;

            // Phase calculation
            _phase = Math.Abs(i1) >= double.Epsilon ? Math.Atan(q1 / i1) * RadToDeg : 0.0;

            // Adaptive alpha
            double delta = Math.Max(_p_phase - _phase, 1.0);
            double alpha = _fastLimit / delta;
            alpha = Math.Clamp(alpha, _slowLimit, _fastLimit);

            // Final indicators
            _mama = alpha * _priceBuffer[0] + (1.0 - alpha) * _p_mama;
            _fama = 0.5 * alpha * _mama + (1.0 - 0.5 * alpha) * _p_fama;
        }
        else
        {
            // Initialization phase
            _sumPr += input.Value;
            double avg = _index > 0 ? _sumPr / _index : input.Value;
            _mama = avg;
            _fama = avg;
            
            // Initialize buffers with 0
            _smoothBuffer.Add(0, isNew);
            _detrender.Add(0, isNew);
            _I1_buffer.Add(0, isNew);
            _Q1_buffer.Add(0, isNew);
        }

        Last = new TValue(input.Time, _mama);
        Fama = new TValue(input.Time, _fama);
        Pub?.Invoke(Last);
        return Last;
    }

    public TSeries Update(TSeries source)
    {
        if (source.Count == 0) return new TSeries();

        int len = source.Count;
        var v = new List<double>(len);
        var t = new List<long>(len);

        var temp = new Mama(_fastLimit, _slowLimit);
        for (int i = 0; i < len; i++)
        {
            var item = source[i];
            var result = temp.Update(item);
            v.Add(result.Value);
            t.Add(item.Time);
        }

        // Copy state from temp to this
        _period = temp._period;
        _p_period = temp._p_period;
        _phase = temp._phase;
        _p_phase = temp._p_phase;
        _mama = temp._mama;
        _p_mama = temp._p_mama;
        _fama = temp._fama;
        _p_fama = temp._p_fama;
        _sumPr = temp._sumPr;
        _p_sumPr = temp._p_sumPr;
        _index = temp._index;

        _i2 = temp._i2;
        _p_i2 = temp._p_i2;
        _q2 = temp._q2;
        _p_q2 = temp._p_q2;
        _re = temp._re;
        _p_re = temp._p_re;
        _im = temp._im;
        _p_im = temp._p_im;
        _lastValidPrice = temp._lastValidPrice;

        _priceBuffer.CopyFrom(temp._priceBuffer);
        _smoothBuffer.CopyFrom(temp._smoothBuffer);
        _detrender.CopyFrom(temp._detrender);
        _I1_buffer.CopyFrom(temp._I1_buffer);
        _Q1_buffer.CopyFrom(temp._Q1_buffer);

        Last = temp.Last;
        Fama = temp.Fama;

        return new TSeries(t, v);
    }

    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, double fastLimit = 0.5, double slowLimit = 0.05)
    {
        var mama = new Mama(fastLimit, slowLimit);
        for (int i = 0; i < source.Length; i++)
        {
            output[i] = mama.Update(new TValue(DateTime.MinValue, source[i])).Value;
        }
    }

    public string Name { get; set; }
}
