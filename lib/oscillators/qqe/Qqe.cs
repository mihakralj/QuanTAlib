// QQE: Quantitative Qualitative Estimation
// Multi-stage smoothed RSI oscillator with dynamic volatility-based trailing bands.
// Four-stage pipeline: Wilder RSI → EMA smooth → double EMA of |delta| → trailing SAR-style level.
// All stages are pure IIR — O(1) per bar, zero heap allocations in Update().
// §2 warmup compensators applied to all four EMA accumulators.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// QQE: Quantitative Qualitative Estimation
/// </summary>
/// <remarks>
/// Applies a four-stage smoothing pipeline to RSI and constructs a
/// dynamic volatility-based trailing band (SAR-style signal line).
/// Stage 1: Wilder RSI via RMA (α = 1/rsiPeriod) with §2 warmup.
/// Stage 2: EMA smooth of RSI (α = 2/(SF+1)) → QQE line (rsiMA).
/// Stage 3: Double EMA of |Δ rsiMA| (period = 2×SF−1) → DAR.
/// Stage 4: Trailing level — ratchets directionally, flips on crossover.
/// Dual output: QqeValue (smoothed RSI) and Signal (trailing level).
/// </remarks>
[SkipLocalsInit]
public sealed class Qqe : AbstractBase
{
    private const int DefaultRsiPeriod = 14;
    private const int DefaultSmoothFactor = 5;
    private const double DefaultQqeFactor = 4.236;
    private const double Epsilon = 1e-10;

    private readonly double _rmaAlpha;      // 1/rsiPeriod
    private readonly double _rmaBeta;       // 1 - _rmaAlpha
    private readonly double _sfAlpha;       // 2/(SF+1)
    private readonly double _sfBeta;        // 1 - _sfAlpha
    private readonly double _darAlpha;      // 2/(2*SF)
    private readonly double _darBeta;       // 1 - _darAlpha
    private readonly double _qqeFactor;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        long Count,
        // Stage 1: Wilder RSI
        double PrevSrc,
        double RmaGain,
        double RmaLoss,
        double ERma,
        // Stage 2: EMA of RSI
        double RawRsiMa,
        double ERsiMa,
        double PrevRsiMa,
        // Stage 3: Double EMA of |delta|
        double RawDar1,
        double EDar1,
        double RawDar2,
        double EDar2,
        // Stage 4: Trailing level
        double Trail,
        double PrevRsiMa2,
        // Outputs
        double QqeValue,
        double Signal,
        double LastValidValue);

    private State _s;
    private State _ps;

    /// <summary>Current QQE line value (EMA-smoothed RSI).</summary>
    public double QqeValue => _s.QqeValue;

    /// <summary>Current Signal line value (dynamic trailing level).</summary>
    public double Signal => _s.Signal;

    public override bool IsHot => _s.Count > WarmupPeriod;

    /// <summary>Creates QQE with specified parameters.</summary>
    /// <param name="rsiPeriod">RSI lookback period (default: 14).</param>
    /// <param name="smoothFactor">EMA smoothing factor for RSI (default: 5).</param>
    /// <param name="qqeFactor">Multiplier for the trailing band (default: 4.236).</param>
    public Qqe(int rsiPeriod = DefaultRsiPeriod, int smoothFactor = DefaultSmoothFactor,
               double qqeFactor = DefaultQqeFactor)
    {
        if (rsiPeriod <= 0)
        {
            throw new ArgumentException("RSI period must be greater than 0", nameof(rsiPeriod));
        }
        if (smoothFactor <= 0)
        {
            throw new ArgumentException("Smooth factor must be greater than 0", nameof(smoothFactor));
        }
        if (qqeFactor <= 0.0)
        {
            throw new ArgumentException("QQE factor must be greater than 0", nameof(qqeFactor));
        }

        _qqeFactor = qqeFactor;

        _rmaAlpha = 1.0 / rsiPeriod;
        _rmaBeta  = 1.0 - _rmaAlpha;

        _sfAlpha = 2.0 / (smoothFactor + 1.0);
        _sfBeta  = 1.0 - _sfAlpha;

        int darPeriod = 2 * smoothFactor - 1;
        _darAlpha = 2.0 / (darPeriod + 1.0);
        _darBeta  = 1.0 - _darAlpha;

        WarmupPeriod = rsiPeriod + smoothFactor + darPeriod * 2;

        _s = new State(
            Count: 0,
            PrevSrc: double.NaN,
            RmaGain: 0.0, RmaLoss: 0.0, ERma: 1.0,
            RawRsiMa: 0.0, ERsiMa: 1.0, PrevRsiMa: double.NaN,
            RawDar1: 0.0, EDar1: 1.0,
            RawDar2: 0.0, EDar2: 1.0,
            Trail: 0.0, PrevRsiMa2: 50.0,
            QqeValue: double.NaN, Signal: double.NaN,
            LastValidValue: double.NaN);
        _ps = _s;

        Name = $"Qqe({rsiPeriod},{smoothFactor},{qqeFactor})";
    }

    /// <summary>Creates QQE subscribed to a source publisher.</summary>
    public Qqe(ITValuePublisher source, int rsiPeriod = DefaultRsiPeriod,
               int smoothFactor = DefaultSmoothFactor, double qqeFactor = DefaultQqeFactor)
        : this(rsiPeriod, smoothFactor, qqeFactor)
    {
        source.Pub += Handle;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        if (isNew)
        {
            _ps = _s;
        }
        else
        {
            _s = _ps;
        }

        var s = _s;

        // NaN/Infinity guard — substitute last-valid value
        double val = input.Value;
        if (!double.IsFinite(val))
        {
            val = double.IsFinite(s.LastValidValue) ? s.LastValidValue : 50.0;
        }
        else
        {
            s.LastValidValue = val;
        }

        // ── Stage 1: Wilder RSI via RMA (α = 1/rsiPeriod) with §2 warmup ──
        double chg  = double.IsNaN(s.PrevSrc) ? 0.0 : val - s.PrevSrc;
        s.PrevSrc   = val;
        double gain = chg > 0.0 ? chg : 0.0;
        double loss = chg < 0.0 ? -chg : 0.0;

        s.RmaGain = Math.FusedMultiplyAdd(s.RmaGain, _rmaBeta, gain * _rmaAlpha);
        s.RmaLoss = Math.FusedMultiplyAdd(s.RmaLoss, _rmaBeta, loss * _rmaAlpha);
        s.ERma   *= _rmaBeta;
        double cRma   = s.ERma > Epsilon ? 1.0 / (1.0 - s.ERma) : 1.0;
        double avgGain = s.RmaGain * cRma;
        double avgLoss = s.RmaLoss * cRma;
        double rs     = avgLoss < Epsilon ? 100.0 : avgGain / avgLoss;
        double rsiVal  = 100.0 - 100.0 / (1.0 + rs);

        // ── Stage 2: EMA smooth of RSI (α = 2/(SF+1)) with §2 warmup → rsiMA ──
        s.RawRsiMa = Math.FusedMultiplyAdd(s.RawRsiMa, _sfBeta, rsiVal * _sfAlpha);
        s.ERsiMa  *= _sfBeta;
        double cRsiMa = s.ERsiMa > Epsilon ? 1.0 / (1.0 - s.ERsiMa) : 1.0;
        double rsiMa  = s.RawRsiMa * cRsiMa;

        // ── Stage 3: Double EMA of |Δ rsiMA| with §2 warmup → DAR ──
        double absDelta = double.IsNaN(s.PrevRsiMa) ? 0.0 : Math.Abs(rsiMa - s.PrevRsiMa);
        s.PrevRsiMa = rsiMa;

        s.RawDar1 = Math.FusedMultiplyAdd(s.RawDar1, _darBeta, absDelta * _darAlpha);
        s.EDar1  *= _darBeta;
        double cDar1 = s.EDar1 > Epsilon ? 1.0 / (1.0 - s.EDar1) : 1.0;
        double dar1  = s.RawDar1 * cDar1;

        s.RawDar2 = Math.FusedMultiplyAdd(s.RawDar2, _darBeta, dar1 * _darAlpha);
        s.EDar2  *= _darBeta;
        double cDar2 = s.EDar2 > Epsilon ? 1.0 / (1.0 - s.EDar2) : 1.0;
        double dar   = s.RawDar2 * cDar2;

        // ── Stage 4: Trailing level (directional flip / SAR logic) ──
        double band      = _qqeFactor * dar;
        double upperBand = rsiMa + band;
        double lowerBand = rsiMa - band;

        double newTrail;
        if (rsiMa > s.Trail && s.PrevRsiMa2 > s.Trail)
        {
            newTrail = Math.Max(s.Trail, lowerBand);
        }
        else if (rsiMa < s.Trail && s.PrevRsiMa2 < s.Trail)
        {
            newTrail = Math.Min(s.Trail, upperBand);
        }
        else
        {
            newTrail = rsiMa > s.Trail ? lowerBand : upperBand;
        }

        s.PrevRsiMa2 = rsiMa;
        s.Trail      = newTrail;

        s.Count++;
        s.QqeValue = rsiMa;
        s.Signal   = newTrail;
        _s = s;

        Last = new TValue(input.Time, rsiMa);
        PubEvent(Last, isNew);
        return Last;
    }

    public override TSeries Update(TSeries source)
    {
        Reset();
        int len = source.Count;
        var tList = new System.Collections.Generic.List<long>(len);
        var vList = new System.Collections.Generic.List<double>(len);
        CollectionsMarshal.SetCount(tList, len);
        CollectionsMarshal.SetCount(vList, len);

        var tSpan = CollectionsMarshal.AsSpan(tList);
        var vSpan = CollectionsMarshal.AsSpan(vList);

        for (int i = 0; i < len; i++)
        {
            _ = Update(new TValue(source.Times[i], source.Values[i]));
            tSpan[i] = source.Times[i];
            vSpan[i] = _s.QqeValue;
        }

        return new TSeries(tList, vList);
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        foreach (double value in source)
        {
            _ = Update(new TValue(DateTime.MinValue, value));
        }
    }

    public override void Reset()
    {
        _s = new State(
            Count: 0,
            PrevSrc: double.NaN,
            RmaGain: 0.0, RmaLoss: 0.0, ERma: 1.0,
            RawRsiMa: 0.0, ERsiMa: 1.0, PrevRsiMa: double.NaN,
            RawDar1: 0.0, EDar1: 1.0,
            RawDar2: 0.0, EDar2: 1.0,
            Trail: 0.0, PrevRsiMa2: 50.0,
            QqeValue: double.NaN, Signal: double.NaN,
            LastValidValue: double.NaN);
        _ps = _s;
        Last = default;
    }

    /// <summary>Batch calculation over a TSeries. Returns the QQE line series.</summary>
    public static TSeries Batch(TSeries source, int rsiPeriod = DefaultRsiPeriod,
                                int smoothFactor = DefaultSmoothFactor,
                                double qqeFactor = DefaultQqeFactor)
    {
        var ind = new Qqe(rsiPeriod, smoothFactor, qqeFactor);
        return ind.Update(source);
    }

    /// <summary>Span-based batch calculation (QQE line only).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output,
                             int rsiPeriod = DefaultRsiPeriod,
                             int smoothFactor = DefaultSmoothFactor,
                             double qqeFactor = DefaultQqeFactor)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }
        if (rsiPeriod <= 0)
        {
            throw new ArgumentException("RSI period must be greater than 0", nameof(rsiPeriod));
        }
        if (smoothFactor <= 0)
        {
            throw new ArgumentException("Smooth factor must be greater than 0", nameof(smoothFactor));
        }
        if (qqeFactor <= 0.0)
        {
            throw new ArgumentException("QQE factor must be greater than 0", nameof(qqeFactor));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        var ind = new Qqe(rsiPeriod, smoothFactor, qqeFactor);
        for (int i = 0; i < len; i++)
        {
            output[i] = ind.Update(new TValue(DateTime.MinValue, source[i])).Value;
        }
    }

    /// <summary>Batch returning both QQE line and Signal as a pair of TSeries.</summary>
    public static (TSeries QqeLine, TSeries SignalLine) BatchFull(
        TSeries source,
        int rsiPeriod = DefaultRsiPeriod,
        int smoothFactor = DefaultSmoothFactor,
        double qqeFactor = DefaultQqeFactor)
    {
        var ind = new Qqe(rsiPeriod, smoothFactor, qqeFactor);
        int len = source.Count;
        var tQ = new System.Collections.Generic.List<long>(len);
        var vQ = new System.Collections.Generic.List<double>(len);
        var tS = new System.Collections.Generic.List<long>(len);
        var vS = new System.Collections.Generic.List<double>(len);
        CollectionsMarshal.SetCount(tQ, len);
        CollectionsMarshal.SetCount(vQ, len);
        CollectionsMarshal.SetCount(tS, len);
        CollectionsMarshal.SetCount(vS, len);

        var tQSpan = CollectionsMarshal.AsSpan(tQ);
        var vQSpan = CollectionsMarshal.AsSpan(vQ);
        var tSSpan = CollectionsMarshal.AsSpan(tS);
        var vSSpan = CollectionsMarshal.AsSpan(vS);

        for (int i = 0; i < len; i++)
        {
            _ = ind.Update(new TValue(source.Times[i], source.Values[i]));
            tQSpan[i] = source.Times[i];
            vQSpan[i] = ind.QqeValue;
            tSSpan[i] = source.Times[i];
            vSSpan[i] = ind.Signal;
        }

        return (new TSeries(tQ, vQ), new TSeries(tS, vS));
    }

    /// <summary>Runs batch calc and returns a hot indicator ready for streaming.</summary>
    public static (TSeries Results, Qqe Indicator) Calculate(TSeries source,
        int rsiPeriod = DefaultRsiPeriod, int smoothFactor = DefaultSmoothFactor,
        double qqeFactor = DefaultQqeFactor)
    {
        var indicator = new Qqe(rsiPeriod, smoothFactor, qqeFactor);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(object? sender, in TValueEventArgs args)
    {
        _ = Update(args.Value, args.IsNew);
    }
}
