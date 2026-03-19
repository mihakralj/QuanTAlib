// AMFM: Ehlers AM Detector / FM Demodulator
// Decomposes price into amplitude (volatility) and frequency (timing) via DSP.
// Reference: John F. Ehlers, TASC May–Jun 2021, mesasoftware.com/papers/AMFM.pdf

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// AMFM: Ehlers AM Detector / FM Demodulator
/// </summary>
/// <remarks>
/// Decomposes price movement into amplitude (AM) and frequency (FM) components
/// using digital signal processing techniques from radio engineering.
///
/// <list type="number">
///   <item>AM Detector: <c>Deriv = Close − Open</c>, envelope = rolling max(|Deriv|, 4),
///         AM = SMA(envelope, 8). Measures volatility.</item>
///   <item>FM Demodulator: <c>Deriv = Close − Open</c>, hard-limit to ±1 (10× gain),
///         integrate via Super Smoother. Tracks price-movement timing.</item>
/// </list>
///
/// Reference: John F. Ehlers, "A Technical Description of Market Data for Traders",
/// TASC May 2021; "Creating More Robust Trading Strategies With The FM Demodulator",
/// TASC June 2021.
/// </remarks>
/// <seealso href="Amfm.md">Detailed documentation</seealso>
/// <seealso href="amfm.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Amfm : ITValuePublisher
{
    // Super Smoother coefficients (FM path)
    private readonly double _c1, _c2, _c3;

    // AM: circular buffer of size 4 for rolling max of |Deriv|
    private readonly double[] _amEnvBuf;
    // AM: circular buffer of size 8 for SMA of envelope
    private readonly double[] _amSmaBuf;

    // Snapshots
    private readonly double[] _amEnvSnap;
    private readonly double[] _amSmaSnap;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double AmSmaSum,     // running sum for SMA(8) of envelope
        double FmSs,         // Super Smoother current value
        double FmSsPrev,     // Super Smoother previous value
        double FmHlPrev,     // previous hard-limited value
        double Am,           // current AM output
        double Fm,           // current FM output
        double LastValidOpen,
        double LastValidClose,
        int EnvIdx,          // write index into _amEnvBuf
        int SmaIdx,          // write index into _amSmaBuf
        int Count);

    private State _s;
    private State _ps;

    private readonly TBarPublishedHandler _barHandler;

    /// <summary>Display name.</summary>
    public string Name { get; }

    /// <summary>Bars needed for first valid output.</summary>
    public int WarmupPeriod { get; }

    /// <summary>True once warmup is complete.</summary>
    public bool IsHot => _s.Count >= WarmupPeriod;

    /// <summary>Current AM detector value (volatility, ≥ 0).</summary>
    public double Am => _s.Am;

    /// <summary>Current FM demodulator value (timing, ≈ [-1, +1]).</summary>
    public double Fm => _s.Fm;

    /// <summary>Primary output (FM as TValue).</summary>
    public TValue Last { get; private set; }

    /// <inheritdoc />
    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Creates an AMFM indicator.
    /// </summary>
    /// <param name="period">Super Smoother period for FM path (must be &gt; 0, default 30).</param>
    public Amfm(int period = 30)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        // Super Smoother coefficients (2-pole Butterworth)
        double a1 = Math.Exp(-1.414 * Math.PI / period);
        double b1 = 2.0 * a1 * Math.Cos(1.414 * Math.PI / period);
        _c2 = b1;
        _c3 = -(a1 * a1);
        _c1 = 1.0 - _c2 - _c3;

        _amEnvBuf = new double[4];
        _amSmaBuf = new double[8];
        _amEnvSnap = new double[4];
        _amSmaSnap = new double[8];

        _s = default;
        _ps = default;

        WarmupPeriod = Math.Max(12, period);
        Name = $"Amfm({period})";
        _barHandler = HandleBar;
    }

    /// <summary>
    /// Creates AMFM chained to a TBarSeries source.
    /// </summary>
    public Amfm(TBarSeries source, int period = 30) : this(period)
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
        _s = default;
        _ps = default;
        Last = default;
        Array.Clear(_amEnvBuf);
        Array.Clear(_amSmaBuf);
        Array.Clear(_amEnvSnap);
        Array.Clear(_amSmaSnap);
    }

    /// <summary>
    /// Updates AMFM with a new bar.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        double openVal = input.Open;
        double closeVal = input.Close;

        // Sanitize NaN/Inf
        if (!double.IsFinite(openVal))
        {
            openVal = double.IsFinite(_s.LastValidOpen) ? _s.LastValidOpen : 0.0;
        }
        else
        {
            _s.LastValidOpen = openVal;
        }

        if (!double.IsFinite(closeVal))
        {
            closeVal = double.IsFinite(_s.LastValidClose) ? _s.LastValidClose : 0.0;
        }
        else
        {
            _s.LastValidClose = closeVal;
        }

        if (isNew)
        {
            _ps = _s;
            Array.Copy(_amEnvBuf, _amEnvSnap, 4);
            Array.Copy(_amSmaBuf, _amSmaSnap, 8);
            _s.Count++;
        }
        else
        {
            _s = _ps;
            Array.Copy(_amEnvSnap, _amEnvBuf, 4);
            Array.Copy(_amSmaSnap, _amSmaBuf, 8);
        }

        // ── Whitened derivative ──────────────────────────────────────
        double deriv = closeVal - openVal;

        // ── AM Detector ──────────────────────────────────────────────
        // Step 1: Envelope = rolling max(|Deriv|, 4)
        double absDeriv = Math.Abs(deriv);
        int envIdx = _s.EnvIdx;
        _amEnvBuf[envIdx] = absDeriv;
        if (isNew)
        {
            _s.EnvIdx = (envIdx + 1) & 3; // mod 4
        }

        // Find max of the 4-element envelope buffer
        double envel = _amEnvBuf[0];
        if (_amEnvBuf[1] > envel)
        {
            envel = _amEnvBuf[1];
        }
        if (_amEnvBuf[2] > envel)
        {
            envel = _amEnvBuf[2];
        }
        if (_amEnvBuf[3] > envel)
        {
            envel = _amEnvBuf[3];
        }

        // Step 2: AM = SMA(envelope, 8)
        int smaIdx = _s.SmaIdx;
        double oldSma = _amSmaBuf[smaIdx];
        _amSmaBuf[smaIdx] = envel;
        if (isNew)
        {
            _s.SmaIdx = (smaIdx + 1) & 7; // mod 8
        }

        double smaSum = _s.AmSmaSum - oldSma + envel;
        _s.AmSmaSum = smaSum;

        int smaCount = Math.Min(_s.Count, 8);
        double am = smaCount > 0 ? smaSum / smaCount : 0.0;
        _s.Am = am;

        // ── FM Demodulator ───────────────────────────────────────────
        // Step 1: Hard limiter (10x gain, clamp to ±1)
        double hl = 10.0 * deriv;
        if (hl > 1.0)
        {
            hl = 1.0;
        }
        else if (hl < -1.0)
        {
            hl = -1.0;
        }

        // Step 2: Super Smoother (2-pole Butterworth IIR)
        double fm;
        if (_s.Count <= 2)
        {
            fm = deriv; // passthrough before IIR is stable
        }
        else
        {
            fm = (_c1 * (hl + _s.FmHlPrev) * 0.5) + (_c2 * _s.FmSs) + (_c3 * _s.FmSsPrev);
        }

        _s.FmSsPrev = _s.FmSs;
        _s.FmSs = fm;
        _s.FmHlPrev = hl;
        _s.Fm = fm;

        Last = new TValue(input.Time, fm);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>
    /// Updates from a TBarSeries, returning dual outputs.
    /// </summary>
    public (TSeries Am, TSeries Fm) UpdateAll(TBarSeries source)
    {
        int len = source.Count;
        if (len == 0)
        {
            return ([], []);
        }

        var amTimes = new List<long>(len);
        var amVals = new List<double>(len);
        var fmTimes = new List<long>(len);
        var fmVals = new List<double>(len);
        CollectionsMarshal.SetCount(amTimes, len);
        CollectionsMarshal.SetCount(amVals, len);
        CollectionsMarshal.SetCount(fmTimes, len);
        CollectionsMarshal.SetCount(fmVals, len);

        var amT = CollectionsMarshal.AsSpan(amTimes);
        var amV = CollectionsMarshal.AsSpan(amVals);
        var fmT = CollectionsMarshal.AsSpan(fmTimes);
        var fmV = CollectionsMarshal.AsSpan(fmVals);

        Reset();
        for (int i = 0; i < len; i++)
        {
            Update(source[i]);
            long t = source[i].Time;
            amT[i] = t;
            amV[i] = _s.Am;
            fmT[i] = t;
            fmV[i] = _s.Fm;
        }

        return (new TSeries(amTimes, amVals), new TSeries(fmTimes, fmVals));
    }

    /// <summary>Batch-process span data (dual output).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> open, ReadOnlySpan<double> close,
        Span<double> amOutput, Span<double> fmOutput, int period = 30)
    {
        int len = open.Length;
        if (len != close.Length || len != amOutput.Length || len != fmOutput.Length)
        {
            throw new ArgumentException("All spans must have the same length", nameof(open));
        }
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }
        if (len == 0)
        {
            return;
        }

        // Super Smoother coefficients
        double a1 = Math.Exp(-1.414 * Math.PI / period);
        double b1 = 2.0 * a1 * Math.Cos(1.414 * Math.PI / period);
        double c2 = b1;
        double c3 = -(a1 * a1);
        double c1 = 1.0 - c2 - c3;

        // AM state
        Span<double> envBuf = stackalloc double[4];
        envBuf.Clear();
        Span<double> smaBuf = stackalloc double[8];
        smaBuf.Clear();
        double smaSum = 0.0;
        int envIdx = 0;
        int smaIdx = 0;

        // FM state
        double fmSs = 0.0;
        double fmSsPrev = 0.0;
        double hlPrev = 0.0;

        for (int i = 0; i < len; i++)
        {
            double deriv = close[i] - open[i];
            double absDeriv = Math.Abs(deriv);

            // AM: envelope (rolling max over 4)
            envBuf[envIdx] = absDeriv;
            envIdx = (envIdx + 1) & 3;

            double envel = envBuf[0];
            if (envBuf[1] > envel)
            {
                envel = envBuf[1];
            }
            if (envBuf[2] > envel)
            {
                envel = envBuf[2];
            }
            if (envBuf[3] > envel)
            {
                envel = envBuf[3];
            }

            // AM: SMA(envelope, 8)
            double oldSma = smaBuf[smaIdx];
            smaBuf[smaIdx] = envel;
            smaIdx = (smaIdx + 1) & 7;
            smaSum = smaSum - oldSma + envel;
            int smaCount = Math.Min(i + 1, 8);
            amOutput[i] = smaSum / smaCount;

            // FM: hard limiter
            double hl = 10.0 * deriv;
            if (hl > 1.0)
            {
                hl = 1.0;
            }
            else if (hl < -1.0)
            {
                hl = -1.0;
            }

            // FM: Super Smoother
            double fm;
            if (i <= 1)
            {
                fm = deriv;
            }
            else
            {
                fm = (c1 * (hl + hlPrev) * 0.5) + (c2 * fmSs) + (c3 * fmSsPrev);
            }
            fmSsPrev = fmSs;
            fmSs = fm;
            hlPrev = hl;
            fmOutput[i] = fm;
        }
    }

    /// <summary>Primes the indicator from historical bars.</summary>
    public void Prime(TBarSeries source)
    {
        for (int i = 0; i < source.Count; i++)
        {
            Update(source[i]);
        }
    }

    /// <summary>Calculate and return both results and indicator.</summary>
    public static ((TSeries Am, TSeries Fm) Results, Amfm Indicator) Calculate(
        TBarSeries source, int period = 30)
    {
        var ind = new Amfm(period);
        return (ind.UpdateAll(source), ind);
    }
}
