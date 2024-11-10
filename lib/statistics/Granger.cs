using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// GRANGER: Granger Causality Test
/// A statistical test to determine whether one time series is useful in forecasting another.
/// Tests if past values of X help predict future values of Y beyond Y's own past values.
/// Returns a value between 0 and 1 representing the probability that X does not Granger-cause Y.
/// </summary>
/// <remarks>
/// The Granger Causality calculation process:
/// 1. Fits two regression models:
///    - Restricted model: Y(t) = α₀ + Σ(β₁ᵢY(t-i)) + ε(t)
///    - Unrestricted model: Y(t) = α₀ + Σ(β₁ᵢY(t-i)) + Σ(β₂ᵢX(t-i)) + ε(t)
/// 2. Calculates F-statistic comparing the models
/// 3. Computes p-value from F-distribution
///
/// Key characteristics:
/// - Tests predictive causality, not true causation
/// - Sensitive to lag selection
/// - Assumes stationarity of time series
/// - Useful for lead/lag relationship analysis
///
/// Formula:
/// F = ((RSS₁ - RSS₂)/p) / (RSS₂/(n-2p-1))
/// where:
/// RSS₁ = residual sum of squares from restricted model
/// RSS₂ = residual sum of squares from unrestricted model
/// p = number of lags
/// n = number of observations
///
/// Market Applications:
/// - Lead/lag analysis between markets
/// - Price discovery analysis
/// - Market efficiency testing
/// - Intermarket analysis
/// - Risk spillover detection
///
/// Sources:
///     https://en.wikipedia.org/wiki/Granger_causality
///     "Investigating Causal Relations by Econometric Models and Cross-spectral Methods" - C.W.J. Granger
///
/// Note: Assumes linear relationships and stationarity
/// </remarks>
[SkipLocalsInit]
public sealed class Granger : AbstractBase
{
    private readonly int Lags;
    private readonly CircularBuffer _xValues;
    private readonly CircularBuffer _yValues;
    private const double Epsilon = 1e-10;
    private const int MinimumLags = 1;

    /// <param name="lags">The number of lags to use in the Granger causality test.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when lags is less than 1.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Granger(int lags)
    {
        if (lags < MinimumLags)
        {
            throw new ArgumentOutOfRangeException(nameof(lags),
                "Number of lags must be at least 1 for Granger causality test.");
        }
        Lags = lags;
        WarmupPeriod = lags + 1;
        _xValues = new CircularBuffer(lags * 2); // Need extra space for lagged values
        _yValues = new CircularBuffer(lags * 2);
        Name = $"Granger(lags={lags})";
        Init();
    }

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="lags">The number of lags to use in the Granger causality test.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Granger(object source, int lags) : this(lags)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Init()
    {
        base.Init();
        _xValues.Clear();
        _yValues.Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Input.Value;
            _index++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private double CalculateRSS(ReadOnlySpan<double> y, ReadOnlySpan<double> yhat)
    {
        double rss = 0;
        for (int i = 0; i < y.Length; i++)
        {
            double residual = y[i] - yhat[i];
            rss += residual * residual;
        }
        return rss;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void FitOLS(ReadOnlySpan<double> y, ReadOnlySpan<double> x, Span<double> beta)
    {
        // Simple OLS implementation for y = Xβ + ε
        int n = y.Length;
        int k = beta.Length;

        // Create X matrix (including constant term)
        var X = new double[n, k];
        for (int i = 0; i < n; i++)
        {
            X[i, 0] = 1.0; // Constant term
            for (int j = 1; j < k; j++)
            {
                X[i, j] = x[i * (k - 1) + (j - 1)];
            }
        }

        // Calculate β = (X'X)⁻¹X'y
        var XtX = new double[k, k];
        var Xty = new double[k];

        // Calculate X'X and X'y
        for (int i = 0; i < k; i++)
        {
            for (int j = 0; j < k; j++)
            {
                double sum = 0;
                for (int l = 0; l < n; l++)
                {
                    sum += X[l, i] * X[l, j];
                }
                XtX[i, j] = sum;
            }

            double sum2 = 0;
            for (int l = 0; l < n; l++)
            {
                sum2 += X[l, i] * y[l];
            }
            Xty[i] = sum2;
        }

        // Solve system of equations
        for (int i = 0; i < k; i++)
        {
            double pivot = XtX[i, i];
            if (Math.Abs(pivot) > Epsilon)
            {
                for (int j = 0; j < k; j++)
                {
                    XtX[i, j] /= pivot;
                }
                Xty[i] /= pivot;

                for (int j = 0; j < k; j++)
                {
                    if (i != j)
                    {
                        double factor = XtX[j, i];
                        for (int l = 0; l < k; l++)
                        {
                            XtX[j, l] -= factor * XtX[i, l];
                        }
                        Xty[j] -= factor * Xty[i];
                    }
                }
            }
        }

        // Copy results to beta
        for (int i = 0; i < k; i++)
        {
            beta[i] = Xty[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private double CalculateFStatistic(double rss1, double rss2, int n, int p)
    {
        // Calculate F-statistic
        double numerator = (rss1 - rss2) / p;
        double denominator = rss2 / (n - 2 * p - 1);
        return numerator / denominator;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double FDistributionPValue(double f, int df1, int df2)
    {
        // Approximate p-value from F-distribution
        // Using a simplified approximation for performance
        double v = df2 / (df2 + df1 * f);
        return Math.Pow(v, df2 / 2.0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        _xValues.Add(Input.Value, Input.IsNew);
        _yValues.Add(Input2.Value, Input.IsNew);

        double pValue = 1.0; // Null hypothesis: X does not Granger-cause Y

        if (_xValues.Count >= WarmupPeriod && _yValues.Count >= WarmupPeriod)
        {
            int n = _xValues.Count - Lags;
            if (n > 2 * Lags + 1)
            {
                ReadOnlySpan<double> x = _xValues.GetSpan();
                ReadOnlySpan<double> y = _yValues.GetSpan();

                // Prepare data for regression
                var yData = y.Slice(Lags, n).ToArray();
                var restricted = new double[Lags + 1];
                var unrestricted = new double[2 * Lags + 1];

                // Fit restricted model (only Y lags)
                FitOLS(yData, y.Slice(0, n), restricted);

                // Calculate RSS for restricted model
                var yhatRestricted = new double[n];
                for (int i = 0; i < n; i++)
                {
                    yhatRestricted[i] = restricted[0];
                    for (int j = 0; j < Lags; j++)
                    {
                        yhatRestricted[i] += restricted[j + 1] * y[i + Lags - j - 1];
                    }
                }
                double rss1 = CalculateRSS(yData, yhatRestricted);

                // Fit unrestricted model (Y and X lags)
                FitOLS(yData, x.Slice(0, n), unrestricted);

                // Calculate RSS for unrestricted model
                var yhatUnrestricted = new double[n];
                for (int i = 0; i < n; i++)
                {
                    yhatUnrestricted[i] = unrestricted[0];
                    for (int j = 0; j < Lags; j++)
                    {
                        yhatUnrestricted[i] += unrestricted[j + 1] * y[i + Lags - j - 1];
                        yhatUnrestricted[i] += unrestricted[j + Lags + 1] * x[i + Lags - j - 1];
                    }
                }
                double rss2 = CalculateRSS(yData, yhatUnrestricted);

                // Calculate F-statistic and p-value
                if (rss2 > Epsilon)
                {
                    double f = CalculateFStatistic(rss1, rss2, n, Lags);
                    pValue = FDistributionPValue(f, Lags, n - 2 * Lags - 1);
                }
            }
        }

        IsHot = _xValues.Count >= WarmupPeriod && _yValues.Count >= WarmupPeriod;
        return pValue;
    }
}
