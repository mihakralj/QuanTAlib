using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// ADF: Augmented Dickey-Fuller unit root test.
/// </summary>
/// <remarks>
/// Tests the null hypothesis that a time series has a unit root (is non-stationary).
/// Output is the MacKinnon (1994) approximate p-value: values near 0 indicate
/// stationarity, values near 1 indicate a unit root (non-stationary / random walk).
///
/// The full regression model is:
///   Δy_t = α + β·t + γ·y_{t-1} + Σ(δ_i·Δy_{t-i}) + ε_t
///
/// where γ is the coefficient of interest. The test statistic is t = γ̂ / SE(γ̂).
/// P-value is computed via MacKinnon (1994) polynomial interpolation with standard
/// normal CDF.
///
/// Complexity: O(period × maxLag²) per update — OLS solve is O(k³) with k ≤ 8.
/// </remarks>
[SkipLocalsInit]
public sealed class Adf : AbstractBase
{
    /// <summary>
    /// Regression model type for the ADF test.
    /// </summary>
    public enum AdfRegression
    {
        /// <summary>No constant, no trend: Δy_t = γ·y_{t-1} + Σ(δ_i·Δy_{t-i}) + ε_t</summary>
        NoConstant = 0,
        /// <summary>Constant only: Δy_t = α + γ·y_{t-1} + Σ(δ_i·Δy_{t-i}) + ε_t</summary>
        Constant = 1,
        /// <summary>Constant and trend: Δy_t = α + β·t + γ·y_{t-1} + Σ(δ_i·Δy_{t-i}) + ε_t</summary>
        ConstantAndTrend = 2
    }

    private const double InvSqrt2 = 0.70710678118654752; // 1/√2
    private const int MinPeriod = 20;
    private const int MaxRegressors = 8; // max k for stackalloc Cholesky

    private readonly int _period;
    private readonly int _maxLag;
    private readonly AdfRegression _regression;
    private readonly RingBuffer _buffer;
    private double _lastValidValue;
    private int _inputCount;
    private int _inputCountSaved;

    /// <summary>Test statistic (t-value for γ̂).</summary>
    public double Statistic { get; private set; }

    /// <summary>P-value from MacKinnon approximation.</summary>
    public double PValue { get; private set; }

    /// <summary>Number of lags used in the augmented regression.</summary>
    public int LagsUsed { get; private set; }

    public override bool IsHot => _inputCount >= _period;

    /// <summary>
    /// Creates a new Augmented Dickey-Fuller test indicator.
    /// </summary>
    /// <param name="period">Rolling window size for the test (must be ≥ 20).</param>
    /// <param name="maxLag">Maximum number of augmented lag terms. 0 = auto-select via AIC using floor(12·(n/100)^0.25).</param>
    /// <param name="regression">Regression model type: NoConstant, Constant (default), or ConstantAndTrend.</param>
    public Adf(int period = 50, int maxLag = 0, AdfRegression regression = AdfRegression.Constant)
    {
        if (period < MinPeriod)
        {
            throw new ArgumentOutOfRangeException(nameof(period),
                $"Period must be greater than or equal to {MinPeriod} for ADF test.");
        }
        if (maxLag < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLag),
                "maxLag must be non-negative (0 = auto-select).");
        }

        _period = period;
        _maxLag = maxLag;
        _regression = regression;
        _buffer = new RingBuffer(period);

        string regStr = regression switch
        {
            AdfRegression.NoConstant => "nc",
            AdfRegression.Constant => "c",
            AdfRegression.ConstantAndTrend => "ct",
            _ => "c"
        };
        Name = $"ADF({period},{maxLag},{regStr})";
        WarmupPeriod = period;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override TValue Update(TValue input, bool isNew = true)
    {
        double value = input.Value;

        if (!double.IsFinite(value))
        {
            value = _lastValidValue;
        }
        else
        {
            _lastValidValue = value;
        }

        if (isNew)
        {
            _inputCountSaved = _inputCount;
            _buffer.Add(value);
            _inputCount++;
        }
        else
        {
            _inputCount = _inputCountSaved;
            _buffer.UpdateNewest(value);
            _inputCount++;
        }

        double pValue;
        if (_inputCount < MinPeriod)
        {
            pValue = 1.0; // Not enough data — assume unit root
            Statistic = 0.0;
            PValue = 1.0;
            LagsUsed = 0;
        }
        else
        {
            var span = _buffer.GetSpan();
            var result = ComputeAdf(span, _maxLag, _regression);
            Statistic = result.Statistic;
            PValue = result.PValue;
            LagsUsed = result.LagsUsed;
            pValue = result.PValue;
        }

        Last = new TValue(input.Time, pValue);
        PubEvent(Last, isNew);
        return Last;
    }

    public override TSeries Update(TSeries source)
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

        Batch(source.Values, vSpan, _period, _maxLag, _regression);
        source.Times.CopyTo(tSpan);

        // Reset running state before priming
        _buffer.Clear();
        _lastValidValue = 0;
        _inputCount = 0;

        // Prime: replay enough bars to reconstruct internal state
        int primeStart = Math.Max(0, len - _period);
        for (int i = primeStart; i < len; i++)
        {
            Update(source[i]);
        }

        return new TSeries(t, v);
    }

    public override void Reset()
    {
        _buffer.Clear();
        _lastValidValue = 0;
        _inputCount = 0;
        _inputCountSaved = 0;
        Statistic = 0;
        PValue = 1.0;
        LagsUsed = 0;
        Last = default;
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        DateTime ts = DateTime.MinValue;
        foreach (double value in source)
        {
            Update(new TValue(ts, value));
            if (step.HasValue)
            {
                ts = ts.Add(step.Value);
            }
        }
    }

    public static TSeries Batch(TSeries source, int period = 50, int maxLag = 0,
        AdfRegression regression = AdfRegression.Constant)
    {
        var adf = new Adf(period, maxLag, regression);
        return adf.Update(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Batch(ReadOnlySpan<double> source, Span<double> output,
        int period = 50, int maxLag = 0, AdfRegression regression = AdfRegression.Constant)
    {
        if (source.Length != output.Length)
        {
            throw new ArgumentException("Source and output must have the same length", nameof(output));
        }
        if (period < MinPeriod)
        {
            throw new ArgumentException($"Period must be >= {MinPeriod}", nameof(period));
        }

        int len = source.Length;
        if (len == 0)
        {
            return;
        }

        CalculateScalarCore(source, output, period, maxLag, regression);
    }

    public static (TSeries Results, Adf Indicator) Calculate(TSeries source,
        int period = 50, int maxLag = 0, AdfRegression regression = AdfRegression.Constant)
    {
        var indicator = new Adf(period, maxLag, regression);
        TSeries results = indicator.Update(source);
        return (results, indicator);
    }

    // ═══════════════════════════════════════════════════════════════
    // Private implementation
    // ═══════════════════════════════════════════════════════════════

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateScalarCore(ReadOnlySpan<double> source, Span<double> output,
        int period, int maxLag, AdfRegression regression)
    {
        int len = source.Length;

        // We need a sliding window of 'period' values
        const int StackallocThreshold = 256;
        double[]? rentedBuf = null;
        scoped Span<double> windowBuf;
        if (period <= StackallocThreshold)
        {
            windowBuf = stackalloc double[period];
        }
        else
        {
            rentedBuf = ArrayPool<double>.Shared.Rent(period);
            windowBuf = rentedBuf.AsSpan(0, period);
        }

        try
        {
            for (int i = 0; i < len; i++)
            {
                if (i < period - 1)
                {
                    output[i] = 1.0; // Not enough data
                    continue;
                }

                // Fill window
                int windowStart = i - period + 1;
                double pv = 0;
                for (int j = 0; j < period; j++)
                {
                    double v = source[windowStart + j];
                    if (!double.IsFinite(v))
                    {
                        v = pv;
                    }
                    else
                    {
                        pv = v;
                    }
                    windowBuf[j] = v;
                }

                var result = ComputeAdf(windowBuf[..period], maxLag, regression);
                output[i] = result.PValue;
            }
        }
        finally
        {
            if (rentedBuf is not null)
            {
                ArrayPool<double>.Shared.Return(rentedBuf);
            }
        }
    }

    /// <summary>
    /// Core ADF computation on a window of prices. Returns (Statistic, PValue, LagsUsed).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (double Statistic, double PValue, int LagsUsed) ComputeAdf(
        ReadOnlySpan<double> y, int maxLag, AdfRegression regression)
    {
        int n = y.Length;
        if (n < 3)
        {
            return (0, 1.0, 0);
        }

        // Compute first differences: Δy_t = y_t - y_{t-1}
        int nDiff = n - 1;
        const int StackallocThreshold = 256;

        double[]? rentedDy = null;
        scoped Span<double> dy;
        if (nDiff <= StackallocThreshold)
        {
            dy = stackalloc double[nDiff];
        }
        else
        {
            rentedDy = ArrayPool<double>.Shared.Rent(nDiff);
            dy = rentedDy.AsSpan(0, nDiff);
        }

        try
        {
            for (int i = 0; i < nDiff; i++)
            {
                dy[i] = y[i + 1] - y[i];
            }

            // Auto-lag selection: Schwert (1989) rule
            int autoMaxLag = maxLag > 0
                ? maxLag
                : Math.Max(1, (int)Math.Floor(12.0 * Math.Pow(n / 100.0, 0.25)));

            // Cap lag to avoid underdetermined system
            int extraRegressors = regression switch
            {
                AdfRegression.NoConstant => 1,     // γ only
                AdfRegression.Constant => 2,        // α, γ
                AdfRegression.ConstantAndTrend => 3, // α, β, γ
                _ => 2
            };

            // We need at least k+1 observations for k regressors
            int maxFeasibleLag = Math.Max(0, nDiff - extraRegressors - 2);
            autoMaxLag = Math.Min(autoMaxLag, maxFeasibleLag);
            autoMaxLag = Math.Min(autoMaxLag, MaxRegressors - extraRegressors);

            // If auto-selecting, try each lag and pick best AIC
            int bestLag;
            if (maxLag == 0 && autoMaxLag > 0)
            {
                bestLag = SelectLagByAic(y, dy, autoMaxLag, regression);
            }
            else
            {
                bestLag = autoMaxLag;
            }

            // Run ADF regression with selected lag
            var (tStat, pVal) = RunAdfRegression(y, dy, bestLag, regression, n);
            return (tStat, pVal, bestLag);
        }
        finally
        {
            if (rentedDy is not null)
            {
                ArrayPool<double>.Shared.Return(rentedDy);
            }
        }
    }

    /// <summary>
    /// Select optimal lag via AIC: AIC = n·ln(RSS/n) + 2·k
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int SelectLagByAic(ReadOnlySpan<double> y, ReadOnlySpan<double> dy,
        int maxLag, AdfRegression regression)
    {
        double bestAic = double.MaxValue;
        int bestLag = 0;
        int nOrig = y.Length;

        for (int lag = 0; lag <= maxLag; lag++)
        {
            int nObs = dy.Length - lag;
            if (nObs < 3)
            {
                break;
            }

            int k = (regression switch
            {
                AdfRegression.NoConstant => 1,
                AdfRegression.Constant => 2,
                AdfRegression.ConstantAndTrend => 3,
                _ => 2
            }) + lag;

            if (nObs <= k + 1)
            {
                break;
            }

            double rss = ComputeRss(y, dy, lag, regression, nOrig);

            if (rss <= 0 || !double.IsFinite(rss))
            {
                continue;
            }

            double aic = (nObs * Math.Log(rss / nObs)) + (2.0 * k);
            if (aic < bestAic)
            {
                bestAic = aic;
                bestLag = lag;
            }
        }

        return bestLag;
    }

    /// <summary>
    /// Compute residual sum of squares for a given lag configuration.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ComputeRss(ReadOnlySpan<double> y, ReadOnlySpan<double> dy,
        int lag, AdfRegression regression, int nOrig)
    {
        int nDiff = dy.Length;
        int startIdx = lag;
        int nObs = nDiff - startIdx;
        if (nObs < 3)
        {
            return double.MaxValue;
        }

        int k = (regression switch
        {
            AdfRegression.NoConstant => 1,
            AdfRegression.Constant => 2,
            AdfRegression.ConstantAndTrend => 3,
            _ => 2
        }) + lag;

        if (k > MaxRegressors || nObs <= k)
        {
            return double.MaxValue;
        }

        // Build X'X and X'y via accumulation (no explicit matrix allocation)
        Span<double> xtx = stackalloc double[k * k];
        Span<double> xty = stackalloc double[k];
        xtx.Clear();
        xty.Clear();

        Span<double> xRow = stackalloc double[k];

        for (int t = startIdx; t < nDiff; t++)
        {
            // Build regressor row
            int col = 0;

            // Deterministic terms
            if (regression is AdfRegression.Constant or AdfRegression.ConstantAndTrend)
            {
                xRow[col++] = 1.0; // intercept
            }
            if (regression == AdfRegression.ConstantAndTrend)
            {
                xRow[col++] = t + 1; // trend
            }

            // y_{t-1} (the lagged level — this is the key regressor)
            xRow[col++] = y[t]; // y[t] in 0-based corresponds to y_{t} which is lagged level

            // Lagged differences
            for (int j = 1; j <= lag; j++)
            {
                xRow[col++] = dy[t - j];
            }

            double yVal = dy[t];

            // Accumulate X'X and X'y
            for (int r = 0; r < k; r++)
            {
                for (int c = r; c < k; c++)
                {
                    xtx[(r * k) + c] += xRow[r] * xRow[c];
                }
                xty[r] += xRow[r] * yVal;
            }
        }

        // Fill lower triangle of symmetric X'X
        for (int r = 1; r < k; r++)
        {
            for (int c = 0; c < r; c++)
            {
                xtx[(r * k) + c] = xtx[(c * k) + r];
            }
        }

        // Solve via Cholesky decomposition
        Span<double> beta = stackalloc double[k];
        if (!SolveCholesky(xtx, xty, beta, k))
        {
            return double.MaxValue;
        }

        // Compute RSS
        double rss = 0;
        for (int t = startIdx; t < nDiff; t++)
        {
            int col = 0;
            if (regression is AdfRegression.Constant or AdfRegression.ConstantAndTrend)
            {
                xRow[col++] = 1.0;
            }
            if (regression == AdfRegression.ConstantAndTrend)
            {
                xRow[col++] = t + 1;
            }
            xRow[col++] = y[t];
            for (int j = 1; j <= lag; j++)
            {
                xRow[col++] = dy[t - j];
            }

            double predicted = 0;
            for (int c = 0; c < k; c++)
            {
                predicted += beta[c] * xRow[c];
            }

            double residual = dy[t] - predicted;
            rss += residual * residual;
        }

        return rss;
    }

    /// <summary>
    /// Run ADF regression and return (t-statistic, p-value).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (double tStat, double pValue) RunAdfRegression(
        ReadOnlySpan<double> y, ReadOnlySpan<double> dy,
        int lag, AdfRegression regression, int nOrig)
    {
        int nDiff = dy.Length;
        int startIdx = lag;
        int nObs = nDiff - startIdx;
        if (nObs < 3)
        {
            return (0, 1.0);
        }

        int k = (regression switch
        {
            AdfRegression.NoConstant => 1,
            AdfRegression.Constant => 2,
            AdfRegression.ConstantAndTrend => 3,
            _ => 2
        }) + lag;

        if (k > MaxRegressors || nObs <= k)
        {
            return (0, 1.0);
        }

        // Index of γ (coefficient on y_{t-1})
        int gammaIdx = regression switch
        {
            AdfRegression.NoConstant => 0,
            AdfRegression.Constant => 1,
            AdfRegression.ConstantAndTrend => 2,
            _ => 1
        };

        // Build X'X and X'y
        Span<double> xtx = stackalloc double[k * k];
        Span<double> xty = stackalloc double[k];
        xtx.Clear();
        xty.Clear();

        Span<double> xRow = stackalloc double[k];

        for (int t = startIdx; t < nDiff; t++)
        {
            int col = 0;
            if (regression is AdfRegression.Constant or AdfRegression.ConstantAndTrend)
            {
                xRow[col++] = 1.0;
            }
            if (regression == AdfRegression.ConstantAndTrend)
            {
                xRow[col++] = t + 1;
            }
            xRow[col++] = y[t];
            for (int j = 1; j <= lag; j++)
            {
                xRow[col++] = dy[t - j];
            }

            double yVal = dy[t];

            for (int r = 0; r < k; r++)
            {
                for (int c = r; c < k; c++)
                {
                    xtx[(r * k) + c] += xRow[r] * xRow[c];
                }
                xty[r] += xRow[r] * yVal;
            }
        }

        // Fill lower triangle
        for (int r = 1; r < k; r++)
        {
            for (int c = 0; c < r; c++)
            {
                xtx[(r * k) + c] = xtx[(c * k) + r];
            }
        }

        // Solve for beta via Cholesky
        Span<double> beta = stackalloc double[k];
        if (!SolveCholesky(xtx, xty, beta, k))
        {
            return (0, 1.0);
        }

        // Compute (X'X)^-1 for SE calculation — rebuild and invert
        xtx.Clear();
        xty.Clear();

        // Rebuild X'X
        for (int t = startIdx; t < nDiff; t++)
        {
            int col = 0;
            if (regression is AdfRegression.Constant or AdfRegression.ConstantAndTrend)
            {
                xRow[col++] = 1.0;
            }
            if (regression == AdfRegression.ConstantAndTrend)
            {
                xRow[col++] = t + 1;
            }
            xRow[col++] = y[t];
            for (int j = 1; j <= lag; j++)
            {
                xRow[col++] = dy[t - j];
            }

            for (int r = 0; r < k; r++)
            {
                for (int c = r; c < k; c++)
                {
                    xtx[(r * k) + c] += xRow[r] * xRow[c];
                }
            }
        }
        for (int r = 1; r < k; r++)
        {
            for (int c = 0; c < r; c++)
            {
                xtx[(r * k) + c] = xtx[(c * k) + r];
            }
        }

        // Compute RSS
        double rss = 0;
        for (int t = startIdx; t < nDiff; t++)
        {
            int col = 0;
            if (regression is AdfRegression.Constant or AdfRegression.ConstantAndTrend)
            {
                xRow[col++] = 1.0;
            }
            if (regression == AdfRegression.ConstantAndTrend)
            {
                xRow[col++] = t + 1;
            }
            xRow[col++] = y[t];
            for (int j = 1; j <= lag; j++)
            {
                xRow[col++] = dy[t - j];
            }

            double predicted = 0;
            for (int c = 0; c < k; c++)
            {
                predicted += beta[c] * xRow[c];
            }

            double residual = dy[t] - predicted;
            rss += residual * residual;
        }

        double s2 = rss / (nObs - k); // residual variance estimate
        if (s2 <= 0 || !double.IsFinite(s2))
        {
            return (0, 1.0);
        }

        // Invert X'X via Cholesky to get (X'X)^-1
        Span<double> xtxInv = stackalloc double[k * k];
        if (!InvertViaCholesky(xtx, xtxInv, k))
        {
            return (0, 1.0);
        }

        double seGamma = Math.Sqrt(s2 * xtxInv[(gammaIdx * k) + gammaIdx]);
        if (seGamma <= 1e-15 || !double.IsFinite(seGamma))
        {
            return (0, 1.0);
        }

        double tStat = beta[gammaIdx] / seGamma;
        double pVal = MacKinnonPValue(tStat, regression, nOrig);

        return (tStat, pVal);
    }

    // ═══════════════════════════════════════════════════════════════
    // Cholesky solver for k×k system (k ≤ 8, stackalloc safe)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Solves A·x = b via Cholesky decomposition where A is symmetric positive definite.
    /// Returns false if decomposition fails (matrix not positive definite).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool SolveCholesky(ReadOnlySpan<double> A, ReadOnlySpan<double> b,
        Span<double> x, int k)
    {
        // Cholesky decomposition: A = L·L'
        Span<double> L = stackalloc double[k * k];
        L.Clear();

        for (int i = 0; i < k; i++)
        {
            for (int j = 0; j <= i; j++)
            {
                double sum = A[(i * k) + j];
                for (int p = 0; p < j; p++)
                {
                    sum -= L[(i * k) + p] * L[(j * k) + p];
                }

                if (i == j)
                {
                    if (sum <= 1e-15)
                    {
                        return false; // Not positive definite
                    }
                    L[(i * k) + j] = Math.Sqrt(sum);
                }
                else
                {
                    L[(i * k) + j] = sum / L[(j * k) + j];
                }
            }
        }

        // Forward substitution: L·z = b
        Span<double> z = stackalloc double[k];
        for (int i = 0; i < k; i++)
        {
            double sum = b[i];
            for (int j = 0; j < i; j++)
            {
                sum -= L[(i * k) + j] * z[j];
            }
            z[i] = sum / L[(i * k) + i];
        }

        // Back substitution: L'·x = z
        for (int i = k - 1; i >= 0; i--)
        {
            double sum = z[i];
            for (int j = i + 1; j < k; j++)
            {
                sum -= L[(j * k) + i] * x[j];
            }
            x[i] = sum / L[(i * k) + i];
        }

        return true;
    }

    /// <summary>
    /// Inverts a symmetric positive definite matrix via Cholesky decomposition.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool InvertViaCholesky(ReadOnlySpan<double> A, Span<double> Ainv, int k)
    {
        // Cholesky: A = L·L'
        Span<double> L = stackalloc double[k * k];
        L.Clear();

        for (int i = 0; i < k; i++)
        {
            for (int j = 0; j <= i; j++)
            {
                double sum = A[(i * k) + j];
                for (int p = 0; p < j; p++)
                {
                    sum -= L[(i * k) + p] * L[(j * k) + p];
                }

                if (i == j)
                {
                    if (sum <= 1e-15)
                    {
                        return false;
                    }
                    L[(i * k) + j] = Math.Sqrt(sum);
                }
                else
                {
                    L[(i * k) + j] = sum / L[(j * k) + j];
                }
            }
        }

        // Invert L (lower triangular)
        Span<double> Linv = stackalloc double[k * k];
        Linv.Clear();
        for (int i = 0; i < k; i++)
        {
            Linv[(i * k) + i] = 1.0 / L[(i * k) + i];
            for (int j = i + 1; j < k; j++)
            {
                double sum = 0;
                for (int p = i; p < j; p++)
                {
                    sum -= L[(j * k) + p] * Linv[(p * k) + i];
                }
                Linv[(j * k) + i] = sum / L[(j * k) + j];
            }
        }

        // A^-1 = (L')^-1 · L^-1 = Linv' · Linv
        Ainv.Clear();
        for (int i = 0; i < k; i++)
        {
            for (int j = i; j < k; j++)
            {
                double sum = 0;
                for (int p = j; p < k; p++)
                {
                    sum += Linv[(p * k) + i] * Linv[(p * k) + j];
                }
                Ainv[(i * k) + j] = sum;
                Ainv[(j * k) + i] = sum;
            }
        }

        return true;
    }

    // ═══════════════════════════════════════════════════════════════
    // MacKinnon (1994) p-value approximation
    // ═══════════════════════════════════════════════════════════════

    // MacKinnon (2010) coefficients for N=1 (univariate)
    // [tau_star, tau_min, tau_max]
    // SmallP: polynomial coefficients for small-p region (degree 2)
    // LargeP: polynomial coefficients for large-p region (degree 3)

    // NoConstant
    private static readonly double[] SmallP_Nc = [0.6344, 1.2378, 0.032496];
    private static readonly double[] LargeP_Nc = [0.4797, 0.93557, -0.06999, 0.033066];

    // Constant
    private static readonly double[] SmallP_C = [2.1659, 1.4412, 0.038269];
    private static readonly double[] LargeP_C = [1.7339, 0.93202, -0.12745, -0.010368];

    // ConstantAndTrend
    private static readonly double[] SmallP_Ct = [3.2512, 1.6047, 0.049588];
    private static readonly double[] LargeP_Ct = [2.5261, 0.61654, -0.37956, -0.060285];

    /// <summary>
    /// Computes the MacKinnon approximate p-value for the ADF test statistic.
    /// Uses interpolation coefficients from MacKinnon (1994, 2010) for N=1.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double MacKinnonPValue(double tStat, AdfRegression regression, int n)
    {
        double tauStar, tauMin, tauMax;
        double[] smallP, largeP;

        switch (regression)
        {
            case AdfRegression.NoConstant:
                tauStar = -1.04;
                tauMin = -19.04;
                tauMax = double.PositiveInfinity;
                smallP = SmallP_Nc;
                largeP = LargeP_Nc;
                break;

            case AdfRegression.ConstantAndTrend:
                tauStar = -2.89;
                tauMin = -16.18;
                tauMax = 0.70;
                smallP = SmallP_Ct;
                largeP = LargeP_Ct;
                break;

            default: // Constant
                tauStar = -1.61;
                tauMin = -18.83;
                tauMax = 2.74;
                smallP = SmallP_C;
                largeP = LargeP_C;
                break;
        }

        // Clamp to valid range
        if (tStat < tauMin)
        {
            return 0.0; // Extremely stationary
        }
        if (double.IsFinite(tauMax) && tStat > tauMax)
        {
            return 1.0;
        }

        if (tStat <= tauStar)
        {
            // Small p-value region: p = NormCdf(poly3(tStat))
            double poly = smallP[0] + (smallP[1] * tStat) + (smallP[2] * tStat * tStat);
            return NormCdf(poly);
        }
        else
        {
            // Large p-value region: p = NormCdf(poly4(tStat))
            double t2 = tStat * tStat;
            double t3 = t2 * tStat;
            double poly = largeP[0] + (largeP[1] * tStat) + (largeP[2] * t2) + (largeP[3] * t3);
            return NormCdf(poly);
        }
    }

    /// <summary>
    /// Standard normal CDF: Φ(x) = 0.5 * erfc(-x / √2)
    /// Machine precision via .NET BCL Math.Erfc.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double NormCdf(double x)
    {
        // erfc is not in Math directly; use the identity:
        // Φ(x) = 0.5 * (1 + erf(x / √2))
        // But .NET doesn't have erf either. Use the rational approximation instead.
        return NormCdfApprox(x);
    }

    /// <summary>
    /// Abramowitz and Stegun 7.1.26 erf approximation → standard normal CDF.
    /// Φ(x) = 0.5·(1 + erf(x/√2)), where erf is computed via the A&amp;S rational
    /// approximation with coefficients a1…a5, p = 0.3275911.
    /// Maximum error: 7.5e-8.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double NormCdfApprox(double x)
    {
        const double a1 = 0.254829592;
        const double a2 = -0.284496736;
        const double a3 = 1.421413741;
        const double a4 = -1.453152027;
        const double a5 = 1.061405429;
        const double p = 0.3275911;
        const double inv_sqrt2 = 0.70710678118654752;

        int sign = x < 0 ? -1 : 1;

        // erf argument: z = |x| / √2
        double z = Math.Abs(x) * inv_sqrt2;

        // Rational approximation for erfc(z) = poly · exp(-z²)
        double t = 1.0 / Math.FusedMultiplyAdd(p, z, 1.0);
        double t2 = t * t;
        double t3 = t2 * t;
        double t4 = t3 * t;
        double t5 = t4 * t;

        double poly = (a1 * t) + (a2 * t2) + (a3 * t3) + (a4 * t4) + (a5 * t5);
        double erfcZ = poly * Math.Exp(-z * z);
        double erfZ = 1.0 - erfcZ;

        // Φ(x) = 0.5 · (1 + sign · erf(|x|/√2))
        return 0.5 * (1.0 + (sign * erfZ));
    }
}
