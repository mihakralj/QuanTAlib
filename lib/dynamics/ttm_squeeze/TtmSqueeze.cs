// TTM_SQUEEZE: TTM Squeeze by John Carter
// Volatility compression indicator using Bollinger Bands and Keltner Channel
// Identifies low-volatility "squeeze" conditions that precede explosive moves

using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// TTM Squeeze: John Carter's Volatility Breakout Indicator
/// </summary>
/// <remarks>
/// Combines Bollinger Bands and Keltner Channels to identify periods of low volatility
/// (squeeze) that typically precede explosive price moves. Also calculates a momentum
/// histogram using linear regression.
///
/// Squeeze Detection:
/// - Squeeze On: Bollinger Bands inside Keltner Channel (low volatility)
/// - Squeeze Off: Bollinger Bands outside Keltner Channel (volatility expansion)
/// - Squeeze Fired: First bar where squeeze transitions from On to Off
///
/// Momentum Calculation:
/// momentum = LinReg(close - donchianMidline, period)
/// where donchianMidline = (Highest(period) + Lowest(period)) / 2
///
/// Color Coding:
/// - Cyan: Momentum rising above zero (strong bullish)
/// - Blue: Momentum falling but above zero (weakening bullish)
/// - Red: Momentum falling below zero (strong bearish)
/// - Yellow: Momentum rising but below zero (weakening bearish)
///
/// Sources:
/// - John Carter's "Mastering the Trade" (2005)
/// - thinkorswim TTM Squeeze implementation
/// </remarks>
[SkipLocalsInit]
public sealed class TtmSqueeze : ITValuePublisher
{
    private readonly int _bbPeriod;
    private readonly double _bbMult;
    private readonly int _kcPeriod;
    private readonly double _kcMult;
    private readonly int _momPeriod;

    // Bollinger Bands components
    private readonly RingBuffer _priceBuffer;
    private double _priceSum;
    private double _priceSumSquares;

    // Keltner Channel components (EMA + ATR)
    private double _ema;
    private double _emaWeight;
    private double _atrRma;
    private double _atrE;
    private double _prevClose;

    // Donchian Channel for momentum (Highest/Lowest)
    private readonly RingBuffer _highBuffer;
    private readonly RingBuffer _lowBuffer;

    // Linear Regression for momentum
    private readonly RingBuffer _momentumBuffer;
    private double _momentumSumY;
    private double _momentumSumXY;

    // Precomputed linear regression constants
    private readonly double _sumX;
    private readonly double _denominator;

    // State tracking
    private double _prevMomentum;
    private bool _prevSqueezeOn;
    private int _barCount;

    // NaN handling
    private double _lastValidClose;
    private double _lastValidHigh;
    private double _lastValidLow;

    // Saved state for bar corrections
    private double _saved_priceSum;
    private double _saved_priceSumSquares;
    private double _saved_ema;
    private double _saved_emaWeight;
    private double _saved_atrRma;
    private double _saved_atrE;
    private double _saved_prevClose;
    private double _saved_momentumSumY;
    private double _saved_momentumSumXY;
    private double _saved_prevMomentum;
    private bool _saved_prevSqueezeOn;
    private int _saved_barCount;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Event publisher for value updates.
    /// </summary>
    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// The momentum value (linear regression of price - donchian midline).
    /// </summary>
    public TValue Momentum { get; private set; }

    /// <summary>
    /// Primary output - same as Momentum.
    /// </summary>
    public TValue Last => Momentum;

    /// <summary>
    /// True when Bollinger Bands are inside Keltner Channel (squeeze condition).
    /// </summary>
    public bool SqueezeOn { get; private set; }

    /// <summary>
    /// True when squeeze just ended (first bar where squeeze transitions Off).
    /// </summary>
    public bool SqueezeFired { get; private set; }

    /// <summary>
    /// True when momentum is above zero.
    /// </summary>
    public bool MomentumPositive { get; private set; }

    /// <summary>
    /// True when momentum is rising (current > previous).
    /// </summary>
    public bool MomentumRising { get; private set; }

    /// <summary>
    /// Color indicator: 0=Cyan (rising above 0), 1=Blue (falling above 0),
    /// 2=Red (falling below 0), 3=Yellow (rising below 0)
    /// </summary>
    public int ColorCode { get; private set; }

    /// <summary>
    /// True when indicator has enough data for valid output.
    /// </summary>
    public bool IsHot => _barCount >= WarmupPeriod;

    /// <summary>
    /// Number of bars required for warmup.
    /// </summary>
    public int WarmupPeriod { get; }

    /// <summary>
    /// Bollinger Band period.
    /// </summary>
    public int BbPeriod => _bbPeriod;

    /// <summary>
    /// Keltner Channel period.
    /// </summary>
    public int KcPeriod => _kcPeriod;

    /// <summary>
    /// Momentum period.
    /// </summary>
    public int MomPeriod => _momPeriod;

    /// <summary>
    /// Creates TTM Squeeze indicator with specified parameters.
    /// </summary>
    /// <param name="bbPeriod">Bollinger Band period (default 20)</param>
    /// <param name="bbMult">Bollinger Band standard deviation multiplier (default 2.0)</param>
    /// <param name="kcPeriod">Keltner Channel period (default 20)</param>
    /// <param name="kcMult">Keltner Channel ATR multiplier (default 1.5)</param>
    /// <param name="momPeriod">Momentum linear regression period (default 20)</param>
    public TtmSqueeze(int bbPeriod = 20, double bbMult = 2.0, int kcPeriod = 20, double kcMult = 1.5, int momPeriod = 20)
    {
        if (bbPeriod < 2)
        {
            throw new ArgumentException("BB Period must be at least 2", nameof(bbPeriod));
        }
        if (kcPeriod < 1)
        {
            throw new ArgumentException("KC Period must be at least 1", nameof(kcPeriod));
        }
        if (momPeriod < 2)
        {
            throw new ArgumentException("Momentum Period must be at least 2", nameof(momPeriod));
        }
        if (bbMult <= 0)
        {
            throw new ArgumentException("BB Multiplier must be positive", nameof(bbMult));
        }
        if (kcMult <= 0)
        {
            throw new ArgumentException("KC Multiplier must be positive", nameof(kcMult));
        }

        _bbPeriod = bbPeriod;
        _bbMult = bbMult;
        _kcPeriod = kcPeriod;
        _kcMult = kcMult;
        _momPeriod = momPeriod;

        Name = $"TtmSqueeze({bbPeriod},{bbMult:F1},{kcPeriod},{kcMult:F1},{momPeriod})";
        WarmupPeriod = Math.Max(Math.Max(bbPeriod, kcPeriod), momPeriod);

        // Initialize buffers
        _priceBuffer = new RingBuffer(bbPeriod);
        _highBuffer = new RingBuffer(momPeriod);
        _lowBuffer = new RingBuffer(momPeriod);
        _momentumBuffer = new RingBuffer(momPeriod);

        // Precompute linear regression constants
        _sumX = 0.5 * momPeriod * (momPeriod - 1);
        double sumX2 = (momPeriod - 1.0) * momPeriod * ((2.0 * momPeriod) - 1.0) / 6.0;
        _denominator = (momPeriod * sumX2) - (_sumX * _sumX);

        Reset();
    }

    /// <summary>
    /// Resets the indicator state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _priceBuffer.Clear();
        _highBuffer.Clear();
        _lowBuffer.Clear();
        _momentumBuffer.Clear();

        _priceSum = 0;
        _priceSumSquares = 0;
        _ema = 0;
        _emaWeight = 0;
        _atrRma = 0;
        _atrE = 1.0;
        _prevClose = double.NaN;
        _momentumSumY = 0;
        _momentumSumXY = 0;
        _prevMomentum = 0;
        _prevSqueezeOn = false;
        _barCount = 0;

        _lastValidClose = double.NaN;
        _lastValidHigh = double.NaN;
        _lastValidLow = double.NaN;

        Momentum = default;
        SqueezeOn = false;
        SqueezeFired = false;
        MomentumPositive = false;
        MomentumRising = false;
        ColorCode = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PubEvent(TValue value, bool isNew = true) =>
        Pub?.Invoke(this, new TValueEventArgs { Value = value, IsNew = isNew });

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private (double close, double high, double low) GetValidValues(double close, double high, double low)
    {
        if (double.IsFinite(close))
        {
            _lastValidClose = close;
        }
        else
        {
            close = double.IsFinite(_lastValidClose) ? _lastValidClose : 0;
        }

        if (double.IsFinite(high))
        {
            _lastValidHigh = high;
        }
        else
        {
            high = double.IsFinite(_lastValidHigh) ? _lastValidHigh : close;
        }

        if (double.IsFinite(low))
        {
            _lastValidLow = low;
        }
        else
        {
            low = double.IsFinite(_lastValidLow) ? _lastValidLow : close;
        }

        return (close, high, low);
    }

    /// <summary>
    /// Updates the TTM Squeeze indicator with a new bar.
    /// </summary>
    /// <param name="input">The price bar (requires OHLC)</param>
    /// <param name="isNew">True for new bar, false for update of current bar</param>
    /// <returns>The momentum value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        if (isNew)
        {
            SaveState();
        }
        else
        {
            RestoreState();
        }

        var (close, high, low) = GetValidValues(input.Close, input.High, input.Low);

        if (isNew)
        {
            _barCount++;
        }

        // === Bollinger Bands Calculation ===
        // Update price buffer and running sums
        if (_priceBuffer.IsFull)
        {
            double oldest = _priceBuffer[0];
            _priceSum -= oldest;
            _priceSumSquares -= oldest * oldest;
        }
        _priceBuffer.Add(close);
        _priceSum += close;
        _priceSumSquares += close * close;

        double bbCount = Math.Min(_barCount, _bbPeriod);
        double bbMean = bbCount > 0 ? _priceSum / bbCount : close;
        double bbVariance = bbCount > 1 ? (_priceSumSquares - (_priceSum * _priceSum / bbCount)) / bbCount : 0;
        double bbStdDev = Math.Sqrt(Math.Max(0, bbVariance));

        double bbUpper = bbMean + (_bbMult * bbStdDev);
        double bbLower = bbMean - (_bbMult * bbStdDev);

        // === Keltner Channel Calculation ===
        // EMA with warmup compensation
        double emaAlpha = 2.0 / (_kcPeriod + 1);
        _emaWeight = Math.FusedMultiplyAdd(_emaWeight, 1 - emaAlpha, emaAlpha);
        _ema = Math.FusedMultiplyAdd(_ema, 1 - emaAlpha, emaAlpha * close);
        double kcMid = _emaWeight > 0 ? _ema / _emaWeight : close;

        // ATR using RMA (Wilder's smoothing) with warmup compensation
        double tr = high - low;
        if (double.IsFinite(_prevClose))
        {
            tr = Math.Max(tr, Math.Max(Math.Abs(high - _prevClose), Math.Abs(low - _prevClose)));
        }
        _prevClose = close;

        double atrAlpha = 1.0 / _kcPeriod;
        _atrRma = Math.FusedMultiplyAdd(_atrRma, 1 - atrAlpha, atrAlpha * tr);
        _atrE = Math.FusedMultiplyAdd(_atrE, 1 - atrAlpha, 0);
        double atr = _atrE < 1.0 ? _atrRma / (1.0 - _atrE) : _atrRma;

        double kcUpper = kcMid + (_kcMult * atr);
        double kcLower = kcMid - (_kcMult * atr);

        // === Squeeze Detection ===
        bool wasSqueezeOn = _prevSqueezeOn;
        bool squeezeOn = bbUpper < kcUpper && bbLower > kcLower;
        SqueezeOn = squeezeOn;
        SqueezeFired = wasSqueezeOn && !squeezeOn;
        _prevSqueezeOn = squeezeOn;

        // === Donchian Midline ===
        _highBuffer.Add(high);
        _lowBuffer.Add(low);

        double donchianHigh = _highBuffer.Max();
        double donchianLow = _lowBuffer.Min();
        double donchianMid = (donchianHigh + donchianLow) / 2;

        // === Momentum (Linear Regression) ===
        double deviation = close - donchianMid;

        // Update momentum buffer and sums
        if (_momentumBuffer.IsFull)
        {
            double oldest = _momentumBuffer[0];
            double prevSumY = _momentumSumY;
            _momentumSumXY = _momentumSumXY + prevSumY - (_momPeriod * oldest);
            _momentumSumY -= oldest;
        }
        _momentumBuffer.Add(deviation);
        _momentumSumY += deviation;

        // Recalculate sumXY during warmup (non-O(1), but short duration)
        int momCount = Math.Min(_barCount, _momPeriod);
        if (!_momentumBuffer.IsFull)
        {
            _momentumSumXY = 0;
            var span = _momentumBuffer.GetSpan();
            for (int i = 0; i < span.Length; i++)
            {
                _momentumSumXY += i * span[i];
            }
        }

        double momentum;
        if (momCount < 2 || Math.Abs(_denominator) < 1e-10)
        {
            momentum = deviation;
        }
        else
        {
            double n = momCount;
            double sx, denom;

            if (momCount < _momPeriod)
            {
                sx = 0.5 * n * (n - 1);
                double sx2 = (n - 1.0) * n * ((2.0 * n) - 1.0) / 6.0;
                denom = (n * sx2) - (sx * sx);
            }
            else
            {
                sx = _sumX;
                denom = _denominator;
            }

            if (Math.Abs(denom) < 1e-10)
            {
                momentum = _momentumSumY / n;
            }
            else
            {
                double slope = ((n * _momentumSumXY) - (sx * _momentumSumY)) / denom;
                double intercept = (_momentumSumY - (slope * sx)) / n;
                // Regression value at current point (x = count - 1)
                momentum = Math.FusedMultiplyAdd(slope, n - 1, intercept);
            }
        }

        // === Momentum Direction ===
        double prevMom = _prevMomentum;
        MomentumPositive = momentum > 0;
        MomentumRising = momentum > prevMom;
        _prevMomentum = momentum;

        // === Color Coding ===
        // 0=Cyan (rising above 0), 1=Blue (falling above 0), 2=Red (falling below 0), 3=Yellow (rising below 0)
        if (MomentumPositive)
        {
            ColorCode = MomentumRising ? 0 : 1; // Cyan : Blue
        }
        else
        {
            ColorCode = MomentumRising ? 3 : 2; // Yellow : Red
        }

        Momentum = new TValue(input.Time, momentum);
        PubEvent(Momentum, isNew);
        return Momentum;
    }

    /// <summary>
    /// Calculates TTM Squeeze for the entire bar series.
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

        for (int i = 0; i < len; i++)
        {
            var bar = source[i];
            Update(bar, isNew: true);
            tList.Add(bar.Time);
            vList.Add(Momentum.Value);
        }

        return new TSeries(tList, vList);
    }

    /// <summary>
    /// Primes the indicator with historical bar data.
    /// </summary>
    public void Prime(TBarSeries source)
    {
        for (int i = 0; i < source.Count; i++)
        {
            Update(source[i], isNew: true);
        }
    }

    /// <summary>
    /// Calculates TTM Squeeze for the entire bar series using default parameters.
    /// </summary>
    public static TSeries Batch(TBarSeries source)
    {
        var squeeze = new TtmSqueeze();
        return squeeze.Update(source);
    }

    /// <summary>
    /// Calculates TTM Squeeze for the entire bar series using custom parameters.
    /// </summary>
    public static TSeries Batch(TBarSeries source, int bbPeriod, double bbMult, int kcPeriod, double kcMult, int momPeriod)
    {
        var squeeze = new TtmSqueeze(bbPeriod, bbMult, kcPeriod, kcMult, momPeriod);
        return squeeze.Update(source);
    }

    /// <summary>
    /// Calculates TTM Squeeze and returns both results and the warm indicator.
    /// </summary>
    public static (TSeries Results, TtmSqueeze Indicator) Calculate(TBarSeries source,
        int bbPeriod = 20, double bbMult = 2.0, int kcPeriod = 20, double kcMult = 1.5, int momPeriod = 20)
    {
        var squeeze = new TtmSqueeze(bbPeriod, bbMult, kcPeriod, kcMult, momPeriod);
        var results = squeeze.Update(source);
        return (results, squeeze);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SaveState()
    {
        _saved_priceSum = _priceSum;
        _saved_priceSumSquares = _priceSumSquares;
        _saved_ema = _ema;
        _saved_emaWeight = _emaWeight;
        _saved_atrRma = _atrRma;
        _saved_atrE = _atrE;
        _saved_prevClose = _prevClose;
        _saved_momentumSumY = _momentumSumY;
        _saved_momentumSumXY = _momentumSumXY;
        _saved_prevMomentum = _prevMomentum;
        _saved_prevSqueezeOn = _prevSqueezeOn;
        _saved_barCount = _barCount;
        _priceBuffer.Snapshot();
        _highBuffer.Snapshot();
        _lowBuffer.Snapshot();
        _momentumBuffer.Snapshot();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RestoreState()
    {
        _priceSum = _saved_priceSum;
        _priceSumSquares = _saved_priceSumSquares;
        _ema = _saved_ema;
        _emaWeight = _saved_emaWeight;
        _atrRma = _saved_atrRma;
        _atrE = _saved_atrE;
        _prevClose = _saved_prevClose;
        _momentumSumY = _saved_momentumSumY;
        _momentumSumXY = _saved_momentumSumXY;
        _prevMomentum = _saved_prevMomentum;
        _prevSqueezeOn = _saved_prevSqueezeOn;
        _barCount = _saved_barCount;
        _priceBuffer.Restore();
        _highBuffer.Restore();
        _lowBuffer.Restore();
        _momentumBuffer.Restore();
    }
}
