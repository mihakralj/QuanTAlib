using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Computes the Percentage Volume Oscillator (PVO) that measures the difference between two
/// volume EMAs as a percentage of the slower EMA, similar to MACD applied to volume.
/// </summary>
/// <remarks>
/// PVO Formula:
/// <c>PVO = ((EMA_fast - EMA_slow) / EMA_slow) × 100</c>,
/// <c>Signal = EMA(PVO, signalPeriod)</c>,
/// <c>Histogram = PVO - Signal</c>.
///
/// Positive values indicate volume above average (bullish); negative indicates below average (bearish).
/// This implementation is optimized for streaming updates with O(1) per bar using EMA compensators
/// for proper early-stage bias correction.
/// Non-finite inputs (NaN/±Inf) are sanitized by substituting the last finite value observed.
///
/// For the authoritative algorithm reference, full rationale, and behavioral contracts, see the
/// companion files in the same directory.
/// </remarks>
/// <seealso href="Pvo.md">Detailed documentation</seealso>
/// <seealso href="pvo.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Pvo : ITValuePublisher
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State
    {
        public double EmaFast;
        public double EmaSlow;
        public double EmaSignal;
        public double EFast;
        public double ESlow;
        public double ESignal;
        public double ESlowest;
        public bool Warmup;
        public double LastValidVolume;
    }

    private State _s;
    private State _ps;
    private readonly double _alphaFast;
    private readonly double _alphaSlow;
    private readonly double _alphaSignal;
    private readonly double _betaFast;
    private readonly double _betaSlow;
    private readonly double _betaSignal;
    private readonly double _betaSlowest;

    private const double COMPENSATOR_THRESHOLD = 1e-10;

    public string Name { get; }
    public int WarmupPeriod { get; }
    public TValue Last { get; private set; }
    public TValue Signal { get; private set; }
    public TValue Histogram { get; private set; }
    public bool IsHot => !_s.Warmup;
    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Initializes a new instance of the Pvo class.
    /// </summary>
    /// <param name="fastPeriod">The fast EMA period (default: 12)</param>
    /// <param name="slowPeriod">The slow EMA period (default: 26)</param>
    /// <param name="signalPeriod">The signal line EMA period (default: 9)</param>
    /// <exception cref="ArgumentException">Thrown when periods are invalid</exception>
    public Pvo(int fastPeriod = 12, int slowPeriod = 26, int signalPeriod = 9)
    {
        if (fastPeriod < 1)
        {
            throw new ArgumentException("Fast period must be >= 1", nameof(fastPeriod));
        }
        if (slowPeriod < 1)
        {
            throw new ArgumentException("Slow period must be >= 1", nameof(slowPeriod));
        }
        if (signalPeriod < 1)
        {
            throw new ArgumentException("Signal period must be >= 1", nameof(signalPeriod));
        }
        if (fastPeriod >= slowPeriod)
        {
            throw new ArgumentException("Fast period must be less than slow period", nameof(fastPeriod));
        }

        _alphaFast = 2.0 / (fastPeriod + 1);
        _alphaSlow = 2.0 / (slowPeriod + 1);
        _alphaSignal = 2.0 / (signalPeriod + 1);
        _betaFast = 1.0 - _alphaFast;
        _betaSlow = 1.0 - _alphaSlow;
        _betaSignal = 1.0 - _alphaSignal;
        _betaSlowest = Math.Max(Math.Max(_betaFast, _betaSlow), _betaSignal);

        WarmupPeriod = slowPeriod;
        Name = $"Pvo({fastPeriod},{slowPeriod},{signalPeriod})";

        _s = new State
        {
            EFast = 1.0,
            ESlow = 1.0,
            ESignal = 1.0,
            ESlowest = 1.0,
            Warmup = true,
            LastValidVolume = 0.0
        };
        _ps = _s;
    }

    /// <summary>
    /// Updates the indicator with a new bar.
    /// </summary>
    /// <param name="bar">The bar data containing Volume</param>
    /// <param name="isNew">Whether this is a new bar or an update to the current bar</param>
    /// <returns>The calculated PVO value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar bar, bool isNew = true)
    {
        return Update(new TValue(bar.Time, bar.Volume), isNew);
    }

    /// <summary>
    /// Updates the indicator with a TValue (volume).
    /// </summary>
    /// <param name="value">The volume value</param>
    /// <param name="isNew">Whether this is a new bar or an update to the current bar</param>
    /// <returns>The calculated PVO value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue value, bool isNew = true)
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

        // Handle NaN/Infinity in volume
        double volume = double.IsFinite(value.Value) ? Math.Max(value.Value, 0.0) : s.LastValidVolume;
        if (double.IsFinite(value.Value))
        {
            s.LastValidVolume = Math.Max(value.Value, 0.0);
        }

        // Update EMAs using standard EMA formula: ema = alpha * (value - ema) + ema
        s.EmaFast = Math.FusedMultiplyAdd(_alphaFast, volume - s.EmaFast, s.EmaFast);
        s.EmaSlow = Math.FusedMultiplyAdd(_alphaSlow, volume - s.EmaSlow, s.EmaSlow);

        // Calculate compensated EMA values during warmup
        double fastComp, slowComp;
        if (s.Warmup)
        {
            s.EFast *= _betaFast;
            s.ESlow *= _betaSlow;
            s.ESignal *= _betaSignal;
            s.ESlowest *= _betaSlowest;
            s.Warmup = s.ESlowest > COMPENSATOR_THRESHOLD;

            fastComp = s.EmaFast / (1.0 - s.EFast);
            slowComp = s.EmaSlow / (1.0 - s.ESlow);
        }
        else
        {
            fastComp = s.EmaFast;
            slowComp = s.EmaSlow;
        }

        // Calculate PVO: ((fastEMA - slowEMA) / slowEMA) * 100
        double pvoValue = slowComp != 0.0 ? ((fastComp - slowComp) / slowComp) * 100.0 : 0.0;

        // Update signal EMA
        s.EmaSignal = Math.FusedMultiplyAdd(_alphaSignal, pvoValue - s.EmaSignal, s.EmaSignal);

        // Calculate compensated signal value
        double signalValue = s.Warmup ? s.EmaSignal / (1.0 - s.ESignal) : s.EmaSignal;

        // Calculate histogram
        double histogramValue = pvoValue - signalValue;

        _s = s;

        Last = new TValue(value.Time, pvoValue);
        Signal = new TValue(value.Time, signalValue);
        Histogram = new TValue(value.Time, histogramValue);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    /// <summary>
    /// Updates PVO with a bar series.
    /// </summary>
    public TSeries Update(TBarSeries source)
    {
        var t = new List<long>(source.Count);
        var v = new List<double>(source.Count);

        Reset();

        for (int i = 0; i < source.Count; i++)
        {
            var val = Update(source[i], isNew: true);
            t.Add(val.Time);
            v.Add(val.Value);
        }

        return new TSeries(t, v);
    }

    /// <summary>
    /// Updates PVO with a bar series and returns PVO, Signal, and Histogram.
    /// </summary>
    public (TSeries Pvo, TSeries Signal, TSeries Histogram) UpdateWithSignal(TBarSeries source)
    {
        var tPvo = new List<long>(source.Count);
        var vPvo = new List<double>(source.Count);
        var tSignal = new List<long>(source.Count);
        var vSignal = new List<double>(source.Count);
        var tHistogram = new List<long>(source.Count);
        var vHistogram = new List<double>(source.Count);

        Reset();

        for (int i = 0; i < source.Count; i++)
        {
            var val = Update(source[i], isNew: true);
            tPvo.Add(val.Time);
            vPvo.Add(val.Value);
            tSignal.Add(Signal.Time);
            vSignal.Add(Signal.Value);
            tHistogram.Add(Histogram.Time);
            vHistogram.Add(Histogram.Value);
        }

        return (new TSeries(tPvo, vPvo), new TSeries(tSignal, vSignal), new TSeries(tHistogram, vHistogram));
    }

    /// <summary>
    /// Resets the indicator to its initial state.
    /// </summary>
    public void Reset()
    {
        _s = new State
        {
            EFast = 1.0,
            ESlow = 1.0,
            ESignal = 1.0,
            ESlowest = 1.0,
            Warmup = true,
            LastValidVolume = 0.0
        };
        _ps = _s;
        Last = default;
        Signal = default;
        Histogram = default;
    }

    /// <summary>
    /// Calculates PVO for a series of bars.
    /// </summary>
    /// <param name="bars">The input bar series</param>
    /// <param name="fastPeriod">The fast EMA period</param>
    /// <param name="slowPeriod">The slow EMA period</param>
    /// <param name="signalPeriod">The signal line EMA period</param>
    /// <returns>A TSeries containing the PVO values</returns>
    public static TSeries Calculate(TBarSeries bars, int fastPeriod = 12, int slowPeriod = 26, int signalPeriod = 9)
    {
        if (bars.Count == 0)
        {
            return [];
        }

        var t = bars.Open.Times.ToArray();
        var v = new double[bars.Count];
        var signal = new double[bars.Count];
        var histogram = new double[bars.Count];

        Calculate(bars.Volume.Values, v, signal, histogram, fastPeriod, slowPeriod, signalPeriod);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Calculates PVO values using span-based processing.
    /// </summary>
    /// <param name="volume">Source volumes</param>
    /// <param name="output">Output span for PVO values</param>
    /// <param name="signal">Output span for signal line values</param>
    /// <param name="histogram">Output span for histogram values</param>
    /// <param name="fastPeriod">The fast EMA period</param>
    /// <param name="slowPeriod">The slow EMA period</param>
    /// <param name="signalPeriod">The signal line EMA period</param>
    /// <exception cref="ArgumentException">Thrown when spans have different lengths or parameters are invalid</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> volume, Span<double> output, Span<double> signal,
        Span<double> histogram, int fastPeriod = 12, int slowPeriod = 26, int signalPeriod = 9)
    {
        if (volume.Length != output.Length)
        {
            throw new ArgumentException("Output span must have the same length as input", nameof(output));
        }
        if (volume.Length != signal.Length)
        {
            throw new ArgumentException("Signal span must have the same length as input", nameof(signal));
        }
        if (volume.Length != histogram.Length)
        {
            throw new ArgumentException("Histogram span must have the same length as input", nameof(histogram));
        }
        if (fastPeriod < 1)
        {
            throw new ArgumentException("Fast period must be >= 1", nameof(fastPeriod));
        }
        if (slowPeriod < 1)
        {
            throw new ArgumentException("Slow period must be >= 1", nameof(slowPeriod));
        }
        if (signalPeriod < 1)
        {
            throw new ArgumentException("Signal period must be >= 1", nameof(signalPeriod));
        }
        if (fastPeriod >= slowPeriod)
        {
            throw new ArgumentException("Fast period must be less than slow period", nameof(fastPeriod));
        }

        int length = volume.Length;
        if (length == 0)
        {
            return;
        }

        // EMA parameters
        double alphaFast = 2.0 / (fastPeriod + 1);
        double alphaSlow = 2.0 / (slowPeriod + 1);
        double alphaSignal = 2.0 / (signalPeriod + 1);
        double betaFast = 1.0 - alphaFast;
        double betaSlow = 1.0 - alphaSlow;
        double betaSignal = 1.0 - alphaSignal;
        double betaSlowest = Math.Max(Math.Max(betaFast, betaSlow), betaSignal);

        // State variables
        double emaFast = 0.0;
        double emaSlow = 0.0;
        double emaSignal = 0.0;
        double eFast = 1.0;
        double eSlow = 1.0;
        double eSignal = 1.0;
        double eSlowest = 1.0;
        bool warmup = true;

        for (int i = 0; i < length; i++)
        {
            double vol = Math.Max(volume[i], 0.0);
            if (!double.IsFinite(vol))
            {
                vol = i > 0 ? Math.Max(volume[i - 1], 0.0) : 0.0;
            }

            // Update EMAs
            emaFast = Math.FusedMultiplyAdd(alphaFast, vol - emaFast, emaFast);
            emaSlow = Math.FusedMultiplyAdd(alphaSlow, vol - emaSlow, emaSlow);

            // Calculate compensated values
            double fastComp, slowComp;
            if (warmup)
            {
                eFast *= betaFast;
                eSlow *= betaSlow;
                eSignal *= betaSignal;
                eSlowest *= betaSlowest;
                warmup = eSlowest > COMPENSATOR_THRESHOLD;

                fastComp = emaFast / (1.0 - eFast);
                slowComp = emaSlow / (1.0 - eSlow);
            }
            else
            {
                fastComp = emaFast;
                slowComp = emaSlow;
            }

            // Calculate PVO
            double pvoValue = slowComp != 0.0 ? ((fastComp - slowComp) / slowComp) * 100.0 : 0.0;
            output[i] = pvoValue;

            // Update signal EMA
            emaSignal = Math.FusedMultiplyAdd(alphaSignal, pvoValue - emaSignal, emaSignal);

            // Calculate compensated signal
            double signalValue = warmup ? emaSignal / (1.0 - eSignal) : emaSignal;
            signal[i] = signalValue;

            // Calculate histogram
            histogram[i] = pvoValue - signalValue;
        }
    }
}