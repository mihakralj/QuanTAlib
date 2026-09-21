// PIVOTDEM: DeMark Pivot Points
// Calculates 3 support/resistance levels from previous bar's OHLC.
// Uses conditional logic based on Open vs Close relationship.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// PIVOTDEM: DeMark Pivot Points
/// </summary>
/// <remarks>
/// Computes 3 horizontal support/resistance levels from the previous bar's
/// open, high, low, and close. The key innovation is the conditional calculation
/// of the intermediate value X, which varies depending on the relationship
/// between open and close, weighting different price components accordingly.
///
/// Calculation (using previous bar's O, H, L, C):
/// <code>
/// If C &lt; O:  X = H + 2L + C
/// If C &gt; O:  X = 2H + L + C
/// If C == O: X = H + L + 2C
///
/// PP = X / 4
/// R1 = X / 2 − L
/// S1 = X / 2 − H
/// </code>
///
/// <b>Key characteristics:</b>
/// - O(1) computation: pure arithmetic from previous bar's OHLC
/// - 3 outputs: PP, R1, S1 (minimalist)
/// - WarmupPeriod = 2 (need previous bar's OHLC)
/// - No configurable parameters
/// - Conditional weighting: bearish bars weight Low, bullish bars weight High
/// - Only pivot variant that uses Open in the calculation
/// </remarks>
/// <seealso href="Pivotdem.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Pivotdem : ITValuePublisher
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double PrevOpen,
        double PrevHigh,
        double PrevLow,
        double PrevClose,
        double LastValidOpen,
        double LastValidHigh,
        double LastValidLow,
        double LastValidClose);

    private State _s;
    private State _ps;
    private int _count;

    private readonly TBarPublishedHandler _barHandler;

    /// <summary>Display name for the indicator.</summary>
    public string Name { get; }

    /// <summary>Bars required for the indicator to warm up.</summary>
    public int WarmupPeriod { get; }

    /// <summary>Central Pivot Point: X / 4 (conditionally weighted)</summary>
    public double PP { get; private set; }

    /// <summary>Resistance 1: X / 2 − prevLow</summary>
    public double R1 { get; private set; }

    /// <summary>Support 1: X / 2 − prevHigh</summary>
    public double S1 { get; private set; }

    /// <summary>Primary output value (PP as TValue).</summary>
    public TValue Last { get; private set; }

    /// <summary>True when enough bars have been processed for valid output.</summary>
    public bool IsHot => _count >= 2;

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Creates a DeMark Pivot Points indicator.
    /// </summary>
    public Pivotdem()
    {
        _count = 0;
        _s = new State(double.NaN, double.NaN, double.NaN, double.NaN,
            double.NaN, double.NaN, double.NaN, double.NaN);
        _ps = _s;

        SetAllNaN();

        Name = "Pivotdem";
        WarmupPeriod = 2;
        _barHandler = HandleBar;
    }

    /// <summary>
    /// Creates a DeMark Pivot Points indicator chained to a TBarSeries source.
    /// </summary>
    public Pivotdem(TBarSeries source)
        : this()
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

        // Validate inputs - substitute last-valid on NaN/Infinity
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
            SetAllNaN();
            Last = new TValue(input.Time, double.NaN);
            PubEvent(Last, isNew);
            return Last;
        }

        // First bar: store OHLC but cannot compute pivots yet (no previous bar)
        if (_count < 2)
        {
            s.PrevOpen = open;
            s.PrevHigh = high;
            s.PrevLow = low;
            s.PrevClose = close;
            _s = s;
            SetAllNaN();
            Last = new TValue(input.Time, double.NaN);
            PubEvent(Last, isNew);
            return Last;
        }

        // Compute DeMark pivot levels from PREVIOUS bar's OHLC
        double pO = s.PrevOpen;
        double pH = s.PrevHigh;
        double pL = s.PrevLow;
        double pC = s.PrevClose;

        // Conditional X calculation
        double x;
        if (pC < pO)
        {
            x = pH + (2.0 * pL) + pC;       // Bearish: weight Low
        }
        else if (pC > pO)
        {
            x = (2.0 * pH) + pL + pC;       // Bullish: weight High
        }
        else
        {
            x = pH + pL + (2.0 * pC);       // Doji: weight Close
        }

        double halfX = x * 0.5;
        PP = x * 0.25;
        R1 = halfX - pL;
        S1 = halfX - pH;

        // Store current bar's OHLC as "previous" for next bar
        s.PrevOpen = open;
        s.PrevHigh = high;
        s.PrevLow = low;
        s.PrevClose = close;
        _s = s;

        Last = new TValue(input.Time, PP);
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
            CollectionsMarshal.AsSpan(v));

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
        _s = new State(double.NaN, double.NaN, double.NaN, double.NaN,
            double.NaN, double.NaN, double.NaN, double.NaN);
        _ps = _s;
        SetAllNaN();
        Last = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetAllNaN()
    {
        PP = double.NaN;
        R1 = double.NaN;
        S1 = double.NaN;
    }

    /// <summary>
    /// Batch computation of DeMark Pivot Points over span data.
    /// Writes PP values to <paramref name="ppOutput"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(
        ReadOnlySpan<double> open,
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        Span<double> ppOutput)
    {
        if (open.Length != high.Length || high.Length != low.Length || high.Length != close.Length)
        {
            throw new ArgumentException("Input spans must have the same length.", nameof(high));
        }
        if (ppOutput.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least as long as input.", nameof(ppOutput));
        }

        int len = high.Length;
        if (len == 0)
        {
            return;
        }

        // First bar: no previous data
        ppOutput[0] = double.NaN;

        // Remaining bars: compute from previous bar's OHLC
        for (int i = 1; i < len; i++)
        {
            double pO = open[i - 1];
            double pH = high[i - 1];
            double pL = low[i - 1];
            double pC = close[i - 1];

            double x;
            if (pC < pO) { x = pH + (2.0 * pL) + pC; }
            else if (pC > pO) { x = (2.0 * pH) + pL + pC; }
            else { x = pH + pL + (2.0 * pC); }

            ppOutput[i] = x * 0.25;
        }
    }

    public static TSeries Batch(TBarSeries source)
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
            CollectionsMarshal.AsSpan(v));

        source.Times.CopyTo(CollectionsMarshal.AsSpan(t));

        return new TSeries(t, v);
    }

    /// <summary>
    /// Batch computation of all 3 DeMark Pivot Point levels over span data.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void BatchAll(
        ReadOnlySpan<double> open,
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        Span<double> ppOut,
        Span<double> r1Out,
        Span<double> s1Out)
    {
        if (open.Length != high.Length || high.Length != low.Length || high.Length != close.Length)
        {
            throw new ArgumentException("Input spans must have the same length.", nameof(high));
        }

        int len = high.Length;

        if (ppOut.Length < len) { throw new ArgumentException("Output span too short.", nameof(ppOut)); }
        if (r1Out.Length < len) { throw new ArgumentException("Output span too short.", nameof(r1Out)); }
        if (s1Out.Length < len) { throw new ArgumentException("Output span too short.", nameof(s1Out)); }

        if (len == 0)
        {
            return;
        }

        // First bar: no previous data
        ppOut[0] = double.NaN;
        r1Out[0] = double.NaN;
        s1Out[0] = double.NaN;

        for (int i = 1; i < len; i++)
        {
            double pO = open[i - 1];
            double pH = high[i - 1];
            double pL = low[i - 1];
            double pC = close[i - 1];

            double x;
            if (pC < pO) { x = pH + (2.0 * pL) + pC; }
            else if (pC > pO) { x = (2.0 * pH) + pL + pC; }
            else { x = pH + pL + (2.0 * pC); }

            double halfX = x * 0.5;
            ppOut[i] = x * 0.25;
            r1Out[i] = halfX - pL;
            s1Out[i] = halfX - pH;
        }
    }

    public static (TSeries Results, Pivotdem Indicator) Calculate(TBarSeries source)
    {
        var indicator = new Pivotdem();
        var results = indicator.Update(source);
        return (results, indicator);
    }
}
