// Garman-Klass Volatility (GKV) Indicator
// A range-based volatility estimator using OHLC data with RMA smoothing

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// GKV: Garman-Klass Volatility
/// A range-based volatility estimator that uses all four OHLC prices,
/// providing more efficient volatility estimates than close-to-close methods.
/// </summary>
/// <remarks>
/// <b>Calculation steps:</b>
/// <list type="number">
/// <item>Calculate log prices: lnH, lnL, lnO, lnC</item>
/// <item>term1 = 0.5 × (lnH - lnL)²</item>
/// <item>term2 = (2×ln(2) - 1) × (lnC - lnO)²</item>
/// <item>gkEstimator = term1 - term2</item>
/// <item>Smooth using bias-corrected RMA</item>
/// <item>volatility = √(smoothedEstimator)</item>
/// <item>If annualize: volatility × √(annualPeriods)</item>
/// </list>
///
/// <b>Key characteristics:</b>
/// <list type="bullet">
/// <item>Uses OHLC data for more efficient estimation</item>
/// <item>RMA (Wilder's) smoothing with bias correction</item>
/// <item>Optional annualization (default 252 trading days)</item>
/// <item>More efficient than close-to-close estimators</item>
/// </list>
///
/// <b>Sources:</b>
/// Mark B. Garman and Michael J. Klass (1980). "On the Estimation of Security Price
/// Volatilities from Historical Data." Journal of Business, 53(1), 67-78.
/// </remarks>
[SkipLocalsInit]
public sealed class Gkv : AbstractBase
{
    private const double C_2LN2_1 = 0.38629436111989061883; // 2 * ln(2) - 1
    private const double Epsilon = 1e-10;

    private readonly int _period;
    private readonly bool _annualize;
    private readonly int _annualPeriods;
    private readonly double _alpha;
    private readonly double _decay;
    private readonly double _annualFactor;

    [StructLayout(LayoutKind.Auto)]
    private record struct State(
        double RawRma,
        double E,
        double LastValidGk,
        double LastValue,
        int Count
    );
    private State _s;
    private State _ps;

    /// <summary>
    /// Initializes a new instance of the Gkv class.
    /// </summary>
    /// <param name="period">The smoothing period (default 20).</param>
    /// <param name="annualize">Whether to annualize the volatility (default true).</param>
    /// <param name="annualPeriods">Number of periods per year (default 252).</param>
    /// <exception cref="ArgumentException">
    /// Thrown when period is less than 1, or annualPeriods is less than 1 when annualizing.
    /// </exception>
    public Gkv(int period = 20, bool annualize = true, int annualPeriods = 252)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }
        if (annualize && annualPeriods <= 0)
        {
            throw new ArgumentException("Annual periods must be greater than 0 when annualizing", nameof(annualPeriods));
        }
        _period = period;
        _annualize = annualize;
        _annualPeriods = annualPeriods;
        _alpha = 1.0 / period;
        _decay = 1.0 - _alpha;
        _annualFactor = annualize ? Math.Sqrt(annualPeriods) : 1.0;
        WarmupPeriod = period;
        Name = $"Gkv({period})";
        _s = new State(0, 1.0, 0, 0, 0);
        _ps = _s;
    }

    /// <summary>
    /// Initializes a new instance of the Gkv class with a source.
    /// </summary>
    /// <param name="source">The data source for chaining.</param>
    /// <param name="period">The smoothing period (default 20).</param>
    /// <param name="annualize">Whether to annualize the volatility (default true).</param>
    /// <param name="annualPeriods">Number of periods per year (default 252).</param>
    public Gkv(ITValuePublisher source, int period = 20, bool annualize = true, int annualPeriods = 252)
        : this(period, annualize, annualPeriods)
    {
        source.Pub += Handle;
    }

    private void Handle(object? sender, in TValueEventArgs e) => Update(e.Value, e.IsNew);

    /// <summary>
    /// True if the indicator has enough data for valid results.
    /// </summary>
    public override bool IsHot => _s.Count >= WarmupPeriod;

    /// <summary>
    /// The smoothing period.
    /// </summary>
    public int Period => _period;

    /// <summary>
    /// Whether volatility is annualized.
    /// </summary>
    public bool Annualize => _annualize;

    /// <summary>
    /// Number of periods per year for annualization.
    /// </summary>
    public int AnnualPeriods => _annualPeriods;

    /// <summary>
    /// Computes the Garman-Klass estimator for a single bar.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ComputeGkEstimator(double open, double high, double low, double close)
    {
        double lnH = Math.Log(high);
        double lnL = Math.Log(low);
        double lnO = Math.Log(open);
        double lnC = Math.Log(close);

        double hlRange = lnH - lnL;
        double coRange = lnC - lnO;

        // term1 = 0.5 * (lnH - lnL)^2
        // term2 = (2*ln(2) - 1) * (lnC - lnO)^2
        // gkEstimator = term1 - term2
        double term1 = 0.5 * hlRange * hlRange;
        double term2 = C_2LN2_1 * coRange * coRange;
        return term1 - term2;
    }

    /// <summary>
    /// Updates the indicator with a TValue input.
    /// For GKV, this treats the value as a pre-computed GK estimator.
    /// Prefer Update(TBar) for standard OHLC data.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        return UpdateCore(input.Time, input.Value, isNew);
    }

    /// <summary>
    /// Updates the indicator with a new bar (preferred method).
    /// </summary>
    /// <param name="bar">The input bar.</param>
    /// <param name="isNew">Whether this is a new bar or an update.</param>
    /// <returns>The calculated volatility value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TBar bar, bool isNew = true)
    {
        // Handle invalid OHLC data
        if (!double.IsFinite(bar.Open) || !double.IsFinite(bar.High) ||
            !double.IsFinite(bar.Low) || !double.IsFinite(bar.Close) ||
            bar.Open <= 0 || bar.High <= 0 || bar.Low <= 0 || bar.Close <= 0)
        {
            // Pass NaN to trigger last-valid-value substitution
            return UpdateCore(bar.Time, double.NaN, isNew);
        }

        double gkEstimator = ComputeGkEstimator(bar.Open, bar.High, bar.Low, bar.Close);
        return UpdateCore(bar.Time, gkEstimator, isNew);
    }

    /// <summary>
    /// Updates the indicator with a bar series.
    /// </summary>
    /// <param name="source">The source bar series.</param>
    /// <returns>A TSeries containing the volatility values.</returns>
    public TSeries Update(TBarSeries source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        // Extract OHLC data
        Span<double> opens = len <= 64 ? stackalloc double[len] : new double[len];
        Span<double> highs = len <= 64 ? stackalloc double[len] : new double[len];
        Span<double> lows = len <= 64 ? stackalloc double[len] : new double[len];
        Span<double> closes = len <= 64 ? stackalloc double[len] : new double[len];

        for (int i = 0; i < len; i++)
        {
            opens[i] = source[i].Open;
            highs[i] = source[i].High;
            lows[i] = source[i].Low;
            closes[i] = source[i].Close;
            tSpan[i] = source[i].Time;
        }

        Batch(opens, highs, lows, closes, vSpan, _period, _annualize, _annualPeriods);

        // Update internal state
        for (int i = 0; i < len; i++)
        {
            Update(source[i], isNew: true);
        }

        return new TSeries(t, v);
    }

    /// <inheritdoc/>
    public override TSeries Update(TSeries source)
    {
        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        // Treat source values as pre-computed GK estimators
        BatchFromEstimators(source.Values, vSpan, _period, _annualize, _annualPeriods);
        source.Times.CopyTo(tSpan);

        // Update internal state
        for (int i = 0; i < len; i++)
        {
            Update(new TValue(source.Times[i], source.Values[i]), isNew: true);
        }

        return new TSeries(t, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TValue UpdateCore(long timeTicks, double gkEstimator, bool isNew)
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

        // Handle non-finite estimator - use last valid value
        if (!double.IsFinite(gkEstimator))
        {
            gkEstimator = s.LastValidGk;
        }
        else
        {
            s.LastValidGk = gkEstimator;
        }

        // RMA smoothing with bias correction
        double rawRma, e;
        if (s.Count == 0)
        {
            rawRma = gkEstimator;
            e = _decay;
        }
        else
        {
            // RMA: raw_rma = prev_rma * decay + alpha * value
            rawRma = Math.FusedMultiplyAdd(s.RawRma, _decay, _alpha * gkEstimator);
            e = _decay * s.E;
        }

        // Bias correction
        double correctedRma = e > Epsilon ? rawRma / (1.0 - e) : rawRma;

        // Calculate volatility
        double volatility;
        if (correctedRma < 0)
        {
            volatility = 0; // Can't take sqrt of negative
        }
        else
        {
            volatility = Math.Sqrt(correctedRma) * _annualFactor;
        }

        if (!double.IsFinite(volatility))
        {
            volatility = s.LastValue;
        }

        // Update state using direct field assignment (like Cvi pattern)
        s.RawRma = rawRma;
        s.E = e;
        s.LastValue = volatility;
        if (isNew)
        {
            s.Count++;
        }

        _s = s;

        Last = new TValue(timeTicks, volatility);
        PubEvent(Last, isNew);
        return Last;
    }

    /// <inheritdoc/>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        for (int i = 0; i < source.Length; i++)
        {
            Update(new TValue(DateTime.UtcNow, source[i]), isNew: true);
        }
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        _s = new State(0, 1.0, 0, 0, 0);
        _ps = _s;
        Last = default;
    }

    /// <summary>
    /// Calculates Garman-Klass Volatility for a bar series (static).
    /// </summary>
    /// <param name="source">The source bar series.</param>
    /// <param name="period">The smoothing period.</param>
    /// <param name="annualize">Whether to annualize.</param>
    /// <param name="annualPeriods">Periods per year.</param>
    /// <returns>A TSeries containing the volatility values.</returns>
    public static TSeries Batch(TBarSeries source, int period = 20, bool annualize = true, int annualPeriods = 252)
    {
        var gkv = new Gkv(period, annualize, annualPeriods);
        return gkv.Update(source);
    }

    /// <summary>
    /// Calculates GKV for a TSeries (treats values as pre-computed GK estimators).
    /// </summary>
    public static TSeries Batch(TSeries source, int period = 20, bool annualize = true, int annualPeriods = 252)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }
        if (annualize && annualPeriods <= 0)
        {
            throw new ArgumentException("Annual periods must be greater than 0 when annualizing", nameof(annualPeriods));
        }

        int len = source.Count;
        var t = new List<long>(len);
        var v = new List<double>(len);
        CollectionsMarshal.SetCount(t, len);
        CollectionsMarshal.SetCount(v, len);

        var tSpan = CollectionsMarshal.AsSpan(t);
        var vSpan = CollectionsMarshal.AsSpan(v);

        BatchFromEstimators(source.Values, vSpan, period, annualize, annualPeriods);
        source.Times.CopyTo(tSpan);

        return new TSeries(t, v);
    }

    /// <summary>
    /// Batch calculation using spans for OHLC data.
    /// </summary>
    /// <param name="open">Open prices.</param>
    /// <param name="high">High prices.</param>
    /// <param name="low">Low prices.</param>
    /// <param name="close">Close prices.</param>
    /// <param name="output">Output volatility values.</param>
    /// <param name="period">The smoothing period.</param>
    /// <param name="annualize">Whether to annualize.</param>
    /// <param name="annualPeriods">Periods per year.</param>
    public static void Batch(
        ReadOnlySpan<double> open,
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
        ReadOnlySpan<double> close,
        Span<double> output,
        int period = 20,
        bool annualize = true,
        int annualPeriods = 252)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }
        if (annualize && annualPeriods <= 0)
        {
            throw new ArgumentException("Annual periods must be greater than 0 when annualizing", nameof(annualPeriods));
        }

        int len = open.Length;
        if (high.Length != len || low.Length != len || close.Length != len)
        {
            throw new ArgumentException("All input spans must have the same length", nameof(high));
        }
        if (output.Length < len)
        {
            throw new ArgumentException("Output span must be at least as long as input spans", nameof(output));
        }

        if (len == 0)
        {
            return;
        }

        double alpha = 1.0 / period;
        double decay = 1.0 - alpha;
        double annualFactor = annualize ? Math.Sqrt(annualPeriods) : 1.0;

        double rawRma = 0;
        double e = 1.0;
        double lastValidGk = 0;
        double lastValue = 0;

        for (int i = 0; i < len; i++)
        {
            double o = open[i];
            double h = high[i];
            double l = low[i];
            double c = close[i];

            double gkEstimator;

            // Handle invalid data
            if (!double.IsFinite(o) || !double.IsFinite(h) ||
                !double.IsFinite(l) || !double.IsFinite(c) ||
                o <= 0 || h <= 0 || l <= 0 || c <= 0)
            {
                gkEstimator = lastValidGk;
            }
            else
            {
                gkEstimator = ComputeGkEstimator(o, h, l, c);
                if (!double.IsFinite(gkEstimator))
                {
                    gkEstimator = lastValidGk;
                }
                else
                {
                    lastValidGk = gkEstimator;
                }
            }

            if (i == 0)
            {
                rawRma = gkEstimator;
                e = decay;
            }
            else
            {
                rawRma = Math.FusedMultiplyAdd(rawRma, decay, alpha * gkEstimator);
                e *= decay;
            }

            double correctedRma = e > Epsilon ? rawRma / (1.0 - e) : rawRma;

            double volatility = correctedRma < 0 ? 0 : Math.Sqrt(correctedRma) * annualFactor;

            if (!double.IsFinite(volatility))
            {
                volatility = lastValue;
            }
            else
            {
                lastValue = volatility;
            }

            output[i] = volatility;
        }
    }

    public static (TSeries Results, Gkv Indicator) Calculate(TBarSeries source, int period = 20, bool annualize = true, int annualPeriods = 252)
    {
        var indicator = new Gkv(period, annualize, annualPeriods);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }


    /// <summary>
    /// Batch calculation from pre-computed GK estimators.
    /// </summary>
    private static void BatchFromEstimators(
        ReadOnlySpan<double> estimators,
        Span<double> output,
        int period,
        bool annualize,
        int annualPeriods)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }
        if (estimators.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }

        int len = estimators.Length;
        if (len == 0)
        {
            return;
        }

        double alpha = 1.0 / period;
        double decay = 1.0 - alpha;
        double annualFactor = annualize ? Math.Sqrt(annualPeriods) : 1.0;

        double rawRma = 0;
        double e = 1.0;
        double lastValidGk = 0;
        double lastValue = 0;

        for (int i = 0; i < len; i++)
        {
            double gkEstimator = estimators[i];

            if (!double.IsFinite(gkEstimator))
            {
                gkEstimator = lastValidGk;
            }
            else
            {
                lastValidGk = gkEstimator;
            }

            if (i == 0)
            {
                rawRma = gkEstimator;
                e = decay;
            }
            else
            {
                rawRma = Math.FusedMultiplyAdd(rawRma, decay, alpha * gkEstimator);
                e *= decay;
            }

            double correctedRma = e > Epsilon ? rawRma / (1.0 - e) : rawRma;

            double volatility = correctedRma < 0 ? 0 : Math.Sqrt(correctedRma) * annualFactor;

            if (!double.IsFinite(volatility))
            {
                volatility = lastValue;
            }
            else
            {
                lastValue = volatility;
            }

            output[i] = volatility;
        }
    }
}
