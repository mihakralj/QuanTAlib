using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// HT_DCPERIOD: Hilbert Transform Dominant Cycle Period - Estimates the period of the dominant cycle in the price data.
/// </summary>
/// <remarks>
/// The Hilbert Transform Dominant Cycle Period, developed by John Ehlers, uses the Hilbert Transform
/// to extract the dominant market cycle period. It measures the period of the phase change rate of the analytic signal.
///
/// Algorithm:
/// 1. Compute the Hilbert Transform of the detrended price to get InPhase (I) and Quadrature (Q) components.
/// 2. Determine the phase angle from I and Q.
/// 3. Measure the rate of change of the phase to derive the instantaneous period.
/// 4. Smooth the raw period using an Exponential Moving Average (EMA) and clamp values to a valid range.
///
/// Properties:
/// - Returns the period length (in bars) of the current dominant cycle.
/// - Adapts to changing market conditions.
/// - Used as a basis for other Ehlers indicators (Sinewave, Phasor).
/// </remarks>
[SkipLocalsInit]
public sealed class HtDcperiod : AbstractBase
{
    private const int LOOKBACK = 32; // TA-Lib lookback for HT_DCPERIOD
    private const int SMOOTH_PRICE_SIZE = 50;
    private const int CIRC_BUFFER_SIZE = 44; // 4 * 11 for Hilbert transform
    private const int PRICE_HISTORY_SIZE = 64;

    private const double A_CONST = 0.0962;
    private const double B_CONST = 0.5769;

    // Hilbert buffer keys (matching TA-Lib layout)
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

    public override bool IsHot => _state.Today > LOOKBACK;

    public HtDcperiod()
    {
        Name = "HtDcperiod";
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

    public HtDcperiod(ITValuePublisher source) : this()
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
    private static void CalcSmoothedPeriod(
        ref double re, double i2, double q2, ref double prevI2, ref double prevQ2, ref double im, ref double period)
    {
        const double Epsilon = 1e-12;

        re = Math.FusedMultiplyAdd(0.2, (i2 * prevI2) + (q2 * prevQ2), 0.8 * re);
        im = Math.FusedMultiplyAdd(0.2, (i2 * prevQ2) - (q2 * prevI2), 0.8 * im);

        prevQ2 = q2;
        prevI2 = i2;

        double tempReal1 = period;
        if (Math.Abs(im) > Epsilon && Math.Abs(re) > Epsilon)
        {
            double angle = Math.Atan(im / re);
            if (Math.Abs(angle) > Epsilon)
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
        s.SmoothPriceIdx = (s.SmoothPriceIdx + 1) % SMOOTH_PRICE_SIZE;

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

        // Write back state
        _state = s;

        return s.SmoothPeriod;
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

    public static void Batch(ReadOnlySpan<double> source, Span<double> output)
    {
        if (output.Length < source.Length)
        {
            throw new ArgumentException("output", nameof(output));
        }

        var ht = new HtDcperiod();
        for (int i = 0; i < source.Length; i++)
        {
            output[i] = ht.Update(new TValue(DateTime.UtcNow.AddTicks(i), source[i])).Value;
        }
    }

    public static TSeries Batch(TSeries source)
    {
        var ht = new HtDcperiod();
        return ht.Update(source);
    }

    public static (TSeries Results, HtDcperiod Indicator) Calculate(TSeries source)
    {
        var indicator = new HtDcperiod();
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}
