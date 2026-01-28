using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// VWAPSD: Volume Weighted Average Price with Standard Deviation Bands
/// A volatility channel indicator using VWAP as the center line with configurable
/// standard deviation bands.
/// </summary>
/// <remarks>
/// The VWAPSD calculation process:
/// 1. Calculate cumulative price×volume sum (sum_pv)
/// 2. Calculate cumulative volume sum (sum_vol)
/// 3. Calculate cumulative price²×volume sum (sum_pv2)
/// 4. VWAP = sum_pv / sum_vol
/// 5. Variance = (sum_pv2 / sum_vol) - VWAP²
/// 6. StdDev = √Variance
/// 7. Bands = VWAP ± (numDevs × StdDev)
///
/// Key characteristics:
/// - Volume-weighted price average as center line
/// - Configurable number of standard deviations for bands
/// - Bands adapt to volume-weighted price dispersion
/// - Can reset on session boundaries or run continuously
///
/// Sources:
///     Standard VWAP calculation with configurable deviation bands
///     Common in institutional trading for intraday analysis
/// </remarks>
[SkipLocalsInit]
public sealed class Vwapsd : AbstractBase
{
    private readonly double _numDevs;
    private const double DefaultNumDevs = 2.0;
    private const double MinNumDevs = 0.1;
    private const double MaxNumDevs = 5.0;

    // State for streaming with bar correction
    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double SumPV,        // Cumulative price × volume
        double SumVol,       // Cumulative volume
        double SumPV2,       // Cumulative price² × volume
        int Count,           // Bar count since reset
        double LastValidPrice,
        double LastValidVolume,
        bool IsInitialized);

    private State _state;
    private State _p_state;
    private int _index;

    public override bool IsHot => _index >= WarmupPeriod;

    /// <summary>
    /// Upper band (VWAP + numDevs × StdDev)
    /// </summary>
    public TValue Upper { get; private set; }

    /// <summary>
    /// Lower band (VWAP - numDevs × StdDev)
    /// </summary>
    public TValue Lower { get; private set; }

    /// <summary>
    /// VWAP value (center line)
    /// </summary>
    public TValue Vwap { get; private set; }

    /// <summary>
    /// Standard deviation of volume-weighted prices
    /// </summary>
    public TValue StdDev { get; private set; }

    /// <summary>
    /// Band width (Upper - Lower = 2 × numDevs × StdDev)
    /// </summary>
    public TValue Width { get; private set; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vwapsd(double numDevs = DefaultNumDevs)
    {
        if (numDevs < MinNumDevs)
        {
            throw new ArgumentOutOfRangeException(nameof(numDevs),
                $"Number of deviations must be at least {MinNumDevs}.");
        }
        if (numDevs > MaxNumDevs)
        {
            throw new ArgumentOutOfRangeException(nameof(numDevs),
                $"Number of deviations must not exceed {MaxNumDevs}.");
        }

        _numDevs = numDevs;
        WarmupPeriod = 2; // Need at least 2 bars for variance
        Name = $"Vwapsd({numDevs:F1})";
        Init();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Init()
    {
        _index = 0;
        _state = new State(0, 0, 0, 0, double.NaN, double.NaN, false);
        _p_state = _state;
        Vwap = new TValue(DateTime.UtcNow, 0);
        Upper = new TValue(DateTime.UtcNow, 0);
        Lower = new TValue(DateTime.UtcNow, 0);
        StdDev = new TValue(DateTime.UtcNow, 0);
        Width = new TValue(DateTime.UtcNow, 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double GetFiniteValue(double value, ref double lastValid)
    {
        if (double.IsFinite(value))
        {
            lastValid = value;
            return value;
        }
        return double.IsFinite(lastValid) ? lastValid : 0;
    }

    /// <summary>
    /// Updates the indicator with a new bar. Uses HLC3 as price and bar volume.
    /// </summary>
    /// <param name="bar">The input bar with OHLCV data</param>
    /// <param name="isNew">True for new bar, false for bar correction</param>
    /// <param name="reset">True to reset VWAP calculation (e.g., new session)</param>
    /// <returns>The VWAP value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar bar, bool isNew = true, bool reset = false)
    {
        return Update(new TValue(bar.Time, bar.HLC3), bar.Volume, isNew, reset);
    }

    /// <summary>
    /// Updates the indicator with price and volume values.
    /// </summary>
    /// <param name="input">Price value (typically HLC3)</param>
    /// <param name="volume">Volume value</param>
    /// <param name="isNew">True for new bar, false for bar correction</param>
    /// <param name="reset">True to reset VWAP calculation (e.g., new session)</param>
    /// <returns>The VWAP value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue input, double volume, bool isNew = true, bool reset = false)
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

        double lastValidPrice = _state.LastValidPrice;
        double lastValidVolume = _state.LastValidVolume;
        double price = GetFiniteValue(input.Value, ref lastValidPrice);
        double vol = GetFiniteValue(volume, ref lastValidVolume);
        _state = _state with { LastValidPrice = lastValidPrice, LastValidVolume = lastValidVolume };

        // Handle reset
        if (reset || !_state.IsInitialized)
        {
            // Reset warmup tracking for proper IsHot gating after session reset
            _index = 1;

            if (vol > 0)
            {
                _state = _state with
                {
                    SumPV = price * vol,
                    SumVol = vol,
                    SumPV2 = price * price * vol,
                    Count = 1,
                    IsInitialized = true
                };
            }
            else
            {
                _state = _state with
                {
                    SumPV = 0,
                    SumVol = 0,
                    SumPV2 = 0,
                    Count = 0,
                    IsInitialized = true
                };
            }
        }
        else
        {
            // Accumulate values
            if (vol > 0)
            {
                _state = _state with
                {
                    SumPV = _state.SumPV + price * vol,
                    SumVol = _state.SumVol + vol,
                    SumPV2 = _state.SumPV2 + price * price * vol,
                    Count = _state.Count + 1
                };
            }
        }

        // Calculate VWAP
        double vwap = _state.SumVol > 0 ? _state.SumPV / _state.SumVol : price;

        // Calculate variance and standard deviation
        double variance = 0;
        if (_state.SumVol > 0 && _state.Count > 1)
        {
            double meanP2 = _state.SumPV2 / _state.SumVol;
            double vwapSquared = vwap * vwap;
            variance = Math.Max(0, meanP2 - vwapSquared);
        }
        double stdev = Math.Sqrt(variance);

        // Calculate bands
        double upper = vwap + _numDevs * stdev;
        double lower = vwap - _numDevs * stdev;

        // Update output values
        Vwap = new TValue(input.Time, vwap);
        Upper = new TValue(input.Time, upper);
        Lower = new TValue(input.Time, lower);
        StdDev = new TValue(input.Time, stdev);
        Width = new TValue(input.Time, upper - lower);

        Last = Vwap;
        return Last;
    }

    /// <summary>
    /// Updates the indicator with a TValue. Assumes volume of 1.0 for each update.
    /// For proper VWAP calculation, use Update(TBar) or Update(TValue, double volume).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        return Update(input, 1.0, isNew, false);
    }

    /// <summary>
    /// Updates the indicator with a bar series.
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
            Update(source[i], isNew: true);
            result.Add(Last.Time, Last.Value, isNew: true);
        }

        return result;
    }

    /// <summary>
    /// Updates the indicator with a price series (uses volume=1 for each bar).
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
            Update(source[i], isNew: true);
            result.Add(Last.Time, Last.Value, isNew: true);
        }

        return result;
    }

    public override void Reset()
    {
        Init();
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        step ??= TimeSpan.FromSeconds(1);
        DateTime startTime = DateTime.UtcNow;

        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(startTime + i * step.Value, source[i]), 1.0, isNew: true, reset: false);
        }
    }

    /// <summary>
    /// Calculates VWAP SD Bands for a bar series.
    /// </summary>
    /// <returns>Tuple of (Upper, Lower, Vwap, StdDev)</returns>
    public static (TSeries Upper, TSeries Lower, TSeries Vwap, TSeries StdDev) Calculate(
        TBarSeries source,
        double numDevs = DefaultNumDevs)
    {
        Vwapsd vwapsd = new(numDevs);
        int len = source.Count;

        TSeries upper = new(capacity: len);
        TSeries lower = new(capacity: len);
        TSeries vwap = new(capacity: len);
        TSeries stdev = new(capacity: len);

        for (int i = 0; i < len; i++)
        {
            vwapsd.Update(source[i], isNew: true);
            upper.Add(vwapsd.Upper.Time, vwapsd.Upper.Value, isNew: true);
            lower.Add(vwapsd.Lower.Time, vwapsd.Lower.Value, isNew: true);
            vwap.Add(vwapsd.Vwap.Time, vwapsd.Vwap.Value, isNew: true);
            stdev.Add(vwapsd.StdDev.Time, vwapsd.StdDev.Value, isNew: true);
        }

        return (upper, lower, vwap, stdev);
    }

    /// <summary>
    /// Calculates VWAP SD Bands using span arrays.
    /// </summary>
    /// <param name="price">Source price values (typically HLC3)</param>
    /// <param name="volume">Volume values</param>
    /// <param name="upper">Output span for upper band</param>
    /// <param name="lower">Output span for lower band</param>
    /// <param name="vwap">Output span for VWAP values</param>
    /// <param name="stdDev">Output span for standard deviation values</param>
    /// <param name="numDevs">Number of standard deviations for bands (default 2.0)</param>
    public static void Calculate(
        ReadOnlySpan<double> price,
        ReadOnlySpan<double> volume,
        Span<double> upper,
        Span<double> lower,
        Span<double> vwap,
        Span<double> stdDev,
        double numDevs = DefaultNumDevs)
    {
        int len = price.Length;
        if (len != volume.Length || len != upper.Length || len != lower.Length || len != vwap.Length || len != stdDev.Length)
        {
            throw new ArgumentException("All spans must have the same length.", nameof(price));
        }
        if (numDevs < MinNumDevs)
        {
            throw new ArgumentOutOfRangeException(nameof(numDevs),
                $"Number of deviations must be at least {MinNumDevs}.");
        }
        if (numDevs > MaxNumDevs)
        {
            throw new ArgumentOutOfRangeException(nameof(numDevs),
                $"Number of deviations must not exceed {MaxNumDevs}.");
        }

        if (len == 0)
        {
            return;
        }

        double sumPV = 0, sumVol = 0, sumPV2 = 0;
        int count = 0;
        double lastValidPrice = double.NaN;
        double lastValidVolume = double.NaN;

        for (int i = 0; i < len; i++)
        {
            double p = GetFiniteValue(price[i], ref lastValidPrice);
            double v = GetFiniteValue(volume[i], ref lastValidVolume);

            if (v > 0)
            {
                sumPV += p * v;
                sumVol += v;
                sumPV2 += p * p * v;
                count++;
            }

            double vwapVal = sumVol > 0 ? sumPV / sumVol : p;

            double variance = 0;
            if (sumVol > 0 && count > 1)
            {
                double meanP2 = sumPV2 / sumVol;
                double vwapSquared = vwapVal * vwapVal;
                variance = Math.Max(0, meanP2 - vwapSquared);
            }
            double stdev = Math.Sqrt(variance);

            vwap[i] = vwapVal;
            stdDev[i] = stdev;
            upper[i] = vwapVal + numDevs * stdev;
            lower[i] = vwapVal - numDevs * stdev;
        }
    }
}
