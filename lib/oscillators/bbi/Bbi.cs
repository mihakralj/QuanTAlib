// BBI: Bulls Bears Index
// Average of four SMAs with geometrically spaced periods (default 3, 6, 12, 24).
// Formula: BBI = (SMA(p1) + SMA(p2) + SMA(p3) + SMA(p4)) / 4
// Origin: Chinese technical analysis community.
// Source: bbi.pine

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// BBI: Bulls Bears Index
/// </summary>
/// <remarks>
/// Computes the arithmetic mean of four independent Simple Moving Averages with
/// geometrically spaced periods (default 3, 6, 12, 24). The composite line captures
/// trend consensus across ultra-short, short, medium, and long timeframes simultaneously.
/// Price above BBI signals bullish regime; price below BBI signals bearish regime.
///
/// Calculation (O(1) per bar via four independent circular-buffer SMAs):
///   BBI = (SMA(src, p1) + SMA(src, p2) + SMA(src, p3) + SMA(src, p4)) / 4
///
/// Default parameters: p1=3, p2=6, p3=12, p4=24
/// WarmupPeriod = max(p1, p2, p3, p4)
///
/// Sources:
///   - Chinese Securities Association technical analysis specifications
///   - TradingView community: "BBI - Bull and Bear Index"
/// </remarks>
[SkipLocalsInit]
public sealed class Bbi : AbstractBase
{
    private const int DefaultP1 = 3;
    private const int DefaultP2 = 6;
    private const int DefaultP3 = 12;
    private const int DefaultP4 = 24;

    private readonly int _p1, _p2, _p3, _p4;

    // Four independent O(1) circular-buffer SMAs
    private readonly double[] _buf1, _buf2, _buf3, _buf4;

    // All scalar state in one record struct for atomic _ps=_s snapshot (bar correction).
    // PrevSlotX = the value that was at buf[headX] BEFORE the most recent isNew=true write.
    // On isNew=false, restore buf[_ps.HeadX] = _s.PrevSlotX, then _s = _ps.
    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double Sum1, int Head1, int Count1, double PrevSlot1,
        double Sum2, int Head2, int Count2, double PrevSlot2,
        double Sum3, int Head3, int Count3, double PrevSlot3,
        double Sum4, int Head4, int Count4, double PrevSlot4,
        int Index, double LastValid);

    private State _s;
    private State _ps;

    /// <summary>
    /// Creates BBI with four customizable SMA periods.
    /// </summary>
    /// <param name="p1">Ultra-short SMA period (must be &gt; 0)</param>
    /// <param name="p2">Short SMA period (must be &gt; 0)</param>
    /// <param name="p3">Medium SMA period (must be &gt; 0)</param>
    /// <param name="p4">Long SMA period (must be &gt; 0)</param>
    public Bbi(int p1 = DefaultP1, int p2 = DefaultP2, int p3 = DefaultP3, int p4 = DefaultP4)
    {
        if (p1 <= 0)
        {
            throw new ArgumentException("Period 1 must be greater than 0", nameof(p1));
        }
        if (p2 <= 0)
        {
            throw new ArgumentException("Period 2 must be greater than 0", nameof(p2));
        }
        if (p3 <= 0)
        {
            throw new ArgumentException("Period 3 must be greater than 0", nameof(p3));
        }
        if (p4 <= 0)
        {
            throw new ArgumentException("Period 4 must be greater than 0", nameof(p4));
        }

        _p1 = p1; _p2 = p2; _p3 = p3; _p4 = p4;

        _buf1 = new double[p1];
        _buf2 = new double[p2];
        _buf3 = new double[p3];
        _buf4 = new double[p4];

        WarmupPeriod = Math.Max(Math.Max(p1, p2), Math.Max(p3, p4));
        Name = $"Bbi({p1},{p2},{p3},{p4})";
        _s = default;
        _ps = _s;
    }

    /// <summary>
    /// Creates BBI subscribed to a source publisher.
    /// </summary>
    public Bbi(ITValuePublisher source,
               int p1 = DefaultP1, int p2 = DefaultP2, int p3 = DefaultP3, int p4 = DefaultP4)
        : this(p1, p2, p3, p4)
    {
        source.Pub += Handle;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>True when enough bars have been processed for valid (full-window) output.</summary>
    public override bool IsHot => _s.Index >= WarmupPeriod;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _ps = _s;
        }
        else
        {
            // Restore the ring-buffer slots that were overwritten by the most-recent isNew=true pass.
            // _ps.HeadX = the write-head position used during that pass.
            // _s.PrevSlotX = the value that was at that head BEFORE the write.
            _buf1[_ps.Head1] = _s.PrevSlot1;
            _buf2[_ps.Head2] = _s.PrevSlot2;
            _buf3[_ps.Head3] = _s.PrevSlot3;
            _buf4[_ps.Head4] = _s.PrevSlot4;
            _s = _ps;
        }

        // Local copy for JIT register promotion
        double sum1 = _s.Sum1; int h1 = _s.Head1; int c1 = _s.Count1;
        double sum2 = _s.Sum2; int h2 = _s.Head2; int c2 = _s.Count2;
        double sum3 = _s.Sum3; int h3 = _s.Head3; int c3 = _s.Count3;
        double sum4 = _s.Sum4; int h4 = _s.Head4; int c4 = _s.Count4;
        int index = _s.Index;
        double lastValid = _s.LastValid;

        // NaN/Infinity substitution
        double val = input.Value;
        if (!double.IsFinite(val))
        {
            val = double.IsFinite(lastValid) ? lastValid : 0.0;
        }
        else
        {
            lastValid = val;
        }

        if (isNew)
        {
            index++;
        }

        // ── SMA 1: capture slot BEFORE writing (for bar-correction restore next time) ──
        double prev1 = _buf1[h1];
        sum1 = c1 < _p1 ? sum1 + val - prev1 : sum1 - prev1 + val;
        if (c1 < _p1) { c1++; }
        _buf1[h1] = val;
        int newH1 = isNew ? (h1 + 1) % _p1 : h1;

        // ── SMA 2 ────────────────────────────────────────────────────────────
        double prev2 = _buf2[h2];
        sum2 = c2 < _p2 ? sum2 + val - prev2 : sum2 - prev2 + val;
        if (c2 < _p2) { c2++; }
        _buf2[h2] = val;
        int newH2 = isNew ? (h2 + 1) % _p2 : h2;

        // ── SMA 3 ────────────────────────────────────────────────────────────
        double prev3 = _buf3[h3];
        sum3 = c3 < _p3 ? sum3 + val - prev3 : sum3 - prev3 + val;
        if (c3 < _p3) { c3++; }
        _buf3[h3] = val;
        int newH3 = isNew ? (h3 + 1) % _p3 : h3;

        // ── SMA 4 ────────────────────────────────────────────────────────────
        double prev4 = _buf4[h4];
        sum4 = c4 < _p4 ? sum4 + val - prev4 : sum4 - prev4 + val;
        if (c4 < _p4) { c4++; }
        _buf4[h4] = val;
        int newH4 = isNew ? (h4 + 1) % _p4 : h4;

        // ── Composite BBI ────────────────────────────────────────────────────
        double sma1 = sum1 / Math.Max(1, c1);
        double sma2 = sum2 / Math.Max(1, c2);
        double sma3 = sum3 / Math.Max(1, c3);
        double sma4 = sum4 / Math.Max(1, c4);
        double bbi = (sma1 + sma2 + sma3 + sma4) * 0.25;

        // Write back state — store PrevSlotX for next bar-correction restore
        _s = new State(
            sum1, newH1, c1, prev1,
            sum2, newH2, c2, prev2,
            sum3, newH3, c3, prev3,
            sum4, newH4, c4, prev4,
            index, lastValid);

        Last = new TValue(input.Time, bbi);
        PubEvent(Last, isNew);
        return Last;
    }
    public override TSeries Update(TSeries source)
    {
        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, _p1, _p2, _p3, _p4);
        source.Times.CopyTo(tSpan);

        // Prime streaming state for continued updates
        Reset();
        for (int i = 0; i < len; i++)
        {
            Update(new TValue(source.Times[i], source.Values[i]), isNew: true);
        }

        return new TSeries(t, v);
    }
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(DateTime.UtcNow, source[i]), isNew: true);
        }
    }
    public override void Reset()
    {
        Array.Clear(_buf1);
        Array.Clear(_buf2);
        Array.Clear(_buf3);
        Array.Clear(_buf4);
        _s = default;
        _ps = _s;
        Last = default;
    }

    // ── Static Batch (TSeries) ───────────────────────────────────────────────

    /// <summary>Calculates BBI for an entire <see cref="TSeries"/>.</summary>
    public static TSeries Batch(
        TSeries source,
        int p1 = DefaultP1, int p2 = DefaultP2, int p3 = DefaultP3, int p4 = DefaultP4)
    {
        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        Batch(source.Values, vSpan, p1, p2, p3, p4);
        source.Times.CopyTo(tSpan);

        return new TSeries(t, v);
    }

    // ── Static Batch (Span) ──────────────────────────────────────────────────

    /// <summary>
    /// Zero-allocation span-based BBI calculation using ArrayPool for ring buffers.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(
        ReadOnlySpan<double> source,
        Span<double> output,
        int p1 = DefaultP1, int p2 = DefaultP2, int p3 = DefaultP3, int p4 = DefaultP4)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }
        if (p1 <= 0)
        {
            throw new ArgumentException("Period 1 must be greater than 0", nameof(p1));
        }
        if (p2 <= 0)
        {
            throw new ArgumentException("Period 2 must be greater than 0", nameof(p2));
        }
        if (p3 <= 0)
        {
            throw new ArgumentException("Period 3 must be greater than 0", nameof(p3));
        }
        if (p4 <= 0)
        {
            throw new ArgumentException("Period 4 must be greater than 0", nameof(p4));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        double[] b1 = ArrayPool<double>.Shared.Rent(p1);
        double[] b2 = ArrayPool<double>.Shared.Rent(p2);
        double[] b3 = ArrayPool<double>.Shared.Rent(p3);
        double[] b4 = ArrayPool<double>.Shared.Rent(p4);

        b1.AsSpan(0, p1).Clear();
        b2.AsSpan(0, p2).Clear();
        b3.AsSpan(0, p3).Clear();
        b4.AsSpan(0, p4).Clear();

        try
        {
            double sum1 = 0, sum2 = 0, sum3 = 0, sum4 = 0;
            int h1 = 0, h2 = 0, h3 = 0, h4 = 0;
            int c1 = 0, c2 = 0, c3 = 0, c4 = 0;
            double lastValid = 0.0;

            for (int i = 0; i < len; i++)
            {
                double val = source[i];
                if (!double.IsFinite(val))
                {
                    val = lastValid;
                }
                else
                {
                    lastValid = val;
                }

                double old1 = b1[h1]; sum1 = c1 < p1 ? sum1 + val - old1 : sum1 - old1 + val; if (c1 < p1) { c1++; }
                b1[h1] = val; h1 = (h1 + 1) % p1;
                double old2 = b2[h2]; sum2 = c2 < p2 ? sum2 + val - old2 : sum2 - old2 + val; if (c2 < p2) { c2++; }
                b2[h2] = val; h2 = (h2 + 1) % p2;
                double old3 = b3[h3]; sum3 = c3 < p3 ? sum3 + val - old3 : sum3 - old3 + val; if (c3 < p3) { c3++; }
                b3[h3] = val; h3 = (h3 + 1) % p3;
                double old4 = b4[h4]; sum4 = c4 < p4 ? sum4 + val - old4 : sum4 - old4 + val; if (c4 < p4) { c4++; }
                b4[h4] = val; h4 = (h4 + 1) % p4;

                output[i] = ((sum1 / Math.Max(1, c1)) + (sum2 / Math.Max(1, c2))
                           + (sum3 / Math.Max(1, c3)) + (sum4 / Math.Max(1, c4))) * 0.25;
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(b1);
            ArrayPool<double>.Shared.Return(b2);
            ArrayPool<double>.Shared.Return(b3);
            ArrayPool<double>.Shared.Return(b4);
        }
    }

    /// <summary>Creates a BBI instance and calculates results for the source series.</summary>
    public static (TSeries Results, Bbi Indicator) Calculate(
        TSeries source,
        int p1 = DefaultP1, int p2 = DefaultP2, int p3 = DefaultP3, int p4 = DefaultP4)
    {
        var indicator = new Bbi(p1, p2, p3, p4);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }
}
