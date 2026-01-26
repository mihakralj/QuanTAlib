using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// ZLEMA: Zero-Lag Exponential Moving Average
/// </summary>
/// <remarks>
/// ZLEMA reduces EMA lag by filtering a zero-lag signal:
/// signal = 2 * price - price_lag
/// zlema = EMA(signal)
/// </remarks>
[SkipLocalsInit]
public sealed class Zlema : AbstractBase
{
    private const double CoverageThreshold = 0.05;
    private const double CompensatorThreshold = 1e-10;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double ZlemaRaw, double E, bool IsHot, bool IsCompensated, int Bars)
    {
        public static State New() => new() { ZlemaRaw = 0.0, E = 1.0, IsHot = false, IsCompensated = false, Bars = 0 };
    }

    private readonly double _alpha;
    private readonly double _beta;
    private readonly int _lag;
    private readonly RingBuffer _lagBuffer;

    private State _state = State.New();
    private State _p_state = State.New();
    private double _lastValidValue = double.NaN;
    private double _p_lastValidValue = double.NaN;

    private readonly ITValuePublisher? _publisher;
    private readonly TValuePublishedHandler? _listener;

    public override bool IsHot => _state.IsHot;

    public Zlema(int period)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);

        _alpha = 2.0 / (period + 1);
        _beta = 1.0 - _alpha;
        _lag = ComputeLag(period);
        _lagBuffer = new RingBuffer(_lag + 1);

        Name = $"Zlema({period})";
        WarmupPeriod = Math.Max(_lag + 1, EstimateWarmupPeriod(_beta));

        Reset();
    }

    public Zlema(double alpha)
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

        Name = $"Zlema(a={alpha:F4})";
        WarmupPeriod = Math.Max(_lag + 1, EstimateWarmupPeriod(_beta));

        Reset();
    }

    public Zlema(ITValuePublisher source, int period) : this(period)
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
            _lagBuffer.Snapshot();
        }
        else
        {
            _state = _p_state;
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

        _state.Bars++;

        _lagBuffer.Add(val);
        double lagged = _lagBuffer.Oldest;
        double signal = Math.FusedMultiplyAdd(2.0, val, -lagged);

        double result = Compute(signal, ref _state);
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

        State preBatchState = _state;
        double preBatchLastValid = _lastValidValue;
        _lagBuffer.Snapshot();

        State state = _state;
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

    public static TSeries Calculate(TSeries source, int period)
    {
        var zlema = new Zlema(period);
        return zlema.Update(source);
    }

    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period)
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
        Calculate(source, output, alpha, period);
    }

    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, double alpha)
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
        Calculate(source, output, alpha, period);
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
        state.ZlemaRaw = Math.FusedMultiplyAdd(state.ZlemaRaw, _beta, _alpha * signal);

        double result;
        if (!state.IsCompensated)
        {
            state.E *= _beta;

            if (!state.IsHot && state.Bars >= _lag + 1 && state.E <= CoverageThreshold)
            {
                state.IsHot = true;
            }

            if (state.E <= CompensatorThreshold)
            {
                state.IsCompensated = true;
                result = state.ZlemaRaw;
            }
            else
            {
                result = state.ZlemaRaw / (1.0 - state.E);
            }
        }
        else
        {
            if (!state.IsHot && state.Bars >= _lag + 1)
            {
                state.IsHot = true;
            }

            result = state.ZlemaRaw;
        }

        return result;
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
        _state = State.New();
        _p_state = _state;
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

    private static void Calculate(ReadOnlySpan<double> source, Span<double> output, double alpha, double period)
    {
        int lag = ComputeLag(period);
        int bufferSize = lag + 1;

        double beta = 1.0 - alpha;
        double zlemaRaw = 0.0;
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

            zlemaRaw = Math.FusedMultiplyAdd(zlemaRaw, beta, alpha * signal);

            if (!isCompensated)
            {
                e *= beta;
                if (e <= CompensatorThreshold)
                {
                    isCompensated = true;
                    output[i] = zlemaRaw;
                }
                else
                {
                    output[i] = zlemaRaw / (1.0 - e);
                }
            }
            else
            {
                output[i] = zlemaRaw;
            }
        }
    }
}
