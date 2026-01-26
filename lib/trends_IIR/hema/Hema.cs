using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// HEMA: Exponential Hull Analog (EMA-domain HMA)
/// </summary>
/// <remarks>
/// HEMA adapts the HMA topology to EMA half-life space.
///
/// Steps:
/// 1) EMA_slow(hl=N)
/// 2) EMA_fast(hl=N/2)
/// 3) De-lag: (EMA_fast - r * EMA_slow) / (1 - r), where r = lag_fast / lag_slow
/// 4) EMA_smooth(hl=sqrt(N)) applied to the de-lagged series
///
/// Half-life mapping:
/// alpha = 1 - exp(-ln(2) / hl)
/// </remarks>
[SkipLocalsInit]
public sealed class Hema : AbstractBase
{
    private const double CoverageThreshold = 0.05;
    private const double CompensatorThreshold = 1e-10;
    private const double MinDenominator = 1e-12;
    private const double MaxRatio = 0.999999;
    private const double Ln2 = 0.693147180559945309417232121458176568;

    [StructLayout(LayoutKind.Sequential)]
    private record struct State
    {
        public double EmaSlowRaw;
        public double EmaFastRaw;
        public double EmaSmoothRaw;
        public double DecaySlow;
        public double DecayFast;
        public double DecaySmooth;
        public bool IsHot;
        public bool Warmup;

        public static State New() => new()
        {
            EmaSlowRaw = 0,
            EmaFastRaw = 0,
            EmaSmoothRaw = 0,
            DecaySlow = 1.0,
            DecayFast = 1.0,
            DecaySmooth = 1.0,
            IsHot = false,
            Warmup = true
        };
    }

    private readonly double _alphaSlow;
    private readonly double _alphaFast;
    private readonly double _alphaSmooth;
    private readonly double _betaSlow;
    private readonly double _betaFast;
    private readonly double _betaSmooth;
    private readonly double _ratio;
    private readonly double _invOneMinusRatio;

    private State _state = State.New();
    private State _p_state = State.New();
    private double _lastValidValue = double.NaN;
    private double _p_lastValidValue = double.NaN;

    private readonly ITValuePublisher? _publisher;
    private readonly TValuePublishedHandler? _listener;

    public override bool IsHot => _state.IsHot;

    public Hema(int period)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);

        double n = Math.Max((double)period, 2.0);
        _alphaSlow = AlphaFromHalfLife(n);
        _alphaFast = AlphaFromHalfLife(Math.Max(1.0, n * 0.5));
        _alphaSmooth = AlphaFromHalfLife(Math.Max(1.0, Math.Sqrt(n)));

        _betaSlow = 1.0 - _alphaSlow;
        _betaFast = 1.0 - _alphaFast;
        _betaSmooth = 1.0 - _alphaSmooth;

        double lagSlow = _betaSlow / _alphaSlow;
        double lagFast = _betaFast / _alphaFast;
        double ratio = lagFast / lagSlow;
        _ratio = Math.Clamp(ratio, 0.0, MaxRatio);
        _invOneMinusRatio = 1.0 / Math.Max(1.0 - _ratio, MinDenominator);

        Name = $"Hema({period})";
        WarmupPeriod = EstimateWarmupPeriod();
    }

    public Hema(ITValuePublisher source, int period) : this(period)
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
            _p_state = _state;
            _p_lastValidValue = _lastValidValue;
        }
        else
        {
            _state = _p_state;
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

        double result = Compute(val, ref _state);
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
        List<long> t = new(len);
        List<double> v = new(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);
        source.Times.CopyTo(tSpan);

        var sourceValues = source.Values;

        State preBatchState = _state;
        double preBatchLastValid = _lastValidValue;

        State state = _state;
        double lastValid = _lastValidValue;

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

            vSpan[i] = Compute(val, ref state);
        }

        _state = state;
        _lastValidValue = lastValid;

        _p_state = preBatchState;
        _p_lastValidValue = preBatchLastValid;

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

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private double Compute(double input, ref State state)
    {
        state.EmaSlowRaw = Math.FusedMultiplyAdd(state.EmaSlowRaw, _betaSlow, _alphaSlow * input);
        state.EmaFastRaw = Math.FusedMultiplyAdd(state.EmaFastRaw, _betaFast, _alphaFast * input);

        if (state.Warmup)
        {
            state.DecaySlow *= _betaSlow;
            state.DecayFast *= _betaFast;
            state.DecaySmooth *= _betaSmooth;

            double invSlow = 1.0 / Math.Max(1.0 - state.DecaySlow, MinDenominator);
            double invFast = 1.0 / Math.Max(1.0 - state.DecayFast, MinDenominator);
            double invSmooth = 1.0 / Math.Max(1.0 - state.DecaySmooth, MinDenominator);

            double emaSlow = state.EmaSlowRaw * invSlow;
            double emaFast = state.EmaFastRaw * invFast;
            double deLag = Math.FusedMultiplyAdd(-_ratio, emaSlow, emaFast) * _invOneMinusRatio;
            if (!double.IsFinite(deLag))
            {
                deLag = input;
            }

            state.EmaSmoothRaw = Math.FusedMultiplyAdd(state.EmaSmoothRaw, _betaSmooth, _alphaSmooth * deLag);

            double maxDecay = Math.Max(state.DecaySlow, Math.Max(state.DecayFast, state.DecaySmooth));
            if (!state.IsHot && maxDecay <= CoverageThreshold)
            {
                state.IsHot = true;
            }

            state.Warmup = maxDecay > CompensatorThreshold;
            if (!state.Warmup)
            {
                state.IsHot = true;
            }

            double result = state.EmaSmoothRaw * invSmooth;
            if (!double.IsFinite(result))
            {
                ResetState(ref state, input);
                return input;
            }

            return result;
        }

        double deLagFast = Math.FusedMultiplyAdd(-_ratio, state.EmaSlowRaw, state.EmaFastRaw) * _invOneMinusRatio;
        if (!double.IsFinite(deLagFast))
        {
            deLagFast = input;
        }

        state.EmaSmoothRaw = Math.FusedMultiplyAdd(state.EmaSmoothRaw, _betaSmooth, _alphaSmooth * deLagFast);

        if (!state.IsHot)
        {
            state.IsHot = true;
        }

        double fastResult = state.EmaSmoothRaw;
        if (!double.IsFinite(fastResult))
        {
            ResetState(ref state, input);
            return input;
        }

        return fastResult;
    }

    public static TSeries Calculate(TSeries source, int period)
    {
        var hema = new Hema(period);
        return hema.Update(source);
    }

    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);

        if (source.Length == 0)
        {
            return;
        }

        double n = Math.Max((double)period, 2.0);
        double alphaSlow = AlphaFromHalfLife(n);
        double alphaFast = AlphaFromHalfLife(Math.Max(1.0, n * 0.5));
        double alphaSmooth = AlphaFromHalfLife(Math.Max(1.0, Math.Sqrt(n)));

        double betaSlow = 1.0 - alphaSlow;
        double betaFast = 1.0 - alphaFast;
        double betaSmooth = 1.0 - alphaSmooth;

        double lagSlow = betaSlow / alphaSlow;
        double lagFast = betaFast / alphaFast;
        double ratio = Math.Clamp(lagFast / lagSlow, 0.0, MaxRatio);
        double invOneMinusRatio = 1.0 / Math.Max(1.0 - ratio, MinDenominator);

        double emaSlowRaw = 0.0;
        double emaFastRaw = 0.0;
        double emaSmoothRaw = 0.0;
        double decaySlow = 1.0;
        double decayFast = 1.0;
        double decaySmooth = 1.0;
        bool warmup = true;

        double lastValid = double.NaN;

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

            emaSlowRaw = Math.FusedMultiplyAdd(emaSlowRaw, betaSlow, alphaSlow * val);
            emaFastRaw = Math.FusedMultiplyAdd(emaFastRaw, betaFast, alphaFast * val);

            if (warmup)
            {
                decaySlow *= betaSlow;
                decayFast *= betaFast;
                decaySmooth *= betaSmooth;

                double invSlow = 1.0 / Math.Max(1.0 - decaySlow, MinDenominator);
                double invFast = 1.0 / Math.Max(1.0 - decayFast, MinDenominator);
                double invSmooth = 1.0 / Math.Max(1.0 - decaySmooth, MinDenominator);

                double emaSlow = emaSlowRaw * invSlow;
                double emaFast = emaFastRaw * invFast;
                double deLag = Math.FusedMultiplyAdd(-ratio, emaSlow, emaFast) * invOneMinusRatio;
                if (!double.IsFinite(deLag))
                {
                    deLag = val;
                }

                emaSmoothRaw = Math.FusedMultiplyAdd(emaSmoothRaw, betaSmooth, alphaSmooth * deLag);
                double result = emaSmoothRaw * invSmooth;
                if (!double.IsFinite(result))
                {
                    emaSlowRaw = val;
                    emaFastRaw = val;
                    emaSmoothRaw = val;
                    decaySlow = 1.0;
                    decayFast = 1.0;
                    decaySmooth = 1.0;
                    output[i] = val;
                    continue;
                }

                output[i] = result;

                double maxDecay = Math.Max(decaySlow, Math.Max(decayFast, decaySmooth));
                warmup = maxDecay > CompensatorThreshold;
            }
            else
            {
                double deLag = Math.FusedMultiplyAdd(-ratio, emaSlowRaw, emaFastRaw) * invOneMinusRatio;
                if (!double.IsFinite(deLag))
                {
                    deLag = val;
                }

                emaSmoothRaw = Math.FusedMultiplyAdd(emaSmoothRaw, betaSmooth, alphaSmooth * deLag);
                double result = emaSmoothRaw;
                if (!double.IsFinite(result))
                {
                    emaSlowRaw = val;
                    emaFastRaw = val;
                    emaSmoothRaw = val;
                    decaySlow = 1.0;
                    decayFast = 1.0;
                    decaySmooth = 1.0;
                    warmup = true;
                    output[i] = val;
                    continue;
                }

                output[i] = result;
            }
        }
    }

    public override void Reset()
    {
        _state = State.New();
        _p_state = _state;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double AlphaFromHalfLife(double halfLife)
    {
        double hl = Math.Max(1.0, halfLife);
        double x = -Ln2 / hl;
        return -Expm1(x);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Expm1(double x)
    {
        double ax = Math.Abs(x);
        if (ax < 1e-5)
        {
            double x2 = x * x;
            return Math.FusedMultiplyAdd(x2 * x, 1.0 / 6.0, x + (x2 * 0.5));
        }

        return Math.Exp(x) - 1.0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ResetState(ref State state, double value)
    {
        state = State.New();
        state.EmaSlowRaw = value;
        state.EmaFastRaw = value;
        state.EmaSmoothRaw = value;
    }

    private int EstimateWarmupPeriod()
    {
        double maxDecay = Math.Max(_betaSlow, Math.Max(_betaFast, _betaSmooth));
        if (maxDecay <= 0)
        {
            return 1;
        }

        double steps = Math.Log(CoverageThreshold) / Math.Log(maxDecay);
        if (double.IsNaN(steps) || double.IsInfinity(steps) || steps <= 0)
        {
            return 1;
        }

        return (int)Math.Ceiling(steps);
    }
}
