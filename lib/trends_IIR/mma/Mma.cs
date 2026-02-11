using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// MMA: Modified Moving Average
/// </summary>
/// <remarks>
/// MMA blends a simple average with a weighted component computed from
/// a lagged buffer to balance smoothness and responsiveness.
/// </remarks>
[SkipLocalsInit]
public sealed class Mma : AbstractBase
{
    private const int MaxPeriod = 4000;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(int Bars, bool IsHot)
    {
        public static State New() => new() { Bars = 0, IsHot = false };
    }

    private readonly int _period;
    private readonly RingBuffer _buffer;

    private State _state = State.New();
    private State _p_state = State.New();
    private double _lastValidValue = double.NaN;
    private double _p_lastValidValue = double.NaN;

    private readonly ITValuePublisher? _publisher;
    private readonly TValuePublishedHandler? _listener;

    public override bool IsHot => _state.IsHot;

    public Mma(int period)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 2);

        _period = Math.Min(Math.Max(2, period), MaxPeriod);
        _buffer = new RingBuffer(_period);

        Name = $"Mma({period})";
        WarmupPeriod = _period;

        Reset();
    }

    public Mma(ITValuePublisher source, int period) : this(period)
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
            _buffer.Snapshot();
        }
        else
        {
            _state = _p_state;
            _lastValidValue = _p_lastValidValue;
            _buffer.Restore();
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
        _buffer.Add(val);

        double result = Compute(ref _state);
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

        // Snapshot buffer state before batch processing
        _buffer.Snapshot();

        State preBatchState = _state;
        double preBatchLastValid = _lastValidValue;
        State state = _state;
        double lastValid = _lastValidValue;

        try
        {
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
                _buffer.Add(val);

                vSpan[i] = Compute(ref state);
            }

            // Only update state after successful completion
            _state = state;
            _lastValidValue = lastValid;

            // Set previous state to pre-batch for bar correction support
            _p_state = preBatchState;
            _p_lastValidValue = preBatchLastValid;
        }
        catch
        {
            // Restore buffer to pre-batch state on any exception
            _buffer.Restore();
            throw;
        }

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
        var mma = new Mma(period);
        return mma.Update(source);
    }

    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length.", nameof(output));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(period, 2);

        if (source.Length == 0)
        {
            return;
        }

        int window = Math.Min(Math.Max(2, period), MaxPeriod);
        double sum = 0.0;
        int count = 0;
        double lastValid = double.NaN;

        Span<double> buffer = window <= 256
            ? stackalloc double[window]
            : new double[window];
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

            if (count < window)
            {
                count++;
            }
            else
            {
                sum -= buffer[head];
            }

            buffer[head] = val;
            sum += val;

            head++;
            if (head == window)
            {
                head = 0;
            }

            double sma = sum / count;
            double weightedSum = ComputeWeightedSum(buffer, head, count);
            double denom = (count + 1.0) * count;
            output[i] = Math.FusedMultiplyAdd(weightedSum, 6.0 / denom, sma);
        }
    }

    public static (TSeries Results, Mma Indicator) Calculate(TSeries source, int period)
    {
        var indicator = new Mma(period);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    public override void Reset()
    {
        _state = State.New();
        _p_state = _state;
        _lastValidValue = double.NaN;
        _p_lastValidValue = double.NaN;
        _buffer.Clear();
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
    private double Compute(ref State state)
    {
        int count = _buffer.Count;
        if (count <= 0)
        {
            return double.NaN;
        }

        double sma = _buffer.Sum / count;
        double weightedSum = ComputeWeightedSum(_buffer, count);
        double denom = (count + 1.0) * count;
        double result = Math.FusedMultiplyAdd(weightedSum, 6.0 / denom, sma);

        if (!state.IsHot && count >= _period)
        {
            state.IsHot = true;
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ComputeWeightedSum(RingBuffer buffer, int count)
    {
        int start = buffer.StartIndex;
        int capacity = buffer.Capacity;
        ReadOnlySpan<double> data = buffer.InternalBuffer;

        int idx = start + count - 1;
        if (idx >= capacity)
        {
            idx -= capacity;
        }

        double weightedSum = 0.0;
        for (int i = 0; i < count; i++)
        {
            double weight = (count - ((2 * i) + 1)) * 0.5;
            weightedSum = Math.FusedMultiplyAdd(data[idx], weight, weightedSum);

            idx--;
            if (idx < 0)
            {
                idx += capacity;
            }
        }

        return weightedSum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ComputeWeightedSum(ReadOnlySpan<double> buffer, int head, int count)
    {
        int idx = head - 1;
        if (idx < 0)
        {
            idx = count - 1;
        }

        double weightedSum = 0.0;
        for (int i = 0; i < count; i++)
        {
            double weight = (count - ((2 * i) + 1)) * 0.5;
            weightedSum = Math.FusedMultiplyAdd(buffer[idx], weight, weightedSum);

            idx--;
            if (idx < 0)
            {
                idx = count - 1;
            }
        }

        return weightedSum;
    }
}