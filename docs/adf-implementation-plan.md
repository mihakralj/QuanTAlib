# ADF: Augmented Dickey-Fuller Test — Implementation Plan

> "The most important question in time series analysis isn't what the trend is — it's whether there is one at all."

## 1. Overview

The **Augmented Dickey-Fuller (ADF)** test is the gold-standard unit root test for determining whether a time series is stationary. A **p-value** output makes it directly actionable: low p-value (< 0.05) → reject the null hypothesis of a unit root → series is stationary.

**Location:** `lib/statistics/adf/`

**Output:** p-value ∈ [0.0, 1.0] per bar (rolling window)

**Category:** Statistics (alongside [`Hurst`](../lib/statistics/hurst/Hurst.cs), [`Granger`](../lib/statistics/granger/Granger.cs), [`Cointegration`](../lib/statistics/cointegration/Cointegration.cs))

---

## 2. Historical Context

- **Dickey & Fuller (1979)** introduced the basic DF test: does an AR(1) process have a unit root?
- **Said & Dickey (1984)** extended it to the **Augmented** DF test by including lagged difference terms to handle serial correlation.
- **MacKinnon (1994, 2010)** derived the response surface approximations that allow computation of **p-values** from the non-standard ADF distribution (which is not normal or t-distributed).
- The existing [`Cointegration`](../lib/statistics/cointegration/Cointegration.cs:31) indicator already uses a simplified ADF internally (no-intercept, no augmented lags) for the Engle-Granger two-step test. The new `Adf` indicator will be a **standalone, full-featured** single-input ADF test.

---

## 3. Mathematical Foundation

### 3.1 The ADF Regression

$$\Delta y_t = \alpha + \beta t + \gamma \, y_{t-1} + \sum_{i=1}^{p} \delta_i \, \Delta y_{t-i} + \varepsilon_t$$

Where:
- $\Delta y_t = y_t - y_{t-1}$ (first difference)
- $\alpha$ = constant/drift term (included in "c" and "ct" modes)
- $\beta t$ = linear trend term (included in "ct" mode only)
- $\gamma$ = **coefficient of interest** — if $\gamma = 0$, unit root exists
- $\delta_i$ = coefficients on lagged differences (augmented terms to absorb serial correlation)
- $p$ = number of augmented lags
- $\varepsilon_t$ = white noise error

### 3.2 Regression Types

| Mode | Model | Regressors (k) | Use Case |
|------|-------|----------------|----------|
| `NoConstant` ("nc") | $\Delta y_t = \gamma y_{t-1} + \ldots$ | $1 + p$ | Pure random walk test |
| `Constant` ("c") | $\Delta y_t = \alpha + \gamma y_{t-1} + \ldots$ | $2 + p$ | **Default** — random walk with drift |
| `ConstantAndTrend` ("ct") | $\Delta y_t = \alpha + \beta t + \gamma y_{t-1} + \ldots$ | $3 + p$ | Trend-stationary alternative |

### 3.3 Test Statistic

$$\text{ADF}_t = \frac{\hat{\gamma}}{\text{SE}(\hat{\gamma})}$$

Where $\hat{\gamma}$ and $\text{SE}(\hat{\gamma})$ are obtained from OLS regression:
- $\hat{\boldsymbol{\beta}} = (\mathbf{X}'\mathbf{X})^{-1} \mathbf{X}'\mathbf{y}$
- $\hat{\sigma}^2 = \frac{\text{RSS}}{m - k}$ where $m$ = number of observations, $k$ = number of regressors
- $\text{SE}(\hat{\gamma}) = \sqrt{\hat{\sigma}^2 \cdot [(\mathbf{X}'\mathbf{X})^{-1}]_{jj}}$ where $j$ is the column index of $y_{t-1}$

### 3.4 P-Value (MacKinnon 1994)

The ADF test statistic does **not** follow a standard distribution. MacKinnon (1994) provides polynomial approximations:

1. **Bounds check:**
   - If $\text{ADF}_t > \tau_{\max}$ → $p = 1.0$
   - If $\text{ADF}_t < \tau_{\min}$ → $p = 0.0$

2. **Polynomial evaluation:**
   - If $\text{ADF}_t \leq \tau^*$ (left tail): use `smallp` coefficients (3 terms)
   - If $\text{ADF}_t > \tau^*$ (right tail): use `largep` coefficients (4 terms)

3. **P-value:**
$$p = \Phi\left(\sum_{i=0}^{n} c_i \cdot \text{ADF}_t^i\right)$$

Where $\Phi$ is the standard normal CDF, implemented as:
$$\Phi(x) = \frac{1}{2} \operatorname{erfc}\!\left(-\frac{x}{\sqrt{2}}\right)$$

### 3.5 Critical Values (MacKinnon 2010)

$$\text{CV}_\alpha(n) = c_0 + \frac{c_1}{n} + \frac{c_2}{n^2} + \frac{c_3}{n^3}$$

| Regression | Level | $c_0$ | $c_1$ | $c_2$ | $c_3$ |
|-----------|-------|--------|--------|--------|--------|
| Constant | 1% | -3.43035 | -6.5393 | -16.786 | -79.433 |
| Constant | 5% | -2.86154 | -2.8903 | -4.234 | -40.040 |
| Constant | 10% | -2.56677 | -1.5384 | -2.809 | 0 |

**Wolfram-validated:** At $n = 100$: $\text{CV}_{1\%} = -3.43035 + \frac{-6.5393}{100} + \frac{-16.786}{10000} + \frac{-79.433}{1000000} = -3.4975$ ✓

### 3.6 Automatic Lag Selection

Default maximum lag: $p_{\max} = \lfloor 12 \cdot (n/100)^{1/4} \rfloor$

Selection via AIC (Akaike Information Criterion):
$$\text{AIC} = n \ln\!\left(\frac{\text{RSS}}{n}\right) + 2k$$

Iterate from $p = p_{\max}$ down to $p = 0$, select the lag that minimizes AIC.

---

## 4. Architecture & Design

### 4.1 Class Hierarchy

```
AbstractBase (lib/core/AbstractBase.cs)
  └── Adf (lib/statistics/adf/Adf.cs)
```

### 4.2 Enum

```csharp
public enum AdfRegression : byte
{
    NoConstant = 0,      // "nc" — no deterministic terms
    Constant = 1,        // "c"  — intercept only (DEFAULT)
    ConstantAndTrend = 2 // "ct" — intercept + linear trend
}
```

### 4.3 Constructor

```csharp
public Adf(int period = 50, int maxLag = 0, AdfRegression regression = AdfRegression.Constant)
```

- `period` ≥ 20 (rolling window of raw prices)
- `maxLag` = 0 → auto via $\lfloor 12(n/100)^{1/4} \rfloor$; otherwise explicit lag count
- `regression` = model specification

### 4.4 State Management

| Field | Type | Purpose |
|-------|------|---------|
| `_buffer` | `RingBuffer` | Rolling window of `period` raw values |
| `_lastValidValue` | `double` | NaN substitution |
| `_p_lastValidValue` | `double` | Bar correction state |

Unlike running-sum indicators, ADF requires **full window recomputation** each update (similar to [`Hurst`](../lib/statistics/hurst/Hurst.cs:23)). No incremental O(1) shortcut is possible due to the matrix regression.

### 4.5 Internal Components

| Component | Method | Purpose |
|-----------|--------|---------|
| **OLS Solver** | `SolveCholesky()` | Cholesky decomposition of X'X, stackalloc for k ≤ 8 |
| **MacKinnon P-Value** | `MacKinnonP()` | Polynomial + `NormCdf()` |
| **Normal CDF** | `NormCdf()` | `0.5 * Math.Erfc(-x * InvSqrt2)` |
| **Polynomial Eval** | `PolyVal()` | Horner's method |
| **Lag Selection** | `SelectLag()` | AIC minimization |
| **ADF Core** | `ComputeAdf()` | Builds design matrix, runs OLS, returns (stat, pValue) |

---

## 5. Performance Profile

### 5.1 Operation Count — Single Value

| Operation | Count | Notes |
|-----------|-------|-------|
| Differences (SUB) | $n - 1$ | Δy computation |
| Matrix fill (MUL/ADD) | $m \times k$ | Design matrix X |
| X'X (MUL/ADD) | $m \times k^2$ | Gram matrix |
| Cholesky (MUL/ADD/SQRT) | $k^3 / 6$ | Decomposition |
| Forward/Back substitution | $k^2$ | Solve |
| RSS (MUL/ADD) | $m \times k$ | Residuals |
| erfc | 1 | P-value |

**Total:** $O(n \times k)$ where $n$ = period, $k$ = 2-8 regressors

### 5.2 Memory

- Stack: `stackalloc double[k*k + k*3 + n]` ≈ 1-2 KB for typical parameters
- Heap: `RingBuffer(period)` — one allocation at construction

---

## 6. File Deliverables

```
lib/statistics/adf/
├── Adf.cs                      # Main implementation
├── Adf.md                      # Indicator documentation
├── adf.pine                    # PineScript v6 reference
├── tests/
│   ├── Adf.Tests.cs            # Unit tests (gold standard pattern)
│   └── Adf.Validation.Tests.cs # Cross-validation vs statsmodels
```

```
quantower/statistics/adf/
├── Adf.Quantower.cs            # Quantower adapter
├── tests/
│   └── Adf.Quantower.Tests.cs  # Adapter tests
```

### 6.1 Method Ordering (Gold Standard)

1. **Constructors** — `Adf(period, maxLag, regression)`, event-source, series
2. **Streaming** — `Update(TValue, bool)`, `Update(TSeries)`
3. **Static Batch** — `Batch(TSeries, ...)`, `Batch(ReadOnlySpan, Span, ...)`
4. **Calculate Bridge** — `Calculate(TSeries, ...)` → `(TSeries, Adf)`
5. **Lifecycle** — `Reset()`, `Dispose(bool)`

---

## 7. Validation Strategy

### 7.1 Python Reference

```python
from statsmodels.tsa.stattools import adfuller
import numpy as np

# Stationary series (white noise)
np.random.seed(42)
series = np.random.randn(100)
result = adfuller(series, maxlag=1, regression='c')
# Expected: p-value ≈ 0.0 (very small)

# Non-stationary series (random walk)
rw = np.cumsum(np.random.randn(100))
result = adfuller(rw, maxlag=1, regression='c')
# Expected: p-value ≈ 0.5-1.0
```

### 7.2 Test Cases

| Test | Input | Expected |
|------|-------|----------|
| White noise | `randn(100)` seed=42 | p-value < 0.05 |
| Random walk | `cumsum(randn(100))` | p-value > 0.05 |
| Known ADF stat | stat=-3.5, regression="c" | p = `Φ(poly(coefs, -3.5))` |
| Edge: period=20 | Minimum valid period | Works without error |
| Edge: NaN input | Series with NaN | Uses last valid value |
| Edge: constant series | All same value | p-value = NaN or 1.0 |

### 7.3 Cross-validation Tolerance

- Against statsmodels: $\epsilon < 10^{-6}$ for p-value
- Against MacKinnon critical values: $\epsilon < 10^{-4}$

---

## 8. Risk Assessment

| Risk | Severity | Mitigation |
|------|----------|------------|
| Breaking changes | ✅ None | New indicator, no existing API modified |
| Ill-conditioned X'X | Medium | Cholesky with diagonal check; return NaN if rank-deficient |
| Numerical drift | Low | Full recomputation each bar (no running sums to drift) |
| Performance | Low | O(n×k) acceptable for statistical test; not a hot-path indicator |
| Python parity | Medium | Comprehensive validation tests against statsmodels |

---

## 9. Future Considerations

- **KPSS test** — complementary stationarity test (opposite null hypothesis)
- **Cointegration refactoring** — could internally delegate ADF computation to `Adf` class
- **Phillips-Perron test** — non-parametric alternative to ADF
- **Zivot-Andrews test** — structural break unit root test

---

## 10. References

1. Dickey, D.A. & Fuller, W.A. (1979). "Distribution of the Estimators for Autoregressive Time Series with a Unit Root." *JASA*, 74(366), 427-431.
2. Said, S.E. & Dickey, D.A. (1984). "Testing for Unit Roots in ARMA Models of Unknown Order." *Biometrika*, 71(3), 599-607.
3. MacKinnon, J.G. (1994). "Approximate Asymptotic Distribution Functions for Unit-Root and Cointegration Tests." *JBES*, 12(2), 167-176.
4. MacKinnon, J.G. (2010). "Critical Values for Cointegration Tests." Queen's Economics Working Paper No. 1227.
5. Hamilton, J.D. (1994). *Time Series Analysis*. Princeton University Press.
