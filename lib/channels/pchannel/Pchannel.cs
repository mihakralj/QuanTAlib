using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// PCHANNEL: Price Channel
/// Upper = rolling highest high; Lower = rolling lowest low; Middle = (Upper + Lower) / 2.
/// Functionally equivalent to Donchian Channels (DCHANNEL).
/// Streaming path uses monotonic deques for O(1) amortized updates; corrections (isNew=false)
/// rebuild deques without allocations.
/// </summary>
[SkipLocalsInit]
public sealed class Pchannel : ITValuePublisher
{
    private readonly int _period;
    private readonly double[] _hBuf;
    private readonly double[] _lBuf;
    private readonly int[] _hDeque;
    private readonly int[] _lDeque;

    // Queue state
    private int _hHead;
    private int _hCount;
    private int _lHead;
    private int _lCount;

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

    public Pchannel(int period)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _period = period;
        _hBuf = new double[_period];
        _lBuf = new double[_period];
        _hDeque = new int[_period];
        _lDeque = new int[_period];
        _hHead = 0;
        _lHead = 0;
        _hCount = 0;
        _lCount = 0;
        _count = 0;
        _index = -1;
        _state = new State(double.NaN, double.NaN, false);
        _p_state = _state;

        Name = $"Pchannel({period})";
        WarmupPeriod = period;
        _barHandler = HandleBar;
    }

    public Pchannel(TBarSeries source, int period) : this(period)
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
    private void PushMax(long logicalIndex, double value)
    {
        long expire = logicalIndex - _period;
        while (_hCount > 0 && _hDeque[_hHead] <= expire)
        {
            _hHead = (_hHead + 1) % _period;
            _hCount--;
        }

        int backIdx;
        while (_hCount > 0)
        {
            backIdx = (_hHead + _hCount - 1) % _period;
            int bufIdx = _hDeque[backIdx] % _period;
            if (_hBuf[bufIdx] <= value)
            {
                _hCount--;
            }
            else
            {
                break;
            }
        }

        int tail = (_hHead + _hCount) % _period;
        _hDeque[tail] = (int)logicalIndex;
        _hCount++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PushMin(long logicalIndex, double value)
    {
        long expire = logicalIndex - _period;
        while (_lCount > 0 && _lDeque[_lHead] <= expire)
        {
            _lHead = (_lHead + 1) % _period;
            _lCount--;
        }

        int backIdx;
        while (_lCount > 0)
        {
            backIdx = (_lHead + _lCount - 1) % _period;
            int bufIdx = _lDeque[backIdx] % _period;
            if (_lBuf[bufIdx] >= value)
            {
                _lCount--;
            }
            else
            {
                break;
            }
        }

        int tail = (_lHead + _lCount) % _period;
        _lDeque[tail] = (int)logicalIndex;
        _lCount++;
    }

    private void RebuildDeques()
    {
        _hHead = 0;
        _lHead = 0;
        _hCount = 0;
        _lCount = 0;

        if (_count == 0)
        {
            return;
        }

        long startLogical = _index - _count + 1;
        for (int i = 0; i < _count; i++)
        {
            long logicalIndex = startLogical + i;
            int bufIdx = (int)(logicalIndex % _period);
            double h = _hBuf[bufIdx];
            double l = _lBuf[bufIdx];
            PushMax(logicalIndex, h);
            PushMin(logicalIndex, l);
        }
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
            PushMax(_index, high);
            PushMin(_index, low);
        }
        else
        {
            RebuildDeques();
        }

        double top = _hBuf[_hDeque[_hHead] % _period];
        double bot = _lBuf[_lDeque[_lHead] % _period];
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
        _hHead = 0;
        _lHead = 0;
        _hCount = 0;
        _lCount = 0;
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
            Highest.Calculate(high, top.AsSpan(0, len), period);
            Lowest.Calculate(low, bot.AsSpan(0, len), period);

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

    public static ((TSeries Middle, TSeries Upper, TSeries Lower) Results, Pchannel Indicator) Calculate(TBarSeries source, int period)
    {
        var indicator = new Pchannel(source, period);
        var results = indicator.Update(source);
        return (results, indicator);
    }
}
