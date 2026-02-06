using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// HT_TRENDMODE: Hilbert Transform Trend Mode - Determines if market is in trend or cycle mode.
/// </summary>
/// <remarks>
/// The Hilbert Transform Trend Mode, developed by John Ehlers and implemented following TA-Lib,
/// uses multiple criteria to determine whether the market is trending (1) or cycling (0).
///
/// Algorithm (from TA-Lib, based on Ehlers' original publication):
/// 1. Compute Hilbert Transform to get Sine/LeadSine indicators.
/// 2. Track days since last Sine/LeadSine crossing.
/// 3. If no crossing for half a dominant cycle period → trend mode.
/// 4. If phase change rate is "normal" (0.67× to 1.5× expected) → cycle mode.
/// 5. If price deviates ≥1.5% from trendline → trend mode override.
///
/// Properties:
/// - Returns binary output: 1 = trend mode, 0 = cycle mode.
/// - Trend mode indicates directional movement dominates.
/// - Cycle mode indicates mean-reverting/oscillating behavior dominates.
/// - Uses SineWave crossings as primary cycle timing.
///
/// Interpretation:
/// - Use trend-following strategies when TrendMode = 1.
/// - Use mean-reversion strategies when TrendMode = 0.
/// </remarks>
[SkipLocalsInit]
public sealed class HtTrendmode : AbstractBase
{
    private const int LOOKBACK = 63; // TA-Lib lookback for HT_TRENDMODE
    private const int SMOOTH_PRICE_SIZE = 50;
    private const int CIRC_BUFFER_SIZE = 44; // 4 * 11 for Hilbert transform
    private const int PRICE_HISTORY_SIZE = 64;

    private const double A_CONST = 0.0962;
    private const double B_CONST = 0.5769;
    private const double RAD2DEG = 180.0 / Math.PI;
    private const double DEG2RAD = Math.PI / 180.0;
    private const double CONST_DEG2RAD_BY_360 = 2.0 * Math.PI;

    // Hilbert buffer keys (matching TA-Lib layout)
    private const int KEY_DETRENDER = 6;
    private const int KEY_Q1 = 17;
    private const int KEY_JI = 28;
    private const int KEY_JQ = 39;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double PrevI2, double PrevQ2, double Re, double Im,
        double Period, double SmoothPeriod, double DCPhase, double PrevDCPhase,
        double I1ForOddPrev3, double I1ForEvenPrev3,
        double I1ForOddPrev2, double I1ForEvenPrev2,
        double PeriodWMASub, double PeriodWMASum, double TrailingWMAValue,
        double Sine, double LeadSine, double PrevSine, double PrevLeadSine,
        double Trendline, double ITrend1, double ITrend2, double ITrend3,
        int TrailingWMAIdx, int HilbertIdx, int SmoothPriceIdx,
        int DaysInTrend, double LastValidPrice, int Today, int TrendMode
    )
    {
        public State() : this(
            0, 0, 0, 0, 0.0, 0.0, 0, 0,
            0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0,
            0, 0, 0, 0,
            0, 0, 0, 0, double.NaN, 0, 0)
        { }
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

    /// <summary>
    /// Gets the current trend mode: 1 = trending, 0 = cycling.
    /// </summary>
    public int TrendMode => _state.TrendMode;

    /// <summary>
    /// Gets the current smooth period from the Hilbert Transform.
    /// </summary>
    public double SmoothPeriod => _state.SmoothPeriod;

    /// <summary>
    /// Gets the current DC Phase.
    /// </summary>
    public double DCPhase => _state.DCPhase;

    /// <summary>
    /// Gets the current trendline value.
    /// </summary>
    public double Trendline => _state.Trendline;

    /// <summary>
    /// Gets days since last SineWave crossing.
    /// </summary>
    public int DaysInTrend => _state.DaysInTrend;

    /// <summary>
    /// Gets the instantaneous period (unsmoothed dominant cycle period).
    /// </summary>
    public double InstPeriod => _state.Period;

    public override bool IsHot => _state.Today > LOOKBACK;

    public HtTrendmode()
    {
        Name = "HtTrendmode";
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

    public HtTrendmode(ITValuePublisher source) : this()
    {
        ArgumentNullException.ThrowIfNull(source);
        source.Pub += _handler;
    }

    private void Init()
    {
        _state = new State();
        _p_state = new State();
        Array.Clear(_circBuffer);
        Array.Clear(_p_circBuffer);
        Array.Clear(_smoothPrice);
        Array.Clear(_p_smoothPrice);
        Array.Clear(_priceHistory);
        Array.Clear(_p_priceHistory);
        Last = default;
    }

    public override void Reset() => Init();

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
    private double Step(double price, bool isNew)
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
        s.Today++;

        // Handle non-finite input
        if (!double.IsFinite(price))
        {
            if (double.IsNaN(s.LastValidPrice))
            {
                _state = s;
                return 0.0;
            }
            price = s.LastValidPrice;
        }
        else
        {
            s.LastValidPrice = price;
        }

        int today = s.Today - 1;

        // WMA initialization phase (first 34 + 3 bars = 37 bars for lookback)
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
                double initVal = _priceHistory[0];
                s.PeriodWMASub = initVal;
                s.PeriodWMASum = initVal;

                initVal = _priceHistory[1];
                s.PeriodWMASub += initVal;
                s.PeriodWMASum += initVal * 2.0;

                initVal = _priceHistory[2];
                s.PeriodWMASub += initVal;
                s.PeriodWMASum += initVal * 3.0;

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
            return 0.0;
        }

        // Calculate smoothed price using WMA
        double adjustedPrevPeriod = 0.075 * s.Period + 0.54;

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
            CalcHilbertEven(_circBuffer.AsSpan(), smoothedValue, ref hilbertIdx, adjustedPrevPeriod,
                s.I1ForEvenPrev3, prevQ2, prevI2, out double i1ForOddPrev3,
                ref i1ForOddPrev2, out q2, out i2);
            s.I1ForOddPrev3 = i1ForOddPrev3;
        }
        else
        {
            // Odd bar
            CalcHilbertOdd(_circBuffer.AsSpan(), smoothedValue, hilbertIdx, adjustedPrevPeriod,
                out double i1ForEvenPrev3, prevQ2, prevI2, s.I1ForOddPrev3,
                ref i1ForEvenPrev2, out q2, out i2);
            s.I1ForEvenPrev3 = i1ForEvenPrev3;
        }

        // Write back Hilbert state
        s.HilbertIdx = hilbertIdx;
        s.I1ForOddPrev2 = i1ForOddPrev2;
        s.I1ForEvenPrev2 = i1ForEvenPrev2;

        // Calculate period from Re/Im
        re = Math.FusedMultiplyAdd(0.2, (i2 * prevI2) + (q2 * prevQ2), 0.8 * re);
        im = Math.FusedMultiplyAdd(0.2, (i2 * prevQ2) - (q2 * prevI2), 0.8 * im);

        s.PrevQ2 = q2;
        s.PrevI2 = i2;
        s.Re = re;
        s.Im = im;

        double tempReal = period;
        if (Math.Abs(im) > 1e-10 && Math.Abs(re) > 1e-10)
        {
            period = 360.0 / (Math.Atan(im / re) * RAD2DEG);
        }

        double tempReal2 = 1.5 * tempReal;
        if (period > tempReal2)
        {
            period = tempReal2;
        }
        tempReal2 = 0.67 * tempReal;
        if (period < tempReal2)
        {
            period = tempReal2;
        }
        if (period < 6)
        {
            period = 6;
        }
        else if (period > 50)
        {
            period = 50;
        }
        period = (0.2 * period) + (0.8 * tempReal);

        s.Period = period;
        s.SmoothPeriod = Math.FusedMultiplyAdd(0.33, period, 0.67 * s.SmoothPeriod);

        // ==========================================
        // Compute Dominant Cycle Phase (DCPhase)
        // ==========================================
        s.PrevDCPhase = s.DCPhase;

        double dcPeriod = s.SmoothPeriod + 0.5;
        int dcPeriodInt = (int)dcPeriod;

        double realPart = 0.0;
        double imagPart = 0.0;

        // Sum over smoothPrice circular buffer
        int idx = s.SmoothPriceIdx;
        for (int i = 0; i < dcPeriodInt && i < SMOOTH_PRICE_SIZE; i++)
        {
            double angle = ((double)i * CONST_DEG2RAD_BY_360) / (double)dcPeriodInt;
            double spVal = _smoothPrice[idx];
            realPart += Math.Sin(angle) * spVal;
            imagPart += Math.Cos(angle) * spVal;
            if (idx == 0)
            {
                idx = SMOOTH_PRICE_SIZE - 1;
            }
            else
            {
                idx--;
            }
        }

        double dcPhase;
        double absImagPart = Math.Abs(imagPart);
        if (absImagPart > 0.0)
        {
            dcPhase = Math.Atan(realPart / imagPart) * RAD2DEG;
        }
        else if (absImagPart <= 0.01)
        {
            dcPhase = s.DCPhase; // Keep previous
            if (realPart < 0.0)
            {
                dcPhase -= 90.0;
            }
            else if (realPart > 0.0)
            {
                dcPhase += 90.0;
            }
        }
        else
        {
            dcPhase = s.DCPhase;
        }

        dcPhase += 90.0;
        // Compensate for one bar lag of the WMA
        dcPhase += 360.0 / s.SmoothPeriod;
        if (imagPart < 0.0)
        {
            dcPhase += 180.0;
        }
        if (dcPhase > 315.0)
        {
            dcPhase -= 360.0;
        }

        s.DCPhase = dcPhase;

        // ==========================================
        // Compute Sine and LeadSine
        // ==========================================
        s.PrevSine = s.Sine;
        s.PrevLeadSine = s.LeadSine;
        s.Sine = Math.Sin(dcPhase * DEG2RAD);
        s.LeadSine = Math.Sin((dcPhase + 45) * DEG2RAD);

        // ==========================================
        // Compute Trendline (SMA over dominant cycle smoothed by WMA)
        // ==========================================
        dcPeriod = s.SmoothPeriod + 0.5;
        dcPeriodInt = (int)dcPeriod;

        // Sum price over dcPeriodInt bars
        double sumPrice = 0.0;
        int priceIdx2 = today;
        for (int i = 0; i < dcPeriodInt && i < PRICE_HISTORY_SIZE && priceIdx2 >= 0; i++)
        {
            sumPrice += _priceHistory[priceIdx2 % PRICE_HISTORY_SIZE];
            priceIdx2--;
        }

        double smaValue = (dcPeriodInt > 0) ? sumPrice / (double)dcPeriodInt : price;

        // WMA smoothing of SMA: (4*current + 3*prev1 + 2*prev2 + prev3) / 10
        double trendline = (4.0 * smaValue + 3.0 * s.ITrend1 + 2.0 * s.ITrend2 + s.ITrend3) / 10.0;
        s.ITrend3 = s.ITrend2;
        s.ITrend2 = s.ITrend1;
        s.ITrend1 = smaValue;
        s.Trendline = trendline;

        // ==========================================
        // Compute Trend Mode (TA-Lib algorithm)
        // ==========================================
        int trend = 1; // Assume trend by default

        // Condition 1: Check for SineWave crossings
        // If sine crosses leadsine, reset daysInTrend and set to cycle mode
        if (((s.Sine > s.LeadSine) && (s.PrevSine <= s.PrevLeadSine)) ||
            ((s.Sine < s.LeadSine) && (s.PrevSine >= s.PrevLeadSine)))
        {
            s.DaysInTrend = 0;
            trend = 0;
        }

        s.DaysInTrend++;

        // Condition 2: Must be in trend for at least half the smooth period
        if (s.DaysInTrend < (0.5 * s.SmoothPeriod))
        {
            trend = 0;
        }

        // Condition 3: Phase change rate check
        // If phase change is "normal" (between 0.67× and 1.5× expected rate), it's cycle mode
        double phaseChange = s.DCPhase - s.PrevDCPhase;
        if (s.SmoothPeriod > 0.0)
        {
            double expectedPhaseChange = 360.0 / s.SmoothPeriod;
            if ((phaseChange > (0.67 * expectedPhaseChange)) && (phaseChange < (1.5 * expectedPhaseChange)))
            {
                trend = 0;
            }
        }

        // Condition 4: Price deviation from trendline
        // If price deviates ≥1.5% from trendline, it's definitely trending
        double smoothPriceNow = _smoothPrice[s.SmoothPriceIdx];
        if (Math.Abs(trendline) > 1e-10 && Math.Abs((smoothPriceNow - trendline) / trendline) >= 0.015)
        {
            trend = 1;
        }

        s.TrendMode = trend;

        // Advance smooth price index
        s.SmoothPriceIdx = (s.SmoothPriceIdx + 1) % SMOOTH_PRICE_SIZE;

        // Write back state
        _state = s;

        return s.TrendMode;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        double result = Step(input.Value, isNew);
        Last = new TValue(input.Time, result);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return new TSeries([], []);
        }

        int len = source.Count;
        var t = new System.Collections.Generic.List<long>(len);
        var v = new System.Collections.Generic.List<double>(len);

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
        long ticksStep = step?.Ticks ?? TimeSpan.FromMinutes(1).Ticks;
        long t = DateTime.UtcNow.Ticks;
        foreach (double value in source)
        {
            Update(new TValue(new DateTime(t, DateTimeKind.Utc), value));
            t += ticksStep;
        }
    }

    public static void Calculate(ReadOnlySpan<double> source, Span<double> output)
    {
        if (output.Length < source.Length)
        {
            throw new ArgumentException("output", nameof(output));
        }

        var ht = new HtTrendmode();
        for (int i = 0; i < source.Length; i++)
        {
            output[i] = ht.Update(new TValue(DateTime.UtcNow.AddTicks(i), source[i])).Value;
        }
    }

    public static TSeries Calculate(TSeries source)
    {
        var ht = new HtTrendmode();
        return ht.Update(source);
    }
}
