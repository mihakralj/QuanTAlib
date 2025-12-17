using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// MESA Adaptive Moving Average (MAMA)
/// A trend-following indicator that adapts to the market's phase rate of change.
/// </summary>
[SkipLocalsInit]
public sealed class Mama : AbstractBase
{
    public TValue Fama { get; private set; }
    public override bool IsHot => _state.Index > 6;

    private readonly double _fastLimit;
    private readonly double _slowLimit;

    private record struct State(
        double Period, double Phase, double Mama, double Fama, double SumPr,
        double I2, double Q2, double Re, double Im, double LastValidPrice, int Index
    );
    private State _state;
    private State _p_state;

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
        WarmupPeriod = 7;
        Init();
    }

    public Mama(ITValuePublisher source, double fastLimit = 0.5, double slowLimit = 0.05) : this(fastLimit, slowLimit)
    {
        source.Pub += (item) => Update(item);
    }

    public void Init()
    {
        Reset();
    }

    public override void Reset()
    {
        _state = default;
        _state.Mama = double.NaN;
        _state.Fama = double.NaN;
        _p_state = _state;

        _priceBuffer.Clear();
        _smoothBuffer.Clear();
        _detrender.Clear();
        _I1_buffer.Clear();
        _Q1_buffer.Clear();

        Last = new TValue(DateTime.MinValue, double.NaN);
        Fama = new TValue(DateTime.MinValue, double.NaN);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double Step(double price, bool isNew)
    {
        if (isNew)
        {
            _p_state = _state;
            _state.Index++;
        }
        else
        {
            _state = _p_state;
        }

        if (!double.IsFinite(price))
        {
            price = _state.LastValidPrice;
        }
        else
        {
            _state.LastValidPrice = price;
        }

        _priceBuffer.Add(price, isNew);

        if (_state.Index > 6)
        {
            double adj = (0.075 * _state.Period) + 0.54;

            // Smooth
            double smooth = (4.0 * _priceBuffer[^1] + 3.0 * _priceBuffer[^2] + 2.0 * _priceBuffer[^3] + _priceBuffer[^4]) * 0.1;
            _smoothBuffer.Add(smooth, isNew);

            // Detrender
            double dt = (c1 * _smoothBuffer[^1] + c2 * _smoothBuffer[^3] - c2 * _smoothBuffer[^5] - c1 * _smoothBuffer[^7]) * adj;
            _detrender.Add(dt, isNew);

            // Q1
            double q1 = (c1 * dt + c2 * _detrender[^3] - c2 * _detrender[^5] - c1 * _detrender[^7]) * adj;
            _Q1_buffer.Add(q1, isNew);

            // I1 = dt[3]
            double i1 = _detrender[^4];
            _I1_buffer.Add(i1, isNew);

            // Advance phases
            // jI = CalculateHilbertTransform(_i1, adj)
            double jI = (c1 * i1 + c2 * _I1_buffer[^3] - c2 * _I1_buffer[^5] - c1 * _I1_buffer[^7]) * adj;
            // jQ = CalculateHilbertTransform(_q1, adj)
            double jQ = (c1 * q1 + c2 * _Q1_buffer[^3] - c2 * _Q1_buffer[^5] - c1 * _Q1_buffer[^7]) * adj;

            // Phasor addition
            double i2_val = i1 - jQ;
            double q2_val = q1 + jI;

            // Smooth i2, q2
            _state.I2 = 0.2 * i2_val + 0.8 * _p_state.I2;
            _state.Q2 = 0.2 * q2_val + 0.8 * _p_state.Q2;

            // Homodyne discriminator
            double re_val = (_state.I2 * _p_state.I2) + (_state.Q2 * _p_state.Q2);
            double im_val = (_state.I2 * _p_state.Q2) - (_state.Q2 * _p_state.I2);

            // Smooth re, im
            _state.Re = 0.2 * re_val + 0.8 * _p_state.Re;
            _state.Im = 0.2 * im_val + 0.8 * _p_state.Im;

            // Calculate Period
            double period = (Math.Abs(_state.Im) > double.Epsilon && Math.Abs(_state.Re) > double.Epsilon)
                ? TWOPI / Math.Atan(_state.Im / _state.Re)
                : 0.0;

            // Adjust Period
            double periodCap = _p_state.Period * 1.5;
            double periodFloor = _p_state.Period * 0.67;

            if (period > periodCap) period = periodCap;
            if (period < periodFloor) period = periodFloor;

            if (period < 6.0) period = 6.0;
            if (period > 50.0) period = 50.0;

            // Smooth Period
            _state.Period = 0.2 * period + 0.8 * _p_state.Period;

            // Phase calculation
            _state.Phase = Math.Abs(i1) >= double.Epsilon ? Math.Atan(q1 / i1) * RadToDeg : 0.0;

            // Adaptive alpha
            double delta = Math.Max(_p_state.Phase - _state.Phase, 1.0);
            double alpha = _fastLimit / delta;
            alpha = Math.Clamp(alpha, _slowLimit, _fastLimit);

            // Final indicators
            _state.Mama = alpha * _priceBuffer[^1] + (1.0 - alpha) * _p_state.Mama;
            _state.Fama = 0.5 * alpha * _state.Mama + (1.0 - 0.5 * alpha) * _p_state.Fama;
        }
        else
        {
            // Initialization phase
            _state.SumPr += price;
            double avg = _state.Index > 0 ? _state.SumPr / _state.Index : price;
            _state.Mama = avg;
            _state.Fama = avg;

            // Initialize buffers with 0
            _smoothBuffer.Add(0, isNew);
            _detrender.Add(0, isNew);
            _I1_buffer.Add(0, isNew);
            _Q1_buffer.Add(0, isNew);
        }

        return _state.Mama;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        double mama = Step(input.Value, isNew);
        Last = new TValue(input.Time, mama);
        Fama = new TValue(input.Time, _state.Fama);
        PubEvent(Last);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0) return new TSeries([], []);

        int len = source.Count;
        var v = new List<double>(len);
        var t = new List<long>(len);

        for (int i = 0; i < len; i++)
        {
            var result = Update(new TValue(source.Times[i], source.Values[i]));
            t.Add(result.Time);
            v.Add(result.Value);
        }

        return new TSeries(t, v);
    }

    public override void Prime(ReadOnlySpan<double> source)
    {
        foreach (var value in source)
        {
            Step(value, true);
        }
    }

    public static TSeries Batch(TSeries source, double fastLimit = 0.5, double slowLimit = 0.05)
    {
        var mama = new Mama(fastLimit, slowLimit);
        return mama.Update(source);
    }

    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, double fastLimit = 0.5, double slowLimit = 0.05)
    {
        if (source.Length == 0) return;

        // Stack allocate buffers for high performance (size 8 for power of 2 masking)
        // We need 7 elements, but 8 allows & 7 masking
        Span<double> priceBuffer = stackalloc double[8];
        Span<double> smoothBuffer = stackalloc double[8];
        Span<double> detrender = stackalloc double[8];
        Span<double> I1_buffer = stackalloc double[8];
        Span<double> Q1_buffer = stackalloc double[8];

        int bufferIdx = 0; // Current index for circular buffer
        int count = 0;

        // State variables
        double period = 0, mama = 0, sumPr = 0;
        double i2 = 0, q2 = 0, re = 0, im = 0, lastValidPrice = 0;
        double p_period = 0, p_phase = 0, p_mama = 0;
        double p_i2 = 0, p_q2 = 0, p_re = 0, p_im = 0;

        // Constants
        const int Mask = 7;

        for (int i = 0; i < source.Length; i++)
        {
            double price = source[i];
            if (!double.IsFinite(price))
            {
                price = count > 0 ? lastValidPrice : 0.0;
            }
            else
            {
                lastValidPrice = price;
            }

            // Circular buffer update
            bufferIdx = (bufferIdx + 1) & Mask;
            priceBuffer[bufferIdx] = price;
            count++;

            if (count > 6)
            {
                double adj = (0.075 * period) + 0.54;

                // Smooth
                double smooth = (4.0 * priceBuffer[bufferIdx] +
                                 3.0 * priceBuffer[(bufferIdx - 1) & Mask] +
                                 2.0 * priceBuffer[(bufferIdx - 2) & Mask] +
                                 priceBuffer[(bufferIdx - 3) & Mask]) * 0.1;

                smoothBuffer[bufferIdx] = smooth;

                // Detrender
                double dt = (c1 * smoothBuffer[bufferIdx] +
                             c2 * smoothBuffer[(bufferIdx - 2) & Mask] -
                             c2 * smoothBuffer[(bufferIdx - 4) & Mask] -
                             c1 * smoothBuffer[(bufferIdx - 6) & Mask]) * adj;

                detrender[bufferIdx] = dt;

                // Q1
                double q1 = (c1 * dt +
                             c2 * detrender[(bufferIdx - 2) & Mask] -
                             c2 * detrender[(bufferIdx - 4) & Mask] -
                             c1 * detrender[(bufferIdx - 6) & Mask]) * adj;

                Q1_buffer[bufferIdx] = q1;

                // I1 = dt[3]
                double i1 = detrender[(bufferIdx - 3) & Mask];
                I1_buffer[bufferIdx] = i1;

                // Advance phases
                double jI = (c1 * i1 +
                             c2 * I1_buffer[(bufferIdx - 2) & Mask] -
                             c2 * I1_buffer[(bufferIdx - 4) & Mask] -
                             c1 * I1_buffer[(bufferIdx - 6) & Mask]) * adj;

                double jQ = (c1 * q1 +
                             c2 * Q1_buffer[(bufferIdx - 2) & Mask] -
                             c2 * Q1_buffer[(bufferIdx - 4) & Mask] -
                             c1 * Q1_buffer[(bufferIdx - 6) & Mask]) * adj;

                // Phasor addition
                double i2_val = i1 - jQ;
                double q2_val = q1 + jI;

                // Smooth i2, q2
                i2 = 0.2 * i2_val + 0.8 * p_i2;
                q2 = 0.2 * q2_val + 0.8 * p_q2;

                // Homodyne discriminator
                double re_val = (i2 * p_i2) + (q2 * p_q2);
                double im_val = (i2 * p_q2) - (q2 * p_i2);

                // Smooth re, im
                re = 0.2 * re_val + 0.8 * p_re;
                im = 0.2 * im_val + 0.8 * p_im;

                // Calculate Period
                double newPeriod = (Math.Abs(im) > double.Epsilon && Math.Abs(re) > double.Epsilon)
                    ? TWOPI / Math.Atan(im / re)
                    : 0.0;

                // Adjust Period
                double periodCap = p_period * 1.5;
                double periodFloor = p_period * 0.67;

                if (newPeriod > periodCap) newPeriod = periodCap;
                if (newPeriod < periodFloor) newPeriod = periodFloor;

                if (newPeriod < 6.0) newPeriod = 6.0;
                if (newPeriod > 50.0) newPeriod = 50.0;

                // Smooth Period
                period = 0.2 * newPeriod + 0.8 * p_period;

                // Phase calculation
                double phase = Math.Abs(i1) >= double.Epsilon ? Math.Atan(q1 / i1) * RadToDeg : 0.0;

                // Adaptive alpha
                double delta = Math.Max(p_phase - phase, 1.0);
                double alpha = fastLimit / delta;
                alpha = Math.Clamp(alpha, slowLimit, fastLimit);

                // Final indicators
                mama = alpha * priceBuffer[bufferIdx] + (1.0 - alpha) * p_mama;

                // Update previous state
                p_i2 = i2;
                p_q2 = q2;
                p_re = re;
                p_im = im;
                p_period = period;
                p_phase = phase;
                p_mama = mama;
            }
            else
            {
                // Initialization
                sumPr += price;
                double avg = count > 0 ? sumPr / count : price;
                mama = avg;

                // Init simple state
                smoothBuffer[bufferIdx] = 0;
                detrender[bufferIdx] = 0;
                I1_buffer[bufferIdx] = 0;
                Q1_buffer[bufferIdx] = 0;

                // Set initial p_state
                p_mama = avg;
                p_period = 0; // Initial period state
                p_phase = 0;

                // Initialize other state variables if needed for next iteration logic?
                // Actually they just stay 0/default until we hit count > 6
            }

            output[i] = mama;
        }
    }
}
