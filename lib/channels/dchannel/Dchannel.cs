using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// DCHANNEL: Donchian Channels
/// Upper = rolling highest high; Lower = rolling lowest low; Middle = (Upper + Lower) / 2.
/// Streaming path uses monotonic deques for O(1) amortized updates; corrections (isNew=false)
/// rebuild deques without allocations.
/// </summary>
[SkipLocalsInit]
public sealed class Dchannel : ITValuePublisher
{
    private readonly int _period;
    private readonly double[] _hBuf;
    private readonly double[] _lBuf;
    private readonly MonotonicDeque _maxDeque;
    private readonly MonotonicDeque _minDeque;

    // Rolling counters
    private int _count;
    private long _index;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(double LastValidHigh, double LastValidLow, bool IsHot);
    private State _state;
    private State _p_state;

    private readonly TBarPublishedHandler _barHandler;

    public string Name { get; }
    public int WarmupPeriod { get; }
    public TValue Last { get; private set; }
    public TValue Upper { get; private set; }
    public TValue Lower { get; private set; }
    public bool IsHot => _count >= _period;

    public event TValuePublishedHandler? Pub;

    public Dchannel(int period)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _period = period;
        _hBuf = new double[_period];
        _lBuf = new double[_period];
        _maxDeque = new MonotonicDeque(_period);
        _minDeque = new MonotonicDeque(_period);
        _count = 0;
        _index = -1;
        _state = new State(double.NaN, double.NaN, false);
        _p_state = _state;

        Name = $"Dchannel({period})";
        WarmupPeriod = period;
        _barHandler = HandleBar;
    }

    public Dchannel(TBarSeries source, int period) : this(period)
    {
        Prime(source);
        source.Pub += _barHandler;
    }

    private void HandleBar(object? sender, in TBarEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PubEvent(TValue value, bool isNew = true) => Pub?.Invoke(this, new TValueEventArgs { Value = value, IsNew = isNew });

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private (double high, double low) GetValid(double high, double low)
    {
        if (double.IsFinite(high))
        {
            _state = _state with { LastValidHigh = high };
        }
        else
        {
            high = _state.LastValidHigh;
        }

        if (double.IsFinite(low))
        {
            _state = _state with { LastValidLow = low };
        }
        else
        {
            low = _state.LastValidLow;
        }

        return (high, low);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
            _index++;
            if (_count < _period)
            {
                _count++;
            }
        }
        else
        {
            _state = _p_state;
        }

        int bufIdx = (int)(_index % _period);
        var (high, low) = GetValid(input.High, input.Low);

        // If still no valid data, return NaN placeholders
        if (double.IsNaN(high) || double.IsNaN(low))
        {
            Last = new TValue(input.Time, double.NaN);
            Upper = new TValue(input.Time, double.NaN);
            Lower = new TValue(input.Time, double.NaN);
            PubEvent(Last, isNew);
            return Last;
        }

        _hBuf[bufIdx] = high;
        _lBuf[bufIdx] = low;

        if (isNew)
        {
            _maxDeque.PushMax(_index, high, _hBuf);
            _minDeque.PushMin(_index, low, _lBuf);
        }
        else
        {
            // Correcting current bar: rebuild deques to maintain consistency
            _maxDeque.RebuildMax(_hBuf, _index, _count);
            _minDeque.RebuildMin(_lBuf, _index, _count);
        }

        double top = _maxDeque.GetExtremum(_hBuf);
        double bot = _minDeque.GetExtremum(_lBuf);
        double mid = (top + bot) * 0.5;

        if (!_state.IsHot && _count >= _period)
        {
            _state = _state with { IsHot = true };
        }

        Last = new TValue(input.Time, mid);
        Upper = new TValue(input.Time, top);
        Lower = new TValue(input.Time, bot);

        PubEvent(Last, isNew);
        return Last;
    }

    public (TSeries Middle, TSeries Upper, TSeries Lower) Update(TBarSeries source)
    {
        if (source.Count == 0)
        {
            return (new TSeries([], []), new TSeries([], []), new TSeries([], []));
        }

        int len = source.Count;
        var tMiddle = new List<long>(len);
        var vMiddle = new List<double>(len);
        var tUpper = new List<long>(len);
        var vUpper = new List<double>(len);
        var tLower = new List<long>(len);
        var vLower = new List<double>(len);

        CollectionsMarshal.SetCount(tMiddle, len);
        CollectionsMarshal.SetCount(vMiddle, len);
        CollectionsMarshal.SetCount(tUpper, len);
        CollectionsMarshal.SetCount(vUpper, len);
        CollectionsMarshal.SetCount(tLower, len);
        CollectionsMarshal.SetCount(vLower, len);

        var tSpan = CollectionsMarshal.AsSpan(tMiddle);
        var vMiddleSpan = CollectionsMarshal.AsSpan(vMiddle);
        var vUpperSpan = CollectionsMarshal.AsSpan(vUpper);
        var vLowerSpan = CollectionsMarshal.AsSpan(vLower);

        Batch(source.HighValues, source.LowValues, vMiddleSpan, vUpperSpan, vLowerSpan, _period);

        source.Times.CopyTo(tSpan);
        tSpan.CopyTo(CollectionsMarshal.AsSpan(tUpper));
        tSpan.CopyTo(CollectionsMarshal.AsSpan(tLower));

        // Prime internal state for continued streaming
        Prime(source);

        var lastTime = new DateTime(source.Times[^1], DateTimeKind.Utc);
        Last = new TValue(lastTime, vMiddleSpan[^1]);
        Upper = new TValue(lastTime, vUpperSpan[^1]);
        Lower = new TValue(lastTime, vLowerSpan[^1]);

        return (new TSeries(tMiddle, vMiddle), new TSeries(tUpper, vUpper), new TSeries(tLower, vLower));
    }

    public void Prime(TBarSeries source)
    {
        Reset();

        if (source.Count == 0)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            Update(source[i], isNew: true);
        }
    }

    public void Reset()
    {
        Array.Clear(_hBuf);
        Array.Clear(_lBuf);
        _maxDeque.Reset();
        _minDeque.Reset();
        _count = 0;
        _index = -1;
        _state = new State(double.NaN, double.NaN, false);
        _p_state = _state;
        Last = default;
        Upper = default;
        Lower = default;
    }

    /// <summary>
    /// Batch calculation using spans (zero allocation).
    /// </summary>
    public static void Batch(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        Span<double> middle,
        Span<double> upper,
        Span<double> lower,
        int period)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        if (high.Length != low.Length)
        {
            throw new ArgumentException("High and Low spans must have the same length", nameof(high));
        }

        if (middle.Length < high.Length || upper.Length < high.Length || lower.Length < high.Length)
        {
            throw new ArgumentException("Output spans must be at least as long as inputs", nameof(middle));
        }

        int len = high.Length;
        if (len == 0)
        {
            return;
        }

        double[] top = ArrayPool<double>.Shared.Rent(len);
        double[] bot = ArrayPool<double>.Shared.Rent(len);

        try
        {
            Highest.Batch(high, top.AsSpan(0, len), period);
            Lowest.Batch(low, bot.AsSpan(0, len), period);

            for (int i = 0; i < len; i++)
            {
                double u = top[i];
                double l = bot[i];
                middle[i] = (u + l) * 0.5;
                upper[i] = u;
                lower[i] = l;
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(top);
            ArrayPool<double>.Shared.Return(bot);
        }
    }

    public static (TSeries Middle, TSeries Upper, TSeries Lower) Batch(TBarSeries source, int period)
    {
        int len = source.Count;
        var tMiddle = new List<long>(len);
        var vMiddle = new List<double>(len);
        var tUpper = new List<long>(len);
        var vUpper = new List<double>(len);
        var tLower = new List<long>(len);
        var vLower = new List<double>(len);

        CollectionsMarshal.SetCount(tMiddle, len);
        CollectionsMarshal.SetCount(vMiddle, len);
        CollectionsMarshal.SetCount(tUpper, len);
        CollectionsMarshal.SetCount(vUpper, len);
        CollectionsMarshal.SetCount(tLower, len);
        CollectionsMarshal.SetCount(vLower, len);

        Batch(source.HighValues, source.LowValues,
            CollectionsMarshal.AsSpan(vMiddle),
            CollectionsMarshal.AsSpan(vUpper),
            CollectionsMarshal.AsSpan(vLower),
            period);

        source.Times.CopyTo(CollectionsMarshal.AsSpan(tMiddle));
        CollectionsMarshal.AsSpan(tMiddle).CopyTo(CollectionsMarshal.AsSpan(tUpper));
        CollectionsMarshal.AsSpan(tMiddle).CopyTo(CollectionsMarshal.AsSpan(tLower));

        return (new TSeries(tMiddle, vMiddle), new TSeries(tUpper, vUpper), new TSeries(tLower, vLower));
    }

    public static ((TSeries Middle, TSeries Upper, TSeries Lower) Results, Dchannel Indicator) Calculate(TBarSeries source, int period)
    {
        var indicator = new Dchannel(source, period);
        var results = indicator.Update(source);
        return (results, indicator);
    }
}
