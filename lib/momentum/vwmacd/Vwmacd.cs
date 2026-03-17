using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// VWMACD: Volume-Weighted Moving Average Convergence Divergence
/// </summary>
/// <remarks>
/// Replaces EMA with VWMA in the standard MACD formula, weighting price
/// changes by their associated volume. Higher-volume bars contribute more
/// to the moving averages, producing a momentum oscillator that naturally
/// prioritises institutional-grade price movements.
///
/// Calculation: <c>VWMACD = VWMA(close,vol,fast) - VWMA(close,vol,slow)</c>,
/// <c>Signal = EMA(VWMACD, signal)</c>, <c>Histogram = VWMACD - Signal</c>.
/// </remarks>
[SkipLocalsInit]
public sealed class Vwmacd : ITValuePublisher, IDisposable
{
    // ── fast VWMA circular buffers ──
    private readonly double[] _pvFast;
    private readonly double[] _volFast;
    private double _pvSumFast, _volSumFast;
    private int _headFast, _countFast;

    // ── slow VWMA circular buffers ──
    private readonly double[] _pvSlow;
    private readonly double[] _volSlow;
    private double _pvSumSlow, _volSumSlow;
    private int _headSlow, _countSlow;

    // ── signal EMA ──
    private readonly double _signalAlpha;
    private double _signalEma;
    private bool _signalInitialised;

    // ── snapshot for bar correction ──
    private double _p_pvSumFast, _p_volSumFast;
    private int _p_headFast, _p_countFast;
    private double _p_pvSumSlow, _p_volSumSlow;
    private int _p_headSlow, _p_countSlow;
    private double _p_signalEma;
    private bool _p_signalInitialised;
    private double[]? _p_pvFastSnap, _p_volFastSnap, _p_pvSlowSnap, _p_volSlowSnap;

    // ── parameters ──
    private readonly int _fastPeriod;
    private readonly int _slowPeriod;

    // ── publisher ──
    private bool _disposed;

    public string Name { get; }
    public int WarmupPeriod { get; }
    public bool IsHot => _countFast >= _fastPeriod && _countSlow >= _slowPeriod && _signalInitialised;

    public TValue Last { get; private set; }
    public TValue Signal { get; private set; }
    public TValue Histogram { get; private set; }

    public event TValuePublishedHandler? Pub;

    // ────────────────────────── constructors ──────────────────────────

    public Vwmacd(int fastPeriod = 12, int slowPeriod = 26, int signalPeriod = 9)
    {
        if (fastPeriod <= 0)
        {
            throw new ArgumentException("Fast period must be > 0", nameof(fastPeriod));
        }
        if (slowPeriod <= 0)
        {
            throw new ArgumentException("Slow period must be > 0", nameof(slowPeriod));
        }
        if (signalPeriod <= 0)
        {
            throw new ArgumentException("Signal period must be > 0", nameof(signalPeriod));
        }

        _fastPeriod = fastPeriod;
        _slowPeriod = slowPeriod;

        _pvFast = new double[fastPeriod];
        _volFast = new double[fastPeriod];
        _pvSlow = new double[slowPeriod];
        _volSlow = new double[slowPeriod];

        _signalAlpha = 2.0 / (signalPeriod + 1.0);

        Name = $"Vwmacd({fastPeriod},{slowPeriod},{signalPeriod})";
        WarmupPeriod = Math.Max(fastPeriod, slowPeriod) + signalPeriod - 2;
    }

    // ────────────────────────── core Update ──────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar bar, bool isNew = true)
    {
        double close = bar.Close;
        double volume = Math.Max(bar.Volume, 0);

        if (isNew)
        {
            SaveState();
        }
        else
        {
            RestoreState();
        }

        double pv = close * volume;

        // ── fast VWMA ──
        UpdateVwma(
            _pvFast, _volFast, ref _pvSumFast, ref _volSumFast,
            ref _headFast, ref _countFast, _fastPeriod, pv, volume);

        double vwmaFast = _volSumFast > 0 ? _pvSumFast / _volSumFast : close;

        // ── slow VWMA ──
        UpdateVwma(
            _pvSlow, _volSlow, ref _pvSumSlow, ref _volSumSlow,
            ref _headSlow, ref _countSlow, _slowPeriod, pv, volume);

        double vwmaSlow = _volSumSlow > 0 ? _pvSumSlow / _volSumSlow : close;

        // ── VWMACD line ──
        double vwmacdValue = vwmaFast - vwmaSlow;

        // ── Signal EMA ──
        if (!_signalInitialised)
        {
            _signalEma = vwmacdValue;
            _signalInitialised = true;
        }
        else
        {
            _signalEma = Math.FusedMultiplyAdd(_signalAlpha, vwmacdValue - _signalEma, _signalEma);
        }

        double histValue = vwmacdValue - _signalEma;

        Last = new TValue(bar.Time, vwmacdValue);
        Signal = new TValue(bar.Time, _signalEma);
        Histogram = new TValue(bar.Time, histValue);

        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    // ────────────────────────── series APIs ──────────────────────────

    public (TSeries Vwmacd, TSeries Signal, TSeries Histogram) Update(TBarSeries source)
    {
        int len = source.Count;
        if (len == 0)
        {
            return ([], [], []);
        }

        var tV = new List<long>(len); var vV = new List<double>(len);
        var tS = new List<long>(len); var vS = new List<double>(len);
        var tH = new List<long>(len); var vH = new List<double>(len);
        CollectionsMarshal.SetCount(tV, len); CollectionsMarshal.SetCount(vV, len);
        CollectionsMarshal.SetCount(tS, len); CollectionsMarshal.SetCount(vS, len);
        CollectionsMarshal.SetCount(tH, len); CollectionsMarshal.SetCount(vH, len);

        var tvSpan = CollectionsMarshal.AsSpan(tV); var vvSpan = CollectionsMarshal.AsSpan(vV);
        var tsSpan = CollectionsMarshal.AsSpan(tS); var vsSpan = CollectionsMarshal.AsSpan(vS);
        var thSpan = CollectionsMarshal.AsSpan(tH); var vhSpan = CollectionsMarshal.AsSpan(vH);

        Reset();
        for (int i = 0; i < len; i++)
        {
            Update(source[i], isNew: true);
            long time = source[i].Time;
            tvSpan[i] = time; vvSpan[i] = Last.Value;
            tsSpan[i] = time; vsSpan[i] = Signal.Value;
            thSpan[i] = time; vhSpan[i] = Histogram.Value;
        }

        return (new TSeries(tV, vV), new TSeries(tS, vS), new TSeries(tH, vH));
    }

    public void Prime(TBarSeries source)
    {
        Reset();
        for (int i = 0; i < source.Count; i++)
        {
            Update(source[i], isNew: true);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        Array.Clear(_pvFast); Array.Clear(_volFast);
        Array.Clear(_pvSlow); Array.Clear(_volSlow);
        _pvSumFast = _volSumFast = 0; _headFast = _countFast = 0;
        _pvSumSlow = _volSumSlow = 0; _headSlow = _countSlow = 0;
        _signalEma = 0; _signalInitialised = false;
        Last = Signal = Histogram = default;
    }

    // ────────────────────────── static Batch ──────────────────────────

    public static void Batch(
        ReadOnlySpan<double> close, ReadOnlySpan<double> volume,
        Span<double> vwmacdOut, Span<double> signalOut, Span<double> histOut,
        int fastPeriod = 12, int slowPeriod = 26, int signalPeriod = 9)
    {
        int n = close.Length;
        if (n != volume.Length || n != vwmacdOut.Length || n != signalOut.Length || n != histOut.Length)
        {
            throw new ArgumentException("All spans must have the same length", nameof(close));
        }

        // ── compute VWMA fast & slow via rolling sums ──
        double pvSumF = 0, volSumF = 0;
        double pvSumS = 0, volSumS = 0;
        double sigEma = 0;
        bool sigInit = false;

        double[] pvBufF = ArrayPool<double>.Shared.Rent(fastPeriod);
        double[] volBufF = ArrayPool<double>.Shared.Rent(fastPeriod);
        double[] pvBufS = ArrayPool<double>.Shared.Rent(slowPeriod);
        double[] volBufS = ArrayPool<double>.Shared.Rent(slowPeriod);
        double sigAlpha = 2.0 / (signalPeriod + 1.0);

        try
        {
            Array.Clear(pvBufF, 0, fastPeriod);
            Array.Clear(volBufF, 0, fastPeriod);
            Array.Clear(pvBufS, 0, slowPeriod);
            Array.Clear(volBufS, 0, slowPeriod);
            int hF = 0, hS = 0, cF = 0, cS = 0;

            for (int i = 0; i < n; i++)
            {
                double c = close[i];
                double v = Math.Max(volume[i], 0);
                double pv = c * v;

                // fast VWMA
                if (cF >= fastPeriod)
                {
                    pvSumF -= pvBufF[hF]; volSumF -= volBufF[hF];
                }
                pvBufF[hF] = pv; volBufF[hF] = v;
                pvSumF += pv; volSumF += v;
                hF = (hF + 1) % fastPeriod;
                if (cF < fastPeriod)
                {
                    cF++;
                }
                double vwmaF = volSumF > 0 ? pvSumF / volSumF : c;

                // slow VWMA
                if (cS >= slowPeriod)
                {
                    pvSumS -= pvBufS[hS]; volSumS -= volBufS[hS];
                }
                pvBufS[hS] = pv; volBufS[hS] = v;
                pvSumS += pv; volSumS += v;
                hS = (hS + 1) % slowPeriod;
                if (cS < slowPeriod)
                {
                    cS++;
                }
                double vwmaS = volSumS > 0 ? pvSumS / volSumS : c;

                double vwmacd = vwmaF - vwmaS;
                vwmacdOut[i] = vwmacd;

                // signal EMA
                if (!sigInit)
                {
                    sigEma = vwmacd; sigInit = true;
                }
                else
                {
                    sigEma = Math.FusedMultiplyAdd(sigAlpha, vwmacd - sigEma, sigEma);
                }

                signalOut[i] = sigEma;
                histOut[i] = vwmacd - sigEma;
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(pvBufF);
            ArrayPool<double>.Shared.Return(volBufF);
            ArrayPool<double>.Shared.Return(pvBufS);
            ArrayPool<double>.Shared.Return(volBufS);
        }
    }

    public static (TSeries Vwmacd, TSeries Signal, TSeries Histogram) Batch(
        TBarSeries source, int fastPeriod = 12, int slowPeriod = 26, int signalPeriod = 9)
    {
        var ind = new Vwmacd(fastPeriod, slowPeriod, signalPeriod);
        return ind.Update(source);
    }

    // ────────────────────────── private helpers ──────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void UpdateVwma(
        double[] pvBuf, double[] volBuf,
        ref double pvSum, ref double volSum,
        ref int head, ref int count, int period,
        double pv, double vol)
    {
        if (count >= period)
        {
            pvSum -= pvBuf[head];
            volSum -= volBuf[head];
        }

        pvBuf[head] = pv;
        volBuf[head] = vol;
        pvSum += pv;
        volSum += vol;

        head = (head + 1) % period;
        if (count < period)
        {
            count++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SaveState()
    {
        _p_pvSumFast = _pvSumFast; _p_volSumFast = _volSumFast;
        _p_headFast = _headFast; _p_countFast = _countFast;
        _p_pvSumSlow = _pvSumSlow; _p_volSumSlow = _volSumSlow;
        _p_headSlow = _headSlow; _p_countSlow = _countSlow;
        _p_signalEma = _signalEma; _p_signalInitialised = _signalInitialised;

        _p_pvFastSnap ??= new double[_fastPeriod];
        _p_volFastSnap ??= new double[_fastPeriod];
        _p_pvSlowSnap ??= new double[_slowPeriod];
        _p_volSlowSnap ??= new double[_slowPeriod];

        Array.Copy(_pvFast, _p_pvFastSnap, _fastPeriod);
        Array.Copy(_volFast, _p_volFastSnap, _fastPeriod);
        Array.Copy(_pvSlow, _p_pvSlowSnap, _slowPeriod);
        Array.Copy(_volSlow, _p_volSlowSnap, _slowPeriod);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RestoreState()
    {
        _pvSumFast = _p_pvSumFast; _volSumFast = _p_volSumFast;
        _headFast = _p_headFast; _countFast = _p_countFast;
        _pvSumSlow = _p_pvSumSlow; _volSumSlow = _p_volSumSlow;
        _headSlow = _p_headSlow; _countSlow = _p_countSlow;
        _signalEma = _p_signalEma; _signalInitialised = _p_signalInitialised;

        if (_p_pvFastSnap != null)
        {
            Array.Copy(_p_pvFastSnap, _pvFast, _fastPeriod);
            Array.Copy(_p_volFastSnap!, _volFast, _fastPeriod);
            Array.Copy(_p_pvSlowSnap!, _pvSlow, _slowPeriod);
            Array.Copy(_p_volSlowSnap!, _volSlow, _slowPeriod);
        }
    }

    // ────────────────────────── IDisposable ──────────────────────────

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
