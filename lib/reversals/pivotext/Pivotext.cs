// PIVOTEXT: Extended Traditional Pivot Points
// Calculates 11 support/resistance levels from previous bar's HLC.
// Classic floor trader formula extended with R4/R5 and S4/S5 levels.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// PIVOTEXT: Extended Traditional Pivot Points
/// </summary>
/// <remarks>
/// Computes 11 horizontal support/resistance levels from the previous bar's
/// high, low, and close. The central pivot point (PP) is the arithmetic mean
/// of HLC; resistance (R1-R5) and support (S1-S5) levels are derived from
/// PP and the prior bar's range. R1-R3/S1-S3 are identical to classic pivots;
/// R4/R5 and S4/S5 extend the range further for extreme move scenarios.
///
/// Calculation (using previous bar's H, L, C):
/// <code>
/// PP = (H + L + C) / 3
/// R1 = 2 * PP - L          S1 = 2 * PP - H
/// R2 = PP + (H - L)        S2 = PP - (H - L)
/// R3 = H + 2 * (PP - L)    S3 = L - 2 * (H - PP)
/// R4 = H + 3 * (PP - L)    S4 = L - 3 * (H - PP)
/// R5 = H + 4 * (PP - L)    S5 = L - 4 * (H - PP)
/// </code>
///
/// <b>Key characteristics:</b>
/// - O(1) computation: pure arithmetic from previous bar's HLC
/// - 11 outputs: PP, R1, R2, R3, R4, R5, S1, S2, S3, S4, S5
/// - WarmupPeriod = 2 (need previous bar's HLC)
/// - No configurable parameters
/// - Levels remain constant until a new bar arrives
/// - R4/R5 and S4/S5 provide extreme support/resistance for gap scenarios
/// </remarks>
/// <seealso href="Pivotext.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Pivotext : ITValuePublisher
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double PrevHigh,
        double PrevLow,
        double PrevClose,
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

    /// <summary>Central Pivot Point: (prevH + prevL + prevC) / 3</summary>
    public double PP { get; private set; }

    /// <summary>Resistance 1: 2 * PP - prevL</summary>
    public double R1 { get; private set; }

    /// <summary>Resistance 2: PP + (prevH - prevL)</summary>
    public double R2 { get; private set; }

    /// <summary>Resistance 3: prevH + 2 * (PP - prevL)</summary>
    public double R3 { get; private set; }

    /// <summary>Resistance 4: prevH + 3 * (PP - prevL)</summary>
    public double R4 { get; private set; }

    /// <summary>Resistance 5: prevH + 4 * (PP - prevL)</summary>
    public double R5 { get; private set; }

    /// <summary>Support 1: 2 * PP - prevH</summary>
    public double S1 { get; private set; }

    /// <summary>Support 2: PP - (prevH - prevL)</summary>
    public double S2 { get; private set; }

    /// <summary>Support 3: prevL - 2 * (prevH - PP)</summary>
    public double S3 { get; private set; }

    /// <summary>Support 4: prevL - 3 * (prevH - PP)</summary>
    public double S4 { get; private set; }

    /// <summary>Support 5: prevL - 4 * (prevH - PP)</summary>
    public double S5 { get; private set; }

    /// <summary>Primary output value (PP as TValue).</summary>
    public TValue Last { get; private set; }

    /// <summary>True when enough bars have been processed for valid output.</summary>
    public bool IsHot => _count >= 2;

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Creates an Extended Traditional Pivot Points indicator.
    /// </summary>
    public Pivotext()
    {
        _count = 0;
        _s = new State(double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);
        _ps = _s;

        SetAllNaN();

        Name = "Pivotext";
        WarmupPeriod = 2;
        _barHandler = HandleBar;
    }

    /// <summary>
    /// Creates an Extended Traditional Pivot Points indicator chained to a TBarSeries source.
    /// </summary>
    public Pivotext(TBarSeries source)
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
            SetAllNaN();
            Last = new TValue(input.Time, double.NaN);
            PubEvent(Last, isNew);
            return Last;
        }

        // First bar: store HLC but cannot compute pivots yet (no previous bar)
        if (_count < 2)
        {
            s.PrevHigh = high;
            s.PrevLow = low;
            s.PrevClose = close;
            _s = s;
            SetAllNaN();
            Last = new TValue(input.Time, double.NaN);
            PubEvent(Last, isNew);
            return Last;
        }

        // Compute extended pivot levels from PREVIOUS bar's HLC
        double pH = s.PrevHigh;
        double pL = s.PrevLow;
        double pC = s.PrevClose;

        double pp = (pH + pL + pC) / 3.0;
        double range = pH - pL;
        double ppMinusL = pp - pL;     // used for R3, R4, R5
        double hMinusPP = pH - pp;     // used for S3, S4, S5

        PP = pp;
        R1 = Math.FusedMultiplyAdd(2.0, pp, -pL);       // 2*pp - pL
        S1 = Math.FusedMultiplyAdd(2.0, pp, -pH);       // 2*pp - pH
        R2 = pp + range;                                  // pp + (pH - pL)
        S2 = pp - range;                                  // pp - (pH - pL)
        R3 = Math.FusedMultiplyAdd(2.0, ppMinusL, pH);   // pH + 2*(pp - pL)
        S3 = Math.FusedMultiplyAdd(-2.0, hMinusPP, pL);  // pL - 2*(pH - pp)
        R4 = Math.FusedMultiplyAdd(3.0, ppMinusL, pH);   // pH + 3*(pp - pL)
        S4 = Math.FusedMultiplyAdd(-3.0, hMinusPP, pL);  // pL - 3*(pH - pp)
        R5 = Math.FusedMultiplyAdd(4.0, ppMinusL, pH);   // pH + 4*(pp - pL)
        S5 = Math.FusedMultiplyAdd(-4.0, hMinusPP, pL);  // pL - 4*(pH - pp)

        // Store current bar's HLC as "previous" for next bar
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

        Batch(source.HighValues, source.LowValues, source.CloseValues,
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
        _s = new State(double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);
        _ps = _s;
        SetAllNaN();
        Last = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetAllNaN()
    {
        PP = double.NaN;
        R1 = double.NaN;
        R2 = double.NaN;
        R3 = double.NaN;
        R4 = double.NaN;
        R5 = double.NaN;
        S1 = double.NaN;
        S2 = double.NaN;
        S3 = double.NaN;
        S4 = double.NaN;
        S5 = double.NaN;
    }

    /// <summary>
    /// Batch computation of Extended Traditional Pivot Points over span data.
    /// Writes PP values to <paramref name="ppOutput"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        Span<double> ppOutput)
    {
        if (high.Length != low.Length || high.Length != close.Length)
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

        // Remaining bars: compute from previous bar's HLC
        for (int i = 1; i < len; i++)
        {
            double pH = high[i - 1];
            double pL = low[i - 1];
            double pC = close[i - 1];
            ppOutput[i] = (pH + pL + pC) / 3.0;
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

        Batch(source.HighValues, source.LowValues, source.CloseValues,
            CollectionsMarshal.AsSpan(v));

        source.Times.CopyTo(CollectionsMarshal.AsSpan(t));

        return new TSeries(t, v);
    }

    /// <summary>
    /// Batch computation of all 11 Extended Traditional Pivot Point levels over span data.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void BatchAll(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        Span<double> ppOut,
        Span<double> r1Out,
        Span<double> s1Out,
        Span<double> r2Out,
        Span<double> s2Out,
        Span<double> r3Out,
        Span<double> s3Out,
        Span<double> r4Out,
        Span<double> s4Out,
        Span<double> r5Out,
        Span<double> s5Out)
    {
        if (high.Length != low.Length || high.Length != close.Length)
        {
            throw new ArgumentException("Input spans must have the same length.", nameof(high));
        }

        int len = high.Length;

        if (ppOut.Length < len) { throw new ArgumentException("Output span too short.", nameof(ppOut)); }
        if (r1Out.Length < len) { throw new ArgumentException("Output span too short.", nameof(r1Out)); }
        if (s1Out.Length < len) { throw new ArgumentException("Output span too short.", nameof(s1Out)); }
        if (r2Out.Length < len) { throw new ArgumentException("Output span too short.", nameof(r2Out)); }
        if (s2Out.Length < len) { throw new ArgumentException("Output span too short.", nameof(s2Out)); }
        if (r3Out.Length < len) { throw new ArgumentException("Output span too short.", nameof(r3Out)); }
        if (s3Out.Length < len) { throw new ArgumentException("Output span too short.", nameof(s3Out)); }
        if (r4Out.Length < len) { throw new ArgumentException("Output span too short.", nameof(r4Out)); }
        if (s4Out.Length < len) { throw new ArgumentException("Output span too short.", nameof(s4Out)); }
        if (r5Out.Length < len) { throw new ArgumentException("Output span too short.", nameof(r5Out)); }
        if (s5Out.Length < len) { throw new ArgumentException("Output span too short.", nameof(s5Out)); }

        if (len == 0)
        {
            return;
        }

        // First bar: no previous data
        ppOut[0] = double.NaN;
        r1Out[0] = double.NaN;
        s1Out[0] = double.NaN;
        r2Out[0] = double.NaN;
        s2Out[0] = double.NaN;
        r3Out[0] = double.NaN;
        s3Out[0] = double.NaN;
        r4Out[0] = double.NaN;
        s4Out[0] = double.NaN;
        r5Out[0] = double.NaN;
        s5Out[0] = double.NaN;

        for (int i = 1; i < len; i++)
        {
            double pH = high[i - 1];
            double pL = low[i - 1];
            double pC = close[i - 1];

            double pp = (pH + pL + pC) / 3.0;
            double range = pH - pL;
            double ppMinusL = pp - pL;
            double hMinusPP = pH - pp;

            ppOut[i] = pp;
            r1Out[i] = Math.FusedMultiplyAdd(2.0, pp, -pL);
            s1Out[i] = Math.FusedMultiplyAdd(2.0, pp, -pH);
            r2Out[i] = pp + range;
            s2Out[i] = pp - range;
            r3Out[i] = Math.FusedMultiplyAdd(2.0, ppMinusL, pH);
            s3Out[i] = Math.FusedMultiplyAdd(-2.0, hMinusPP, pL);
            r4Out[i] = Math.FusedMultiplyAdd(3.0, ppMinusL, pH);
            s4Out[i] = Math.FusedMultiplyAdd(-3.0, hMinusPP, pL);
            r5Out[i] = Math.FusedMultiplyAdd(4.0, ppMinusL, pH);
            s5Out[i] = Math.FusedMultiplyAdd(-4.0, hMinusPP, pL);
        }
    }

    public static (TSeries Results, Pivotext Indicator) Calculate(TBarSeries source)
    {
        var indicator = new Pivotext();
        var results = indicator.Update(source);
        return (results, indicator);
    }
}
