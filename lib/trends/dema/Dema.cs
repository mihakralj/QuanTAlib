using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// DEMA: Double Exponential Moving Average
/// </summary>
/// <remarks>
/// DEMA reduces the lag of traditional EMA by subtracting the lag from the original EMA.
///
/// Calculation:
/// EMA1 = EMA(input)
/// EMA2 = EMA(EMA1)
/// DEMA = 2 * EMA1 - EMA2
///
/// O(1) update:
/// Uses two EMA instances, each with O(1) update complexity.
///
/// IsHot:
/// Becomes true when the second EMA converges (approx. 2x EMA convergence time).
/// </remarks>
[SkipLocalsInit]
public sealed class Dema : AbstractBase, IDisposable
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
    private EmaState _p_state1 = EmaState.New();
    private EmaState _p_state2 = EmaState.New();

    private double _lastValidValue = double.NaN;
    private double _p_lastValidValue = double.NaN;
    private readonly ITValuePublisher? _publisher;
    private readonly TValuePublishedHandler? _listener;

    public override bool IsHot => _state2.IsHot;

    public Dema(int period)
    {
        if (period <= 0) throw new ArgumentException("Period must be greater than 0", nameof(period));

        _alpha = 2.0 / (period + 1);
        _decay = 1.0 - _alpha;
        Name = $"Dema({period})";
        WarmupPeriod = period;
    }

    public Dema(ITValuePublisher source, int period) : this(period)
    {
        _publisher = source;
        _listener = Handle;
        source.Pub += _listener;
    }

    public Dema(double alpha)
    {
        if (alpha <= 0 || alpha > 1) throw new ArgumentException("Alpha must be between 0 and 1", nameof(alpha));

        _alpha = alpha;
        _decay = 1.0 - alpha;
        Name = $"Dema(α={alpha:F4})";
        WarmupPeriod = (int)((2.0 / alpha) - 1.0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state1 = _state1;
            _p_state2 = _state2;
            _p_lastValidValue = _lastValidValue;
        }
        else
        {
            _state1 = _p_state1;
            _state2 = _p_state2;
            _lastValidValue = _p_lastValidValue;
        }

        // EMA1
        double val = input.Value;
        if (double.IsFinite(val))
            _lastValidValue = val;
        else
            val = _lastValidValue;

        if (double.IsNaN(val))
        {
            Last = new TValue(input.Time, double.NaN);
            PubEvent(Last);
            return Last;
        }

        double e1 = Compute(val, _alpha, _decay, ref _state1);

        // EMA2 (input is e1, which is always valid)
        double e2 = Compute(e1, _alpha, _decay, ref _state2);

        double result = 2 * e1 - e2;
        Last = new TValue(input.Time, result);
        PubEvent(Last);
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

        // Capture pre-batch state for rollback
        EmaState preBatch_s1 = _state1;
        EmaState preBatch_s2 = _state2;
        double preBatch_lastValid = _lastValidValue;

        // Use current state for calculation
        EmaState s1 = _state1;
        EmaState s2 = _state2;
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

            if (double.IsNaN(val))
            {
                vSpan[i] = double.NaN;
                continue;
            }

            double e1 = Compute(val, alpha, decay, ref s1);
            double e2 = Compute(e1, alpha, decay, ref s2);

            vSpan[i] = 2 * e1 - e2;
        }

        // Update instance state with post-batch values
        _state1 = s1;
        _state2 = s2;
        _lastValidValue = lastValid;

        // Preserve pre-batch state for rollback (isNew=false)
        _p_state1 = preBatch_s1;
        _p_state2 = preBatch_s2;
        _p_lastValidValue = preBatch_lastValid;

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (var value in source)
        {
            Update(new TValue(DateTime.MinValue, value));
        }
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

    public static TSeries Calculate(TSeries source, int period)
    {
        var dema = new Dema(period);
        return dema.Update(source);
    }

    public static TSeries Calculate(TSeries source, double alpha)
    {
        var dema = new Dema(alpha);
        return dema.Update(source);
    }

    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0", nameof(period));

        double alpha = 2.0 / (period + 1);
        Calculate(source, output, alpha);
    }

    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, double alpha)
    {
        if (source.Length != output.Length)
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        if (alpha <= 0 || alpha > 1)
            throw new ArgumentException("Alpha must be between 0 and 1", nameof(alpha));

        if (source.Length == 0) return;

        double decay = 1.0 - alpha;
        double lastValid = double.NaN;

        // State for EMA1
        double ema1_val = 0;
        double ema1_e = 1.0;
        bool ema1_isCompensated = false;

        // State for EMA2
        double ema2_val = 0;
        double ema2_e = 1.0;
        bool ema2_isCompensated = false;

        for (int i = 0; i < source.Length; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
                lastValid = val;
            else
                val = lastValid;

            if (double.IsNaN(val))
            {
                output[i] = double.NaN;
                continue;
            }

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

            // DEMA = 2 * EMA1 - EMA2
            output[i] = 2 * e1 - e2;
        }
    }

    public override void Reset()
    {
        _state1 = EmaState.New();
        _state2 = EmaState.New();
        _p_state1 = EmaState.New();
        _p_state2 = EmaState.New();
        _lastValidValue = double.NaN;
        _p_lastValidValue = double.NaN;
        Last = default;
    }

    public void Dispose()
    {
        if (_publisher != null && _listener != null)
        {
            _publisher.Pub -= _listener;
        }
    }

    private void Handle(object? sender, TValueEventArgs e) => Update(e.Value, e.IsNew);
}
