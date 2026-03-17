using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// VSTOP: Volatility Stop (Wilder's Volatility System)
/// ATR-based trailing stop that tracks trend direction and flips on reversal.
/// Uses Significant Close (SIC) tracking: highest close in uptrend, lowest in downtrend.
/// SAR = SIC ± ATR × multiplier.
/// </summary>
/// <seealso href="https://dotnet.stockindicators.dev/indicators/VolatilityStop/">Skender reference</seealso>
[SkipLocalsInit]
public sealed class Vstop : ITValuePublisher
{
    private readonly int _period;
    private readonly double _multiplier;
    private readonly Atr _atr;
    private int _count;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        bool IsLong,
        double Sic,
        double LastValidHigh,
        double LastValidLow,
        double LastValidClose);

    private State _s;
    private State _ps;

    private readonly TBarPublishedHandler _barHandler;

    /// <summary>Display name.</summary>
    public string Name { get; }

    /// <summary>ATR lookback period.</summary>
    public int Period => _period;

    /// <summary>ATR multiplier for stop offset.</summary>
    public double Multiplier => _multiplier;

    /// <summary>Bars required for valid output.</summary>
    public int WarmupPeriod { get; }

    /// <summary>Current SAR (Stop and Reverse) value.</summary>
    public double SarValue { get; private set; }

    /// <summary>True when the indicator is in uptrend mode.</summary>
    public bool IsLong => _s.IsLong;

    /// <summary>True when a stop reversal occurred on the current bar.</summary>
    public bool IsStop { get; private set; }

    /// <summary>Primary output value (SAR as TValue for overlay plotting).</summary>
    public TValue Last { get; private set; }

    /// <summary>True when enough bars have been processed.</summary>
    public bool IsHot => _count >= _period;

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Creates a Volatility Stop indicator.
    /// </summary>
    /// <param name="period">ATR lookback period (default 7).</param>
    /// <param name="multiplier">ATR multiplier (default 3.0).</param>
    public Vstop(int period = 7, double multiplier = 3.0)
    {
        if (period <= 1)
        {
            throw new ArgumentException("Period must be greater than 1.", nameof(period));
        }
        if (multiplier <= 0)
        {
            throw new ArgumentException("Multiplier must be greater than 0.", nameof(multiplier));
        }

        _period = period;
        _multiplier = multiplier;
        _atr = new Atr(period);
        _count = 0;

        _s = new State(
            IsLong: true,
            Sic: double.NaN,
            LastValidHigh: double.NaN,
            LastValidLow: double.NaN,
            LastValidClose: double.NaN);
        _ps = _s;

        Name = $"Vstop({period},{multiplier:F1})";
        WarmupPeriod = period;
        SarValue = double.NaN;
        _barHandler = HandleBar;
    }

    /// <summary>
    /// Creates a Volatility Stop chained to a TBarSeries source.
    /// </summary>
    public Vstop(TBarSeries source, int period = 7, double multiplier = 3.0)
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

        if (double.IsNaN(high) || double.IsNaN(low) || double.IsNaN(close))
        {
            _s = s;
            Last = new TValue(input.Time, double.NaN);
            PubEvent(Last, isNew);
            return Last;
        }

        // Update internal ATR
        TValue atrResult = _atr.Update(input, isNew);
        double atrValue = atrResult.Value;

        double sarResult;
        IsStop = false;

        if (_count == 1)
        {
            // First bar: initialize SIC, no SAR yet
            s.Sic = close;
            s.IsLong = true;
            sarResult = double.NaN;
        }
        else if (!_atr.IsHot)
        {
            // Warmup: track initial trend direction
            if (_count == _period)
            {
                // At warmup end: determine initial trend from first close vs current
                // (we stored the first close in Sic on bar 1)
                s.IsLong = close >= s.Sic;
                s.Sic = close;
            }
            else
            {
                s.Sic = s.IsLong
                    ? Math.Max(s.Sic, close)
                    : Math.Min(s.Sic, close);
            }
            sarResult = double.NaN;
        }
        else
        {
            // Update SIC (Significant Close)
            s.Sic = s.IsLong
                ? Math.Max(s.Sic, close)
                : Math.Min(s.Sic, close);

            // Calculate SAR
            double arc = atrValue * _multiplier;
            sarResult = s.IsLong ? s.Sic - arc : s.Sic + arc;

            // Evaluate stop and reverse
            if ((s.IsLong && close < sarResult) || (!s.IsLong && close > sarResult))
            {
                IsStop = true;
                s.Sic = close;
                s.IsLong = !s.IsLong;

                // Recalculate SAR with new direction
                sarResult = s.IsLong ? s.Sic - arc : s.Sic + arc;
            }
        }

        SarValue = sarResult;
        _s = s;

        Last = new TValue(input.Time, sarResult);
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

        Batch(source.HighValues, source.LowValues, source.CloseValues,
            CollectionsMarshal.AsSpan(v), _period, _multiplier);

        source.Times.CopyTo(CollectionsMarshal.AsSpan(t));

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

    public void Reset()
    {
        _atr.Reset();
        _count = 0;
        _s = new State(
            IsLong: true,
            Sic: double.NaN,
            LastValidHigh: double.NaN,
            LastValidLow: double.NaN,
            LastValidClose: double.NaN);
        _ps = _s;
        SarValue = double.NaN;
        IsStop = false;
        Last = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        Span<double> output,
        int period = 7,
        double multiplier = 3.0)
    {
        if (period <= 1)
        {
            throw new ArgumentException("Period must be greater than 1.", nameof(period));
        }
        if (multiplier <= 0)
        {
            throw new ArgumentException("Multiplier must be greater than 0.", nameof(multiplier));
        }
        if (high.Length != low.Length || high.Length != close.Length)
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

        // State machine precludes SIMD — use streaming instance
        var indicator = new Vstop(period, multiplier);
        long baseTime = DateTime.UtcNow.Ticks;
        for (int i = 0; i < len; i++)
        {
            _ = indicator.Update(
                new TBar(baseTime + i, high[i], high[i], low[i], close[i], 0),
                isNew: true);
            output[i] = indicator.SarValue;
        }
    }

    public static TSeries Batch(TBarSeries source, int period = 7, double multiplier = 3.0)
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

        Batch(source.HighValues, source.LowValues, source.CloseValues,
            CollectionsMarshal.AsSpan(v), period, multiplier);

        source.Times.CopyTo(CollectionsMarshal.AsSpan(t));

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (TSeries Results, Vstop Indicator) Calculate(
        TBarSeries source, int period = 7, double multiplier = 3.0)
    {
        var indicator = new Vstop(period, multiplier);
        var results = indicator.Update(source);
        return (results, indicator);
    }
}
