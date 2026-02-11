using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// HTIT: Hilbert Transform Instantaneous Trendline
/// </summary>
/// <remarks>
/// Ehlers' adaptive trendline using Hilbert Transform cycle measurement.
/// Averages price over the measured dominant cycle period for cycle-adaptive smoothing.
///
/// Key features: homodyne discriminator, period-adaptive averaging window.
/// </remarks>
/// <seealso href="Htit.md">Detailed documentation</seealso>
/// <seealso href="htit.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Htit : AbstractBase
{
    public override bool IsHot => _state.Index >= WarmupPeriod;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double I2, double Q2, double Re, double Im,
        double Period, double SmoothPeriod,
        double LastValidPrice, int Index
    )
    {
        // Initialize LastValidPrice to NaN to detect first valid price
        public State() : this(0, 0, 0, 0, 0, 0, double.NaN, 0) { }
    }
    private State _state;
    private State _p_state;

    private readonly RingBuffer _priceBuffer;
    private readonly RingBuffer _smoothBuffer;
    private readonly RingBuffer _detrenderBuffer;
    private readonly RingBuffer _i1Buffer;
    private readonly RingBuffer _q1Buffer;
    private readonly RingBuffer _itBuffer;
    private readonly TValuePublishedHandler _handler;

    // High-precision constants
    private const double c1 = 5.0 / 52.0;   // ~0.09615385
    private const double c2 = 15.0 / 26.0;  // ~0.57692308
    private const double adjSlope = 3.0 / 40.0; // 0.075
    private const double adjIntercept = 27.0 / 50.0; // 0.54
    private const double TwoPi = 2.0 * Math.PI;
    private const double MinDeltaRadians = Math.PI / 180.0; // 1 degree in radians

    public Htit()
    {
        Name = "Htit";
        WarmupPeriod = 12;
        _handler = Handle;

        // Initialize buffers with size 8 (power of 2) for consistency with Calculate optimization
        // except priceBuffer which needs to be larger for IT calculation
        _priceBuffer = new RingBuffer(64); // Needs to hold enough history for IT calculation (up to 50 bars)
        _smoothBuffer = new RingBuffer(8);
        _detrenderBuffer = new RingBuffer(8);
        _i1Buffer = new RingBuffer(8);
        _q1Buffer = new RingBuffer(8);
        _itBuffer = new RingBuffer(8);

        Init();
    }

    public Htit(ITValuePublisher source) : this()
    {
        source.Pub += _handler;
    }

    private void Init()
    {
        Reset();
    }

    public override void Reset()
    {
        _state = new State();
        _p_state = new State();

        _priceBuffer.Clear();
        _smoothBuffer.Clear();
        _detrenderBuffer.Clear();
        _i1Buffer.Clear();
        _q1Buffer.Clear();
        _itBuffer.Clear();

        Last = new TValue(DateTime.MinValue, double.NaN);
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

        // Handle non-finite input: skip processing if no valid price seen yet
        if (!double.IsFinite(price))
        {
            // If we haven't seen a valid price yet, return NaN (early exit)
            if (double.IsNaN(_state.LastValidPrice))
            {
                return double.NaN;
            }
            // Otherwise, use the last valid price
            price = _state.LastValidPrice;
        }
        else
        {
            _state.LastValidPrice = price;
        }

        _priceBuffer.Add(price, isNew);

        // Need enough data for smooth calculation (4 bars) + detrender (7 bars total lag)
        if (_state.Index < 7)
        {
            // During warmup, propagate NaN if input is NaN
            _smoothBuffer.Add(price, isNew);
            _detrenderBuffer.Add(0, isNew);
            _i1Buffer.Add(0, isNew);
            _q1Buffer.Add(0, isNew);
            _itBuffer.Add(price, isNew);
            return price; // May be NaN if no valid input yet
        }

        // 1. Smooth Price using FMA for precision
        // smooth = (4*Price + 3*Price[1] + 2*Price[2] + Price[3]) / 10
        double smooth = Math.FusedMultiplyAdd(4.0, _priceBuffer[^1],
            Math.FusedMultiplyAdd(3.0, _priceBuffer[^2],
                Math.FusedMultiplyAdd(2.0, _priceBuffer[^3], _priceBuffer[^4]))) * 0.1;
        _smoothBuffer.Add(smooth, isNew);

        // 2. Detrender
        // In streaming, we use previous period from state
        double prevPeriod = _p_state.Period;
        double adj = (adjSlope * prevPeriod) + adjIntercept;

        // Use FMA for detrender calculation
        double detrender = Math.FusedMultiplyAdd(c1, _smoothBuffer[^1],
            Math.FusedMultiplyAdd(c2, _smoothBuffer[^3],
                Math.FusedMultiplyAdd(-c2, _smoothBuffer[^5], -c1 * _smoothBuffer[^7]))) * adj;
        _detrenderBuffer.Add(detrender, isNew);

        // 3. In-Phase and Quadrature using FMA
        double q1 = Math.FusedMultiplyAdd(c1, _detrenderBuffer[^1],
            Math.FusedMultiplyAdd(c2, _detrenderBuffer[^3],
                Math.FusedMultiplyAdd(-c2, _detrenderBuffer[^5], -c1 * _detrenderBuffer[^7]))) * adj;
        double i1 = _detrenderBuffer[^4];

        _q1Buffer.Add(q1, isNew);
        _i1Buffer.Add(i1, isNew);

        // 4. Advance phases by 90 degrees using FMA
        double jI = Math.FusedMultiplyAdd(c1, _i1Buffer[^1],
            Math.FusedMultiplyAdd(c2, _i1Buffer[^3],
                Math.FusedMultiplyAdd(-c2, _i1Buffer[^5], -c1 * _i1Buffer[^7]))) * adj;
        double jQ = Math.FusedMultiplyAdd(c1, _q1Buffer[^1],
            Math.FusedMultiplyAdd(c2, _q1Buffer[^3],
                Math.FusedMultiplyAdd(-c2, _q1Buffer[^5], -c1 * _q1Buffer[^7]))) * adj;

        // 5. Phasor addition
        double i2_val = i1 - jQ;
        double q2_val = q1 + jI;

        // Smooth i2, q2 (using FMA for precision)
        _state.I2 = Math.FusedMultiplyAdd(0.2, i2_val, 0.8 * _p_state.I2);
        _state.Q2 = Math.FusedMultiplyAdd(0.2, q2_val, 0.8 * _p_state.Q2);

        // 6. Homodyne Discriminator
        double re_val = Math.FusedMultiplyAdd(_state.I2, _p_state.I2, _state.Q2 * _p_state.Q2);
        double im_val = Math.FusedMultiplyAdd(_state.I2, _p_state.Q2, -_state.Q2 * _p_state.I2);

        // Smooth re, im (using FMA)
        _state.Re = Math.FusedMultiplyAdd(0.2, re_val, 0.8 * _p_state.Re);
        _state.Im = Math.FusedMultiplyAdd(0.2, im_val, 0.8 * _p_state.Im);

        // 7. Calculate Period
        double angle = Math.Atan2(_state.Im, _state.Re);
        double period = Math.Abs(angle) > MinDeltaRadians
            ? TwoPi / Math.Abs(angle)
            : _p_state.Period;

        // Adjust period to thresholds
        if (prevPeriod > 0)
        {
            double cap = 1.5 * prevPeriod;
            double floor = 0.67 * prevPeriod;
            if (period > cap)
            {
                period = cap;
            }

            if (period < floor)
            {
                period = floor;
            }
        }
        if (period < 6)
        {
            period = 6;
        }

        if (period > 50)
        {
            period = 50;
        }

        // Smooth the period (using FMA)
        _state.Period = Math.FusedMultiplyAdd(0.2, period, 0.8 * prevPeriod);
        _state.SmoothPeriod = Math.FusedMultiplyAdd(0.33, _state.Period, 0.67 * _p_state.SmoothPeriod);

        // 8. Instantaneous Trend
        int dcPeriods = (int)(double.IsNaN(_state.SmoothPeriod) ? 0 : _state.SmoothPeriod + 0.5);
        double sumPr = 0;
        int count = 0;

        // Sum price over dcPeriods
        for (int d = 0; d < dcPeriods; d++)
        {
            // Check if we have enough history
            if (d < _priceBuffer.Count)
            {
                sumPr += _priceBuffer[^(d + 1)];
                count++;
            }
        }

        double it = count > 0 ? sumPr / count : price;
        _itBuffer.Add(it, isNew);

        // 9. Final Trendline
        // Need at least 12 bars total (Index > 11) to have valid IT history for smoothing
        if (_state.Index >= 12)
        {
            // NaN will propagate if IT buffer contains NaN
            return (4.0 * _itBuffer[^1] + 3.0 * _itBuffer[^2] + 2.0 * _itBuffer[^3] + _itBuffer[^4]) * 0.1;
        }

        return price; // May be NaN if no valid input yet
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        double val = Step(input.Value, isNew);
        Last = new TValue(input.Time, val);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>
    /// Updates the indicator with a TSeries (batch mode).
    /// This method processes each value through the streaming Update method,
    /// maintaining full state for subsequent streaming updates.
    /// For high-performance batch-only processing, use the static Calculate method instead.
    /// </summary>
    /// <param name="source">Input time series</param>
    /// <returns>Output time series with HTIT values</returns>
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

    private void Handle(object? sender, in TValueEventArgs args)
    {
        Update(args.Value, args.IsNew);
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (var value in source)
        {
            Step(value, isNew: true);
        }
    }

    public static TSeries Batch(TSeries source)
    {
        var htit = new Htit();
        return htit.Update(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (source.Length == 0)
        {
            return;
        }

        // Stack allocate buffers
        // priceBuffer needs to be larger for IT calculation (up to 50 bars)
        // Using 64 (power of 2) for efficient masking
        Span<double> priceBuffer = stackalloc double[64];
        Span<double> smoothBuffer = stackalloc double[8];
        Span<double> detrenderBuffer = stackalloc double[8];
        Span<double> i1Buffer = stackalloc double[8];
        Span<double> q1Buffer = stackalloc double[8];
        Span<double> itBuffer = stackalloc double[8];

        int pIdx = 0; // Index for priceBuffer (mask 63)
        int sIdx = 0; // Index for other buffers (mask 7)
        int count = 0;

        // State variables
        double i2 = 0, q2 = 0, re = 0, im = 0;
        double period = 0, smoothPeriod = 0;
        // Initialize to NaN to detect first valid price
        double lastValidPrice = double.NaN;

        // Previous state variables
        double p_i2 = 0, p_q2 = 0, p_re = 0, p_im = 0;
        double p_period = 0, p_smoothPeriod = 0;

        const int Mask63 = 63;
        const int Mask7 = 7;

        for (int i = 0; i < source.Length; i++)
        {
            double price = source[i];

            // Handle non-finite input: skip processing if no valid price seen yet
            if (!double.IsFinite(price))
            {
                // If we haven't seen a valid price yet, output NaN
                if (double.IsNaN(lastValidPrice))
                {
                    output[i] = double.NaN;
                    continue;
                }
                // Otherwise, use the last valid price
                price = lastValidPrice;
            }
            else
            {
                lastValidPrice = price;
            }

            // Update circular buffer indices
            pIdx = (pIdx + 1) & Mask63;
            sIdx = (sIdx + 1) & Mask7;
            count++;

            priceBuffer[pIdx] = price;

            if (count > 6)
            {
                // 1. Smooth Price using FMA
                double smooth = Math.FusedMultiplyAdd(4.0, priceBuffer[pIdx],
                    Math.FusedMultiplyAdd(3.0, priceBuffer[(pIdx - 1) & Mask63],
                        Math.FusedMultiplyAdd(2.0, priceBuffer[(pIdx - 2) & Mask63],
                            priceBuffer[(pIdx - 3) & Mask63]))) * 0.1;
                smoothBuffer[sIdx] = smooth;

                // 2. Detrender
                double adj = (adjSlope * p_period) + adjIntercept;

                // Use FMA for detrender
                double detrender = Math.FusedMultiplyAdd(c1, smoothBuffer[sIdx],
                    Math.FusedMultiplyAdd(c2, smoothBuffer[(sIdx - 2) & Mask7],
                        Math.FusedMultiplyAdd(-c2, smoothBuffer[(sIdx - 4) & Mask7],
                            -c1 * smoothBuffer[(sIdx - 6) & Mask7]))) * adj;
                detrenderBuffer[sIdx] = detrender;

                // 3. In-Phase and Quadrature using FMA
                double q1 = Math.FusedMultiplyAdd(c1, detrender,
                    Math.FusedMultiplyAdd(c2, detrenderBuffer[(sIdx - 2) & Mask7],
                        Math.FusedMultiplyAdd(-c2, detrenderBuffer[(sIdx - 4) & Mask7],
                            -c1 * detrenderBuffer[(sIdx - 6) & Mask7]))) * adj;
                q1Buffer[sIdx] = q1;

                double i1 = detrenderBuffer[(sIdx - 3) & Mask7];
                i1Buffer[sIdx] = i1;

                // 4. Advance phases using FMA
                double jI = Math.FusedMultiplyAdd(c1, i1,
                    Math.FusedMultiplyAdd(c2, i1Buffer[(sIdx - 2) & Mask7],
                        Math.FusedMultiplyAdd(-c2, i1Buffer[(sIdx - 4) & Mask7],
                            -c1 * i1Buffer[(sIdx - 6) & Mask7]))) * adj;

                double jQ = Math.FusedMultiplyAdd(c1, q1,
                    Math.FusedMultiplyAdd(c2, q1Buffer[(sIdx - 2) & Mask7],
                        Math.FusedMultiplyAdd(-c2, q1Buffer[(sIdx - 4) & Mask7],
                            -c1 * q1Buffer[(sIdx - 6) & Mask7]))) * adj;

                // 5. Phasor addition
                double i2_val = i1 - jQ;
                double q2_val = q1 + jI;

                i2 = Math.FusedMultiplyAdd(0.2, i2_val, 0.8 * p_i2);
                q2 = Math.FusedMultiplyAdd(0.2, q2_val, 0.8 * p_q2);

                // 6. Homodyne Discriminator
                double re_val = Math.FusedMultiplyAdd(i2, p_i2, q2 * p_q2);
                double im_val = Math.FusedMultiplyAdd(i2, p_q2, -q2 * p_i2);

                re = Math.FusedMultiplyAdd(0.2, re_val, 0.8 * p_re);
                im = Math.FusedMultiplyAdd(0.2, im_val, 0.8 * p_im);

                // 7. Calculate Period
                double angle = Math.Atan2(im, re);
                double newPeriod = Math.Abs(angle) > MinDeltaRadians
                    ? TwoPi / Math.Abs(angle)
                    : p_period;

                if (p_period > 0)
                {
                    double cap = 1.5 * p_period;
                    double floor = 0.67 * p_period;
                    if (newPeriod > cap)
                    {
                        newPeriod = cap;
                    }

                    if (newPeriod < floor)
                    {
                        newPeriod = floor;
                    }
                }
                if (newPeriod < 6)
                {
                    newPeriod = 6;
                }

                if (newPeriod > 50)
                {
                    newPeriod = 50;
                }

                period = Math.FusedMultiplyAdd(0.2, newPeriod, 0.8 * p_period);
                smoothPeriod = Math.FusedMultiplyAdd(0.33, period, 0.67 * p_smoothPeriod);

                // 8. Instantaneous Trend
                double safeSmooth = double.IsNaN(smoothPeriod) ? 0 : smoothPeriod;
                int dcPeriods = (int)(safeSmooth + 0.5);
                double sumPr = 0;
                int prCount = 0;

                for (int d = 0; d < dcPeriods; d++)
                {
                    if (d < count)
                    {
                        sumPr += priceBuffer[(pIdx - d) & Mask63];
                        prCount++;
                    }
                }

                double it = prCount > 0 ? sumPr / prCount : price;
                itBuffer[sIdx] = it;

                // 9. Final Trendline using FMA
                output[i] = count >= 12
                    ? Math.FusedMultiplyAdd(4.0, itBuffer[sIdx],
                        Math.FusedMultiplyAdd(3.0, itBuffer[(sIdx - 1) & Mask7],
                            Math.FusedMultiplyAdd(2.0, itBuffer[(sIdx - 2) & Mask7],
                                itBuffer[(sIdx - 3) & Mask7]))) * 0.1
                    : price;

                // Update previous state
                p_i2 = i2;
                p_q2 = q2;
                p_re = re;
                p_im = im;
                p_period = period;
                p_smoothPeriod = smoothPeriod;
            }
            else
            {
                // Initialization - propagate NaN if no valid price yet
                smoothBuffer[sIdx] = price;
                detrenderBuffer[sIdx] = 0;
                i1Buffer[sIdx] = 0;
                q1Buffer[sIdx] = 0;
                itBuffer[sIdx] = price;
                output[i] = price; // May be NaN if no valid input yet

                // Reset state variables
                p_i2 = 0; p_q2 = 0; p_re = 0; p_im = 0;
                p_period = 0; p_smoothPeriod = 0;
            }
        }
    }

    public static (TSeries Results, Htit Indicator) Calculate(TSeries source)
    {
        var indicator = new Htit();
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}