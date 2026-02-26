// DOSC: Derivative Oscillator
// Four-stage pipeline: Wilder RSI → EMA1 → EMA2 (double-smooth) → SMA signal → DOSC = EMA2 - Signal
// Formula: DOSC = EMA2(EMA1(RSI(src, rsi))) - SMA(EMA2(EMA1(RSI(src, rsi))), sig)
// Source: Brown, C. (1994). Technical Analysis for the Trading Professional. McGraw-Hill.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// DOSC: Derivative Oscillator
/// </summary>
/// <remarks>
/// Applies a four-stage pipeline to extract momentum inflection points:
/// Wilder RSI → first EMA smoothing → second EMA (double-smooth) → SMA signal line.
/// DOSC = EMA2 - SMA(EMA2). Zero crossings mark momentum acceleration/deceleration.
///
/// Calculation:
/// <c>avgGain/avgLoss via Wilder RMA (alpha = 1/rsiPeriod)</c>
/// <c>RSI = 100 - 100 / (1 + avgGain / avgLoss)</c>
/// <c>EMA1 = alpha1 * RSI + (1-alpha1) * EMA1[1]</c>
/// <c>EMA2 = alpha2 * EMA1 + (1-alpha2) * EMA2[1]</c>
/// <c>Signal = SMA(EMA2, sigPeriod)   [O(1) via circular buffer + running sum]</c>
/// <c>DOSC = EMA2 - Signal</c>
/// </remarks>
/// <seealso href="Dosc.md">Detailed documentation</seealso>
/// <seealso href="dosc.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Dosc : AbstractBase
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double AvgGain, double AvgLoss,
        double Ema1, double Ema2,
        double SigSum,
        int SigHead, int SigCount,
        double PrevSig,
        double Src1,
        int Count,
        double LastValidSrc,
        bool Ema1Init, bool Ema2Init)
    {
        public static State New() => new()
        {
            AvgGain = 0,
            AvgLoss = 0,
            Ema1 = 0,
            Ema2 = 0,
            SigSum = 0,
            SigHead = 0,
            SigCount = 0,
            PrevSig = 0,
            Src1 = 0,
            Count = 0,
            LastValidSrc = 0,
            Ema1Init = false,
            Ema2Init = false
        };
    }

    private readonly int _sigPeriod;

    private readonly double _rsiAlpha;   // 1/rsiPeriod  (Wilder RMA)
    private readonly double _rsiDecay;   // 1 - _rsiAlpha
    private readonly double _alpha1;     // 2/(ema1Period+1)
    private readonly double _decay1;     // 1 - _alpha1
    private readonly double _alpha2;     // 2/(ema2Period+1)
    private readonly double _decay2;     // 1 - _alpha2

    private State _s = State.New();
    private State _ps = State.New();

    // Signal-line SMA circular buffer — size = sigPeriod
    private readonly double[] _sigBuf;
    private int _sigSnapHead;            // snapshot of _s.SigHead for rollback

    private const int StackallocThreshold = 256;

    /// <summary>
    /// Creates DOSC with specified parameters.
    /// </summary>
    /// <param name="rsiPeriod">RSI Wilder smoothing period (must be &gt; 0)</param>
    /// <param name="ema1Period">First EMA smoothing period (must be &gt; 0)</param>
    /// <param name="ema2Period">Second EMA (double-smooth) period (must be &gt; 0)</param>
    /// <param name="sigPeriod">SMA signal line period (must be &gt; 0)</param>
    public Dosc(int rsiPeriod = 14, int ema1Period = 5, int ema2Period = 3, int sigPeriod = 9)
    {
        if (rsiPeriod <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rsiPeriod), rsiPeriod, "Period must be greater than 0.");
        }
        if (ema1Period <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ema1Period), ema1Period, "Period must be greater than 0.");
        }
        if (ema2Period <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ema2Period), ema2Period, "Period must be greater than 0.");
        }
        if (sigPeriod <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sigPeriod), sigPeriod, "Period must be greater than 0.");
        }

        _sigPeriod = sigPeriod;

        _rsiAlpha = 1.0 / rsiPeriod;
        _rsiDecay = 1.0 - _rsiAlpha;
        _alpha1 = 2.0 / (ema1Period + 1.0);
        _decay1 = 1.0 - _alpha1;
        _alpha2 = 2.0 / (ema2Period + 1.0);
        _decay2 = 1.0 - _alpha2;

        _sigBuf = new double[sigPeriod];
        _sigSnapHead = 0;

        Name = $"Dosc({rsiPeriod},{ema1Period},{ema2Period},{sigPeriod})";
        // Warmup: RSI needs rsiPeriod; EMA1/EMA2 converge quickly; SMA signal needs sigPeriod.
        WarmupPeriod = rsiPeriod + sigPeriod;
    }

    /// <summary>
    /// Creates DOSC subscribing to specified source publisher.
    /// </summary>
    public Dosc(ITValuePublisher source, int rsiPeriod = 14, int ema1Period = 5, int ema2Period = 3, int sigPeriod = 9)
        : this(rsiPeriod, ema1Period, ema2Period, sigPeriod)
    {
        source.Pub += Handle;
    }

    /// <summary>
    /// Creates DOSC from a TSeries source, primes from history, then subscribes.
    /// </summary>
    public Dosc(TSeries source, int rsiPeriod = 14, int ema1Period = 5, int ema2Period = 3, int sigPeriod = 9)
        : this(rsiPeriod, ema1Period, ema2Period, sigPeriod)
    {
        Prime(source.Values);
        if (source.Count > 0)
        {
            Last = new TValue(source.LastTime, Last.Value);
        }
        source.Pub += Handle;
    }

    /// <inheritdoc/>
    public override bool IsHot => _s.Count >= WarmupPeriod;

    /// <inheritdoc/>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        if (source.Length == 0)
        {
            return;
        }

        _s = State.New();
        _ps = State.New();
        Array.Clear(_sigBuf);
        _sigSnapHead = 0;

        int len = source.Length;
        double[]? rented = len > StackallocThreshold ? ArrayPool<double>.Shared.Rent(len) : null;
        Span<double> temp = rented != null ? rented.AsSpan(0, len) : stackalloc double[len];

        try
        {
            CalculateCore(source, temp, ref _s, _sigBuf,
                _rsiAlpha, _rsiDecay, _alpha1, _decay1, _alpha2, _decay2, _sigPeriod);

            Last = new TValue(DateTime.MinValue, temp[len - 1]);
            _ps = _s;
            _sigSnapHead = _s.SigHead;
        }
        finally
        {
            if (rented != null)
            {
                ArrayPool<double>.Shared.Return(rented);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double GetValidValue(double input, ref State s)
    {
        if (double.IsFinite(input))
        {
            s.LastValidSrc = input;
            return input;
        }
        return s.LastValidSrc;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _ps = _s;
            _sigSnapHead = _s.SigHead;
        }
        else
        {
            // _s.PrevSig was set during the last Compute call — it holds the old
            // slot value that was overwritten at _sigSnapHead. Capture it before
            // restoring _s so we can put the buffer slot back.
            double prevSlot = _s.PrevSig;
            int snapHead = _sigSnapHead;
            _s = _ps;
            _s.SigHead = snapHead;
            // Restore the circular buffer slot that was overwritten in the bad bar.
            _sigBuf[snapHead] = prevSlot;
        }

        double val = GetValidValue(input.Value, ref _s);
        double result = Compute(val, ref _s, _sigBuf,
            _rsiAlpha, _rsiDecay, _alpha1, _decay1, _alpha2, _decay2, _sigPeriod);

        Last = new TValue(input.Time, result);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public override TSeries Update(TSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        CalculateCore(source.Values, vSpan, ref _s, _sigBuf,
            _rsiAlpha, _rsiDecay, _alpha1, _decay1, _alpha2, _decay2, _sigPeriod);

        source.Times.CopyTo(tSpan);
        _ps = _s;
        _sigSnapHead = _s.SigHead;
        Last = new TValue(tSpan[len - 1], vSpan[len - 1]);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Core per-bar streaming computation: Wilder RSI → EMA1 → EMA2 → SMA signal → DOSC.
    /// O(1) per bar for all four stages.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double Compute(double src, ref State s, double[] sigBuf,
        double rsiAlpha, double rsiDecay,
        double alpha1, double decay1,
        double alpha2, double decay2,
        int sigPeriod)
    {
        s.Count++;

        // --- Stage 1: Wilder RSI ---
        double changeUp = s.Count > 1 ? Math.Max(src - s.Src1, 0.0) : 0.0;
        double changeDn = s.Count > 1 ? Math.Max(s.Src1 - src, 0.0) : 0.0;
        s.Src1 = src;

        if (s.Count <= 1)
        {
            s.AvgGain = changeUp;
            s.AvgLoss = changeDn;
        }
        else
        {
            s.AvgGain = Math.FusedMultiplyAdd(rsiAlpha, changeUp, rsiDecay * s.AvgGain);
            s.AvgLoss = Math.FusedMultiplyAdd(rsiAlpha, changeDn, rsiDecay * s.AvgLoss);
        }

        double rsiVal = s.AvgLoss == 0.0 ? 100.0 : 100.0 - 100.0 / (1.0 + s.AvgGain / s.AvgLoss);

        // --- Stage 2: EMA1 of RSI ---
        double ema1;
        if (!s.Ema1Init)
        {
            s.Ema1Init = true;
            ema1 = rsiVal;
        }
        else
        {
            ema1 = Math.FusedMultiplyAdd(alpha1, rsiVal, decay1 * s.Ema1);
        }
        s.Ema1 = ema1;

        // --- Stage 3: EMA2 of EMA1 ---
        double ema2;
        if (!s.Ema2Init)
        {
            s.Ema2Init = true;
            ema2 = ema1;
        }
        else
        {
            ema2 = Math.FusedMultiplyAdd(alpha2, ema1, decay2 * s.Ema2);
        }
        s.Ema2 = ema2;

        // --- Stage 4: SMA signal via circular buffer + running sum (O(1)) ---
        double oldestSlot = sigBuf[s.SigHead];
        bool slotWasFilled = s.SigCount >= sigPeriod;

        if (slotWasFilled)
        {
            s.SigSum -= oldestSlot;
        }
        else
        {
            s.SigCount++;
        }

        s.PrevSig = oldestSlot;
        sigBuf[s.SigHead] = ema2;
        s.SigSum += ema2;

        s.SigHead = (s.SigHead + 1) % sigPeriod;

        double signal = s.SigCount > 0 ? s.SigSum / s.SigCount : 0.0;

        return ema2 - signal;
    }

    /// <summary>Core batch calculation — iterates source calling Compute per bar.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CalculateCore(ReadOnlySpan<double> source, Span<double> output, ref State s,
        double[] sigBuf,
        double rsiAlpha, double rsiDecay,
        double alpha1, double decay1,
        double alpha2, double decay2,
        int sigPeriod)
    {
        int len = source.Length;
        for (int i = 0; i < len; i++)
        {
            double val = source[i];
            if (double.IsFinite(val))
            {
                s.LastValidSrc = val;
            }
            else
            {
                val = s.LastValidSrc;
            }

            output[i] = Compute(val, ref s, sigBuf,
                rsiAlpha, rsiDecay, alpha1, decay1, alpha2, decay2, sigPeriod);
        }
    }

    /// <summary>
    /// Batch calculation returning a TSeries.
    /// </summary>
    public static TSeries Batch(TSeries source, int rsiPeriod = 14, int ema1Period = 5, int ema2Period = 3, int sigPeriod = 9)
    {
        var indicator = new Dosc(rsiPeriod, ema1Period, ema2Period, sigPeriod);
        return indicator.Update(source);
    }

    /// <summary>
    /// Batch calculation writing to a pre-allocated output span. Zero-allocation hot path.
    /// </summary>
    public static void Batch(ReadOnlySpan<double> source, Span<double> output,
        int rsiPeriod = 14, int ema1Period = 5, int ema2Period = 3, int sigPeriod = 9)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length.", nameof(output));
        }
        if (rsiPeriod <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rsiPeriod), rsiPeriod, "Period must be greater than 0.");
        }
        if (ema1Period <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ema1Period), ema1Period, "Period must be greater than 0.");
        }
        if (ema2Period <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ema2Period), ema2Period, "Period must be greater than 0.");
        }
        if (sigPeriod <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sigPeriod), sigPeriod, "Period must be greater than 0.");
        }

        if (source.Length == 0)
        {
            return;
        }

        double ra = 1.0 / rsiPeriod;
        double rd = 1.0 - ra;
        double a1 = 2.0 / (ema1Period + 1.0);
        double d1 = 1.0 - a1;
        double a2 = 2.0 / (ema2Period + 1.0);
        double d2 = 1.0 - a2;

        var state = State.New();
        var sigBuf = new double[sigPeriod];

        CalculateCore(source, output, ref state, sigBuf, ra, rd, a1, d1, a2, d2, sigPeriod);
    }

    /// <summary>
    /// Creates a hot indicator from historical data, ready for streaming.
    /// </summary>
    public static (TSeries Results, Dosc Indicator) Calculate(TSeries source,
        int rsiPeriod = 14, int ema1Period = 5, int ema2Period = 3, int sigPeriod = 9)
    {
        var indicator = new Dosc(rsiPeriod, ema1Period, ema2Period, sigPeriod);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        _s = State.New();
        _ps = _s;
        Array.Clear(_sigBuf);
        _sigSnapHead = 0;
        Last = default;
    }
}
