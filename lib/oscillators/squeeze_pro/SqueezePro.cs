using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// SQUEEZE_PRO: LazyBear's Squeeze Pro (enhanced TTM Squeeze)
/// Detects multi-level volatility compressions using three Keltner Channel widths
/// (wide, normal, narrow) against Bollinger Bands. Momentum is computed as
/// MOM(close, momLength) smoothed by SMA or EMA.
/// Outputs: Momentum (smoothed histogram) and SqueezeLevel (0=off, 1=wide, 2=normal, 3=narrow).
/// </summary>
[SkipLocalsInit]
public sealed class SqueezePro : ITValuePublisher
{
    private readonly int _period;
    private readonly double _bbMult;
    private readonly double _kcMultWide;
    private readonly double _kcMultNormal;
    private readonly double _kcMultNarrow;
    private readonly int _momLength;
    private readonly int _momSmooth;
    private readonly bool _useSma;

    // Circular buffers
    private readonly double[] _smaBuf;      // close values for SMA + variance (period)
    private readonly double[] _closeBuf;    // close values for MOM (momLength)
    private readonly double[] _smoothBuf;   // MOM values for SMA smoothing (momSmooth)

    // Snapshots for bar-correction rollback
    private readonly double[] _smaBufSnap;
    private readonly double[] _closeBufSnap;
    private readonly double[] _smoothBufSnap;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        // SMA + variance for Bollinger Bands
        double SmaSum, double SmaSumSq, int SmaHead, int SmaCount,
        // EMA for KC midline (bias-corrected)
        double RawEma, double EEma,
        // ATR via Wilder RMA (bias-corrected)
        double RawRma, double ERma, double PrevClose,
        // MOM close buffer tracking
        int MomHead, int MomCount,
        // SMA smoothing of MOM
        double SmoothSum, int SmoothHead, int SmoothCount,
        // EMA smoothing of MOM (for useSma=false mode)
        double RawSmoothEma, double ESmoothEma,
        // NaN substitution tracking
        double LastValidHigh, double LastValidLow, double LastValidClose);

    private State _s;
    private State _ps;

    private readonly TBarPublishedHandler _barHandler;

    public string Name { get; }
    public int WarmupPeriod { get; }
    public TValue Last { get; private set; }

    /// <summary>Smoothed momentum value (MOM smoothed by SMA or EMA).</summary>
    public double Momentum { get; private set; }

    /// <summary>
    /// Squeeze level: 0=off/no squeeze, 1=wide squeeze, 2=normal squeeze, 3=narrow squeeze.
    /// Higher values indicate tighter compression.
    /// </summary>
    public int SqueezeLevel { get; private set; }

    public bool IsHot => _s.SmoothCount >= _momSmooth && _s.MomCount >= _momLength;

    public event TValuePublishedHandler? Pub;

    public SqueezePro(int period = 20, double bbMult = 2.0,
        double kcMultWide = 2.0, double kcMultNormal = 1.5, double kcMultNarrow = 1.0,
        int momLength = 12, int momSmooth = 6, bool useSma = true)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }
        if (bbMult <= 0.0)
        {
            throw new ArgumentException("BB multiplier must be greater than 0", nameof(bbMult));
        }
        if (kcMultWide <= 0.0)
        {
            throw new ArgumentException("KC wide multiplier must be greater than 0", nameof(kcMultWide));
        }
        if (kcMultNormal <= 0.0)
        {
            throw new ArgumentException("KC normal multiplier must be greater than 0", nameof(kcMultNormal));
        }
        if (kcMultNarrow <= 0.0)
        {
            throw new ArgumentException("KC narrow multiplier must be greater than 0", nameof(kcMultNarrow));
        }
        if (momLength <= 0)
        {
            throw new ArgumentException("Momentum length must be greater than 0", nameof(momLength));
        }
        if (momSmooth <= 0)
        {
            throw new ArgumentException("Momentum smooth must be greater than 0", nameof(momSmooth));
        }

        _period = period;
        _bbMult = bbMult;
        _kcMultWide = kcMultWide;
        _kcMultNormal = kcMultNormal;
        _kcMultNarrow = kcMultNarrow;
        _momLength = momLength;
        _momSmooth = momSmooth;
        _useSma = useSma;

        _smaBuf = new double[period];
        _closeBuf = new double[momLength];
        _smoothBuf = new double[momSmooth];
        _smaBufSnap = new double[period];
        _closeBufSnap = new double[momLength];
        _smoothBufSnap = new double[momSmooth];

        Array.Fill(_smaBuf, double.NaN);
        Array.Fill(_closeBuf, double.NaN);
        Array.Fill(_smoothBuf, double.NaN);

        _s = MakeInitialState();
        _ps = _s;

        Name = $"SqueezePro({period},{bbMult},{kcMultWide},{kcMultNormal},{kcMultNarrow})";
        WarmupPeriod = Math.Max(period, momLength + momSmooth);
        _barHandler = HandleBar;
    }

    public SqueezePro(TBarSeries source, int period = 20, double bbMult = 2.0,
        double kcMultWide = 2.0, double kcMultNormal = 1.5, double kcMultNarrow = 1.0,
        int momLength = 12, int momSmooth = 6, bool useSma = true)
        : this(period, bbMult, kcMultWide, kcMultNormal, kcMultNarrow, momLength, momSmooth, useSma)
    {
        Prime(source);
        source.Pub += _barHandler;
    }

    private static State MakeInitialState() =>
        new(SmaSum: 0.0, SmaSumSq: 0.0, SmaHead: 0, SmaCount: 0,
            RawEma: 0.0, EEma: 1.0,
            RawRma: 0.0, ERma: 1.0, PrevClose: double.NaN,
            MomHead: 0, MomCount: 0,
            SmoothSum: 0.0, SmoothHead: 0, SmoothCount: 0,
            RawSmoothEma: 0.0, ESmoothEma: 1.0,
            LastValidHigh: double.NaN, LastValidLow: double.NaN, LastValidClose: double.NaN);

    private void HandleBar(object? sender, in TBarEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PubEvent(TValue value, bool isNew = true) =>
        Pub?.Invoke(this, new TValueEventArgs { Value = value, IsNew = isNew });

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double UpdateMomBuf(ref State s, double close)
    {
        double laggedClose = _closeBuf[s.MomHead];
        _closeBuf[s.MomHead] = close;
        s.MomHead = (s.MomHead + 1) % _momLength;

        if (s.MomCount < _momLength)
        {
            s.MomCount++;
            return double.NaN; // not enough data for MOM yet
        }

        // MOM = close - close[momLength bars ago]
        return close - laggedClose;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double UpdateSmoothBuf(ref State s, double mom)
    {
        if (_useSma)
        {
            // SMA smoothing
            double oldVal = _smoothBuf[s.SmoothHead];
            if (double.IsNaN(oldVal))
            {
                s.SmoothCount++;
            }
            else
            {
                s.SmoothSum -= oldVal;
            }
            s.SmoothSum += mom;
            _smoothBuf[s.SmoothHead] = mom;
            s.SmoothHead = (s.SmoothHead + 1) % _momSmooth;

            return s.SmoothSum / Math.Max(1, s.SmoothCount);
        }
        else
        {
            // EMA smoothing (bias-corrected)
            const double EPSILON = 1e-10;
            double alpha = 2.0 / (_momSmooth + 1.0);
            double beta = 1.0 - alpha;

            s.RawSmoothEma = Math.FusedMultiplyAdd(s.RawSmoothEma, beta, alpha * mom);
            s.ESmoothEma *= beta;
            double c = s.ESmoothEma > EPSILON ? 1.0 / (1.0 - s.ESmoothEma) : 1.0;
            s.SmoothCount = Math.Min(s.SmoothCount + 1, _momSmooth);
            return s.RawSmoothEma * c;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        if (isNew)
        {
            _ps = _s;
            Array.Copy(_smaBuf, _smaBufSnap, _period);
            Array.Copy(_closeBuf, _closeBufSnap, _momLength);
            Array.Copy(_smoothBuf, _smoothBufSnap, _momSmooth);
        }
        else
        {
            _s = _ps;
            Array.Copy(_smaBufSnap, _smaBuf, _period);
            Array.Copy(_closeBufSnap, _closeBuf, _momLength);
            Array.Copy(_smoothBufSnap, _smoothBuf, _momSmooth);
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
            SqueezeLevel = 0;
            PubEvent(Last, isNew);
            return Last;
        }

        // ===== STAGE 1: SMA + Variance → Bollinger Bands =====
        UpdateSmaBuf(ref s, close);

        int n = Math.Max(1, s.SmaCount);
        double smaVal = s.SmaSum / n;
        double variance = Math.Max(0.0, (s.SmaSumSq / n) - (smaVal * smaVal));
        double stddev = Math.Sqrt(variance);
        double bbUpper = Math.FusedMultiplyAdd(_bbMult, stddev, smaVal);
        double bbLower = Math.FusedMultiplyAdd(-_bbMult, stddev, smaVal);

        // ===== STAGE 2: EMA + ATR via RMA → Keltner Channels =====
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

        // Three KC widths
        double kcWideUpper = Math.FusedMultiplyAdd(_kcMultWide, atr, emaVal);
        double kcWideLower = Math.FusedMultiplyAdd(-_kcMultWide, atr, emaVal);
        double kcNormalUpper = Math.FusedMultiplyAdd(_kcMultNormal, atr, emaVal);
        double kcNormalLower = Math.FusedMultiplyAdd(-_kcMultNormal, atr, emaVal);
        double kcNarrowUpper = Math.FusedMultiplyAdd(_kcMultNarrow, atr, emaVal);
        double kcNarrowLower = Math.FusedMultiplyAdd(-_kcMultNarrow, atr, emaVal);

        // ===== STAGE 3: Squeeze level classification =====
        // 3 = narrow (tightest): BB inside KC_narrow
        // 2 = normal: BB inside KC_normal but not KC_narrow
        // 1 = wide: BB inside KC_wide but not KC_normal
        // 0 = off: BB outside KC_wide (expansion)
        int sqLevel;
        bool insideNarrow = bbUpper < kcNarrowUpper && bbLower > kcNarrowLower;
        bool insideNormal = bbUpper < kcNormalUpper && bbLower > kcNormalLower;
        bool insideWide = bbUpper < kcWideUpper && bbLower > kcWideLower;

        if (insideNarrow) { sqLevel = 3; }
        else if (insideNormal) { sqLevel = 2; }
        else if (insideWide) { sqLevel = 1; }
        else { sqLevel = 0; }

        // ===== STAGE 4: MOM = close - close[momLength ago] =====
        double rawMom = UpdateMomBuf(ref s, close);

        // ===== STAGE 5: Smooth MOM via SMA or EMA =====
        // Use 0.0 for insufficient MOM data (matches batch path)
        double momVal = double.IsNaN(rawMom) ? 0.0 : rawMom;
        double momentum = UpdateSmoothBuf(ref s, momVal);

        _s = s;

        Momentum = momentum;
        SqueezeLevel = sqLevel;
        Last = new TValue(input.Time, momentum);
        PubEvent(Last, isNew);
        return Last;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true) =>
        Update(new TBar(input.Time, input.Value, input.Value, input.Value, input.Value, 0), isNew);

    public (TSeries Momentum, TSeries SqueezeLevel) Update(TBarSeries source)
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
            vMomSpan, vSqSpan, _period, _bbMult, _kcMultWide, _kcMultNormal, _kcMultNarrow,
            _momLength, _momSmooth, _useSma);

        var tSpan = CollectionsMarshal.AsSpan(tMom);
        source.Times.CopyTo(tSpan);
        tSpan.CopyTo(CollectionsMarshal.AsSpan(tSq));

        Prime(source);

        if (len > 0)
        {
            Momentum = vMomSpan[^1];
            SqueezeLevel = (int)vSqSpan[^1];
            Last = new TValue(new DateTime(source.Times[^1], DateTimeKind.Utc), Momentum);
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
        Array.Fill(_closeBuf, double.NaN);
        Array.Fill(_smoothBuf, double.NaN);
        Array.Fill(_smaBufSnap, double.NaN);
        Array.Fill(_closeBufSnap, double.NaN);
        Array.Fill(_smoothBufSnap, double.NaN);
        _s = MakeInitialState();
        _ps = _s;
        Last = default;
        Momentum = 0.0;
        SqueezeLevel = 0;
    }

    /// <summary>
    /// Span-based batch Squeeze Pro calculation.
    /// </summary>
    public static void Batch(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        Span<double> momOut,
        Span<double> sqOut,
        int period = 20,
        double bbMult = 2.0,
        double kcMultWide = 2.0,
        double kcMultNormal = 1.5,
        double kcMultNarrow = 1.0,
        int momLength = 12,
        int momSmooth = 6,
        bool useSma = true)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }
        if (bbMult <= 0.0)
        {
            throw new ArgumentException("BB multiplier must be greater than 0", nameof(bbMult));
        }
        if (kcMultWide <= 0.0)
        {
            throw new ArgumentException("KC wide multiplier must be greater than 0", nameof(kcMultWide));
        }
        if (kcMultNormal <= 0.0)
        {
            throw new ArgumentException("KC normal multiplier must be greater than 0", nameof(kcMultNormal));
        }
        if (kcMultNarrow <= 0.0)
        {
            throw new ArgumentException("KC narrow multiplier must be greater than 0", nameof(kcMultNarrow));
        }
        if (momLength <= 0)
        {
            throw new ArgumentException("Momentum length must be greater than 0", nameof(momLength));
        }
        if (momSmooth <= 0)
        {
            throw new ArgumentException("Momentum smooth must be greater than 0", nameof(momSmooth));
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
            throw new ArgumentException("SqueezeLevel output span must be at least as long as input", nameof(sqOut));
        }

        int len = high.Length;
        if (len == 0) { return; }

        const int StackallocThreshold = 256;
        int totalBuf = period + momLength + momSmooth;

        double[]? rented = null;
        scoped Span<double> smaBuf;
        scoped Span<double> closeBuf;
        scoped Span<double> smoothBuf;

        if (totalBuf <= StackallocThreshold)
        {
            Span<double> allBuf = stackalloc double[totalBuf];
            smaBuf = allBuf.Slice(0, period);
            closeBuf = allBuf.Slice(period, momLength);
            smoothBuf = allBuf.Slice(period + momLength, momSmooth);
        }
        else
        {
            rented = ArrayPool<double>.Shared.Rent(totalBuf);
            smaBuf = rented.AsSpan(0, period);
            closeBuf = rented.AsSpan(period, momLength);
            smoothBuf = rented.AsSpan(period + momLength, momSmooth);
        }

        smaBuf.Fill(double.NaN);
        closeBuf.Fill(double.NaN);
        smoothBuf.Fill(double.NaN);

        try
        {
            BatchCore(high, low, close, momOut, sqOut, period, bbMult,
                kcMultWide, kcMultNormal, kcMultNarrow, momLength, momSmooth, useSma,
                smaBuf, closeBuf, smoothBuf);
        }
        finally
        {
            if (rented != null) { ArrayPool<double>.Shared.Return(rented); }
        }
    }

    public static (TSeries Momentum, TSeries SqueezeLevel) Batch(
        TBarSeries source, int period = 20, double bbMult = 2.0,
        double kcMultWide = 2.0, double kcMultNormal = 1.5, double kcMultNarrow = 1.0,
        int momLength = 12, int momSmooth = 6, bool useSma = true)
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
            period, bbMult, kcMultWide, kcMultNormal, kcMultNarrow, momLength, momSmooth, useSma);

        var tSpan = CollectionsMarshal.AsSpan(tMom);
        source.Times.CopyTo(tSpan);
        tSpan.CopyTo(CollectionsMarshal.AsSpan(tSq));

        return (new TSeries(tMom, vMom), new TSeries(tSq, vSq));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ((TSeries Momentum, TSeries SqueezeLevel) Results, SqueezePro Indicator) Calculate(
        TBarSeries source, int period = 20, double bbMult = 2.0,
        double kcMultWide = 2.0, double kcMultNormal = 1.5, double kcMultNarrow = 1.0,
        int momLength = 12, int momSmooth = 6, bool useSma = true)
    {
        var indicator = new SqueezePro(period, bbMult, kcMultWide, kcMultNormal, kcMultNarrow, momLength, momSmooth, useSma);
        var results = indicator.Update(source);
        return (results, indicator);
    }

    private static void BatchCore(
        ReadOnlySpan<double> high, ReadOnlySpan<double> low, ReadOnlySpan<double> close,
        Span<double> momOut, Span<double> sqOut,
        int period, double bbMult,
        double kcMultWide, double kcMultNormal, double kcMultNarrow,
        int momLength, int momSmooth, bool useSma,
        Span<double> smaBuf, Span<double> closeBuf, Span<double> smoothBuf)
    {
        int len = high.Length;
        int smaHead = 0, smaCount = 0;
        double smaSum = 0.0, smaSumSq = 0.0;

        double rawEma = 0.0, eEma = 1.0;
        double rawRma = 0.0, eRma = 1.0;
        double prevClose = double.NaN;

        int momHead = 0, momCount = 0;
        double smoothSum = 0.0;
        int smoothHead = 0, smoothCount = 0;
        double rawSmoothEma = 0.0, eSmoothEma = 1.0;

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

            // Three KC widths
            double kcWU = Math.FusedMultiplyAdd(kcMultWide, atr, emaVal);
            double kcWL = Math.FusedMultiplyAdd(-kcMultWide, atr, emaVal);
            double kcNU = Math.FusedMultiplyAdd(kcMultNormal, atr, emaVal);
            double kcNL = Math.FusedMultiplyAdd(-kcMultNormal, atr, emaVal);
            double kcRU = Math.FusedMultiplyAdd(kcMultNarrow, atr, emaVal);
            double kcRL = Math.FusedMultiplyAdd(-kcMultNarrow, atr, emaVal);

            // Stage 3: Squeeze classification
            bool insideNarrow = bbUpper < kcRU && bbLower > kcRL;
            bool insideNormal = bbUpper < kcNU && bbLower > kcNL;
            bool insideWide = bbUpper < kcWU && bbLower > kcWL;

            double sqVal;
            if (insideNarrow) { sqVal = 3.0; }
            else if (insideNormal) { sqVal = 2.0; }
            else if (insideWide) { sqVal = 1.0; }
            else { sqVal = 0.0; }

            // Stage 4: MOM = close - close[momLength ago]
            double laggedClose = closeBuf[momHead];
            closeBuf[momHead] = c;
            momHead = (momHead + 1) % momLength;
            double rawMom;
            if (momCount < momLength)
            {
                momCount++;
                rawMom = 0.0; // not enough data yet
            }
            else
            {
                rawMom = c - laggedClose;
            }

            // Stage 5: Smooth MOM
            double momentum;
            if (useSma)
            {
                double oldSmooth = smoothBuf[smoothHead];
                if (double.IsNaN(oldSmooth))
                {
                    smoothCount++;
                }
                else
                {
                    smoothSum -= oldSmooth;
                }
                smoothSum += rawMom;
                smoothBuf[smoothHead] = rawMom;
                smoothHead = (smoothHead + 1) % momSmooth;
                momentum = smoothSum / Math.Max(1, smoothCount);
            }
            else
            {
                double smAlpha = 2.0 / (momSmooth + 1.0);
                double smBeta = 1.0 - smAlpha;
                rawSmoothEma = Math.FusedMultiplyAdd(rawSmoothEma, smBeta, smAlpha * rawMom);
                eSmoothEma *= smBeta;
                double smC = eSmoothEma > EPSILON ? 1.0 / (1.0 - eSmoothEma) : 1.0;
                momentum = rawSmoothEma * smC;
            }

            momOut[i] = momentum;
            sqOut[i] = sqVal;
        }
    }
}
