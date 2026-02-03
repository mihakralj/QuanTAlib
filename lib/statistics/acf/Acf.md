# ACF: Autocorrelation Function

> "The past doesn't predict the future, but it whispers patterns to those who listen."

The Autocorrelation Function (ACF) measures the correlation of a time series with a lagged copy of itself. It is fundamental for identifying repeating patterns, seasonal effects, and determining the order of time series models like ARMA/ARIMA.

## Historical Context

Autocorrelation was formalized by statisticians in the early 20th century, with key contributions from Udny Yule (1927) and Gilbert Walker. The concept became central to time series analysis with Box and Jenkins' influential 1970 work on ARIMA models.

In financial markets, ACF reveals whether past returns predict future returns. A significant positive ACF at lag 1 suggests momentum; significant negative ACF suggests mean reversion. White noise (truly random data) should exhibit near-zero ACF at all lags.

## Architecture & Physics

The ACF indicator uses a sliding window (RingBuffer) to maintain the last `N` data points. While the theoretical formula suggests O(N) complexity per update, the implementation employs running sums where possible and periodic resynchronization to manage floating-point drift.

### Core Components

1. **RingBuffer**: Maintains the sliding window of `period` values
2. **Running Sums**: Tracks sum and sum of squares for mean/variance calculation
3. **Autocovariance Calculation**: Computes correlation at the specified lag
4. **Resync Mechanism**: Recalculates sums every 1000 updates to prevent drift

## Mathematical Foundation

### Autocorrelation Coefficient

The ACF at lag $k$ is defined as:

$$ r_k = \frac{\gamma_k}{\gamma_0} $$

where:

* $\gamma_k$ is the autocovariance at lag $k$
* $\gamma_0$ is the variance (autocovariance at lag 0)

### Autocovariance at Lag k

$$ \gamma_k = \frac{1}{n} \sum_{t=k+1}^{n} (x_t - \mu)(x_{t-k} - \mu) $$

where:

* $n$ is the sample size (period)
* $\mu$ is the sample mean
* $x_t$ is the value at time $t$
* $x_{t-k}$ is the value at time $t-k$

### Variance (Autocovariance at Lag 0)

$$ \gamma_0 = \frac{1}{n} \sum_{t=1}^{n} (x_t - \mu)^2 $$

### Properties

* $r_0 = 1$ (correlation with itself at lag 0)
* $-1 \leq r_k \leq 1$ for all $k$
* $r_k = r_{-k}$ (symmetry)
* For stationary processes, ACF decays towards zero as lag increases
* For MA(q) processes, ACF cuts off after lag $q$
* For AR(p) processes, ACF decays exponentially or sinusoidally

### AR(1) Process Example

For an AR(1) process $X_t = \phi X_{t-1} + \epsilon_t$:

$$ r_k = \phi^k $$

This means ACF decays geometrically at rate $\phi$.

## Performance Profile

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | ~50 ns/bar | Autocovariance loop required |
| **Allocations** | 0 | Zero-allocation in hot path |
| **Complexity** | O(period) | Due to autocovariance calculation |
| **Accuracy** | 8 | Good accuracy with biased estimator; resync prevents drift |

### Operation Count (per update)

| Operation | Count | Notes |
| :--- | :---: | :--- |
| ADD/SUB | ~3N | Mean, variance, autocovariance |
| MUL | ~2N | Squared deviations, cross products |
| DIV | 3 | Mean, variance, final ratio |

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **TA-Lib** | N/A | Not available in TA-Lib |
| **Skender** | N/A | Not available in Skender |
| **Mathematical** | ✅ | Validated against AR(1) theoretical values |

ACF is validated through mathematical properties:
- Bounded output [-1, 1]
- Constant series returns 0 (no correlation beyond lag 0)
- Alternating sequence produces negative ACF
- AR(1) process produces ACF ≈ φ^k

## Common Pitfalls

1. **Period vs Lag Constraint**: Period must be greater than `lag + 1`. Common mistake is setting period = lag, which produces undefined results.

2. **Warmup Period**: ACF requires a full window (`period` values) to be meaningful. Values during warmup may be unreliable.

3. **Non-Stationarity**: ACF assumes stationarity. Trending data should be differenced first.

4. **Significance Testing**: ACF values should be tested against confidence bounds. For white noise, 95% confidence bounds are approximately $\pm 1.96/\sqrt{n}$.

5. **Lag Selection**: Higher lags require larger periods for statistical significance. Rule of thumb: period ≥ 4 × lag.

## Usage

```csharp
using QuanTAlib;

// Create a 20-period ACF indicator with lag 1
var acf = new Acf(period: 20, lag: 1);

// Update with new values
var result = acf.Update(new TValue(DateTime.UtcNow, 100.0));

// Access the last calculated ACF value
Console.WriteLine($"ACF(1): {acf.Last.Value}");

// Chained usage
var source = new TSeries();
var acfChained = new Acf(source, period: 20, lag: 1);

// Static batch calculation
var output = Acf.Calculate(source, period: 20, lag: 1);

// Span-based calculation
Span<double> outputSpan = stackalloc double[source.Count];
Acf.Batch(source.Values, outputSpan, period: 20, lag: 1);
```

## Applications

### ARIMA Model Identification

ACF patterns help identify the order of MA components:
- Sharp cutoff after lag q suggests MA(q)
- Gradual decay suggests AR component

### Mean Reversion Detection

Negative ACF at lag 1 suggests mean-reverting behavior, useful for pairs trading strategies.

### Seasonality Detection

Significant ACF at seasonal lags (e.g., lag 12 for monthly data with annual seasonality) indicates periodic patterns.

### Random Walk Testing

A random walk should have ACF ≈ 0 at all lags. Significant ACF values indicate predictable structure.

## References

- Box, G.E.P., Jenkins, G.M. (1970). *Time Series Analysis: Forecasting and Control*. Holden-Day.
- Hamilton, J.D. (1994). *Time Series Analysis*. Princeton University Press.
- Yule, G.U. (1927). "On a Method of Investigating Periodicities in Disturbed Series." *Philosophical Transactions of the Royal Society*.