using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// TEMA: Triple Exponential Moving Average
/// </summary>
/// <remarks>
/// TEMA uses triple smoothing to reduce lag even further than DEMA.
///
/// Calculation:
/// EMA1 = EMA(input)
/// EMA2 = EMA(EMA1)
/// EMA3 = EMA(EMA2)
/// TEMA = 3 * EMA1 - 3 * EMA2 + EMA3
///
/// O(1) update:
/// Uses three EMA instances, each with O(1) update complexity.
///
/// IsHot:
/// Becomes true when the TEMA step response converges to within 5% error.
/// This happens when the third EMA's error factor drops below ~9% (approx 2.43/alpha steps),
/// which is faster than the standard EMA convergence (3/alpha steps).
/// </remarks>
[SkipLocalsInit]
public sealed class Tema : AbstractBase
{
    [StructLayout(LayoutKind.Auto)]
    private record struct EmaState(double Ema, double E, bool IsHot, bool IsCompensated)
    {
        public static EmaState New() => new() { Ema = 0, E = 1.0, IsHot = false, IsCompensated = false };
    }

    private readonly double _alpha;
    private readonly double _decay;

    private EmaState _state1 = EmaState.New();
    private EmaState _state2 = EmaState.New();
    private EmaState _state3 = EmaState.New();
    private EmaState _p_state1 = EmaState.New();
    private EmaState _p_state2 = EmaState.New();
    private EmaState _p_state3 = EmaState.New();
    private readonly TValuePublishedHandler _handler;

    private double _lastValidValue;
    private double _p_lastValidValue;

    public override bool IsHot => _state3.E <= 0.09;

    public Tema(int period)
    {
        if (period <= 0) throw new ArgumentException("Period must be greater than 0", nameof(period));

        _alpha = 2.0 / (period + 1);
        _decay = 1.0 - _alpha;
        Name = $"Tema({period})";
        WarmupPeriod = period * 3;
        _handler = Handle;
    }

    public Tema(ITValuePublisher source, int period) : this(period)
    {
        source.Pub += _handler;
    }

    public Tema(TSeries source, int period) : this(period)
    {
        Prime(source.Values);
        if (source.Count > 0)
        {
            Last = new TValue(source.LastTime, Last.Value);
        }
        source.Pub += _handler;
    }

    public Tema(double alpha)
    {
        if (alpha <= 0 || alpha >= 1) throw new ArgumentException("Alpha must be strictly between 0 and 1", nameof(alpha));

        _alpha = alpha;
        _decay = 1.0 - alpha;
        Name = $"Tema(α={alpha:F4})";
        WarmupPeriod = (int)(3 * (2.0 / alpha - 1.0));
        _handler = Handle;
    }

    private void Handle(object? sender, TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// Initializes the indicator state using the provided history.
    /// </summary>
    /// <param name="source">Historical data</param>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        if (source.Length == 0) return;

        // Reset state
        _state1 = EmaState.New();
        _state2 = EmaState.New();
        _state3 = EmaState.New();
        _p_state1 = EmaState.New();
        _p_state2 = EmaState.New();
        _p_state3 = EmaState.New();
        _lastValidValue = 0;
        _p_lastValidValue = 0;

        // Run the calculation on the history to update state
        // We don't need the output, just the final state
        int len = source.Length;
        double lastValid = 0;

        // Search for the first finite value to initialize lastValid
        // If no finite value is found, lastValid remains 0
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
        double alpha = _alpha;
        double decay = _decay;

        for (int i = 0; i < len; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
                lastValid = val;
            else
                val = lastValid;

            double e1 = Compute(val, alpha, decay, ref s1);
            double e2 = Compute(e1, alpha, decay, ref s2);
            Compute(e2, alpha, decay, ref s3);
        }

        _state1 = s1;
        _state2 = s2;
        _state3 = s3;
        _lastValidValue = lastValid;

        // Calculate the initial "Last" value
        // We need to re-compute the last step to get the result
        // But Compute updates state, so we can't just call it again without side effects if we pass ref state.
        // However, we can calculate the result from the current state.
        // TEMA = 3 * EMA1 - 3 * EMA2 + EMA3
        // The state contains the updated EMA values (Ema field).
        // But wait, Compute returns the *compensated* value.
        // The state.Ema is the raw EMA value.
        // We need to apply compensation logic to get the correct E1, E2, E3.

        double GetCompensated(EmaState s)
        {
            if (s.IsCompensated) return s.Ema;
            return s.Ema / (1.0 - s.E);
        }

        double e1_final = GetCompensated(_state1);
        double e2_final = GetCompensated(_state2);
        double e3_final = GetCompensated(_state3);
        double result = 3 * e1_final - 3 * e2_final + e3_final;

        Last = new TValue(DateTime.MinValue, result);

        _p_state1 = _state1;
        _p_state2 = _state2;
        _p_state3 = _state3;
        _p_lastValidValue = _lastValidValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state1 = _state1;
            _p_state2 = _state2;
            _p_state3 = _state3;
            _p_lastValidValue = _lastValidValue;
        }
        else
        {
            _state1 = _p_state1;
            _state2 = _p_state2;
            _state3 = _p_state3;
            _lastValidValue = _p_lastValidValue;
        }

        // EMA1
        double val = input.Value;
        if (double.IsFinite(val))
            _lastValidValue = val;
        else
            val = _lastValidValue;

        double e1 = Compute(val, _alpha, _decay, ref _state1);

        // EMA2 (input is e1)
        double e2 = Compute(e1, _alpha, _decay, ref _state2);

        // EMA3 (input is e2)
        double e3 = Compute(e2, _alpha, _decay, ref _state3);

        double result = 3 * e1 - 3 * e2 + e3;
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

        // Use current state
        EmaState s1 = _state1;
        EmaState s2 = _state2;
        EmaState s3 = _state3;
        double lastValid = _lastValidValue;
        double alpha = _alpha;
        double decay = _decay;

        for (int i = 0; i < len; i++)
        {
            double val = sourceValues[i];
            if (double.IsFinite(val))
                lastValid = val;
            else
                val = lastValid;

            double e1 = Compute(val, alpha, decay, ref s1);
            double e2 = Compute(e1, alpha, decay, ref s2);
            double e3 = Compute(e2, alpha, decay, ref s3);

            vSpan[i] = 3 * e1 - 3 * e2 + e3;
        }

        // Update instance state
        _state1 = s1;
        _state2 = s2;
        _state3 = s3;
        _p_state1 = s1;
        _p_state2 = s2;
        _p_state3 = s3;
        _lastValidValue = lastValid;
        _p_lastValidValue = lastValid;

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Compute(double input, double alpha, double decay, ref EmaState state)
    {
        state.Ema += alpha * (input - state.Ema);

        double result;
        if (!state.IsCompensated)
        {
            state.E *= decay;

            if (!state.IsHot && state.E <= 0.05) // COVERAGE_THRESHOLD
                state.IsHot = true;

            if (state.E <= 1e-10) // COMPENSATOR_THRESHOLD
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

    public static TSeries Batch(TSeries source, int period)
    {
        var tema = new Tema(period);
        return tema.Update(source);
    }

    public static TSeries Batch(TSeries source, double alpha)
    {
        var tema = new Tema(alpha);
        return tema.Update(source);
    }

    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        double alpha = 2.0 / (period + 1);
        Batch(source, output, alpha);
    }

    public static void Batch(ReadOnlySpan<double> source, Span<double> output, double alpha)
    {
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        if (alpha <= 0 || alpha >= 1)
            throw new ArgumentException("Alpha must be strictly between 0 and 1", nameof(alpha));

        if (source.Length == 0) return;

        double decay = 1.0 - alpha;
        double lastValid = 0;

        // Search for the first finite value to initialize lastValid
        for (int i = 0; i < source.Length; i++)
        {
            if (double.IsFinite(source[i]))
            {
                lastValid = source[i];
                break;
            }
        }

        // State for EMA1
        double ema1_val = 0;
        double ema1_e = 1.0;
        bool ema1_isCompensated = false;

        // State for EMA2
        double ema2_val = 0;
        double ema2_e = 1.0;
        bool ema2_isCompensated = false;

        // State for EMA3
        double ema3_val = 0;
        double ema3_e = 1.0;
        bool ema3_isCompensated = false;

        for (int i = 0; i < source.Length; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
                lastValid = val;
            else
                val = lastValid;

            // Update EMA1
            ema1_val += alpha * (val - ema1_val);
            double e1;
            if (!ema1_isCompensated)
            {
                ema1_e *= decay;
                if (ema1_e <= 1e-10)
                {
                    ema1_isCompensated = true;
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

            // Update EMA2 (input is e1)
            ema2_val += alpha * (e1 - ema2_val);
            double e2;
            if (!ema2_isCompensated)
            {
                ema2_e *= decay;
                if (ema2_e <= 1e-10)
                {
                    ema2_isCompensated = true;
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

            // Update EMA3 (input is e2)
            ema3_val += alpha * (e2 - ema3_val);
            double e3;
            if (!ema3_isCompensated)
            {
                ema3_e *= decay;
                if (ema3_e <= 1e-10)
                {
                    ema3_isCompensated = true;
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

            // TEMA = 3 * EMA1 - 3 * EMA2 + EMA3
            output[i] = 3 * e1 - 3 * e2 + e3;
        }
    }

    public override void Reset()
    {
        _state1 = EmaState.New();
        _state2 = EmaState.New();
        _state3 = EmaState.New();
        _p_state1 = EmaState.New();
        _p_state2 = EmaState.New();
        _p_state3 = EmaState.New();
        _lastValidValue = 0;
        _p_lastValidValue = 0;
        Last = default;
    }
}
