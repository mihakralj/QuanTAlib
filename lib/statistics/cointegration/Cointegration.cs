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

    // Residual tracking
    private double _prevResidual;
    private double _p_prevResidual;
    private bool _hasPrevResidual;
    private bool _p_hasPrevResidual;

    // ADF regression running sums (period-1 window)
    private readonly RingBuffer _deltaResiduals;
    private readonly RingBuffer _laggedResiduals;
    private double _sumDelta, _sumLagged;
    private double _sumDeltaLagged, _sumLagged2;

    // Last valid values for NaN handling
    private double _lastValidA, _lastValidB;
    private double _p_lastValidA, _p_lastValidB;

    private int _updateCount;
    private const int ResyncInterval = 1000;
    private const double Epsilon = 1e-10;

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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(double seriesA, double seriesB, bool isNew = true)
    {
        return Update(new TValue(DateTime.UtcNow, seriesA), new TValue(DateTime.UtcNow, seriesB), isNew);
    }

    /// <inheritdoc/>
    /// <remarks>Not supported for bi-input indicator. Use Update(seriesA, seriesB) instead.</remarks>
    public override TValue Update(TValue input, bool isNew = true)
    {
        throw new NotSupportedException("Cointegration requires two inputs (seriesA and seriesB). Use Update(seriesA, seriesB).");
    }

    /// <inheritdoc/>
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

        // Update main buffers
        if (_bufferA.IsFull)
        {
            double oldA = _bufferA.Oldest;
            double oldB = _bufferB.Oldest;
            _sumA -= oldA;
            _sumB -= oldB;
            _sumA2 = FusedMultiplyAdd(-oldA, oldA, _sumA2);
            _sumB2 = FusedMultiplyAdd(-oldB, oldB, _sumB2);
            _sumAB = FusedMultiplyAdd(-oldA, oldB, _sumAB);
        }

        _bufferA.Add(a);
        _bufferB.Add(b);

        _sumA += a;
        _sumB += b;
        _sumA2 = FusedMultiplyAdd(a, a, _sumA2);
        _sumB2 = FusedMultiplyAdd(b, b, _sumB2);
        _sumAB = FusedMultiplyAdd(a, b, _sumAB);

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
                _sumDelta -= oldDelta;
                _sumLagged -= oldLagged;
                _sumDeltaLagged = FusedMultiplyAdd(-oldDelta, oldLagged, _sumDeltaLagged);
                _sumLagged2 = FusedMultiplyAdd(-oldLagged, oldLagged, _sumLagged2);
            }

            _deltaResiduals.Add(delta);
            _laggedResiduals.Add(lagged);

            _sumDelta += delta;
            _sumLagged += lagged;
            _sumDeltaLagged = FusedMultiplyAdd(delta, lagged, _sumDeltaLagged);
            _sumLagged2 = FusedMultiplyAdd(lagged, lagged, _sumLagged2);
        }

        _prevResidual = residual;
        _hasPrevResidual = true;

        _updateCount++;
        if (_updateCount % ResyncInterval == 0)
        {
            Resync();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessBarCorrection(double a, double b)
    {
        // Restore state
        _lastValidA = _p_lastValidA;
        _lastValidB = _p_lastValidB;
        _prevResidual = _p_prevResidual;
        _hasPrevResidual = _p_hasPrevResidual;

        // Update newest values in main buffers
        if (_bufferA.Count > 0)
        {
            double oldA = _bufferA.Newest;
            double oldB = _bufferB.Newest;

            _sumA = FusedMultiplyAdd(1.0, a, FusedMultiplyAdd(-1.0, oldA, _sumA));
            _sumB = FusedMultiplyAdd(1.0, b, FusedMultiplyAdd(-1.0, oldB, _sumB));
            _sumA2 = FusedMultiplyAdd(a, a, FusedMultiplyAdd(-oldA, oldA, _sumA2));
            _sumB2 = FusedMultiplyAdd(b, b, FusedMultiplyAdd(-oldB, oldB, _sumB2));
            _sumAB = FusedMultiplyAdd(a, b, FusedMultiplyAdd(-oldA, oldB, _sumAB));

            _bufferA.UpdateNewest(a);
            _bufferB.UpdateNewest(b);
        }
        else
        {
            _bufferA.Add(a);
            _bufferB.Add(b);
            _sumA = a;
            _sumB = b;
            _sumA2 = a * a;
            _sumB2 = b * b;
            _sumAB = a * b;
        }

        // Calculate current residual
        double residual = CalculateResidual(a, b);

        // Update ADF regression buffers
        if (_hasPrevResidual)
        {
            double delta = residual - _prevResidual;
            double lagged = _prevResidual;

            if (_deltaResiduals.Count > 0)
            {
                double oldDelta = _deltaResiduals.Newest;
                double oldLagged = _laggedResiduals.Newest;

                _sumDelta = FusedMultiplyAdd(1.0, delta, FusedMultiplyAdd(-1.0, oldDelta, _sumDelta));
                _sumLagged = FusedMultiplyAdd(1.0, lagged, FusedMultiplyAdd(-1.0, oldLagged, _sumLagged));
                _sumDeltaLagged = FusedMultiplyAdd(delta, lagged, FusedMultiplyAdd(-oldDelta, oldLagged, _sumDeltaLagged));
                _sumLagged2 = FusedMultiplyAdd(lagged, lagged, FusedMultiplyAdd(-oldLagged, oldLagged, _sumLagged2));

                _deltaResiduals.UpdateNewest(delta);
                _laggedResiduals.UpdateNewest(lagged);
            }
            else
            {
                _deltaResiduals.Add(delta);
                _laggedResiduals.Add(lagged);
                _sumDelta = delta;
                _sumLagged = lagged;
                _sumDeltaLagged = delta * lagged;
                _sumLagged2 = lagged * lagged;
            }
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

        // Calculate variances and covariance
        double varA = Max(0.0, (_sumA2 / n) - (meanA * meanA));
        double varB = Max(0.0, (_sumB2 / n) - (meanB * meanB));
        double cov = (_sumAB / n) - (meanA * meanB);

        // Calculate standard deviations
        double stdA = Sqrt(varA);
        double stdB = Sqrt(varB);

        // Calculate correlation
        double correlation = 0.0;
        double denom = stdA * stdB;
        if (Abs(denom) > Epsilon)
        {
            correlation = cov / denom;
        }

        // Calculate beta and alpha
        double beta = 0.0;
        if (Abs(stdB) > Epsilon)
        {
            beta = correlation * (stdA / stdB);
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

        // Calculate gamma (coefficient in ADF regression)
        // Δε_t = γ × ε_{t-1} + u_t
        // γ = Cov(Δε, ε_{t-1}) / Var(ε_{t-1})

        double meanDelta = _sumDelta / n;
        double meanLagged = _sumLagged / n;

        // Variance of lagged residuals
        double varLagged = (_sumLagged2 / n) - (meanLagged * meanLagged);
        if (Abs(varLagged) < Epsilon)
        {
            return double.NaN;
        }

        // Covariance of delta and lagged
        double covDeltaLagged = (_sumDeltaLagged / n) - (meanDelta * meanLagged);

        // Gamma coefficient
        double gamma = covDeltaLagged / varLagged;

        // Calculate standard error of gamma
        // SE(γ) = sqrt(Var(u) / (n × Var(ε_{t-1})))
        // where u_t = Δε_t - γ × ε_{t-1}

        // Calculate sum of squared regression errors
        double sumErrorSq = 0.0;
        for (int i = 0; i < n; i++)
        {
            double delta = _deltaResiduals[i];
            double lagged = _laggedResiduals[i];
            double error = delta - (gamma * lagged);
            sumErrorSq = FusedMultiplyAdd(error, error, sumErrorSq);
        }

        double varError = sumErrorSq / n;
        double seGammaSq = varError / (n * varLagged);

        if (seGammaSq <= 0 || !double.IsFinite(seGammaSq))
        {
            return double.NaN;
        }

        double seGamma = Sqrt(seGammaSq);
        if (Abs(seGamma) < Epsilon)
        {
            return double.NaN;
        }

        return gamma / seGamma;
    }

    private void Resync()
    {
        // Resync main buffer sums
        _sumA = 0;
        _sumB = 0;
        _sumA2 = 0;
        _sumB2 = 0;
        _sumAB = 0;

        for (int i = 0; i < _bufferA.Count; i++)
        {
            double a = _bufferA[i];
            double b = _bufferB[i];
            _sumA += a;
            _sumB += b;
            _sumA2 = FusedMultiplyAdd(a, a, _sumA2);
            _sumB2 = FusedMultiplyAdd(b, b, _sumB2);
            _sumAB = FusedMultiplyAdd(a, b, _sumAB);
        }

        // Resync ADF regression sums
        _sumDelta = 0;
        _sumLagged = 0;
        _sumDeltaLagged = 0;
        _sumLagged2 = 0;

        for (int i = 0; i < _deltaResiduals.Count; i++)
        {
            double delta = _deltaResiduals[i];
            double lagged = _laggedResiduals[i];
            _sumDelta += delta;
            _sumLagged += lagged;
            _sumDeltaLagged = FusedMultiplyAdd(delta, lagged, _sumDeltaLagged);
            _sumLagged2 = FusedMultiplyAdd(lagged, lagged, _sumLagged2);
        }
    }

    /// <inheritdoc/>
    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        throw new NotSupportedException("Cointegration requires two inputs.");
    }

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

        _sumDelta = 0;
        _sumLagged = 0;
        _sumDeltaLagged = 0;
        _sumLagged2 = 0;

        _prevResidual = 0;
        _p_prevResidual = 0;
        _hasPrevResidual = false;
        _p_hasPrevResidual = false;

        _lastValidA = 0;
        _lastValidB = 0;
        _p_lastValidA = 0;
        _p_lastValidB = 0;

        _updateCount = 0;
        Last = default;
    }

    /// <summary>
    /// Calculates cointegration for two time series.
    /// </summary>
    public static TSeries Batch(TSeries seriesA, TSeries seriesB, int period = 20)
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
            var tvalA = new TValue(timesA[i], valuesA[i]);
            var tvalB = new TValue(timesA[i], valuesB[i]);
            result.Add(indicator.Update(tvalA, tvalB, isNew: true));
        }

        return result;
    }

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

    public static (TSeries Results, Cointegration Indicator) Calculate(TSeries seriesA, TSeries seriesB, int period = 20)
    {
        var indicator = new Cointegration(period);
        TSeries results = Batch(seriesA, seriesB, period);
        return (results, indicator);
    }

}
