using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// BBS: Bollinger Band Squeeze
/// </summary>
/// <remarks>
/// <para>
/// Detects when Bollinger Bands contract inside Keltner Channels,
/// indicating low volatility consolidation that typically precedes breakouts.
/// </para>
///
/// Squeeze Detection:
/// <c>SqueezeOn = BB_Upper &lt; KC_Upper AND BB_Lower &gt; KC_Lower</c>
///
/// Bandwidth Output:
/// <c>Bandwidth = ((BB_Upper - BB_Lower) / BB_Middle) * 100</c>
///
/// Bollinger Bands:
/// <c>BB_Middle = SMA(close, bbPeriod)</c>
/// <c>BB_Dev = sqrt(E[x^2] - E[x]^2)</c>
/// <c>BB_Upper = BB_Middle + bbMult * BB_Dev</c>
/// <c>BB_Lower = BB_Middle - bbMult * BB_Dev</c>
///
/// Keltner Channels:
/// <c>KC_Middle = SMA(close, kcPeriod)</c>
/// <c>ATR = EMA-smoothed True Range with warmup compensation</c>
/// <c>KC_Upper = KC_Middle + kcMult * ATR</c>
/// <c>KC_Lower = KC_Middle - kcMult * ATR</c>
///
/// References:
/// - John Bollinger, "Bollinger on Bollinger Bands"
/// - PineScript reference: bbs.pine
/// </remarks>
[SkipLocalsInit]
public sealed class Bbs : ITValuePublisher
{
    private readonly int _bbPeriod;
    private readonly double _bbMult;
    private readonly int _kcPeriod;
    private readonly double _kcMult;

    // Bollinger Bands: rolling sum/sumSq for O(1) SMA + stddev
    private readonly RingBuffer _bbBuffer;

    // Keltner Channel: rolling sum for SMA middle
    private readonly RingBuffer _kcBuffer;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double BbSum,
        double BbSumSq,
        double KcSum,
        double AtrRaw,
        double AtrE,
        double PrevClose,
        double LastValidClose,
        double LastValidHigh,
        double LastValidLow,
        int Bars,
        bool IsHot);

    private State _state;
    private State _p_state;

    private const int ResyncInterval = 1000;
    private int _tickCount;
    private int _p_tickCount;

    // Saved squeeze state for SqueezeFired detection
    private bool _prevSqueezeOn;
    private bool _p_prevSqueezeOn;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Event publisher for value updates.
    /// </summary>
    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// The bandwidth value: ((BB_Upper - BB_Lower) / BB_Middle) * 100.
    /// Primary numeric output.
    /// </summary>
    public TValue Last { get; private set; }

    /// <summary>
    /// True when Bollinger Bands are inside Keltner Channel (squeeze condition).
    /// </summary>
    public bool SqueezeOn { get; private set; }

    /// <summary>
    /// True when squeeze just ended (first bar where squeeze transitions Off).
    /// </summary>
    public bool SqueezeFired { get; private set; }

    /// <summary>
    /// True when indicator has enough data for valid output.
    /// </summary>
    public bool IsHot => _state.IsHot;

    /// <summary>
    /// Number of bars required for warmup.
    /// </summary>
    public int WarmupPeriod { get; }

    /// <summary>
    /// Bollinger Band period.
    /// </summary>
    public int BbPeriod => _bbPeriod;

    /// <summary>
    /// Bollinger Band standard deviation multiplier.
    /// </summary>
    public double BbMult => _bbMult;

    /// <summary>
    /// Keltner Channel period.
    /// </summary>
    public int KcPeriod => _kcPeriod;

    /// <summary>
    /// Keltner Channel ATR multiplier.
    /// </summary>
    public double KcMult => _kcMult;

    /// <summary>
    /// Creates BBS indicator with specified parameters.
    /// </summary>
    /// <param name="bbPeriod">Bollinger Band period (default 20, must be &gt; 0)</param>
    /// <param name="bbMult">Bollinger Band standard deviation multiplier (default 2.0, must be &gt; 0)</param>
    /// <param name="kcPeriod">Keltner Channel period (default 20, must be &gt; 0)</param>
    /// <param name="kcMult">Keltner Channel ATR multiplier (default 1.5, must be &gt; 0)</param>
    public Bbs(int bbPeriod = 20, double bbMult = 2.0, int kcPeriod = 20, double kcMult = 1.5)
    {
        if (bbPeriod <= 0)
        {
            throw new ArgumentException("BB Period must be greater than 0", nameof(bbPeriod));
        }

        if (kcPeriod <= 0)
        {
            throw new ArgumentException("KC Period must be greater than 0", nameof(kcPeriod));
        }

        if (bbMult <= 0)
        {
            throw new ArgumentException("BB Multiplier must be greater than 0", nameof(bbMult));
        }

        if (kcMult <= 0)
        {
            throw new ArgumentException("KC Multiplier must be greater than 0", nameof(kcMult));
        }

        _bbPeriod = bbPeriod;
        _bbMult = bbMult;
        _kcPeriod = kcPeriod;
        _kcMult = kcMult;

        Name = $"Bbs({bbPeriod},{bbMult:F1},{kcPeriod},{kcMult:F1})";
        WarmupPeriod = Math.Max(bbPeriod, kcPeriod);

        _bbBuffer = new RingBuffer(bbPeriod);
        _kcBuffer = new RingBuffer(kcPeriod);

        _state = new State(0, 0, 0, 0, 1.0, double.NaN, double.NaN, double.NaN, double.NaN, 0, false);
        _p_state = _state;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PubEvent(TValue value, bool isNew = true) =>
        Pub?.Invoke(this, new TValueEventArgs { Value = value, IsNew = isNew });

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private (double close, double high, double low) GetValidValues(double close, double high, double low)
    {
        if (double.IsFinite(close))
        {
            _state = _state with { LastValidClose = close };
        }
        else if (double.IsFinite(_state.LastValidClose))
        {
            close = _state.LastValidClose;
        }
        else
        {
            close = 0.0;
        }

        if (double.IsFinite(high))
        {
            _state = _state with { LastValidHigh = high };
        }
        else if (double.IsFinite(_state.LastValidHigh))
        {
            high = _state.LastValidHigh;
        }
        else
        {
            high = close;
        }

        if (double.IsFinite(low))
        {
            _state = _state with { LastValidLow = low };
        }
        else if (double.IsFinite(_state.LastValidLow))
        {
            low = _state.LastValidLow;
        }
        else
        {
            low = close;
        }

        return (close, high, low);
    }

    /// <summary>
    /// Updates the BBS indicator with a new bar.
    /// </summary>
    /// <param name="input">The price bar (requires OHLC)</param>
    /// <param name="isNew">True for new bar, false for update of current bar</param>
    /// <returns>The bandwidth value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        if (isNew)
        {
            _p_state = _state;
            _p_tickCount = _tickCount;
            _p_prevSqueezeOn = _prevSqueezeOn;
        }
        else
        {
            _state = _p_state;
            _tickCount = _p_tickCount;
            _prevSqueezeOn = _p_prevSqueezeOn;
        }

        var (close, high, low) = GetValidValues(input.Close, input.High, input.Low);

        if (isNew)
        {
            _state = _state with { Bars = _state.Bars + 1 };
        }

        // === Bollinger Bands: SMA + population stddev via rolling sum/sumSq ===
        if (_bbBuffer.IsFull)
        {
            double oldest = _bbBuffer.Oldest;
            _state = _state with
            {
                BbSum = _state.BbSum - oldest,
                BbSumSq = _state.BbSumSq - (oldest * oldest)
            };
        }

        _bbBuffer.Add(close, isNew);
        _state = _state with
        {
            BbSum = _state.BbSum + close,
            BbSumSq = _state.BbSumSq + (close * close)
        };

        int bbCount = _bbBuffer.Count;
        double bbMean = bbCount > 0 ? _state.BbSum / bbCount : close;
        double bbVariance = Math.Max(0.0, (_state.BbSumSq / bbCount) - (bbMean * bbMean));
        double bbStdDev = Math.Sqrt(bbVariance);

        double bbUpper = bbMean + (_bbMult * bbStdDev);
        double bbLower = bbMean - (_bbMult * bbStdDev);

        // === Keltner Channel: SMA middle + EMA-smoothed ATR ===
        if (_kcBuffer.IsFull)
        {
            double oldest = _kcBuffer.Oldest;
            _state = _state with { KcSum = _state.KcSum - oldest };
        }

        _kcBuffer.Add(close, isNew);
        _state = _state with { KcSum = _state.KcSum + close };

        int kcCount = _kcBuffer.Count;
        double kcMid = kcCount > 0 ? _state.KcSum / kcCount : close;

        // True Range
        double tr = high - low;
        if (double.IsFinite(_state.PrevClose))
        {
            tr = Math.Max(tr, Math.Max(Math.Abs(high - _state.PrevClose), Math.Abs(low - _state.PrevClose)));
        }

        _state = _state with { PrevClose = close };

        // ATR using EMA smoothing with warmup compensation (matching Pine spec)
        double atrAlpha = 2.0 / (_kcPeriod + 1);
        double atrBeta = 1.0 - atrAlpha;

        double newAtrRaw = Math.FusedMultiplyAdd(_state.AtrRaw, atrBeta, atrAlpha * tr);
        double newAtrE = _state.AtrE * atrBeta;

        double atr;
        if (newAtrE > 1e-10)
        {
            atr = newAtrRaw / (1.0 - newAtrE);
        }
        else
        {
            atr = newAtrRaw;
        }

        _state = _state with { AtrRaw = newAtrRaw, AtrE = newAtrE };

        double kcUpper = kcMid + (_kcMult * atr);
        double kcLower = kcMid - (_kcMult * atr);

        // === Squeeze Detection ===
        bool wasSqueezeOn = _prevSqueezeOn;
        bool squeezeOn = bbUpper < kcUpper && bbLower > kcLower;
        SqueezeOn = squeezeOn;
        SqueezeFired = wasSqueezeOn && !squeezeOn;
        _prevSqueezeOn = squeezeOn;

        // === Bandwidth ===
        double bandwidth = bbMean != 0.0 ? ((bbUpper - bbLower) / bbMean) * 100.0 : 0.0; // skipcq: CS-R1077 - Exact-zero div guard: price avg

        // === Resync for floating-point drift ===
        if (isNew)
        {
            _tickCount++;
            if (_bbBuffer.IsFull && _tickCount >= ResyncInterval)
            {
                _tickCount = 0;
                RecalculateSums();
            }
        }

        // === IsHot ===
        if (!_state.IsHot && _state.Bars >= WarmupPeriod)
        {
            _state = _state with { IsHot = true };
        }

        Last = new TValue(input.Time, bandwidth);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <summary>
    /// Calculates BBS for the entire bar series.
    /// </summary>
    public TSeries Update(TBarSeries source)
    {
        if (source.Count == 0)
        {
            return new TSeries([], []);
        }

        int len = source.Count;
        var tList = new List<long>(len);
        var vList = new List<double>(len);
        CollectionsMarshal.SetCount(tList, len);
        CollectionsMarshal.SetCount(vList, len);

        var tSpan = CollectionsMarshal.AsSpan(tList);
        var vSpan = CollectionsMarshal.AsSpan(vList);

        Batch(source.HighValues, source.LowValues, source.CloseValues,
              vSpan, _bbPeriod, _bbMult);
        source.Times.CopyTo(tSpan);

        // Prime internal state for continued streaming
        Prime(source);

        return new TSeries(tList, vList);
    }

    /// <summary>
    /// Primes the indicator with historical bar data.
    /// </summary>
    public void Prime(TBarSeries source)
    {
        Reset();
        for (int i = 0; i < source.Count; i++)
        {
            Update(source[i], isNew: true);
        }
    }

    /// <summary>
    /// Calculates BBS for the entire bar series using default parameters.
    /// </summary>
    public static TSeries Batch(TBarSeries source)
    {
        var bbs = new Bbs();
        return bbs.Update(source);
    }

    /// <summary>
    /// Calculates BBS for the entire bar series using custom parameters.
    /// </summary>
    public static TSeries Batch(TBarSeries source, int bbPeriod, double bbMult, int kcPeriod, double kcMult)
    {
        var bbs = new Bbs(bbPeriod, bbMult, kcPeriod, kcMult);
        return bbs.Update(source);
    }

    /// <summary>
    /// Batch BBS calculation using spans (zero allocation hot path).
    /// Outputs bandwidth values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        Span<double> output,
        int bbPeriod = 20,
        double bbMult = 2.0)
    {
        if (bbPeriod <= 0)
        {
            throw new ArgumentException("BB Period must be greater than 0", nameof(bbPeriod));
        }

        if (bbMult <= 0)
        {
            throw new ArgumentException("BB Multiplier must be greater than 0", nameof(bbMult));
        }

        if (high.Length != low.Length || high.Length != close.Length)
        {
            throw new ArgumentException("High, Low, and Close spans must have the same length", nameof(high));
        }

        if (output.Length < high.Length)
        {
            throw new ArgumentException("Output span must be at least as long as inputs", nameof(output));
        }

        int len = high.Length;
        if (len == 0)
        {
            return;
        }

        // BB rolling state
        var bbRing = new RingBuffer(bbPeriod);
        double bbSum = 0.0;
        double bbSumSq = 0.0;

        for (int i = 0; i < len; i++)
        {
            double c = close[i];

            // === Bollinger Bands ===
            if (bbRing.IsFull)
            {
                double oldest = bbRing.Oldest;
                bbSum -= oldest;
                bbSumSq -= oldest * oldest;
            }

            bbSum += c;
            bbSumSq += c * c;
            bbRing.Add(c);

            int bbCount = bbRing.Count;
            double bbMean = bbSum / bbCount;
            double bbVariance = Math.Max(0.0, (bbSumSq / bbCount) - (bbMean * bbMean));
            double bbStdDev = Math.Sqrt(bbVariance);
            double bbUpper = bbMean + (bbMult * bbStdDev);
            double bbLower = bbMean - (bbMult * bbStdDev);

            // === Bandwidth ===
            // Note: bandwidth only depends on BB, not KC. KC state not needed for this overload.
            double bandwidth = bbMean != 0.0 ? ((bbUpper - bbLower) / bbMean) * 100.0 : 0.0;
            output[i] = bandwidth;
        }
    }

    /// <summary>
    /// Batch BBS calculation returning squeeze detection array alongside bandwidth.
    /// </summary>
    public static void Batch(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        Span<double> bandwidth,
        Span<bool> squeezeOn,
        int bbPeriod = 20,
        double bbMult = 2.0,
        int kcPeriod = 20,
        double kcMult = 1.5)
    {
        if (bbPeriod <= 0)
        {
            throw new ArgumentException("BB Period must be greater than 0", nameof(bbPeriod));
        }

        if (kcPeriod <= 0)
        {
            throw new ArgumentException("KC Period must be greater than 0", nameof(kcPeriod));
        }

        if (bbMult <= 0)
        {
            throw new ArgumentException("BB Multiplier must be greater than 0", nameof(bbMult));
        }

        if (kcMult <= 0)
        {
            throw new ArgumentException("KC Multiplier must be greater than 0", nameof(kcMult));
        }

        if (high.Length != low.Length || high.Length != close.Length)
        {
            throw new ArgumentException("High, Low, and Close spans must have the same length", nameof(high));
        }

        if (bandwidth.Length < high.Length || squeezeOn.Length < high.Length)
        {
            throw new ArgumentException("Output spans must be at least as long as inputs", nameof(bandwidth));
        }

        int len = high.Length;
        if (len == 0)
        {
            return;
        }

        // BB rolling state
        var bbRing = new RingBuffer(bbPeriod);
        double bbSum = 0.0;
        double bbSumSq = 0.0;

        // KC rolling state
        var kcRing = new RingBuffer(kcPeriod);
        double kcSum = 0.0;

        // ATR EMA state
        double atrAlpha = 2.0 / (kcPeriod + 1);
        double atrBeta = 1.0 - atrAlpha;
        double atrRaw = 0.0;
        double atrE = 1.0;
        double prevClose = close[0];

        for (int i = 0; i < len; i++)
        {
            double c = close[i];
            double h = high[i];
            double l = low[i];

            // === Bollinger Bands ===
            if (bbRing.IsFull)
            {
                double oldest = bbRing.Oldest;
                bbSum -= oldest;
                bbSumSq -= oldest * oldest;
            }

            bbSum += c;
            bbSumSq += c * c;
            bbRing.Add(c);

            int bbCount = bbRing.Count;
            double bbMean = bbSum / bbCount;
            double bbVariance = Math.Max(0.0, (bbSumSq / bbCount) - (bbMean * bbMean));
            double bbStdDev = Math.Sqrt(bbVariance);
            double bbUpper = bbMean + (bbMult * bbStdDev);
            double bbLower = bbMean - (bbMult * bbStdDev);

            // === Keltner Channel ===
            if (kcRing.IsFull)
            {
                double oldest = kcRing.Oldest;
                kcSum -= oldest;
            }

            kcSum += c;
            kcRing.Add(c);

            int kcCount = kcRing.Count;
            double kcMid = kcSum / kcCount;

            // True Range
            double tr = h - l;
            if (i > 0)
            {
                tr = Math.Max(tr, Math.Max(Math.Abs(h - prevClose), Math.Abs(l - prevClose)));
            }

            prevClose = c;

            // ATR (EMA with warmup compensation)
            atrRaw = Math.FusedMultiplyAdd(atrRaw, atrBeta, atrAlpha * tr);
            atrE *= atrBeta;
            double atr = atrE > 1e-10 ? atrRaw / (1.0 - atrE) : atrRaw;

            double kcUpper = kcMid + (kcMult * atr);
            double kcLower = kcMid - (kcMult * atr);

            // Squeeze
            squeezeOn[i] = bbUpper < kcUpper && bbLower > kcLower;

            // Bandwidth
            bandwidth[i] = bbMean != 0.0 ? ((bbUpper - bbLower) / bbMean) * 100.0 : 0.0; // skipcq: CS-R1077 - Exact-zero div guard: price avg
        }
    }

    /// <summary>
    /// Calculates BBS and returns both results and the warm indicator.
    /// </summary>
    public static (TSeries Results, Bbs Indicator) Calculate(TBarSeries source,
        int bbPeriod = 20, double bbMult = 2.0, int kcPeriod = 20, double kcMult = 1.5)
    {
        var indicator = new Bbs(bbPeriod, bbMult, kcPeriod, kcMult);
        var results = indicator.Update(source);
        return (results, indicator);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecalculateSums()
    {
        double bbSum = 0.0;
        double bbSumSq = 0.0;
        for (int i = 0; i < _bbBuffer.Count; i++)
        {
            double v = _bbBuffer[i];
            bbSum += v;
            bbSumSq += v * v;
        }

        double kcSum = 0.0;
        for (int i = 0; i < _kcBuffer.Count; i++)
        {
            kcSum += _kcBuffer[i];
        }

        _state = _state with { BbSum = bbSum, BbSumSq = bbSumSq, KcSum = kcSum };
    }

    /// <summary>
    /// Resets the indicator state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _bbBuffer.Clear();
        _kcBuffer.Clear();

        _state = new State(0, 0, 0, 0, 1.0, double.NaN, double.NaN, double.NaN, double.NaN, 0, false);
        _p_state = _state;
        _tickCount = 0;
        _p_tickCount = 0;
        _prevSqueezeOn = false;
        _p_prevSqueezeOn = false;

        Last = default;
        SqueezeOn = false;
        SqueezeFired = false;
    }
}
