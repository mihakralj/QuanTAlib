using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// KCHANNEL: Keltner Channel
/// A volatility-based envelope using EMA as the middle line and ATR for band width.
/// Middle = EMA(source, period) with warmup compensation
/// Upper = Middle + (multiplier × ATR)
/// Lower = Middle - (multiplier × ATR)
/// ATR uses RMA (Wilder's smoothing) with warmup compensation.
/// </summary>
[SkipLocalsInit]
public sealed class Kchannel : ITValuePublisher
{
    private readonly int _period;
    private readonly double _multiplier;
    private readonly double _emaAlpha;
    private readonly double _atrAlpha;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double EmaSum,
        double EmaWeight,
        double RawRma,
        double E,
        double PrevClose,
        double LastValidClose,
        double LastValidHigh,
        double LastValidLow,
        int Bars,
        bool IsHot);

    private State _state;
    private State _p_state;

    private readonly TBarPublishedHandler _barHandler;

    private const double Epsilon = 1e-10;

    public string Name { get; }
    public int WarmupPeriod { get; }
    public TValue Last { get; private set; }
    public TValue Upper { get; private set; }
    public TValue Lower { get; private set; }
    public bool IsHot => _state.IsHot;

    public event TValuePublishedHandler? Pub;

    public Kchannel(int period = 20, double multiplier = 2.0)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be >= 1.");
        }

        if (multiplier <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(multiplier), "Multiplier must be > 0.");
        }

        _period = period;
        _multiplier = multiplier;
        _emaAlpha = 2.0 / (period + 1);
        _atrAlpha = 1.0 / period;

        WarmupPeriod = period * 2;

        Name = $"Kchannel({period},{multiplier})";
        _barHandler = HandleBar;

        Reset();
    }

    public Kchannel(TBarSeries source, int period = 20, double multiplier = 2.0) : this(period, multiplier)
    {
        Prime(source);
        source.Pub += _barHandler;
    }

    private void HandleBar(object? sender, in TBarEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PubEvent(TValue value, bool isNew = true) =>
        Pub?.Invoke(this, new TValueEventArgs { Value = value, IsNew = isNew });

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _state = new State(0, 0, 0, 1.0, double.NaN, double.NaN, double.NaN, double.NaN, 0, false);
        _p_state = _state;
        Last = default;
        Upper = default;
        Lower = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private (double close, double high, double low) GetValid(double close, double high, double low)
    {
        if (double.IsFinite(close))
        {
            _state = _state with { LastValidClose = close };
        }
        else
        {
            close = _state.LastValidClose;
        }

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

        return (close, high, low);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
        }
        else
        {
            _state = _p_state;
        }

        var (close, high, low) = GetValid(input.Close, input.High, input.Low);

        // Handle first bar
        if (_state.Bars == 0)
        {
            _state = _state with
            {
                EmaSum = close,
                EmaWeight = 1.0,
                RawRma = 0.0,
                E = 1.0,
                PrevClose = close,
                Bars = 1
            };

            double ema = close;
            Last = new TValue(input.Time, ema);
            Upper = new TValue(input.Time, ema);
            Lower = new TValue(input.Time, ema);
            PubEvent(Last, isNew);
            return Last;
        }

        if (isNew)
        {
            _state = _state with { Bars = _state.Bars + 1 };
        }

        // EMA with warmup compensation (sum/weight approach)
        double newSum = Math.FusedMultiplyAdd(_state.EmaSum, 1.0 - _emaAlpha, close * _emaAlpha);
        double newWeight = Math.FusedMultiplyAdd(_state.EmaWeight, 1.0 - _emaAlpha, _emaAlpha);
        double emaValue = newSum / newWeight;

        // True Range
        double prevClose = _state.PrevClose;
        double tr1 = high - low;
        double tr2 = Math.Abs(high - prevClose);
        double tr3 = Math.Abs(low - prevClose);
        double trueRange = Math.Max(tr1, Math.Max(tr2, tr3));

        // ATR using RMA with warmup compensation
        double newRawRma = (_state.RawRma * (_period - 1) + trueRange) / _period;
        double newE = (1.0 - _atrAlpha) * _state.E;
        double atrValue = newE > Epsilon ? newRawRma / (1.0 - newE) : newRawRma;

        // Update state
        _state = _state with
        {
            EmaSum = newSum,
            EmaWeight = newWeight,
            RawRma = newRawRma,
            E = newE,
            PrevClose = close
        };

        // Calculate bands
        double width = _multiplier * atrValue;
        double upper = emaValue + width;
        double lower = emaValue - width;

        if (!_state.IsHot && _state.Bars >= WarmupPeriod)
        {
            _state = _state with { IsHot = true };
        }

        Last = new TValue(input.Time, emaValue);
        Upper = new TValue(input.Time, upper);
        Lower = new TValue(input.Time, lower);

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

        Batch(source.HighValues, source.LowValues, source.CloseValues,
              vMiddleSpan, vUpperSpan, vLowerSpan, _period, _multiplier);

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
        ReadOnlySpan<double> close,
        Span<double> middle,
        Span<double> upper,
        Span<double> lower,
        int period,
        double multiplier = 2.0)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be >= 1.");
        }

        if (multiplier <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(multiplier), "Multiplier must be > 0.");
        }

        if (high.Length != low.Length || high.Length != close.Length)
        {
            throw new ArgumentException("High, Low, and Close spans must have the same length", nameof(high));
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

        double emaAlpha = 2.0 / (period + 1);
        double atrAlpha = 1.0 / period;

        double emaSum = close[0];
        double emaWeight = 1.0;
        double rawRma = 0.0;
        double e = 1.0;
        double prevClose = close[0];

        // First bar
        middle[0] = close[0];
        upper[0] = close[0];
        lower[0] = close[0];

        for (int i = 1; i < len; i++)
        {
            double c = close[i];
            double h = high[i];
            double l = low[i];

            // EMA with warmup
            emaSum = Math.FusedMultiplyAdd(emaSum, 1.0 - emaAlpha, c * emaAlpha);
            emaWeight = Math.FusedMultiplyAdd(emaWeight, 1.0 - emaAlpha, emaAlpha);
            double ema = emaSum / emaWeight;

            // True Range
            double tr1 = h - l;
            double tr2 = Math.Abs(h - prevClose);
            double tr3 = Math.Abs(l - prevClose);
            double tr = Math.Max(tr1, Math.Max(tr2, tr3));

            // ATR (RMA with warmup)
            rawRma = (rawRma * (period - 1) + tr) / period;
            e = (1.0 - atrAlpha) * e;
            double atr = e > Epsilon ? rawRma / (1.0 - e) : rawRma;

            prevClose = c;

            double width = multiplier * atr;
            middle[i] = ema;
            upper[i] = ema + width;
            lower[i] = ema - width;
        }
    }

    public static (TSeries Middle, TSeries Upper, TSeries Lower) Batch(TBarSeries source, int period = 20, double multiplier = 2.0)
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

        Batch(source.HighValues, source.LowValues, source.CloseValues,
              CollectionsMarshal.AsSpan(vMiddle),
              CollectionsMarshal.AsSpan(vUpper),
              CollectionsMarshal.AsSpan(vLower),
              period, multiplier);

        source.Times.CopyTo(CollectionsMarshal.AsSpan(tMiddle));
        CollectionsMarshal.AsSpan(tMiddle).CopyTo(CollectionsMarshal.AsSpan(tUpper));
        CollectionsMarshal.AsSpan(tMiddle).CopyTo(CollectionsMarshal.AsSpan(tLower));

        return (new TSeries(tMiddle, vMiddle), new TSeries(tUpper, vUpper), new TSeries(tLower, vLower));
    }

    public static ((TSeries Middle, TSeries Upper, TSeries Lower) Results, Kchannel Indicator) Calculate(TBarSeries source, int period = 20, double multiplier = 2.0)
    {
        var indicator = new Kchannel(source, period, multiplier);
        var results = indicator.Update(source);
        return (results, indicator);
    }
}
