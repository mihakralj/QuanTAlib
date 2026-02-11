// TTM_TREND: John Carter's TTM Trend Indicator
// Color-coded EMA for visual trend identification
// Uses 6-period EMA of HLC/3 (typical price) by default

using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// TTM_TREND: John Carter's TTM Trend Indicator
/// A fast EMA-based trend indicator with color-coded direction and strength measurement.
///
/// Calculation: EMA(source, period) with trend = sign(EMA - prevEMA)
/// </summary>
/// <remarks>
/// <b>Calculation:</b>
/// <code>
/// alpha = 2 / (period + 1)
/// EMA = alpha * source + (1 - alpha) * prevEMA
/// trend = sign(EMA - prevEMA)
/// strength = |EMA - prevEMA| / prevEMA * 100
/// </code>
///
/// <b>Key characteristics:</b>
/// - O(1) update complexity per bar
/// - Uses EMA for smooth, responsive trend following
/// - Trend direction: +1 (bullish), -1 (bearish), 0 (neutral)
/// - Strength measures percent change between EMA values
/// - Default period of 6 for fast trend detection
/// </remarks>
/// <seealso href="TtmTrend.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class TtmTrend : ITValuePublisher
{
    private const int DefaultPeriod = 6;

    private readonly int _period;
    private readonly double _alpha;

    // Current state
    private double _ema;
    private double _prevEma;
    private int _sampleCount;

    // Saved state for bar correction
    private double _p_ema;
    private double _p_prevEma;
    private int _p_sampleCount;

    /// <summary>
    /// Display name for the indicator.
    /// </summary>
    public string Name { get; }

    public event TValuePublishedHandler? Pub;

    /// <summary>
    /// Current TTM Trend EMA value.
    /// </summary>
    public TValue Last { get; private set; }

    /// <summary>
    /// Current trend direction: +1 (bullish), -1 (bearish), 0 (neutral).
    /// </summary>
    public int Trend { get; private set; }

    /// <summary>
    /// Current trend strength as percent change between EMA values.
    /// </summary>
    public double Strength { get; private set; }

    /// <summary>
    /// True when the indicator has calculated a valid value (after 2 bars).
    /// </summary>
    public bool IsHot => _sampleCount > 1;

    /// <summary>
    /// The lookback period parameter.
    /// </summary>
    public int Period => _period;

    /// <summary>
    /// The number of bars required for the indicator to warm up.
    /// </summary>
    public static int WarmupPeriod => 2;

    /// <summary>
    /// Creates a TTM Trend indicator with specified period.
    /// </summary>
    /// <param name="period">Lookback period for EMA (must be >= 1, default 6)</param>
    public TtmTrend(int period = DefaultPeriod)
    {
        if (period < 1)
        {
            throw new ArgumentException("Period must be at least 1", nameof(period));
        }

        _period = period;
        _alpha = 2.0 / (period + 1);
        Name = $"TTM_TREND({period})";
    }

    /// <summary>
    /// Resets the indicator state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _ema = 0;
        _prevEma = 0;
        _sampleCount = 0;
        _p_ema = 0;
        _p_prevEma = 0;
        _p_sampleCount = 0;
        Trend = 0;
        Strength = 0;
        Last = default;
    }

    /// <summary>
    /// Updates the TTM Trend indicator with a new value.
    /// </summary>
    /// <param name="input">Input value (typically HLC/3)</param>
    /// <param name="isNew">True for new bar, false for update of current bar</param>
    /// <returns>The current TTM Trend EMA value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, bool isNew = true)
    {
        double value = input.Value;

        // Handle NaN/Infinity inputs
        if (!double.IsFinite(value))
        {
            Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
            return Last;
        }

        // State management for bar correction
        if (isNew)
        {
            _p_ema = _ema;
            _p_prevEma = _prevEma;
            _p_sampleCount = _sampleCount;
        }
        else
        {
            _ema = _p_ema;
            _prevEma = _p_prevEma;
            _sampleCount = _p_sampleCount;
        }

        // EMA calculation
        if (_sampleCount == 0)
        {
            _ema = value;
            _prevEma = value;
        }
        else
        {
            _prevEma = _ema;
            _ema = Math.FusedMultiplyAdd(_alpha, value - _ema, _ema);
        }

        if (isNew)
        {
            _sampleCount++;
        }

        // Calculate trend and strength
        double diff = _ema - _prevEma;
        Trend = Math.Sign(diff);
        Strength = _prevEma > 1e-10 ? Math.Abs(diff) / _prevEma * 100.0 : 0.0;

        Last = new TValue(input.Time, _ema);
        Pub?.Invoke(this, new TValueEventArgs { Value = Last, IsNew = isNew });
        return Last;
    }

    /// <summary>
    /// Updates the TTM Trend indicator with a bar using typical price (HLC/3).
    /// </summary>
    /// <param name="bar">The price bar</param>
    /// <param name="isNew">True for new bar, false for update of current bar</param>
    /// <returns>The current TTM Trend EMA value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar bar, bool isNew = true)
    {
        double typical = (bar.High + bar.Low + bar.Close) / 3.0;
        return Update(new TValue(bar.Time, typical), isNew);
    }

    /// <summary>
    /// Updates with a value series.
    /// </summary>
    public TSeries Update(TSeries source)
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
            var result = Update(source[i], isNew: true);
            tList.Add(source.Times[i]);
            vList.Add(result.Value);
        }

        return new TSeries(tList, vList);
    }

    /// <summary>
    /// Updates with a bar series.
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

        var times = source.Open.Times;
        for (int i = 0; i < len; i++)
        {
            var result = Update(source[i], isNew: true);
            tList.Add(times[i]);
            vList.Add(result.Value);
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
    /// Creates and returns results for a bar series.
    /// </summary>
    public static TSeries Batch(TBarSeries source, int period = DefaultPeriod)
    {
        var indicator = new TtmTrend(period);
        return indicator.Update(source);
    }

    /// <summary>
    /// Returns the indicator and its results.
    /// </summary>
    public static (TSeries Results, TtmTrend Indicator) Calculate(TBarSeries source, int period = DefaultPeriod)
    {
        var indicator = new TtmTrend(period);
        var results = indicator.Update(source);
        return (results, indicator);
    }
}
