using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// SQUEEZE: Squeeze Momentum Oscillator
/// Detects low-volatility compressions (Bollinger Bands inside Keltner Channel)
/// and measures directional momentum via linear regression of detrended price.
/// Outputs: Momentum (histogram value) and SqueezeOn (true = BB inside KC).
/// Algorithm: BB(SMA+StdDev) vs KC(EMA+ATR/RMA), then LinReg of delta from Donchian midline.
/// </summary>
[SkipLocalsInit]
public sealed class Squeeze : ITValuePublisher
{
    private readonly int _period;
    private readonly double _bbMult;
    private readonly double _kcMult;

    // Circular buffers — managed separately for snapshot/rollback
    private readonly double[] _smaBuf;  // close values for SMA + variance
    private readonly double[] _hiBuf;   // high values for Donchian
    private readonly double[] _loBuf;   // low values for Donchian
    private readonly double[] _lrBuf;   // delta values for LinReg

    // Snapshots for bar-correction rollback (circular-buffer-snapshot-rollback pattern)
    private readonly double[] _smaBufSnap;
    private readonly double[] _hiBufSnap;
    private readonly double[] _loBufSnap;
    private readonly double[] _lrBufSnap;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        // SMA + variance (Bollinger Bands, §3 count-based warmup)
        double SmaSum, double SmaSumSq, int SmaHead, int SmaCount,
        // EMA for KC midline (§2 exponential warmup)
        double RawEma, double EEma,
        // ATR via Wilder RMA (§2 exponential warmup)
        double RawRma, double ERma, double PrevClose,
        // Donchian high/low buffers (O(period) scan for max/min)
        int DonHead, int DonCount,
        // LinReg incremental state (O(1))
        double SumY, double SumXY, int LrHead, int LrCount,
        // NaN substitution tracking
        double LastValidHigh, double LastValidLow, double LastValidClose);

    private State _s;
    private State _ps;

    private readonly TBarPublishedHandler _barHandler;

    public string Name { get; }
    public int WarmupPeriod { get; }
    public TValue Last { get; private set; }
    public double Momentum { get; private set; }
    public bool SqueezeOn { get; private set; }
    public bool IsHot => _s.LrCount >= _period;

    public event TValuePublishedHandler? Pub;

    public Squeeze(int period = 20, double bbMult = 2.0, double kcMult = 1.5)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }
        if (bbMult <= 0.0)
        {
            throw new ArgumentException("BB multiplier must be greater than 0", nameof(bbMult));
        }
        if (kcMult <= 0.0)
        {
            throw new ArgumentException("KC multiplier must be greater than 0", nameof(kcMult));
        }

        _period = period;
        _bbMult = bbMult;
        _kcMult = kcMult;

        _smaBuf = new double[period];
        _hiBuf = new double[period];
        _loBuf = new double[period];
        _lrBuf = new double[period];
        _smaBufSnap = new double[period];
        _hiBufSnap = new double[period];
        _loBufSnap = new double[period];
        _lrBufSnap = new double[period];

        // NaN sentinels — unfilled slots are distinguishable from real values
        Array.Fill(_smaBuf, double.NaN);
        Array.Fill(_hiBuf, double.NaN);
        Array.Fill(_loBuf, double.NaN);
        Array.Fill(_lrBuf, double.NaN);

        _s = MakeInitialState();
        _ps = _s;

        Name = $"Squeeze({period},{bbMult},{kcMult})";
        WarmupPeriod = period;
        _barHandler = HandleBar;
    }

    public Squeeze(TBarSeries source, int period = 20, double bbMult = 2.0, double kcMult = 1.5)
        : this(period, bbMult, kcMult)
    {
        Prime(source);
        source.Pub += _barHandler;
    }

    private static State MakeInitialState() =>
        new(SmaSum: 0.0, SmaSumSq: 0.0, SmaHead: 0, SmaCount: 0,
            RawEma: 0.0, EEma: 1.0,
            RawRma: 0.0, ERma: 1.0, PrevClose: double.NaN,
            DonHead: 0, DonCount: 0,
            SumY: 0.0, SumXY: 0.0, LrHead: 0, LrCount: 0,
            LastValidHigh: double.NaN, LastValidLow: double.NaN, LastValidClose: double.NaN);

    private void HandleBar(object? sender, in TBarEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PubEvent(TValue value, bool isNew = true) =>
        Pub?.Invoke(this, new TValueEventArgs { Value = value, IsNew = isNew });

    // Extracted from Update() — SMA circular buffer step (satisfies S1199)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateSmaBuf(ref State s, double close)
    {
        double oldVal = _smaBuf[s.SmaHead];
        if (double.IsNaN(oldVal))
        {
            s.SmaCount++;
        }
        else
        {
            s.SmaSum -= oldVal;
            s.SmaSumSq -= oldVal * oldVal;
        }
        s.SmaSum += close;
        s.SmaSumSq += close * close;
        _smaBuf[s.SmaHead] = close;
        s.SmaHead = (s.SmaHead + 1) % _period;
    }

    // Extracted from Update() — LinReg circular buffer step (satisfies S1199)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateLrBuf(ref State s, double delta)
    {
        double oldLr = _lrBuf[s.LrHead];
        if (!double.IsNaN(oldLr))
        {
            int oldIdx = s.LrCount - _period;
            s.SumY -= oldLr;
            s.SumXY -= (double)oldIdx * oldLr;
        }
        s.SumY += delta;
        s.SumXY += (double)s.LrCount * delta;
        _lrBuf[s.LrHead] = delta;
        s.LrHead = (s.LrHead + 1) % _period;
        s.LrCount++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        if (isNew)
        {
            // Snapshot state + circular buffers before advancing
            _ps = _s;
            Array.Copy(_smaBuf, _smaBufSnap, _period);
            Array.Copy(_hiBuf, _hiBufSnap, _period);
            Array.Copy(_loBuf, _loBufSnap, _period);
            Array.Copy(_lrBuf, _lrBufSnap, _period);
        }
        else
        {
            // Rollback to previous snapshot
            _s = _ps;
            Array.Copy(_smaBufSnap, _smaBuf, _period);
            Array.Copy(_hiBufSnap, _hiBuf, _period);
            Array.Copy(_loBufSnap, _loBuf, _period);
            Array.Copy(_lrBufSnap, _lrBuf, _period);
        }

        var s = _s;

        // === NaN/Infinity substitution (last-valid-value) ===
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
            Momentum = double.NaN;
            SqueezeOn = false;
            PubEvent(Last, isNew);
            return Last;
        }

        // ===== STAGE 1: SMA + Variance (Bollinger Bands, §3 count-based warmup) =====
        UpdateSmaBuf(ref s, close);

        int n = Math.Max(1, s.SmaCount);
        double smaVal = s.SmaSum / n;
        double variance = Math.Max(0.0, (s.SmaSumSq / n) - (smaVal * smaVal));
        double stddev = Math.Sqrt(variance);
        double bbUpper = Math.FusedMultiplyAdd(_bbMult, stddev, smaVal);
        double bbLower = Math.FusedMultiplyAdd(-_bbMult, stddev, smaVal);

        // ===== STAGE 2: EMA for KC midline + ATR via RMA (§2 exponential warmup) =====
        const double EPSILON = 1e-10;
        double emaAlpha = 2.0 / (_period + 1.0);
        double emaBeta = 1.0 - emaAlpha;
        double rmaAlpha = 1.0 / _period;
        double rmaBeta = 1.0 - rmaAlpha;

        s.RawEma = Math.FusedMultiplyAdd(s.RawEma, emaBeta, emaAlpha * close);
        s.EEma *= emaBeta;
        double cEma = s.EEma > EPSILON ? 1.0 / (1.0 - s.EEma) : 1.0;
        double emaVal = s.RawEma * cEma;

        // True Range
        double tr = high - low;
        if (double.IsFinite(s.PrevClose))
        {
            double hiPrev = Math.Abs(high - s.PrevClose);
            double loPrev = Math.Abs(low - s.PrevClose);
            if (hiPrev > tr) { tr = hiPrev; }
            if (loPrev > tr) { tr = loPrev; }
        }
        s.PrevClose = close;

        s.RawRma = Math.FusedMultiplyAdd(s.RawRma, rmaBeta, rmaAlpha * tr);
        s.ERma *= rmaBeta;
        double cRma = s.ERma > EPSILON ? 1.0 / (1.0 - s.ERma) : 1.0;
        double atr = s.RawRma * cRma;

        double kcUpper = Math.FusedMultiplyAdd(_kcMult, atr, emaVal);
        double kcLower = Math.FusedMultiplyAdd(-_kcMult, atr, emaVal);

        // ===== STAGE 3: Squeeze detection =====
        bool squeezeOn = bbUpper < kcUpper && bbLower > kcLower;

        // ===== STAGE 4: Donchian midline (O(period) scan) =====
        _hiBuf[s.DonHead] = high;
        _loBuf[s.DonHead] = low;
        if (s.DonCount < _period) { s.DonCount++; }
        s.DonHead = (s.DonHead + 1) % _period;

        double highest = high;
        double lowest = low;
        int donFilled = s.DonCount;
        for (int i = 0; i < donFilled; i++)
        {
            double dh = _hiBuf[i];
            double dl = _loBuf[i];
            if (!double.IsNaN(dh) && dh > highest) { highest = dh; }
            if (!double.IsNaN(dl) && dl < lowest) { lowest = dl; }
        }
        double donMid = (highest + lowest) * 0.5;
        double delta = close - ((donMid + smaVal) * 0.5);

        // ===== STAGE 5: Linear regression of delta over period (O(1) incremental) =====
        UpdateLrBuf(ref s, delta);

        int pn = Math.Min(s.LrCount, _period);
        int startIdx = s.LrCount - pn;
        // Closed-form sums: ΣX and ΣX²
        double sumX = (double)pn * ((2.0 * startIdx) + pn - 1) * 0.5;
        double sumX2 = Math.FusedMultiplyAdd(
            pn, (double)startIdx * startIdx,
            Math.FusedMultiplyAdd(
                (double)startIdx * (pn - 1), pn,
                (double)(pn - 1) * pn * ((2 * pn) - 1) / 6.0));
        double denomX = Math.FusedMultiplyAdd(pn, sumX2, -(sumX * sumX));
        double slope = denomX == 0.0 ? 0.0
            : Math.FusedMultiplyAdd(pn, s.SumXY, -(sumX * s.SumY)) / denomX;
        double intercept = (s.SumY - (slope * sumX)) / pn;
        double momentum = Math.FusedMultiplyAdd(slope, s.LrCount - 1, intercept);

        _s = s;

        Momentum = momentum;
        SqueezeOn = squeezeOn;
        Last = new TValue(input.Time, momentum);
        PubEvent(Last, isNew);
        return Last;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true) =>
        Update(new TBar(input.Time, input.Value, input.Value, input.Value, input.Value, 0), isNew);

    public (TSeries Momentum, TSeries SqueezeOn) Update(TBarSeries source)
    {
        if (source.Count == 0)
        {
            return (new TSeries([], []), new TSeries([], []));
        }

        int len = source.Count;
        var tMom = new List<long>(len);
        var vMom = new List<double>(len);
        var tSq = new List<long>(len);
        var vSq = new List<double>(len);

        CollectionsMarshal.SetCount(tMom, len);
        CollectionsMarshal.SetCount(vMom, len);
        CollectionsMarshal.SetCount(tSq, len);
        CollectionsMarshal.SetCount(vSq, len);

        var vMomSpan = CollectionsMarshal.AsSpan(vMom);
        var vSqSpan = CollectionsMarshal.AsSpan(vSq);

        Batch(source.HighValues, source.LowValues, source.CloseValues,
            vMomSpan, vSqSpan, _period, _bbMult, _kcMult);

        var tSpan = CollectionsMarshal.AsSpan(tMom);
        source.Times.CopyTo(tSpan);
        tSpan.CopyTo(CollectionsMarshal.AsSpan(tSq));

        Prime(source);  // restore streaming state to end of series

        if (len > 0)
        {
            var lastTime = new DateTime(source.Times[^1], DateTimeKind.Utc);
            Momentum = vMomSpan[^1];
            SqueezeOn = vSqSpan[^1] >= 0.5;
            Last = new TValue(lastTime, Momentum);
        }

        return (new TSeries(tMom, vMom), new TSeries(tSq, vSq));
    }

    public void Prime(TBarSeries source)
    {
        Reset();
        for (int i = 0; i < source.Count; i++)
        {
            Update(source[i], isNew: true);
        }
    }

    public void Reset()
    {
        Array.Fill(_smaBuf, double.NaN);
        Array.Fill(_hiBuf, double.NaN);
        Array.Fill(_loBuf, double.NaN);
        Array.Fill(_lrBuf, double.NaN);
        Array.Fill(_smaBufSnap, double.NaN);
        Array.Fill(_hiBufSnap, double.NaN);
        Array.Fill(_loBufSnap, double.NaN);
        Array.Fill(_lrBufSnap, double.NaN);
        _s = MakeInitialState();
        _ps = _s;
        Last = default;
        Momentum = 0.0;
        SqueezeOn = false;
    }

    /// <summary>
    /// Span-based batch Squeeze calculation. Populates both the momentum and squeeze-state output spans.
    /// </summary>
    /// <param name="momOut">Output span for Squeeze momentum values (linear regression of detrended price).</param>
    /// <param name="sqOut">Output span for squeeze-state values (positive when Bollinger Bands are inside Keltner Channel).</param>
    public static void Batch(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        Span<double> momOut,
        Span<double> sqOut,
        int period = 20,
        double bbMult = 2.0,
        double kcMult = 1.5)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }
        if (bbMult <= 0.0)
        {
            throw new ArgumentException("BB multiplier must be greater than 0", nameof(bbMult));
        }
        if (kcMult <= 0.0)
        {
            throw new ArgumentException("KC multiplier must be greater than 0", nameof(kcMult));
        }
        if (high.Length != low.Length || high.Length != close.Length)
        {
            throw new ArgumentException("Input spans must have the same length", nameof(high));
        }
        if (momOut.Length < high.Length)
        {
            throw new ArgumentException("Momentum output span must be at least as long as input", nameof(momOut));
        }
        if (sqOut.Length < high.Length)
        {
            throw new ArgumentException("SqueezeOn output span must be at least as long as input", nameof(sqOut));
        }

        int len = high.Length;
        if (len == 0) { return; }

        const int StackallocThreshold = 256;

        double[]? rentedSma = null;
        double[]? rentedHi = null;
        double[]? rentedLo = null;
        double[]? rentedLr = null;

        scoped Span<double> smaBuf;
        scoped Span<double> hiBuf;
        scoped Span<double> loBuf;
        scoped Span<double> lrBuf;

        if (period <= StackallocThreshold)
        {
            smaBuf = stackalloc double[period];
            hiBuf = stackalloc double[period];
            loBuf = stackalloc double[period];
            lrBuf = stackalloc double[period];
        }
        else
        {
            rentedSma = ArrayPool<double>.Shared.Rent(period);
            rentedHi = ArrayPool<double>.Shared.Rent(period);
            rentedLo = ArrayPool<double>.Shared.Rent(period);
            rentedLr = ArrayPool<double>.Shared.Rent(period);
            smaBuf = rentedSma.AsSpan(0, period);
            hiBuf = rentedHi.AsSpan(0, period);
            loBuf = rentedLo.AsSpan(0, period);
            lrBuf = rentedLr.AsSpan(0, period);
        }

        // NaN sentinels for unfilled slots
        smaBuf.Fill(double.NaN);
        hiBuf.Fill(double.NaN);
        loBuf.Fill(double.NaN);
        lrBuf.Fill(double.NaN);

        try
        {
            BatchCore(high, low, close, momOut, sqOut, period, bbMult, kcMult,
                smaBuf, hiBuf, loBuf, lrBuf);
        }
        finally
        {
            if (rentedSma != null) { ArrayPool<double>.Shared.Return(rentedSma); }
            if (rentedHi != null) { ArrayPool<double>.Shared.Return(rentedHi); }
            if (rentedLo != null) { ArrayPool<double>.Shared.Return(rentedLo); }
            if (rentedLr != null) { ArrayPool<double>.Shared.Return(rentedLr); }
        }
    }

    public static (TSeries Momentum, TSeries SqueezeOn) Batch(
        TBarSeries source, int period = 20, double bbMult = 2.0, double kcMult = 1.5)
    {
        if (source == null || source.Count == 0)
        {
            return (new TSeries([], []), new TSeries([], []));
        }

        int len = source.Count;
        var tMom = new List<long>(len);
        var vMom = new List<double>(len);
        var tSq = new List<long>(len);
        var vSq = new List<double>(len);

        CollectionsMarshal.SetCount(tMom, len);
        CollectionsMarshal.SetCount(vMom, len);
        CollectionsMarshal.SetCount(tSq, len);
        CollectionsMarshal.SetCount(vSq, len);

        Batch(source.HighValues, source.LowValues, source.CloseValues,
            CollectionsMarshal.AsSpan(vMom),
            CollectionsMarshal.AsSpan(vSq),
            period, bbMult, kcMult);

        var tSpan = CollectionsMarshal.AsSpan(tMom);
        source.Times.CopyTo(tSpan);
        tSpan.CopyTo(CollectionsMarshal.AsSpan(tSq));

        return (new TSeries(tMom, vMom), new TSeries(tSq, vSq));
    }

    public static ((TSeries Momentum, TSeries SqueezeOn) Results, Squeeze Indicator) Calculate(
        TBarSeries source, int period = 20, double bbMult = 2.0, double kcMult = 1.5)
    {
        var indicator = new Squeeze(period, bbMult, kcMult);
        var results = indicator.Update(source);
        return (results, indicator);
    }

    private static void BatchCore(
        ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> momOut, Span<double> sqOut,
        int period, double bbMult, double kcMult,
        Span<double> smaBuf, Span<double> hiBuf, Span<double> loBuf, Span<double> lrBuf)
    {
        int len = high.Length;
        int smaHead = 0, smaCount = 0;
        double smaSum = 0.0, smaSumSq = 0.0;

        double rawEma = 0.0, eEma = 1.0;
        double rawRma = 0.0, eRma = 1.0;
        double prevClose = double.NaN;

        int donHead = 0, donCount = 0;

        double sumY = 0.0, sumXY = 0.0;
        int lrHead = 0, lrCount = 0;

        double emaAlpha = 2.0 / (period + 1.0);
        double emaBeta = 1.0 - emaAlpha;
        double rmaAlpha = 1.0 / period;
        double rmaBeta = 1.0 - rmaAlpha;
        const double EPSILON = 1e-10;

        for (int i = 0; i < len; i++)
        {
            double h = high[i];
            double l = low[i];
            double c = close[i];
            if (!double.IsFinite(h)) { h = 0.0; }
            if (!double.IsFinite(l)) { l = 0.0; }
            if (!double.IsFinite(c)) { c = 0.0; }

            // Stage 1: SMA + StdDev for BB
            double oldSma = smaBuf[smaHead];
            if (double.IsNaN(oldSma))
            {
                smaCount++;
            }
            else
            {
                smaSum -= oldSma;
                smaSumSq -= oldSma * oldSma;
            }
            smaSum += c;
            smaSumSq += c * c;
            smaBuf[smaHead] = c;
            smaHead = (smaHead + 1) % period;

            int n = Math.Max(1, smaCount);
            double smaVal = smaSum / n;
            double vari = Math.Max(0.0, (smaSumSq / n) - (smaVal * smaVal));
            double sd = Math.Sqrt(vari);
            double bbUpper = Math.FusedMultiplyAdd(bbMult, sd, smaVal);
            double bbLower = Math.FusedMultiplyAdd(-bbMult, sd, smaVal);

            // Stage 2: EMA + ATR for KC
            rawEma = Math.FusedMultiplyAdd(rawEma, emaBeta, emaAlpha * c);
            eEma *= emaBeta;
            double cEma = eEma > EPSILON ? 1.0 / (1.0 - eEma) : 1.0;
            double emaVal = rawEma * cEma;

            double tr = h - l;
            if (double.IsFinite(prevClose))
            {
                double hp = Math.Abs(h - prevClose);
                double lp = Math.Abs(l - prevClose);
                if (hp > tr) { tr = hp; }
                if (lp > tr) { tr = lp; }
            }
            prevClose = c;

            rawRma = Math.FusedMultiplyAdd(rawRma, rmaBeta, rmaAlpha * tr);
            eRma *= rmaBeta;
            double cRma = eRma > EPSILON ? 1.0 / (1.0 - eRma) : 1.0;
            double atr = rawRma * cRma;

            double kcUpper = Math.FusedMultiplyAdd(kcMult, atr, emaVal);
            double kcLower = Math.FusedMultiplyAdd(-kcMult, atr, emaVal);

            // Stage 3: Squeeze detection
            double sqVal = bbUpper < kcUpper && bbLower > kcLower ? 1.0 : 0.0;

            // Stage 4: Donchian midline
            hiBuf[donHead] = h;
            loBuf[donHead] = l;
            if (donCount < period) { donCount++; }
            donHead = (donHead + 1) % period;

            double highest = h;
            double lowest = l;
            for (int j = 0; j < donCount; j++)
            {
                double dh = hiBuf[j];
                double dl = loBuf[j];
                if (!double.IsNaN(dh) && dh > highest) { highest = dh; }
                if (!double.IsNaN(dl) && dl < lowest) { lowest = dl; }
            }
            double donMid = (highest + lowest) * 0.5;
            double delta = c - ((donMid + smaVal) * 0.5);

            // Stage 5: LinReg incremental
            double oldLr = lrBuf[lrHead];
            if (!double.IsNaN(oldLr))
            {
                int oldIdx = lrCount - period;
                sumY -= oldLr;
                sumXY -= (double)oldIdx * oldLr;
            }
            sumY += delta;
            sumXY += (double)lrCount * delta;
            lrBuf[lrHead] = delta;
            lrHead = (lrHead + 1) % period;
            lrCount++;

            int pn = Math.Min(lrCount, period);
            int startI = lrCount - pn;
            double sx = (double)pn * ((2.0 * startI) + pn - 1) * 0.5;
            double sx2 = Math.FusedMultiplyAdd(
                pn, (double)startI * startI,
                Math.FusedMultiplyAdd(
                    (double)startI * (pn - 1), pn,
                    (double)(pn - 1) * pn * ((2 * pn) - 1) / 6.0));
            double denomX = Math.FusedMultiplyAdd(pn, sx2, -(sx * sx));
            double slope = denomX == 0.0 ? 0.0
                : Math.FusedMultiplyAdd(pn, sumXY, -(sx * sumY)) / denomX;
            double intc = (sumY - (slope * sx)) / pn;
            double momentum = Math.FusedMultiplyAdd(slope, lrCount - 1, intc);

            momOut[i] = momentum;
            sqOut[i] = sqVal;
        }
    }
}
