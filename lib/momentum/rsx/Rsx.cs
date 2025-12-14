using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// RSX: Relative Strength X (Jurik's RSI Variant)
/// </summary>
/// <remarks>
/// RSX is a noise-free version of RSI that eliminates lag and choppiness.
/// It uses a cascading IIR filter structure to achieve smoothness while preserving
/// turning points and the 0-100 range.
///
/// Key characteristics:
/// - Zero lag (compared to smoothed RSI)
/// - Ultra smooth output
/// - Bounded 0-100
///
/// Sources:
/// - https://scribd.com/document/253633684/Jurik-RSX
/// - https://www.prorealcode.com/prorealtime-indicators/jurik-rsx/
/// </remarks>
[SkipLocalsInit]
public sealed class Rsx : ITValuePublisher
{
    private readonly int _period;
    private readonly double _alpha;

    private record struct State
    {
        // Momentum filters (3 stages, 2 filters each)
        public double M1_1, M1_2;
        public double M2_1, M2_2;
        public double M3_1, M3_2;

        // Absolute Momentum filters (3 stages, 2 filters each)
        public double A1_1, A1_2;
        public double A2_1, A2_2;
        public double A3_1, A3_2;

        public double LastPrice;
        public double LastValidValue;
        public bool IsInitialized;
    }

    private State _state;
    private State _p_state;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    public event Action<TValue>? Pub;

    /// <summary>
    /// Creates RSX with specified period.
    /// </summary>
    /// <param name="period">Length of the filter (typically 8-40).</param>
    public Rsx(int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        _period = period;
        _alpha = 3.0 / (period + 2.0);
        Name = $"Rsx({period})";
    }

    public Rsx(ITValuePublisher source, int period) : this(period)
    {
        source.Pub += (item) => Update(item);
    }

    /// <summary>
    /// Current RSX value.
    /// </summary>
    public TValue Last { get; private set; }

    /// <summary>
    /// True if the indicator has processed enough data to be considered valid.
    /// </summary>
    public bool IsHot => _state.IsInitialized;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
        }
        else
        {
            _state = _p_state;
        }

        double price = input.Value;
        if (!double.IsFinite(price))
        {
            price = _state.LastValidValue;
        }
        else
        {
            _state.LastValidValue = price;
        }

        if (!_state.IsInitialized)
        {
            _state.LastPrice = price;
            _state.IsInitialized = true;
        }

        // Calculate momentum (change in price * 100)
        double momentum = (price - _state.LastPrice) * 100.0;
        
        if (isNew)
        {
             _state.LastPrice = price;
        }

        // --- Momentum Smoothing ---

        // Stage 1
        _state.M1_1 += _alpha * (momentum - _state.M1_1);
        _state.M1_2 += _alpha * (_state.M1_1 - _state.M1_2);
        double m1_out = (3.0 * _state.M1_1 - _state.M1_2) * 0.5;

        // Stage 2
        _state.M2_1 += _alpha * (m1_out - _state.M2_1);
        _state.M2_2 += _alpha * (_state.M2_1 - _state.M2_2);
        double m2_out = (3.0 * _state.M2_1 - _state.M2_2) * 0.5;

        // Stage 3
        _state.M3_1 += _alpha * (m2_out - _state.M3_1);
        _state.M3_2 += _alpha * (_state.M3_1 - _state.M3_2);
        double smoothedMomentum = (3.0 * _state.M3_1 - _state.M3_2) * 0.5;

        // --- Absolute Momentum Smoothing ---
        double absMomentum = Math.Abs(momentum);

        // Stage 1
        _state.A1_1 += _alpha * (absMomentum - _state.A1_1);
        _state.A1_2 += _alpha * (_state.A1_1 - _state.A1_2);
        double a1_out = (3.0 * _state.A1_1 - _state.A1_2) * 0.5;

        // Stage 2
        _state.A2_1 += _alpha * (a1_out - _state.A2_1);
        _state.A2_2 += _alpha * (_state.A2_1 - _state.A2_2);
        double a2_out = (3.0 * _state.A2_1 - _state.A2_2) * 0.5;

        // Stage 3
        _state.A3_1 += _alpha * (a2_out - _state.A3_1);
        _state.A3_2 += _alpha * (_state.A3_1 - _state.A3_2);
        double smoothedAbsMomentum = (3.0 * _state.A3_1 - _state.A3_2) * 0.5;

        // --- Final RSX Calculation ---
        double rsx;
        if (smoothedAbsMomentum > 1e-10)
        {
            double v4 = (smoothedMomentum / smoothedAbsMomentum + 1.0) * 50.0;
            rsx = Math.Clamp(v4, 0.0, 100.0);
        }
        else
        {
            rsx = 50.0;
        }

        Last = new TValue(input.Time, rsx);
        Pub?.Invoke(Last);
        return Last;
    }

    public TSeries Update(TSeries source)
    {
        if (source.Count == 0) return [];

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Calculate(source.Values, vSpan, _period);
        source.Times.CopyTo(tSpan);

        // Restore state by replaying the last few bars
        Reset();
        int warmup = Math.Max(0, len - 200);
        for (int i = warmup; i < len; i++)
        {
            Update(new TValue(source.Times[i], source.Values[i]), true);
        }

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    public static TSeries Calculate(TSeries source, int period)
    {
        var rsx = new Rsx(period);
        return rsx.Update(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length");
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        int len = source.Length;
        if (len == 0) return;

        double alpha = 3.0 / (period + 2.0);

        // Momentum filters
        double m1_1 = 0, m1_2 = 0;
        double m2_1 = 0, m2_2 = 0;
        double m3_1 = 0, m3_2 = 0;

        // Abs Momentum filters
        double a1_1 = 0, a1_2 = 0;
        double a2_1 = 0, a2_2 = 0;
        double a3_1 = 0, a3_2 = 0;

        double lastPrice = 0;
        bool initialized = false;
        double lastValidValue = 0;

        for (int i = 0; i < len; i++)
        {
            double price = source[i];
            if (!double.IsFinite(price))
            {
                price = lastValidValue;
            }
            else
            {
                lastValidValue = price;
            }

            if (!initialized)
            {
                lastPrice = price;
                initialized = true;
            }

            double momentum = (price - lastPrice) * 100.0;
            lastPrice = price;

            // Momentum Smoothing
            m1_1 += alpha * (momentum - m1_1);
            m1_2 += alpha * (m1_1 - m1_2);
            double m1_out = (3.0 * m1_1 - m1_2) * 0.5;

            m2_1 += alpha * (m1_out - m2_1);
            m2_2 += alpha * (m2_1 - m2_2);
            double m2_out = (3.0 * m2_1 - m2_2) * 0.5;

            m3_1 += alpha * (m2_out - m3_1);
            m3_2 += alpha * (m3_1 - m3_2);
            double smoothedMomentum = (3.0 * m3_1 - m3_2) * 0.5;

            // Abs Momentum Smoothing
            double absMomentum = Math.Abs(momentum);

            a1_1 += alpha * (absMomentum - a1_1);
            a1_2 += alpha * (a1_1 - a1_2);
            double a1_out = (3.0 * a1_1 - a1_2) * 0.5;

            a2_1 += alpha * (a1_out - a2_1);
            a2_2 += alpha * (a2_1 - a2_2);
            double a2_out = (3.0 * a2_1 - a2_2) * 0.5;

            a3_1 += alpha * (a2_out - a3_1);
            a3_2 += alpha * (a3_1 - a3_2);
            double smoothedAbsMomentum = (3.0 * a3_1 - a3_2) * 0.5;

            // Final RSX
            double rsx;
            if (smoothedAbsMomentum > 1e-10)
            {
                double v4 = (smoothedMomentum / smoothedAbsMomentum + 1.0) * 50.0;
                rsx = Math.Clamp(v4, 0.0, 100.0);
            }
            else
            {
                rsx = 50.0;
            }

            output[i] = rsx;
        }
    }

    public void Reset()
    {
        _state = default;
        _p_state = default;
        Last = default;
    }
}
