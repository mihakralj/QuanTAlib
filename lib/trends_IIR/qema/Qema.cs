using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// QEMA: Quad Exponential Moving Average with Progressive Alphas and Zero-Lag Weighting
/// </summary>
/// <remarks>
/// QEMA uses four cascaded EMAs with progressively increasing alphas (decreasing responsiveness)
/// and combines them using optimized weights that minimize energy while achieving zero DC lag.
///
/// Calculation:
/// 1. Base alpha: α₁ = 2 / (period + 1)
/// 2. Progressive alphas: r = (1/α₁)^(1/4), then α₂ = α₁·r, α₃ = α₂·r, α₄ = α₃·r
/// 3. Four cascaded EMAs: EMA1(input), EMA2(EMA1), EMA3(EMA2), EMA4(EMA3)
/// 4. Cumulative lags: L₁ = (1-α₁)/α₁, L₂ = L₁ + (1-α₂)/α₂, etc.
/// 5. Option A weights: Minimize energy subject to Σw=1 and Σw·L=0 (zero DC lag)
/// 6. Output: w₁·EMA1 + w₂·EMA2 + w₃·EMA3 + w₄·EMA4
///
/// O(1) update:
/// Uses four EMA state accumulators, each with O(1) update complexity.
///
/// IsHot:
/// Becomes true when the slowest EMA (stage 1) has converged to within 5% coverage.
/// </remarks>
[SkipLocalsInit]
public sealed class Qema : AbstractBase
{
    [StructLayout(LayoutKind.Auto)]
    private record struct EmaState(double Ema, double E, bool IsHot, bool IsCompensated)
    {
        public static EmaState New() => new() { Ema = 0, E = 1.0, IsHot = false, IsCompensated = false };
    }

    private readonly double _alpha1, _alpha2, _alpha3, _alpha4;
    private readonly double _decay1, _decay2, _decay3, _decay4;

    private EmaState _state1 = EmaState.New();
    private EmaState _state2 = EmaState.New();
    private EmaState _state3 = EmaState.New();
    private EmaState _state4 = EmaState.New();
    private EmaState _p_state1 = EmaState.New();
    private EmaState _p_state2 = EmaState.New();
    private EmaState _p_state3 = EmaState.New();
    private EmaState _p_state4 = EmaState.New();
    private readonly TValuePublishedHandler _handler;

    private double _lastValidValue;
    private double _p_lastValidValue;

    private const double COVERAGE_THRESHOLD = 0.05;
    private const double COMPENSATOR_THRESHOLD = 1e-10;
    private const double DEGENERATE_THRESHOLD = 1e-12;

    /// <summary>
    /// True when the slowest EMA (stage 1) has warmed up and is providing valid results.
    /// </summary>
    public override bool IsHot => _state1.E <= COVERAGE_THRESHOLD;

    /// <summary>
    /// Creates QEMA with specified period.
    /// Alpha1 = 2 / (period + 1), with progressive alphas ramped geometrically.
    /// </summary>
    /// <param name="period">Period for base EMA calculation (must be > 0)</param>
    public Qema(int period)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);

        _alpha1 = Clamp01(2.0 / (period + 1));

        // Progressive alpha ramp: r = (1/α₁)^(1/4) → α₂=α₁^(3/4), α₃=α₁^(1/2), α₄=α₁^(1/4)
        double r = Math.Pow(1.0 / _alpha1, 0.25);
        _alpha2 = Clamp01(_alpha1 * r);
        _alpha3 = Clamp01(_alpha2 * r);
        _alpha4 = Clamp01(_alpha3 * r);

        _decay1 = 1.0 - _alpha1;
        _decay2 = 1.0 - _alpha2;
        _decay3 = 1.0 - _alpha3;
        _decay4 = 1.0 - _alpha4;

        Name = $"Qema({period})";
        WarmupPeriod = period;
        _handler = Handle;
    }

    /// <summary>
    /// Creates QEMA with specified source and period.
    /// Subscribes to source.Pub event.
    /// </summary>
    /// <param name="source">Source to subscribe to</param>
    /// <param name="period">Period for base EMA calculation</param>
    public Qema(ITValuePublisher source, int period) : this(period)
    {
        source.Pub += _handler;
    }

    /// <summary>
    /// Creates QEMA with specified source TSeries and period.
    /// Primes with historical data and subscribes to updates.
    /// </summary>
    /// <param name="source">Source TSeries</param>
    /// <param name="period">Period for base EMA calculation</param>
    public Qema(TSeries source, int period) : this(period)
    {
        Prime(source.Values);
        if (source.Count > 0)
        {
            Last = new TValue(source.LastTime, Last.Value);
        }
        source.Pub += _handler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Clamp01(double x) => Math.Min(1.0, Math.Max(x, DEGENERATE_THRESHOLD));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Lag(double alpha) => (1.0 - alpha) / alpha;

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// Initializes the indicator state using the provided history.
    /// </summary>
    /// <param name="source">Historical data</param>
    /// <param name="step">Optional time step (not used)</param>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        if (source.Length == 0) return;

        // Reset state
        _state1 = EmaState.New();
        _state2 = EmaState.New();
        _state3 = EmaState.New();
        _state4 = EmaState.New();
        _p_state1 = EmaState.New();
        _p_state2 = EmaState.New();
        _p_state3 = EmaState.New();
        _p_state4 = EmaState.New();
        _lastValidValue = 0;
        _p_lastValidValue = 0;

        int len = source.Length;
        double lastValid = 0;

        // Find first finite value
        for (int i = 0; i < len; i++)
        {
            if (double.IsFinite(source[i]))
            {
                lastValid = source[i];
                break;
            }
        }

        EmaState s1 = _state1;
        EmaState s2 = _state2;
        EmaState s3 = _state3;
        EmaState s4 = _state4;

        for (int i = 0; i < len; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
                lastValid = val;
            else
                val = lastValid;

            double e1 = ComputeEma(val, _alpha1, _decay1, ref s1);
            double e2 = ComputeEma(e1, _alpha2, _decay2, ref s2);
            double e3 = ComputeEma(e2, _alpha3, _decay3, ref s3);
            ComputeEma(e3, _alpha4, _decay4, ref s4);
        }

        _state1 = s1;
        _state2 = s2;
        _state3 = s3;
        _state4 = s4;
        _lastValidValue = lastValid;

        // Calculate final output
        double e1_final = GetCompensated(_state1);
        double e2_final = GetCompensated(_state2);
        double e3_final = GetCompensated(_state3);
        double e4_final = GetCompensated(_state4);

        var (w1, w2, w3, w4) = ComputeWeights(_alpha1, _alpha2, _alpha3, _alpha4);
        double result = Math.FusedMultiplyAdd(w1, e1_final, Math.FusedMultiplyAdd(w2, e2_final, Math.FusedMultiplyAdd(w3, e3_final, w4 * e4_final)));

        Last = new TValue(DateTime.MinValue, result);

        _p_state1 = _state1;
        _p_state2 = _state2;
        _p_state3 = _state3;
        _p_state4 = _state4;
        _p_lastValidValue = _lastValidValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double GetCompensated(EmaState s)
    {
        if (s.IsCompensated) return s.Ema;
        return s.Ema / (1.0 - s.E);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state1 = _state1;
            _p_state2 = _state2;
            _p_state3 = _state3;
            _p_state4 = _state4;
            _p_lastValidValue = _lastValidValue;
        }
        else
        {
            _state1 = _p_state1;
            _state2 = _p_state2;
            _state3 = _p_state3;
            _state4 = _p_state4;
            _lastValidValue = _p_lastValidValue;
        }

        double val = input.Value;
        if (double.IsFinite(val))
            _lastValidValue = val;
        else
            val = _lastValidValue;

        // Cascaded EMAs
        double e1 = ComputeEma(val, _alpha1, _decay1, ref _state1);
        double e2 = ComputeEma(e1, _alpha2, _decay2, ref _state2);
        double e3 = ComputeEma(e2, _alpha3, _decay3, ref _state3);
        double e4 = ComputeEma(e3, _alpha4, _decay4, ref _state4);

        // Compute weights and combine
        var (w1, w2, w3, w4) = ComputeWeights(_alpha1, _alpha2, _alpha3, _alpha4);
        double result = Math.FusedMultiplyAdd(w1, e1, Math.FusedMultiplyAdd(w2, e2, Math.FusedMultiplyAdd(w3, e3, w4 * e4)));

        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0) return [];

        int len = source.Count;
        List<long> t = new(len);
        List<double> v = new(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);
        source.Times.CopyTo(tSpan);

        var sourceValues = source.Values;

        EmaState s1 = _state1;
        EmaState s2 = _state2;
        EmaState s3 = _state3;
        EmaState s4 = _state4;
        double lastValid = _lastValidValue;

        var (w1, w2, w3, w4) = ComputeWeights(_alpha1, _alpha2, _alpha3, _alpha4);

        for (int i = 0; i < len; i++)
        {
            double val = sourceValues[i];
            if (double.IsFinite(val))
                lastValid = val;
            else
                val = lastValid;

            double e1 = ComputeEma(val, _alpha1, _decay1, ref s1);
            double e2 = ComputeEma(e1, _alpha2, _decay2, ref s2);
            double e3 = ComputeEma(e2, _alpha3, _decay3, ref s3);
            double e4 = ComputeEma(e3, _alpha4, _decay4, ref s4);

            vSpan[i] = Math.FusedMultiplyAdd(w1, e1, Math.FusedMultiplyAdd(w2, e2, Math.FusedMultiplyAdd(w3, e3, w4 * e4)));
        }

        _state1 = s1;
        _state2 = s2;
        _state3 = s3;
        _state4 = s4;
        _p_state1 = s1;
        _p_state2 = s2;
        _p_state3 = s3;
        _p_state4 = s4;
        _lastValidValue = lastValid;
        _p_lastValidValue = lastValid;

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    /// <summary>
    /// Computes the bias-corrected EMA value and updates state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ComputeEma(double input, double alpha, double decay, ref EmaState state)
    {
        state.Ema = Math.FusedMultiplyAdd(state.Ema, decay, alpha * input);

        double result;
        if (!state.IsCompensated)
        {
            state.E *= decay;

            if (!state.IsHot && state.E <= COVERAGE_THRESHOLD)
                state.IsHot = true;

            if (state.E <= COMPENSATOR_THRESHOLD)
            {
                state.IsCompensated = true;
                result = state.Ema;
            }
            else
            {
                result = state.Ema / (1.0 - state.E);
            }
        }
        else
        {
            result = state.Ema;
        }

        return result;
    }

    /// <summary>
    /// Computes Option A weights for minimum energy with zero DC lag constraint.
    /// Solves: Σw = 1, Σw·L = 0 (δ=0 for zero lag)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (double w1, double w2, double w3, double w4) ComputeWeights(double a1, double a2, double a3, double a4)
    {
        double t1 = Lag(a1);
        double t2 = Lag(a2);
        double t3 = Lag(a3);
        double t4 = Lag(a4);

        // Cumulative lags
        double L1 = t1;
        double L2 = t1 + t2;
        double L3 = L2 + t3;
        double L4 = L3 + t4;

        // Option A: min-energy with constraints Σw=1, Σw·L=δ (δ=0)
        double B = L1 + L2 + L3 + L4;
        double C = Math.FusedMultiplyAdd(L1, L1, Math.FusedMultiplyAdd(L2, L2, Math.FusedMultiplyAdd(L3, L3, L4 * L4)));
        double D = Math.FusedMultiplyAdd(4.0, C, -B * B);

        double w1, w2, w3, w4;
        if (Math.Abs(D) < DEGENERATE_THRESHOLD)
        {
            // Degenerate case (e.g., alpha=1 → all L=0): output is EMA1≈input
            w1 = 1.0;
            w2 = 0.0;
            w3 = 0.0;
            w4 = 0.0;
        }
        else
        {
            double lambda = C / D;
            double mu = -B / D;
            w1 = Math.FusedMultiplyAdd(mu, L1, lambda);
            w2 = Math.FusedMultiplyAdd(mu, L2, lambda);
            w3 = Math.FusedMultiplyAdd(mu, L3, lambda);
            w4 = Math.FusedMultiplyAdd(mu, L4, lambda);
        }

        return (w1, w2, w3, w4);
    }

    /// <summary>
    /// Calculates QEMA for the entire series using a new instance.
    /// </summary>
    /// <param name="source">Input series</param>
    /// <param name="period">QEMA period</param>
    /// <returns>QEMA series</returns>
    public static TSeries Batch(TSeries source, int period)
    {
        var qema = new Qema(period);
        return qema.Update(source);
    }

    /// <summary>
    /// Calculates QEMA in-place using period, writing results to pre-allocated output span.
    /// Zero-allocation method for maximum performance.
    /// </summary>
    /// <param name="source">Input values</param>
    /// <param name="output">Output span (must be same length as source)</param>
    /// <param name="period">QEMA period (must be > 0)</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);

        if (source.Length == 0) return;

        double alpha1 = Clamp01(2.0 / (period + 1));
        double r = Math.Pow(1.0 / alpha1, 0.25);
        double alpha2 = Clamp01(alpha1 * r);
        double alpha3 = Clamp01(alpha2 * r);
        double alpha4 = Clamp01(alpha3 * r);

        double decay1 = 1.0 - alpha1;
        double decay2 = 1.0 - alpha2;
        double decay3 = 1.0 - alpha3;
        double decay4 = 1.0 - alpha4;

        double lastValid = 0;

        // Find first finite value
        for (int i = 0; i < source.Length; i++)
        {
            if (double.IsFinite(source[i]))
            {
                lastValid = source[i];
                break;
            }
        }

        // EMA states
        double ema1_val = 0, ema1_e = 1.0;
        bool ema1_compensated = false;
        double ema2_val = 0, ema2_e = 1.0;
        bool ema2_compensated = false;
        double ema3_val = 0, ema3_e = 1.0;
        bool ema3_compensated = false;
        double ema4_val = 0, ema4_e = 1.0;
        bool ema4_compensated = false;

        var (w1, w2, w3, w4) = ComputeWeights(alpha1, alpha2, alpha3, alpha4);

        for (int i = 0; i < source.Length; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
                lastValid = val;
            else
                val = lastValid;

            // EMA1
            ema1_val = Math.FusedMultiplyAdd(ema1_val, decay1, alpha1 * val);
            double e1;
            if (!ema1_compensated)
            {
                ema1_e *= decay1;
                if (ema1_e <= COMPENSATOR_THRESHOLD)
                {
                    ema1_compensated = true;
                    e1 = ema1_val;
                }
                else
                {
                    e1 = ema1_val / (1.0 - ema1_e);
                }
            }
            else
            {
                e1 = ema1_val;
            }

            // EMA2
            ema2_val = Math.FusedMultiplyAdd(ema2_val, decay2, alpha2 * e1);
            double e2;
            if (!ema2_compensated)
            {
                ema2_e *= decay2;
                if (ema2_e <= COMPENSATOR_THRESHOLD)
                {
                    ema2_compensated = true;
                    e2 = ema2_val;
                }
                else
                {
                    e2 = ema2_val / (1.0 - ema2_e);
                }
            }
            else
            {
                e2 = ema2_val;
            }

            // EMA3
            ema3_val = Math.FusedMultiplyAdd(ema3_val, decay3, alpha3 * e2);
            double e3;
            if (!ema3_compensated)
            {
                ema3_e *= decay3;
                if (ema3_e <= COMPENSATOR_THRESHOLD)
                {
                    ema3_compensated = true;
                    e3 = ema3_val;
                }
                else
                {
                    e3 = ema3_val / (1.0 - ema3_e);
                }
            }
            else
            {
                e3 = ema3_val;
            }

            // EMA4
            ema4_val = Math.FusedMultiplyAdd(ema4_val, decay4, alpha4 * e3);
            double e4;
            if (!ema4_compensated)
            {
                ema4_e *= decay4;
                if (ema4_e <= COMPENSATOR_THRESHOLD)
                {
                    ema4_compensated = true;
                    e4 = ema4_val;
                }
                else
                {
                    e4 = ema4_val / (1.0 - ema4_e);
                }
            }
            else
            {
                e4 = ema4_val;
            }

            output[i] = Math.FusedMultiplyAdd(w1, e1, Math.FusedMultiplyAdd(w2, e2, Math.FusedMultiplyAdd(w3, e3, w4 * e4)));
        }
    }

    /// <summary>
    /// Resets the QEMA state.
    /// </summary>
    public override void Reset()
    {
        _state1 = EmaState.New();
        _state2 = EmaState.New();
        _state3 = EmaState.New();
        _state4 = EmaState.New();
        _p_state1 = EmaState.New();
        _p_state2 = EmaState.New();
        _p_state3 = EmaState.New();
        _p_state4 = EmaState.New();
        _lastValidValue = 0;
        _p_lastValidValue = 0;
        Last = default;
    }
}
