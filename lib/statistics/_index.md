# Statistics

> "All models are wrong, but some are useful." — George Box

Statistical tools applied to price and returns. These indicators quantify relationships, measure dispersion, test hypotheses. Unlike momentum or trend indicators, statistics describe the data itself.

| Indicator | Full Name | Description |
| :--- | :--- | :--- |
| [ACF](acf/Acf.md) | Autocorrelation Function | Correlation of time series with lagged copy. For ARMA model identification. |
| [BETA](beta/Beta.md) | Beta Coefficient | Asset volatility relative to market. β=1 means market-matched risk. |
| [CMA](cma/Cma.md) | Cumulative Moving Average | Running average of all values. Welford's algorithm. No window. |
| [COINTEGRATION](cointegration/Cointegration.md) | Cointegration | Tests if series share long-term equilibrium. Pairs trading foundation. |
| [CORRELATION](correlation/Correlation.md) | Correlation | Linear relationship between two variables. Range: -1 to +1. |
| [COVARIANCE](covariance/Covariance.md) | Covariance | Joint variability of two random variables. Building block for β. |
| [ENTROPY](entropy/Entropy.md) | Shannon Entropy | Measures uncertainty/randomness. Higher entropy = less predictable. |
| [GEOMEAN](geomean/Geomean.md) | Geometric Mean | nth root of product. Use for growth rates and ratios. |
| [GRANGER](granger/Granger.md) | Granger Causality | Tests if one series helps predict another. Not true causality. |
| [HARMEAN](harmean/Harmean.md) | Harmonic Mean | Reciprocal of arithmetic mean of reciprocals. For rates/ratios. |
| [HURST](hurst/Hurst.md) | Hurst Exponent | Long-term memory. H>0.5: trending. H<0.5: mean-reverting. |
| [IQR](iqr/Iqr.md) | Interquartile Range | P75 - P25. Robust dispersion measure. |
| [JB](jb/Jb.md) | Jarque-Bera Test | Normality test using skewness and kurtosis. |
| [KENDALL](kendall/Kendall.md) | Kendall Rank Correlation | Ordinal association. Robust to outliers. |
| [KURTOSIS](kurtosis/Kurtosis.md) | Kurtosis | Tail heaviness. High kurtosis = fat tails = more extreme events. |
| [LINREG](linreg/LinReg.md) | Linear Regression | Least squares fit. Outputs slope, intercept, R². |
| [MEDIAN](median/Median.md) | Median | Middle value in sorted window. Robust to outliers. |
| [MODE](mode/Mode.md) | Mode | Most frequent value. Use for categorical or discrete data. |
| [PACF](pacf/Pacf.md) | Partial Autocorrelation Function | Direct correlation at lag k after removing intermediate effects. For AR model identification. |
| [PERCENTILE](percentile/Percentile.md) | Percentile | Value below which given percentage of observations fall. |
| [QUANTILE](quantile/Quantile.md) | Quantile | Divides distribution into equal probability intervals. |
| [SKEW](skew/Skew.md) | Skewness | Distribution asymmetry. Positive: right tail. Negative: left tail. |
| [SPEARMAN](spearman/Spearman.md) | Spearman Rank Correlation | Pearson on ranks. Measures monotonic relationship. |
| [STDDEV](stddev/StdDev.md) | Standard Deviation | Square root of variance. Same units as data. |
| [SUM](sum/Sum.md) | Rolling Sum | Kahan-Babuška summation. Numerically stable. |
| [THEIL](theil/Theil.md) | Theil Index | Inequality measure. Decomposable into within/between group. |
| [VARIANCE](variance/Variance.md) | Variance | Average squared deviation from mean. Units are squared. |
| [ZSCORE](zscore/Zscore.md) | Z-Score | Standard deviations from mean. Normalizes different scales. |
| [ZTEST](ztest/Ztest.md) | Z-Test | One-sample t-test statistic against hypothesized mean. |
| [MEANDEV](meandev/MeanDev.md) | Mean Absolute Deviation | Outlier-robust dispersion. Core of CCI. MD ≈ 0.7979σ for normal data. |
| [STDERR](stderr/Stderr.md) | Standard Error of Regression | OLS residual scatter over rolling window. Quantifies trend fit quality. |
| [POLYFIT](polyfit/Polyfit.md) | Polynomial Fitting | Least-squares polynomial regression. |
| [TRIM](trim/Trim.md) | Trimmed Mean MA | Mean after discarding extreme percentiles. |
| [WAVG](wavg/Wavg.md) | Weighted Average | Generic weighted mean. |
| [WINS](wins/Wins.md) | Winsorized Mean MA | Mean with extreme values clamped to percentile bounds. |
