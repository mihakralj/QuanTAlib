# Statistics

> "All models are wrong, but some are useful." — George Box

Statistical tools applied to price and returns. These indicators quantify relationships, measure dispersion, test hypotheses. Unlike momentum or trend indicators, statistics describe the data itself.

| Indicator | Full Name | Description |
| :--- | :--- | :--- |
| [BETA](/lib/statistics/beta/Beta.md) | Beta Coefficient | Asset volatility relative to market. β=1 means market-matched risk. |
| [BIAS](/lib/statistics/bias/Bias.md) | Bias | Percentage deviation from moving average. Measures overextension. |
| [CMA](/lib/statistics/cma/Cma.md) | Cumulative Moving Average | Running average of all values. Welford's algorithm. No window. |
| [COINTEGRATION](/lib/statistics/cointegration/Cointegration.md) | Cointegration | Tests if series share long-term equilibrium. Pairs trading foundation. |
| [CORRELATION](/lib/statistics/correlation/Correlation.md) | Correlation | Linear relationship between two variables. Range: -1 to +1. |
| [COVARIANCE](/lib/statistics/covariance/Covariance.md) | Covariance | Joint variability of two random variables. Building block for β. |
| [CUMMEAN](/lib/statistics/cummean/Cummean.md) | Cumulative Mean | Cumulative mean from series start. Ignores NaN values. |
| [ENTROPY](/lib/statistics/entropy/Entropy.md) | Shannon Entropy | Measures uncertainty/randomness. Higher entropy = less predictable. |
| [GEOMEAN](/lib/statistics/geomean/Geomean.md) | Geometric Mean | nth root of product. Use for growth rates and ratios. |
| [GRANGER](/lib/statistics/granger/Granger.md) | Granger Causality | Tests if one series helps predict another. Not true causality. |
| [HARMEAN](/lib/statistics/harmean/Harmean.md) | Harmonic Mean | Reciprocal of arithmetic mean of reciprocals. For rates/ratios. |
| [HURST](/lib/statistics/hurst/Hurst.md) | Hurst Exponent | Long-term memory. H>0.5: trending. H<0.5: mean-reverting. |
| [IQR](/lib/statistics/iqr/Iqr.md) | Interquartile Range | P75 - P25. Robust dispersion measure. |
| [JB](/lib/statistics/jb/Jb.md) | Jarque-Bera Test | Normality test using skewness and kurtosis. |
| [KENDALL](/lib/statistics/kendall/Kendall.md) | Kendall Rank Correlation | Ordinal association. Robust to outliers. |
| [KURTOSIS](/lib/statistics/kurtosis/Kurtosis.md) | Kurtosis | Tail heaviness. High kurtosis = fat tails = more extreme events. |
| [LINREG](/lib/statistics/linreg/LinReg.md) | Linear Regression | Least squares fit. Outputs slope, intercept, R². |
| [MEDIAN](/lib/statistics/median/Median.md) | Median | Middle value in sorted window. Robust to outliers. |
| [MODE](/lib/statistics/mode/Mode.md) | Mode | Most frequent value. Use for categorical or discrete data. |
| [PERCENTILE](/lib/statistics/percentile/Percentile.md) | Percentile | Value below which given percentage of observations fall. |
| [QUANTILE](/lib/statistics/quantile/Quantile.md) | Quantile | Divides distribution into equal probability intervals. |
| [SKEW](/lib/statistics/skew/Skew.md) | Skewness | Distribution asymmetry. Positive: right tail. Negative: left tail. |
| [SPEARMAN](/lib/statistics/spearman/Spearman.md) | Spearman Rank Correlation | Pearson on ranks. Measures monotonic relationship. |
| [STDDEV](/lib/statistics/stddev/StdDev.md) | Standard Deviation | Square root of variance. Same units as data. |
| [SUM](/lib/statistics/sum/Sum.md) | Rolling Sum | Kahan-Babuška summation. Numerically stable. |
| [THEIL](/lib/statistics/theil/Theil.md) | Theil Index | Inequality measure. Decomposable into within/between group. |
| [VARIANCE](/lib/statistics/variance/Variance.md) | Variance | Average squared deviation from mean. Units are squared. |
| [ZSCORE](/lib/statistics/zscore/Zscore.md) | Z-Score | Standard deviations from mean. Normalizes different scales. |
| [ZTEST](/lib/statistics/ztest/Ztest.md) | Z-Test | Hypothesis test comparing sample mean to population mean. |
