using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// HT_PHASOR: Hilbert Transform Phasor Components - Decomposes price data into InPhase and Quadrature components.
/// </summary>
/// <remarks>
/// The Hilbert Transform Phasor Components indicator splits the price signal into two orthogonal components:
/// InPhase (Real part) and Quadrature (Imaginary part). These components represent the cyclic behavior of the market.
///
/// Algorithm:
/// 1. Detrend the price using a Homodyne discriminator or similar filter.
/// 2. Apply the Hilbert Transform to the detrended signal.
/// 3. Extract the InPhase (associated with the signal itself) and Quadrature (shifted by 90 degrees) components.
/// 4. The implementation matches TA-Lib's HT_PHASOR, including 32-bar warmup and specific smoothing.
///
/// Properties:
/// - InPhase component corresponds to the cyclic movement aligned with price.
/// - Quadrature component corresponds to the rate of change of the cycle.
/// - A crossover of these components can signal cycle turning points.
/// </remarks>
[SkipLocalsInit]
public sealed class HtPhasor : AbstractBase
{
    private const int LOOKBACK = 32; // TA-Lib HT_PHASOR lookback
    private const int SMOOTH_PRICE_SIZE = 50;
    private const int CIRC_BUFFER_SIZE = 44; // 4 * 11 for Hilbert transform
    private const int PRICE_HISTORY_SIZE = 64;

    private const double A_CONST = 0.0962;
    private const double B_CONST = 0.5769;

    public double Quadrature { get; private set; }

    private const int KEY_DETRENDER = 6;
    private const int KEY_Q1 = 17;
    private const int KEY_JI = 28;
    private const int KEY_JQ = 39;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double PrevI2, double PrevQ2, double Re, double Im,
        double Period, double SmoothPeriod,
        double I1ForOddPrev3, double I1ForEvenPrev3,
        double I1ForOddPrev2, double I1ForEvenPrev2,
        double PeriodWMASub, double PeriodWMASum, double TrailingWMAValue,
        int TrailingWMAIdx, int HilbertIdx, int SmoothPriceIdx,
        double LastValidPrice, int Today
    )
    {
        public State() : this(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, double.NaN, 0) { }
    }

    private State _state;
    private State _p_state;

    private readonly double[] _circBuffer;
    private readonly double[] _p_circBuffer;
    private readonly double[] _smoothPrice;
    private readonly double[] _p_smoothPrice;
    private readonly double[] _priceHistory;
    private readonly double[] _p_priceHistory;

    private readonly TValuePublishedHandler _handler;

    public override bool IsHot => _state.Today >= LOOKBACK;

    public HtPhasor()
    {
        Name = "HtPhasor";
        WarmupPeriod = LOOKBACK;
        _handler = Handle;

        _circBuffer = new double[CIRC_BUFFER_SIZE];
        _p_circBuffer = new double[CIRC_BUFFER_SIZE];
        _smoothPrice = new double[SMOOTH_PRICE_SIZE];
        _p_smoothPrice = new double[SMOOTH_PRICE_SIZE];
        _priceHistory = new double[PRICE_HISTORY_SIZE];
        _p_priceHistory = new double[PRICE_HISTORY_SIZE];

        Init();
    }

    public HtPhasor(ITValuePublisher source) : this()
    {
        ArgumentNullException.ThrowIfNull(source);
        source.Pub += _handler;
    }

    private void Init() => Reset();

    public override void Reset()
    {
        _state = new State();
        _p_state = new State();

        Array.Clear(_circBuffer);
        Array.Clear(_p_circBuffer);
        Array.Clear(_smoothPrice);
        Array.Clear(_p_smoothPrice);
        Array.Clear(_priceHistory);
        Array.Clear(_p_priceHistory);

        Quadrature = 0;
        Last = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DoHilbertTransform(
        Span<double> buffer, int baseKey, double input, bool isOdd, int hilbertIdx, double adjustedPrevPeriod)
    {
        double hilbertTempT = A_CONST * input;
        int hilbertIndex = baseKey - (isOdd ? 6 : 3) + hilbertIdx;
        int prevIndex = baseKey + (isOdd ? 1 : 2);
        int prevInputIndex = baseKey + (isOdd ? 3 : 4);

        buffer[baseKey] = -buffer[hilbertIndex];
        buffer[hilbertIndex] = hilbertTempT;
        buffer[baseKey] += hilbertTempT;
        buffer[baseKey] -= buffer[prevIndex];
        buffer[prevIndex] = B_CONST * buffer[prevInputIndex];
        buffer[baseKey] += buffer[prevIndex];
        buffer[prevInputIndex] = input;
        buffer[baseKey] *= adjustedPrevPeriod;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalcHilbertOdd(
        Span<double> buffer, double smoothedValue, int hilbertIdx, double adjustedPrevPeriod,
        out double i1ForEvenPrev3, double prevQ2, double prevI2, double i1ForOddPrev3,
        ref double i1ForEvenPrev2, out double q2, out double i2)
    {
        DoHilbertTransform(buffer, KEY_DETRENDER, smoothedValue, true, hilbertIdx, adjustedPrevPeriod);
        double input = buffer[KEY_DETRENDER];
        DoHilbertTransform(buffer, KEY_Q1, input, true, hilbertIdx, adjustedPrevPeriod);
        DoHilbertTransform(buffer, KEY_JI, i1ForOddPrev3, true, hilbertIdx, adjustedPrevPeriod);
        double input1 = buffer[KEY_Q1];
        DoHilbertTransform(buffer, KEY_JQ, input1, true, hilbertIdx, adjustedPrevPeriod);

        q2 = 0.2 * (buffer[KEY_Q1] + buffer[KEY_JI]) + 0.8 * prevQ2;
        i2 = 0.2 * (i1ForOddPrev3 - buffer[KEY_JQ]) + 0.8 * prevI2;

        i1ForEvenPrev3 = i1ForEvenPrev2;
        i1ForEvenPrev2 = buffer[KEY_DETRENDER];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalcHilbertEven(
        Span<double> buffer, double smoothedValue, ref int hilbertIdx, double adjustedPrevPeriod,
        double i1ForEvenPrev3, double prevQ2, double prevI2, out double i1ForOddPrev3,
        ref double i1ForOddPrev2, out double q2, out double i2)
    {
        DoHilbertTransform(buffer, KEY_DETRENDER, smoothedValue, false, hilbertIdx, adjustedPrevPeriod);
        double input = buffer[KEY_DETRENDER];
        DoHilbertTransform(buffer, KEY_Q1, input, false, hilbertIdx, adjustedPrevPeriod);
        DoHilbertTransform(buffer, KEY_JI, i1ForEvenPrev3, false, hilbertIdx, adjustedPrevPeriod);
        double input1 = buffer[KEY_Q1];
        DoHilbertTransform(buffer, KEY_JQ, input1, false, hilbertIdx, adjustedPrevPeriod);

        if (++hilbertIdx == 3)
        {
            hilbertIdx = 0;
        }

        q2 = 0.2 * (buffer[KEY_Q1] + buffer[KEY_JI]) + 0.8 * prevQ2;
        i2 = 0.2 * (i1ForEvenPrev3 - buffer[KEY_JQ]) + 0.8 * prevI2;

        i1ForOddPrev3 = i1ForOddPrev2;
        i1ForOddPrev2 = buffer[KEY_DETRENDER];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalcSmoothedPeriod(
        ref double re, double i2, double q2, ref double prevI2, ref double prevQ2, ref double im, ref double period)
    {
        re = Math.FusedMultiplyAdd(0.2, i2 * prevI2 + q2 * prevQ2, 0.8 * re);
        im = Math.FusedMultiplyAdd(0.2, i2 * prevQ2 - q2 * prevI2, 0.8 * im);

        prevQ2 = q2;
        prevI2 = i2;

        double tempReal1 = period;
        if (Math.Abs(im) > 1e-10 && Math.Abs(re) > 1e-10)
        {
            double angle = Math.Atan(im / re);
            if (Math.Abs(angle) > 1e-10)
            {
                period = (2.0 * Math.PI) / angle;
            }
        }

        double tempReal2 = 1.5 * tempReal1;
        period = Math.Min(period, tempReal2);

        tempReal2 = 0.67 * tempReal1;
        period = Math.Max(period, tempReal2);

        period = Math.Clamp(period, 6.0, 50.0);
        period = Math.FusedMultiplyAdd(0.2, period, 0.8 * tempReal1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double UpdateWma(ref State s, double price, double[] priceHistory, bool isNew)
    {
        int historyIdx;
        if (isNew)
        {
            historyIdx = s.Today % PRICE_HISTORY_SIZE;
        }
        else if (s.Today == 0)
        {
            historyIdx = 0;
        }
        else
        {
            historyIdx = (s.Today - 1 + PRICE_HISTORY_SIZE) % PRICE_HISTORY_SIZE;
        }

        priceHistory[historyIdx] = price;

        int processed = s.Today + (isNew ? 1 : 0);
        if (processed <= 3)
        {
            if (isNew)
            {
                s.Today++;
            }
            return 0.0;
        }

        static double Get(double[] hist, int latestIdx, int offset)
        {
            int idx = (latestIdx - offset + PRICE_HISTORY_SIZE) % PRICE_HISTORY_SIZE;
            return hist[idx];
        }

        double p0 = price;
        double p1 = Get(priceHistory, historyIdx, 1);
        double p2 = Get(priceHistory, historyIdx, 2);
        double p3 = Get(priceHistory, historyIdx, 3);

        double smoothedValue = (4.0 * p0 + 3.0 * p1 + 2.0 * p2 + p3) * 0.1;

        s.PeriodWMASub = p0 + p1 + p2 + p3;
        s.PeriodWMASum = smoothedValue * 10.0;
        s.TrailingWMAValue = p3;

        if (isNew)
        {
            s.Today++;
        }

        return smoothedValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private (double inPhase, double quadrature) Step(double price, bool isNew)
    {
        if (isNew)
        {
            _p_state = _state;
            Array.Copy(_circBuffer, _p_circBuffer, CIRC_BUFFER_SIZE);
            Array.Copy(_smoothPrice, _p_smoothPrice, SMOOTH_PRICE_SIZE);
            Array.Copy(_priceHistory, _p_priceHistory, PRICE_HISTORY_SIZE);
        }
        else
        {
            _state = _p_state;
            Array.Copy(_p_circBuffer, _circBuffer, CIRC_BUFFER_SIZE);
            Array.Copy(_p_smoothPrice, _smoothPrice, SMOOTH_PRICE_SIZE);
            Array.Copy(_p_priceHistory, _priceHistory, PRICE_HISTORY_SIZE);
        }

        var s = _state;

        if (!isNew)
        {
            // Same-bar updates should reuse prior result without mutating buffers or state
            _state = s;
            return (Last.Value, Quadrature);
        }

        if (!double.IsFinite(price))
        {
            if (double.IsNaN(s.LastValidPrice))
            {
                return (double.NaN, double.NaN);
            }
            price = s.LastValidPrice;
        }
        else
        {
            s.LastValidPrice = price;
        }

        // WMA init and smoothing (updates day counter only when isNew)
        double smoothedValue = UpdateWma(ref s, price, _priceHistory, isNew);

        // Still initializing WMA until day 3; smoothedValue only valid from day >=3
        if (s.Today <= 3)
        {
            _state = s;
            return (0.0, 0.0);
        }

        // Before Hilbert warmup (need several smoothed values). TA pre-loop does 9 iterations after first 3 -> require day >= 13
        if (s.Today < 13)
        {
            _state = s;
            return (0.0, 0.0);
        }

        // Extract fields
        int hilbertIdx = s.HilbertIdx;
        double i1ForOddPrev2 = s.I1ForOddPrev2;
        double i1ForEvenPrev2 = s.I1ForEvenPrev2;
        double re = s.Re;
        double im = s.Im;
        double prevI2 = s.PrevI2;
        double prevQ2 = s.PrevQ2;
        double period = s.Period;

        double adjustedPrevPeriod = 0.075 * period + 0.54;
        _smoothPrice[s.SmoothPriceIdx] = smoothedValue;

        double q2, i2;
        double inPhaseOutput;
        double quadratureOutput;

        if ((s.Today & 1) == 0)
        {
            // even bar
            CalcHilbertEven(_circBuffer, smoothedValue, ref hilbertIdx, adjustedPrevPeriod,
                s.I1ForEvenPrev3, prevQ2, prevI2, out double i1ForOddPrev3,
                ref i1ForOddPrev2, out q2, out i2);
            s.I1ForOddPrev3 = i1ForOddPrev3;
            inPhaseOutput = s.I1ForEvenPrev3;
        }
        else
        {
            // odd bar
            CalcHilbertOdd(_circBuffer, smoothedValue, hilbertIdx, adjustedPrevPeriod,
                out double i1ForEvenPrev3, prevQ2, prevI2, s.I1ForOddPrev3,
                ref i1ForEvenPrev2, out q2, out i2);
            s.I1ForEvenPrev3 = i1ForEvenPrev3;
            inPhaseOutput = s.I1ForOddPrev3;
        }

        quadratureOutput = _circBuffer[KEY_Q1];

        s.HilbertIdx = hilbertIdx;
        s.I1ForOddPrev2 = i1ForOddPrev2;
        s.I1ForEvenPrev2 = i1ForEvenPrev2;

        CalcSmoothedPeriod(ref re, i2, q2, ref prevI2, ref prevQ2, ref im, ref period);

        s.Re = re;
        s.Im = im;
        s.PrevI2 = prevI2;
        s.PrevQ2 = prevQ2;
        s.Period = period;
        s.SmoothPeriod = Math.FusedMultiplyAdd(0.33, period, 0.67 * s.SmoothPeriod);

        s.SmoothPriceIdx = (s.SmoothPriceIdx + 1) % SMOOTH_PRICE_SIZE;

        _state = s;

        return (inPhaseOutput, quadratureOutput);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        var (inPhase, quadrature) = Step(input.Value, isNew);
        Quadrature = quadrature;
        Last = new TValue(input.Time, inPhase);
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
        var t = new List<long>(len);
        var v = new List<double>(len);

        for (int i = 0; i < len; i++)
        {
            var result = Update(new TValue(source.Times[i], source.Values[i]));
            t.Add(result.Time);
            v.Add(result.Value);
        }

        return new TSeries(t, v);
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (double value in source)
        {
            Update(new TValue(DateTime.UtcNow, value));
        }
    }

    /// <summary>
    /// Calculates HT_PHASOR for a time series.
    /// </summary>
    public static TSeries Batch(TSeries source)
    {
        var htPhasor = new HtPhasor();
        return htPhasor.Update(source);
    }

    /// <summary>
    /// Calculates HT_PHASOR in-place using pre-allocated output spans.
    /// </summary>
    /// <param name="source">Input price data.</param>
    /// <param name="inPhase">Output span for InPhase values.</param>
    /// <param name="quadrature">Output span for Quadrature values.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> inPhase, Span<double> quadrature)
    {
        if (source.Length != inPhase.Length)
        {
            throw new ArgumentException("Source and inPhase must have the same length", nameof(inPhase));
        }
        if (source.Length != quadrature.Length)
        {
            throw new ArgumentException("Source and quadrature must have the same length", nameof(quadrature));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        var htPhasor = new HtPhasor();
        for (int i = 0; i < len; i++)
        {
            htPhasor.Update(new TValue(DateTime.UtcNow, source[i]));
            inPhase[i] = htPhasor.Last.Value;
            quadrature[i] = htPhasor.Quadrature;
        }
    }

    public static (TSeries Results, HtPhasor Indicator) Calculate(TSeries source)
    {
        var indicator = new HtPhasor();
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}