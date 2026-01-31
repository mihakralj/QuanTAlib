using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// KVO: Klinger Volume Oscillator
/// </summary>
/// <remarks>
/// Volume-based oscillator comparing volume flow with price movements for money flow trends.
/// Positive values indicate accumulation; negative indicates distribution.
///
/// Calculation: <c>HLC3 = (H+L+C)/3</c>, <c>Trend = ±1 based on HLC3 direction</c>,
/// <c>DM = Trend × Volume × CM</c>, <c>KVO = EMA(DM, fast) - EMA(DM, slow)</c>.
/// </remarks>
/// <seealso href="Kvo.md">Detailed documentation</seealso>
/// <seealso href="kvo.pine">Reference Pine Script implementation</seealso>
[SkipLocalsInit]
public sealed class Kvo : ITValuePublisher
{
    [StructLayout(LayoutKind.Auto)]
    private record struct State
    {
        public double PrevHlc3;
        public double Trend;
        public double EmaFast;
        public double EmaSlow;
        public double EmaSignal;
        public double EFast;
        public double ESlow;
        public double ESignal;
        public double LastValidValue;
        public bool HasPrevHlc3;
    }

    private State _s;
    private State _ps;
    private readonly double _alphaFast;
    private readonly double _alphaSlow;
    private readonly double _alphaSignal;
    private readonly double _decayFast;
    private readonly double _decaySlow;
    private readonly double _decaySignal;

    private const double COMPENSATOR_THRESHOLD = 1e-10;

    public string Name { get; }
    public int WarmupPeriod { get; }
    public TValue Last { get; private set; }
    public TValue Signal { get; private set; }
    public bool IsHot { get; private set; }
    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Initializes a new instance of the Kvo class.
    /// </summary>
    /// <param name="fastPeriod">The fast EMA period (default: 34)</param>
    /// <param name="slowPeriod">The slow EMA period (default: 55)</param>
    /// <param name="signalPeriod">The signal line EMA period (default: 13)</param>
    /// <exception cref="ArgumentException">Thrown when periods are invalid</exception>
    public Kvo(int fastPeriod = 34, int slowPeriod = 55, int signalPeriod = 13)
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
        _decayFast = 1.0 - _alphaFast;
        _decaySlow = 1.0 - _alphaSlow;
        _decaySignal = 1.0 - _alphaSignal;

        WarmupPeriod = slowPeriod;
        Name = $"Kvo({fastPeriod},{slowPeriod},{signalPeriod})";

        _s = new State
        {
            Trend = 1.0,
            EFast = 1.0,
            ESlow = 1.0,
            ESignal = 1.0,
            LastValidValue = 0.0
        };
        _ps = _s;
    }

    /// <summary>
    /// Updates the indicator with a new bar.
    /// </summary>
    /// <param name="bar">The bar data containing High, Low, Close, and Volume</param>
    /// <param name="isNew">Whether this is a new bar or an update to the current bar</param>
    /// <returns>The calculated KVO value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar bar, bool isNew = true)
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

        double high = bar.High;
        double low = bar.Low;
        double close = bar.Close;
        double volume = Math.Max(bar.Volume, 0.0);

        // Calculate HLC3 (typical price)
        double hlc3 = (high + low + close) / 3.0;

        // Determine trend direction
        if (s.HasPrevHlc3)
        {
            if (hlc3 > s.PrevHlc3)
            {
                s.Trend = 1.0;
            }
            else if (hlc3 < s.PrevHlc3)
            {
                s.Trend = -1.0;
            }
            // else trend unchanged
        }

        // Calculate price range and cumulation measure (CM)
        double range = high - low;
        double cm = 0.0;
        if (range > 0)
        {
            cm = Math.Abs(2.0 * ((range - (close - low)) / range) - 1.0);
        }

        // Calculate direction multiplier (DM)
        double dm = s.Trend * volume * cm;

        // Handle NaN/Infinity
        if (!double.IsFinite(dm))
        {
            dm = s.LastValidValue;
        }
        else
        {
            s.LastValidValue = dm;
        }

        // Update EMAs with FMA
        s.EmaFast = Math.FusedMultiplyAdd(s.EmaFast, _decayFast, _alphaFast * dm);
        s.EmaSlow = Math.FusedMultiplyAdd(s.EmaSlow, _decaySlow, _alphaSlow * dm);

        // Calculate compensated EMA values
        double fastValue, slowValue;
        bool warmupComplete = true;

        if (s.EFast > COMPENSATOR_THRESHOLD)
        {
            s.EFast *= _decayFast;
            fastValue = s.EmaFast / (1.0 - s.EFast);
            warmupComplete = false;
        }
        else
        {
            fastValue = s.EmaFast;
        }

        if (s.ESlow > COMPENSATOR_THRESHOLD)
        {
            s.ESlow *= _decaySlow;
            slowValue = s.EmaSlow / (1.0 - s.ESlow);
            warmupComplete = false;
        }
        else
        {
            slowValue = s.EmaSlow;
        }

        // Calculate KVO line
        double kvoLine = fastValue - slowValue;

        // Update signal EMA
        s.EmaSignal = Math.FusedMultiplyAdd(s.EmaSignal, _decaySignal, _alphaSignal * kvoLine);

        // Calculate compensated signal value
        double signalValue;
        if (s.ESignal > COMPENSATOR_THRESHOLD)
        {
            s.ESignal *= _decaySignal;
            signalValue = s.EmaSignal / (1.0 - s.ESignal);
        }
        else
        {
            signalValue = s.EmaSignal;
        }

        // Update previous HLC3
        s.PrevHlc3 = hlc3;
        s.HasPrevHlc3 = true;

        _s = s;

        IsHot = warmupComplete;
        Last = new TValue(bar.Time, kvoLine);
        Signal = new TValue(bar.Time, signalValue);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    /// <summary>
    /// TValue input is not supported for KVO - requires TBar (OHLCV) data.
    /// </summary>
#pragma warning disable S2325 // Method signature must match ITValuePublisher contract
    public TValue Update(TValue value, bool isNew = true)
#pragma warning restore S2325
    {
        throw new NotSupportedException("KVO requires TBar (OHLCV) data. Use Update(TBar) instead.");
    }

    /// <summary>
    /// Updates KVO with a bar series.
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
    /// Updates KVO with a bar series and returns both KVO and Signal.
    /// </summary>
    public (TSeries Kvo, TSeries Signal) UpdateWithSignal(TBarSeries source)
    {
        var tKvo = new List<long>(source.Count);
        var vKvo = new List<double>(source.Count);
        var tSignal = new List<long>(source.Count);
        var vSignal = new List<double>(source.Count);

        Reset();

        for (int i = 0; i < source.Count; i++)
        {
            var val = Update(source[i], isNew: true);
            tKvo.Add(val.Time);
            vKvo.Add(val.Value);
            tSignal.Add(Signal.Time);
            vSignal.Add(Signal.Value);
        }

        return (new TSeries(tKvo, vKvo), new TSeries(tSignal, vSignal));
    }

    /// <summary>
    /// Resets the indicator to its initial state.
    /// </summary>
    public void Reset()
    {
        _s = new State
        {
            Trend = 1.0,
            EFast = 1.0,
            ESlow = 1.0,
            ESignal = 1.0,
            LastValidValue = 0.0
        };
        _ps = _s;
        IsHot = false;
        Last = default;
        Signal = default;
    }

    /// <summary>
    /// Calculates KVO for a series of bars.
    /// </summary>
    /// <param name="bars">The input bar series</param>
    /// <param name="fastPeriod">The fast EMA period</param>
    /// <param name="slowPeriod">The slow EMA period</param>
    /// <param name="signalPeriod">The signal line EMA period</param>
    /// <returns>A TSeries containing the KVO values</returns>
    public static TSeries Calculate(TBarSeries bars, int fastPeriod = 34, int slowPeriod = 55, int signalPeriod = 13)
    {
        if (bars.Count == 0)
        {
            return [];
        }

        var t = bars.Open.Times.ToArray();
        var v = new double[bars.Count];
        var signal = new double[bars.Count];

        Calculate(bars.High.Values, bars.Low.Values, bars.Close.Values, bars.Volume.Values,
            v, signal, fastPeriod, slowPeriod, signalPeriod);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Calculates KVO values using span-based processing.
    /// </summary>
    /// <param name="high">Source high prices</param>
    /// <param name="low">Source low prices</param>
    /// <param name="close">Source close prices</param>
    /// <param name="volume">Source volumes</param>
    /// <param name="output">Output span for KVO values</param>
    /// <param name="signal">Output span for signal line values</param>
    /// <param name="fastPeriod">The fast EMA period</param>
    /// <param name="slowPeriod">The slow EMA period</param>
    /// <param name="signalPeriod">The signal line EMA period</param>
    /// <exception cref="ArgumentException">Thrown when spans have different lengths or parameters are invalid</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Calculate(ReadOnlySpan<double> high, ReadOnlySpan<double> low,
        ReadOnlySpan<double> close, ReadOnlySpan<double> volume,
        Span<double> output, Span<double> signal,
        int fastPeriod = 34, int slowPeriod = 55, int signalPeriod = 13)
    {
        if (high.Length != low.Length)
        {
            throw new ArgumentException("High and low spans must have the same length", nameof(low));
        }
        if (high.Length != close.Length)
        {
            throw new ArgumentException("High and close spans must have the same length", nameof(close));
        }
        if (high.Length != volume.Length)
        {
            throw new ArgumentException("High and volume spans must have the same length", nameof(volume));
        }
        if (high.Length != output.Length)
        {
            throw new ArgumentException("Output span must have the same length as input", nameof(output));
        }
        if (high.Length != signal.Length)
        {
            throw new ArgumentException("Signal span must have the same length as input", nameof(signal));
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

        int length = high.Length;
        if (length == 0)
        {
            return;
        }

        // EMA parameters
        double alphaFast = 2.0 / (fastPeriod + 1);
        double alphaSlow = 2.0 / (slowPeriod + 1);
        double alphaSignal = 2.0 / (signalPeriod + 1);
        double decayFast = 1.0 - alphaFast;
        double decaySlow = 1.0 - alphaSlow;
        double decaySignal = 1.0 - alphaSignal;

        // State variables
        double prevHlc3 = (high[0] + low[0] + close[0]) / 3.0;
        double trend = 1.0;
        double emaFast = 0.0;
        double emaSlow = 0.0;
        double emaSignal = 0.0;
        double eFast = 1.0;
        double eSlow = 1.0;
        double eSignal = 1.0;

        for (int i = 0; i < length; i++)
        {
            double h = high[i];
            double l = low[i];
            double c = close[i];
            double vol = Math.Max(volume[i], 0.0);

            // Calculate HLC3
            double hlc3 = (h + l + c) / 3.0;

            // Determine trend direction
            if (i > 0)
            {
                if (hlc3 > prevHlc3)
                {
                    trend = 1.0;
                }
                else if (hlc3 < prevHlc3)
                {
                    trend = -1.0;
                }
            }

            // Calculate CM
            double range = h - l;
            double cm = range > 0 ? Math.Abs(2.0 * ((range - (c - l)) / range) - 1.0) : 0.0;

            // Calculate DM
            double dm = trend * vol * cm;

            if (!double.IsFinite(dm))
            {
                dm = i > 0 ? output[i - 1] : 0.0;
            }

            // Update EMAs
            emaFast = Math.FusedMultiplyAdd(emaFast, decayFast, alphaFast * dm);
            emaSlow = Math.FusedMultiplyAdd(emaSlow, decaySlow, alphaSlow * dm);

            // Calculate compensated values
            double fastValue, slowValue;

            if (eFast > COMPENSATOR_THRESHOLD)
            {
                eFast *= decayFast;
                fastValue = emaFast / (1.0 - eFast);
            }
            else
            {
                fastValue = emaFast;
            }

            if (eSlow > COMPENSATOR_THRESHOLD)
            {
                eSlow *= decaySlow;
                slowValue = emaSlow / (1.0 - eSlow);
            }
            else
            {
                slowValue = emaSlow;
            }

            // Calculate KVO
            double kvoLine = fastValue - slowValue;
            output[i] = kvoLine;

            // Update signal EMA
            emaSignal = Math.FusedMultiplyAdd(emaSignal, decaySignal, alphaSignal * kvoLine);

            if (eSignal > COMPENSATOR_THRESHOLD)
            {
                eSignal *= decaySignal;
                signal[i] = emaSignal / (1.0 - eSignal);
            }
            else
            {
                signal[i] = emaSignal;
            }

            prevHlc3 = hlc3;
        }
    }
}