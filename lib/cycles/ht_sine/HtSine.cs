using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// HT_SINE: Hilbert Transform - SineWave (also known as SINE) indicator that uses
/// the Hilbert Transform to compute the sine of the dominant cycle phase. Returns
/// both Sine and LeadSine (45° phase lead) for cycle timing.
/// </summary>
/// <remarks>
/// The Hilbert Transform SineWave indicator identifies the dominant market cycle
/// and outputs the sine of the current phase angle. The LeadSine provides a 45°
/// phase lead for early signal detection.
///
/// Key Features:
/// - Oscillates between -1 and +1
/// - Crossover of Sine/LeadSine indicates cycle turning points
/// - Sine crossing LeadSine from below = potential buy
/// - Sine crossing LeadSine from above = potential sell
/// - Works best in ranging/cycling markets
///
/// Reference: John Ehlers' "Rocket Science for Traders", TA-Lib implementation
/// </remarks>
[SkipLocalsInit]
public sealed class HtSine : AbstractBase
{
    private const int LOOKBACK = 63; // 31 + 32 for TA-Lib compatibility
    private const int SMOOTH_PRICE_SIZE = 50;
    private const int CIRC_BUFFER_SIZE = 44; // 4 * 11 for Hilbert transform
    private const int PRICE_HISTORY_SIZE = 64; // Must hold at least LOOKBACK prices

    // Hilbert transform constants (TA-Lib exact values)
    private const double A_CONST = 0.0962;
    private const double B_CONST = 0.5769;

    /// <summary>
    /// Gets the current LeadSine value (45° phase lead).
    /// </summary>
    public double LeadSine { get; private set; }

    // Hilbert buffer keys (matching TA-Lib HTHelper.HilbertKeys)
    private const int KEY_DETRENDER = 6;
    private const int KEY_Q1 = 17;
    private const int KEY_JI = 28;
    private const int KEY_JQ = 39;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double PrevI2, double PrevQ2, double Re, double Im,
        double Period, double SmoothPeriod, double DcPhase,
        double I1ForOddPrev3, double I1ForEvenPrev3,
        double I1ForOddPrev2, double I1ForEvenPrev2,
        double PeriodWMASub, double PeriodWMASum, double TrailingWMAValue,
        int TrailingWMAIdx, int HilbertIdx, int SmoothPriceIdx,
        double LastValidPrice, int Today, bool WmaInitialized
    )
    {
        public State() : this(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, double.NaN, 0, false) { }
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

    /// <summary>
    /// Creates a new Hilbert Transform SineWave indicator.
    /// </summary>
    public HtSine()
    {
        Name = "HtSine";
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

    /// <summary>
    /// Creates a chained Hilbert Transform SineWave indicator.
    /// </summary>
    /// <param name="source">The source indicator to chain from.</param>
    public HtSine(ITValuePublisher source) : this()
    {
        ArgumentNullException.ThrowIfNull(source);
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

        Array.Clear(_circBuffer);
        Array.Clear(_p_circBuffer);
        Array.Clear(_smoothPrice);
        Array.Clear(_p_smoothPrice);
        Array.Clear(_priceHistory);
        Array.Clear(_p_priceHistory);

        LeadSine = 0;
        Last = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs e)
    {
        Update(e.Value, e.IsNew);
    }

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

        q2 = (0.2 * (buffer[KEY_Q1] + buffer[KEY_JI])) + (0.8 * prevQ2);
        i2 = (0.2 * (i1ForOddPrev3 - buffer[KEY_JQ])) + (0.8 * prevI2);

        // The variable I1 is the detrender delayed for 3 price bars.
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

        q2 = (0.2 * (buffer[KEY_Q1] + buffer[KEY_JI])) + (0.8 * prevQ2);
        i2 = (0.2 * (i1ForEvenPrev3 - buffer[KEY_JQ])) + (0.8 * prevI2);

        // The variable i1 is the detrender delayed for 3 price bars.
        i1ForOddPrev3 = i1ForOddPrev2;
        i1ForOddPrev2 = buffer[KEY_DETRENDER];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalcSmoothedPeriod(
        ref double re, double i2, double q2, ref double prevI2, ref double prevQ2, ref double im, ref double period)
    {
        re = Math.FusedMultiplyAdd(0.2, (i2 * prevI2) + (q2 * prevQ2), 0.8 * re);
        im = Math.FusedMultiplyAdd(0.2, (i2 * prevQ2) - (q2 * prevI2), 0.8 * im);

        prevQ2 = q2;
        prevI2 = i2;

        double tempReal1 = period;
        if (im != 0.0 && re != 0.0) // skipcq: CS-R1077 - Exact-zero guard: atan(im/re) needs nonzero; zero means no signal
        {
            double angle = Math.Atan(im / re);
            if (angle != 0.0) // skipcq: CS-R1077 - Exact-zero guard: angle == 0 means period undefined (div by angle)
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
    private static double ComputeDcPhase(ReadOnlySpan<double> smoothPrice, double smoothPeriod, int smoothPriceIdx, int bufferSize)
    {
        int dcPeriodInt = (int)(smoothPeriod + 0.5);
        double realPart = 0.0;
        double imagPart = 0.0;

        int idx = smoothPriceIdx;
        for (int i = 0; i < dcPeriodInt; i++)
        {
            double tempReal = i * 2.0 * Math.PI / dcPeriodInt;
            double tempReal2 = smoothPrice[idx];
            realPart += Math.Sin(tempReal) * tempReal2;
            imagPart += Math.Cos(tempReal) * tempReal2;

            idx = idx == 0 ? bufferSize - 1 : idx - 1;
        }

        double dcPhase;
        double absImagPart = Math.Abs(imagPart);
        if (absImagPart > 0.0)
        {
            dcPhase = Math.Atan(realPart / imagPart) * (180.0 / Math.PI);
        }
        else if (absImagPart <= 0.01)
        {
            if (realPart < 0.0)
            {
                dcPhase = -90.0;
            }
            else if (realPart > 0.0)
            {
                dcPhase = 90.0;
            }
            else
            {
                dcPhase = 0.0;
            }
        }
        else
        {
            dcPhase = 0.0;
        }

        // Adjustments
        dcPhase += 90.0;
        dcPhase += 360.0 / smoothPeriod; // Compensate for WMA lag

        if (imagPart < 0.0)
        {
            dcPhase += 180.0;
        }

        if (dcPhase > 315.0)
        {
            dcPhase -= 360.0;
        }

        return dcPhase;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private (double sine, double leadSine) Step(double price, bool isNew)
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

        _state.Today++;

        // Local copy of state for struct promotion (AGENTS.md §2.5)
        var s = _state;

        // Handle non-finite input
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

        int today = s.Today - 1;

        // WMA initialization phase (first 34 + 3 bars)
        if (today < 37)
        {
            // Store prices for WMA initialization
            if (today >= 0)
            {
                _priceHistory[today % PRICE_HISTORY_SIZE] = price;
            }

            // Initialize WMA (TA-Lib pattern: unrolled first 3, then loop for period)
            if (today == 36)
            {
                // Now we have enough data to initialize WMA
                double tempReal = _priceHistory[0];
                s.PeriodWMASub = tempReal;
                s.PeriodWMASum = tempReal;

                tempReal = _priceHistory[1];
                s.PeriodWMASub += tempReal;
                s.PeriodWMASum += tempReal * 2.0;

                tempReal = _priceHistory[2];
                s.PeriodWMASub += tempReal;
                s.PeriodWMASum += tempReal * 3.0;

                s.TrailingWMAValue = 0.0;
                s.TrailingWMAIdx = 0;

                // Process remaining bars in period (34 iterations)
                for (int i = 0; i < 34; i++)
                {
                    int priceIdx = 3 + i;
                    double priceVal = _priceHistory[priceIdx];

                    s.PeriodWMASub += priceVal;
                    s.PeriodWMASub -= s.TrailingWMAValue;
                    s.PeriodWMASum += priceVal * 4.0;
                    s.TrailingWMAValue = _priceHistory[s.TrailingWMAIdx++];

                    s.PeriodWMASum -= s.PeriodWMASub;
                }
            }

            _state = s;
            return (0.0, 0.0);
        }

        // Calculate smoothed price using WMA
        double adjustedPrevPeriod = (0.075 * s.Period) + 0.54;

        s.PeriodWMASub += price;
        s.PeriodWMASub -= s.TrailingWMAValue;
        s.PeriodWMASum += price * 4.0;

        // Get trailing value (TA-Lib uses a linear trailing index)
        int trailIdx = s.TrailingWMAIdx % PRICE_HISTORY_SIZE;
        s.TrailingWMAValue = _priceHistory[trailIdx];
        s.TrailingWMAIdx++;

        int historyIdx = today % PRICE_HISTORY_SIZE;
        _priceHistory[historyIdx] = price;

        double smoothedValue = s.PeriodWMASum * 0.1;
        s.PeriodWMASum -= s.PeriodWMASub;

        // Store smoothed value
        _smoothPrice[s.SmoothPriceIdx] = smoothedValue;

        // Extract fields for ref/out parameters
        int hilbertIdx = s.HilbertIdx;
        double i1ForOddPrev2 = s.I1ForOddPrev2;
        double i1ForEvenPrev2 = s.I1ForEvenPrev2;
        double re = s.Re;
        double im = s.Im;
        double prevI2 = s.PrevI2;
        double prevQ2 = s.PrevQ2;
        double period = s.Period;

        // Perform Hilbert Transform (alternating odd/even)
        double q2, i2;
        if (today % 2 == 0)
        {
            // Even bar
            CalcHilbertEven(_circBuffer, smoothedValue, ref hilbertIdx, adjustedPrevPeriod,
                s.I1ForEvenPrev3, prevQ2, prevI2, out double i1ForOddPrev3,
                ref i1ForOddPrev2, out q2, out i2);
            s.I1ForOddPrev3 = i1ForOddPrev3;
        }
        else
        {
            // Odd bar
            CalcHilbertOdd(_circBuffer, smoothedValue, hilbertIdx, adjustedPrevPeriod,
                out double i1ForEvenPrev3, prevQ2, prevI2, s.I1ForOddPrev3,
                ref i1ForEvenPrev2, out q2, out i2);
            s.I1ForEvenPrev3 = i1ForEvenPrev3;
        }

        // Write back ref parameters
        s.HilbertIdx = hilbertIdx;
        s.I1ForOddPrev2 = i1ForOddPrev2;
        s.I1ForEvenPrev2 = i1ForEvenPrev2;

        // Calculate smoothed period
        CalcSmoothedPeriod(ref re, i2, q2, ref prevI2, ref prevQ2, ref im, ref period);

        // Write back ref parameters
        s.Re = re;
        s.Im = im;
        s.PrevI2 = prevI2;
        s.PrevQ2 = prevQ2;
        s.Period = period;

        s.SmoothPeriod = Math.FusedMultiplyAdd(0.33, period, 0.67 * s.SmoothPeriod);

        // Calculate DC Phase
        s.DcPhase = ComputeDcPhase(_smoothPrice, s.SmoothPeriod, s.SmoothPriceIdx, SMOOTH_PRICE_SIZE);

        // Update smooth price index
        s.SmoothPriceIdx = (s.SmoothPriceIdx + 1) % SMOOTH_PRICE_SIZE;

        // Write back state
        _state = s;

        // Calculate sine and leadsine from DCPhase
        double sine = Math.Sin(s.DcPhase * (Math.PI / 180.0));
        double leadSine = Math.Sin((s.DcPhase + 45.0) * (Math.PI / 180.0));

        return (sine, leadSine);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        var (sine, leadSine) = Step(input.Value, isNew);
        LeadSine = leadSine;
        Last = new TValue(input.Time, sine);
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
    /// Calculates HT_SINE for a time series.
    /// </summary>
    public static TSeries Batch(TSeries source)
    {
        var htSine = new HtSine();
        return htSine.Update(source);
    }

    /// <summary>
    /// Calculates HT_SINE in-place using pre-allocated output spans.
    /// </summary>
    /// <param name="source">Input price data.</param>
    /// <param name="sine">Output span for Sine values.</param>
    /// <param name="leadSine">Output span for LeadSine values.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> sine, Span<double> leadSine)
    {
        if (source.Length != sine.Length)
        {
            throw new ArgumentException("Source and sine must have the same length", nameof(sine));
        }
        if (source.Length != leadSine.Length)
        {
            throw new ArgumentException("Source and leadSine must have the same length", nameof(leadSine));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        var htSine = new HtSine();
        for (int i = 0; i < len; i++)
        {
            htSine.Update(new TValue(DateTime.UtcNow, source[i]));
            sine[i] = htSine.Last.Value;
            leadSine[i] = htSine.LeadSine;
        }
    }

    public static (TSeries Results, HtSine Indicator) Calculate(TSeries source)
    {
        var indicator = new HtSine();
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}