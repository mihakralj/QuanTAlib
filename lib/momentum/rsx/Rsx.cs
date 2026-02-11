using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// RSX: Jurik Relative Strength Index (Jurik's RSI Variant)
/// </summary>
/// <remarks>
/// Noise-free RSI using cascading IIR filters for zero-lag, ultra-smooth output [0-100].
/// Preserves turning points while eliminating choppiness.
///
/// Calculation: Triple-cascaded momentum/abs-momentum smoothing → <c>RSX = (ratio + 1) × 50</c>.
/// </remarks>
/// <seealso href="Rsx.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Rsx : ITValuePublisher
{
    private readonly int _period;
    private readonly double _alpha;
    private readonly double _decay;

    [StructLayout(LayoutKind.Auto)]
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
    private readonly TValuePublishedHandler _handler;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// The number of bars required to warm up the indicator.
    /// </summary>
    public int WarmupPeriod { get; }

    /// <summary>
    /// Creates RSX with specified period.
    /// </summary>
    /// <param name="period">Length of the filter (typically 8-40).</param>
    public Rsx(int period)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _period = period;
        WarmupPeriod = period;
        _alpha = 3.0 / (period + 2.0);
        _decay = 1.0 - _alpha;
        Name = $"Rsx({period})";
        _handler = Handle;
    }

    public Rsx(ITValuePublisher source, int period) : this(period)
    {
        source.Pub += _handler;
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
    private void Handle(object? sender, in TValueEventArgs args) => Update(args.Value, args.IsNew);

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
        else if (isNew)
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

        // --- Momentum Smoothing (using FMA for precision and performance) ---
        // EMA update: new = old + alpha * (input - old) = old * (1-alpha) + alpha * input = old * decay + alpha * input
        double m1_1 = Math.FusedMultiplyAdd(_state.M1_1, _decay, _alpha * momentum);
        double m1_2 = Math.FusedMultiplyAdd(_state.M1_2, _decay, _alpha * m1_1);
        double m1_out = Math.FusedMultiplyAdd(3.0, m1_1, -m1_2) * 0.5;

        double m2_1 = Math.FusedMultiplyAdd(_state.M2_1, _decay, _alpha * m1_out);
        double m2_2 = Math.FusedMultiplyAdd(_state.M2_2, _decay, _alpha * m2_1);
        double m2_out = Math.FusedMultiplyAdd(3.0, m2_1, -m2_2) * 0.5;

        double m3_1 = Math.FusedMultiplyAdd(_state.M3_1, _decay, _alpha * m2_out);
        double m3_2 = Math.FusedMultiplyAdd(_state.M3_2, _decay, _alpha * m3_1);
        double smoothedMomentum = Math.FusedMultiplyAdd(3.0, m3_1, -m3_2) * 0.5;

        // --- Absolute Momentum Smoothing (using FMA) ---
        double absMomentum = Math.Abs(momentum);

        double a1_1 = Math.FusedMultiplyAdd(_state.A1_1, _decay, _alpha * absMomentum);
        double a1_2 = Math.FusedMultiplyAdd(_state.A1_2, _decay, _alpha * a1_1);
        double a1_out = Math.FusedMultiplyAdd(3.0, a1_1, -a1_2) * 0.5;

        double a2_1 = Math.FusedMultiplyAdd(_state.A2_1, _decay, _alpha * a1_out);
        double a2_2 = Math.FusedMultiplyAdd(_state.A2_2, _decay, _alpha * a2_1);
        double a2_out = Math.FusedMultiplyAdd(3.0, a2_1, -a2_2) * 0.5;

        double a3_1 = Math.FusedMultiplyAdd(_state.A3_1, _decay, _alpha * a2_out);
        double a3_2 = Math.FusedMultiplyAdd(_state.A3_2, _decay, _alpha * a3_1);
        double smoothedAbsMomentum = Math.FusedMultiplyAdd(3.0, a3_1, -a3_2) * 0.5;

        if (isNew)
        {
            _state.M1_1 = m1_1; _state.M1_2 = m1_2;
            _state.M2_1 = m2_1; _state.M2_2 = m2_2;
            _state.M3_1 = m3_1; _state.M3_2 = m3_2;

            _state.A1_1 = a1_1; _state.A1_2 = a1_2;
            _state.A2_1 = a2_1; _state.A2_2 = a2_2;
            _state.A3_1 = a3_1; _state.A3_2 = a3_2;
        }

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
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    public TSeries Update(TSeries source)
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

        Batch(source.Values, vSpan, _period);
        source.Times.CopyTo(tSpan);

        // Restore state by replaying the last few bars (use WarmupPeriod instead of hardcoded 200)
        Reset();
        int warmup = Math.Max(0, len - WarmupPeriod);
        for (int i = warmup; i < len; i++)
        {
            Update(new TValue(source.Times[i], source.Values[i]), isNew: true);
        }

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    public static TSeries Batch(TSeries source, int period)
    {
        var rsx = new Rsx(period);
        return rsx.Update(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        double alpha = 3.0 / (period + 2.0);
        double decay = 1.0 - alpha;

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

            // Momentum Smoothing (using FMA for precision and performance)
            m1_1 = Math.FusedMultiplyAdd(m1_1, decay, alpha * momentum);
            m1_2 = Math.FusedMultiplyAdd(m1_2, decay, alpha * m1_1);
            double m1_out = Math.FusedMultiplyAdd(3.0, m1_1, -m1_2) * 0.5;

            m2_1 = Math.FusedMultiplyAdd(m2_1, decay, alpha * m1_out);
            m2_2 = Math.FusedMultiplyAdd(m2_2, decay, alpha * m2_1);
            double m2_out = Math.FusedMultiplyAdd(3.0, m2_1, -m2_2) * 0.5;

            m3_1 = Math.FusedMultiplyAdd(m3_1, decay, alpha * m2_out);
            m3_2 = Math.FusedMultiplyAdd(m3_2, decay, alpha * m3_1);
            double smoothedMomentum = Math.FusedMultiplyAdd(3.0, m3_1, -m3_2) * 0.5;

            // Abs Momentum Smoothing (using FMA)
            double absMomentum = Math.Abs(momentum);

            a1_1 = Math.FusedMultiplyAdd(a1_1, decay, alpha * absMomentum);
            a1_2 = Math.FusedMultiplyAdd(a1_2, decay, alpha * a1_1);
            double a1_out = Math.FusedMultiplyAdd(3.0, a1_1, -a1_2) * 0.5;

            a2_1 = Math.FusedMultiplyAdd(a2_1, decay, alpha * a1_out);
            a2_2 = Math.FusedMultiplyAdd(a2_2, decay, alpha * a2_1);
            double a2_out = Math.FusedMultiplyAdd(3.0, a2_1, -a2_2) * 0.5;

            a3_1 = Math.FusedMultiplyAdd(a3_1, decay, alpha * a2_out);
            a3_2 = Math.FusedMultiplyAdd(a3_2, decay, alpha * a3_1);
            double smoothedAbsMomentum = Math.FusedMultiplyAdd(3.0, a3_1, -a3_2) * 0.5;

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

    public static (TSeries Results, Rsx Indicator) Calculate(TSeries source, int period)
    {
        var indicator = new Rsx(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    public void Reset()
    {
        _state = default;
        _p_state = default;
        Last = default;
    }
}