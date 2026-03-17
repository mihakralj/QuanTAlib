// QuanTAlib Python NativeAOT Exports — all ~290 [UnmanagedCallersOnly] entry points
// Organized by SPEC §8 categories. Each export validates inputs, wraps Batch in try/catch,
// and returns a status code (0=OK, 1=NULL_PTR, 2=INVALID_LENGTH, 3=INVALID_PARAM, 4=INTERNAL).
//
// Pattern key:
//   A = (src, n, out, scalar_params...)
//   B = (h, l, c, v, n, out, params...)      HLCV
//   C = (o, h, l, c, n, out, params...)      OHLC
//   D = (h, l, n, out, params...)            HL
//   E = (h, l, c, n, out, params...)         HLC
//   F = (actual, predicted, n, out, params...) dual-input error metrics
//   G = (src, vol, n, out, params...)        source+volume
//   H = (x, y, n, out, params...)           seriesX+seriesY
//   I = multi-output variants

#nullable enable
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using QuanTAlib;

namespace QuanTAlib.Python;

#pragma warning disable CA1031 // catch general exception types — ABI boundary requires catching all
#pragma warning disable IDE0060 // unused parameters — some reserved for future use

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Security Hotspot",
    "S6640",
    Justification = "NativeAOT unmanaged exports must accept raw caller-owned buffers. Each entry point validates null pointers and lengths before projecting them into spans, and the ABI surface intentionally centralizes the required unsafe context on the export type.")]
[SkipLocalsInit]
public static unsafe partial class Exports
{
    // ═══════════════════════════════════════════════════════════════════════
    //  Validation helpers — keep exports compact
    // ═══════════════════════════════════════════════════════════════════════

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Chk1(double* src, double* dst, int n)
    {
        if (src == null || dst == null) return StatusCodes.QTL_ERR_NULL_PTR;
        if (n <= 0) return StatusCodes.QTL_ERR_INVALID_LENGTH;
        return StatusCodes.QTL_OK;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Chk2(double* a, double* b, double* dst, int n)
    {
        if (a == null || b == null || dst == null) return StatusCodes.QTL_ERR_NULL_PTR;
        if (n <= 0) return StatusCodes.QTL_ERR_INVALID_LENGTH;
        return StatusCodes.QTL_OK;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Chk3(double* a, double* b, double* c, double* dst, int n)
    {
        if (a == null || b == null || c == null || dst == null) return StatusCodes.QTL_ERR_NULL_PTR;
        if (n <= 0) return StatusCodes.QTL_ERR_INVALID_LENGTH;
        return StatusCodes.QTL_OK;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Chk4(double* a, double* b, double* c, double* d, double* dst, int n)
    {
        if (a == null || b == null || c == null || d == null || dst == null) return StatusCodes.QTL_ERR_NULL_PTR;
        if (n <= 0) return StatusCodes.QTL_ERR_INVALID_LENGTH;
        return StatusCodes.QTL_OK;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ChkPeriod(int period)
        => period > 0 ? StatusCodes.QTL_OK : StatusCodes.QTL_ERR_INVALID_PARAM;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ChkAlpha(double alpha)
        => alpha > 0.0 && alpha <= 1.0 ? StatusCodes.QTL_OK : StatusCodes.QTL_ERR_INVALID_PARAM;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ReadOnlySpan<double> Src(double* p, int n) => new(p, n);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Span<double> Dst(double* p, int n) => new(p, n);

    // ═══════════════════════════════════════════════════════════════════════
    //  §0  Skeleton / version
    // ═══════════════════════════════════════════════════════════════════════

    [UnmanagedCallersOnly(EntryPoint = "qtl_skeleton_noop")]
    public static int SkeletonNoop() => StatusCodes.QTL_OK;

    // ═══════════════════════════════════════════════════════════════════════
    //  §8.1  Core (price transforms)
    // ═══════════════════════════════════════════════════════════════════════

    // Avgprice: Pattern C (OHLC, no params) — Batch(ROS open, ROS high, ROS low, ROS close, S out)
    [UnmanagedCallersOnly(EntryPoint = "qtl_avgprice")]
    public static int QtlAvgprice(double* open, double* high, double* low, double* close, int n, double* dst)
    {
        if (open == null || high == null || low == null || close == null || dst == null) return StatusCodes.QTL_ERR_NULL_PTR;
        if (n <= 0) return StatusCodes.QTL_ERR_INVALID_LENGTH;
        try { Avgprice.Batch(Src(open, n), Src(high, n), Src(low, n), Src(close, n), Dst(dst, n)); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Medprice: Pattern D (HL, no params) — Batch(ROS high, ROS low, S out)
    [UnmanagedCallersOnly(EntryPoint = "qtl_medprice")]
    public static int QtlMedprice(double* high, double* low, int n, double* dst)
    {
        int v = Chk2(high, low, dst, n); if (v != 0) return v;
        try { Medprice.Batch(Src(high, n), Src(low, n), Dst(dst, n)); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Typprice: Batch(ROS open, ROS high, ROS low, S out) — OHL, no close
    [UnmanagedCallersOnly(EntryPoint = "qtl_typprice")]
    public static int QtlTypprice(double* open, double* high, double* low, int n, double* dst)
    {
        if (open == null || high == null || low == null || dst == null) return StatusCodes.QTL_ERR_NULL_PTR;
        if (n <= 0) return StatusCodes.QTL_ERR_INVALID_LENGTH;
        try { Typprice.Batch(Src(open, n), Src(high, n), Src(low, n), Dst(dst, n)); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Midbody: Batch(ROS open, ROS close, S out) — OC pattern
    [UnmanagedCallersOnly(EntryPoint = "qtl_midbody")]
    public static int QtlMidbody(double* open, double* close, int n, double* dst)
    {
        int v = Chk2(open, close, dst, n); if (v != 0) return v;
        try { Midbody.Batch(Src(open, n), Src(close, n), Dst(dst, n)); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  §8.2  Momentum
    // ═══════════════════════════════════════════════════════════════════════

    // Rsi: Pattern A (src, out, int period)
    [UnmanagedCallersOnly(EntryPoint = "qtl_rsi")]
    public static int QtlRsi(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Rsi.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Roc: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_roc")]
    public static int QtlRoc(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Roc.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Mom: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_mom")]
    public static int QtlMom(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Mom.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Cmo: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_cmo")]
    public static int QtlCmo(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Cmo.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Tsi: Pattern A (src, out, int longPeriod, int shortPeriod)
    [UnmanagedCallersOnly(EntryPoint = "qtl_tsi")]
    public static int QtlTsi(double* src, int n, double* dst, int longPeriod, int shortPeriod)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        if (longPeriod <= 0 || shortPeriod <= 0) return StatusCodes.QTL_ERR_INVALID_PARAM;
        try { Tsi.Batch(Src(src, n), Dst(dst, n), longPeriod, shortPeriod); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Apo: Pattern A (src, out, int fast, int slow)
    [UnmanagedCallersOnly(EntryPoint = "qtl_apo")]
    public static int QtlApo(double* src, int n, double* dst, int fast, int slow)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        if (fast <= 0 || slow <= 0) return StatusCodes.QTL_ERR_INVALID_PARAM;
        try { Apo.Batch(Src(src, n), Dst(dst, n), fast, slow); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Bias: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_bias")]
    public static int QtlBias(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Bias.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Cfo: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_cfo")]
    public static int QtlCfo(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Cfo.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Cfb: Pattern A + int[] lengths — special: null-safe lengths array
    [UnmanagedCallersOnly(EntryPoint = "qtl_cfb")]
    public static int QtlCfb(double* src, int n, double* dst, int* lengths, int lengthsCount)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        try
        {
            int[]? managed = null;
            if (lengths != null && lengthsCount > 0)
            {
                managed = new int[lengthsCount];
                new ReadOnlySpan<int>(lengths, lengthsCount).CopyTo(managed);
            }
            Cfb.Batch(Src(src, n), Dst(dst, n), managed);
            return StatusCodes.QTL_OK;
        }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Asi: Pattern C (OHLC + double limitMove)
    [UnmanagedCallersOnly(EntryPoint = "qtl_asi")]
    public static int QtlAsi(double* open, double* high, double* low, double* close, int n, double* dst, double limitMove)
    {
        int v = Chk4(open, high, low, close, dst, n); if (v != 0) return v;
        try { Asi.Batch(Src(open, n), Src(high, n), Src(low, n), Src(close, n), Dst(dst, n), limitMove); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Vwmacd: multi-output (close, volume → vwmacd, signal, histogram)
    [UnmanagedCallersOnly(EntryPoint = "qtl_vwmacd")]
    public static int QtlVwmacd(double* close, double* volume, int n,
        double* dstVwmacd, double* dstSignal, double* dstHist,
        int fastPeriod, int slowPeriod, int signalPeriod)
    {
        if (n <= 0) return StatusCodes.QTL_ERR_INVALID_LENGTH;
        if (close == null || volume == null ||
            dstVwmacd == null || dstSignal == null || dstHist == null) return StatusCodes.QTL_ERR_NULL_PTR;
        if (fastPeriod < 1 || slowPeriod < 1 || signalPeriod < 1) return StatusCodes.QTL_ERR_INVALID_PARAM;
        try
        {
            Vwmacd.Batch(
                Src(close, n), Src(volume, n),
                Dst(dstVwmacd, n), Dst(dstSignal, n), Dst(dstHist, n),
                fastPeriod, slowPeriod, signalPeriod);
            return StatusCodes.QTL_OK;
        }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  §8.3  Oscillators
    // ═══════════════════════════════════════════════════════════════════════

    // Fisher: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_fisher")]
    public static int QtlFisher(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Fisher.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Fisher04: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_fisher04")]
    public static int QtlFisher04(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Fisher04.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Dpo: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_dpo")]
    public static int QtlDpo(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Dpo.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Trix: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_trix")]
    public static int QtlTrix(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Trix.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Inertia: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_inertia")]
    public static int QtlInertia(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Inertia.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Rsx: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_rsx")]
    public static int QtlRsx(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Rsx.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Rrsi: Pattern A (dual period params)
    [UnmanagedCallersOnly(EntryPoint = "qtl_rrsi")]
    public static int QtlRrsi(double* src, int n, double* dst, int smoothLength, int rsiLength)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(smoothLength); if (v != 0) return v;
        v = ChkPeriod(rsiLength); if (v != 0) return v;
        try { Rrsi.Batch(Src(src, n), Dst(dst, n), smoothLength, rsiLength); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Er: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_er")]
    public static int QtlEr(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Er.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Cti: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_cti")]
    public static int QtlCti(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Cti.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Reflex: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_reflex")]
    public static int QtlReflex(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Reflex.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Trendflex: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_trendflex")]
    public static int QtlTrendflex(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Trendflex.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Kri: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_kri")]
    public static int QtlKri(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Kri.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Psl: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_psl")]
    public static int QtlPsl(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Psl.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Deco: Pattern A (src, out, int period, int emaPeriod)
    [UnmanagedCallersOnly(EntryPoint = "qtl_deco")]
    public static int QtlDeco(double* src, int n, double* dst, int period, int emaPeriod)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        if (period <= 0 || emaPeriod <= 0) return StatusCodes.QTL_ERR_INVALID_PARAM;
        try { Deco.Batch(Src(src, n), Dst(dst, n), period, emaPeriod); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Dosc: Pattern A (src, out, int shortPeriod, int longPeriod, int ppoShort, int ppoLong)
    [UnmanagedCallersOnly(EntryPoint = "qtl_dosc")]
    public static int QtlDosc(double* src, int n, double* dst, int p1, int p2, int p3, int p4)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        if (p1 <= 0 || p2 <= 0 || p3 <= 0 || p4 <= 0) return StatusCodes.QTL_ERR_INVALID_PARAM;
        try { Dosc.Batch(Src(src, n), Dst(dst, n), p1, p2, p3, p4); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Dymi: Pattern A (src, out, int p1..p5)
    [UnmanagedCallersOnly(EntryPoint = "qtl_dymi")]
    public static int QtlDymi(double* src, int n, double* dst, int p1, int p2, int p3, int p4, int p5)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        try { Dymi.Batch(Src(src, n), Dst(dst, n), p1, p2, p3, p4, p5); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Crsi: Pattern A (src, out, int rsiPeriod, int streakPeriod, int rankPeriod)
    [UnmanagedCallersOnly(EntryPoint = "qtl_crsi")]
    public static int QtlCrsi(double* src, int n, double* dst, int rsiPeriod, int streakPeriod, int rankPeriod)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        if (rsiPeriod <= 0 || streakPeriod <= 0 || rankPeriod <= 0) return StatusCodes.QTL_ERR_INVALID_PARAM;
        try { Crsi.Batch(Src(src, n), Dst(dst, n), rsiPeriod, streakPeriod, rankPeriod); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Bbb: Pattern A (src, out, int period, double mult)
    [UnmanagedCallersOnly(EntryPoint = "qtl_bbb")]
    public static int QtlBbb(double* src, int n, double* dst, int period, double mult)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Bbb.Batch(Src(src, n), Dst(dst, n), period, mult); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Bbi: Pattern A (src, out, int p1, int p2, int p3, int p4)
    [UnmanagedCallersOnly(EntryPoint = "qtl_bbi")]
    public static int QtlBbi(double* src, int n, double* dst, int p1, int p2, int p3, int p4)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        try { Bbi.Batch(Src(src, n), Dst(dst, n), p1, p2, p3, p4); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Dem: Pattern D (high, low, out, int period)
    [UnmanagedCallersOnly(EntryPoint = "qtl_dem")]
    public static int QtlDem(double* high, double* low, int n, double* dst, int period)
    {
        int v = Chk2(high, low, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Dem.Batch(Src(high, n), Src(low, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Brar: Pattern C (OHLC, 2 outputs, int period) — multi-output
    [UnmanagedCallersOnly(EntryPoint = "qtl_brar")]
    public static int QtlBrar(double* open, double* high, double* low, double* close, int n, double* dstBr, double* dstAr, int period)
    {
        if (open == null || high == null || low == null || close == null || dstBr == null || dstAr == null) return StatusCodes.QTL_ERR_NULL_PTR;
        if (n <= 0) return StatusCodes.QTL_ERR_INVALID_LENGTH;
        int v = ChkPeriod(period); if (v != 0) return v;
        try { Brar.Batch(Src(open, n), Src(high, n), Src(low, n), Src(close, n), Dst(dstBr, n), Dst(dstAr, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  §8.4  Trends — FIR
    // ═══════════════════════════════════════════════════════════════════════

    // Sma: Pattern A (src, out, int period)
    [UnmanagedCallersOnly(EntryPoint = "qtl_sma")]
    public static int QtlSma(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Sma.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Wma: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_wma")]
    public static int QtlWma(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Wma.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Hma: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_hma")]
    public static int QtlHma(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Hma.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Trima: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_trima")]
    public static int QtlTrima(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Trima.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Swma: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_swma")]
    public static int QtlSwma(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Swma.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Dwma: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_dwma")]
    public static int QtlDwma(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Dwma.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Blma: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_blma")]
    public static int QtlBlma(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Blma.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Alma: Pattern A (src, out, int period, double offset, double sigma)
    [UnmanagedCallersOnly(EntryPoint = "qtl_alma")]
    public static int QtlAlma(double* src, int n, double* dst, int period, double offset, double sigma)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Alma.Batch(Src(src, n), Dst(dst, n), period, offset, sigma); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Lsma: Pattern A (src, out, int period, int offset, double multiplier)
    [UnmanagedCallersOnly(EntryPoint = "qtl_lsma")]
    public static int QtlLsma(double* src, int n, double* dst, int period, int offset, double multiplier)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Lsma.Batch(Src(src, n), Dst(dst, n), period, offset, multiplier); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Sgma: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_sgma")]
    public static int QtlSgma(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Sgma.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Sinema: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_sinema")]
    public static int QtlSinema(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Sinema.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Hanma: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_hanma")]
    public static int QtlHanma(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Hanma.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Parzen: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_parzen")]
    public static int QtlParzen(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Parzen.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Tsf: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_tsf")]
    public static int QtlTsf(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Tsf.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Conv: Pattern A + double[] kernel
    [UnmanagedCallersOnly(EntryPoint = "qtl_conv")]
    public static int QtlConv(double* src, int n, double* dst, double* kernel, int kernelLen)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        if (kernel == null || kernelLen <= 0) return StatusCodes.QTL_ERR_INVALID_PARAM;
        try
        {
            var k = new double[kernelLen];
            new ReadOnlySpan<double>(kernel, kernelLen).CopyTo(k);
            Conv.Batch(Src(src, n), Dst(dst, n), k);
            return StatusCodes.QTL_OK;
        }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Bwma: Pattern A (src, out, int period, int polyOrder)
    [UnmanagedCallersOnly(EntryPoint = "qtl_bwma")]
    public static int QtlBwma(double* src, int n, double* dst, int period, int polyOrder)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Bwma.Batch(Src(src, n), Dst(dst, n), period, polyOrder); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Crma: Pattern A (src, out, int period, double volumeFactor)
    [UnmanagedCallersOnly(EntryPoint = "qtl_crma")]
    public static int QtlCrma(double* src, int n, double* dst, int period, double volumeFactor)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Crma.Batch(Src(src, n), Dst(dst, n), period, volumeFactor); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Sp15: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_sp15")]
    public static int QtlSp15(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Sp15.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Tukey_w: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_tukey_w")]
    public static int QtlTukeyW(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Tukey_w.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Rain: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_rain")]
    public static int QtlRain(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Rain.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Afirma: Pattern A + WindowType enum (mapped to int)
    [UnmanagedCallersOnly(EntryPoint = "qtl_afirma")]
    public static int QtlAfirma(double* src, int n, double* dst, int period, int windowType, int useSimd)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Afirma.Batch(Src(src, n), Dst(dst, n), period, (Afirma.WindowType)windowType, useSimd != 0); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  §8.5  Trends — IIR
    // ═══════════════════════════════════════════════════════════════════════

    // Ema: takes double alpha — export both period (with conversion) and alpha variants
    [UnmanagedCallersOnly(EntryPoint = "qtl_ema")]
    public static int QtlEma(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Ema.Batch(Src(src, n), Dst(dst, n), 2.0 / (period + 1)); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    [UnmanagedCallersOnly(EntryPoint = "qtl_ema_alpha")]
    public static int QtlEmaAlpha(double* src, int n, double* dst, double alpha)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkAlpha(alpha); if (v != 0) return v;
        try { Ema.Batch(Src(src, n), Dst(dst, n), alpha); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Dema: double alpha
    [UnmanagedCallersOnly(EntryPoint = "qtl_dema")]
    public static int QtlDema(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Dema.Batch(Src(src, n), Dst(dst, n), 2.0 / (period + 1)); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    [UnmanagedCallersOnly(EntryPoint = "qtl_dema_alpha")]
    public static int QtlDemaAlpha(double* src, int n, double* dst, double alpha)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkAlpha(alpha); if (v != 0) return v;
        try { Dema.Batch(Src(src, n), Dst(dst, n), alpha); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Tema: double alpha
    [UnmanagedCallersOnly(EntryPoint = "qtl_tema")]
    public static int QtlTema(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Tema.Batch(Src(src, n), Dst(dst, n), 2.0 / (period + 1)); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Lema: double alpha
    [UnmanagedCallersOnly(EntryPoint = "qtl_lema")]
    public static int QtlLema(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Lema.Batch(Src(src, n), Dst(dst, n), 2.0 / (period + 1)); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Hema: int period
    [UnmanagedCallersOnly(EntryPoint = "qtl_hema")]
    public static int QtlHema(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Hema.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Ahrens: int period
    [UnmanagedCallersOnly(EntryPoint = "qtl_ahrens")]
    public static int QtlAhrens(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Ahrens.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Decycler: int period
    [UnmanagedCallersOnly(EntryPoint = "qtl_decycler")]
    public static int QtlDecycler(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Decycler.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Dsma: int period, double volumeFactor
    [UnmanagedCallersOnly(EntryPoint = "qtl_dsma")]
    public static int QtlDsma(double* src, int n, double* dst, int period, double factor)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Dsma.Batch(Src(src, n), Dst(dst, n), period, factor); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Gdema: int period, double volumeFactor
    [UnmanagedCallersOnly(EntryPoint = "qtl_gdema")]
    public static int QtlGdema(double* src, int n, double* dst, int period, double factor)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Gdema.Batch(Src(src, n), Dst(dst, n), period, factor); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Coral: int period, double friction
    [UnmanagedCallersOnly(EntryPoint = "qtl_coral")]
    public static int QtlCoral(double* src, int n, double* dst, int period, double friction)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Coral.Batch(Src(src, n), Dst(dst, n), period, friction); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Agc: double alpha
    [UnmanagedCallersOnly(EntryPoint = "qtl_agc")]
    public static int QtlAgc(double* src, int n, double* dst, double alpha)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        try { Agc.Batch(Src(src, n), Dst(dst, n), alpha); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Ccyc: double alpha
    [UnmanagedCallersOnly(EntryPoint = "qtl_ccyc")]
    public static int QtlCcyc(double* src, int n, double* dst, double alpha)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        try { Ccyc.Batch(Src(src, n), Dst(dst, n), alpha); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  §8.6  Channels
    // ═══════════════════════════════════════════════════════════════════════

    // Bbands: Pattern I (src, upper, mid, lower, int period, double mult)
    [UnmanagedCallersOnly(EntryPoint = "qtl_bbands")]
    public static int QtlBbands(double* src, int n, double* upper, double* mid, double* lower, int period, double mult)
    {
        if (src == null || upper == null || mid == null || lower == null) return StatusCodes.QTL_ERR_NULL_PTR;
        if (n <= 0) return StatusCodes.QTL_ERR_INVALID_LENGTH;
        int v = ChkPeriod(period); if (v != 0) return v;
        try { Bbands.Batch(Src(src, n), Dst(mid, n), Dst(upper, n), Dst(lower, n), period, mult); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // AtrBands: Pattern E-I (HLC, 3 outputs, int period, double mult)
    [UnmanagedCallersOnly(EntryPoint = "qtl_atrbands")]
    public static int QtlAtrBands(double* high, double* low, double* close, int n, double* upper, double* mid, double* lower, int period, double mult)
    {
        if (high == null || low == null || close == null || upper == null || mid == null || lower == null) return StatusCodes.QTL_ERR_NULL_PTR;
        if (n <= 0) return StatusCodes.QTL_ERR_INVALID_LENGTH;
        int v = ChkPeriod(period); if (v != 0) return v;
        try { AtrBands.Batch(Src(high, n), Src(low, n), Src(close, n), Dst(mid, n), Dst(upper, n), Dst(lower, n), period, mult); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Apchannel: Pattern D-I (HL, 2 outputs, double multiplier)
    [UnmanagedCallersOnly(EntryPoint = "qtl_apchannel")]
    public static int QtlApchannel(double* high, double* low, int n, double* dstUp, double* dstLow, double multiplier)
    {
        if (high == null || low == null || dstUp == null || dstLow == null) return StatusCodes.QTL_ERR_NULL_PTR;
        if (n <= 0) return StatusCodes.QTL_ERR_INVALID_LENGTH;
        try { Apchannel.Batch(Src(high, n), Src(low, n), Dst(dstUp, n), Dst(dstLow, n), multiplier); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Hwc: multi-output (src → upper, middle, lower; int period, double multiplier)
    [UnmanagedCallersOnly(EntryPoint = "qtl_hwc")]
    public static int QtlHwc(double* src, int n,
        double* dstUpper, double* dstMiddle, double* dstLower,
        int period, double multiplier)
    {
        if (n <= 0) return StatusCodes.QTL_ERR_INVALID_LENGTH;
        if (src == null || dstUpper == null || dstMiddle == null || dstLower == null) return StatusCodes.QTL_ERR_NULL_PTR;
        if (period < 1) return StatusCodes.QTL_ERR_INVALID_PARAM;
        try
        {
            Hwc.Batch(
                Src(src, n),
                Dst(dstUpper, n), Dst(dstMiddle, n), Dst(dstLower, n),
                period, multiplier);
            return StatusCodes.QTL_OK;
        }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  §8.7  Volatility
    // ═══════════════════════════════════════════════════════════════════════

    // Tr: Pattern E (HLC, no params)
    [UnmanagedCallersOnly(EntryPoint = "qtl_tr")]
    public static int QtlTr(double* high, double* low, double* close, int n, double* dst)
    {
        int v = Chk3(high, low, close, dst, n); if (v != 0) return v;
        try { Tr.Batch(Src(high, n), Src(low, n), Src(close, n), Dst(dst, n)); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Bbw: Pattern A (src, out, int period, double mult)
    [UnmanagedCallersOnly(EntryPoint = "qtl_bbw")]
    public static int QtlBbw(double* src, int n, double* dst, int period, double mult)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Bbw.Batch(Src(src, n), Dst(dst, n), period, mult); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Bbwn: Pattern A (src, out, int period, double mult, int lookback)
    [UnmanagedCallersOnly(EntryPoint = "qtl_bbwn")]
    public static int QtlBbwn(double* src, int n, double* dst, int period, double mult, int lookback)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Bbwn.Batch(Src(src, n), Dst(dst, n), period, mult, lookback); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Bbwp: Pattern A (src, out, int period, double mult, int lookback)
    [UnmanagedCallersOnly(EntryPoint = "qtl_bbwp")]
    public static int QtlBbwp(double* src, int n, double* dst, int period, double mult, int lookback)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Bbwp.Batch(Src(src, n), Dst(dst, n), period, mult, lookback); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // StdDev: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_stddev")]
    public static int QtlStdDev(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { StdDev.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Variance: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_variance")]
    public static int QtlVariance(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Variance.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Etherm: Pattern D (high, low, out, int period)
    [UnmanagedCallersOnly(EntryPoint = "qtl_etherm")]
    public static int QtlEtherm(double* high, double* low, int n, double* dst, int period)
    {
        int v = Chk2(high, low, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Etherm.Batch(Src(high, n), Src(low, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Ccv: Pattern A (src, out, int shortPeriod, int longPeriod)
    [UnmanagedCallersOnly(EntryPoint = "qtl_ccv")]
    public static int QtlCcv(double* src, int n, double* dst, int shortPeriod, int longPeriod)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        if (shortPeriod <= 0 || longPeriod <= 0) return StatusCodes.QTL_ERR_INVALID_PARAM;
        try { Ccv.Batch(Src(src, n), Dst(dst, n), shortPeriod, longPeriod); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Cv: Pattern A (src, out, int period, double minVol, double maxVol)
    [UnmanagedCallersOnly(EntryPoint = "qtl_cv")]
    public static int QtlCv(double* src, int n, double* dst, int period, double minVol, double maxVol)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Cv.Batch(Src(src, n), Dst(dst, n), period, minVol, maxVol); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Cvi: Pattern A (src, out, int emaPeriod, int rocPeriod)
    [UnmanagedCallersOnly(EntryPoint = "qtl_cvi")]
    public static int QtlCvi(double* src, int n, double* dst, int emaPeriod, int rocPeriod)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        if (emaPeriod <= 0 || rocPeriod <= 0) return StatusCodes.QTL_ERR_INVALID_PARAM;
        try { Cvi.Batch(Src(src, n), Dst(dst, n), emaPeriod, rocPeriod); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Ewma: Pattern A (src, out, int period, bool isPopulation, int annFactor)
    [UnmanagedCallersOnly(EntryPoint = "qtl_ewma")]
    public static int QtlEwma(double* src, int n, double* dst, int period, int isPopulation, int annFactor)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Ewma.Batch(Src(src, n), Dst(dst, n), period, isPopulation != 0, annFactor); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  §8.8  Volume
    // ═══════════════════════════════════════════════════════════════════════

    // Obv: Pattern G (close, volume, out)
    [UnmanagedCallersOnly(EntryPoint = "qtl_obv")]
    public static int QtlObv(double* close, double* volume, int n, double* dst)
    {
        int v = Chk2(close, volume, dst, n); if (v != 0) return v;
        try { Obv.Batch(Src(close, n), Src(volume, n), Dst(dst, n)); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Pvt: Pattern G
    [UnmanagedCallersOnly(EntryPoint = "qtl_pvt")]
    public static int QtlPvt(double* close, double* volume, int n, double* dst)
    {
        int v = Chk2(close, volume, dst, n); if (v != 0) return v;
        try { Pvt.Batch(Src(close, n), Src(volume, n), Dst(dst, n)); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Pvr: Pattern G
    [UnmanagedCallersOnly(EntryPoint = "qtl_pvr")]
    public static int QtlPvr(double* close, double* volume, int n, double* dst)
    {
        int v = Chk2(close, volume, dst, n); if (v != 0) return v;
        try { Pvr.Batch(Src(close, n), Src(volume, n), Dst(dst, n)); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Vf: Pattern G
    [UnmanagedCallersOnly(EntryPoint = "qtl_vf")]
    public static int QtlVf(double* close, double* volume, int n, double* dst)
    {
        int v = Chk2(close, volume, dst, n); if (v != 0) return v;
        try { Vf.Batch(Src(close, n), Src(volume, n), Dst(dst, n)); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Nvi: Pattern G
    [UnmanagedCallersOnly(EntryPoint = "qtl_nvi")]
    public static int QtlNvi(double* close, double* volume, int n, double* dst)
    {
        int v = Chk2(close, volume, dst, n); if (v != 0) return v;
        try { Nvi.Batch(Src(close, n), Src(volume, n), Dst(dst, n)); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Pvi: Pattern G
    [UnmanagedCallersOnly(EntryPoint = "qtl_pvi")]
    public static int QtlPvi(double* close, double* volume, int n, double* dst)
    {
        int v = Chk2(close, volume, dst, n); if (v != 0) return v;
        try { Pvi.Batch(Src(close, n), Src(volume, n), Dst(dst, n)); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Tvi: Pattern G + int period
    [UnmanagedCallersOnly(EntryPoint = "qtl_tvi")]
    public static int QtlTvi(double* close, double* volume, int n, double* dst, int period)
    {
        int v = Chk2(close, volume, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Tvi.Batch(Src(close, n), Src(volume, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Pvd: Pattern G + int period
    [UnmanagedCallersOnly(EntryPoint = "qtl_pvd")]
    public static int QtlPvd(double* close, double* volume, int n, double* dst, int period)
    {
        int v = Chk2(close, volume, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Pvd.Batch(Src(close, n), Src(volume, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Vwma: Pattern G + int period
    [UnmanagedCallersOnly(EntryPoint = "qtl_vwma")]
    public static int QtlVwma(double* close, double* volume, int n, double* dst, int period)
    {
        int v = Chk2(close, volume, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Vwma.Batch(Src(close, n), Src(volume, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Evwma: Pattern G + int period
    [UnmanagedCallersOnly(EntryPoint = "qtl_evwma")]
    public static int QtlEvwma(double* close, double* volume, int n, double* dst, int period)
    {
        int v = Chk2(close, volume, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Evwma.Batch(Src(close, n), Src(volume, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Efi: Pattern G + int period
    [UnmanagedCallersOnly(EntryPoint = "qtl_efi")]
    public static int QtlEfi(double* close, double* volume, int n, double* dst, int period)
    {
        int v = Chk2(close, volume, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Efi.Batch(Src(close, n), Src(volume, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Aobv: Pattern G, 2 outputs (fast, slow)
    [UnmanagedCallersOnly(EntryPoint = "qtl_aobv")]
    public static int QtlAobv(double* close, double* volume, int n, double* dstFast, double* dstSlow)
    {
        if (close == null || volume == null || dstFast == null || dstSlow == null) return StatusCodes.QTL_ERR_NULL_PTR;
        if (n <= 0) return StatusCodes.QTL_ERR_INVALID_LENGTH;
        try { Aobv.Batch(Src(close, n), Src(volume, n), Dst(dstFast, n), Dst(dstSlow, n)); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Mfi: Pattern B (HLCV + int period)
    [UnmanagedCallersOnly(EntryPoint = "qtl_mfi")]
    public static int QtlMfi(double* high, double* low, double* close, double* volume, int n, double* dst, int period)
    {
        if (high == null || low == null || close == null || volume == null || dst == null) return StatusCodes.QTL_ERR_NULL_PTR;
        if (n <= 0) return StatusCodes.QTL_ERR_INVALID_LENGTH;
        int v = ChkPeriod(period); if (v != 0) return v;
        try { Mfi.Batch(Src(high, n), Src(low, n), Src(close, n), Src(volume, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Cmf: Pattern B (HLCV + int period)
    [UnmanagedCallersOnly(EntryPoint = "qtl_cmf")]
    public static int QtlCmf(double* high, double* low, double* close, double* volume, int n, double* dst, int period)
    {
        if (high == null || low == null || close == null || volume == null || dst == null) return StatusCodes.QTL_ERR_NULL_PTR;
        if (n <= 0) return StatusCodes.QTL_ERR_INVALID_LENGTH;
        int v = ChkPeriod(period); if (v != 0) return v;
        try { Cmf.Batch(Src(high, n), Src(low, n), Src(close, n), Src(volume, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Eom: Pattern (HL + volume, int period, double divisor) — 3 inputs + 2 params
    [UnmanagedCallersOnly(EntryPoint = "qtl_eom")]
    public static int QtlEom(double* high, double* low, double* volume, int n, double* dst, int period, double divisor)
    {
        if (high == null || low == null || volume == null || dst == null) return StatusCodes.QTL_ERR_NULL_PTR;
        if (n <= 0) return StatusCodes.QTL_ERR_INVALID_LENGTH;
        int v = ChkPeriod(period); if (v != 0) return v;
        try { Eom.Batch(Src(high, n), Src(low, n), Src(volume, n), Dst(dst, n), period, divisor); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Pvo: Pattern I (volume, 3 outputs, int fast, int slow, int signal)
    [UnmanagedCallersOnly(EntryPoint = "qtl_pvo")]
    public static int QtlPvo(double* volume, int n, double* dstPvo, double* dstSignal, double* dstHist, int fast, int slow, int signal)
    {
        if (volume == null || dstPvo == null || dstSignal == null || dstHist == null) return StatusCodes.QTL_ERR_NULL_PTR;
        if (n <= 0) return StatusCodes.QTL_ERR_INVALID_LENGTH;
        if (fast <= 0 || slow <= 0 || signal <= 0) return StatusCodes.QTL_ERR_INVALID_PARAM;
        try { Pvo.Batch(Src(volume, n), Dst(dstPvo, n), Dst(dstSignal, n), Dst(dstHist, n), fast, slow, signal); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  §8.9  Statistics
    // ═══════════════════════════════════════════════════════════════════════

    // Zscore: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_zscore")]
    public static int QtlZscore(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Zscore.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Cma: Pattern A (src, out) — no params
    [UnmanagedCallersOnly(EntryPoint = "qtl_cma")]
    public static int QtlCma(double* src, int n, double* dst)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        try { Cma.Batch(Src(src, n), Dst(dst, n)); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Entropy: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_entropy")]
    public static int QtlEntropy(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Entropy.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Correlation: Pattern H (x, y, out, int period)
    [UnmanagedCallersOnly(EntryPoint = "qtl_correlation")]
    public static int QtlCorrelation(double* x, double* y, int n, double* dst, int period)
    {
        int v = Chk2(x, y, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Correl.Batch(Src(x, n), Src(y, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Covariance: Pattern H + bool isPopulation
    [UnmanagedCallersOnly(EntryPoint = "qtl_covariance")]
    public static int QtlCovariance(double* x, double* y, int n, double* dst, int period, int isPopulation)
    {
        int v = Chk2(x, y, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Covariance.Batch(Src(x, n), Src(y, n), Dst(dst, n), period, isPopulation != 0); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Cointegration: Pattern H
    [UnmanagedCallersOnly(EntryPoint = "qtl_cointegration")]
    public static int QtlCointegration(double* x, double* y, int n, double* dst, int period)
    {
        int v = Chk2(x, y, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Cointegration.Batch(Src(x, n), Src(y, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Convexity: multi-output (asset, market → betaStd, betaUp, betaDown, ratio, convexity)
    [UnmanagedCallersOnly(EntryPoint = "qtl_convexity")]
    public static int QtlConvexity(double* asset, double* market, int n,
        double* dstBetaStd, double* dstBetaUp, double* dstBetaDown,
        double* dstRatio, double* dstConvexity, int period)
    {
        if (n <= 0) return StatusCodes.QTL_ERR_INVALID_LENGTH;
        if (asset == null || market == null ||
            dstBetaStd == null || dstBetaUp == null || dstBetaDown == null ||
            dstRatio == null || dstConvexity == null) return StatusCodes.QTL_ERR_NULL_PTR;
        if (period < 1) return StatusCodes.QTL_ERR_INVALID_PARAM;
        try
        {
            Convexity.Batch(
                Src(asset, n), Src(market, n),
                Dst(dstBetaStd, n), Dst(dstBetaUp, n), Dst(dstBetaDown, n),
                Dst(dstRatio, n), Dst(dstConvexity, n), period);
            return StatusCodes.QTL_OK;
        }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  §8.10  Error Metrics
    // ═══════════════════════════════════════════════════════════════════════

    // Mse: Pattern F (actual, predicted, out, int period)
    [UnmanagedCallersOnly(EntryPoint = "qtl_mse")]
    public static int QtlMse(double* actual, double* predicted, int n, double* dst, int period)
    {
        int v = Chk2(actual, predicted, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Mse.Batch(Src(actual, n), Src(predicted, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Rmse: Pattern F
    [UnmanagedCallersOnly(EntryPoint = "qtl_rmse")]
    public static int QtlRmse(double* actual, double* predicted, int n, double* dst, int period)
    {
        int v = Chk2(actual, predicted, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Rmse.Batch(Src(actual, n), Src(predicted, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Mae: Pattern F
    [UnmanagedCallersOnly(EntryPoint = "qtl_mae")]
    public static int QtlMae(double* actual, double* predicted, int n, double* dst, int period)
    {
        int v = Chk2(actual, predicted, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Mae.Batch(Src(actual, n), Src(predicted, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Mape: Pattern F
    [UnmanagedCallersOnly(EntryPoint = "qtl_mape")]
    public static int QtlMape(double* actual, double* predicted, int n, double* dst, int period)
    {
        int v = Chk2(actual, predicted, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Mape.Batch(Src(actual, n), Src(predicted, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  §8.11  Filters
    // ═══════════════════════════════════════════════════════════════════════

    // Bessel: Pattern A (src, out, int period)
    [UnmanagedCallersOnly(EntryPoint = "qtl_bessel")]
    public static int QtlBessel(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Bessel.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Butter2: Pattern A (src, out, int period, double gain)
    [UnmanagedCallersOnly(EntryPoint = "qtl_butter2")]
    public static int QtlButter2(double* src, int n, double* dst, int period, double gain)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Butter2.Batch(Src(src, n), Dst(dst, n), period, gain); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Butter3: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_butter3")]
    public static int QtlButter3(double* src, int n, double* dst, int period, double gain)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Butter3.Batch(Src(src, n), Dst(dst, n), period, gain); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Cheby1: Pattern A (int period, double ripple)
    [UnmanagedCallersOnly(EntryPoint = "qtl_cheby1")]
    public static int QtlCheby1(double* src, int n, double* dst, int period, double ripple)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Cheby1.Batch(Src(src, n), Dst(dst, n), period, ripple); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Cheby2: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_cheby2")]
    public static int QtlCheby2(double* src, int n, double* dst, int period, double ripple)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Cheby2.Batch(Src(src, n), Dst(dst, n), period, ripple); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Elliptic: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_elliptic")]
    public static int QtlElliptic(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Elliptic.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Edcf: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_edcf")]
    public static int QtlEdcf(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Edcf.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Bpf: Pattern A (int period, int bandwidth)
    [UnmanagedCallersOnly(EntryPoint = "qtl_bpf")]
    public static int QtlBpf(double* src, int n, double* dst, int period, int bandwidth)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Bpf.Batch(Src(src, n), Dst(dst, n), period, bandwidth); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // ALaguerre: Pattern A (int period, int order)
    [UnmanagedCallersOnly(EntryPoint = "qtl_alaguerre")]
    public static int QtlALaguerre(double* src, int n, double* dst, int period, int order)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { ALaguerre.Batch(Src(src, n), Dst(dst, n), period, order); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Bilateral: Pattern A (int period, double sigmaS, double sigmaR)
    [UnmanagedCallersOnly(EntryPoint = "qtl_bilateral")]
    public static int QtlBilateral(double* src, int n, double* dst, int period, double sigmaS, double sigmaR)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Bilateral.Batch(Src(src, n), Dst(dst, n), period, sigmaS, sigmaR); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // BaxterKing: Pattern A (int period, int minPeriod, int maxPeriod)
    [UnmanagedCallersOnly(EntryPoint = "qtl_baxterking")]
    public static int QtlBaxterKing(double* src, int n, double* dst, int period, int minPeriod, int maxPeriod)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { BaxterKing.Batch(Src(src, n), Dst(dst, n), period, minPeriod, maxPeriod); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Cfitz: Pattern A (int period, int bandwidthPeriod)
    [UnmanagedCallersOnly(EntryPoint = "qtl_cfitz")]
    public static int QtlCfitz(double* src, int n, double* dst, int period, int bandwidthPeriod)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Cfitz.Batch(Src(src, n), Dst(dst, n), period, bandwidthPeriod); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  §8.12  Cycles
    // ═══════════════════════════════════════════════════════════════════════

    // Cg: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_cg")]
    public static int QtlCg(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Cg.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Dsp: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_dsp")]
    public static int QtlDsp(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Dsp.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Ccor: Pattern A (int period, double alpha)
    [UnmanagedCallersOnly(EntryPoint = "qtl_ccor")]
    public static int QtlCcor(double* src, int n, double* dst, int period, double alpha)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Ccor.Batch(Src(src, n), Dst(dst, n), period, alpha); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Ebsw: Pattern A (int period, int hpPeriod)
    [UnmanagedCallersOnly(EntryPoint = "qtl_ebsw")]
    public static int QtlEbsw(double* src, int n, double* dst, int period, int hpPeriod)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Ebsw.Batch(Src(src, n), Dst(dst, n), period, hpPeriod); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Eacp: (int minPeriod, int maxPeriod, int avgLength, bool enhance → int)
    [UnmanagedCallersOnly(EntryPoint = "qtl_eacp")]
    public static int QtlEacp(double* src, int n, double* dst, int minPeriod, int maxPeriod, int avgLength, int enhance)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(minPeriod); if (v != 0) return v;
        try { Eacp.Batch(Src(src, n), Dst(dst, n), minPeriod, maxPeriod, avgLength, enhance != 0); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  §8.14  Numerics / transforms
    // ═══════════════════════════════════════════════════════════════════════

    // Change: Pattern A
    [UnmanagedCallersOnly(EntryPoint = "qtl_change")]
    public static int QtlChange(double* src, int n, double* dst, int period)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Change.Batch(Src(src, n), Dst(dst, n), period); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Exptrans: Pattern A (no params)
    [UnmanagedCallersOnly(EntryPoint = "qtl_exptrans")]
    public static int QtlExptrans(double* src, int n, double* dst)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        try { Exptrans.Batch(Src(src, n), Dst(dst, n)); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Betadist: Pattern A (int period, double alpha, double beta)
    [UnmanagedCallersOnly(EntryPoint = "qtl_betadist")]
    public static int QtlBetadist(double* src, int n, double* dst, int period, double alpha, double beta)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Betadist.Batch(Src(src, n), Dst(dst, n), period, alpha, beta); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Expdist: Pattern A (int period, double lambda)
    [UnmanagedCallersOnly(EntryPoint = "qtl_expdist")]
    public static int QtlExpdist(double* src, int n, double* dst, int period, double lambda)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Expdist.Batch(Src(src, n), Dst(dst, n), period, lambda); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Binomdist: Pattern A (int period, int trials, int successes)
    [UnmanagedCallersOnly(EntryPoint = "qtl_binomdist")]
    public static int QtlBinomdist(double* src, int n, double* dst, int period, int trials, int successes)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Binomdist.Batch(Src(src, n), Dst(dst, n), period, trials, successes); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Cwt: Pattern A (double scale, double omega)
    [UnmanagedCallersOnly(EntryPoint = "qtl_cwt")]
    public static int QtlCwt(double* src, int n, double* dst, double scale, double omega)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        try { Cwt.Batch(Src(src, n), Dst(dst, n), scale, omega); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // Dwt: Pattern A (int period, int levels)
    [UnmanagedCallersOnly(EntryPoint = "qtl_dwt")]
    public static int QtlDwt(double* src, int n, double* dst, int period, int levels)
    {
        int v = Chk1(src, dst, n); if (v != 0) return v;
        v = ChkPeriod(period); if (v != 0) return v;
        try { Dwt.Batch(Src(src, n), Dst(dst, n), period, levels); return StatusCodes.QTL_OK; }
        catch { return StatusCodes.QTL_ERR_INTERNAL; }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  §8.16  Forecasts
    // ═══════════════════════════════════════════════════════════════════════

    // Afirma already exported above in §8.4

    // ═══════════════════════════════════════════════════════════════════════
    //  Rs: Pattern H (seriesX, seriesY, out, int period) — Momentum/Statistics
    // ═══════════════════════════════════════════════════════════════════════

    // Note: Rs might not have a span overload — check at build time
}

#pragma warning restore CA1031
#pragma warning restore IDE0060
