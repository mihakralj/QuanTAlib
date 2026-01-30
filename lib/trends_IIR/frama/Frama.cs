using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// FRAMA: Ehlers Fractal Adaptive Moving Average
/// </summary>
/// <remarks>
/// Classic Traders' Tips FRAMA:
/// - Ranges are computed from High/Low (not from source).
/// - Smoothed price is HL2.
/// - alpha = exp(-4.6 * (D - 1)), clamped to [0.01, 1].
/// - Period forced to even, >= 2.
/// </remarks>
[SkipLocalsInit]
public sealed class Frama : ITValuePublisher, IDisposable
{
    private const double AlphaFloor = 0.01;
    private const double AlphaCeil = 1.0;
    private const double Log2 = 0.693147180559945309417232121458176568;

    private readonly int _periodEven;
    private readonly int _half;
    private readonly RingBuffer _highs;
    private readonly RingBuffer _lows;
    private readonly TValuePublishedHandler _handler;
    private readonly ITValuePublisher? _source;
    private bool _disposed;

    [StructLayout(LayoutKind.Sequential)]
    private record struct State
    {
        public double Frama;
        public double LastHigh;
        public double LastLow;
        public int Bars;
        public bool HasValue;
    }

    private State _state;
    private State _p_state;

    public string Name { get; }
    public int WarmupPeriod { get; }
    public bool IsHot => _state.Bars >= _periodEven;

    public event TValuePublishedHandler? Pub;

    public TValue Last { get; private set; }

    public Frama(int period)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 2);

        int pe = (period % 2 == 0) ? period : period + 1;
        _periodEven = pe;
        _half = pe / 2;

        _highs = new RingBuffer(pe);
        _lows = new RingBuffer(pe);
        _handler = Handle;

        Name = $"Frama({period})";
        WarmupPeriod = pe;

        Reset();
    }

    public Frama(ITValuePublisher source, int period) : this(period)
    {
        _source = source;
        source.Pub += _handler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _state = default;
        _p_state = default;
        _highs.Clear();
        _lows.Clear();
        Last = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
            _highs.Snapshot();
            _lows.Snapshot();
        }
        else
        {
            _state = _p_state;
            _highs.Restore();
            _lows.Restore();
        }

        double high = input.High;
        double low = input.Low;

        if (!double.IsFinite(high) || !double.IsFinite(low))
        {
            if (_state.Bars == 0)
            {
                Last = new TValue(input.Time, double.NaN);
                Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
                return Last;
            }

            high = _state.LastHigh;
            low = _state.LastLow;
        }

        _state.LastHigh = high;
        _state.LastLow = low;

        _state.Bars++;
        _highs.Add(high);
        _lows.Add(low);

        if (_state.Bars < _periodEven)
        {
            _state.Frama = double.NaN;
            Last = new TValue(input.Time, double.NaN);
            Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
            return Last;
        }

        double price = (high + low) * 0.5;

        // Recent half: last _half values (most recent)
        double maxRecent = GetMax(_highs, _half);
        double minRecent = GetMin(_lows, _half);
        // Full period: all _periodEven values
        double maxFull = GetMax(_highs, _periodEven);
        double minFull = GetMin(_lows, _periodEven);
        // Previous half: older _half values (starts at count - _periodEven)
        int prevOffset = _highs.Count - _periodEven;
        double maxPrev = GetMax(_highs, _half, startOffset: prevOffset);
        double minPrev = GetMin(_lows, _half, startOffset: prevOffset);

        double n1 = (maxRecent - minRecent) / _half;
        double n2 = (maxPrev - minPrev) / _half;
        double n3 = (maxFull - minFull) / _periodEven;

        double alpha = AlphaCeil;
        if (n1 > 0.0 && n2 > 0.0 && n3 > 0.0)
        {
            double dimen = (Math.Log(n1 + n2) - Math.Log(n3)) / Log2;
            alpha = Math.Exp(-4.6 * (dimen - 1.0));
            if (alpha < AlphaFloor)
            {
                alpha = AlphaFloor;
            }

            if (alpha > AlphaCeil)
            {
                alpha = AlphaCeil;
            }
        }

        double prev = _state.HasValue && double.IsFinite(_state.Frama) ? _state.Frama : price;
        double result = Math.FusedMultiplyAdd(prev, 1.0 - alpha, alpha * price);

        _state.Frama = result;
        _state.HasValue = true;

        Last = new TValue(input.Time, result);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        return Update(new TBar(input.Time, input.Value, input.Value, input.Value, input.Value, 0), isNew);
    }

    public TSeries Update(TBarSeries source)
    {
        if (source.Count == 0)
        {
            return new TSeries([], []);
        }

        int len = source.Count;
        var v = new double[len];

        Calculate(source.High.Values, source.Low.Values, _periodEven, v);

        var tList = new List<long>(len);
        var times = source.Open.Times;
        for (int i = 0; i < len; i++)
        {
            tList.Add(times[i]);
        }

        Reset();
        for (int i = 0; i < len; i++)
        {
            Update(source[i], isNew: true);
        }

        return new TSeries(tList, [.. v]);
    }

    public TSeries Update(TSeries source)
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

        Reset();
        for (int i = 0; i < len; i++)
        {
            TValue result = Update(source[i], isNew: true);
            vSpan[i] = result.Value;
        }

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs args) => Update(args.Value, args.IsNew);

    public static void Calculate(ReadOnlySpan<double> high, ReadOnlySpan<double> low, int period, Span<double> output)
    {
        if (high.Length != low.Length || high.Length != output.Length)
        {
            throw new ArgumentException("Input spans must have the same length.", nameof(output));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(period, 2);

        var frama = new Frama(period);
        for (int i = 0; i < high.Length; i++)
        {
            var bar = new TBar(DateTime.MinValue, high[i], high[i], low[i], low[i], 0);
            output[i] = frama.Update(bar, isNew: true).Value;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> source, Span<double> output, int period)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length.", nameof(output));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(period, 2);

        var frama = new Frama(period);
        for (int i = 0; i < source.Length; i++)
        {
            var bar = new TBar(DateTime.MinValue, source[i], source[i], source[i], source[i], 0);
            output[i] = frama.Update(bar, isNew: true).Value;
        }
    }

    public static TSeries Batch(TBarSeries source, int period)
    {
        if (source.Count == 0)
        {
            return new TSeries([], []);
        }

        int len = source.Count;
        var v = new double[len];
        Calculate(source.High.Values, source.Low.Values, period, v);

        var tList = new List<long>(len);
        var times = source.Open.Times;
        for (int i = 0; i < len; i++)
        {
            tList.Add(times[i]);
        }

        return new TSeries(tList, [.. v]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double GetMax(RingBuffer buffer, int length, int startOffset = -1)
    {
        int count = buffer.Count;
        if (count == 0 || length <= 0)
        {
            return double.NaN;
        }

        int capacity = buffer.Capacity;
        int start = buffer.StartIndex;
        ReadOnlySpan<double> data = buffer.InternalBuffer;

        int offset = startOffset >= 0 ? startOffset : count - length;

        double max = double.MinValue;
        for (int i = 0; i < length; i++)
        {
            int idx = start + offset + i;
            if (idx >= capacity)
            {
                idx -= capacity;
            }

            double v = data[idx];
            if (v > max)
            {
                max = v;
            }
        }

        return max;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double GetMin(RingBuffer buffer, int length, int startOffset = -1)
    {
        int count = buffer.Count;
        if (count == 0 || length <= 0)
        {
            return double.NaN;
        }

        int capacity = buffer.Capacity;
        int start = buffer.StartIndex;
        ReadOnlySpan<double> data = buffer.InternalBuffer;

        int offset = startOffset >= 0 ? startOffset : count - length;

        double min = double.MaxValue;
        for (int i = 0; i < length; i++)
        {
            int idx = start + offset + i;
            if (idx >= capacity)
            {
                idx -= capacity;
            }

            double v = data[idx];
            if (v < min)
            {
                min = v;
            }
        }

        return min;
    }

    /// <summary>
    /// Disposes the indicator and unsubscribes from the source.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing && _source != null)
            {
                _source.Pub -= _handler;
            }
            _disposed = true;
        }
    }
}
