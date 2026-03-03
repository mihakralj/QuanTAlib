// DEM: DeMarker Oscillator
// Measures demand by comparing current bar's High/Low against the previous bar's High/Low.
// Tom DeMark, "The New Science of Technical Analysis" (1994).

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// DEM: DeMarker Oscillator
/// </summary>
/// <remarks>
/// Bounded [0, 1] oscillator measuring sequential buying/selling pressure:
/// <list type="bullet">
///   <item>DeMax = max(High − prevHigh, 0)</item>
///   <item>DeMin = max(prevLow − Low, 0)</item>
///   <item>DEM = SMA(DeMax, period) / (SMA(DeMax, period) + SMA(DeMin, period))</item>
/// </list>
/// Two O(1) rolling sums via circular buffers — 2 additions + 2 subtractions per bar
/// regardless of period length. Guard: zero denominator → 0.5 (neutral).
///
/// References:
///   DeMark, Tom (1994). The New Science of Technical Analysis.
///   PineScript reference: dem.pine
/// </remarks>
[SkipLocalsInit]
public sealed class Dem : ITValuePublisher
{
    private readonly int _period;

    // Two circular buffers for O(1) SMA rolling sums
    private readonly double[] _deMaxBuf;
    private readonly double[] _deMinBuf;

    // Snapshots for idempotent isNew=false rollback — full array copy required
    // because isNew=false must restore the exact buffer state before the last new bar
    private readonly double[] _deMaxSnap;
    private readonly double[] _deMinSnap;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double DeMaxSum,
        double DeMinSum,
        double PrevHigh,
        double PrevLow,
        double LastValid,
        int Count,
        int Idx);

    private State _s;
    private State _ps;

    private readonly TBarPublishedHandler _barHandler;

    /// <summary>Display name for the indicator.</summary>
    public string Name { get; }

    /// <summary>Bars required for the first valid output.</summary>
    public int WarmupPeriod { get; }

    /// <summary>True once the rolling window is fully populated (needs period+1 bars).</summary>
    public bool IsHot => _s.Count > _period;

    /// <summary>Current DEM value in [0, 1].</summary>
    public TValue Last { get; private set; }

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Creates DEM with the specified SMA period.
    /// </summary>
    /// <param name="period">SMA lookback period (must be &gt;= 1, default 14)</param>
    public Dem(int period = 14)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        _period = period;
        _deMaxBuf = new double[period];
        _deMinBuf = new double[period];
        _deMaxSnap = new double[period];
        _deMinSnap = new double[period];

        _s = new State(0, 0, double.NaN, double.NaN, 0.5, 0, 0);
        _ps = _s;

        WarmupPeriod = period + 1;
        Name = $"Dem({period})";
        _barHandler = HandleBar;
    }

    /// <summary>
    /// Creates DEM chained to a TBarSeries source.
    /// </summary>
    public Dem(TBarSeries source, int period = 14) : this(period)
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
        _s = new State(0, 0, double.NaN, double.NaN, 0.5, 0, 0);
        _ps = _s;
        Last = default;
        Array.Clear(_deMaxBuf);
        Array.Clear(_deMinBuf);
        Array.Clear(_deMaxSnap);
        Array.Clear(_deMinSnap);
    }

    /// <summary>
    /// Updates DEM with a new OHLCV bar.
    /// </summary>
    /// <param name="input">OHLCV bar data</param>
    /// <param name="isNew">True to advance state; false to rewrite the latest bar</param>
    /// <returns>Current DEM value as TValue</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        var s = _s;

        if (isNew)
        {
            // Snapshot buffers before mutation — required for idempotent rollback
            _ps = s;
            Array.Copy(_deMaxBuf, _deMaxSnap, _period);
            Array.Copy(_deMinBuf, _deMinSnap, _period);
            s.Count++;
        }
        else
        {
            // Rollback: restore scalar state + both buffer snapshots
            s = _ps;
            Array.Copy(_deMaxSnap, _deMaxBuf, _period);
            Array.Copy(_deMinSnap, _deMinBuf, _period);
        }

        // Sanitize OHLC inputs — use last-valid on NaN/Infinity
        double rawHigh = input.High;
        double rawLow = input.Low;
        double high = double.IsFinite(rawHigh) ? rawHigh : s.LastValid;
        double low = double.IsFinite(rawLow) ? rawLow : s.LastValid;

        // First bar: no previous high/low — DeMax=DeMin=0 by convention
        double prevHigh = double.IsFinite(s.PrevHigh) ? s.PrevHigh : high;
        double prevLow = double.IsFinite(s.PrevLow) ? s.PrevLow : low;

        // Per-bar demand/supply components
        double deMax = Math.Max(high - prevHigh, 0.0);
        double deMin = Math.Max(prevLow - low, 0.0);

        // O(1) circular-buffer rolling sums: subtract outgoing, write new, add incoming
        int idx = s.Idx;

        s.DeMaxSum -= _deMaxBuf[idx];
        s.DeMinSum -= _deMinBuf[idx];

        _deMaxBuf[idx] = deMax;
        _deMinBuf[idx] = deMin;

        s.DeMaxSum += deMax;
        s.DeMinSum += deMin;

        // Advance circular index only on new bars
        if (isNew)
        {
            s.Idx = (idx + 1) % _period;
        }

        // Compute DEM — default to 0.5 (neutral) on zero denominator
        double denom = s.DeMaxSum + s.DeMinSum;
        double dem = denom != 0.0 ? s.DeMaxSum / denom : 0.5;

        // Store last valid value for NaN protection
        if (double.IsFinite(dem))
        {
            s.LastValid = dem;
        }

        // Store current high/low as next bar's prev
        s.PrevHigh = high;
        s.PrevLow = low;

        _s = s;

        Last = new TValue(input.Time, IsHot ? dem : s.LastValid);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>
    /// Updates DEM from a scalar TValue (uses Val as proxy; High=Low=Val).
    /// Primarily for ITValuePublisher compatibility — not the natural input for DEM.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        double v = double.IsFinite(input.Value) ? input.Value : _s.LastValid;
        return Update(new TBar(input.Time, v, v, v, v, 0), isNew);
    }

    /// <summary>
    /// Batch-computes DEM over raw High/Low spans. Zero-allocation path for large datasets.
    /// </summary>
    /// <param name="high">Source high prices</param>
    /// <param name="low">Source low prices</param>
    /// <param name="output">Destination span for DEM values</param>
    /// <param name="period">SMA period (must be &gt; 0)</param>
    public static void Batch(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        Span<double> output,
        int period = 14)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        int len = high.Length;

        if (low.Length != len)
        {
            throw new ArgumentException("Low length must match high length", nameof(low));
        }

        if (output.Length != len)
        {
            throw new ArgumentException("Output length must match input length", nameof(output));
        }

        if (len == 0)
        {
            return;
        }

        const int StackallocThreshold = 256;

        double[]? rentedMax = null;
        double[]? rentedMin = null;

        scoped Span<double> deMaxBuf;
        scoped Span<double> deMinBuf;

        if (period <= StackallocThreshold)
        {
            deMaxBuf = stackalloc double[period];
            deMinBuf = stackalloc double[period];
        }
        else
        {
            rentedMax = ArrayPool<double>.Shared.Rent(period);
            rentedMin = ArrayPool<double>.Shared.Rent(period);
            deMaxBuf = rentedMax.AsSpan(0, period);
            deMinBuf = rentedMin.AsSpan(0, period);
        }

        try
        {
            deMaxBuf.Clear();
            deMinBuf.Clear();

            double deMaxSum = 0.0;
            double deMinSum = 0.0;
            double prevHigh = double.NaN;
            double prevLow = double.NaN;
            int idx = 0;

            for (int i = 0; i < len; i++)
            {
                double h = high[i];
                double l = low[i];

                // First bar bootstrap: DeMax=DeMin=0
                double ph = double.IsFinite(prevHigh) ? prevHigh : h;
                double pl = double.IsFinite(prevLow) ? prevLow : l;

                double deMax = Math.Max(h - ph, 0.0);
                double deMin = Math.Max(pl - l, 0.0);

                deMaxSum -= deMaxBuf[idx];
                deMinSum -= deMinBuf[idx];

                deMaxBuf[idx] = deMax;
                deMinBuf[idx] = deMin;

                deMaxSum += deMax;
                deMinSum += deMin;

                idx = (idx + 1) % period;
                prevHigh = h;
                prevLow = l;

                double denom = deMaxSum + deMinSum;
                output[i] = denom != 0.0 ? deMaxSum / denom : 0.5;
            }
        }
        finally
        {
            if (rentedMax != null) { ArrayPool<double>.Shared.Return(rentedMax); }
            if (rentedMin != null) { ArrayPool<double>.Shared.Return(rentedMin); }
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
