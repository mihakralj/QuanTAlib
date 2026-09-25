using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// OBVM: On-Balance Volume Modified (Vitali Apirine, TASC Apr 2020).
/// Smooths OBV with two cascaded EMAs to produce an OBVM line and a Signal line.
///   OBV:    cumulative volume added on up-closes, subtracted on down-closes.
///   OBVM:   EMA(OBV, obvmLength)
///   Signal: EMA(OBVM, signalLength)
/// Dual output: OBVM line + Signal line.
/// </summary>
/// <remarks>
/// All EMA smoothing is computed inline with precomputed alphas;
/// no sub-indicator allocations on the hot path.
/// </remarks>
[SkipLocalsInit]
public sealed class Obvm : ITValuePublisher
{
    private const int DefaultObvmLength = 7;
    private const int DefaultSignalLength = 10;

    private readonly int _obvmLength;
    private readonly int _signalLength;
    private readonly double _obvmAlpha;
    private readonly double _signalAlpha;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double ObvValue,
        double ObvmEma,
        double SignalEma,
        double PrevClose,
        double LastValidClose,
        double LastValidVolume,
        int Index);

    private State _s;
    private State _ps;

    private readonly TBarPublishedHandler _barHandler;

    /// <summary>Display name for the indicator.</summary>
    public string Name { get; }

    /// <summary>Warmup period required before the indicator is considered hot.</summary>
    public int WarmupPeriod { get; }

    /// <summary>Current primary value (same as ObvmValue).</summary>
    public TValue Last { get; private set; }

    /// <summary>The OBVM line (EMA of OBV) for the current bar.</summary>
    public TValue ObvmValue { get; private set; }

    /// <summary>The signal line (EMA of OBVM) for the current bar.</summary>
    public TValue Signal { get; private set; }

    /// <summary>True if the indicator has processed enough bars to be reliable.</summary>
    public bool IsHot => _s.Index >= WarmupPeriod;

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Creates a new OBVM indicator with the specified parameters.
    /// </summary>
    /// <param name="obvmLength">EMA period for smoothing OBV. Default 7.</param>
    /// <param name="signalLength">EMA period for the signal line. Default 10.</param>
    public Obvm(int obvmLength = DefaultObvmLength, int signalLength = DefaultSignalLength)
    {
        if (obvmLength <= 0)
        {
            throw new ArgumentException("OBVM length must be greater than 0", nameof(obvmLength));
        }
        if (signalLength <= 0)
        {
            throw new ArgumentException("Signal length must be greater than 0", nameof(signalLength));
        }

        _obvmLength = obvmLength;
        _signalLength = signalLength;
        _obvmAlpha = 2.0 / (_obvmLength + 1);
        _signalAlpha = 2.0 / (_signalLength + 1);

        _s = new State(0, double.NaN, double.NaN, 0, 0, 0, 0);
        _ps = _s;

        Name = $"OBVM({obvmLength},{signalLength})";
        WarmupPeriod = Math.Max(obvmLength, signalLength) + 1;
        _barHandler = HandleBar;
    }

    /// <summary>
    /// Creates an OBVM indicator and immediately primes it from a source series.
    /// </summary>
    public Obvm(TBarSeries source, int obvmLength = DefaultObvmLength,
        int signalLength = DefaultSignalLength)
        : this(obvmLength, signalLength)
    {
        Prime(source);
        source.Pub += _barHandler;
    }

    private void HandleBar(object? sender, in TBarEventArgs e) => Update(e.Value, e.IsNew);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PubEvent(TValue value, bool isNew = true) =>
        Pub?.Invoke(this, new TValueEventArgs { Value = value, IsNew = isNew });

    /// <summary>
    /// Updates the indicator with a new TBar value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
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

        // Guard inputs — substitute last-valid on NaN/Infinity
        double close = input.Close;
        double volume = input.Volume;

        if (double.IsFinite(close) && close > 0)
        {
            s.LastValidClose = close;
        }
        else
        {
            close = s.LastValidClose;
        }

        if (double.IsFinite(volume) && volume >= 0)
        {
            s.LastValidVolume = volume;
        }
        else
        {
            volume = s.LastValidVolume;
        }

        // OBV calculation — compare close to previous close
        if (s.Index > 0 && s.PrevClose > 0)
        {
            if (close > s.PrevClose)
            {
                s.ObvValue += volume;
            }
            else if (close < s.PrevClose)
            {
                s.ObvValue -= volume;
            }
            // If close == prevClose, OBV stays the same
        }

        s.PrevClose = close;

        // OBVM = EMA(OBV, obvmLength)
        double obvmEma;
        if (double.IsNaN(s.ObvmEma))
        {
            obvmEma = s.ObvValue;
        }
        else
        {
            obvmEma = Math.FusedMultiplyAdd(_obvmAlpha, s.ObvValue - s.ObvmEma, s.ObvmEma);
        }

        // Signal = EMA(OBVM, signalLength)
        double signalEma;
        if (double.IsNaN(s.SignalEma))
        {
            signalEma = obvmEma;
        }
        else
        {
            signalEma = Math.FusedMultiplyAdd(_signalAlpha, obvmEma - s.SignalEma, s.SignalEma);
        }

        s.ObvmEma = obvmEma;
        s.SignalEma = signalEma;

        if (isNew)
        {
            s.Index++;
        }

        _s = s;

        ObvmValue = new TValue(input.Time, obvmEma);
        Signal = new TValue(input.Time, signalEma);
        Last = new TValue(input.Time, obvmEma);

        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>
    /// Updates OBVM with a single TValue (no volume data — OBV stays unchanged).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        // OBV requires volume; without it, we can't compute
        if (isNew)
        {
            _ps = _s;
        }
        else
        {
            _s = _ps;
        }

        Last = new TValue(input.Time, _s.ObvmEma);
        ObvmValue = Last;
        Signal = new TValue(input.Time, _s.SignalEma);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>
    /// Batch-updates from a TBarSeries and returns dual output series.
    /// </summary>
    public (TSeries Obvm, TSeries Signal) Update(TBarSeries source)
    {
        if (source.Count == 0)
        {
            return (new TSeries([], []), new TSeries([], []));
        }

        int len = source.Count;
        var tO = new List<long>(len);
        var vO = new List<double>(len);
        var tSig = new List<long>(len);
        var vSig = new List<double>(len);

        CollectionsMarshal.SetCount(tO, len);
        CollectionsMarshal.SetCount(vO, len);
        CollectionsMarshal.SetCount(tSig, len);
        CollectionsMarshal.SetCount(vSig, len);

        var vOSpan = CollectionsMarshal.AsSpan(vO);
        var vSigSpan = CollectionsMarshal.AsSpan(vSig);

        Batch(source.CloseValues, source.VolumeValues,
            vOSpan, vSigSpan, _obvmLength, _signalLength);

        var tSpan = CollectionsMarshal.AsSpan(tO);
        source.Times.CopyTo(tSpan);
        tSpan.CopyTo(CollectionsMarshal.AsSpan(tSig));

        // Prime internal state for continued streaming
        Prime(source);

        var lastTime = new DateTime(source.Times[^1], DateTimeKind.Utc);
        ObvmValue = new TValue(lastTime, vOSpan[^1]);
        Signal = new TValue(lastTime, vSigSpan[^1]);
        Last = new TValue(lastTime, vOSpan[^1]);

        return (new TSeries(tO, vO), new TSeries(tSig, vSig));
    }

    /// <summary>Primes the indicator from historical data.</summary>
    public void Prime(TBarSeries source)
    {
        Reset();

        if (source.Count == 0)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            Update(source[i], isNew: true);
        }
    }

    /// <summary>Resets the indicator to its initial state.</summary>
    public void Reset()
    {
        _s = new State(0, double.NaN, double.NaN, 0, 0, 0, 0);
        _ps = _s;
        Last = default;
        ObvmValue = default;
        Signal = default;
    }

    /// <summary>
    /// Batch computation over raw spans — zero-allocation hot path.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(
        ReadOnlySpan<double> close,
        ReadOnlySpan<double> volume,
        Span<double> obvmOut,
        Span<double> signalOut,
        int obvmLength = DefaultObvmLength,
        int signalLength = DefaultSignalLength)
    {
        if (obvmLength <= 0)
        {
            throw new ArgumentException("OBVM length must be greater than 0", nameof(obvmLength));
        }
        if (signalLength <= 0)
        {
            throw new ArgumentException("Signal length must be greater than 0", nameof(signalLength));
        }
        if (close.Length != volume.Length)
        {
            throw new ArgumentException("Close and Volume spans must be of the same length", nameof(volume));
        }
        if (obvmOut.Length < close.Length)
        {
            throw new ArgumentException("OBVM output span must be at least as long as input", nameof(obvmOut));
        }
        if (signalOut.Length < close.Length)
        {
            throw new ArgumentException("Signal output span must be at least as long as input", nameof(signalOut));
        }

        int len = close.Length;
        if (len == 0)
        {
            return;
        }

        double obvmAlpha = 2.0 / (obvmLength + 1);
        double signalAlpha = 2.0 / (signalLength + 1);

        double obv = 0;
        double prevClose = close[0];

        // Seed EMA accumulators with OBV[0] = 0 (matches streaming bar-0 seed)
        double obvmEma = 0;
        double signalEma = 0;

        // First bar: OBV = 0, OBVM = 0, Signal = 0
        obvmOut[0] = 0;
        signalOut[0] = 0;

        for (int i = 1; i < len; i++)
        {
            double currentClose = close[i];
            double currentVolume = volume[i];

            // OBV update
            if (double.IsFinite(currentClose) && double.IsFinite(currentVolume) && double.IsFinite(prevClose))
            {
                if (currentClose > prevClose)
                {
                    obv += currentVolume;
                }
                else if (currentClose < prevClose)
                {
                    obv -= currentVolume;
                }
            }

            // OBVM = EMA(OBV)
            obvmEma = Math.FusedMultiplyAdd(obvmAlpha, obv - obvmEma, obvmEma);

            // Signal = EMA(OBVM)
            signalEma = Math.FusedMultiplyAdd(signalAlpha, obvmEma - signalEma, signalEma);

            obvmOut[i] = obvmEma;
            signalOut[i] = signalEma;

            if (double.IsFinite(currentClose))
            {
                prevClose = currentClose;
            }
        }
    }

    /// <summary>
    /// Convenience batch method that returns TSeries tuples.
    /// </summary>
    public static (TSeries Obvm, TSeries Signal) Batch(TBarSeries source,
        int obvmLength = DefaultObvmLength, int signalLength = DefaultSignalLength)
    {
        if (source == null || source.Count == 0)
        {
            return (new TSeries([], []), new TSeries([], []));
        }

        int len = source.Count;
        var tO = new List<long>(len);
        var vO = new List<double>(len);
        var tSig = new List<long>(len);
        var vSig = new List<double>(len);

        CollectionsMarshal.SetCount(tO, len);
        CollectionsMarshal.SetCount(vO, len);
        CollectionsMarshal.SetCount(tSig, len);
        CollectionsMarshal.SetCount(vSig, len);

        Batch(source.CloseValues, source.VolumeValues,
            CollectionsMarshal.AsSpan(vO),
            CollectionsMarshal.AsSpan(vSig),
            obvmLength, signalLength);

        var tSpan = CollectionsMarshal.AsSpan(tO);
        source.Times.CopyTo(tSpan);
        tSpan.CopyTo(CollectionsMarshal.AsSpan(tSig));

        return (new TSeries(tO, vO), new TSeries(tSig, vSig));
    }

    /// <summary>
    /// Creates and primes an OBVM indicator, returning both results and indicator.
    /// </summary>
    public static ((TSeries Obvm, TSeries Signal) Results, Obvm Indicator) Calculate(
        TBarSeries source, int obvmLength = DefaultObvmLength,
        int signalLength = DefaultSignalLength)
    {
        var indicator = new Obvm(obvmLength, signalLength);
        var results = indicator.Update(source);
        return (results, indicator);
    }
}
