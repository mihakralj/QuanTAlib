// High-Low Volatility (HLV) Indicator
// A range-based volatility estimator using the Parkinson method with RMA smoothing

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// HLV: High-Low Volatility (Parkinson)
/// A range-based volatility estimator that uses only High-Low prices,
/// providing more efficient volatility estimates than close-to-close methods.
/// </summary>
/// <remarks>
/// <b>Calculation steps:</b>
/// <list type="number">
/// <item>Calculate log prices: lnH, lnL</item>
/// <item>parkinsonEstimator = (1/(4×ln(2))) × (lnH - lnL)²</item>
/// <item>Smooth using bias-corrected RMA</item>
/// <item>volatility = √(smoothedEstimator)</item>
/// <item>If annualize: volatility × √(annualPeriods)</item>
/// </list>
///
/// <b>Key characteristics:</b>
/// <list type="bullet">
/// <item>Uses only High-Low data (simpler than Garman-Klass)</item>
/// <item>RMA (Wilder's) smoothing with bias correction</item>
/// <item>Optional annualization (default 252 trading days)</item>
/// <item>5× more efficient than close-to-close estimators</item>
/// </list>
///
/// <b>Sources:</b>
/// Michael Parkinson (1980). "The Extreme Value Method for Estimating the Variance
/// of the Rate of Return." Journal of Business, 53(1), 61-65.
/// </remarks>
[SkipLocalsInit]
public sealed class Hlv : AbstractBase
{
    private const double C_4LN2_INV = 0.36067376022224085; // 1 / (4 * ln(2))
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
        double LastValidPk,
        double LastValue,
        int Count
    );
    private State _s;
    private State _ps;

    /// <summary>
    /// Initializes a new instance of the Hlv class.
    /// </summary>
    /// <param name="period">The smoothing period (default 20).</param>
    /// <param name="annualize">Whether to annualize the volatility (default true).</param>
    /// <param name="annualPeriods">Number of periods per year (default 252).</param>
    /// <exception cref="ArgumentException">
    /// Thrown when period is less than 1, or annualPeriods is less than 1 when annualizing.
    /// </exception>
    public Hlv(int period = 20, bool annualize = true, int annualPeriods = 252)
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
        Name = $"Hlv({period})";
        _s = new State(0, 1.0, 0, 0, 0);
        _ps = _s;
    }

    /// <summary>
    /// Initializes a new instance of the Hlv class with a source.
    /// </summary>
    /// <param name="source">The data source for chaining.</param>
    /// <param name="period">The smoothing period (default 20).</param>
    /// <param name="annualize">Whether to annualize the volatility (default true).</param>
    /// <param name="annualPeriods">Number of periods per year (default 252).</param>
    public Hlv(ITValuePublisher source, int period = 20, bool annualize = true, int annualPeriods = 252)
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
    /// Computes the Parkinson estimator for a single bar.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ComputeParkinsonEstimator(double high, double low)
    {
        double lnH = Math.Log(high);
        double lnL = Math.Log(low);
        double hlRange = lnH - lnL;

        // parkinsonEstimator = (1/(4*ln(2))) * (lnH - lnL)^2
        return C_4LN2_INV * hlRange * hlRange;
    }

    /// <summary>
    /// Updates the indicator with a TValue input.
    /// For HLV, this treats the value as a pre-computed Parkinson estimator.
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
        // Handle invalid High-Low data
        if (!double.IsFinite(bar.High) || !double.IsFinite(bar.Low) ||
            bar.High <= 0 || bar.Low <= 0)
        {
            // Pass NaN to trigger last-valid-value substitution
            return UpdateCore(bar.Time, double.NaN, isNew);
        }

        double pkEstimator = ComputeParkinsonEstimator(bar.High, bar.Low);
        return UpdateCore(bar.Time, pkEstimator, isNew);
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

        // Extract High-Low data
        Span<double> highs = len <= 128 ? stackalloc double[len] : new double[len];
        Span<double> lows = len <= 128 ? stackalloc double[len] : new double[len];

        for (int i = 0; i < len; i++)
        {
            highs[i] = source[i].High;
            lows[i] = source[i].Low;
            tSpan[i] = source[i].Time;
        }

        Batch(highs, lows, vSpan, _period, _annualize, _annualPeriods);

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

        // Treat source values as pre-computed Parkinson estimators
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
    private TValue UpdateCore(long timeTicks, double pkEstimator, bool isNew)
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
        if (!double.IsFinite(pkEstimator))
        {
            pkEstimator = s.LastValidPk;
        }
        else
        {
            s.LastValidPk = pkEstimator;
        }

        // RMA smoothing with bias correction
        double rawRma, e;
        if (s.Count == 0)
        {
            rawRma = pkEstimator;
            e = _decay;
        }
        else
        {
            // RMA: raw_rma = prev_rma * decay + alpha * value
            rawRma = Math.FusedMultiplyAdd(s.RawRma, _decay, _alpha * pkEstimator);
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
    /// Calculates High-Low Volatility for a bar series (static).
    /// </summary>
    /// <param name="source">The source bar series.</param>
    /// <param name="period">The smoothing period.</param>
    /// <param name="annualize">Whether to annualize.</param>
    /// <param name="annualPeriods">Periods per year.</param>
    /// <returns>A TSeries containing the volatility values.</returns>
    public static TSeries Calculate(TBarSeries source, int period = 20, bool annualize = true, int annualPeriods = 252)
    {
        var hlv = new Hlv(period, annualize, annualPeriods);
        return hlv.Update(source);
    }

    /// <summary>
    /// Calculates HLV for a TSeries (treats values as pre-computed Parkinson estimators).
    /// </summary>
    public static TSeries Calculate(TSeries source, int period = 20, bool annualize = true, int annualPeriods = 252)
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
    /// Batch calculation using spans for High-Low data.
    /// </summary>
    /// <param name="high">High prices.</param>
    /// <param name="low">Low prices.</param>
    /// <param name="output">Output volatility values.</param>
    /// <param name="period">The smoothing period.</param>
    /// <param name="annualize">Whether to annualize.</param>
    /// <param name="annualPeriods">Periods per year.</param>
    public static void Batch(
        ReadOnlySpan<double> high,
        ReadOnlySpan<double> low,
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

        int len = high.Length;
        if (low.Length != len)
        {
            throw new ArgumentException("High and low spans must have the same length", nameof(low));
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
        double lastValidPk = 0;
        double lastValue = 0;

        for (int i = 0; i < len; i++)
        {
            double h = high[i];
            double l = low[i];

            double pkEstimator;

            // Handle invalid data
            if (!double.IsFinite(h) || !double.IsFinite(l) ||
                h <= 0 || l <= 0)
            {
                pkEstimator = lastValidPk;
            }
            else
            {
                pkEstimator = ComputeParkinsonEstimator(h, l);
                if (!double.IsFinite(pkEstimator))
                {
                    pkEstimator = lastValidPk;
                }
                else
                {
                    lastValidPk = pkEstimator;
                }
            }

            if (i == 0)
            {
                rawRma = pkEstimator;
                e = decay;
            }
            else
            {
                rawRma = Math.FusedMultiplyAdd(rawRma, decay, alpha * pkEstimator);
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

    /// <summary>
    /// Batch calculation from pre-computed Parkinson estimators.
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
        double lastValidPk = 0;
        double lastValue = 0;

        for (int i = 0; i < len; i++)
        {
            double pkEstimator = estimators[i];

            if (!double.IsFinite(pkEstimator))
            {
                pkEstimator = lastValidPk;
            }
            else
            {
                lastValidPk = pkEstimator;
            }

            if (i == 0)
            {
                rawRma = pkEstimator;
                e = decay;
            }
            else
            {
                rawRma = Math.FusedMultiplyAdd(rawRma, decay, alpha * pkEstimator);
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