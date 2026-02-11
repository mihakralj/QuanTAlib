using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// ZLTEMA: Zero-Lag Triple Exponential Moving Average
/// </summary>
/// <remarks>
/// Hybrid triple-stage predictive architecture combining ZLEMA signal preprocessing with TEMA smoothing.
/// Applies lag compensation to the input signal, then cascades through three EMA stages with
/// optimized coefficients (3, -3, 1) for reduced lag and enhanced noise suppression.
///
/// Calculation: <c>Signal = 2×Price - Price[lag]</c>, then <c>ZLTEMA = 3×EMA1(Signal) - 3×EMA2(EMA1) + EMA3(EMA2)</c>
/// </remarks>
/// <seealso href="Zltema.md">Detailed documentation</seealso>
/// <seealso href="zltema.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Zltema : AbstractBase
{
    private const double CoverageThreshold = 0.05;
    private const double CompensatorThreshold = 1e-10;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double Ema1Raw, double Ema2Raw, double Ema3Raw, double E, bool IsHot, bool IsCompensated, int Bars)
    {
        public static State New() => new() { Ema1Raw = 0.0, Ema2Raw = 0.0, Ema3Raw = 0.0, E = 1.0, IsHot = false, IsCompensated = false, Bars = 0 };
    }

    private readonly double _alpha;
    private readonly double _beta;
    private readonly int _lag;
    private readonly RingBuffer _lagBuffer;

    private State _s = State.New();
    private State _ps = State.New();
    private double _lastValidValue = double.NaN;
    private double _p_lastValidValue = double.NaN;

    private readonly ITValuePublisher? _publisher;
    private readonly TValuePublishedHandler? _listener;

    public override bool IsHot => _s.IsHot;

    public Zltema(int period)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);

        _alpha = 2.0 / (period + 1);
        _beta = 1.0 - _alpha;
        _lag = ComputeLag(period);
        _lagBuffer = new RingBuffer(_lag + 1);

        Name = $"Zltema({period})";
        WarmupPeriod = Math.Max(_lag + 1, EstimateWarmupPeriod(_beta));

        Reset();
    }

    public Zltema(double alpha)
    {
        if (alpha <= 0.0 || alpha > 1.0 || !double.IsFinite(alpha))
        {
            throw new ArgumentException("Alpha must be finite and in (0, 1].", nameof(alpha));
        }

        _alpha = alpha;
        _beta = 1.0 - _alpha;
        double period = (2.0 / alpha) - 1.0;
        _lag = ComputeLag(period);
        _lagBuffer = new RingBuffer(_lag + 1);

        Name = $"Zltema(a={alpha:F4})";
        WarmupPeriod = Math.Max(_lag + 1, EstimateWarmupPeriod(_beta));

        Reset();
    }

    public Zltema(ITValuePublisher source, int period) : this(period)
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
            _ps = _s;
            _p_lastValidValue = _lastValidValue;
            _lagBuffer.Snapshot();
        }
        else
        {
            _s = _ps;
            _lastValidValue = _p_lastValidValue;
            _lagBuffer.Restore();
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

        var s = _s;
        s.Bars++;

        _lagBuffer.Add(val);
        double lagged = _lagBuffer.Oldest;
        double signal = Math.FusedMultiplyAdd(2.0, val, -lagged);

        double result = Compute(signal, ref s);
        _s = s;

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

        State preBatchState = _s;
        double preBatchLastValid = _lastValidValue;
        _lagBuffer.Snapshot();

        State state = _s;
        double lastValid = _lastValidValue;

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

            state.Bars++;
            _lagBuffer.Add(val);
            double lagged = _lagBuffer.Oldest;
            double signal = Math.FusedMultiplyAdd(2.0, val, -lagged);

            vSpan[i] = Compute(signal, ref state);
        }

        _s = state;
        _lastValidValue = lastValid;
        _ps = preBatchState;
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

    public static TSeries Batch(TSeries source, int period)
    {
        var zltema = new Zltema(period);
        return zltema.Update(source);
    }

    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length.", nameof(output));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);

        if (source.Length == 0)
        {
            return;
        }

        double alpha = 2.0 / (period + 1);
        BatchCore(source, output, alpha, period);
    }

    public static void Batch(ReadOnlySpan<double> source, Span<double> output, double alpha)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length.", nameof(output));
        }

        if (alpha <= 0.0 || alpha > 1.0 || !double.IsFinite(alpha))
        {
            throw new ArgumentException("Alpha must be finite and in (0, 1].", nameof(alpha));
        }

        if (source.Length == 0)
        {
            return;
        }

        double period = (2.0 / alpha) - 1.0;
        BatchCore(source, output, alpha, period);
    }

    public static (TSeries Results, Zltema Indicator) Calculate(TSeries source, int period)
    {
        var indicator = new Zltema(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ComputeLag(double period)
    {
        double lag = (period - 1.0) * 0.5;
        int lagInt = (int)Math.Round(lag, MidpointRounding.AwayFromZero);
        return Math.Max(1, lagInt);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private double Compute(double signal, ref State state)
    {
        // First EMA stage
        state.Ema1Raw = Math.FusedMultiplyAdd(state.Ema1Raw, _beta, _alpha * signal);

        double ema1, ema2, ema3;

        if (!state.IsCompensated)
        {
            state.E *= _beta;

            if (!state.IsHot && state.Bars >= _lag + 1 && state.E <= CoverageThreshold)
            {
                state.IsHot = true;
            }

            double compensator = 1.0 / (1.0 - state.E);
            ema1 = state.Ema1Raw * compensator;

            // Second EMA stage
            state.Ema2Raw = Math.FusedMultiplyAdd(state.Ema2Raw, _beta, _alpha * ema1);
            ema2 = state.Ema2Raw * compensator;

            // Third EMA stage
            state.Ema3Raw = Math.FusedMultiplyAdd(state.Ema3Raw, _beta, _alpha * ema2);
            ema3 = state.Ema3Raw * compensator;

            if (state.E <= CompensatorThreshold)
            {
                state.IsCompensated = true;
            }
        }
        else
        {
            if (!state.IsHot && state.Bars >= _lag + 1)
            {
                state.IsHot = true;
            }

            ema1 = state.Ema1Raw;
            state.Ema2Raw = Math.FusedMultiplyAdd(state.Ema2Raw, _beta, _alpha * ema1);
            ema2 = state.Ema2Raw;

            state.Ema3Raw = Math.FusedMultiplyAdd(state.Ema3Raw, _beta, _alpha * ema2);
            ema3 = state.Ema3Raw;
        }

        // TEMA formula: 3 * EMA1 - 3 * EMA2 + EMA3
        return Math.FusedMultiplyAdd(3.0, ema1, Math.FusedMultiplyAdd(-3.0, ema2, ema3));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int EstimateWarmupPeriod(double beta)
    {
        if (beta <= 0.0)
        {
            return 1;
        }

        double steps = Math.Log(CoverageThreshold) / Math.Log(beta);
        if (double.IsNaN(steps) || double.IsInfinity(steps) || steps <= 0.0)
        {
            return 1;
        }

        return (int)Math.Ceiling(steps);
    }

    public override void Reset()
    {
        _s = State.New();
        _ps = _s;
        _lastValidValue = double.NaN;
        _p_lastValidValue = double.NaN;

        _lagBuffer.Clear();
        for (int i = 0; i < _lagBuffer.Capacity; i++)
        {
            _lagBuffer.Add(0.0);
        }

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

    private static void BatchCore(ReadOnlySpan<double> source, Span<double> output, double alpha, double period)
    {
        int lag = ComputeLag(period);
        int bufferSize = lag + 1;

        double beta = 1.0 - alpha;
        double ema1Raw = 0.0;
        double ema2Raw = 0.0;
        double ema3Raw = 0.0;
        double e = 1.0;
        bool isCompensated = false;

        double lastValid = double.NaN;

        Span<double> buffer = bufferSize <= 256
            ? stackalloc double[bufferSize]
            : new double[bufferSize];

        buffer.Clear();
        int head = 0;

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

            buffer[head] = val;
            head++;
            if (head == bufferSize)
            {
                head = 0;
            }

            double lagged = buffer[head];
            double signal = Math.FusedMultiplyAdd(2.0, val, -lagged);

            ema1Raw = Math.FusedMultiplyAdd(ema1Raw, beta, alpha * signal);

            double ema1, ema2, ema3;

            if (!isCompensated)
            {
                e *= beta;
                double compensator = 1.0 / (1.0 - e);
                ema1 = ema1Raw * compensator;

                ema2Raw = Math.FusedMultiplyAdd(ema2Raw, beta, alpha * ema1);
                ema2 = ema2Raw * compensator;

                ema3Raw = Math.FusedMultiplyAdd(ema3Raw, beta, alpha * ema2);
                ema3 = ema3Raw * compensator;

                if (e <= CompensatorThreshold)
                {
                    isCompensated = true;
                }
            }
            else
            {
                ema1 = ema1Raw;
                ema2Raw = Math.FusedMultiplyAdd(ema2Raw, beta, alpha * ema1);
                ema2 = ema2Raw;

                ema3Raw = Math.FusedMultiplyAdd(ema3Raw, beta, alpha * ema2);
                ema3 = ema3Raw;
            }

            output[i] = Math.FusedMultiplyAdd(3.0, ema1, Math.FusedMultiplyAdd(-3.0, ema2, ema3));
        }
    }
}