using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// MAMA: MESA Adaptive Moving Average
/// </summary>
/// <remarks>
/// Ehlers' dual-output adaptive filter using Hilbert Transform for cycle measurement.
/// MAMA tracks price closely; FAMA provides smoother confirmation signal.
///
/// Key features: homodyne discriminator, adaptive alpha from phase rate-of-change.
/// </remarks>
/// <seealso href="Mama.md">Detailed documentation</seealso>
/// <seealso href="mama.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Mama : AbstractBase
{
    public TValue Fama { get; private set; }
    public override bool IsHot => _state.Index > 50;

    private readonly double _fastLimit;
    private readonly double _slowLimit;
    private readonly double _scaledFastLimit;
    private readonly TValuePublishedHandler _handler;

    [StructLayout(LayoutKind.Auto)]
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

    // High-precision constants
    private const double C1 = 5.0 / 52.0;   // ~0.09615385
    private const double C2 = 15.0 / 26.0;  // ~0.57692308

    // Hilbert Transform Correction Factors
    // These empirical constants (0.075 and 0.54) are derived by John Ehlers to tune
    // the Hilbert Transform for the expected range of market cycles.
    // CorrectionFactor = 0.075 * Period + 0.54
    private const double AdjSlope = 3.0 / 40.0; // 0.075
    private const double AdjIntercept = 27.0 / 50.0; // 0.54

    private const double TwoPi = 2.0 * Math.PI;
    private const double MinDeltaRadians = Math.PI / 180.0; // 1 degree in radians
    private const double SmoothCoef = 0.2;
    private const double SmoothPrev = 0.8;
    private const double FamaAlphaFactor = 0.5;
    private const double MinPeriod = 6.0;
    private const double MaxPeriod = 50.0;

    public Mama(double fastLimit = 0.5, double slowLimit = 0.05)
    {
        if (fastLimit <= slowLimit || fastLimit <= 0 || slowLimit <= 0)
        {
            throw new ArgumentException("FastLimit must be > SlowLimit and > 0", nameof(fastLimit));
        }
        _fastLimit = fastLimit;
        _slowLimit = slowLimit;
        _scaledFastLimit = fastLimit * MinDeltaRadians;

        _priceBuffer = new RingBuffer(7);
        _smoothBuffer = new RingBuffer(7);
        _detrender = new RingBuffer(7);
        _I1_buffer = new RingBuffer(7);
        _Q1_buffer = new RingBuffer(7);

        Name = $"Mama({fastLimit:F2},{slowLimit:F2})";
        WarmupPeriod = 50;
        _handler = Handle;
        Init();
    }

    public Mama(ITValuePublisher source, double fastLimit = 0.5, double slowLimit = 0.05) : this(fastLimit, slowLimit)
    {
        source.Pub += _handler;
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    private void Init()
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
    private static double NormalizeAngle(double angle)
    {
        // Guard against non-finite inputs to prevent infinite loop
        if (!double.IsFinite(angle))
        {
            return 0.0; // Return neutral angle for invalid inputs
        }

        while (angle <= -Math.PI)
        {
            angle += TwoPi;
        }

        while (angle > Math.PI)
        {
            angle -= TwoPi;
        }

        return angle;
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
            double adj = (AdjSlope * _state.Period) + AdjIntercept;

            // Smooth
            double smooth = Math.FusedMultiplyAdd(4.0, _priceBuffer[^1], Math.FusedMultiplyAdd(3.0, _priceBuffer[^2], Math.FusedMultiplyAdd(2.0, _priceBuffer[^3], _priceBuffer[^4]))) * 0.1;
            _smoothBuffer.Add(smooth, isNew);

            // Detrender
            double dt = Math.FusedMultiplyAdd(C1, _smoothBuffer[^1], Math.FusedMultiplyAdd(C2, _smoothBuffer[^3], Math.FusedMultiplyAdd(-C2, _smoothBuffer[^5], -C1 * _smoothBuffer[^7]))) * adj;
            _detrender.Add(dt, isNew);

            // Q1
            double q1 = Math.FusedMultiplyAdd(C1, dt, Math.FusedMultiplyAdd(C2, _detrender[^3], Math.FusedMultiplyAdd(-C2, _detrender[^5], -C1 * _detrender[^7]))) * adj;
            _Q1_buffer.Add(q1, isNew);

            // I1 = dt[3]
            double i1 = _detrender[^4];
            _I1_buffer.Add(i1, isNew);

            // Advance phases
            // jI = CalculateHilbertTransform(_i1, adj)
            double jI = Math.FusedMultiplyAdd(C1, i1, Math.FusedMultiplyAdd(C2, _I1_buffer[^3], Math.FusedMultiplyAdd(-C2, _I1_buffer[^5], -C1 * _I1_buffer[^7]))) * adj;
            // jQ = CalculateHilbertTransform(_q1, adj)
            double jQ = Math.FusedMultiplyAdd(C1, q1, Math.FusedMultiplyAdd(C2, _Q1_buffer[^3], Math.FusedMultiplyAdd(-C2, _Q1_buffer[^5], -C1 * _Q1_buffer[^7]))) * adj;

            // Phasor addition
            double i2_val = i1 - jQ;
            double q2_val = q1 + jI;

            // Smooth i2, q2 (using FMA for precision)
            _state.I2 = Math.FusedMultiplyAdd(SmoothCoef, i2_val, SmoothPrev * _p_state.I2);
            _state.Q2 = Math.FusedMultiplyAdd(SmoothCoef, q2_val, SmoothPrev * _p_state.Q2);

            // Homodyne discriminator
            double re_val = Math.FusedMultiplyAdd(_state.I2, _p_state.I2, _state.Q2 * _p_state.Q2);
            double im_val = Math.FusedMultiplyAdd(_state.I2, _p_state.Q2, -_state.Q2 * _p_state.I2);

            // Smooth re, im (using FMA)
            _state.Re = Math.FusedMultiplyAdd(SmoothCoef, re_val, SmoothPrev * _p_state.Re);
            _state.Im = Math.FusedMultiplyAdd(SmoothCoef, im_val, SmoothPrev * _p_state.Im);

            // Calculate Period
            double angle = Math.Atan2(_state.Im, _state.Re);
            double period = Math.Abs(angle) > MinDeltaRadians
                ? TwoPi / Math.Abs(angle)
                : _p_state.Period;

            // Adjust Period
            double periodCap = _p_state.Period * 1.5;
            double periodFloor = _p_state.Period * 0.67;

            if (period > periodCap)
            {
                period = periodCap;
            }

            if (period < periodFloor)
            {
                period = periodFloor;
            }

            if (period < MinPeriod)
            {
                period = MinPeriod;
            }

            if (period > MaxPeriod)
            {
                period = MaxPeriod;
            }

            // Smooth Period (using FMA)
            _state.Period = Math.FusedMultiplyAdd(SmoothCoef, period, SmoothPrev * _p_state.Period);

            // Phase calculation
            _state.Phase = Math.Atan2(q1, i1);

            // Adaptive alpha
            double diff = NormalizeAngle(_p_state.Phase - _state.Phase);
            double delta = Math.Max(Math.Abs(diff), MinDeltaRadians);
            double alpha = _scaledFastLimit / delta;
            alpha = Math.Clamp(alpha, _slowLimit, _fastLimit);

            // Final indicators (using FMA for precision)
            double decay = 1.0 - alpha;
            _state.Mama = Math.FusedMultiplyAdd(_p_state.Mama, decay, alpha * _priceBuffer[^1]);
            double famaAlpha = FamaAlphaFactor * alpha;
            double famaDecay = 1.0 - famaAlpha;
            _state.Fama = Math.FusedMultiplyAdd(_p_state.Fama, famaDecay, famaAlpha * _state.Mama);
        }
        else
        {
            // Initialization phase
            _state.SumPr += price;
            double avg = _state.Index > 0 ? _state.SumPr / _state.Index : price;
            _state.Mama = avg;
            _state.Fama = avg;
            _state.Period = MinPeriod;

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
        PubEvent(Last, isNew);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return new TSeries([], []);
        }

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

    /// <summary>
    /// Primes the indicator with historical data.
    /// </summary>
    /// <param name="source">Historical price data</param>
    /// <param name="step">Time step parameter (unused for this indicator but required by base signature)</param>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        _ = step; // Parameter required by base signature but not used by MAMA
        foreach (var value in source)
        {
            Step(value, isNew: true);
        }
    }

    public static TSeries Batch(TSeries source, double fastLimit = 0.5, double slowLimit = 0.05)
    {
        var mama = new Mama(fastLimit, slowLimit);
        return mama.Update(source);
    }

    public static void Batch(ReadOnlySpan<double> source, Span<double> output, double fastLimit = 0.5, double slowLimit = 0.05, Span<double> famaOutput = default)
    {
        if (fastLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fastLimit), "FastLimit must be > 0");
        }
        if (slowLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(slowLimit), "SlowLimit must be > 0");
        }
        if (fastLimit > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(fastLimit), "FastLimit must be <= 1");
        }
        if (slowLimit > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(slowLimit), "SlowLimit must be <= 1");
        }
        if (fastLimit <= slowLimit)
        {
            throw new ArgumentOutOfRangeException(nameof(fastLimit), "FastLimit must be > SlowLimit");
        }

        if (source.Length == 0)
        {
            return;
        }

        if (output.Length < source.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(output), "Output buffer must be at least as large as the input buffer.");
        }
        if (!famaOutput.IsEmpty && famaOutput.Length < source.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(famaOutput), "FAMA output buffer must be at least as large as the input buffer.");
        }

        // Stack allocate buffers for high performance (size 8 for power of 2 masking)
        // We need 7 elements, but 8 allows & 7 masking
        Span<double> priceBuffer = stackalloc double[8];
        Span<double> smoothBuffer = stackalloc double[8];
        Span<double> detrender = stackalloc double[8];
        Span<double> I1_buffer = stackalloc double[8];
        Span<double> Q1_buffer = stackalloc double[8];

        int bufferIdx = 0; // Current index for circular buffer
        int count = 0;

        // State variables (initialized: used before assignment; uninitialized: always assigned before read)
        double period = MinPeriod, sumPr = 0, lastValidPrice = 0;
        double mama = 0, fama = 0, i2 = 0, q2 = 0, re = 0, im = 0;
        double p_period = MinPeriod, p_phase = 0, p_mama = 0, p_fama = 0;
        double p_i2 = 0, p_q2 = 0, p_re = 0, p_im = 0;

        // Constants
        const int Mask = 7;
        // Pre-scale fastLimit by MinDeltaRadians so alpha calculation
        // produces same numerical results as degree-based formula:
        // alpha_rad = (fastLimit × π/180) / delta_rad ≡ alpha_deg = fastLimit / delta_deg
        double scaledFastLimit = fastLimit * MinDeltaRadians;

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
                double adj = (AdjSlope * period) + AdjIntercept;

                // Smooth
                double smooth = (4.0 * priceBuffer[bufferIdx] +
                                 3.0 * priceBuffer[(bufferIdx - 1) & Mask] +
                                 2.0 * priceBuffer[(bufferIdx - 2) & Mask] +
                                 priceBuffer[(bufferIdx - 3) & Mask]) * 0.1;

                smoothBuffer[bufferIdx] = smooth;

                // Detrender
                double dt = (C1 * smoothBuffer[bufferIdx] +
                             C2 * smoothBuffer[(bufferIdx - 2) & Mask] -
                             C2 * smoothBuffer[(bufferIdx - 4) & Mask] -
                             C1 * smoothBuffer[(bufferIdx - 6) & Mask]) * adj;

                detrender[bufferIdx] = dt;

                // Q1
                double q1 = (C1 * dt +
                             C2 * detrender[(bufferIdx - 2) & Mask] -
                             C2 * detrender[(bufferIdx - 4) & Mask] -
                             C1 * detrender[(bufferIdx - 6) & Mask]) * adj;

                Q1_buffer[bufferIdx] = q1;

                // I1 = dt[3]
                double i1 = detrender[(bufferIdx - 3) & Mask];
                I1_buffer[bufferIdx] = i1;

                // Advance phases
                double jI = (C1 * i1 +
                             C2 * I1_buffer[(bufferIdx - 2) & Mask] -
                             C2 * I1_buffer[(bufferIdx - 4) & Mask] -
                             C1 * I1_buffer[(bufferIdx - 6) & Mask]) * adj;

                double jQ = (C1 * q1 +
                             C2 * Q1_buffer[(bufferIdx - 2) & Mask] -
                             C2 * Q1_buffer[(bufferIdx - 4) & Mask] -
                             C1 * Q1_buffer[(bufferIdx - 6) & Mask]) * adj;

                // Phasor addition
                double i2_val = i1 - jQ;
                double q2_val = q1 + jI;

                // Smooth i2, q2 (using FMA for precision)
                i2 = Math.FusedMultiplyAdd(SmoothCoef, i2_val, SmoothPrev * p_i2);
                q2 = Math.FusedMultiplyAdd(SmoothCoef, q2_val, SmoothPrev * p_q2);

                // Homodyne discriminator
                double re_val = Math.FusedMultiplyAdd(i2, p_i2, q2 * p_q2);
                double im_val = Math.FusedMultiplyAdd(i2, p_q2, -q2 * p_i2);

                // Smooth re, im (using FMA)
                re = Math.FusedMultiplyAdd(SmoothCoef, re_val, SmoothPrev * p_re);
                im = Math.FusedMultiplyAdd(SmoothCoef, im_val, SmoothPrev * p_im);

                // Calculate Period
                double angle = Math.Atan2(im, re);
                double newPeriod = Math.Abs(angle) > MinDeltaRadians
                    ? TwoPi / Math.Abs(angle)
                    : p_period;

                // Adjust Period
                double periodCap = p_period * 1.5;
                double periodFloor = p_period * 0.67;

                if (newPeriod > periodCap)
                {
                    newPeriod = periodCap;
                }

                if (newPeriod < periodFloor)
                {
                    newPeriod = periodFloor;
                }

                if (newPeriod < MinPeriod)
                {
                    newPeriod = MinPeriod;
                }

                if (newPeriod > MaxPeriod)
                {
                    newPeriod = MaxPeriod;
                }

                // Smooth Period (using FMA)
                period = Math.FusedMultiplyAdd(SmoothCoef, newPeriod, SmoothPrev * p_period);

                // Phase calculation
                double phase = Math.Atan2(q1, i1);

                // Adaptive alpha
                double diff = NormalizeAngle(p_phase - phase);
                double delta = Math.Max(Math.Abs(diff), MinDeltaRadians);
                double alpha = scaledFastLimit / delta;
                alpha = Math.Clamp(alpha, slowLimit, fastLimit);

                // Final indicators (using FMA for precision)
                double decay = 1.0 - alpha;
                mama = Math.FusedMultiplyAdd(p_mama, decay, alpha * priceBuffer[bufferIdx]);
                double famaAlpha = FamaAlphaFactor * alpha;
                double famaDecay = 1.0 - famaAlpha;
                fama = Math.FusedMultiplyAdd(p_fama, famaDecay, famaAlpha * mama);

                // Update previous state
                p_i2 = i2;
                p_q2 = q2;
                p_re = re;
                p_im = im;
                p_period = period;
                p_phase = phase;
                p_mama = mama;
                p_fama = fama;
            }
            else
            {
                // Initialization
                sumPr += price;
                double avg = count > 0 ? sumPr / count : price;
                mama = avg;
                fama = avg;

                // Init simple state
                smoothBuffer[bufferIdx] = 0;
                detrender[bufferIdx] = 0;
                I1_buffer[bufferIdx] = 0;
                Q1_buffer[bufferIdx] = 0;

                // Set initial p_state
                p_mama = avg;
                p_fama = avg;
                p_period = MinPeriod;
                p_phase = 0;
            }

            output[i] = mama;
            if (!famaOutput.IsEmpty)
            {
                famaOutput[i] = fama;
            }
        }
    }

    public static (TSeries Results, Mama Indicator) Calculate(TSeries source, double fastLimit = 0.5, double slowLimit = 0.05)
    {
        var indicator = new Mama(fastLimit, slowLimit);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}