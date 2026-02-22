using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// GDEMA: Generalized Double Exponential Moving Average
/// </summary>
/// <remarks>
/// Extends standard DEMA with a tunable volume factor v that controls
/// the aggressiveness of lag compensation. Two cascaded EMAs with shared
/// warmup compensator combined via parameterized linear combination.
///
/// Calculation: <c>GDEMA = (1+v)×EMA₁ - v×EMA₂</c> where EMA₂ = EMA(EMA₁).
/// When v=0 → EMA, v=1 → standard DEMA, v&gt;1 → more aggressive lag removal.
/// </remarks>
[SkipLocalsInit]
public sealed class Gdema : AbstractBase
{
    [StructLayout(LayoutKind.Auto)]
    private record struct EmaState(double Ema, double E, bool IsHot, bool IsCompensated)
    {
        public static EmaState New() => new() { Ema = 0, E = 1.0, IsHot = false, IsCompensated = false };
    }

    private readonly double _alpha;
    private readonly double _decay;
    private readonly double _vfactor;
    private readonly double _onePlusV; // precomputed (1 + v)

    private EmaState _state1 = EmaState.New();
    private EmaState _state2 = EmaState.New();
    private EmaState _p_state1 = EmaState.New();
    private EmaState _p_state2 = EmaState.New();

    private double _lastValidValue = double.NaN;
    private double _p_lastValidValue = double.NaN;

    private readonly ITValuePublisher? _publisher;
    private readonly TValuePublishedHandler? _listener;

    public override bool IsHot => _state2.IsHot;

    public Gdema(int period = 10, double vfactor = 1.0)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 1);

        _alpha = 2.0 / (period + 1);
        _decay = 1.0 - _alpha;
        _vfactor = vfactor;
        _onePlusV = 1.0 + vfactor;

        Name = $"Gdema({period},{vfactor:F1})";
        WarmupPeriod = period;
    }

    public Gdema(ITValuePublisher source, int period = 10, double vfactor = 1.0) : this(period, vfactor)
    {
        _publisher = source;
        _listener = Handle;
        source.Pub += _listener;
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

        double e1 = ComputeEma(val, _alpha, _decay, ref _state1);
        double e2 = ComputeEma(e1, _alpha, _decay, ref _state2);

        // GDEMA = (1+v)*EMA1 - v*EMA2
        double result = Math.FusedMultiplyAdd(_onePlusV, e1, -_vfactor * e2);

        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public override TSeries Update(TSeries source)
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
        source.Times.CopyTo(tSpan);

        EmaState preBatch_s1 = _state1;
        EmaState preBatch_s2 = _state2;
        double preBatch_lastValid = _lastValidValue;

        EmaState s1 = _state1;
        EmaState s2 = _state2;
        double lastValid = _lastValidValue;
        double alpha = _alpha;
        double decay = _decay;
        double onePlusV = _onePlusV;
        double vf = _vfactor;

        for (int i = 0; i < len; i++)
        {
            double val = source.Values[i];
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

            double e1 = ComputeEma(val, alpha, decay, ref s1);
            double e2 = ComputeEma(e1, alpha, decay, ref s2);

            vSpan[i] = Math.FusedMultiplyAdd(onePlusV, e1, -vf * e2);
        }

        _state1 = s1;
        _state2 = s2;
        _lastValidValue = lastValid;

        _p_state1 = preBatch_s1;
        _p_state2 = preBatch_s2;
        _p_lastValidValue = preBatch_lastValid;

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (double value in source)
        {
            Update(new TValue(DateTime.MinValue, value));
        }
    }

    public static TSeries Batch(TSeries source, int period = 10, double vfactor = 1.0)
    {
        var gdema = new Gdema(period, vfactor);
        return gdema.Update(source);
    }

    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period = 10, double vfactor = 1.0)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length.", nameof(output));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(period, 1);

        if (source.Length == 0)
        {
            return;
        }

        double alpha = 2.0 / (period + 1);
        double decay = 1.0 - alpha;
        double onePlusV = 1.0 + vfactor;
        double lastValid = double.NaN;

        double ema1_val = 0;
        double ema1_e = 1.0;
        bool ema1_isCompensated = false;

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

            // EMA1
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

            // EMA2
            ema2_val = Math.FusedMultiplyAdd(ema2_val, decay, alpha * e1);
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

            // GDEMA = (1+v)*EMA1 - v*EMA2
            output[i] = Math.FusedMultiplyAdd(onePlusV, e1, -vfactor * e2);
        }
    }

    public static (TSeries Results, Gdema Indicator) Calculate(TSeries source, int period = 10, double vfactor = 1.0)
    {
        var indicator = new Gdema(period, vfactor);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double ComputeEma(double input, double alpha, double decay, ref EmaState state)
    {
        state.Ema = Math.FusedMultiplyAdd(state.Ema, decay, alpha * input);

        double result;
        if (!state.IsCompensated)
        {
            state.E *= decay;

            if (!state.IsHot && state.E <= 0.05)
            {
                state.IsHot = true;
            }

            if (state.E <= 1e-10)
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
}
