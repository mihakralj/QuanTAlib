using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// ATRSTOP: ATR Trailing Stop (Wilder)
/// Dynamic trailing stop using ATR multiplier with band ratcheting.
/// Upper/lower bands tighten in trending direction, flip on reversal.
/// </summary>
/// <seealso href="https://dotnet.stockindicators.dev/indicators/AtrStop/">Skender reference</seealso>
[SkipLocalsInit]
public sealed class Atrstop : ITValuePublisher
{
    private readonly int _period;
    private readonly double _multiplier;
    private readonly bool _useHighLow;
    private readonly Atr _atr;
    private int _count;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        bool IsBullish,
        double UpperBand,
        double LowerBand,
        double PrevClose,
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

    /// <summary>ATR multiplier for band width.</summary>
    public double Multiplier => _multiplier;

    /// <summary>True if using High/Low for band calculation instead of Close.</summary>
    public bool UseHighLow => _useHighLow;

    /// <summary>Bars required for valid output.</summary>
    public int WarmupPeriod { get; }

    /// <summary>Current trailing stop value.</summary>
    public double StopValue { get; private set; }

    /// <summary>True when the indicator is in bullish (uptrend) mode.</summary>
    public bool IsBullish => _s.IsBullish;

    /// <summary>Primary output value (stop level as TValue for overlay plotting).</summary>
    public TValue Last { get; private set; }

    /// <summary>True when enough bars have been processed.</summary>
    public bool IsHot => _count > _period;

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Creates an ATR Trailing Stop indicator.
    /// </summary>
    /// <param name="period">ATR lookback period (default 21).</param>
    /// <param name="multiplier">ATR multiplier (default 3.0).</param>
    /// <param name="useHighLow">If true, use High/Low for band offsets; otherwise use Close (default false).</param>
    public Atrstop(int period = 21, double multiplier = 3.0, bool useHighLow = false)
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
        _useHighLow = useHighLow;
        _atr = new Atr(period);
        _count = 0;

        _s = new State(
            IsBullish: true,
            UpperBand: double.NaN,
            LowerBand: double.NaN,
            PrevClose: double.NaN,
            LastValidHigh: double.NaN,
            LastValidLow: double.NaN,
            LastValidClose: double.NaN);
        _ps = _s;

        string mode = useHighLow ? "HL" : "C";
        Name = $"AtrStop({period},{multiplier:F1},{mode})";
        WarmupPeriod = period + 1;
        StopValue = double.NaN;
        _barHandler = HandleBar;
    }

    /// <summary>
    /// Creates an ATR Trailing Stop chained to a TBarSeries source.
    /// </summary>
    public Atrstop(TBarSeries source, int period = 21, double multiplier = 3.0, bool useHighLow = false)
        : this(period, multiplier, useHighLow)
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

        // Validate inputs
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

        double stopResult;

        if (!_atr.IsHot || _count <= _period)
        {
            // Warmup period — no stop value yet
            s.PrevClose = close;
            stopResult = double.NaN;
        }
        else
        {
            // Compute potential bands
            double upperEval, lowerEval;
            if (_useHighLow)
            {
                upperEval = high + (_multiplier * atrValue);
                lowerEval = low - (_multiplier * atrValue);
            }
            else
            {
                upperEval = close + (_multiplier * atrValue);
                lowerEval = close - (_multiplier * atrValue);
            }

            // Initialize bands on first hot bar
            if (double.IsNaN(s.UpperBand))
            {
                s.IsBullish = close >= s.PrevClose;
                s.UpperBand = upperEval;
                s.LowerBand = lowerEval;
            }
            else
            {
                // Ratchet upper band: only tighten (decrease) unless prev close broke above
                if (upperEval < s.UpperBand || s.PrevClose > s.UpperBand)
                {
                    s.UpperBand = upperEval;
                }

                // Ratchet lower band: only tighten (increase) unless prev close broke below
                if (lowerEval > s.LowerBand || s.PrevClose < s.LowerBand)
                {
                    s.LowerBand = lowerEval;
                }
            }

            // Determine trend and stop value
            if (s.IsBullish && close <= s.LowerBand)
            {
                // Flip to bearish
                s.IsBullish = false;
                stopResult = s.UpperBand;
            }
            else if (!s.IsBullish && close >= s.UpperBand)
            {
                // Flip to bullish
                s.IsBullish = true;
                stopResult = s.LowerBand;
            }
            else
            {
                stopResult = s.IsBullish ? s.LowerBand : s.UpperBand;
            }

            s.PrevClose = close;
        }

        StopValue = stopResult;
        _s = s;

        Last = new TValue(input.Time, stopResult);
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
            CollectionsMarshal.AsSpan(v), _period, _multiplier, _useHighLow);

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
            IsBullish: true,
            UpperBand: double.NaN,
            LowerBand: double.NaN,
            PrevClose: double.NaN,
            LastValidHigh: double.NaN,
            LastValidLow: double.NaN,
            LastValidClose: double.NaN);
        _ps = _s;
        StopValue = double.NaN;
        Last = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        Span<double> output,
        int period = 21,
        double multiplier = 3.0,
        bool useHighLow = false)
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
        var indicator = new Atrstop(period, multiplier, useHighLow);
        long baseTime = DateTime.UtcNow.Ticks;
        for (int i = 0; i < len; i++)
        {
            _ = indicator.Update(
                new TBar(baseTime + i, high[i], high[i], low[i], close[i], 0),
                isNew: true);
            output[i] = indicator.StopValue;
        }
    }

    public static TSeries Batch(TBarSeries source, int period = 21, double multiplier = 3.0, bool useHighLow = false)
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
            CollectionsMarshal.AsSpan(v), period, multiplier, useHighLow);

        source.Times.CopyTo(CollectionsMarshal.AsSpan(t));

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (TSeries Results, Atrstop Indicator) Calculate(
        TBarSeries source, int period = 21, double multiplier = 3.0, bool useHighLow = false)
    {
        var indicator = new Atrstop(period, multiplier, useHighLow);
        var results = indicator.Update(source);
        return (results, indicator);
    }
}
