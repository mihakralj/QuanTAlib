# REGCHANNEL: Linear Regression Channel

> "Linear regression isn't about predicting the future—it's about understanding where price *should* be given recent history, and measuring how far it's strayed."

The Linear Regression Channel (REGCHANNEL) plots a best-fit line through price data over a specified period, with parallel bands at a configurable standard deviation distance. This implementation uses ordinary least squares (OLS) regression with population standard deviation of residuals, providing a statistically grounded view of trend direction and price deviation.

## Historical Context

Linear regression channels emerged from basic statistical analysis applied to financial markets. The concept combines two fundamental statistical tools: linear regression (fitting a line to minimize squared errors) and standard deviation (measuring dispersion around that line).

Unlike moving average envelopes that simply offset from a smoothed price, regression channels adapt their slope to the underlying trend and their width to actual price volatility around that trend. This makes them particularly useful for identifying when prices have deviated significantly from their recent trajectory.

The indicator is functionally identical to SDCHANNEL but uses "Regchannel" naming convention, which may be preferred in some trading platforms and literature.

## Architecture & Physics

### 1. Sliding Window Buffer

The indicator maintains a rolling window of the most recent `period` price values:

$$
W_t = \{P_{t-n+1}, P_{t-n+2}, \ldots, P_t\}
$$

where $n = \min(t+1, \text{period})$. During warmup ($t < \text{period}$), all available values are used.

### 2. Linear Regression via Least Squares

For each update, the indicator computes the best-fit line $y = mx + b$ using the normal equations:

$$
m = \frac{n \sum_{i=0}^{n-1} x_i y_i - \sum_{i=0}^{n-1} x_i \sum_{i=0}^{n-1} y_i}{n \sum_{i=0}^{n-1} x_i^2 - \left(\sum_{i=0}^{n-1} x_i\right)^2}
$$

$$
b = \frac{\sum_{i=0}^{n-1} y_i - m \sum_{i=0}^{n-1} x_i}{n}
$$

where $x_i = i$ (time index) and $y_i = P_i$ (price at that index).

### 3. Regression Value Calculation

The middle line value at the current bar (rightmost point of the regression line):

$$
\text{Middle}_t = m \cdot (n-1) + b
$$

This represents the expected price based on the linear trend through the window.

### 4. Standard Deviation of Residuals

The indicator computes population standard deviation of the residuals (differences between actual and predicted values):

$$
\sigma_t = \sqrt{\frac{\sum_{i=0}^{n-1} (y_i - \hat{y}_i)^2}{n}}
$$

where $\hat{y}_i = m \cdot i + b$ is the predicted value at position $i$.

### 5. Channel Bands

Upper and lower bands are placed at a configurable multiple of the standard deviation:

$$
\text{Upper}_t = \text{Middle}_t + k \cdot \sigma_t
$$

$$
\text{Lower}_t = \text{Middle}_t - k \cdot \sigma_t
$$

where $k$ is the multiplier parameter (default 2.0).

## Mathematical Foundation

### Efficient Computation Using Running Sums

Rather than recalculating sums from scratch each bar, the implementation maintains running sums and adjusts them incrementally. For a sliding window of size $n$:

- $\sum x = 0 + 1 + \ldots + (n-1) = \frac{n(n-1)}{2}$
- $\sum x^2 = 0^2 + 1^2 + \ldots + (n-1)^2 = \frac{n(n-1)(2n-1)}{6}$

These are constants for a fixed period, computed once at construction.

### Denominator and Numerical Stability

The denominator in the slope calculation:

$$
D = n \sum x^2 - \left(\sum x\right)^2
$$

For $n \geq 2$, this is always positive, ensuring numerical stability. The implementation guards against $D = 0$ (which can only occur for $n = 1$).

### Residual Calculation

For each point in the window:

$$
r_i = y_i - (m \cdot i + b)
$$

The sum of squared residuals:

$$
\text{SSR} = \sum_{i=0}^{n-1} r_i^2
$$

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ADD/SUB | ~3n+15 | 1 | ~3n+15 |
| MUL | ~2n+10 | 3 | ~6n+30 |
| DIV | 4 | 15 | 60 |
| SQRT | 1 | 15 | 15 |
| Ring buffer ops | 2 | 5 | 10 |
| **Total** | — | — | **~9n+130** |

For period=20: approximately 310 cycles per bar.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | Exact OLS regression; population σ |
| **Timeliness** | 7/10 | Inherent lag from lookback window |
| **Smoothness** | 8/10 | Regression naturally smooths |
| **Responsiveness** | 6/10 | Slower to react than EMA-based channels |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | No direct equivalent |
| **Skender** | N/A | No direct equivalent |
| **Tulip** | N/A | No direct equivalent |
| **Manual** | ✅ | Verified against hand calculations |

Linear regression channels are not commonly found in standard TA libraries with this exact specification. Validation relies on mathematical verification against known formulas.

## Common Pitfalls

1. **Warmup Period**: The indicator requires `period` bars to reach full accuracy. During warmup, it uses all available data but may produce different results than post-warmup.

2. **Slope Interpretation**: A positive slope indicates uptrend within the window; negative indicates downtrend. The magnitude indicates trend strength.

3. **Band Width = 0**: When prices fall perfectly on a line (zero residuals), bands collapse to the middle line. This is mathematically correct but visually unexpected.

4. **Standard Deviation Choice**: This implementation uses population σ (dividing by n), not sample σ (dividing by n-1). Some implementations differ.

5. **Memory Footprint**: Each instance requires a RingBuffer of `period` doubles (~8 bytes each) plus state structs (~80 bytes). For period=20: ~240 bytes per instance.

6. **isNew Parameter**: When `isNew=false`, the indicator rolls back to the previous state before incorporating the update. This enables bar correction without state accumulation errors.

## References

- Draper, N.R. & Smith, H. (1998). "Applied Regression Analysis." Wiley.
- Murphy, J.J. (1999). "Technical Analysis of the Financial Markets." New York Institute of Finance.
- PineScript Reference: Linear Regression implementation patterns.
