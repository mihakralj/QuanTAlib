using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// AHRENS: Ahrens Moving Average
/// </summary>
/// <remarks>
/// A self-dampening IIR filter that uses a circular buffer of its own past
/// output values. The correction term shrinks as current and lagged states
/// converge, producing inherent smoothing without explicit decay constants.
///
/// Formula: AHRENS[t] = AHRENS[t-1] + (source - (AHRENS[t-1] + AHRENS[t-N]) / 2) / N
/// </remarks>
[SkipLocalsInit]
public sealed class Ahrens : AbstractBase
{
    private const int MaxPeriod = 4000;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(int Bars, bool IsHot)
    {
        public double Prev;
        public static State New() => new() { Bars = 0, IsHot = false, Prev = double.NaN };
    }

    private readonly int _period;
    private readonly double _invPeriod;
    private readonly RingBuffer _buffer; // stores past AHRENS output values

    private State _state = State.New();
    private State _p_state = State.New();
    private double _lastValidValue = double.NaN;
    private double _p_lastValidValue = double.NaN;

    private readonly ITValuePublisher? _publisher;
    private readonly TValuePublishedHandler? _listener;

    public override bool IsHot => _state.IsHot;

    public Ahrens(int period = 9)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 1);

        _period = Math.Min(period, MaxPeriod);
        _invPeriod = 1.0 / _period;
        _buffer = new RingBuffer(_period);

        Name = $"Ahrens({period})";
        WarmupPeriod = _period;

        Reset();
    }

    public Ahrens(ITValuePublisher source, int period = 9) : this(period)
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

        var s = _state;
        s.Bars++;

        double result = Compute(val, ref s);

        _state = s;
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
                vSpan[i] = Compute(val, ref state);
            }

            _state = state;
            _lastValidValue = lastValid;
            _p_state = preBatchState;
            _p_lastValidValue = preBatchLastValid;
        }
        catch
        {
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

    public static TSeries Batch(TSeries source, int period = 9)
    {
        var ahrens = new Ahrens(period);
        return ahrens.Update(source);
    }

    public static void Batch(ReadOnlySpan<double> source, Span<double> output, int period = 9)
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

        int window = Math.Min(period, MaxPeriod);
        double invPeriod = 1.0 / window;
        double lastValid = double.NaN;
        double prev = double.NaN;

        Span<double> buffer = window <= 256
            ? stackalloc double[window]
            : new double[window];

        int head = 0;
        int count = 0; // tracks how many values written to buffer

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

            // First bar: seed with source value
            if (double.IsNaN(prev))
            {
                prev = val;
            }

            // Get lagged value: oldest written result in buffer, or source if buffer empty
            // This matches streaming Compute where _buffer.Oldest returns first stored result
            double lagged;
            if (count > 0)
            {
                // oldest written index = (head - count + window) % window
                int oldestIdx = head - count;
                if (oldestIdx < 0)
                {
                    oldestIdx += window;
                }
                lagged = buffer[oldestIdx];
            }
            else
            {
                lagged = val;
            }

            // AHRENS formula: result = prev + (source - midpoint) / period
            // midpoint = (prev + lagged) * 0.5
            double midpoint = (prev + lagged) * 0.5;
            double result = Math.FusedMultiplyAdd(val - midpoint, invPeriod, prev);

            // Store output in buffer and advance head
            buffer[head] = result;
            head++;
            if (head == window)
            {
                head = 0;
            }
            if (count < window)
            {
                count++;
            }

            prev = result;
            output[i] = result;
        }
    }

    public static (TSeries Results, Ahrens Indicator) Calculate(TSeries source, int period = 9)
    {
        var indicator = new Ahrens(period);
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
    private double Compute(double val, ref State s)
    {
        // First bar: seed prev with incoming value
        if (double.IsNaN(s.Prev))
        {
            s.Prev = val;
        }

        // Get lagged AHRENS output from N bars ago
        double lagged;
        if (_buffer.Count > 0)
        {
            lagged = _buffer.Oldest;
        }
        else
        {
            lagged = val;
        }

        // AHRENS formula: result = prev + (source - (prev + lagged) / 2) / N
        double midpoint = (s.Prev + lagged) * 0.5;
        double result = Math.FusedMultiplyAdd(val - midpoint, _invPeriod, s.Prev);

        // Store output in buffer (buffer holds past AHRENS outputs)
        _buffer.Add(result);

        s.Prev = result;

        if (!s.IsHot && s.Bars >= _period)
        {
            s.IsHot = true;
        }

        return result;
    }
}
