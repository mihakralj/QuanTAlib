using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// FCB: Fractal Chaos Bands
/// Tracks the highest fractal high and lowest fractal low over a lookback period.
/// A fractal high occurs when high[1] > high[0] and high[1] > high[2] (3-bar pattern).
/// A fractal low occurs when low[1] < low[0] and low[1] < low[2] (3-bar pattern).
/// Uses monotonic deques for O(1) amortized complexity.
/// </summary>
[SkipLocalsInit]
public sealed class Fcb : ITValuePublisher
{
    private readonly int _period;

    // Circular buffers for fractal values
    private readonly double[] _hBuf;
    private readonly double[] _lBuf;

    // Monotonic deques (store indices as long to avoid truncation)
    private readonly long[] _hDeque;
    private readonly long[] _lDeque;

    // Deque state
    private int _hHead;
    private int _hCount;
    private int _lHead;
    private int _lCount;

    // Rolling counters
    private int _count;
    private long _index;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double High0, double High1, double High2,
        double Low0, double Low1, double Low2,
        double HiFractal, double LoFractal,
        double LastValidHigh, double LastValidLow,
        bool IsHot);

    private State _state;
    private State _p_state;

    private readonly TBarPublishedHandler _barHandler;

    public string Name { get; }
    public int WarmupPeriod { get; }
    public TValue Last { get; private set; }
    public TValue Upper { get; private set; }
    public TValue Lower { get; private set; }
    public bool IsHot => _state.IsHot;

    public event TValuePublishedHandler? Pub;

    public Fcb(int period = 20)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be >= 1.");
        }

        _period = period;
        WarmupPeriod = period + 2; // Need 2 extra bars for fractal detection

        _hBuf = new double[_period];
        _lBuf = new double[_period];
        _hDeque = new long[_period];
        _lDeque = new long[_period];

        Name = $"Fcb({period})";
        _barHandler = HandleBar;

        Reset();
    }

    public Fcb(TBarSeries source, int period = 20) : this(period)
    {
        Prime(source);
        source.Pub += _barHandler;
    }

    private void HandleBar(object? sender, in TBarEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PubEvent(TValue value, bool isNew = true) =>
        Pub?.Invoke(this, new TValueEventArgs { Value = value, IsNew = isNew });

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
        // Expire old indices
        long expire = logicalIndex - _period;
        while (_hCount > 0 && _hDeque[_hHead] <= expire)
        {
            _hHead = (_hHead + 1) % _period;
            _hCount--;
        }

        // Maintain monotonic non-increasing deque
        while (_hCount > 0)
        {
            int backIdx = (_hHead + _hCount - 1) % _period;
            int bufIdx = (int)(_hDeque[backIdx] % _period);
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
        _hDeque[tail] = logicalIndex;
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

        while (_lCount > 0)
        {
            int backIdx = (_lHead + _lCount - 1) % _period;
            int bufIdx = (int)(_lDeque[backIdx] % _period);
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
        _lDeque[tail] = logicalIndex;
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
        _state = new State(0, 0, 0, 0, 0, 0, double.NaN, double.NaN, double.NaN, double.NaN, false);
        _p_state = _state;
        Last = default;
        Upper = default;
        Lower = default;
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

        var (high, low) = GetValid(input.High, input.Low);

        // Shift high/low history
        _state = _state with
        {
            High2 = _state.High1,
            High1 = _state.High0,
            High0 = high,
            Low2 = _state.Low1,
            Low1 = _state.Low0,
            Low0 = low
        };

        // Initialize fractal values on first bar
        if (_index == 0)
        {
            _state = _state with
            {
                HiFractal = high,
                LoFractal = low
            };
        }

        // Detect fractals (need at least 3 bars: indices 0, 1, 2 means _index >= 2)
        if (_index >= 2)
        {
            // Fractal high: high[1] > high[2] and high[1] > high[0]
            if (_state.High1 > _state.High2 && _state.High1 > _state.High0)
            {
                _state = _state with { HiFractal = _state.High1 };
            }

            // Fractal low: low[1] < low[2] and low[1] < low[0]
            if (_state.Low1 < _state.Low2 && _state.Low1 < _state.Low0)
            {
                _state = _state with { LoFractal = _state.Low1 };
            }
        }

        // Handle invalid fractal values (shouldn't happen after warmup)
        double hiFrac = double.IsFinite(_state.HiFractal) ? _state.HiFractal : high;
        double loFrac = double.IsFinite(_state.LoFractal) ? _state.LoFractal : low;

        int bufIdx = (int)(_index % _period);
        _hBuf[bufIdx] = hiFrac;
        _lBuf[bufIdx] = loFrac;

        if (isNew)
        {
            PushMax(_index, hiFrac);
            PushMin(_index, loFrac);
        }
        else
        {
            RebuildDeques();
        }

        double top = _hBuf[(int)(_hDeque[_hHead] % _period)];
        double bot = _lBuf[(int)(_lDeque[_lHead] % _period)];
        double mid = (top + bot) * 0.5;

        if (!_state.IsHot && _index + 1 >= WarmupPeriod)
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
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be >= 1.");
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

        // Allocate buffers for fractal tracking and deques
        double[] hBuf = ArrayPool<double>.Shared.Rent(period);
        double[] lBuf = ArrayPool<double>.Shared.Rent(period);
        long[] hDeque = ArrayPool<long>.Shared.Rent(period);
        long[] lDeque = ArrayPool<long>.Shared.Rent(period);

        try
        {
            int hHead = 0, hCount = 0;
            int lHead = 0, lCount = 0;

            double h0 = 0, h1 = 0, h2 = 0;
            double l0 = 0, l1 = 0, l2 = 0;
            double hiFractal = high[0];
            double loFractal = low[0];

            for (int i = 0; i < len; i++)
            {
                // Shift history
                h2 = h1;
                h1 = h0;
                h0 = high[i];

                l2 = l1;
                l1 = l0;
                l0 = low[i];

                // Detect fractals after 3 bars
                if (i >= 2)
                {
                    if (h1 > h2 && h1 > h0)
                    {
                        hiFractal = h1;
                    }

                    if (l1 < l2 && l1 < l0)
                    {
                        loFractal = l1;
                    }
                }

                int bufIdx = i % period;
                hBuf[bufIdx] = hiFractal;
                lBuf[bufIdx] = loFractal;

                // Push to max deque
                long expire = i - period;
                while (hCount > 0 && hDeque[hHead] <= expire)
                {
                    hHead = (hHead + 1) % period;
                    hCount--;
                }
                while (hCount > 0)
                {
                    int backIdx = (hHead + hCount - 1) % period;
                    int bIdx = (int)(hDeque[backIdx] % period);
                    if (hBuf[bIdx] <= hiFractal)
                    {
                        hCount--;
                    }
                    else
                    {
                        break;
                    }
                }
                int tail = (hHead + hCount) % period;
                hDeque[tail] = i;
                hCount++;

                // Push to min deque
                while (lCount > 0 && lDeque[lHead] <= expire)
                {
                    lHead = (lHead + 1) % period;
                    lCount--;
                }
                while (lCount > 0)
                {
                    int backIdx = (lHead + lCount - 1) % period;
                    int bIdx = (int)(lDeque[backIdx] % period);
                    if (lBuf[bIdx] >= loFractal)
                    {
                        lCount--;
                    }
                    else
                    {
                        break;
                    }
                }
                tail = (lHead + lCount) % period;
                lDeque[tail] = i;
                lCount++;

                double top = hBuf[(int)(hDeque[hHead] % period)];
                double bot = lBuf[(int)(lDeque[lHead] % period)];
                upper[i] = top;
                lower[i] = bot;
                middle[i] = (top + bot) * 0.5;
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(hBuf);
            ArrayPool<double>.Shared.Return(lBuf);
            ArrayPool<long>.Shared.Return(hDeque);
            ArrayPool<long>.Shared.Return(lDeque);
        }
    }

    public static (TSeries Middle, TSeries Upper, TSeries Lower) Batch(TBarSeries source, int period = 20)
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

    public static ((TSeries Middle, TSeries Upper, TSeries Lower) Results, Fcb Indicator) Calculate(TBarSeries source, int period = 20)
    {
        // Use parameterless constructor to avoid double-priming:
        // The Fcb(source, period) constructor already calls Prime(source),
        // so calling Update(source) afterwards would Prime again.
        var indicator = new Fcb(period);
        var results = indicator.Update(source);
        return (results, indicator);
    }
}
