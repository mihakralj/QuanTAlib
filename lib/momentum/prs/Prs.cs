// PRS: Price Relative Strength
// Compares the performance of one asset to another by calculating the ratio
// and optionally applying EMA smoothing for trend identification.

using System.Runtime.CompilerServices;
using static System.Math;

namespace QuanTAlib;

/// <summary>
/// PRS: Price Relative Strength
/// </summary>
/// <remarks>
/// Measures relative performance between two assets by calculating their price ratio.
/// A rising PRS indicates the base asset is outperforming the comparison asset.
/// A falling PRS indicates underperformance. Optional EMA smoothing reduces noise.
///
/// Key characteristics:
/// - Ratio-based: PRS = Base / Comparison
/// - Trend indicator: Rising = outperformance, Falling = underperformance
/// - Smoothing: Optional EMA with bias compensation for warmup
/// - Division by zero: Returns NaN when comparison is zero
///
/// Calculation:
/// <code>
/// Raw Ratio = Base / Comparison
/// Smoothed = EMA(Raw Ratio, smoothPeriod) with bias compensation
/// </code>
///
/// Interpretation:
/// - PRS > 1.0: Base asset is worth more per unit
/// - PRS increasing: Base outperforming comparison
/// - PRS decreasing: Base underperforming comparison
/// - Use with baseline (1.0 or initial ratio) for normalized view
/// </remarks>
/// <seealso href="Prs.md">Detailed documentation</seealso>
[SkipLocalsInit]
public sealed class Prs : AbstractBase
{
    private const double Epsilon = 1e-10;
    private readonly int _smoothPeriod;
    private readonly double _alpha;

    // EMA state with bias compensation
    private double _ema;
    private double _e;  // Bias compensation factor
    private bool _isEmaInitialized;
    private bool _isWarmup;

    // State for bar correction
    private double _lastValidBase;
    private double _lastValidComp;
    private double _p_ema;
    private double _p_e;
    private bool _p_isEmaInitialized;
    private bool _p_isWarmup;
    private double _p_lastValidBase;
    private double _p_lastValidComp;

    private int _count;

    /// <summary>
    /// Gets the raw (unsmoothed) ratio from the last update.
    /// </summary>
    public double RawRatio { get; private set; }

    /// <summary>
    /// Gets the smoothing period for the EMA.
    /// </summary>
    public int SmoothPeriod => _smoothPeriod;

    public override bool IsHot => _count >= _smoothPeriod;

    /// <summary>
    /// Creates a new Price Relative Strength indicator.
    /// </summary>
    /// <param name="smoothPeriod">Smoothing period for EMA (1 = no smoothing)</param>
    public Prs(int smoothPeriod = 1)
    {
        if (smoothPeriod < 1)
        {
            throw new ArgumentException("Smoothing period must be >= 1", nameof(smoothPeriod));
        }

        _smoothPeriod = smoothPeriod;
        _alpha = 2.0 / Max(smoothPeriod, 1);
        _isWarmup = true;
        _e = 1.0;

        Name = smoothPeriod == 1 ? "Prs" : $"Prs({smoothPeriod})";
        WarmupPeriod = smoothPeriod;
    }

    /// <summary>
    /// Updates the PRS indicator with new values from both series.
    /// </summary>
    /// <param name="baseValue">Base asset price</param>
    /// <param name="compValue">Comparison asset price</param>
    /// <param name="isNew">Whether this is a new bar</param>
    /// <returns>The smoothed relative strength ratio</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue baseValue, TValue compValue, bool isNew = true)
    {
        double basePrice = SanitizeBase(baseValue.Value);
        double compPrice = SanitizeComp(compValue.Value);

        if (isNew)
        {
            SaveState();
        }
        else
        {
            RestoreState();
        }

        double result;
        if (Abs(compPrice) < Epsilon)
        {
            // Division by zero - return NaN
            RawRatio = double.NaN;
            result = double.NaN;
        }
        else
        {
            double ratio = basePrice / compPrice;
            RawRatio = ratio;
            result = CalculateSmoothedRatio(ratio);
        }

        if (isNew)
        {
            _count++;
        }

        Last = new TValue(baseValue.Time, result);
        PubEvent(Last);
        return Last;
    }

    /// <summary>
    /// Updates with raw double values.
    /// </summary>
    /// <remarks>
    /// Stamps both inputs with <c>DateTime.UtcNow</c> as their timestamp. For
    /// deterministic or replay-safe sequences use
    /// <see cref="Update(TValue, TValue, bool)"/> with explicit timestamps instead.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(double baseValue, double compValue, bool isNew = true)
    {
        return Update(new TValue(DateTime.UtcNow, baseValue), new TValue(DateTime.UtcNow, compValue), isNew);
    }
    /// <remarks>Not supported for bi-input indicator. Use Update(baseValue, compValue) instead.</remarks>
    public override TValue Update(TValue input, bool isNew = true)
    {
        throw new NotSupportedException("PRS requires two inputs (base and comparison). Use Update(baseValue, compValue).");
    }
    /// <remarks>Not supported for bi-input indicator. Use Calculate(baseSeries, compSeries, period) instead.</remarks>
    public override TSeries Update(TSeries source)
    {
        throw new NotSupportedException("PRS requires two inputs. Use Batch(baseSeries, compSeries, period).");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double SanitizeBase(double value)
    {
        if (double.IsFinite(value))
        {
            _lastValidBase = value;
            return value;
        }
        return double.IsFinite(_lastValidBase) ? _lastValidBase : 0.0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double SanitizeComp(double value)
    {
        if (double.IsFinite(value))
        {
            _lastValidComp = value;
            return value;
        }
        return double.IsFinite(_lastValidComp) ? _lastValidComp : 0.0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SaveState()
    {
        _p_ema = _ema;
        _p_e = _e;
        _p_isEmaInitialized = _isEmaInitialized;
        _p_isWarmup = _isWarmup;
        _p_lastValidBase = _lastValidBase;
        _p_lastValidComp = _lastValidComp;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RestoreState()
    {
        _ema = _p_ema;
        _e = _p_e;
        _isEmaInitialized = _p_isEmaInitialized;
        _isWarmup = _p_isWarmup;
        _lastValidBase = _p_lastValidBase;
        _lastValidComp = _p_lastValidComp;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateSmoothedRatio(double ratio)
    {
        if (_smoothPeriod == 1)
        {
            // No smoothing
            return ratio;
        }

        if (!_isEmaInitialized)
        {
            // First value: initialize EMA with the first ratio
            _ema = ratio;
            _isEmaInitialized = true;
            return ratio;
        }

        // EMA calculation with bias compensation
        _ema = FusedMultiplyAdd(_alpha, ratio - _ema, _ema);

        if (_isWarmup)
        {
            _e *= (1 - _alpha);
            double compensation = 1.0 / (1.0 - _e);
            double result = compensation * _ema;

            if (_e <= 1e-10)
            {
                _isWarmup = false;
            }

            return result;
        }

        return _ema;
    }
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        throw new NotSupportedException("PRS requires two inputs. Use Prime(baseSource, compSource).");
    }

    /// <summary>
    /// Primes the indicator with historical data from both series.
    /// </summary>
    public void Prime(ReadOnlySpan<double> baseSource, ReadOnlySpan<double> compSource, TimeSpan? step = null)
    {
        if (baseSource.Length != compSource.Length)
        {
            throw new ArgumentException("Source arrays must have the same length", nameof(compSource));
        }

        TimeSpan interval = step ?? TimeSpan.FromSeconds(1);
        DateTime time = DateTime.UtcNow - (interval * baseSource.Length);

        for (int i = 0; i < baseSource.Length; i++)
        {
            Update(new TValue(time, baseSource[i]), new TValue(time, compSource[i]), true);
            time += interval;
        }
    }

    public override void Reset()
    {
        _ema = 0;
        _e = 1.0;
        _isEmaInitialized = false;
        _isWarmup = true;
        _lastValidBase = 0;
        _lastValidComp = 0;
        _count = 0;
        RawRatio = 0;
        Last = default;

        _p_ema = 0;
        _p_e = 1.0;
        _p_isEmaInitialized = false;
        _p_isWarmup = true;
        _p_lastValidBase = 0;
        _p_lastValidComp = 0;
    }

    /// <summary>
    /// Calculates PRS for two time series.
    /// </summary>
    public static TSeries Batch(TSeries baseSeries, TSeries compSeries, int smoothPeriod = 1)
    {
        if (baseSeries.Count != compSeries.Count)
        {
            throw new ArgumentException("Series must have the same length", nameof(compSeries));
        }

        var indicator = new Prs(smoothPeriod);
        var result = new TSeries(baseSeries.Count);

        var times = baseSeries.Times;
        var baseValues = baseSeries.Values;
        var compValues = compSeries.Values;

        for (int i = 0; i < baseSeries.Count; i++)
        {
            var tvalBase = new TValue(times[i], baseValues[i]);
            var tvalComp = new TValue(times[i], compValues[i]);
            result.Add(indicator.Update(tvalBase, tvalComp, isNew: true));
        }

        return result;
    }

    /// <summary>
    /// Static batch calculation for span-based processing.
    /// </summary>
    public static void Batch(
        ReadOnlySpan<double> baseSeries,
        ReadOnlySpan<double> compSeries,
        Span<double> output,
        int smoothPeriod = 1)
    {
        if (baseSeries.Length != compSeries.Length)
        {
            throw new ArgumentException("Series must have the same length", nameof(compSeries));
        }

        if (baseSeries.Length != output.Length)
        {
            throw new ArgumentException("Output must have the same length as input", nameof(output));
        }

        if (smoothPeriod < 1)
        {
            throw new ArgumentException("Smoothing period must be >= 1", nameof(smoothPeriod));
        }

        var indicator = new Prs(smoothPeriod);

        for (int i = 0; i < baseSeries.Length; i++)
        {
            var result = indicator.Update(baseSeries[i], compSeries[i], isNew: true);
            output[i] = result.Value;
        }
    }

    public static (TSeries Results, Prs Indicator) Calculate(TSeries baseSeries, TSeries compSeries, int smoothPeriod = 1)
    {
        var indicator = new Prs(smoothPeriod);
        TSeries results = Batch(baseSeries, compSeries, smoothPeriod);
        return (results, indicator);
    }

}
