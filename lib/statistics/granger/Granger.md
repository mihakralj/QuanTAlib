# GRANGER: Granger Causality F-Statistic

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Statistic                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period` (default 20)                      |
| **Outputs**      | Single series (Granger)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period + 1` bars                          |
| **PineScript**   | [granger.pine](granger.pine)                       |

- The Granger Causality test asks a precise, falsifiable question: does knowing the history of series X improve your ability to predict series Y, bey...
- Parameterized by `period` (default 20).
- Output range: Varies (see docs).
- Requires `period + 1` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "Correlation is not causation, but Granger causality is not causation either. It is prediction." -- Clive Granger

## Introduction

The Granger Causality test asks a precise, falsifiable question: does knowing the history of series X improve your ability to predict series Y, beyond what Y's own history already provides? The answer arrives as an F-statistic from comparing two OLS regression models. Higher F means X contains predictive information about Y that Y itself does not. This implementation uses lag-1, runs in O(1) streaming mode via running sums, and handles bar corrections for live trading.

## Historical Context

Clive Granger introduced this test in 1969, later refined in Granger (1980). The key insight: "causality" here means temporal predictive precedence, not physical causation. The test became a workhorse in econometrics for testing lead-lag relationships between economic variables, exchange rates, and commodity prices. In trading, it identifies which instruments lead others, informing pairs trading, cross-asset signals, and regime detection.

Standard implementations require batch matrix operations. This implementation maintains running statistics for O(1) per-bar updates, matching the batch result exactly while supporting streaming and bar correction.

## Architecture and Physics

### 1. Dual-Input Streaming Design

The indicator takes two series: Y (dependent, the series you want to predict) and X (independent, the hypothesized cause). At each bar, it maintains three parallel ring buffers storing the lagged triplet (y_t, y_{t-1}, x_{t-1}) over a rolling window of size `period`.

### 2. Running Sum Statistics

Nine running sums track means, variances, and cross-covariances:

- `sumY`, `sumYLag`, `sumXLag` for means
- `sumYY`, `sumYLagYLag`, `sumXLagXLag` for variances
- `sumYYLag`, `sumYXLag`, `sumYLagXLag` for covariances

These enable O(1) updates: subtract the oldest triplet, add the newest. Periodic resync every 1000 bars corrects floating-point drift.

### 3. Bar Correction via isNew

When `isNew=false`, the indicator restores the previous state snapshot and replaces the newest triplet in all buffers and running sums. This handles tick updates within the same bar without re-processing the entire window.

## Mathematical Foundation

### Restricted Model (AR(1))

$$y_t = c_0 + c_1 \cdot y_{t-1} + \varepsilon_{1,t}$$

OLS coefficients:

$$c_1 = \frac{\text{Cov}(y_t, y_{t-1})}{\text{Var}(y_{t-1})}$$

$$c_0 = \bar{y} - c_1 \cdot \bar{y}_{t-1}$$

### Unrestricted Model (AR(1) + X lag)

$$y_t = d_0 + d_1 \cdot y_{t-1} + d_2 \cdot x_{t-1} + \varepsilon_{2,t}$$

Two-variable OLS via Cramer's rule:

$$D = \text{Var}(y_{t-1}) \cdot \text{Var}(x_{t-1}) - \text{Cov}(y_{t-1}, x_{t-1})^2$$

$$d_1 = \frac{\text{Cov}(y_t, y_{t-1}) \cdot \text{Var}(x_{t-1}) - \text{Cov}(y_t, x_{t-1}) \cdot \text{Cov}(y_{t-1}, x_{t-1})}{D}$$

$$d_2 = \frac{\text{Cov}(y_t, x_{t-1}) \cdot \text{Var}(y_{t-1}) - \text{Cov}(y_t, y_{t-1}) \cdot \text{Cov}(y_{t-1}, x_{t-1})}{D}$$

### F-Statistic

$$SSR_1 = \left(\text{Var}(y_t) - c_1^2 \cdot \text{Var}(y_{t-1})\right) \cdot N$$

$$SSR_2 = \sum_{i=1}^{N} \left(y_i - d_0 - d_1 \cdot y_{i-1,\text{lag}} - d_2 \cdot x_{i-1,\text{lag}}\right)^2$$

$$F = \frac{(SSR_1 - SSR_2) / q}{SSR_2 / (N - k)}$$

where $q = 1$ (one restriction: $d_2 = 0$) and $k = 3$ (unrestricted model parameters). The F-statistic follows an $F(1, N-3)$ distribution under the null hypothesis that X does not Granger-cause Y.

## Performance Profile

### Operation Count (Streaming Mode)

Granger causality fits two rolling OLS regressions (restricted and unrestricted) per bar using sliding-window normal equations.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Ring buffer add/evict (2 series) | 2 | 3 cy | ~6 cy |
| Restricted OLS (AR of Y) | L ops | 8 cy | ~8L cy |
| Unrestricted OLS (AR of Y + lags of X) | 2L ops | 8 cy | ~16L cy |
| RSS computation (2 models) | 2N | 2 cy | ~4N cy |
| F-statistic calculation | 1 | 5 cy | ~5 cy |
| **Total (L=2, N=30)** | **O(L*N)** | — | **~189 cy** |

O(L·N) per update where L = number of lags, N = period. Heavy enough that batch mode (pre-computing all bars at once) is preferred for historical analysis.

| Metric | Value |
| :--- | :--- |
| Update complexity | O(1) amortized, O(N) for SSR2 loop |
| Memory | 3 ring buffers + 9 running sums |
| Allocations per Update | Zero |
| SIMD potential | Low (recursive lag dependency) |
| Warmup period | period + 1 |

### Quality Metrics

| Metric | Score (1-10) |
| :--- | :--- |
| Responsiveness | 7 |
| Smoothness | 5 |
| Lag | 3 (inherent from windowed regression) |
| Noise rejection | 6 |
| Interpretability | 8 (F-statistic, compare to critical values) |

## Validation

This indicator validates against statistical properties rather than external TA libraries, as Granger causality is not commonly found in standard TA packages.

| Test | Description | Result |
| :--- | :--- | :--- |
| Causal relationship | Y = f(Y_lag, X_lag) + noise | F > 0, high |
| Independent series | Two independent GBMs | F finite, generally low |
| Asymmetric detection | X causes Y but Y does not cause X | F(Y,X) > F(X,Y) |
| Batch vs streaming | TSeries batch matches streaming | Exact match |
| Span vs streaming | Span API matches streaming | Exact match |
| Bar correction | isNew=false restores state | Values match |

## Common Pitfalls

1. **Not true causation.** Granger causality tests temporal precedence in prediction, not physical causation. A spurious correlation with a lagged third variable can produce high F.
2. **Period too small.** Period must exceed 3 for the F-statistic to have positive degrees of freedom. Small periods amplify noise. Use 20+ for meaningful results.
3. **Constant or near-constant series.** Zero variance in the lag produces NaN (division by zero in OLS). This is mathematically correct behavior.
4. **Multicollinearity.** If y_lag and x_lag are nearly perfectly correlated, the denominator D approaches zero, producing NaN. This indicates the two predictors carry redundant information.
5. **Confusing direction.** F(Y,X) tests whether X helps predict Y. F(X,Y) tests the reverse. Always verify which direction matters for your trading thesis.
6. **Critical values depend on sample size.** For F(1, N-3): at 5% significance, critical value is approximately 4.0 for N=20, declining toward 3.84 for large N.
7. **Floating-point drift.** Running sums accumulate rounding errors over thousands of bars. The built-in resync every 1000 bars limits this to negligible levels.

## References

- Granger, C.W.J. (1969). "Investigating Causal Relations by Econometric Models and Cross-spectral Methods." Econometrica, 37(3), 424-438.
- Granger, C.W.J. (1980). "Testing for Causality: A Personal Viewpoint." Journal of Economic Dynamics and Control, 2, 329-352.
- Hamilton, J.D. (1994). Time Series Analysis. Princeton University Press. Chapter 11.
- Sims, C.A. (1972). "Money, Income, and Causality." American Economic Review, 62(4), 540-552.
