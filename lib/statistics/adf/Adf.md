# ADF: Augmented Dickey-Fuller Test

> *"A time series that looks like it has a trend might just be a random walk wearing a disguise."*

| Property         | Value                                          |
| ---------------- | ---------------------------------------------- |
| **Category**     | Statistics                                     |
| **Inputs**       | Source (close)                                  |
| **Parameters**   | `period` (default 50), `maxLag` (default 0=auto), `regression` (default Constant) |
| **Outputs**      | Single series (p-value)                         |
| **Output range** | $[0, 1]$                                       |
| **Warmup**       | `period` bars                                   |
| **PineScript**   | [adf.pine](adf.pine)                           |

- Tests the null hypothesis that a time series contains a unit root (non-stationary). Output near **0** → stationary; output near **1** → unit root.
- **Similar:** [Hurst](../hurst/Hurst.md), [Cointegration](../cointegration/Cointegration.md) | **Complementary:** Z-Score, Variance | **Trading note:** ADF < 0.05 confirms mean-reversion suitability.
- Validated against Python `statsmodels.tsa.stattools.adfuller` reference implementation.

The Augmented Dickey-Fuller test is the gold standard for detecting whether a financial time series is stationary or contains a unit root. Unlike the original Dickey-Fuller test, the augmented version includes lagged difference terms $\Delta y_{t-i}$ to absorb serial correlation, ensuring the test statistic follows the correct distribution. The p-value output uses MacKinnon (1994, 2010) polynomial interpolation with a standard normal CDF approximation, providing machine-precision results without lookup tables.

## Historical Context

David Dickey and Wayne Fuller introduced the original unit root test in 1979, establishing the foundation for modern time series econometrics. Said and Dickey (1984) proposed the augmented version that includes lagged differences to handle serial correlation in the residuals. James MacKinnon (1994, 2010) developed the response surface regression approach that maps the test statistic to an approximate p-value, replacing the need for Monte Carlo simulation tables. The ADF test remains the most widely used stationarity test in quantitative finance, underpinning pairs trading, cointegration analysis, and regime detection.

## Architecture & Physics

### 1. First Differences

$$
\Delta y_t = y_t - y_{t-1}, \quad t = 1, \ldots, n-1
$$

### 2. Augmented Regression Model

$$
\Delta y_t = \alpha + \beta t + \gamma y_{t-1} + \sum_{i=1}^{p} \delta_i \Delta y_{t-i} + \varepsilon_t
$$

where $\alpha$ is the intercept (constant model), $\beta t$ is the linear trend (trend model), $\gamma$ is the coefficient of interest on the lagged level, and $\delta_i$ are coefficients on lagged differences that absorb serial correlation.

### 3. OLS Estimation via Cholesky

The normal equations $X'X \hat{\beta} = X'y$ are solved via Cholesky decomposition $X'X = LL'$ with forward-backward substitution. For $k \leq 8$ regressors, all matrices fit in `stackalloc` — zero heap allocation.

### 4. Test Statistic

$$
t = \frac{\hat{\gamma}}{\text{SE}(\hat{\gamma})} \quad \text{where} \quad \text{SE}(\hat{\gamma}) = \sqrt{s^2 \cdot (X'X)^{-1}_{\gamma\gamma}}
$$

and $s^2 = \text{RSS} / (n - k)$ is the residual variance estimate.

### 5. Auto-Lag Selection (AIC)

When `maxLag = 0`, the optimal lag is selected by minimizing:

$$
\text{AIC} = n \cdot \ln(\text{RSS}/n) + 2k
$$

over lags $p = 0, 1, \ldots, p_{\max}$ where the Schwert (1989) upper bound is:

$$
p_{\max} = \left\lfloor 12 \cdot \left(\frac{T}{100}\right)^{0.25} \right\rfloor
$$

| Sample Size $T$ | $(T/100)^{0.25}$ | $p_{\max}$ |
|:-:|:-:|:-:|
| 50 | 0.84 | 10 |
| 100 | 1.00 | 12 |
| 250 | 1.26 | 15 |
| 500 | 1.50 | 17 |

### 6. MacKinnon P-Value

The test statistic is mapped to a p-value via MacKinnon (1994, 2010) response surface polynomial interpolation:

- **Small-p region** ($\tau \leq \tau^*$): $Z = c_0 + c_1 \tau + c_2 \tau^2$
- **Large-p region** ($\tau > \tau^*$): $Z = c_0 + c_1 \tau + c_2 \tau^2 + c_3 \tau^3$

The polynomial Z-score is then passed through the standard normal CDF: $p = \Phi(Z)$.

#### Small-P Coefficients ($\tau \leq \tau^*$)

| Regression | $c_0$ | $c_1$ | $c_2$ |
|:-:|:-:|:-:|:-:|
| nc | 0.6344 | 1.2378 | 0.032496 |
| c  | 2.1659 | 1.4412 | 0.038269 |
| ct | 3.2512 | 1.6047 | 0.049588 |

#### Large-P Coefficients ($\tau > \tau^*$)

| Regression | $c_0$ | $c_1$ | $c_2$ | $c_3$ |
|:-:|:-:|:-:|:-:|:-:|
| nc | 0.4797 | 0.93557 | −0.06999 | 0.033066 |
| c  | 1.7339 | 0.93202 | −0.12745 | −0.010368 |
| ct | 2.5261 | 0.61654 | −0.37956 | −0.060285 |

### 7. Standard Normal CDF via Error Function

The standard normal CDF $\Phi(x)$ is computed via the Abramowitz & Stegun (1964) rational approximation of the Gaussian error function (equation 7.1.26):

$$
\Phi(x) = \frac{1}{2}\left[1 + \text{sgn}(x) \cdot \text{erf}\!\left(\frac{|x|}{\sqrt{2}}\right)\right]
$$

where $\text{erf}(z) = 1 - (a_1 t + a_2 t^2 + a_3 t^3 + a_4 t^4 + a_5 t^5) e^{-z^2}$ with $t = \frac{1}{1 + pz}$.

| Parameter | Value |
|:-:|:-:|
| $p$   | 0.3275911 |
| $a_1$ | 0.254829592 |
| $a_2$ | −0.284496736 |
| $a_3$ | 1.421413741 |
| $a_4$ | −1.453152027 |
| $a_5$ | 1.061405429 |

Maximum relative error: $\pm 1.5 \times 10^{-7}$, providing at least 6 decimal places of p-value accuracy.

### 8. Complexity

| Aspect | Cost |
|--------|------|
| Streaming (per bar) | $O(n \cdot p^2)$ where $n$ = period, $p$ = lags |
| OLS solve | $O(k^3)$ with $k \leq 8$ |
| Memory | $O(n)$ via RingBuffer |

## Mathematical Foundation

### Parameters

| Parameter    | Symbol | Default  | Constraint           |
|-------------|--------|----------|----------------------|
| `period`    | $n$    | 50       | $n \geq 20$          |
| `maxLag`    | $p$    | 0 (auto) | $p \geq 0$           |
| `regression`| —      | Constant | {NoConstant, Constant, ConstantAndTrend} |

### Regression Models

| Model           | Equation | Use Case |
|-----------------|----------|----------|
| NoConstant (nc) | $\Delta y_t = \gamma y_{t-1} + \ldots$ | Known zero-mean process |
| Constant (c)    | $\Delta y_t = \alpha + \gamma y_{t-1} + \ldots$ | General stationarity test |
| ConstantAndTrend (ct) | $\Delta y_t = \alpha + \beta t + \gamma y_{t-1} + \ldots$ | Trend-stationary test |

### MacKinnon Coefficients (N=1)

| Regression | $\tau^*$ | $\tau_{\min}$ | $\tau_{\max}$ |
|------------|---------|--------------|--------------|
| nc | -1.04 | -19.04 | $+\infty$ |
| c  | -1.61 | -18.83 | 2.74 |
| ct | -2.89 | -16.18 | 0.70 |

### Output Interpretation

| P-Value | Interpretation |
|---------|---------------|
| < 0.01 | Strong evidence of stationarity — reject unit root at 1% |
| < 0.05 | Evidence of stationarity — reject unit root at 5% |
| < 0.10 | Weak evidence of stationarity — reject at 10% |
| ≥ 0.10 | Cannot reject unit root — series may be non-stationary |

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Cost (cycles) | Subtotal |
|-----------|------:|:-------------:|:--------:|
| SUB (differences) | $n$ | 1 | $n$ |
| MUL (X'X accum) | $n \cdot k^2$ | 3 | $3nk^2$ |
| SQRT (Cholesky) | $k$ | 20 | $20k$ |
| DIV (triangular solve) | $k^2$ | 15 | $15k^2$ |
| LN (AIC) | $p_{\max}$ | 50 | $50p_{\max}$ |
| EXP (NormCdf) | 1 | 50 | 50 |
| **Total** | — | — | **~$3nk^2 + 50$** |

### SIMD Analysis (Batch Mode)

| Operation | Vectorizable? | Notes |
|-----------|:------------:|-------|
| First differences | ✅ | Subtraction loop |
| X'X accumulation | Partial | Inner products vectorizable |
| Cholesky solve | ❌ | Sequential dependency |
| MacKinnon poly | ❌ | Scalar, 1 call per bar |
| NormCdf | ❌ | Scalar, 1 call per bar |

## Validation

| Library | Status | Notes |
|---------|:------:|-------|
| **statsmodels** | ✅ | `adfuller()` — primary reference |
| **TA-Lib** | N/A | Not available |
| **Skender** | N/A | Not available |
| **Self-consistency** | ✅ | Batch/streaming/span match |

## Common Pitfalls

1. **Warmup period**: Requires at least 20 bars. Results before warmup return p=1.0 (assume unit root).
2. **Lag selection**: Auto-lag (maxLag=0) uses AIC which may overfit on short windows. For periods < 50, consider fixing maxLag=1.
3. **Regression model**: Using `ConstantAndTrend` when no trend exists reduces power (higher p-values). Default `Constant` is correct for most financial series.
4. **Non-standard distribution**: The test statistic does NOT follow a t-distribution — MacKinnon critical values are mandatory.
5. **Window size**: Period < 30 gives unreliable results. Period ≥ 50 recommended for financial data.
6. **Multiple testing**: Running ADF on many series without Bonferroni correction inflates false positives.
7. **Structural breaks**: ADF has low power against alternatives with structural breaks. Consider using Zivot-Andrews test instead.

## Related Indicators

- [**Hurst**](../hurst/Hurst.md): Measures long-range dependence. H < 0.5 and ADF p < 0.05 together strongly confirm mean-reversion.
- [**Cointegration**](../cointegration/Cointegration.md): Uses a simplified ADF internally on OLS residuals for the Engle-Granger two-step test.
- [**Variance**](../variance/Variance.md): High variance ratio rejection and low ADF p-value confirm stationarity.

## References

- Dickey, D.A. and Fuller, W.A. (1979). "Distribution of the Estimators for Autoregressive Time Series with a Unit Root." *Journal of the American Statistical Association*, 74(366), 427-431.
- Said, S.E. and Dickey, D.A. (1984). "Testing for Unit Roots in Autoregressive-Moving Average Models of Unknown Order." *Biometrika*, 71(3), 599-607.
- Schwert, G.W. (1989). "Tests for Unit Roots: A Monte Carlo Investigation." *Journal of Business & Economic Statistics*, 7(2), 147-159.
- MacKinnon, J.G. (1994). "Approximate Asymptotic Distribution Functions for Unit-Root and Cointegration Tests." *Journal of Business & Economic Statistics*, 12(2), 167-176.
- MacKinnon, J.G. (2010). "Critical Values for Cointegration Tests." Working Paper 1227, Queen's University Department of Economics.
- Abramowitz, M. and Stegun, I.A. (1964). *Handbook of Mathematical Functions with Formulas, Graphs, and Mathematical Tables*. National Bureau of Standards Applied Mathematics Series 55, §7.1.26.
