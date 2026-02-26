// BRAR: Bull-Bear Power Ratio Oscillator
// Dual-output sentiment oscillator: AR (Atmosphere Ratio) and BR (Buying Ratio).
// Originates from Japanese technical analysis (強弱レシオ).

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// BRAR: Bull-Bear Power Ratio Oscillator
/// </summary>
/// <remarks>
/// Dual-output sentiment oscillator measuring two independent ratios:
/// <list type="bullet">
///   <item>BR (Buying Ratio) = SUM(max(0, H − PrevC), N) / SUM(max(0, PrevC − L), N) × 100</item>
///   <item>AR (Atmosphere Ratio) = SUM(max(0, H − O), N) / SUM(max(0, O − L), N) × 100</item>
/// </list>
/// Both lines oscillate around 100 (equilibrium). Four O(1) rolling sums via circular
/// buffers — 4 additions + 4 subtractions per bar regardless of period length.
///
/// First-bar bootstrap: when no previous close exists, the current open is used,
/// matching PineScript's <c>nz(close[1], open)</c> behaviour.
///
/// References:
///   Shimizu, Seiki (1986). The Japanese Chart of Charts.
///   PineScript reference: brar.pine
/// </remarks>
[SkipLocalsInit]
public sealed class Brar : ITValuePublisher
{
    private readonly int _period;

    // Four circular buffers for O(1) rolling sums
    private readonly double[] _brNumBuf;
    private readonly double[] _brDenBuf;
    private readonly double[] _arNumBuf;
    private readonly double[] _arDenBuf;

    // Snapshots of the four buffers saved on each isNew=true call — full array copy
    // is required because isNew=false must be idempotent across N consecutive calls.
    // Saving only the overwritten slot is NOT sufficient: _ps is captured before
    // s.OldXxx is set, so the scalar fields would hold the previous bar's stale value.
    private readonly double[] _brNumSnap;
    private readonly double[] _brDenSnap;
    private readonly double[] _arNumSnap;
    private readonly double[] _arDenSnap;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double BrNumSum,
        double BrDenSum,
        double ArNumSum,
        double ArDenSum,
        double PrevClose,
        double Br,
        double Ar,
        int Count,
        int Idx);

    private State _s;
    private State _ps;

    private readonly TBarPublishedHandler _barHandler;

    /// <summary>Display name for the indicator.</summary>
    public string Name { get; }

    /// <summary>Bars required for the first valid output.</summary>
    public int WarmupPeriod { get; }

    /// <summary>True once the rolling window is fully populated.</summary>
    public bool IsHot => _s.Count >= _period;

    /// <summary>Current AR (Atmosphere Ratio) value.</summary>
    public double Ar => _s.Ar;

    /// <summary>Current BR (Buying Ratio) value.</summary>
    public double Br => _s.Br;

    /// <summary>Primary output (BR as TValue).</summary>
    public TValue Last { get; private set; }

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Creates BRAR with the specified rolling-window period.
    /// </summary>
    /// <param name="period">Rolling window length (must be &gt; 0, default 26)</param>
    public Brar(int period = 26)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _period = period;
        _brNumBuf = new double[period];
        _brDenBuf = new double[period];
        _arNumBuf = new double[period];
        _arDenBuf = new double[period];

        _brNumSnap = new double[period];
        _brDenSnap = new double[period];
        _arNumSnap = new double[period];
        _arDenSnap = new double[period];

        _s = new State(0, 0, 0, 0, double.NaN, 100.0, 100.0, 0, 0);
        _ps = _s;

        WarmupPeriod = period;
        Name = $"Brar({period})";
        _barHandler = HandleBar;
    }

    /// <summary>
    /// Creates BRAR chained to a TBarSeries source.
    /// </summary>
    public Brar(TBarSeries source, int period = 26) : this(period)
    {
        Prime(source);
        source.Pub += _barHandler;
    }

    private void HandleBar(object? sender, in TBarEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PubEvent(TValue value, bool isNew) =>
        Pub?.Invoke(this, new TValueEventArgs { Value = value, IsNew = isNew });

    /// <summary>Resets all state to initial conditions.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _s = new State(0, 0, 0, 0, double.NaN, 100.0, 100.0, 0, 0);
        _ps = _s;
        Last = default;
        Array.Clear(_brNumBuf);
        Array.Clear(_brDenBuf);
        Array.Clear(_arNumBuf);
        Array.Clear(_arDenBuf);
        Array.Clear(_brNumSnap);
        Array.Clear(_brDenSnap);
        Array.Clear(_arNumSnap);
        Array.Clear(_arDenSnap);
    }

    /// <summary>
    /// Updates BRAR with a new bar.
    /// </summary>
    /// <param name="input">OHLCV bar data</param>
    /// <param name="isNew">True to advance state; false to rewrite the latest bar</param>
    /// <returns>Current BR value as TValue (primary output)</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        var s = _s;

        if (isNew)
        {
            // Snapshot all four buffers before any mutation — required for idempotent
            // isNew=false rollback across multiple consecutive correction calls.
            // Saving only the overwritten slot is insufficient: _ps is captured here,
            // before s.OldXxx would be set, so scalar fields carry the prior bar's stale value.
            _ps = s;
            Array.Copy(_brNumBuf, _brNumSnap, _period);
            Array.Copy(_brDenBuf, _brDenSnap, _period);
            Array.Copy(_arNumBuf, _arNumSnap, _period);
            Array.Copy(_arDenBuf, _arDenSnap, _period);
            s.Count++;
        }
        else
        {
            // Rollback: restore _ps scalar state + all four buffer snapshots.
            // Every isNew=false call starts from the identical pre-bar-N state,
            // so N consecutive correction calls are all idempotent.
            s = _ps;
            Array.Copy(_brNumSnap, _brNumBuf, _period);
            Array.Copy(_brDenSnap, _brDenBuf, _period);
            Array.Copy(_arNumSnap, _arNumBuf, _period);
            Array.Copy(_arDenSnap, _arDenBuf, _period);
        }

        // Sanitize OHLC inputs — use last-valid on NaN/Infinity
        double rawOpen = input.Open;
        double rawHigh = input.High;
        double rawLow = input.Low;
        double rawClose = input.Close;

        double open = double.IsFinite(rawOpen) ? rawOpen : 0.0;
        double high = double.IsFinite(rawHigh) ? rawHigh : open;
        double low = double.IsFinite(rawLow) ? rawLow : 0.0;
        double close = double.IsFinite(rawClose) ? rawClose : open;

        // First bar: use open as previous close (matches PineScript nz(close[1], open))
        double prevClose = double.IsFinite(s.PrevClose) ? s.PrevClose : open;

        // Compute per-bar contributions (clamped to 0)
        double brNum = Math.Max(0.0, high - prevClose);
        double brDen = Math.Max(0.0, prevClose - low);
        double arNum = Math.Max(0.0, high - open);
        double arDen = Math.Max(0.0, open - low);

        // O(1) circular-buffer rolling sums: subtract outgoing, write new, add incoming
        int idx = s.Idx;

        s.BrNumSum -= _brNumBuf[idx];
        s.BrDenSum -= _brDenBuf[idx];
        s.ArNumSum -= _arNumBuf[idx];
        s.ArDenSum -= _arDenBuf[idx];

        _brNumBuf[idx] = brNum;
        _brDenBuf[idx] = brDen;
        _arNumBuf[idx] = arNum;
        _arDenBuf[idx] = arDen;

        s.BrNumSum += brNum;
        s.BrDenSum += brDen;
        s.ArNumSum += arNum;
        s.ArDenSum += arDen;

        // Advance circular index only on new bars
        if (isNew)
        {
            s.Idx = (idx + 1) % _period;
        }

        // Compute ratios — default to 100 (equilibrium) on zero denominator
        s.Br = s.BrDenSum != 0.0 ? s.BrNumSum / s.BrDenSum * 100.0 : 100.0;
        s.Ar = s.ArDenSum != 0.0 ? s.ArNumSum / s.ArDenSum * 100.0 : 100.0;

        // Store close for next bar's prevClose
        s.PrevClose = close;

        _s = s;

        Last = new TValue(input.Time, s.Br);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>
    /// Updates BRAR from a TBarSeries, computing BR and AR series.
    /// </summary>
    public (TSeries Br, TSeries Ar) UpdateAll(TBarSeries source)
    {
        int len = source.Count;
        if (len == 0)
        {
            return (new TSeries([], []), new TSeries([], []));
        }

        var brList = new List<double>(len);
        var arList = new List<double>(len);
        CollectionsMarshal.SetCount(brList, len);
        CollectionsMarshal.SetCount(arList, len);

        var brSpan = CollectionsMarshal.AsSpan(brList);
        var arSpan = CollectionsMarshal.AsSpan(arList);

        Batch(
            source.Open.Values, source.High.Values,
            source.Low.Values, source.Close.Values,
            brSpan, arSpan, _period);

        var tList = new List<long>(len);
        CollectionsMarshal.SetCount(tList, len);
        source.Open.Times.CopyTo(CollectionsMarshal.AsSpan(tList));

        // Replay to synchronise internal state
        Reset();
        for (int i = 0; i < len; i++)
        {
            Update(source[i], isNew: true);
        }

        return (new TSeries(tList, brList), new TSeries(tList, arList));
    }

    /// <summary>
    /// Batch-computes BRAR over raw OHLC spans. Zero-allocation path for large datasets.
    /// </summary>
    /// <param name="open">Source open prices</param>
    /// <param name="high">Source high prices</param>
    /// <param name="low">Source low prices</param>
    /// <param name="close">Source close prices</param>
    /// <param name="brOutput">Destination span for BR values</param>
    /// <param name="arOutput">Destination span for AR values</param>
    /// <param name="period">Rolling window length (must be &gt; 0)</param>
    public static void Batch(
        ReadOnlySpan<double> open,
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        Span<double> brOutput,
        Span<double> arOutput,
        int period = 26)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
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

        if (brOutput.Length != len)
        {
            throw new ArgumentException("brOutput length must match input length", nameof(brOutput));
        }

        if (arOutput.Length != len)
        {
            throw new ArgumentException("arOutput length must match input length", nameof(arOutput));
        }

        if (len == 0)
        {
            return;
        }

        const int StackallocThreshold = 256;

        // Four circular buffers — stack for small periods, ArrayPool for large
        double[]? rentedBrNum = null;
        double[]? rentedBrDen = null;
        double[]? rentedArNum = null;
        double[]? rentedArDen = null;

        scoped Span<double> brNumBuf;
        scoped Span<double> brDenBuf;
        scoped Span<double> arNumBuf;
        scoped Span<double> arDenBuf;

        if (period <= StackallocThreshold)
        {
            brNumBuf = stackalloc double[period];
            brDenBuf = stackalloc double[period];
            arNumBuf = stackalloc double[period];
            arDenBuf = stackalloc double[period];
        }
        else
        {
            rentedBrNum = ArrayPool<double>.Shared.Rent(period);
            rentedBrDen = ArrayPool<double>.Shared.Rent(period);
            rentedArNum = ArrayPool<double>.Shared.Rent(period);
            rentedArDen = ArrayPool<double>.Shared.Rent(period);

            brNumBuf = rentedBrNum.AsSpan(0, period);
            brDenBuf = rentedBrDen.AsSpan(0, period);
            arNumBuf = rentedArNum.AsSpan(0, period);
            arDenBuf = rentedArDen.AsSpan(0, period);
        }

        try
        {
            brNumBuf.Clear();
            brDenBuf.Clear();
            arNumBuf.Clear();
            arDenBuf.Clear();

            double brNumSum = 0.0;
            double brDenSum = 0.0;
            double arNumSum = 0.0;
            double arDenSum = 0.0;
            double prevClose = double.NaN;
            int idx = 0;

            for (int i = 0; i < len; i++)
            {
                double o = open[i];
                double h = high[i];
                double l = low[i];
                double c = close[i];

                // First bar: use open as prevClose if no prior close available
                double pc = double.IsFinite(prevClose) ? prevClose : o;

                double brNum = Math.Max(0.0, h - pc);
                double brDen = Math.Max(0.0, pc - l);
                double arNum = Math.Max(0.0, h - o);
                double arDen = Math.Max(0.0, o - l);

                brNumSum -= brNumBuf[idx];
                brDenSum -= brDenBuf[idx];
                arNumSum -= arNumBuf[idx];
                arDenSum -= arDenBuf[idx];

                brNumBuf[idx] = brNum;
                brDenBuf[idx] = brDen;
                arNumBuf[idx] = arNum;
                arDenBuf[idx] = arDen;

                brNumSum += brNum;
                brDenSum += brDen;
                arNumSum += arNum;
                arDenSum += arDen;

                idx = (idx + 1) % period;
                prevClose = c;

                brOutput[i] = brDenSum != 0.0 ? brNumSum / brDenSum * 100.0 : 100.0;
                arOutput[i] = arDenSum != 0.0 ? arNumSum / arDenSum * 100.0 : 100.0;
            }
        }
        finally
        {
            if (rentedBrNum != null) { ArrayPool<double>.Shared.Return(rentedBrNum); }
            if (rentedBrDen != null) { ArrayPool<double>.Shared.Return(rentedBrDen); }
            if (rentedArNum != null) { ArrayPool<double>.Shared.Return(rentedArNum); }
            if (rentedArDen != null) { ArrayPool<double>.Shared.Return(rentedArDen); }
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
