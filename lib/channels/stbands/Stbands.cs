using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// STBANDS: Super Trend Bands
/// An ATR-based dynamic support/resistance channel indicator that adapts to price action.
/// Bands only move in the direction favorable to the current trend, creating trailing
/// stop-loss levels that follow price movement.
/// </summary>
/// <remarks>
/// The STBands calculation process:
/// 1. Calculate ATR over the specified period
/// 2. Basic upper band = HL2 + (multiplier × ATR)
/// 3. Basic lower band = HL2 - (multiplier × ATR)
/// 4. Final upper band: min(basic_upper, prev_upper) unless price closed above prev_upper
/// 5. Final lower band: max(basic_lower, prev_lower) unless price closed below prev_lower
/// 6. Trend: -1 (bearish) when price ≥ upper, +1 (bullish) when price ≤ lower
///
/// Key characteristics:
/// - Upper band only moves down (tightens) in downtrends
/// - Lower band only moves up (tightens) in uptrends
/// - Provides trailing stop-loss levels
/// - Trend direction signals potential reversals
///
/// Sources:
///     Olivier Seban - Original SuperTrend concept
///     https://www.tradingview.com/wiki/SuperTrend
/// </remarks>
[SkipLocalsInit]
public sealed class Stbands : AbstractBase
{
    private readonly double _multiplier;
    private readonly RingBuffer _trBuffer;
    private double _trSum;
    private int _trCount;
    private const int DefaultPeriod = 10;
    private const double DefaultMultiplier = 3.0;
    private const double MinMultiplier = 0.001;
    private const int MinPeriod = 1;

    // State for streaming with bar correction
    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double FinalUpper,
        double FinalLower,
        int Trend,
        double PrevClose,
        double TrSum,
        int TrCount,
        bool IsInitialized);

    private State _state;
    private State _p_state;
    private int _index;

    public override bool IsHot => _index >= WarmupPeriod;

    /// <summary>
    /// Upper band (resistance level)
    /// </summary>
    public TValue Upper { get; private set; }

    /// <summary>
    /// Lower band (support level)
    /// </summary>
    public TValue Lower { get; private set; }

    /// <summary>
    /// Trend direction: +1 = bullish, -1 = bearish
    /// </summary>
    public TValue Trend { get; private set; }

    /// <summary>
    /// Band width (Upper - Lower)
    /// </summary>
    public TValue Width { get; private set; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Stbands(int period = DefaultPeriod, double multiplier = DefaultMultiplier)
    {
        if (period < MinPeriod)
        {
            throw new ArgumentOutOfRangeException(nameof(period),
                $"Period must be at least {MinPeriod}.");
        }
        if (multiplier < MinMultiplier)
        {
            throw new ArgumentOutOfRangeException(nameof(multiplier),
                $"Multiplier must be at least {MinMultiplier}.");
        }

        _multiplier = multiplier;
        _trBuffer = new RingBuffer(period);
        WarmupPeriod = period;
        Name = $"Stbands({period},{multiplier:F1})";
        Init();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Init()
    {
        _index = 0;
        _trSum = 0;
        _trCount = 0;
        _state = new State(0, 0, 1, 0, 0, 0, false);
        _p_state = _state;
        _trBuffer.Clear();
        Upper = new TValue(DateTime.UtcNow, 0);
        Lower = new TValue(DateTime.UtcNow, 0);
        Trend = new TValue(DateTime.UtcNow, 1);
        Width = new TValue(DateTime.UtcNow, 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double GetFiniteValue(double value, double fallback) =>
        double.IsFinite(value) ? value : fallback;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar input, bool isNew = true)
    {
        // State management for bar correction
        if (isNew)
        {
            _p_state = _state;
            _index++;
        }
        else
        {
            // Restore previous state
            _state = _p_state;
        }

        double high = GetFiniteValue(input.High, _state.PrevClose);
        double low = GetFiniteValue(input.Low, _state.PrevClose);
        double close = GetFiniteValue(input.Close, _state.PrevClose);
        double prevClose = _state.IsInitialized ? _state.PrevClose : close;

        // Calculate True Range
        double hl = high - low;
        double hpc = Math.Abs(high - prevClose);
        double lpc = Math.Abs(low - prevClose);
        double tr = Math.Max(hl, Math.Max(hpc, lpc));

        // Add TR to buffer with bar correction support
        _trBuffer.Add(tr, isNew);

        // Calculate ATR using buffer's maintained running sum
        double atr = _trBuffer.Count > 0 ? _trBuffer.Sum / _trBuffer.Count : tr;

        // Calculate HL2
        double hl2 = (high + low) / 2.0;

        // Calculate basic bands
        double basicUpper = hl2 + (_multiplier * atr);
        double basicLower = hl2 - (_multiplier * atr);

        double finalUpper;
        double finalLower;
        int trend;

        if (!_state.IsInitialized)
        {
            // First bar initialization
            finalUpper = basicUpper;
            finalLower = basicLower;
            trend = 1;
        }
        else
        {
            double prevUpper = _state.FinalUpper;
            double prevLower = _state.FinalLower;
            int prevTrend = _state.Trend;

            // Upper band: only moves down unless price broke above
            finalUpper = (basicUpper < prevUpper || prevClose > prevUpper) ? basicUpper : prevUpper;

            // Lower band: only moves up unless price broke below
            finalLower = (basicLower > prevLower || prevClose < prevLower) ? basicLower : prevLower;

            // Determine trend
            if (close <= finalLower)
                trend = 1;  // Bullish
            else if (close >= finalUpper)
                trend = -1; // Bearish
            else
                trend = prevTrend;
        }

        // Update state
        _state = new State(finalUpper, finalLower, trend, close, _trSum, _trCount, true);

        // Update output values
        Upper = new TValue(input.Time, finalUpper);
        Lower = new TValue(input.Time, finalLower);
        Trend = new TValue(input.Time, trend);
        Width = new TValue(input.Time, finalUpper - finalLower);

        // Last returns the band corresponding to trend direction
        double result = trend > 0 ? finalLower : finalUpper;
        Last = new TValue(input.Time, result);

        return Last;
    }

    /// <summary>
    /// Updates with TValue - requires High, Low, Close data so this uses the value as Close
    /// with High = Low = Close (not recommended, use TBar overload instead)
    /// </summary>
    public override TValue Update(TValue input, bool isNew = true)
    {
        // Convert to TBar with O=H=L=C=value, V=0
        TBar bar = new(input.Time, input.Value, input.Value, input.Value, input.Value, 0);
        return Update(bar, isNew);
    }

    /// <summary>
    /// Updates the indicator with a bar series and returns the super trend series.
    /// </summary>
    public TSeries Update(TBarSeries source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        int len = source.Count;
        TSeries result = new(capacity: len);

        for (int i = 0; i < len; i++)
        {
            var bar = source[i];
            Update(bar, isNew: true);
            result.Add(Last.Time, Last.Value, isNew: true);
        }

        return result;
    }

    /// <summary>
    /// Updates the indicator with a new time series and returns the result series.
    /// </summary>
    public override TSeries Update(TSeries source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        int len = source.Count;
        TSeries result = new(capacity: len);

        for (int i = 0; i < len; i++)
        {
            var item = source[i];
            Update(item, isNew: true);
            result.Add(Last.Time, Last.Value, isNew: true);
        }

        return result;
    }

    public override void Reset()
    {
        _trBuffer.Clear();
        Init();
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        step ??= TimeSpan.FromSeconds(1);
        DateTime startTime = DateTime.UtcNow;

        for (int i = 0; i < source.Length; i++)
        {
            // Treat as close price only
            Update(new TValue(startTime + i * step.Value, source[i]), isNew: true);
        }
    }

    /// <summary>
    /// Calculates Super Trend Bands for the entire bar series.
    /// </summary>
    public static TSeries Calculate(TBarSeries source, int period = DefaultPeriod, double multiplier = DefaultMultiplier)
    {
        Stbands stbands = new(period, multiplier);
        return stbands.Update(source);
    }

    /// <summary>
    /// Calculates Super Trend Bands across OHLC data using spans.
    /// </summary>
    public static void Calculate(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        Span<double> upper,
        Span<double> lower,
        Span<double> trend,
        int period = DefaultPeriod,
        double multiplier = DefaultMultiplier)
    {
        int len = high.Length;
        if (len != low.Length || len != close.Length || len != upper.Length || len != lower.Length || len != trend.Length)
        {
            throw new ArgumentException("All spans must have the same length.", nameof(high));
        }
        if (period < MinPeriod)
        {
            throw new ArgumentOutOfRangeException(nameof(period),
                $"Period must be at least {MinPeriod}.");
        }
        if (multiplier < MinMultiplier)
        {
            throw new ArgumentOutOfRangeException(nameof(multiplier),
                $"Multiplier must be at least {MinMultiplier}.");
        }

        if (len == 0)
        {
            return;
        }

        // Use stackalloc for TR buffer if small enough
        Span<double> trBuffer = period <= 256 ? stackalloc double[period] : new double[period];
        int head = 0;
        int count = 0;
        double trSum = 0;

        double finalUpper = 0;
        double finalLower = 0;
        int currentTrend = 1;
        double prevClose = close[0];

        for (int i = 0; i < len; i++)
        {
            double h = double.IsFinite(high[i]) ? high[i] : prevClose;
            double l = double.IsFinite(low[i]) ? low[i] : prevClose;
            double c = double.IsFinite(close[i]) ? close[i] : prevClose;

            // Calculate True Range
            double hl = h - l;
            double hpc = i > 0 ? Math.Abs(h - prevClose) : 0;
            double lpc = i > 0 ? Math.Abs(l - prevClose) : 0;
            double tr = i > 0 ? Math.Max(hl, Math.Max(hpc, lpc)) : hl;

            // Update running sum with ring buffer
            if (count == period)
            {
                trSum -= trBuffer[head];
                count--;
            }
            trSum += tr;
            count++;
            trBuffer[head] = tr;
            head = (head + 1) % period;

            // Calculate ATR
            double atr = count > 0 ? trSum / count : tr;

            // Calculate HL2 and basic bands
            double hl2 = (h + l) / 2.0;
            double basicUpper = hl2 + (multiplier * atr);
            double basicLower = hl2 - (multiplier * atr);

            if (i == 0)
            {
                finalUpper = basicUpper;
                finalLower = basicLower;
                currentTrend = 1;
            }
            else
            {
                // Upper band: only moves down unless price broke above
                finalUpper = (basicUpper < finalUpper || prevClose > finalUpper) ? basicUpper : finalUpper;

                // Lower band: only moves up unless price broke below
                finalLower = (basicLower > finalLower || prevClose < finalLower) ? basicLower : finalLower;

                // Determine trend
                if (c <= finalLower)
                    currentTrend = 1;
                else if (c >= finalUpper)
                    currentTrend = -1;
            }

            upper[i] = finalUpper;
            lower[i] = finalLower;
            trend[i] = currentTrend;
            prevClose = c;
        }
    }
}