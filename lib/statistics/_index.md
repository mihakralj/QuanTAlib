# Statistics

> "All models are wrong, but some are useful." — George Box

Statistical tools applied to price and returns. These indicators quantify relationships, measure dispersion, test hypotheses. Unlike momentum or trend indicators, statistics describe the data itself.

| Indicator | Full Name | Description |
| :--- | :--- | :--- |
| [ACF](acf/Acf.md) | Autocorrelation Function | Correlation of time series with lagged copy. For ARMA model identification. |
| [BETA](beta/Beta.md) | Beta Coefficient | Asset volatility relative to market. β=1 means market-matched risk. |
| [BIAS](bias/Bias.md) | Bias | Percentage deviation from moving average. Measures overextension. |
| [CMA](cma/Cma.md) | Cumulative Moving Average | Running average of all values. Welford's algorithm. No window. |
| [COINTEGRATION](cointegration/Cointegration.md) | Cointegration | Tests if series share long-term equilibrium. Pairs trading foundation. |
| [CORRELATION](correlation/Correlation.md) | Correlation | Linear relationship between two variables. Range: -1 to +1. |
| [COVARIANCE](covariance/Covariance.md) | Covariance | Joint variability of two random variables. Building block for β. |
| [ENTROPY](entropy/Entropy.md) | Shannon Entropy | Measures uncertainty/randomness. Higher entropy = less predictable. |
| [GEOMEAN](geomean/Geomean.md) | Geometric Mean | nth root of product. Use for growth rates and ratios. |
| [GRANGER](granger/Granger.md) | Granger Causality | Tests if one series helps predict another. Not true causality. |
| HARMEAN | Harmonic Mean | Reciprocal of arithmetic mean of reciprocals. For rates/ratios. |
| HURST | Hurst Exponent | Long-term memory. H>0.5: trending. H<0.5: mean-reverting. |
| IQR | Interquartile Range | P75 - P25. Robust dispersion measure. |
| JB | Jarque-Bera Test | Normality test using skewness and kurtosis. |
| KENDALL | Kendall Rank Correlation | Ordinal association. Robust to outliers. |
| KURTOSIS | Kurtosis | Tail heaviness. High kurtosis = fat tails = more extreme events. |
| [LINREG](linreg/LinReg.md) | Linear Regression | Least squares fit. Outputs slope, intercept, R². |
| [MEDIAN](median/Median.md) | Median | Middle value in sorted window. Robust to outliers. |
| MODE | Mode | Most frequent value. Use for categorical or discrete data. |
| [PACF](pacf/Pacf.md) | Partial Autocorrelation Function | Direct correlation at lag k after removing intermediate effects. For AR model identification. |
| PERCENTILE | Percentile | Value below which given percentage of observations fall. |
| QUANTILE | Quantile | Divides distribution into equal probability intervals. |
| [SKEW](skew/Skew.md) | Skewness | Distribution asymmetry. Positive: right tail. Negative: left tail. |
| SPEARMAN | Spearman Rank Correlation | Pearson on ranks. Measures monotonic relationship. |
| [STDDEV](stddev/StdDev.md) | Standard Deviation | Square root of variance. Same units as data. |
| [SUM](sum/Sum.md) | Rolling Sum | Kahan-Babuška summation. Numerically stable. |
| THEIL | Theil Index | Inequality measure. Decomposable into within/between group. |
| [VARIANCE](variance/Variance.md) | Variance | Average squared deviation from mean. Units are squared. |
| ZSCORE | Z-Score | Standard deviations from mean. Normalizes different scales. |
| ZTEST | Z-Test | Hypothesis test comparing sample mean to population mean. |
