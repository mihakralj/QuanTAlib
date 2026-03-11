# PACF: Partial Autocorrelation Function

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Statistic                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`, `lag` (default 1)                      |
| **Outputs**      | Single series (Pacf)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [pacf.pine](pacf.pine)                       |

- The Partial Autocorrelation Function (PACF) measures the correlation between a time series and its lagged values, after removing the effects of all...
- Parameterized by `period`, `lag` (default 1).
- Output range: Varies (see docs).
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against mathematical properties and Durbin-Levinson recursion expectations.

> "Strip away the intermediaries, and you'll see the true direct relationship."

The Partial Autocorrelation Function (PACF) measures the correlation between a time series and its lagged values, after removing the effects of all intermediate lags. While ACF shows total correlation at each lag, PACF isolates the direct correlation, making it essential for AR model identification.

## Historical Context

The partial autocorrelation concept emerged from regression theory, where researchers needed to isolate the direct effect of a variable while controlling for confounding factors. The Durbin-Levinson algorithm (1960) provided an efficient recursive method to compute PACF, reducing the computational burden from solving a new system of equations for each lag.

In time series analysis, PACF became a cornerstone of the Box-Jenkins methodology (1970) for ARIMA model identification. While ACF helps identify MA order, PACF is the primary tool for identifying AR order.

## Architecture & Physics

The PACF indicator uses the Durbin-Levinson recursion to efficiently compute partial autocorrelations. This avoids the need to solve separate regression equations for each lag, instead building up the solution recursively from ACF values.

### Core Components

1. **RingBuffer**: Maintains the sliding window of `period` values
2. **ACF Computation**: Calculates all autocorrelations up to the target lag
3. **Durbin-Levinson Recursion**: Computes PACF from ACF values
4. **Coefficient Arrays**: Temporary storage for recursion (stack-allocated for small lags)

## Mathematical Foundation

### Partial Autocorrelation Definition

The partial autocorrelation at lag $k$, denoted $\phi_{kk}$, is the correlation between $X_t$ and $X_{t-k}$ after removing the linear dependence on $X_{t-1}, X_{t-2}, \ldots, X_{t-k+1}$.

Equivalently, $\phi_{kk}$ is the last coefficient in the AR(k) regression:

$$ X_t = \phi_{k1} X_{t-1} + \phi_{k2} X_{t-2} + \cdots + \phi_{kk} X_{t-k} + \epsilon_t $$

### Durbin-Levinson Algorithm

The algorithm recursively computes PACF from ACF values:

**Initialization:**
$$ \phi_{11} = r_1 $$

**Recursion for k = 2, 3, ..., K:**

$$ \phi_{kk} = \frac{r_k - \sum_{j=1}^{k-1} \phi_{k-1,j} \cdot r_{k-j}}{1 - \sum_{j=1}^{k-1} \phi_{k-1,j} \cdot r_j} $$

**Coefficient Update:**
$$ \phi_{kj} = \phi_{k-1,j} - \phi_{kk} \cdot \phi_{k-1,k-j} \quad \text{for } j = 1, \ldots, k-1 $$

### Key Properties

* $\phi_{11} = r_1$ (PACF at lag 1 equals ACF at lag 1)
* $-1 \leq \phi_{kk} \leq 1$ for all $k$
* For AR(p) processes, PACF cuts off after lag $p$ ($\phi_{kk} = 0$ for $k > p$)
* For MA(q) processes, PACF decays exponentially or sinusoidally
* For ARMA(p,q) processes, PACF exhibits complex behavior after lag $p-q$

### AR Process Identification

For an AR(p) process:

$$ X_t = \phi_1 X_{t-1} + \phi_2 X_{t-2} + \cdots + \phi_p X_{t-p} + \epsilon_t $$

The PACF exhibits:
* $\phi_{kk} \neq 0$ for $k \leq p$ (significant values)
* $\phi_{kk} = 0$ for $k > p$ (cuts off sharply)

This cutoff property makes PACF the primary diagnostic for AR order selection.

### AR(1) Example

For AR(1) process $X_t = \phi X_{t-1} + \epsilon_t$:
* $\phi_{11} = \phi$ (the AR coefficient)
* $\phi_{kk} = 0$ for $k > 1$

### AR(2) Example

For AR(2) process $X_t = \phi_1 X_{t-1} + \phi_2 X_{t-2} + \epsilon_t$:
* $\phi_{11}$ and $\phi_{22}$ are non-zero
* $\phi_{kk} = 0$ for $k > 2$

## Performance Profile

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | ~100 ns/bar | ACF loop + Durbin-Levinson recursion |
| **Allocations** | 0 | Zero-allocation for lag ≤ 64 (stackalloc) |
| **Complexity** | O(period + lag²) | ACF computation + Durbin-Levinson |
| **Accuracy** | 8 | Good accuracy; numerical stability from recursion |

### Operation Count (per update)

| Operation | Count | Notes |
| :--- | :---: | :--- |
| ADD/SUB | ~3N + K² | Mean, variance, autocovariance, recursion |
| MUL | ~2N + 2K² | Cross products, coefficient updates |
| DIV | K + 2 | ACF ratios, recursion denominators |

Where N = period, K = lag.

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **TA-Lib** | N/A | Not available in TA-Lib |
| **Skender** | N/A | Not available in Skender |
| **Tulip** | N/A | Not available in Tulip |
| **Mathematical** | ✅ | Validated against theoretical properties |

PACF is validated through mathematical properties:
- $\phi_{11} = r_1$ (PACF at lag 1 equals ACF at lag 1)
- Bounded output [-1, 1]
- Constant series returns 0
- AR(1) process produces PACF ≈ φ at lag 1, ≈ 0 for higher lags

## Common Pitfalls

1. **Period vs Lag Constraint**: Period must be greater than `lag + 1`. Insufficient data produces undefined or unstable results.

2. **PACF ≠ ACF**: A common confusion is treating PACF and ACF identically. While $\phi_{11} = r_1$, higher-order PACF values differ significantly from ACF.

3. **Warmup Period**: PACF requires a full window (`period` values) plus sufficient data for stable ACF estimates. Values during warmup are unreliable.

4. **Numerical Stability**: For very high lags, the Durbin-Levinson recursion can accumulate numerical errors. The denominator approaching zero indicates potential instability.

5. **AR vs MA Confusion**: Sharp PACF cutoff indicates AR; sharp ACF cutoff indicates MA. Using the wrong criterion leads to model misspecification.

6. **Significance Testing**: PACF values should be tested against confidence bounds. For white noise, 95% confidence bounds are approximately $\pm 1.96/\sqrt{n}$.

7. **Non-Stationarity**: Like ACF, PACF assumes stationarity. Trending data should be differenced first.

## Usage

```csharp
using QuanTAlib;

// Create a 20-period PACF indicator with lag 1
var pacf = new Pacf(period: 20, lag: 1);

// Update with new values
var result = pacf.Update(new TValue(DateTime.UtcNow, 100.0));

// Access the last calculated PACF value
Console.WriteLine($"PACF(1): {pacf.Last.Value}");

// Verify key property: PACF(1) should equal ACF(1)
var acf = new Acf(period: 20, lag: 1);
// ... feed same data to both ...
// pacf.Last.Value ≈ acf.Last.Value

// Chained usage
var source = new TSeries();
var pacfChained = new Pacf(source, period: 20, lag: 1);

// Static batch calculation
var output = Pacf.Calculate(source, period: 20, lag: 1);

// Span-based calculation
Span<double> outputSpan = stackalloc double[source.Count];
Pacf.Batch(source.Values, outputSpan, period: 20, lag: 1);
```

## Applications

### AR Order Identification

The primary application of PACF is determining the order of AR models:
- PACF cuts off after lag p → suggests AR(p)
- Combined with ACF cutoff after lag q → suggests ARMA(p,q)

### Model Validation

After fitting an AR model, residual PACF should show no significant values, indicating all autocorrelation structure has been captured.

### Lead-Lag Analysis

In financial markets, PACF can reveal direct lead-lag relationships between assets after controlling for intermediate effects.

### Signal Processing

PACF is used in linear prediction and filter design, where the partial correlation structure determines optimal predictor coefficients.

## Comparison: ACF vs PACF

| Property | ACF | PACF |
| :--- | :--- | :--- |
| **Measures** | Total correlation at lag k | Direct correlation at lag k |
| **AR(p) process** | Exponential/sinusoidal decay | Cuts off after lag p |
| **MA(q) process** | Cuts off after lag q | Exponential/sinusoidal decay |
| **ARMA(p,q)** | Tails off | Tails off |
| **Primary use** | MA order identification | AR order identification |
| **Lag 1 value** | $r_1$ | $\phi_{11} = r_1$ |

## References

- Box, G.E.P., Jenkins, G.M. (1970). *Time Series Analysis: Forecasting and Control*. Holden-Day.
- Durbin, J. (1960). "The fitting of time series models." *Review of the International Statistical Institute*, 28, 233-243.
- Levinson, N. (1946). "The Wiener RMS error criterion in filter design and prediction." *Journal of Mathematics and Physics*, 25, 261-278.
- Hamilton, J.D. (1994). *Time Series Analysis*. Princeton University Press.
