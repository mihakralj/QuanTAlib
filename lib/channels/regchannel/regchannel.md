# REGCHANNEL: Linear Regression Channel

> *A regression line flanked by standard error bands — the channel where statistics meets price trajectory.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Channel                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period` (default 20), `multiplier` (default 2.0)                      |
| **Outputs**      | Multiple series (Upper, Lower)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [regchannel.pine](regchannel.pine)                       |

- Linear Regression Channel plots a best-fit line through price data over a specified period with parallel bands at a configurable standard deviation...
- **Similar:** [SDChannel](../sdchannel/sdchannel.md), [BBands](../bbands/bbands.md) | **Complementary:** R-squared for trend strength | **Trading note:** Linear regression channel; mean-reversion trades at band extremes.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

Linear Regression Channel plots a best-fit line through price data over a specified period with parallel bands at a configurable standard deviation of residuals. Unlike moving average envelopes that offset from a smoothed price, regression channels adapt their slope to the underlying trend and their width to actual dispersion around that trend. The algorithm uses ordinary least squares with precomputed index sums, requiring two passes per bar: one for the regression coefficients and one for the residual standard deviation.

## Historical Context

Linear regression channels emerged from basic statistical methods applied to financial markets in the 1980s and 1990s. Gilbert Raff popularized "Raff Regression Channels" which use the same concept: fit a line, measure how far price wanders from it, and draw parallel bands at that distance.

The key insight separating regression channels from moving average envelopes: a moving average treats all recent prices equally, while linear regression fits a line that best explains the directional trend. The residuals (actual minus predicted) measure how much price deviates from this trajectory. When prices consistently touch the upper band, the trend is accelerating; when they compress toward the regression line, momentum is fading.

REGCHANNEL and SDCHANNEL implement identical algorithms. The distinction is purely naming convention: some platforms and literature label the indicator "Regression Channel" while others use "Standard Deviation Channel." Both compute OLS regression with population standard deviation of residuals.

## Architecture & Physics

### 1. Sliding Window Buffer

The indicator maintains a rolling window of the most recent $n$ price values:

$$
W_t = \{P_{t-n+1},\; P_{t-n+2},\; \ldots,\; P_t\}
$$

### 2. Linear Regression via Normal Equations

For each update, the best-fit line $y = mx + b$ is computed using time indices $x_i = i$ and prices $y_i = P_i$:

$$
m = \frac{n \sum x_i y_i - \sum x_i \sum y_i}{n \sum x_i^2 - \left(\sum x_i\right)^2}
$$

$$
b = \frac{\sum y_i - m \sum x_i}{n}
$$

The middle band value is the regression line evaluated at the rightmost point:

$$
\text{Middle}_t = m \cdot (n - 1) + b
$$

### 3. Standard Deviation of Residuals

The population standard deviation of the differences between actual and predicted values:

$$
\sigma_t = \sqrt{\frac{1}{n} \sum_{i=0}^{n-1} \left(y_i - (m \cdot i + b)\right)^2}
$$

### 4. Band Construction

Upper and lower bands at a configurable multiple $k$ of the residual standard deviation:

$$
U_t = \text{Middle}_t + k \cdot \sigma_t
$$

$$
L_t = \text{Middle}_t - k \cdot \sigma_t
$$

### 5. Complexity

Per bar: $O(n)$ due to two loops over the window (one for sums, one for residuals). Memory: a ring buffer of $n$ doubles. The index sums $\sum x$ and $\sum x^2$ are constants for fixed $n$ and can be precomputed at construction.

## Mathematical Foundation

### Parameters

| Symbol | Name | Default | Constraint | Description |
|--------|------|---------|------------|-------------|
| $n$ | period | 20 | $> 1$ | Lookback window for regression |
| $k$ | multiplier | 2.0 | $> 0$ | Stddev multiplier for band width |

### Precomputed Constants

For a fixed period $n$, the index sums are constants:

$$
\sum_{i=0}^{n-1} i = \frac{n(n-1)}{2}, \qquad \sum_{i=0}^{n-1} i^2 = \frac{n(n-1)(2n-1)}{6}
$$

$$
D = n \sum i^2 - \left(\sum i\right)^2
$$

For $n \geq 2$, $D > 0$ always, ensuring numerical stability.

### Output Interpretation

| Output | Interpretation |
|--------|---------------|
| Positive slope | Uptrend within the window |
| Negative slope | Downtrend within the window |
| $\sigma \to 0$ | Price perfectly linear; bands collapse to the regression line |
| Price at upper band | Overextended above trend (mean-reversion signal) |
| Price at lower band | Overextended below trend |
| Band width expanding | Increasing residual dispersion; trend becoming noisy |

## Performance Profile

### Operation Count (Streaming Mode)

REGCHANNEL requires two $O(n)$ passes per bar: one for regression sums, one for residual standard deviation:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ADD (sum_y accumulation, pass 1) | $n$ | 1 | $n$ |
| FMA (i × y for sum_xy, pass 1) | $n$ | 4 | $4n$ |
| MUL + DIV (slope, intercept) | 4 | ~9 | 36 |
| FMA (slope × i + intercept, pass 2) | $n$ | 4 | $4n$ |
| SUB (residual = y - predicted) | $n$ | 1 | $n$ |
| MUL (residual², pass 2) | $n$ | 3 | $3n$ |
| ADD (ssr accumulation, pass 2) | $n$ | 1 | $n$ |
| DIV (ssr / n) | 1 | 15 | 15 |
| SQRT (σ) | 1 | 20 | 20 |
| MUL + ADD/SUB (bands) | 3 | ~5 | 15 |
| **Total** | **~$7n + 9$** | — | **~$14n + 86$ cycles** |

For period 20: ~366 cycles/bar. The two window scans dominate. Index sums $\sum x$ and $\sum x^2$ are precomputed constants.

### Batch Mode (SIMD Analysis)

Both passes iterate over a contiguous ring buffer, making them prime candidates for SIMD vectorization:

| Operation | Scalar Ops | SIMD Ops (AVX-512) | Speedup |
| :--- | :---: | :---: | :---: |
| Pass 1: sum_y, sum_xy | $2n$ | $n/8$ | ~16× |
| Pass 2: residuals + squared sum | $4n$ | $n/2$ | ~8× |
| Slope/intercept/bands | 9 | 9 | 1× |

## Resources

- Raff, G. (1991). "Trading the Regression Channel." *Technical Analysis of Stocks & Commodities*.
- Draper, N. & Smith, H. (1998). *Applied Regression Analysis*. Wiley.
- Kaufman, P. (2013). *Trading Systems and Methods*, 5th ed. Wiley.
