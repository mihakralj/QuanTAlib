using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// HOMOD: Homodyne Discriminator - Ehlers dominant cycle detection using
/// homodyne multiplication and phase angle measurement.
/// </summary>
/// <remarks>
/// The Homodyne Discriminator, developed by John Ehlers, estimates the dominant
/// cycle period by multiplying the analytic signal with a delayed version of itself
/// (homodyne mixing). The resulting phase difference directly yields cycle frequency.
///
/// Algorithm:
/// 1. 4-bar weighted moving average smooths input price
/// 2. Hilbert Transform detects phase components (I and Q)
/// 3. Homodyne mixing: multiply I/Q with their 1-bar delayed values
/// 4. Re = I*I[1] + Q*Q[1], Im = I*Q[1] - Q*I[1]
/// 5. Angle = atan2(Im, Re) gives instantaneous phase change
/// 6. Period = 2Ï€ / angle with clamping and smoothing
///
/// Properties:
/// - Returns smoothed dominant cycle period
/// - Detects cycle frequency from phase rate of change
/// - Robust to noise via multiple EMA smoothing stages
/// - Exponential warmup compensation for fast convergence
///
/// Key Insight:
/// Homodyne mixing reveals instantaneous frequency by measuring the phase
/// rotation between consecutive samples. This is more responsive than
/// spectral methods while maintaining noise immunity.
/// </remarks>
[SkipLocalsInit]
public sealed class Homod : AbstractBase
{
    private readonly double _minPeriod;
    private readonly double _maxPeriod;
    private const double TwoPi = 2.0 * Math.PI;
    private const double HalfPi = Math.PI / 2.0;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        // Price history for 4-bar WMA
        double Price0, double Price1, double Price2, double Price3,
        // Smooth price history for detrender
        double Sp0, double Sp1, double Sp2, double Sp3, double Sp4, double Sp5, double Sp6,
        // Detrender history for Q1
        double Det0, double Det1, double Det2, double Det3, double Det4, double Det5, double Det6,
        // I1 history for JI
        double I1_0, double I1_1, double I1_2, double I1_3, double I1_4, double I1_5, double I1_6,
        // Q1 history for JQ
        double Q1_0, double Q1_1, double Q1_2, double Q1_3, double Q1_4, double Q1_5, double Q1_6,
        // I2, Q2 for homodyne
        double I2, double I2Prev,
        double Q2, double Q2Prev,
        // Re, Im for angle calculation
        double Re, double Im,
        // Period tracking
        double Period, double SmoothPeriod,
        // Warmup
        double WarmDecay, bool InWarmup,
        // General
        int BarCount, double LastValidValue
    );

    private State _s;
    private State _ps;

    /// <summary>Gets the current dominant cycle period.</summary>
    public double DominantCycle => _s.SmoothPeriod;

    public override bool IsHot => _s.BarCount >= WarmupPeriod;

    /// <summary>
    /// Creates a new Homodyne Discriminator indicator.
    /// </summary>
    /// <param name="minPeriod">Minimum period to detect (must be > 0).</param>
    /// <param name="maxPeriod">Maximum period to detect (must be > minPeriod).</param>
    public Homod(double minPeriod = 6.0, double maxPeriod = 50.0)
    {
        if (minPeriod <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minPeriod), "Min period must be greater than 0.");
        }
        if (maxPeriod <= minPeriod)
        {
            throw new ArgumentOutOfRangeException(nameof(maxPeriod), "Max period must be greater than min period.");
        }

        _minPeriod = minPeriod;
        _maxPeriod = maxPeriod;

        Name = $"Homod({minPeriod},{maxPeriod})";
        WarmupPeriod = (int)(maxPeriod * 2);

        // Initialize state with default period of 15
        const double initialPeriod = 15.0;
        _s = new State(
            0, 0, 0, 0,           // Price history
            0, 0, 0, 0, 0, 0, 0,  // Smooth price history
            0, 0, 0, 0, 0, 0, 0,  // Detrender history
            0, 0, 0, 0, 0, 0, 0,  // I1 history
            0, 0, 0, 0, 0, 0, 0,  // Q1 history
            0, 0, 0, 0,           // I2, Q2 with prev
            0, 0,                 // Re, Im
            initialPeriod, initialPeriod,  // Period, SmoothPeriod
            1.0, true,            // WarmDecay, InWarmup
            0, 0                  // BarCount, LastValidValue
        );
        _ps = _s;
    }

    /// <summary>
    /// Creates a chained Homodyne Discriminator indicator.
    /// </summary>
    public Homod(ITValuePublisher source, double minPeriod = 6.0, double maxPeriod = 50.0)
        : this(minPeriod, maxPeriod)
    {
        ArgumentNullException.ThrowIfNull(source);
        source.Pub += HandleInput;
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

        // Shift price history
        double price3 = s.Price2;
        double price2 = s.Price1;
        double price1 = s.Price0;
        double price0 = price;

        // Calculate bandwidth based on smooth period
        double bandwidth = 0.075 * s.SmoothPeriod + 0.54;

        // 4-bar weighted moving average: (4*p0 + 3*p1 + 2*p2 + p3) / 10
        double smoothPrice = (4.0 * price0 + 3.0 * price1 + 2.0 * price2 + price3) / 10.0;

        // Shift smooth price history
        double sp6 = s.Sp5;
        double sp5 = s.Sp4;
        double sp4 = s.Sp3;
        double sp3 = s.Sp2;
        double sp2 = s.Sp1;
        double sp1 = s.Sp0;
        double sp0 = smoothPrice;

        // Hilbert Transform detrender: coefficients [0.0962, 0, 0.5769, 0, -0.5769, 0, -0.0962] * bandwidth
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

        // JI = Hilbert Transform of I1
        double ji = (0.0962 * i1_0 + 0.5769 * i1_2 - 0.5769 * i1_4 - 0.0962 * i1_6) * bandwidth;

        // JQ = Hilbert Transform of Q1
        double jq = (0.0962 * q1_0 + 0.5769 * q1_2 - 0.5769 * q1_4 - 0.0962 * q1_6) * bandwidth;

        // Calculate I2 and Q2 (phasor rotation)
        double i2Raw = i1 - jq;
        double q2Raw = q1 + ji;

        // EMA smooth I2 and Q2 (alpha = 0.2): FMA(0.2, x, 0.8*y)
        double i2 = Math.FusedMultiplyAdd(0.2, i2Raw, 0.8 * s.I2);
        double q2 = Math.FusedMultiplyAdd(0.2, q2Raw, 0.8 * s.Q2);

        // Homodyne discriminator: multiply with previous values
        double reRaw = Math.FusedMultiplyAdd(i2, s.I2, q2 * s.Q2);
        double imRaw = Math.FusedMultiplyAdd(i2, s.Q2, -(q2 * s.I2));

        // EMA smooth Re and Im (alpha = 0.2): FMA(0.2, x, 0.8*y)
        double re = Math.FusedMultiplyAdd(0.2, reRaw, 0.8 * s.Re);
        double im = Math.FusedMultiplyAdd(0.2, imRaw, 0.8 * s.Im);

        // Calculate period from angle
        double period = s.Period;
        double magnitude = Math.Abs(re) + Math.Abs(im);

        if (magnitude > 1e-10)
        {
            double angle = Atan2(im, re);
            if (Math.Abs(angle) > 1e-10)
            {
                double candidate = TwoPi / angle;
                double clamped = Math.Clamp(Math.Abs(candidate), _minPeriod, _maxPeriod);
                period = Math.FusedMultiplyAdd(0.2, clamped, 0.8 * period);
            }
        }

        // Smooth the period (alpha = 0.33): FMA(alpha, delta, prevSmooth)
        const double alpha = 0.33;
        double smoothPeriod = Math.FusedMultiplyAdd(alpha, period - s.SmoothPeriod, s.SmoothPeriod);

        // Exponential warmup compensation
        double result = smoothPeriod;
        double warmDecay = s.WarmDecay;
        bool inWarmup = s.InWarmup;

        if (inWarmup)
        {
            warmDecay *= 1.0 - alpha;
            double denom = 1.0 - warmDecay;
            if (denom > 1e-10)
            {
                result /= denom;
            }
            inWarmup = warmDecay > 1e-10;
        }

        // Update state
        _s = new State(
            price0, price1, price2, price3,
            sp0, sp1, sp2, sp3, sp4, sp5, sp6,
            det0, det1, det2, det3, det4, det5, det6,
            i1_0, i1_1, i1_2, i1_3, i1_4, i1_5, i1_6,
            q1_0, q1_1, q1_2, q1_3, q1_4, q1_5, q1_6,
            i2, s.I2,  // I2 and I2Prev
            q2, s.Q2,  // Q2 and Q2Prev
            re, im,
            period, smoothPeriod,
            warmDecay, inWarmup,
            barCount, s.LastValidValue
        );

        Last = new TValue(input.Time, result);
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

    /// <summary>
    /// Quadrant-aware angle calculation using stable atan2.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Atan2(double y, double x)
    {
        if (y == 0.0 && x == 0.0) // skipcq: CS-R1077 - Exact-zero guard: both quadrature components zero means no signal; atan2(0,0) is undefined
        {
            return 0.0; // Return 0 instead of error for robustness
        }

        double ay = Math.Abs(y);
        double ax = Math.Abs(x);
        double angle;

        if (ax > ay)
        {
            angle = Math.Atan(ay / ax);
        }
        else
        {
            angle = HalfPi - Math.Atan(ax / ay);
        }

        if (x < 0.0)
        {
            angle = Math.PI - angle;
        }
        if (y < 0.0)
        {
            angle = -angle;
        }

        return angle;
    }

    public override void Reset()
    {
        const double initialPeriod = 15.0;
        _s = new State(
            0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0,
            0, 0,
            initialPeriod, initialPeriod,
            1.0, true,
            0, 0
        );
        _ps = _s;
        Last = default;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (double value in source)
        {
            Update(new TValue(DateTime.UtcNow, value));
        }
    }

    /// <summary>
    /// Calculates Homodyne Discriminator for a time series.
    /// </summary>
    public static TSeries Batch(TSeries source, double minPeriod = 6.0, double maxPeriod = 50.0)
    {
        var homod = new Homod(minPeriod, maxPeriod);
        return homod.Update(source);
    }

    /// <summary>
    /// Calculates Homodyne Discriminator in-place using a pre-allocated output span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output,
                             double minPeriod = 6.0, double maxPeriod = 50.0)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (minPeriod <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minPeriod), "Min period must be greater than 0.");
        }
        if (maxPeriod <= minPeriod)
        {
            throw new ArgumentOutOfRangeException(nameof(maxPeriod), "Max period must be greater than min period.");
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        var homod = new Homod(minPeriod, maxPeriod);
        for (int i = 0; i < len; i++)
        {
            var result = homod.Update(new TValue(DateTime.UtcNow, source[i]));
            output[i] = result.Value;
        }
    }

    public static (TSeries Results, Homod Indicator) Calculate(TSeries source, double minPeriod = 6.0, double maxPeriod = 50.0)
    {
        var indicator = new Homod(minPeriod, maxPeriod);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}