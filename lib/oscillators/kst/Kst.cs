// KST: Know Sure Thing Oscillator
// Weighted sum of 4 smoothed Rate-of-Change values + signal line (SMA of KST).
// Formula: KST = 1*SMA(ROC(r1),s1) + 2*SMA(ROC(r2),s2) + 3*SMA(ROC(r3),s3) + 4*SMA(ROC(r4),s4)
// Signal = SMA(KST, sigPeriod)
// Source: Martin Pring, "The KST System", Technical Analysis of Stocks & Commodities (1992)

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// KST: Know Sure Thing Oscillator
/// </summary>
/// <remarks>
/// KST combines four Rate-of-Change values at progressively longer lookback periods,
/// smooths each with an independent SMA, then combines with linear weights (1,2,3,4).
/// A signal line (SMA of KST) provides crossover triggers.
///
/// Calculation:
/// 1. ROC_k = (price / price[r_k] - 1) * 100  for k in {1,2,3,4}
/// 2. SM_k   = SMA(ROC_k, s_k)
/// 3. KST    = 1*SM1 + 2*SM2 + 3*SM3 + 4*SM4
/// 4. Signal = SMA(KST, sigPeriod)
///
/// Default parameters: r=(10,15,20,30), s=(10,10,10,15), sigPeriod=9
///
/// Sources:
/// - Pring, M.J. (1992). "The KST System." Technical Analysis of Stocks &amp; Commodities
/// - Pring, M.J. (2002). Technical Analysis Explained, 4th ed. McGraw-Hill
/// </remarks>
[SkipLocalsInit]
public sealed class Kst : ITValuePublisher
{
    private const int DefaultR1 = 10;
    private const int DefaultR2 = 15;
    private const int DefaultR3 = 20;
    private const int DefaultR4 = 30;
    private const int DefaultS1 = 10;
    private const int DefaultS2 = 10;
    private const int DefaultS3 = 10;
    private const int DefaultS4 = 15;
    private const int DefaultSigPeriod = 9;

    private readonly int _r1, _r2, _r3, _r4;
    private readonly int _s1, _s2, _s3, _s4;
    private readonly int _sigPeriod;

    // ROC lookback circular buffers — ring size = rN+1 (slot 0 is overwritten when full)
    private readonly double[] _p1, _p2, _p3, _p4;
    // SMA running-sum circular buffers for each ROC channel
    private readonly double[] _sma1, _sma2, _sma3, _sma4;
    // SMA buffer for signal line
    private readonly double[] _sigBuf;

    // All scalar state in one record struct — enables _ps = _s snapshot for bar-correction.
    // PrevXxx fields capture the ring-buffer slot value BEFORE each isNew=true write,
    // so isNew=false can restore those slots to their pre-write state.
    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        int P1Head, int P2Head, int P3Head, int P4Head,
        double PrevP1, double PrevP2, double PrevP3, double PrevP4,
        double Sum1, int SmaHead1, int SmaCount1, double PrevSma1,
        double Sum2, int SmaHead2, int SmaCount2, double PrevSma2,
        double Sum3, int SmaHead3, int SmaCount3, double PrevSma3,
        double Sum4, int SmaHead4, int SmaCount4, double PrevSma4,
        double SigSum, int SigHead, int SigCount, double PrevSig,
        int Count, double LastValidPrice);

    private State _s;
    private State _ps;

    public string Name { get; }
    public int WarmupPeriod { get; }
    public TValue Last { get; private set; }

    /// <summary>Primary KST line value.</summary>
    public TValue KstValue { get; private set; }

    /// <summary>Signal line value (SMA of KST).</summary>
    public TValue Signal { get; private set; }

    /// <summary>True when enough bars have been processed for valid output.</summary>
    public bool IsHot => _s.Count >= WarmupPeriod;

    public event TValuePublishedHandler? Pub;

    public Kst(
        int r1 = DefaultR1, int r2 = DefaultR2, int r3 = DefaultR3, int r4 = DefaultR4,
        int s1 = DefaultS1, int s2 = DefaultS2, int s3 = DefaultS3, int s4 = DefaultS4,
        int sigPeriod = DefaultSigPeriod)
    {
        if (r1 <= 0)
        {
            throw new ArgumentException("ROC period 1 must be greater than 0", nameof(r1));
        }
        if (r2 <= 0)
        {
            throw new ArgumentException("ROC period 2 must be greater than 0", nameof(r2));
        }
        if (r3 <= 0)
        {
            throw new ArgumentException("ROC period 3 must be greater than 0", nameof(r3));
        }
        if (r4 <= 0)
        {
            throw new ArgumentException("ROC period 4 must be greater than 0", nameof(r4));
        }
        if (s1 <= 0)
        {
            throw new ArgumentException("SMA period 1 must be greater than 0", nameof(s1));
        }
        if (s2 <= 0)
        {
            throw new ArgumentException("SMA period 2 must be greater than 0", nameof(s2));
        }
        if (s3 <= 0)
        {
            throw new ArgumentException("SMA period 3 must be greater than 0", nameof(s3));
        }
        if (s4 <= 0)
        {
            throw new ArgumentException("SMA period 4 must be greater than 0", nameof(s4));
        }
        if (sigPeriod <= 0)
        {
            throw new ArgumentException("Signal period must be greater than 0", nameof(sigPeriod));
        }

        _r1 = r1; _r2 = r2; _r3 = r3; _r4 = r4;
        _s1 = s1; _s2 = s2; _s3 = s3; _s4 = s4;
        _sigPeriod = sigPeriod;

        _p1 = new double[r1 + 1];
        _p2 = new double[r2 + 1];
        _p3 = new double[r3 + 1];
        _p4 = new double[r4 + 1];
        _sma1 = new double[s1];
        _sma2 = new double[s2];
        _sma3 = new double[s3];
        _sma4 = new double[s4];
        _sigBuf = new double[sigPeriod];

        // Warmup: need max_roc bars until ROC valid + max_sma for SMA warmup + sig for signal warmup
        WarmupPeriod = Math.Max(Math.Max(r1, r2), Math.Max(r3, r4))
                     + Math.Max(Math.Max(s1, s2), Math.Max(s3, s4))
                     + sigPeriod - 2;

        _s = default;
        _ps = _s;
        Name = $"Kst({r1},{r2},{r3},{r4},{s1},{s2},{s3},{s4},{sigPeriod})";
    }

    public Kst(ITValuePublisher source,
        int r1 = DefaultR1, int r2 = DefaultR2, int r3 = DefaultR3, int r4 = DefaultR4,
        int s1 = DefaultS1, int s2 = DefaultS2, int s3 = DefaultS3, int s4 = DefaultS4,
        int sigPeriod = DefaultSigPeriod)
        : this(r1, r2, r3, r4, s1, s2, s3, s4, sigPeriod)
    {
        source.Pub += Handle;
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PubEvent(TValue value, bool isNew = true) =>
        Pub?.Invoke(this, new TValueEventArgs { Value = value, IsNew = isNew });

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _ps = _s;
        }
        else
        {
            // Restore ring-buffer slots that were overwritten by the most recent isNew=true call.
            // _ps.XxxHead = slot index written during bar N (head BEFORE the advance).
            // _s.PrevXxx  = the value that was at that slot BEFORE bar N wrote it.
            // Using _s (not _ps) for values because _s captured them during bar N processing.
            _p1[_ps.P1Head] = _s.PrevP1;
            _p2[_ps.P2Head] = _s.PrevP2;
            _p3[_ps.P3Head] = _s.PrevP3;
            _p4[_ps.P4Head] = _s.PrevP4;
            _sma1[_ps.SmaHead1] = _s.PrevSma1;
            _sma2[_ps.SmaHead2] = _s.PrevSma2;
            _sma3[_ps.SmaHead3] = _s.PrevSma3;
            _sma4[_ps.SmaHead4] = _s.PrevSma4;
            _sigBuf[_ps.SigHead] = _s.PrevSig;
            _s = _ps;
        }

        // Local copy for JIT register promotion
        int p1H = _s.P1Head, p2H = _s.P2Head, p3H = _s.P3Head, p4H = _s.P4Head;
        double sum1 = _s.Sum1; int sh1 = _s.SmaHead1; int sc1 = _s.SmaCount1;
        double sum2 = _s.Sum2; int sh2 = _s.SmaHead2; int sc2 = _s.SmaCount2;
        double sum3 = _s.Sum3; int sh3 = _s.SmaHead3; int sc3 = _s.SmaCount3;
        double sum4 = _s.Sum4; int sh4 = _s.SmaHead4; int sc4 = _s.SmaCount4;
        double sigSum = _s.SigSum; int sigH = _s.SigHead; int sigC = _s.SigCount;
        int count = _s.Count;
        double lastValid = _s.LastValidPrice;

        double price = input.Value;
        if (!double.IsFinite(price))
        {
            price = double.IsFinite(lastValid) ? lastValid : 0.0;
        }
        else
        {
            lastValid = price;
        }

        if (isNew)
        {
            count++;
        }

        // ── ROC lookback ring buffers ─────────────────────────────────────────
        // Capture the slot value BEFORE writing (needed to restore on next isNew=false call)
        double prevP1 = _p1[p1H];
        double prevP2 = _p2[p2H];
        double prevP3 = _p3[p3H];
        double prevP4 = _p4[p4H];
        double prev1 = prevP1;
        double prev2 = prevP2;
        double prev3 = prevP3;
        double prev4 = prevP4;

        _p1[p1H] = price;
        _p2[p2H] = price;
        _p3[p3H] = price;
        _p4[p4H] = price;

        if (isNew)
        {
            p1H = (p1H + 1) % (_r1 + 1);
            p2H = (p2H + 1) % (_r2 + 1);
            p3H = (p3H + 1) % (_r3 + 1);
            p4H = (p4H + 1) % (_r4 + 1);
        }

        // ── ROC values ────────────────────────────────────────────────────────
        double roc1 = prev1 != 0.0 ? 100.0 * (price - prev1) / prev1 : 0.0;
        double roc2 = prev2 != 0.0 ? 100.0 * (price - prev2) / prev2 : 0.0;
        double roc3 = prev3 != 0.0 ? 100.0 * (price - prev3) / prev3 : 0.0;
        double roc4 = prev4 != 0.0 ? 100.0 * (price - prev4) / prev4 : 0.0;

        // ── SMA of each ROC via running-sum ring buffer ───────────────────────
        double sm1 = StepSma(_sma1, ref sum1, ref sh1, ref sc1, roc1, _s1, isNew, out double prevSma1);
        double sm2 = StepSma(_sma2, ref sum2, ref sh2, ref sc2, roc2, _s2, isNew, out double prevSma2);
        double sm3 = StepSma(_sma3, ref sum3, ref sh3, ref sc3, roc3, _s3, isNew, out double prevSma3);
        double sm4 = StepSma(_sma4, ref sum4, ref sh4, ref sc4, roc4, _s4, isNew, out double prevSma4);

        // ── KST composite (weighted sum, FMA for w1..w3) ─────────────────────
        double kstVal = Math.FusedMultiplyAdd(3.0, sm3, Math.FusedMultiplyAdd(2.0, sm2, sm1))
                      + 4.0 * sm4;

        // ── Signal line (SMA of KST) ──────────────────────────────────────────
        double sigVal = StepSma(_sigBuf, ref sigSum, ref sigH, ref sigC, kstVal, _sigPeriod, isNew, out double prevSig);

        // ── Write back local state (including pre-write slot snapshots) ───────
        _s = new State(
            p1H, p2H, p3H, p4H,
            prevP1, prevP2, prevP3, prevP4,
            sum1, sh1, sc1, prevSma1,
            sum2, sh2, sc2, prevSma2,
            sum3, sh3, sc3, prevSma3,
            sum4, sh4, sc4, prevSma4,
            sigSum, sigH, sigC, prevSig,
            count, lastValid);

        KstValue = new TValue(input.Time, kstVal);
        Signal = new TValue(input.Time, sigVal);
        Last = KstValue;

        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>Updates streaming state from a <see cref="TSeries"/> and returns dual output series.</summary>
    public (TSeries Kst, TSeries Signal) Update(TSeries source)
    {
        int len = source.Count;
        if (len == 0)
        {
            return (new TSeries([], []), new TSeries([], []));
        }

        var tK = new List<long>(len);
        var vK = new List<double>(len);
        var tS = new List<long>(len);
        var vS = new List<double>(len);
        CollectionsMarshal.SetCount(tK, len);
        CollectionsMarshal.SetCount(vK, len);
        CollectionsMarshal.SetCount(tS, len);
        CollectionsMarshal.SetCount(vS, len);

        Batch(source.Values,
              CollectionsMarshal.AsSpan(vK),
              CollectionsMarshal.AsSpan(vS),
              _r1, _r2, _r3, _r4, _s1, _s2, _s3, _s4, _sigPeriod);

        var tSpan = CollectionsMarshal.AsSpan(tK);
        source.Times.CopyTo(tSpan);
        tSpan.CopyTo(CollectionsMarshal.AsSpan(tS));

        // Prime streaming state for continued updates
        Reset();
        for (int i = 0; i < len; i++)
        {
            Update(new TValue(source.Times[i], source.Values[i]), isNew: true);
        }

        return (new TSeries(tK, vK), new TSeries(tS, vS));
    }

    /// <summary>Resets all internal state.</summary>
    public void Reset()
    {
        Array.Clear(_p1);
        Array.Clear(_p2);
        Array.Clear(_p3);
        Array.Clear(_p4);
        Array.Clear(_sma1);
        Array.Clear(_sma2);
        Array.Clear(_sma3);
        Array.Clear(_sma4);
        Array.Clear(_sigBuf);
        _s = default;
        _ps = _s;
        Last = default;
        KstValue = default;
        Signal = default;
    }

    // ── Static Span Batch ────────────────────────────────────────────────────

    /// <summary>
    /// Calculates KST and Signal for the full source span. Uses ArrayPool for all intermediate buffers.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(
        ReadOnlySpan<double> source,
        Span<double> kstOut,
        Span<double> sigOut,
        int r1 = DefaultR1, int r2 = DefaultR2, int r3 = DefaultR3, int r4 = DefaultR4,
        int s1 = DefaultS1, int s2 = DefaultS2, int s3 = DefaultS3, int s4 = DefaultS4,
        int sigPeriod = DefaultSigPeriod)
    {
        if (source.Length != kstOut.Length)
        {
            throw new ArgumentException("Source and kstOut must have the same length", nameof(kstOut));
        }
        if (source.Length != sigOut.Length)
        {
            throw new ArgumentException("Source and sigOut must have the same length", nameof(sigOut));
        }
        if (r1 <= 0)
        {
            throw new ArgumentException("ROC period 1 must be greater than 0", nameof(r1));
        }
        if (r2 <= 0)
        {
            throw new ArgumentException("ROC period 2 must be greater than 0", nameof(r2));
        }
        if (r3 <= 0)
        {
            throw new ArgumentException("ROC period 3 must be greater than 0", nameof(r3));
        }
        if (r4 <= 0)
        {
            throw new ArgumentException("ROC period 4 must be greater than 0", nameof(r4));
        }
        if (s1 <= 0)
        {
            throw new ArgumentException("SMA period 1 must be greater than 0", nameof(s1));
        }
        if (s2 <= 0)
        {
            throw new ArgumentException("SMA period 2 must be greater than 0", nameof(s2));
        }
        if (s3 <= 0)
        {
            throw new ArgumentException("SMA period 3 must be greater than 0", nameof(s3));
        }
        if (s4 <= 0)
        {
            throw new ArgumentException("SMA period 4 must be greater than 0", nameof(s4));
        }
        if (sigPeriod <= 0)
        {
            throw new ArgumentException("Signal period must be greater than 0", nameof(sigPeriod));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        int rBuf1 = r1 + 1, rBuf2 = r2 + 1, rBuf3 = r3 + 1, rBuf4 = r4 + 1;

        double[] p1 = ArrayPool<double>.Shared.Rent(rBuf1);
        double[] p2 = ArrayPool<double>.Shared.Rent(rBuf2);
        double[] p3 = ArrayPool<double>.Shared.Rent(rBuf3);
        double[] p4 = ArrayPool<double>.Shared.Rent(rBuf4);
        double[] sm1b = ArrayPool<double>.Shared.Rent(s1);
        double[] sm2b = ArrayPool<double>.Shared.Rent(s2);
        double[] sm3b = ArrayPool<double>.Shared.Rent(s3);
        double[] sm4b = ArrayPool<double>.Shared.Rent(s4);
        double[] sigb = ArrayPool<double>.Shared.Rent(sigPeriod);

        p1.AsSpan(0, rBuf1).Clear();
        p2.AsSpan(0, rBuf2).Clear();
        p3.AsSpan(0, rBuf3).Clear();
        p4.AsSpan(0, rBuf4).Clear();
        sm1b.AsSpan(0, s1).Clear();
        sm2b.AsSpan(0, s2).Clear();
        sm3b.AsSpan(0, s3).Clear();
        sm4b.AsSpan(0, s4).Clear();
        sigb.AsSpan(0, sigPeriod).Clear();

        try
        {
            int ph1 = 0, ph2 = 0, ph3 = 0, ph4 = 0;
            double sum1 = 0, sum2 = 0, sum3 = 0, sum4 = 0, sumSig = 0;
            int sh1 = 0, sh2 = 0, sh3 = 0, sh4 = 0, shSig = 0;
            int sc1 = 0, sc2 = 0, sc3 = 0, sc4 = 0, scSig = 0;
            double lastValid = 0.0;

            for (int i = 0; i < len; i++)
            {
                double price = source[i];
                if (!double.IsFinite(price))
                {
                    price = lastValid;
                }
                else
                {
                    lastValid = price;
                }

                double prev1 = p1[ph1]; p1[ph1] = price; ph1 = (ph1 + 1) % rBuf1;
                double prev2 = p2[ph2]; p2[ph2] = price; ph2 = (ph2 + 1) % rBuf2;
                double prev3 = p3[ph3]; p3[ph3] = price; ph3 = (ph3 + 1) % rBuf3;
                double prev4 = p4[ph4]; p4[ph4] = price; ph4 = (ph4 + 1) % rBuf4;

                double roc1 = prev1 != 0.0 ? 100.0 * (price - prev1) / prev1 : 0.0;
                double roc2 = prev2 != 0.0 ? 100.0 * (price - prev2) / prev2 : 0.0;
                double roc3 = prev3 != 0.0 ? 100.0 * (price - prev3) / prev3 : 0.0;
                double roc4 = prev4 != 0.0 ? 100.0 * (price - prev4) / prev4 : 0.0;

                double sm1 = BatchStepSma(sm1b, s1, ref sum1, ref sh1, ref sc1, roc1);
                double sm2 = BatchStepSma(sm2b, s2, ref sum2, ref sh2, ref sc2, roc2);
                double sm3 = BatchStepSma(sm3b, s3, ref sum3, ref sh3, ref sc3, roc3);
                double sm4 = BatchStepSma(sm4b, s4, ref sum4, ref sh4, ref sc4, roc4);

                double kstVal = Math.FusedMultiplyAdd(3.0, sm3, Math.FusedMultiplyAdd(2.0, sm2, sm1))
                              + 4.0 * sm4;

                sigOut[i] = BatchStepSma(sigb, sigPeriod, ref sumSig, ref shSig, ref scSig, kstVal);
                kstOut[i] = kstVal;
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(p1);
            ArrayPool<double>.Shared.Return(p2);
            ArrayPool<double>.Shared.Return(p3);
            ArrayPool<double>.Shared.Return(p4);
            ArrayPool<double>.Shared.Return(sm1b);
            ArrayPool<double>.Shared.Return(sm2b);
            ArrayPool<double>.Shared.Return(sm3b);
            ArrayPool<double>.Shared.Return(sm4b);
            ArrayPool<double>.Shared.Return(sigb);
        }
    }

    /// <summary>Calculates KST for an entire <see cref="TSeries"/>.</summary>
    public static (TSeries Kst, TSeries Signal) Batch(
        TSeries source,
        int r1 = DefaultR1, int r2 = DefaultR2, int r3 = DefaultR3, int r4 = DefaultR4,
        int s1 = DefaultS1, int s2 = DefaultS2, int s3 = DefaultS3, int s4 = DefaultS4,
        int sigPeriod = DefaultSigPeriod)
    {
        if (source == null || source.Count == 0)
        {
            return (new TSeries([], []), new TSeries([], []));
        }

        int len = source.Count;
        var tK = new List<long>(len);
        var vK = new List<double>(len);
        var tS = new List<long>(len);
        var vS = new List<double>(len);
        CollectionsMarshal.SetCount(tK, len);
        CollectionsMarshal.SetCount(vK, len);
        CollectionsMarshal.SetCount(tS, len);
        CollectionsMarshal.SetCount(vS, len);

        Batch(source.Values, CollectionsMarshal.AsSpan(vK), CollectionsMarshal.AsSpan(vS),
              r1, r2, r3, r4, s1, s2, s3, s4, sigPeriod);

        var tSpan = CollectionsMarshal.AsSpan(tK);
        source.Times.CopyTo(tSpan);
        tSpan.CopyTo(CollectionsMarshal.AsSpan(tS));

        return (new TSeries(tK, vK), new TSeries(tS, vS));
    }

    /// <summary>Creates a KST indicator and calculates results for the source series.</summary>
    public static ((TSeries Kst, TSeries Signal) Results, Kst Indicator) Calculate(
        TSeries source,
        int r1 = DefaultR1, int r2 = DefaultR2, int r3 = DefaultR3, int r4 = DefaultR4,
        int s1 = DefaultS1, int s2 = DefaultS2, int s3 = DefaultS3, int s4 = DefaultS4,
        int sigPeriod = DefaultSigPeriod)
    {
        var indicator = new Kst(r1, r2, r3, r4, s1, s2, s3, s4, sigPeriod);
        var results = indicator.Update(source);
        return (results, indicator);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// O(1) circular-buffer SMA with running sum.
    /// Returns the previous slot value (for bar-correction state capture) via <paramref name="prevSlot"/>.
    /// When isNew=false the head is not advanced (same slot overwritten for bar correction).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double StepSma(
        double[] buf, ref double sum, ref int head, ref int count,
        double value, int period, bool isNew, out double prevSlot)
    {
        int h = head;
        double oldest = buf[h];
        prevSlot = oldest; // capture the value being overwritten
        if (count < period)
        {
            sum += value - oldest;
            count++;
        }
        else
        {
            sum = sum - oldest + value;
        }
        buf[h] = value;
        if (isNew)
        {
            head = (h + 1) % period;
        }
        return sum / Math.Max(1, count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double BatchStepSma(
        double[] buf, int period,
        ref double sum, ref int head, ref int count,
        double value)
    {
        int h = head;
        double oldest = buf[h];
        if (count < period)
        {
            sum += value - oldest;
            count++;
        }
        else
        {
            sum = sum - oldest + value;
        }
        buf[h] = value;
        head = (h + 1) % period;
        return sum / Math.Max(1, count);
    }
}
