// PSAR: Parabolic Stop And Reverse (Wilder, 1978)
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// skipcq: CS-W1028 - Intentional sealed class with no inheritance
// skipcq: CS-R1140 - State machine requires sequential long/short logic; splitting fragments state transitions

namespace QuanTAlib;

/// <summary>
/// PSAR: Parabolic Stop And Reverse
/// </summary>
/// <remarks>
/// Trend-following overlay indicator developed by J. Welles Wilder Jr. (1978).
/// Produces a trailing stop that accelerates toward price as the trend progresses.
///
/// Calculation:
/// <code>
/// Bar 0:  isLong = close > open; SAR = isLong ? low : high; EP = isLong ? high : low; AF = afStart
/// Bar 1+: newSAR = SAR + AF * (EP - SAR)
///          Long:  clamp newSAR ≤ min(low[1], low[2]); if low &lt; newSAR → reverse
///          Short: clamp newSAR ≥ max(high[1], high[2]); if high &gt; newSAR → reverse
///          On new EP: AF = min(AF + afIncrement, afMax)
///          On reversal: SAR = EP; EP = new extreme; AF = afStart; flip direction
/// </code>
///
/// <b>Key characteristics:</b>
/// - O(1) per-bar state machine with long/short mode transitions
/// - Acceleration factor ramps from afStart to afMax as trend strengthens
/// - SAR clamped to prior 2 bars' extremes to prevent crossover artifacts
/// - Default parameters: afStart=0.02, afIncrement=0.02, afMax=0.20 (Wilder's originals)
/// </remarks>
/// <seealso href="Psar.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Psar : ITValuePublisher
{
    private const double DefaultAfStart = 0.02;
    private const double DefaultAfIncrement = 0.02;
    private const double DefaultAfMax = 0.20;

    private readonly double _afStart;
    private readonly double _afIncrement;
    private readonly double _afMax;

    private int _count;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        bool IsLong,
        double Sar,
        double Ep,
        double Af,
        double Prev1High,
        double Prev1Low,
        double Prev2High,
        double Prev2Low,
        double LastValidOpen,
        double LastValidHigh,
        double LastValidLow,
        double LastValidClose);

    private State _s;
    private State _ps;

    private readonly TBarPublishedHandler _barHandler;

    /// <summary>Display name for the indicator.</summary>
    public string Name { get; }

    /// <summary>Initial acceleration factor.</summary>
    public double AfStart => _afStart;

    /// <summary>Acceleration factor increment per new extreme.</summary>
    public double AfIncrement => _afIncrement;

    /// <summary>Maximum acceleration factor.</summary>
    public double AfMax => _afMax;

    /// <summary>Bars required for the indicator to warm up.</summary>
    public int WarmupPeriod { get; }

    /// <summary>Current SAR value (the stop level).</summary>
    public double Sar { get; private set; }

    /// <summary>True when the PSAR is in long (uptrend) mode.</summary>
    public bool IsLong => _s.IsLong;

    /// <summary>Primary output value (SAR as TValue for overlay plotting).</summary>
    public TValue Last { get; private set; }

    /// <summary>True when enough bars have been processed for valid output.</summary>
    public bool IsHot => _count >= 1;

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Creates a Parabolic SAR indicator.
    /// </summary>
    /// <param name="afStart">Initial acceleration factor (default 0.02).</param>
    /// <param name="afIncrement">AF increment per new extreme (default 0.02).</param>
    /// <param name="afMax">Maximum acceleration factor (default 0.20).</param>
    public Psar(double afStart = DefaultAfStart, double afIncrement = DefaultAfIncrement, double afMax = DefaultAfMax)
    {
        if (afStart <= 0)
        {
            throw new ArgumentException("Start AF must be > 0.", nameof(afStart));
        }
        if (afIncrement <= 0)
        {
            throw new ArgumentException("AF increment must be > 0.", nameof(afIncrement));
        }
        if (afStart > afMax)
        {
            throw new ArgumentException("Start AF must be <= Max AF.", nameof(afStart));
        }
        if (afMax <= afStart)
        {
            throw new ArgumentException("Max AF must be > Start AF.", nameof(afMax));
        }

        _afStart = afStart;
        _afIncrement = afIncrement;
        _afMax = afMax;

        _count = 0;
        _s = new State(
            IsLong: true,
            Sar: double.NaN,
            Ep: double.NaN,
            Af: afStart,
            Prev1High: double.NaN,
            Prev1Low: double.NaN,
            Prev2High: double.NaN,
            Prev2Low: double.NaN,
            LastValidOpen: double.NaN,
            LastValidHigh: double.NaN,
            LastValidLow: double.NaN,
            LastValidClose: double.NaN);
        _ps = _s;

        Name = $"Psar({afStart:F2},{afIncrement:F2},{afMax:F2})";
        WarmupPeriod = 1;
        _barHandler = HandleBar;
    }

    /// <summary>
    /// Creates a Parabolic SAR chained to a TBarSeries source.
    /// </summary>
    public Psar(TBarSeries source, double afStart = DefaultAfStart, double afIncrement = DefaultAfIncrement, double afMax = DefaultAfMax)
        : this(afStart, afIncrement, afMax)
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
        double open = input.Open;
        double high = input.High;
        double low = input.Low;
        double close = input.Close;

        if (double.IsFinite(open)) { s.LastValidOpen = open; }
        else { open = s.LastValidOpen; }

        if (double.IsFinite(high)) { s.LastValidHigh = high; }
        else { high = s.LastValidHigh; }

        if (double.IsFinite(low)) { s.LastValidLow = low; }
        else { low = s.LastValidLow; }

        if (double.IsFinite(close)) { s.LastValidClose = close; }
        else { close = s.LastValidClose; }

        // If still no valid data, return NaN
        if (double.IsNaN(open) || double.IsNaN(high) || double.IsNaN(low) || double.IsNaN(close))
        {
            _s = s;
            Last = new TValue(input.Time, double.NaN);
            PubEvent(Last, isNew);
            return Last;
        }

        double sarResult;

        if (_count == 1)
        {
            // Bar 0: Initialize direction from close vs open
            s.IsLong = close > open;
            s.Sar = s.IsLong ? low : high;
            s.Ep = s.IsLong ? high : low;
            s.Af = _afStart;
            s.Prev1High = high;
            s.Prev1Low = low;
            s.Prev2High = high;
            s.Prev2Low = low;
            sarResult = s.Sar;
        }
        else
        {
            // Compute new SAR: sar + af * (ep - sar) → FMA: af*ep + sar*(1-af)
            double newSar = Math.FusedMultiplyAdd(s.Af, s.Ep - s.Sar, s.Sar);

            if (s.IsLong)
            {
                // Clamp SAR to be at or below prior lows
                newSar = Math.Min(newSar, s.Prev1Low);
                if (_count > 2)
                {
                    newSar = Math.Min(newSar, s.Prev2Low);
                }

                // Check for reversal: price crosses below SAR
                if (low < newSar)
                {
                    // Reverse to short
                    s.IsLong = false;
                    newSar = s.Ep;
                    s.Ep = low;
                    s.Af = _afStart;
                }
                else
                {
                    // Check for new extreme point
                    if (high > s.Ep)
                    {
                        s.Ep = high;
                        s.Af = Math.Min(s.Af + _afIncrement, _afMax);
                    }
                }
            }
            else
            {
                // Short mode: clamp SAR to be at or above prior highs
                newSar = Math.Max(newSar, s.Prev1High);
                if (_count > 2)
                {
                    newSar = Math.Max(newSar, s.Prev2High);
                }

                // Check for reversal: price crosses above SAR
                if (high > newSar)
                {
                    // Reverse to long
                    s.IsLong = true;
                    newSar = s.Ep;
                    s.Ep = high;
                    s.Af = _afStart;
                }
                else
                {
                    // Check for new extreme point
                    if (low < s.Ep)
                    {
                        s.Ep = low;
                        s.Af = Math.Min(s.Af + _afIncrement, _afMax);
                    }
                }
            }

            s.Sar = newSar;
            sarResult = newSar;

            // Shift prior bar tracking
            if (isNew)
            {
                s.Prev2High = s.Prev1High;
                s.Prev2Low = s.Prev1Low;
                s.Prev1High = high;
                s.Prev1Low = low;
            }
            else
            {
                // Bar correction: update current bar's values
                s.Prev1High = high;
                s.Prev1Low = low;
            }
        }

        Sar = sarResult;
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

        Batch(source.OpenValues, source.HighValues, source.LowValues, source.CloseValues,
            CollectionsMarshal.AsSpan(v), _afStart, _afIncrement, _afMax);

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
        _count = 0;
        _s = new State(
            IsLong: true,
            Sar: double.NaN,
            Ep: double.NaN,
            Af: _afStart,
            Prev1High: double.NaN,
            Prev1Low: double.NaN,
            Prev2High: double.NaN,
            Prev2Low: double.NaN,
            LastValidOpen: double.NaN,
            LastValidHigh: double.NaN,
            LastValidLow: double.NaN,
            LastValidClose: double.NaN);
        _ps = _s;
        Sar = double.NaN;
        Last = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(
        ReadOnlySpan<double> open,
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        Span<double> output,
        double afStart = DefaultAfStart,
        double afIncrement = DefaultAfIncrement,
        double afMax = DefaultAfMax)
    {
        if (afStart <= 0 || afStart > afMax)
        {
            throw new ArgumentException("Start AF must be > 0 and <= Max AF.", nameof(afStart));
        }
        if (afIncrement <= 0)
        {
            throw new ArgumentException("AF increment must be > 0.", nameof(afIncrement));
        }
        if (afMax <= afStart)
        {
            throw new ArgumentException("Max AF must be > Start AF.", nameof(afMax));
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

        // Compute via streaming instance for correctness (state machine prevents SIMD)
        var indicator = new Psar(afStart, afIncrement, afMax);

        long baseTime = DateTime.UtcNow.Ticks;
        for (int i = 0; i < len; i++)
        {
            _ = indicator.Update(
                new TBar(baseTime + i, open[i], high[i], low[i], close[i], 0),
                isNew: true);
            output[i] = indicator.Sar;
        }
    }

    public static TSeries Batch(TBarSeries source, double afStart = DefaultAfStart, double afIncrement = DefaultAfIncrement, double afMax = DefaultAfMax)
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
            CollectionsMarshal.AsSpan(v), afStart, afIncrement, afMax);

        source.Times.CopyTo(CollectionsMarshal.AsSpan(t));

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (TSeries Results, Psar Indicator) Calculate(
        TBarSeries source, double afStart = DefaultAfStart, double afIncrement = DefaultAfIncrement, double afMax = DefaultAfMax)
    {
        var indicator = new Psar(afStart, afIncrement, afMax);
        var results = indicator.Update(source);
        return (results, indicator);
    }
}
