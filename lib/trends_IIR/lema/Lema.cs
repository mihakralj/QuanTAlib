using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// LEMA: Leader Exponential Moving Average
/// </summary>
/// <remarks>
/// Adds a smoothed error correction to the standard EMA, making it respond
/// faster than EMA while maintaining smoothness. The error term captures the
/// systematic tracking deficit and adds it back.
///
/// Calculation: <c>LEMA = EMA(source, N) + EMA(source - EMA(source, N), N)</c>.
/// </remarks>
/// <seealso href="Lema.md">Detailed documentation</seealso>
/// <seealso href="lema.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Lema : AbstractBase
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
    private bool _isNew = true;
    private readonly ITValuePublisher? _publisher;
    private readonly TValuePublishedHandler? _listener;

    public bool IsNew => _isNew;
    public override bool IsHot => _state2.IsHot;

    public Lema(int period)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _alpha = 2.0 / (period + 1);
        _decay = 1.0 - _alpha;
        Name = $"Lema({period})";
        WarmupPeriod = period;
    }

    public Lema(ITValuePublisher source, int period) : this(period)
    {
        _publisher = source;
        _listener = Handle;
        source.Pub += _listener;
    }

    public Lema(double alpha)
    {
        if (alpha <= 0 || alpha > 1)
        {
            throw new ArgumentException("Alpha must be between 0 and 1", nameof(alpha));
        }

        _alpha = alpha;
        _decay = 1.0 - alpha;
        Name = $"Lema(α={alpha:F4})";
        WarmupPeriod = (int)((2.0 / alpha) - 1.0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        _isNew = isNew;
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

        // Sanitize input
        double val = input.Value;
        if (double.IsFinite(val))
        {
            _lastValidValue = val;
        }
        else
        {
            val = _lastValidValue;
        }

        if (double.IsNaN(val))
        {
            Last = new TValue(input.Time, double.NaN);
            PubEvent(Last, isNew);
            return Last;
        }

        // EMA1: standard EMA of source
        double e1 = Compute(val, _alpha, _decay, ref _state1);

        // Error: source - EMA(source)
        double error = val - e1;

        // EMA2: EMA of the error series
        double e2 = Compute(error, _alpha, _decay, ref _state2);

        // LEMA = EMA(source) + EMA(error)
        double result = e1 + e2;
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
            {
                lastValid = val;
            }
            else
            {
                val = lastValid;
            }

            if (double.IsNaN(val))
            {
                vSpan[i] = double.NaN;
                continue;
            }

            double e1 = Compute(val, alpha, decay, ref s1);
            double error = val - e1;
            double e2 = Compute(error, alpha, decay, ref s2);

            vSpan[i] = e1 + e2;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double Compute(double input, double alpha, double decay, ref EmaState state)
    {
        state.Ema = Math.FusedMultiplyAdd(state.Ema, decay, alpha * input);

        double result;
        if (!state.IsCompensated)
        {
            state.E *= decay;

            if (!state.IsHot && state.E <= 0.05) // COVERAGE_THRESHOLD
            {
                state.IsHot = true;
            }

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
        var lema = new Lema(period);
        return lema.Update(source);
    }

    public static TSeries Batch(TSeries source, double alpha)
    {
        var lema = new Lema(alpha);
        return lema.Update(source);
    }

    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        double alpha = 2.0 / (period + 1);
        Batch(source, output, alpha);
    }

    public static void Batch(ReadOnlySpan<double> source, Span<double> output, double alpha)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        if (alpha <= 0 || alpha > 1)
        {
            throw new ArgumentException("Alpha must be between 0 and 1", nameof(alpha));
        }

        if (source.Length == 0)
        {
            return;
        }

        double decay = 1.0 - alpha;
        double lastValid = double.NaN;

        // State for EMA1 (source)
        double ema1_val = 0;
        double ema1_e = 1.0;
        bool ema1_isCompensated = false;

        // State for EMA2 (error)
        double ema2_val = 0;
        double ema2_e = 1.0;
        bool ema2_isCompensated = false;

        for (int i = 0; i < source.Length; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
            {
                lastValid = val;
            }
            else
            {
                val = lastValid;
            }

            if (double.IsNaN(val))
            {
                output[i] = double.NaN;
                continue;
            }

            // Update EMA1 (source)
            ema1_val = Math.FusedMultiplyAdd(ema1_val, decay, alpha * val);
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

            // Error = source - EMA(source)
            double error = val - e1;

            // Update EMA2 (error)
            ema2_val = Math.FusedMultiplyAdd(ema2_val, decay, alpha * error);
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

            // LEMA = EMA(source) + EMA(error)
            output[i] = e1 + e2;
        }
    }

    public static (TSeries Results, Lema Indicator) Calculate(TSeries source, int period)
    {
        var indicator = new Lema(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
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

    protected override void Dispose(bool disposing)
    {
        if (disposing && _publisher != null && _listener != null)
        {
            _publisher.Pub -= _listener;
        }
        base.Dispose(disposing);
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);
}
