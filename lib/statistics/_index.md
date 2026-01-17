# Statistics

> "All models are wrong, but some are useful." — George Box

Statistical tools applied to price and returns. These indicators quantify relationships, measure dispersion, test hypotheses. Unlike momentum or trend indicators, statistics describe the data itself.

## Indicator Status

| Indicator | Full Name | Status | Description |
| :--- | :--- | :---: | :--- |
| [BETA](lib/statistics/beta/beta.md) | Beta Coefficient | ✅ | Asset volatility relative to market. β=1 means market-matched risk. |
| BIAS | Bias | 📋 | Percentage deviation from moving average. Measures overextension. |
| [CMA](lib/statistics/cma/Cma.md) | Cumulative Moving Average | ✅ | Running average of all values. Welford's algorithm. No window. |
| COINTEGRATION | Cointegration | 📋 | Tests if series share long-term equilibrium. Pairs trading foundation. |
| CORRELATION | Correlation (Pearson's) | 📋 | Linear relationship between two variables. Range: -1 to +1. |
| [COVARIANCE](lib/statistics/covariance/Covariance.md) | Covariance | ✅ | Joint variability of two random variables. Building block for β. |
| CUMMEAN | Cumulative Mean | 📋 | Cumulative mean from series start. Ignores NaN values. |
| ENTROPY | Shannon Entropy | 📋 | Measures uncertainty/randomness. Higher entropy = less predictable. |
| GEOMEAN | Geometric Mean | 📋 | nth root of product. Use for growth rates and ratios. |
| GRANGER | Granger Causality | 📋 | Tests if one series helps predict another. Not true causality. |
| HARMEAN | Harmonic Mean | 📋 | Reciprocal of arithmetic mean of reciprocals. For rates/ratios. |
| HURST | Hurst Exponent | 📋 | Long-term memory. H>0.5: trending. H<0.5: mean-reverting. |
| IQR | Interquartile Range | 📋 | P75 - P25. Robust dispersion measure. |
| JB | Jarque-Bera Test | 📋 | Normality test using skewness and kurtosis. |
| KENDALL | Kendall Rank Correlation | 📋 | Ordinal association. Robust to outliers. |
| KURTOSIS | Kurtosis | 📋 | Tail heaviness. High kurtosis = fat tails = more extreme events. |
| [LINREG](lib/statistics/linreg/LinReg.md) | Linear Regression | ✅ | Least squares fit. Outputs slope, intercept, R². |
| [MEDIAN](lib/statistics/median/Median.md) | Median | ✅ | Middle value in sorted window. Robust to outliers. |
| MODE | Mode | 📋 | Most frequent value. Use for categorical or discrete data. |
| PERCENTILE | Percentile | 📋 | Value below which given percentage of observations fall. |
| QUANTILE | Quantile | 📋 | Divides distribution into equal probability intervals. |
| [SKEW](lib/statistics/skew/Skew.md) | Skewness | ✅ | Distribution asymmetry. Positive: right tail. Negative: left tail. |
| SPEARMAN | Spearman Rank Correlation | 📋 | Pearson on ranks. Measures monotonic relationship. |
| [STDDEV](lib/statistics/stddev/StdDev.md) | Standard Deviation | ✅ | Square root of variance. Same units as data. |
| [SUM](lib/statistics/sum/Sum.md) | Rolling Sum | ✅ | Kahan-Babuška summation. Numerically stable. |
| THEIL | Theil Index | 📋 | Inequality measure. Decomposable into within/between group. |
| [VARIANCE](lib/statistics/variance/Variance.md) | Variance | ✅ | Average squared deviation from mean. Units are squared. |
| ZSCORE | Z-Score | 📋 | Standard deviations from mean. Normalizes different scales. |
| ZTEST | Z-Test | 📋 | Hypothesis test comparing sample mean to population mean. |

**Status Key:** ✅ Implemented | 📋 Planned

## Selection Guide

| Use Case | Recommended | Why |
| :--- | :--- | :--- |
| Dispersion measurement | STDDEV, VARIANCE | Standard measures. STDDEV in original units. |
| Outlier-robust dispersion | MEDIAN, IQR | Median ignores extremes. IQR measures middle 50%. |
| Central tendency | CMA, MEDIAN | CMA for normal data. MEDIAN for skewed data. |
| Trend fitting | LINREG | Least squares regression. Provides slope and R². |
| Distribution shape | SKEW, KURTOSIS | Skew for asymmetry. Kurtosis for tail risk. |
| Pair relationships | CORRELATION, COVARIANCE, BETA | Correlation normalized. Covariance raw. Beta relative to benchmark. |
| Regime detection | HURST, ENTROPY | Hurst for trending vs mean-reverting. Entropy for randomness. |
| Normality testing | JB | Quick normality check before parametric tests. |

## Statistical Concepts

| Concept | Implemented As | Interpretation |
| :--- | :--- | :--- |
| Location | CMA, MEDIAN | Where is the center? |
| Spread | VARIANCE, STDDEV, IQR | How dispersed is data? |
| Shape | SKEW, KURTOSIS | Is distribution symmetric? Fat-tailed? |
| Relationship | CORRELATION, COVARIANCE, BETA | How do two series move together? |
| Trend | LINREG | What is underlying direction? |
| Memory | HURST | Does past predict future? |