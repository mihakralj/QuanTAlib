using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// CKSTOP: Chande Kroll Stop
/// </summary>
/// <remarks>
/// ATR-based trailing stop indicator producing two overlay lines (StopLong, StopShort).
/// Developed by Tushar Chande and Stanley Kroll ("The New Technical Trader", 1994).
///
/// Calculation:
/// <code>
/// Step 1: ATR = RMA(TrueRange, atrPeriod)
/// Step 2: first_high_stop = HighestHigh(atrPeriod) - multiplier × ATR
///         first_low_stop  = LowestLow(atrPeriod)  + multiplier × ATR
/// Step 3: StopShort = Highest(first_high_stop, stopPeriod)
///         StopLong  = Lowest(first_low_stop, stopPeriod)
/// </code>
///
/// <b>Key characteristics:</b>
/// - O(1) amortized update via MonotonicDeque sliding windows
/// - Composes internal RMA child indicator for ATR smoothing
/// - Dual output: StopLong (long position stop) and StopShort (short position stop)
/// - Default parameters: atrPeriod=10, multiplier=1.0, stopPeriod=9
/// </remarks>
/// <seealso href="Ckstop.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Ckstop : ITValuePublisher
{
    private const int DefaultAtrPeriod = 10;
    private const double DefaultMultiplier = 1.0;
    private const int DefaultStopPeriod = 9;

    private readonly int _atrPeriod;
    private readonly double _multiplier;
    private readonly int _stopPeriod;
    private readonly Rma _rma;

    // Buffers for highest-high / lowest-low over atrPeriod
    private readonly double[] _hBuf;
    private readonly double[] _lBuf;
    private readonly MonotonicDeque _maxDequeHigh;
    private readonly MonotonicDeque _minDequeLow;

    // Buffers for highest/lowest of initial stops over stopPeriod
    private readonly double[] _initStopShortBuf;
    private readonly double[] _initStopLongBuf;
    private readonly MonotonicDeque _maxDequeStopShort;
    private readonly MonotonicDeque _minDequeStopLong;

    private int _count;
    private long _index;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double PrevClose,
        bool IsInitialized,
        double LastValidHigh,
        double LastValidLow,
        double LastValidClose);

    private State _s;
    private State _ps;

    private readonly TBarPublishedHandler _barHandler;

    /// <summary>Display name for the indicator.</summary>
    public string Name { get; }

    /// <summary>The ATR lookback period (p).</summary>
    public int AtrPeriod => _atrPeriod;

    /// <summary>The stop multiplier (x).</summary>
    public double Multiplier => _multiplier;

    /// <summary>The stop smoothing period (q).</summary>
    public int StopPeriod => _stopPeriod;

    /// <summary>Bars required for the indicator to warm up.</summary>
    public int WarmupPeriod { get; }

    /// <summary>Current stop level for long positions (green line).</summary>
    public double StopLong { get; private set; }

    /// <summary>Current stop level for short positions (red line).</summary>
    public double StopShort { get; private set; }

    /// <summary>Primary output value (StopLong as TValue for overlay plotting).</summary>
    public TValue Last { get; private set; }

    /// <summary>True when enough bars have been processed for valid output.</summary>
    public bool IsHot => _count >= _atrPeriod + _stopPeriod;

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Creates a Chande Kroll Stop indicator.
    /// </summary>
    /// <param name="atrPeriod">ATR lookback period (default 10).</param>
    /// <param name="multiplier">ATR multiplier for initial stops (default 1.0).</param>
    /// <param name="stopPeriod">Smoothing period for final stops (default 9).</param>
    public Ckstop(int atrPeriod = DefaultAtrPeriod, double multiplier = DefaultMultiplier, int stopPeriod = DefaultStopPeriod)
    {
        if (atrPeriod <= 0)
        {
            throw new ArgumentException("ATR period must be greater than 0.", nameof(atrPeriod));
        }
        if (multiplier <= 0)
        {
            throw new ArgumentException("Multiplier must be greater than 0.", nameof(multiplier));
        }
        if (stopPeriod <= 0)
        {
            throw new ArgumentException("Stop period must be greater than 0.", nameof(stopPeriod));
        }

        _atrPeriod = atrPeriod;
        _multiplier = multiplier;
        _stopPeriod = stopPeriod;
        _rma = new Rma(atrPeriod);

        _hBuf = new double[_atrPeriod];
        _lBuf = new double[_atrPeriod];
        _maxDequeHigh = new MonotonicDeque(_atrPeriod);
        _minDequeLow = new MonotonicDeque(_atrPeriod);

        _initStopShortBuf = new double[_stopPeriod];
        _initStopLongBuf = new double[_stopPeriod];
        _maxDequeStopShort = new MonotonicDeque(_stopPeriod);
        _minDequeStopLong = new MonotonicDeque(_stopPeriod);

        _count = 0;
        _index = -1;
        _s = new State(double.NaN, false, double.NaN, double.NaN, double.NaN);
        _ps = _s;

        Name = $"Ckstop({atrPeriod},{multiplier:F1},{stopPeriod})";
        WarmupPeriod = atrPeriod + stopPeriod;
        _barHandler = HandleBar;
    }

    /// <summary>
    /// Creates a Chande Kroll Stop chained to a TBarSeries source.
    /// </summary>
    public Ckstop(TBarSeries source, int atrPeriod = DefaultAtrPeriod, double multiplier = DefaultMultiplier, int stopPeriod = DefaultStopPeriod)
        : this(atrPeriod, multiplier, stopPeriod)
    {
        Prime(source);
        source.Pub += _barHandler;
    }

    private void HandleBar(object? sender, in TBarEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PubEvent(TValue value, bool isNew = true) =>
        Pub?.Invoke(this, new TValueEventArgs { Value = value, IsNew = isNew });

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        if (isNew)
        {
            _ps = _s;
            _index++;
            _count++;
        }
        else
        {
            _s = _ps;
        }

        var s = _s;

        // Validate inputs — substitute last-valid on NaN/Infinity
        double high = input.High;
        double low = input.Low;
        double close = input.Close;

        if (double.IsFinite(high)) { s.LastValidHigh = high; }
        else { high = s.LastValidHigh; }

        if (double.IsFinite(low)) { s.LastValidLow = low; }
        else { low = s.LastValidLow; }

        if (double.IsFinite(close)) { s.LastValidClose = close; }
        else { close = s.LastValidClose; }

        // If still no valid data, return NaN
        if (double.IsNaN(high) || double.IsNaN(low) || double.IsNaN(close))
        {
            _s = s;
            Last = new TValue(input.Time, double.NaN);
            PubEvent(Last, isNew);
            return Last;
        }

        // Step 1: Compute True Range
        double tr;
        if (!s.IsInitialized)
        {
            tr = high - low;
            s.IsInitialized = true;
        }
        else
        {
            double hl = high - low;
            double hpc = Math.Abs(high - s.PrevClose);
            double lpc = Math.Abs(low - s.PrevClose);
            tr = Math.Max(hl, Math.Max(hpc, lpc));
        }

        if (isNew)
        {
            s.PrevClose = close;
        }

        // Step 1b: Smooth TR via RMA → ATR
        _ = _rma.Update(new TValue(input.Time, tr), isNew);
        double atr = _rma.Last.Value;

        // Step 2a: Track highest-high and lowest-low over atrPeriod
        int hBufIdx = (int)(_index % _atrPeriod);
        _hBuf[hBufIdx] = high;
        _lBuf[hBufIdx] = low;

        if (isNew)
        {
            _maxDequeHigh.PushMax(_index, high, _hBuf);
            _minDequeLow.PushMin(_index, low, _lBuf);
        }
        else
        {
            _maxDequeHigh.RebuildMax(_hBuf, _index, Math.Min(_count, _atrPeriod));
            _minDequeLow.RebuildMin(_lBuf, _index, Math.Min(_count, _atrPeriod));
        }

        double highestHigh = _maxDequeHigh.GetExtremum(_hBuf);
        double lowestLow = _minDequeLow.GetExtremum(_lBuf);

        // Step 2b: First (initial) stops
        double initStopShort = highestHigh - _multiplier * atr;
        double initStopLong = lowestLow + _multiplier * atr;

        // Step 3: Track highest/lowest of initial stops over stopPeriod
        int sBufIdx = (int)(_index % _stopPeriod);
        _initStopShortBuf[sBufIdx] = initStopShort;
        _initStopLongBuf[sBufIdx] = initStopLong;

        if (isNew)
        {
            _maxDequeStopShort.PushMax(_index, initStopShort, _initStopShortBuf);
            _minDequeStopLong.PushMin(_index, initStopLong, _initStopLongBuf);
        }
        else
        {
            _maxDequeStopShort.RebuildMax(_initStopShortBuf, _index, Math.Min(_count, _stopPeriod));
            _minDequeStopLong.RebuildMin(_initStopLongBuf, _index, Math.Min(_count, _stopPeriod));
        }

        StopShort = _maxDequeStopShort.GetExtremum(_initStopShortBuf);
        StopLong = _minDequeStopLong.GetExtremum(_initStopLongBuf);

        _s = s;

        Last = new TValue(input.Time, StopLong);
        PubEvent(Last, isNew);
        return Last;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true) =>
        Update(new TBar(input.Time, input.Value, input.Value, input.Value, input.Value, 0), isNew);

    public TSeries Update(TBarSeries source)
    {
        if (source.Count == 0)
        {
            return new TSeries([], []);
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);

        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        Batch(source.OpenValues, source.HighValues, source.LowValues, source.CloseValues,
            CollectionsMarshal.AsSpan(v), _atrPeriod, _multiplier, _stopPeriod);

        source.Times.CopyTo(CollectionsMarshal.AsSpan(t));

        // Prime internal state for continued streaming
        Prime(source);

        var lastTime = new DateTime(source.Times[^1], DateTimeKind.Utc);
        Last = new TValue(lastTime, CollectionsMarshal.AsSpan(v)[^1]);

        return new TSeries(t, v);
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

    public void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        Reset();

        if (source.Length == 0)
        {
            return;
        }

        long t = DateTime.UtcNow.Ticks;
        long stepTicks = (step ?? TimeSpan.FromMinutes(1)).Ticks;

        for (int i = 0; i < source.Length; i++)
        {
            double val = source[i];
            Update(new TBar(t, val, val, val, val, 0), isNew: true);
            t += stepTicks;
        }
    }

    public void Reset()
    {
        _rma.Reset();
        Array.Clear(_hBuf);
        Array.Clear(_lBuf);
        Array.Clear(_initStopShortBuf);
        Array.Clear(_initStopLongBuf);
        _maxDequeHigh.Reset();
        _minDequeLow.Reset();
        _maxDequeStopShort.Reset();
        _minDequeStopLong.Reset();
        _count = 0;
        _index = -1;
        _s = new State(double.NaN, false, double.NaN, double.NaN, double.NaN);
        _ps = _s;
        StopLong = double.NaN;
        StopShort = double.NaN;
        Last = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(
        ReadOnlySpan<double> open,
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        Span<double> output,
        int atrPeriod,
        double multiplier = DefaultMultiplier,
        int stopPeriod = DefaultStopPeriod)
    {
        if (atrPeriod <= 0)
        {
            throw new ArgumentException("ATR period must be greater than 0.", nameof(atrPeriod));
        }
        if (multiplier <= 0)
        {
            throw new ArgumentException("Multiplier must be greater than 0.", nameof(multiplier));
        }
        if (stopPeriod <= 0)
        {
            throw new ArgumentException("Stop period must be greater than 0.", nameof(stopPeriod));
        }
        if (high.Length != low.Length || high.Length != close.Length || high.Length != open.Length)
        {
            throw new ArgumentException("Input spans must have the same length.", nameof(high));
        }
        if (output.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least as long as input.", nameof(output));
        }

        int len = high.Length;
        if (len == 0)
        {
            return;
        }

        // Compute via streaming instance for correctness
        var indicator = new Ckstop(atrPeriod, multiplier, stopPeriod);

        long baseTime = DateTime.UtcNow.Ticks;
        for (int i = 0; i < len; i++)
        {
            _ = indicator.Update(
                new TBar(baseTime + i, open[i], high[i], low[i], close[i], 0),
                isNew: true);
            output[i] = indicator.StopLong;
        }
    }

    public static TSeries Batch(TBarSeries source, int atrPeriod = DefaultAtrPeriod, double multiplier = DefaultMultiplier, int stopPeriod = DefaultStopPeriod)
    {
        if (source == null || source.Count == 0)
        {
            return new TSeries([], []);
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);

        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        Batch(source.OpenValues, source.HighValues, source.LowValues, source.CloseValues,
            CollectionsMarshal.AsSpan(v), atrPeriod, multiplier, stopPeriod);

        source.Times.CopyTo(CollectionsMarshal.AsSpan(t));

        return new TSeries(t, v);
    }

    public static (TSeries Results, Ckstop Indicator) Calculate(
        TBarSeries source, int atrPeriod = DefaultAtrPeriod, double multiplier = DefaultMultiplier, int stopPeriod = DefaultStopPeriod)
    {
        var indicator = new Ckstop(atrPeriod, multiplier, stopPeriod);
        var results = indicator.Update(source);
        return (results, indicator);
    }
}
