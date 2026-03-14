// ASI: Accumulation Swing Index
// Welles Wilder's cumulative swing index from "New Concepts in Technical Trading Systems" (1978).
// Measures the true strength of price swings by accounting for open, high, low, close
// across consecutive bars. The cumulative sum separates genuine breakouts from noise.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// ASI: Accumulation Swing Index
/// </summary>
/// <remarks>
/// Welles Wilder's cumulative swing index that measures genuine directional price movement.
/// Each bar produces a Swing Index (SI) value based on the relationship between current
/// and previous OHLC prices, scaled by the limit move parameter T:
///
/// <code>
/// K = max(|H - C1|, |L - C1|)
/// R = largest of: |H-C1| - 0.5|L-C1| + 0.25|C1-O1|
///                 |L-C1| - 0.5|H-C1| + 0.25|C1-O1|
///                 |H-L| + 0.25|C1-O1|
/// SI = 50 * ((C-C1) + 0.5*(C-O) + 0.25*(C1-O1)) / R * (K/T)
/// ASI = cumulative sum of SI
/// </code>
///
/// The first bar produces 0 (no previous bar available). IsHot after bar 2.
/// Guard: R=0 produces SI=0.
///
/// References:
///   Wilder, J.W. (1978). New Concepts in Technical Trading Systems. Trend Research.
/// </remarks>
/// <seealso href="Asi.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Asi : ITValuePublisher
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double PrevClose,
        double PrevOpen,
        double Asi,
        double LastValidClose,
        double LastValidOpen,
        int Count);

    private State _s;
    private State _ps;

    private readonly double _limitMove;
    private readonly TBarPublishedHandler _barHandler;
    private readonly TValuePublishedHandler _handler;

    /// <summary>Display name for the indicator.</summary>
    public string Name { get; }

    /// <summary>Bars required for the first valid output (2 bars needed — first is always 0).</summary>
#pragma warning disable S2325 // Instance property required by ITValuePublisher convention; cannot be static
    public int WarmupPeriod => 2;
#pragma warning restore S2325

    /// <summary>True once at least 2 bars have been processed.</summary>
    public bool IsHot => _s.Count >= 2;

    /// <summary>Current ASI value.</summary>
    public TValue Last { get; private set; }

    /// <summary>Event fired after each Update call.</summary>
    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Creates ASI with the given limit move value.
    /// </summary>
    /// <param name="limitMove">Maximum daily price change (T). Typically 3.0 for stocks.</param>
    public Asi(double limitMove = 3.0)
    {
        if (limitMove <= 0)
        {
            throw new ArgumentException("LimitMove must be greater than 0", nameof(limitMove));
        }

        _limitMove = limitMove;
        Name = $"Asi({limitMove})";
        _s = new State(double.NaN, double.NaN, 0.0, double.NaN, double.NaN, 0);
        _ps = _s;
        _barHandler = HandleBar;
        _handler = Handle;
    }

    /// <summary>
    /// Creates ASI chained to a TBarSeries source.
    /// </summary>
    public Asi(TBarSeries source, double limitMove = 3.0) : this(limitMove)
    {
        Prime(source);
        source.Pub += _barHandler;
    }

    /// <summary>
    /// Creates ASI chained to an ITValuePublisher source (uses close price only).
    /// </summary>
    public Asi(ITValuePublisher source, double limitMove = 3.0) : this(limitMove)
    {
        source.Pub += _handler;
    }

    private void HandleBar(object? sender, in TBarEventArgs e) => Update(e.Value, e.IsNew);
    private void Handle(object? sender, in TValueEventArgs e) =>
        Update(new TBar(e.Value.Time, e.Value.Value, e.Value.Value, e.Value.Value, e.Value.Value, 0), e.IsNew);

    /// <summary>Resets all state to initial conditions.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _s = new State(double.NaN, double.NaN, 0.0, double.NaN, double.NaN, 0);
        _ps = _s;
        Last = default;
    }

    /// <summary>
    /// Updates ASI with a new OHLC bar.
    /// </summary>
    /// <param name="input">OHLCV bar data</param>
    /// <param name="isNew">True to advance state; false to rewrite the latest bar</param>
    /// <returns>Current ASI value as TValue</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        var s = _s;

        if (isNew)
        {
            _ps = s;
            s.Count++;
        }
        else
        {
            s = _ps;
        }

        // Sanitize inputs — use last-valid on NaN/Infinity
        double rawClose = input.Close;
        double rawOpen = input.Open;
        double rawHigh = input.High;
        double rawLow = input.Low;

        double close;
        if (double.IsFinite(rawClose)) { close = rawClose; }
        else if (double.IsFinite(s.LastValidClose)) { close = s.LastValidClose; }
        else { close = 0.0; }

        double open;
        if (double.IsFinite(rawOpen)) { open = rawOpen; }
        else if (double.IsFinite(s.LastValidOpen)) { open = s.LastValidOpen; }
        else { open = close; }

        double high = double.IsFinite(rawHigh) ? rawHigh : close;
        double low = double.IsFinite(rawLow) ? rawLow : close;

        if (double.IsFinite(rawClose)) { s.LastValidClose = rawClose; }
        if (double.IsFinite(rawOpen)) { s.LastValidOpen = rawOpen; }

        double si = 0.0;

        // First bar: no previous close available — SI = 0
        if (double.IsFinite(s.PrevClose))
        {
            double prevClose = s.PrevClose;
            double prevOpen = double.IsFinite(s.PrevOpen) ? s.PrevOpen : prevClose;

            double absHC = Math.Abs(high - prevClose);
            double absLC = Math.Abs(low - prevClose);
            double absHL = Math.Abs(high - low);
            double absC1O1 = Math.Abs(prevClose - prevOpen);

            double K = Math.Max(absHC, absLC);

            double R;
            if (absHC >= absLC && absHC >= absHL)
            {
                R = Math.FusedMultiplyAdd(-0.5, absLC, absHC) + (0.25 * absC1O1);
            }
            else if (absLC >= absHC && absLC >= absHL)
            {
                R = Math.FusedMultiplyAdd(-0.5, absHC, absLC) + (0.25 * absC1O1);
            }
            else
            {
                R = absHL + (0.25 * absC1O1);
            }

            if (R > 0.0)
            {
                // SI = 50 * [(C-C1) + 0.5*(C-O) + 0.25*(C1-O1)] / R * (K/T)
                double numerator = Math.FusedMultiplyAdd(0.5, close - open, close - prevClose) + (0.25 * (prevClose - prevOpen));
                si = 50.0 * numerator / R * (K / _limitMove);
            }
        }

        s.Asi += si;
        s.PrevClose = close;
        s.PrevOpen = open;

        _s = s;

        Last = new TValue(input.Time, s.Asi);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    /// <summary>
    /// Updates ASI as TValue (uses value as close; open=high=low=close).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true) =>
        Update(new TBar(input.Time, input.Value, input.Value, input.Value, input.Value, 0), isNew);

    /// <summary>
    /// Batch-computes ASI over a TBarSeries.
    /// </summary>
    public TSeries Update(TBarSeries source)
    {
        int len = source.Count;
        if (len == 0)
        {
            return [];
        }

        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(
            source.Open.Values, source.High.Values,
            source.Low.Values, source.Close.Values,
            vSpan, _limitMove);

        source.Open.Times.CopyTo(tSpan);

        // Restore streaming state by replaying the last bar
        Reset();
        for (int i = 0; i < len; i++)
        {
            Update(source[i], isNew: true);
        }

        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);
        return new TSeries(t, v);
    }

    /// <summary>
    /// Static factory: computes ASI over a TBarSeries and returns the result series + indicator.
    /// </summary>
    public static (TSeries Results, Asi Indicator) Calculate(TBarSeries source, double limitMove = 3.0)
    {
        var indicator = new Asi(limitMove);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    /// <summary>
    /// Batch-computes ASI over raw OHLC spans. Zero-allocation for small inputs (stackalloc) or
    /// direct scalar computation since ASI has no rolling window — it is purely cumulative.
    /// </summary>
    /// <param name="open">Source open prices</param>
    /// <param name="high">Source high prices</param>
    /// <param name="low">Source low prices</param>
    /// <param name="close">Source close prices</param>
    /// <param name="output">Destination span for ASI values</param>
    /// <param name="limitMove">Limit move value T (must be &gt; 0)</param>
    public static void Batch(
        ReadOnlySpan<double> open,
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        Span<double> output,
        double limitMove = 3.0)
    {
        if (limitMove <= 0)
        {
            throw new ArgumentException("LimitMove must be greater than 0", nameof(limitMove));
        }

        int len = open.Length;

        if (high.Length != len)
        {
            throw new ArgumentException("High length must match open length", nameof(high));
        }

        if (low.Length != len)
        {
            throw new ArgumentException("Low length must match open length", nameof(low));
        }

        if (close.Length != len)
        {
            throw new ArgumentException("Close length must match open length", nameof(close));
        }

        if (output.Length != len)
        {
            throw new ArgumentException("Output length must match input length", nameof(output));
        }

        if (len == 0)
        {
            return;
        }

        double asi = 0.0;
        double prevClose = double.NaN;
        double prevOpen = double.NaN;

        for (int i = 0; i < len; i++)
        {
            double o;
            if (double.IsFinite(open[i])) { o = open[i]; }
            else if (double.IsFinite(prevClose)) { o = prevClose; }
            else { o = 0.0; }

            double h = double.IsFinite(high[i]) ? high[i] : o;
            double l = double.IsFinite(low[i]) ? low[i] : o;

            double c;
            if (double.IsFinite(close[i])) { c = close[i]; }
            else if (double.IsFinite(prevClose)) { c = prevClose; }
            else { c = 0.0; }

            double si = 0.0;

            if (double.IsFinite(prevClose))
            {
                double pc = prevClose;
                double po = double.IsFinite(prevOpen) ? prevOpen : pc;

                double absHC = Math.Abs(h - pc);
                double absLC = Math.Abs(l - pc);
                double absHL = Math.Abs(h - l);
                double absC1O1 = Math.Abs(pc - po);

                double K = Math.Max(absHC, absLC);

                double R;
                if (absHC >= absLC && absHC >= absHL)
                {
                    R = Math.FusedMultiplyAdd(-0.5, absLC, absHC) + (0.25 * absC1O1);
                }
                else if (absLC >= absHC && absLC >= absHL)
                {
                    R = Math.FusedMultiplyAdd(-0.5, absHC, absLC) + (0.25 * absC1O1);
                }
                else
                {
                    R = absHL + (0.25 * absC1O1);
                }

                if (R > 0.0)
                {
                    double numerator = Math.FusedMultiplyAdd(0.5, c - o, c - pc) + (0.25 * (pc - po));
                    si = 50.0 * numerator / R * (K / limitMove);
                }
            }

            asi += si;
            output[i] = asi;
            prevClose = double.IsFinite(close[i]) ? close[i] : prevClose;
            prevOpen = double.IsFinite(open[i]) ? open[i] : prevOpen;
        }
    }

    /// <summary>Primes the indicator by replaying historical data without firing events.</summary>
    public void Prime(TBarSeries source)
    {
        foreach (var bar in source)
        {
            Update(bar, isNew: true);
        }
    }
}
