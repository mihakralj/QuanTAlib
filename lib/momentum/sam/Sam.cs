using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// SAM: Smoothed Adaptive Momentum - Ehlers adaptive momentum oscillator that
/// measures price change over the dominant cycle period, then smooths with a
/// 2-pole Super Smoother filter.
/// </summary>
/// <remarks>
/// Algorithm (Ehlers, "Cybernetic Analysis for Stocks and Futures", 2004, Ch.12):
/// 1. 4-bar FIR smoother: (src + 2*src[1] + 2*src[2] + src[3]) / 6
/// 2. Hilbert Transform via 7-tap FIR (0.0962 / 0.5769 coefficients)
/// 3. Homodyne Discriminator: Re/Im from phasor correlation, period = 2π/atan(Im/Re)
/// 4. Double-smoothed dominant cycle: instPeriod(0.33) → dcPeriod(0.15)
/// 5. Adaptive momentum: src - src[dcPeriod]
/// 6. 2-pole Super Smoother with configurable cutoff
///
/// Properties:
/// - Zero-lag momentum that adapts to dominant cycle length
/// - Oscillates around zero; no fixed bias from fractional-cycle measurement
/// - Super Smoother output removes high-frequency noise without phase distortion
/// </remarks>
/// <seealso href="sam.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Sam : AbstractBase
{
    private readonly double _alpha;
    private readonly double _alphaDecay; // 1 - alpha
    private readonly RingBuffer _priceBuf; // lookback buffer for adaptive momentum

    // Super Smoother coefficients (precomputed from cutoff)
    private readonly double _ssC1;
    private readonly double _ssC2;
    private readonly double _ssC3;

    private const double TwoPi = 2.0 * Math.PI;
    private const double HalfPi = Math.PI / 2.0;
    private const double Sqrt2 = 1.4142135623730951;
    private const int MaxCyclePeriod = 50;
    private const int MinCyclePeriod = 6;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        // Price history for 4-bar FIR smoother
        double Price0, double Price1, double Price2, double Price3,
        // Smooth price history for detrender (7 taps)
        double Sp0, double Sp1, double Sp2, double Sp3, double Sp4, double Sp5, double Sp6,
        // Detrender history for Q1 (7 taps)
        double Det0, double Det1, double Det2, double Det3, double Det4, double Det5, double Det6,
        // I1 history for JI (7 taps)
        double I1_0, double I1_1, double I1_2, double I1_3, double I1_4, double I1_5, double I1_6,
        // Q1 history for JQ (7 taps)
        double Q1_0, double Q1_1, double Q1_2, double Q1_3, double Q1_4, double Q1_5, double Q1_6,
        // I2, Q2 smoothed phasor
        double I2, double Q2,
        // Re, Im smoothed homodyne components
        double Re, double Im,
        // Period tracking: raw → instPeriod → dcPeriod
        double Period, double InstPeriod, double DcPeriod,
        // Super Smoother state
        double Mom0, double Mom1, double Filt1, double Filt2,
        // General
        int BarCount, double LastValidValue
    );

    private State _s;
    private State _ps;
    private ITValuePublisher? _source;
    private bool _disposed;

    /// <summary>Gets the current estimated dominant cycle period.</summary>
    public double DominantCycle => _s.DcPeriod;

    public override bool IsHot => _s.BarCount >= WarmupPeriod;

    /// <summary>
    /// Creates a new Smoothed Adaptive Momentum indicator.
    /// </summary>
    /// <param name="alpha">Smoothing factor for cycle measurement (0 &lt; alpha &lt;= 1). Default 0.07.</param>
    /// <param name="cutoff">Super Smoother cutoff period (must be >= 2). Default 8.</param>
    public Sam(double alpha = 0.07, int cutoff = 8)
    {
        if (alpha is <= 0 or > 1)
        {
            throw new ArgumentException("Alpha must be in (0, 1]", nameof(alpha));
        }
        if (cutoff < 2)
        {
            throw new ArgumentException("Cutoff must be >= 2", nameof(cutoff));
        }

        _alpha = alpha;
        _alphaDecay = 1.0 - alpha;

        // Precompute Super Smoother coefficients
        double a1 = Math.Exp(-Sqrt2 * Math.PI / cutoff);
        double b1 = 2.0 * a1 * Math.Cos(Sqrt2 * Math.PI / cutoff);
        _ssC2 = b1;
        _ssC3 = -(a1 * a1);
        _ssC1 = 1.0 - _ssC2 - _ssC3;

        // Price lookback buffer: max dominant cycle period
        _priceBuf = new RingBuffer(MaxCyclePeriod + 1);

        Name = $"Sam({alpha},{cutoff})";
        WarmupPeriod = MaxCyclePeriod * 2; // 100 bars for stable cycle detection

        // Initialize state with default period estimate
        const double initialPeriod = 15.0;
        _s = new State(
            0, 0, 0, 0,                       // Price history
            0, 0, 0, 0, 0, 0, 0,              // Smooth price history
            0, 0, 0, 0, 0, 0, 0,              // Detrender history
            0, 0, 0, 0, 0, 0, 0,              // I1 history
            0, 0, 0, 0, 0, 0, 0,              // Q1 history
            0, 0,                              // I2, Q2
            0, 0,                              // Re, Im
            initialPeriod, initialPeriod, initialPeriod, // Period, InstPeriod, DcPeriod
            0, 0, 0, 0,                        // Mom0, Mom1, Filt1, Filt2
            0, 0                               // BarCount, LastValidValue
        );
        _ps = _s;
    }

    /// <summary>
    /// Creates a chained Smoothed Adaptive Momentum indicator.
    /// </summary>
    public Sam(ITValuePublisher source, double alpha = 0.07, int cutoff = 8)
        : this(alpha, cutoff)
    {
        _source = source;
        _source.Pub += HandleInput;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleInput(object? sender, in TValueEventArgs e)
    {
        Update(e.Value, e.IsNew);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _ps = _s;
        }
        else
        {
            _s = _ps;
        }

        var s = _s;

        // Handle non-finite values
        double price = input.Value;
        if (!double.IsFinite(price))
        {
            price = s.LastValidValue;
        }
        else
        {
            s = s with { LastValidValue = price };
        }

        // Increment bar count
        int barCount = isNew ? s.BarCount + 1 : s.BarCount;

        // Add price to lookback buffer for adaptive momentum
        _priceBuf.Add(price, isNew);

        // ── Stage 1: 4-bar FIR smoother: (src + 2*src[1] + 2*src[2] + src[3]) / 6 ──
        double price3 = s.Price2;
        double price2 = s.Price1;
        double price1 = s.Price0;
        double price0 = price;

        double smoothPrice = (price0 + 2.0 * price1 + 2.0 * price2 + price3) / 6.0;

        // ── Stage 2: Hilbert Transform ──
        // Adaptive bandwidth based on previous smooth period
        double bandwidth = 0.075 * s.DcPeriod + 0.54;

        // Shift smooth price history
        double sp6 = s.Sp5;
        double sp5 = s.Sp4;
        double sp4 = s.Sp3;
        double sp3 = s.Sp2;
        double sp2 = s.Sp1;
        double sp1 = s.Sp0;
        double sp0 = smoothPrice;

        // Detrender: Hilbert Transform of smooth price
        double detrender = (0.0962 * sp0 + 0.5769 * sp2 - 0.5769 * sp4 - 0.0962 * sp6) * bandwidth;

        // Shift detrender history
        double det6 = s.Det5;
        double det5 = s.Det4;
        double det4 = s.Det3;
        double det3 = s.Det2;
        double det2 = s.Det1;
        double det1 = s.Det0;
        double det0 = detrender;

        // Q1 via Hilbert Transform of detrender
        double q1 = (0.0962 * det0 + 0.5769 * det2 - 0.5769 * det4 - 0.0962 * det6) * bandwidth;

        // I1 is detrender delayed by 3 bars
        double i1 = det3;

        // Shift I1 history for JI calculation
        double i1_6 = s.I1_5;
        double i1_5 = s.I1_4;
        double i1_4 = s.I1_3;
        double i1_3 = s.I1_2;
        double i1_2 = s.I1_1;
        double i1_1 = s.I1_0;
        double i1_0 = i1;

        // Shift Q1 history for JQ calculation
        double q1_6 = s.Q1_5;
        double q1_5 = s.Q1_4;
        double q1_4 = s.Q1_3;
        double q1_3 = s.Q1_2;
        double q1_2 = s.Q1_1;
        double q1_1 = s.Q1_0;
        double q1_0 = q1;

        // ── Stage 3: Phase advance ──
        // JI = Hilbert Transform of I1
        double ji = (0.0962 * i1_0 + 0.5769 * i1_2 - 0.5769 * i1_4 - 0.0962 * i1_6) * bandwidth;

        // JQ = Hilbert Transform of Q1
        double jq = (0.0962 * q1_0 + 0.5769 * q1_2 - 0.5769 * q1_4 - 0.0962 * q1_6) * bandwidth;

        // Phasor addition: I2 = I1 - JQ, Q2 = Q1 + JI
        double i2Raw = i1 - jq;
        double q2Raw = q1 + ji;

        // EMA smooth I2 and Q2 with configurable alpha
        double i2 = Math.FusedMultiplyAdd(_alphaDecay, s.I2, _alpha * i2Raw);
        double q2 = Math.FusedMultiplyAdd(_alphaDecay, s.Q2, _alpha * q2Raw);

        // ── Stage 4: Homodyne Discriminator ──
        double reRaw = Math.FusedMultiplyAdd(i2, s.I2, q2 * s.Q2);
        double imRaw = Math.FusedMultiplyAdd(i2, s.Q2, -(q2 * s.I2));

        // EMA smooth Re and Im
        double re = Math.FusedMultiplyAdd(_alphaDecay, s.Re, _alpha * reRaw);
        double im = Math.FusedMultiplyAdd(_alphaDecay, s.Im, _alpha * imRaw);

        // Calculate period from phase angle
        double period = s.Period;
        if (Math.Abs(im) > 1e-10 && Math.Abs(re) > 1e-10)
        {
            double candidate = TwoPi / Math.Atan(im / re);
            period = Math.Clamp(Math.Abs(candidate), MinCyclePeriod, MaxCyclePeriod);
        }

        // Double-smoothed dominant cycle period
        double instPeriod = Math.FusedMultiplyAdd(0.33, period, 0.67 * s.InstPeriod);
        double dcPeriod = Math.FusedMultiplyAdd(0.15, instPeriod, 0.85 * s.DcPeriod);

        // ── Stage 5: Adaptive momentum ──
        int dcLen = Math.Max((int)dcPeriod, 1);
        double momentum;
        if (_priceBuf.Count > dcLen)
        {
            // RingBuffer[0] is oldest; we want price[dcLen] bars ago
            // Current price is at index (Count-1), price dcLen bars ago is at index (Count-1-dcLen)
            int lookbackIdx = _priceBuf.Count - 1 - dcLen;
            momentum = price - _priceBuf[lookbackIdx];
        }
        else
        {
            momentum = 0.0;
        }

        // ── Stage 6: 2-pole Super Smoother ──
        double mom1 = s.Mom0;
        double mom0 = momentum;

        double filt = _ssC1 * (mom0 + mom1) / 2.0 + _ssC2 * s.Filt1 + _ssC3 * s.Filt2;

        // Update state
        _s = new State(
            price0, price1, price2, price3,
            sp0, sp1, sp2, sp3, sp4, sp5, sp6,
            det0, det1, det2, det3, det4, det5, det6,
            i1_0, i1_1, i1_2, i1_3, i1_4, i1_5, i1_6,
            q1_0, q1_1, q1_2, q1_3, q1_4, q1_5, q1_6,
            i2, q2,
            re, im,
            period, instPeriod, dcPeriod,
            mom0, mom1, filt, s.Filt1,
            barCount, s.LastValidValue
        );

        Last = new TValue(input.Time, filt);
        PubEvent(Last, isNew);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        for (int i = 0; i < len; i++)
        {
            var result = Update(source[i]);
            vSpan[i] = result.Value;
        }
        source.Times.CopyTo(tSpan);

        return new TSeries(t, v);
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        TimeSpan interval = step ?? TimeSpan.FromSeconds(1);
        DateTime time = DateTime.UtcNow - (interval * source.Length);

        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(time, source[i]), true);
            time += interval;
        }
    }

    /// <summary>
    /// Calculates SAM for a time series.
    /// </summary>
    public static TSeries Batch(TSeries source, double alpha = 0.07, int cutoff = 8)
    {
        var sam = new Sam(alpha, cutoff);
        return sam.Update(source);
    }

    /// <summary>
    /// Calculates SAM in-place using a pre-allocated output span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output,
                             double alpha = 0.07, int cutoff = 8)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (alpha is <= 0 or > 1)
        {
            throw new ArgumentException("Alpha must be in (0, 1]", nameof(alpha));
        }
        if (cutoff < 2)
        {
            throw new ArgumentException("Cutoff must be >= 2", nameof(cutoff));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        var sam = new Sam(alpha, cutoff);
        for (int i = 0; i < len; i++)
        {
            var result = sam.Update(new TValue(DateTime.UtcNow, source[i]));
            output[i] = result.Value;
        }
    }

    public static (TSeries Results, Sam Indicator) Calculate(TSeries source, double alpha = 0.07, int cutoff = 8)
    {
        var indicator = new Sam(alpha, cutoff);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    public override void Reset()
    {
        const double initialPeriod = 15.0;
        _priceBuf.Clear();
        _s = new State(
            0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0,
            0, 0,
            0, 0,
            initialPeriod, initialPeriod, initialPeriod,
            0, 0, 0, 0,
            0, 0
        );
        _ps = _s;
        Last = default;
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing && _source != null)
            {
                _source.Pub -= HandleInput;
                _source = null;
            }
            _disposed = true;
        }
        base.Dispose(disposing);
    }
}
