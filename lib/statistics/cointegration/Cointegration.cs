using System.Runtime.CompilerServices;
using static System.Math;

namespace QuanTAlib;

/// <summary>
/// Cointegration: Measures the statistical equilibrium relationship between two price series
/// using the Engle-Granger two-step method with Augmented Dickey-Fuller test.
/// </summary>
/// <remarks>
/// Cointegration tests whether two non-stationary time series have a long-run equilibrium
/// relationship. The indicator returns the ADF test statistic for the regression residuals.
///
/// Algorithm:
/// 1. Estimate linear regression: A = α + β*B + ε
///    - β = correlation(A,B) × (σA/σB)
///    - α = mean(A) - β × mean(B)
/// 2. Calculate residuals: ε = A - (α + β×B)
/// 3. Run ADF test on residuals:
///    - Δε_t = γ × ε_{t-1} + u_t
///    - ADF statistic = γ / SE(γ)
///
/// Interpretation:
/// - More negative ADF values indicate stronger evidence of cointegration
/// - Critical values (approx): -3.43 (1%), -2.86 (5%), -2.57 (10%)
/// - Values more negative than critical values reject null hypothesis of no cointegration
///
/// Uses Kahan compensated summation for numerical stability over long streams.
/// </remarks>
[SkipLocalsInit]
public sealed class Cointegration : AbstractBase
{
    private readonly RingBuffer _bufferA;
    private readonly RingBuffer _bufferB;

    // Running sums for O(1) statistics
    private double _sumA, _sumB;
    private double _sumA2, _sumB2;
    private double _sumAB;

    // Kahan compensation for main sums
    private double _sumAComp, _sumBComp;
    private double _sumA2Comp, _sumB2Comp;
    private double _sumABComp;

    // Residual tracking
    private double _prevResidual;
    private double _p_prevResidual;
    private bool _hasPrevResidual;
    private bool _p_hasPrevResidual;

    // ADF regression running sums (period-1 window)
    private readonly RingBuffer _deltaResiduals;
    private readonly RingBuffer _laggedResiduals;
    private double _sumDeltaLagged, _sumLagged2, _sumDelta2;

    // Kahan compensation for ADF sums
    private double _sumDeltaLaggedComp, _sumLagged2Comp, _sumDelta2Comp;

    // Previous compensation state for rollback
    private double _p_sumAComp, _p_sumBComp;
    private double _p_sumA2Comp, _p_sumB2Comp;
    private double _p_sumABComp;
    private double _p_sumDeltaLaggedComp, _p_sumLagged2Comp, _p_sumDelta2Comp;

    // Last valid values for NaN handling
    private double _lastValidA, _lastValidB;
    private double _p_lastValidA, _p_lastValidB;

    private const double Epsilon = 1e-10;

    /// <inheritdoc />
    public override bool IsHot => _bufferA.IsFull && _hasPrevResidual;

    /// <summary>
    /// Creates a new Cointegration indicator.
    /// </summary>
    /// <param name="period">Lookback period for regression and ADF test (must be > 1)</param>
    public Cointegration(int period = 20)
    {
        if (period <= 1)
        {
            throw new ArgumentException("Period must be greater than 1", nameof(period));
        }

        _bufferA = new RingBuffer(period);
        _bufferB = new RingBuffer(period);
        _deltaResiduals = new RingBuffer(period - 1);
        _laggedResiduals = new RingBuffer(period - 1);

        Name = $"Cointegration({period})";
        WarmupPeriod = period + 1; // Need extra bar for first delta
    }

    /// <summary>
    /// Updates the Cointegration indicator with new values from both series.
    /// </summary>
    /// <param name="seriesA">First series value (dependent variable)</param>
    /// <param name="seriesB">Second series value (independent variable)</param>
    /// <param name="isNew">Whether this is a new bar</param>
    /// <returns>The ADF test statistic (more negative = stronger cointegration)</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue seriesA, TValue seriesB, bool isNew = true)
    {
        double a = SanitizeA(seriesA.Value);
        double b = SanitizeB(seriesB.Value);

        if (isNew)
        {
            ProcessNewBar(a, b);
        }
        else
        {
            ProcessBarCorrection(a, b);
        }

        double adfStat = CalculateAdfStatistic();

        Last = new TValue(seriesA.Time, adfStat);
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
    public TValue Update(double seriesA, double seriesB, bool isNew = true)
    {
        DateTime now = DateTime.UtcNow;
        return Update(new TValue(now, seriesA), new TValue(now, seriesB), isNew);
    }
    /// <summary>Not supported. This indicator requires two inputs; use <see cref="Update(TValue, TValue, bool)"/> instead.</summary>
    /// <remarks>Not supported for bi-input indicator. Use Update(seriesA, seriesB) instead.</remarks>
    public override TValue Update(TValue input, bool isNew = true)
    {
        throw new NotSupportedException("Cointegration requires two inputs (seriesA and seriesB). Use Update(seriesA, seriesB).");
    }
    /// <summary>Not supported. This indicator requires two inputs; use <see cref="Batch(TSeries, TSeries, int)"/> instead.</summary>
    /// <remarks>Not supported for bi-input indicator. Use Calculate(seriesA, seriesB, period) instead.</remarks>
    public override TSeries Update(TSeries source)
    {
        throw new NotSupportedException("Cointegration requires two inputs. Use Batch(seriesA, seriesB, period).");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double SanitizeA(double value)
    {
        if (double.IsFinite(value))
        {
            _lastValidA = value;
            return value;
        }
        return double.IsFinite(_lastValidA) ? _lastValidA : 0.0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double SanitizeB(double value)
    {
        if (double.IsFinite(value))
        {
            _lastValidB = value;
            return value;
        }
        return double.IsFinite(_lastValidB) ? _lastValidB : 0.0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessNewBar(double a, double b)
    {
        // Save state for bar correction
        _p_lastValidA = _lastValidA;
        _p_lastValidB = _lastValidB;
        _p_prevResidual = _prevResidual;
        _p_hasPrevResidual = _hasPrevResidual;
        _p_sumAComp = _sumAComp;
        _p_sumBComp = _sumBComp;
        _p_sumA2Comp = _sumA2Comp;
        _p_sumB2Comp = _sumB2Comp;
        _p_sumABComp = _sumABComp;
        _p_sumDeltaLaggedComp = _sumDeltaLaggedComp;
        _p_sumLagged2Comp = _sumLagged2Comp;
        _p_sumDelta2Comp = _sumDelta2Comp;

        // Update main buffers
        if (_bufferA.IsFull)
        {
            double oldA = _bufferA.Oldest;
            double oldB = _bufferB.Oldest;

            // Kahan subtract oldA from _sumA
            { double y = -oldA - _sumAComp; double t = _sumA + y; _sumAComp = (t - _sumA) - y; _sumA = t; }
            // Kahan subtract oldB from _sumB
            { double y = -oldB - _sumBComp; double t = _sumB + y; _sumBComp = (t - _sumB) - y; _sumB = t; }
            // Kahan subtract oldA² from _sumA2
            { double y = -(oldA * oldA) - _sumA2Comp; double t = _sumA2 + y; _sumA2Comp = (t - _sumA2) - y; _sumA2 = t; }
            // Kahan subtract oldB² from _sumB2
            { double y = -(oldB * oldB) - _sumB2Comp; double t = _sumB2 + y; _sumB2Comp = (t - _sumB2) - y; _sumB2 = t; }
            // Kahan subtract oldA*oldB from _sumAB
            { double y = -(oldA * oldB) - _sumABComp; double t = _sumAB + y; _sumABComp = (t - _sumAB) - y; _sumAB = t; }
        }

        _bufferA.Add(a);
        _bufferB.Add(b);

        // Kahan add a to _sumA
        { double y = a - _sumAComp; double t = _sumA + y; _sumAComp = (t - _sumA) - y; _sumA = t; }
        // Kahan add b to _sumB
        { double y = b - _sumBComp; double t = _sumB + y; _sumBComp = (t - _sumB) - y; _sumB = t; }
        // Kahan add a² to _sumA2
        { double y = (a * a) - _sumA2Comp; double t = _sumA2 + y; _sumA2Comp = (t - _sumA2) - y; _sumA2 = t; }
        // Kahan add b² to _sumB2
        { double y = (b * b) - _sumB2Comp; double t = _sumB2 + y; _sumB2Comp = (t - _sumB2) - y; _sumB2 = t; }
        // Kahan add a*b to _sumAB
        { double y = (a * b) - _sumABComp; double t = _sumAB + y; _sumABComp = (t - _sumAB) - y; _sumAB = t; }

        // Calculate current residual
        double residual = CalculateResidual(a, b);

        // Update ADF regression buffers
        if (_hasPrevResidual)
        {
            double delta = residual - _prevResidual;
            double lagged = _prevResidual;

            if (_deltaResiduals.IsFull)
            {
                double oldDelta = _deltaResiduals.Oldest;
                double oldLagged = _laggedResiduals.Oldest;
                // Kahan subtract from ADF sums
                { double y = -(oldDelta * oldLagged) - _sumDeltaLaggedComp; double t = _sumDeltaLagged + y; _sumDeltaLaggedComp = (t - _sumDeltaLagged) - y; _sumDeltaLagged = t; }
                { double y = -(oldLagged * oldLagged) - _sumLagged2Comp; double t = _sumLagged2 + y; _sumLagged2Comp = (t - _sumLagged2) - y; _sumLagged2 = t; }
                { double y = -(oldDelta * oldDelta) - _sumDelta2Comp; double t = _sumDelta2 + y; _sumDelta2Comp = (t - _sumDelta2) - y; _sumDelta2 = t; }
            }

            _deltaResiduals.Add(delta);
            _laggedResiduals.Add(lagged);

            // Kahan add to ADF sums
            { double y = (delta * lagged) - _sumDeltaLaggedComp; double t = _sumDeltaLagged + y; _sumDeltaLaggedComp = (t - _sumDeltaLagged) - y; _sumDeltaLagged = t; }
            { double y = (lagged * lagged) - _sumLagged2Comp; double t = _sumLagged2 + y; _sumLagged2Comp = (t - _sumLagged2) - y; _sumLagged2 = t; }
            { double y = (delta * delta) - _sumDelta2Comp; double t = _sumDelta2 + y; _sumDelta2Comp = (t - _sumDelta2) - y; _sumDelta2 = t; }
        }

        _prevResidual = residual;
        _hasPrevResidual = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessBarCorrection(double a, double b)
    {
        // Restore state
        _lastValidA = _p_lastValidA;
        _lastValidB = _p_lastValidB;
        _prevResidual = _p_prevResidual;
        _hasPrevResidual = _p_hasPrevResidual;
        _sumAComp = _p_sumAComp;
        _sumBComp = _p_sumBComp;
        _sumA2Comp = _p_sumA2Comp;
        _sumB2Comp = _p_sumB2Comp;
        _sumABComp = _p_sumABComp;
        _sumDeltaLaggedComp = _p_sumDeltaLaggedComp;
        _sumLagged2Comp = _p_sumLagged2Comp;
        _sumDelta2Comp = _p_sumDelta2Comp;

        // Update newest values in main buffers
        if (_bufferA.Count == 0)
        {
            // Nothing to correct yet; no current bar exists
            return;
        }

        double oldA = _bufferA.Newest;
        double oldB = _bufferB.Newest;

        // Kahan subtract old + add new for main sums
        { double y = (-oldA + a) - _sumAComp; double t = _sumA + y; _sumAComp = (t - _sumA) - y; _sumA = t; }
        { double y = (-oldB + b) - _sumBComp; double t = _sumB + y; _sumBComp = (t - _sumB) - y; _sumB = t; }
        { double y = (-(oldA * oldA) + (a * a)) - _sumA2Comp; double t = _sumA2 + y; _sumA2Comp = (t - _sumA2) - y; _sumA2 = t; }
        { double y = (-(oldB * oldB) + (b * b)) - _sumB2Comp; double t = _sumB2 + y; _sumB2Comp = (t - _sumB2) - y; _sumB2 = t; }
        { double y = (-(oldA * oldB) + (a * b)) - _sumABComp; double t = _sumAB + y; _sumABComp = (t - _sumAB) - y; _sumAB = t; }

        _bufferA.UpdateNewest(a);
        _bufferB.UpdateNewest(b);

        // Calculate current residual
        double residual = CalculateResidual(a, b);

        // Update ADF regression buffers
        if (_hasPrevResidual)
        {
            double delta = residual - _prevResidual;
            double lagged = _prevResidual;

            if (_deltaResiduals.Count == 0)
            {
                // Nothing to correct yet in ADF buffers; no current entry exists
                return;
            }

            double oldDelta = _deltaResiduals.Newest;
            double oldLagged = _laggedResiduals.Newest;

            // Kahan subtract old + add new for ADF sums
            { double y = (-(oldDelta * oldLagged) + (delta * lagged)) - _sumDeltaLaggedComp; double t = _sumDeltaLagged + y; _sumDeltaLaggedComp = (t - _sumDeltaLagged) - y; _sumDeltaLagged = t; }
            { double y = (-(oldLagged * oldLagged) + (lagged * lagged)) - _sumLagged2Comp; double t = _sumLagged2 + y; _sumLagged2Comp = (t - _sumLagged2) - y; _sumLagged2 = t; }
            { double y = (-(oldDelta * oldDelta) + (delta * delta)) - _sumDelta2Comp; double t = _sumDelta2 + y; _sumDelta2Comp = (t - _sumDelta2) - y; _sumDelta2 = t; }

            _deltaResiduals.UpdateNewest(delta);
            _laggedResiduals.UpdateNewest(lagged);
        }

        _prevResidual = residual;
        _hasPrevResidual = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateResidual(double a, double b)
    {
        int n = _bufferA.Count;
        if (n < 2)
        {
            return 0.0;
        }

        // Calculate means
        double meanA = _sumA / n;
        double meanB = _sumB / n;

        // Calculate variance of B and covariance
        double varB = Max(0.0, (_sumB2 / n) - (meanB * meanB));
        double cov = (_sumAB / n) - (meanA * meanB);

        // Calculate beta and alpha
        double beta = 0.0;
        if (varB > Epsilon)
        {
            beta = cov / varB;
        }
        double alpha = meanA - (beta * meanB);

        // Calculate residual
        return a - (alpha + (beta * b));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CalculateAdfStatistic()
    {
        int n = _deltaResiduals.Count;
        if (n < 2)
        {
            return double.NaN;
        }

        if (_sumLagged2 < Epsilon)
        {
            return double.NaN;
        }

        // No-intercept ADF regression: Δε_t = γ × ε_{t-1} + u_t
        double gamma = _sumDeltaLagged / _sumLagged2;

        // Calculate sum of squared regression errors in O(1)
        // Sum((Δε_t - γ ε_{t-1})^2) = Sum(Δε_t^2) - 2γ Sum(Δε_t ε_{t-1}) + γ^2 Sum(ε_{t-1}^2)
        double sumErrorSq = _sumDelta2 - (2.0 * gamma * _sumDeltaLagged) + (gamma * gamma * _sumLagged2);

        // Ensure non-negative due to floating point errors
        sumErrorSq = Max(0.0, sumErrorSq);

        double varError = sumErrorSq / (n - 1);
        double seGammaSq = varError / _sumLagged2;

        if (seGammaSq <= 0 || !double.IsFinite(seGammaSq))
        {
            return double.NaN;
        }

        double seGamma = Sqrt(seGammaSq);
        if (seGamma < Epsilon)
        {
            return double.NaN;
        }

        return gamma / seGamma;
    }

    /// <summary>Not supported. This indicator requires two input spans.</summary>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        throw new NotSupportedException("Cointegration requires two inputs.");
    }

    /// <inheritdoc />
    public override void Reset()
    {
        _bufferA.Clear();
        _bufferB.Clear();
        _deltaResiduals.Clear();
        _laggedResiduals.Clear();

        _sumA = 0;
        _sumB = 0;
        _sumA2 = 0;
        _sumB2 = 0;
        _sumAB = 0;

        _sumAComp = 0;
        _sumBComp = 0;
        _sumA2Comp = 0;
        _sumB2Comp = 0;
        _sumABComp = 0;

        _sumDeltaLagged = 0;
        _sumLagged2 = 0;
        _sumDelta2 = 0;

        _sumDeltaLaggedComp = 0;
        _sumLagged2Comp = 0;
        _sumDelta2Comp = 0;

        _prevResidual = 0;
        _p_prevResidual = 0;
        _hasPrevResidual = false;
        _p_hasPrevResidual = false;

        _lastValidA = 0;
        _lastValidB = 0;
        _p_lastValidA = 0;
        _p_lastValidB = 0;

        Last = default;
    }

    /// <summary>
    /// Calculates cointegration for two time series.
    /// </summary>
    public static TSeries Batch(TSeries seriesA, TSeries seriesB, int period = 20)
        => Calculate(seriesA, seriesB, period).Results;

    /// <summary>
    /// Static batch calculation for span-based processing.
    /// </summary>
    public static void Batch(
        ReadOnlySpan<double> seriesA,
        ReadOnlySpan<double> seriesB,
        Span<double> output,
        int period = 20)
    {
        if (seriesA.Length != seriesB.Length)
        {
            throw new ArgumentException("Series must have the same length", nameof(seriesB));
        }

        if (seriesA.Length != output.Length)
        {
            throw new ArgumentException("Output must have the same length as input", nameof(output));
        }

        if (period <= 1)
        {
            throw new ArgumentException("Period must be greater than 1", nameof(period));
        }

        var indicator = new Cointegration(period);

        for (int i = 0; i < seriesA.Length; i++)
        {
            var result = indicator.Update(seriesA[i], seriesB[i], isNew: true);
            output[i] = result.Value;
        }
    }

    /// <summary>
    /// Calculates the ADF cointegration statistic for two time series and returns both the result series and the live indicator instance.
    /// </summary>
    public static (TSeries Results, Cointegration Indicator) Calculate(TSeries seriesA, TSeries seriesB, int period = 20)
    {
        if (seriesA.Count != seriesB.Count)
        {
            throw new ArgumentException("Series must have the same length", nameof(seriesB));
        }

        var indicator = new Cointegration(period);
        var result = new TSeries(seriesA.Count);

        var timesA = seriesA.Times;
        var valuesA = seriesA.Values;
        var valuesB = seriesB.Values;

        for (int i = 0; i < seriesA.Count; i++)
        {
            result.Add(indicator.Update(new TValue(timesA[i], valuesA[i]), new TValue(timesA[i], valuesB[i]), isNew: true));
        }

        return (result, indicator);
    }
}
