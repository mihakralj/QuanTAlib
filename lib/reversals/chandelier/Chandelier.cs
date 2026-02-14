using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// CHANDELIER: Chandelier Exit
/// </summary>
/// <remarks>
/// ATR-based trailing stop indicator producing two overlay lines (ExitLong, ExitShort).
/// Developed by Charles Le Beau and popularized by Alexander Elder.
///
/// Calculation:
/// <code>
/// ATR         = Wilder's SMA-seeded RMA(TrueRange, period)
/// ExitLong    = HighestHigh(period) - multiplier × ATR
/// ExitShort   = LowestLow(period)   + multiplier × ATR
/// </code>
///
/// <b>Key characteristics:</b>
/// - O(1) amortized update via MonotonicDeque sliding windows
/// - Inline SMA-seeded Wilder ATR (skips first bar's TR, matches Skender/TA-Lib convention)
/// - Dual output: ExitLong (long position exit) and ExitShort (short position exit)
/// - Default parameters: period=22, multiplier=3.0
/// </remarks>
/// <seealso href="Chandelier.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Chandelier : ITValuePublisher
{
    private const int DefaultPeriod = 22;
    private const double DefaultMultiplier = 3.0;

    private readonly int _period;
    private readonly double _multiplier;

    // Buffers for highest-high / lowest-low over period
    private readonly double[] _hBuf;
    private readonly double[] _lBuf;
    private readonly MonotonicDeque _maxDequeHigh;
    private readonly MonotonicDeque _minDequeLow;

    private int _count;
    private long _index;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double PrevClose,
        bool IsInitialized,
        double LastValidHigh,
        double LastValidLow,
        double LastValidClose,
        double SumTr,
        double Atr);

    private State _s;
    private State _ps;

    private readonly TBarPublishedHandler _barHandler;

    /// <summary>Display name for the indicator.</summary>
    public string Name { get; }

    /// <summary>The lookback period.</summary>
    public int Period => _period;

    /// <summary>The ATR multiplier.</summary>
    public double Multiplier => _multiplier;

    /// <summary>Bars required for the indicator to warm up.</summary>
    public int WarmupPeriod { get; }

    /// <summary>Current exit level for long positions (green line).</summary>
    public double ExitLong { get; private set; }

    /// <summary>Current exit level for short positions (red line).</summary>
    public double ExitShort { get; private set; }

    /// <summary>Primary output value (ExitLong as TValue for overlay plotting).</summary>
    public TValue Last { get; private set; }

    /// <summary>True when enough bars have been processed for valid output.</summary>
    public bool IsHot => _count > _period;

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Creates a Chandelier Exit indicator.
    /// </summary>
    /// <param name="period">Lookback period for ATR and HH/LL (default 22).</param>
    /// <param name="multiplier">ATR multiplier (default 3.0).</param>
    public Chandelier(int period = DefaultPeriod, double multiplier = DefaultMultiplier)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0.", nameof(period));
        }
        if (multiplier <= 0)
        {
            throw new ArgumentException("Multiplier must be greater than 0.", nameof(multiplier));
        }

        _period = period;
        _multiplier = multiplier;

        _hBuf = new double[_period];
        _lBuf = new double[_period];
        _maxDequeHigh = new MonotonicDeque(_period);
        _minDequeLow = new MonotonicDeque(_period);

        _count = 0;
        _index = -1;
        _s = new State(double.NaN, false, double.NaN, double.NaN, double.NaN, 0.0, 0.0);
        _ps = _s;

        Name = $"Chandelier({period},{multiplier:F1})";
        WarmupPeriod = period + 1;
        _barHandler = HandleBar;
    }

    /// <summary>
    /// Creates a Chandelier Exit chained to a TBarSeries source.
    /// </summary>
    public Chandelier(TBarSeries source, int period = DefaultPeriod, double multiplier = DefaultMultiplier)
        : this(period, multiplier)
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

        // Step 1b: Inline SMA-seeded Wilder ATR (matches Skender/SuperTrend convention)
        // Bar 1 (_count==1): skip first bar's TR for initial SMA sum
        // Bars 2.._period+1: accumulate TR into SumTr, seed ATR = SumTr/period at bar _period+1
        // Bars _period+2+: Wilder RMA = (prevATR * (period-1) + TR) / period
        double atr;
        if (_count == 1)
        {
            // Skip first bar's TR for SMA calculation (Skender convention)
            atr = 0;
        }
        else if (_count <= _period + 1)
        {
            s.SumTr += tr;
            if (_count == _period + 1)
            {
                s.Atr = s.SumTr / _period;
            }
            atr = s.Atr;
        }
        else
        {
            // Wilder RMA: (prevAtr * (period - 1) + tr) / period
            double invPeriod = 1.0 / _period;
            s.Atr = Math.FusedMultiplyAdd(s.Atr, 1.0 - invPeriod, tr * invPeriod);
            atr = s.Atr;
        }

        // Step 2: Track highest-high and lowest-low over period
        int bufIdx = (int)(_index % _period);
        _hBuf[bufIdx] = high;
        _lBuf[bufIdx] = low;

        if (isNew)
        {
            _maxDequeHigh.PushMax(_index, high, _hBuf);
            _minDequeLow.PushMin(_index, low, _lBuf);
        }
        else
        {
            _maxDequeHigh.RebuildMax(_hBuf, _index, Math.Min(_count, _period));
            _minDequeLow.RebuildMin(_lBuf, _index, Math.Min(_count, _period));
        }

        double highestHigh = _maxDequeHigh.GetExtremum(_hBuf);
        double lowestLow = _minDequeLow.GetExtremum(_lBuf);

        // Step 3: Chandelier exits — no second-stage smoothing
        ExitLong = highestHigh - _multiplier * atr;
        ExitShort = lowestLow + _multiplier * atr;

        _s = s;

        Last = new TValue(input.Time, ExitLong);
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
            CollectionsMarshal.AsSpan(v), _period, _multiplier);

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
        Array.Clear(_hBuf);
        Array.Clear(_lBuf);
        _maxDequeHigh.Reset();
        _minDequeLow.Reset();
        _count = 0;
        _index = -1;
        _s = new State(double.NaN, false, double.NaN, double.NaN, double.NaN, 0.0, 0.0);
        _ps = _s;
        ExitLong = double.NaN;
        ExitShort = double.NaN;
        Last = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(
        ReadOnlySpan<double> open,
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        Span<double> output,
        int period,
        double multiplier = DefaultMultiplier)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0.", nameof(period));
        }
        if (multiplier <= 0)
        {
            throw new ArgumentException("Multiplier must be greater than 0.", nameof(multiplier));
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
        var indicator = new Chandelier(period, multiplier);

        long baseTime = DateTime.UtcNow.Ticks;
        for (int i = 0; i < len; i++)
        {
            _ = indicator.Update(
                new TBar(baseTime + i, open[i], high[i], low[i], close[i], 0),
                isNew: true);
            output[i] = indicator.ExitLong;
        }
    }

    public static TSeries Batch(TBarSeries source, int period = DefaultPeriod, double multiplier = DefaultMultiplier)
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
            CollectionsMarshal.AsSpan(v), period, multiplier);

        source.Times.CopyTo(CollectionsMarshal.AsSpan(t));

        return new TSeries(t, v);
    }

    public static (TSeries Results, Chandelier Indicator) Calculate(
        TBarSeries source, int period = DefaultPeriod, double multiplier = DefaultMultiplier)
    {
        var indicator = new Chandelier(period, multiplier);
        var results = indicator.Update(source);
        return (results, indicator);
    }
}
