# SDCHANNEL: Standard Deviation Channel

> *Standard deviation channels center on a moving average and let dispersion define the expected range.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Channel                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period` (default 20), `multiplier` (default 2.0)                      |
| **Outputs**      | Multiple series (Upper, Lower)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [sdchannel.pine](sdchannel.pine)                       |

- Standard Deviation Channel plots a linear regression line through price data with parallel bands at a specified number of standard deviations of re...
- Parameterized by `period` (default 20), `multiplier` (default 2.0).
- Output range: Tracks input.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

Standard Deviation Channel plots a linear regression line through price data with parallel bands at a specified number of standard deviations of residuals above and below. Unlike Bollinger Bands which measure deviation from a moving average, SDCHANNEL measures deviation from the best-fit trend line, capturing how much price wanders from its underlying trajectory rather than from its simple average. The algorithm is identical to REGCHANNEL; the distinction is purely a naming convention found across different platforms and literature.

## Historical Context

Linear regression channels emerged from statistical methods applied to financial markets in the 1980s and 1990s. Gilbert Raff popularized "Raff Regression Channels" which use the same principle: fit a least-squares line to a price window and draw parallel bands at the residual standard deviation distance.

The critical distinction from moving average bands: a regression line projects the trend direction, not the average level. The residuals (actual minus predicted prices) measure how much price deviates from this directional fit. When residuals are small, price is tracking the trend cleanly. When residuals grow, the trend is becoming noisy or price is breaking away from its recent trajectory.

Some platforms label this indicator "Standard Deviation Channel" (emphasizing the band-width metric), while others use "Regression Channel" (emphasizing the centerline method). Both SDCHANNEL and REGCHANNEL implement identical OLS regression with population standard deviation.

## Architecture & Physics

### 1. Linear Regression (Middle Band)

The best-fit line through the lookback window using ordinary least squares, with time indices $x_i = i$ and prices $y_i = P_i$:

$$
m = \frac{n \sum x_i y_i - \sum x_i \sum y_i}{n \sum x_i^2 - \left(\sum x_i\right)^2}
$$

$$
b = \frac{\sum y_i - m \sum x_i}{n}
$$

The middle band is the regression line evaluated at the current bar (the rightmost point):

$$
\text{Middle}_t = m \cdot (n - 1) + b
$$

### 2. Residual Standard Deviation

For each point in the window, the residual is the difference between the actual and predicted value. The population standard deviation of these residuals:

$$
\sigma_t = \sqrt{\frac{1}{n} \sum_{i=0}^{n-1} \left(y_i - (m \cdot i + b)\right)^2}
$$

Note: this uses population $\sigma$ (dividing by $n$), not sample $s$ (dividing by $n - 1$).

### 3. Band Construction

$$
U_t = \text{Middle}_t + k \cdot \sigma_t
$$

$$
L_t = \text{Middle}_t - k \cdot \sigma_t
$$

where $k$ is the multiplier (default 2.0). Under normality assumptions, $k = 2$ captures approximately 95% of residuals.

### 4. Residual Properties

By definition of least squares: (1) the sum of residuals equals zero, (2) residuals are uncorrelated with the $x$ values, and (3) when $\sigma = 0$, all points lie exactly on the regression line.

### 5. Complexity

Per bar: $O(n)$ due to two passes over the window (sums, then residuals). Memory: a ring buffer of $n$ doubles. The index sums $\sum x$ and $\sum x^2$ are precomputed constants for fixed $n$.

## Mathematical Foundation

### Parameters

| Symbol | Name | Default | Constraint | Description |
|--------|------|---------|------------|-------------|
| $n$ | period | 20 | $> 1$ | Lookback window for regression |
| $k$ | multiplier | 2.0 | $> 0$ | Stddev multiplier for band width |

### Precomputed Constants

$$
\sum_{i=0}^{n-1} i = \frac{n(n-1)}{2}, \qquad \sum_{i=0}^{n-1} i^2 = \frac{n(n-1)(2n-1)}{6}
$$

$$
D = n \sum i^2 - \left(\sum i\right)^2
$$

For $n \geq 2$, $D > 0$ always holds, so the slope denominator is never zero.

### Slope Interpretation

| Slope | Market State |
|-------|-------------|
| $m > 0$ | Uptrend within the window |
| $m < 0$ | Downtrend within the window |
| $m \approx 0$ | Sideways / consolidation |
| $|m|$ increasing | Trend accelerating |
| $|m|$ decreasing | Trend decelerating |

### Output Interpretation

| Output | Interpretation |
|--------|---------------|
| Price at upper band | Overextended above trend |
| Price at lower band | Overextended below trend |
| $\sigma \to 0$ | Perfect linear trend; bands collapse |
| Band width expanding | Increasing noise around the trend |

## Performance Profile

### Operation Count (Streaming Mode)

SDCHANNEL is algorithmically identical to REGCHANNEL — two $O(n)$ passes per bar:

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

For period 20: ~366 cycles/bar. Identical performance characteristics to REGCHANNEL.

### Batch Mode (SIMD Analysis)

Both passes iterate over contiguous memory, enabling SIMD vectorization of the inner loops:

| Operation | Scalar Ops | SIMD Ops (AVX-512) | Speedup |
| :--- | :---: | :---: | :---: |
| Pass 1: sum_y, sum_xy | $2n$ | $n/8$ | ~16× |
| Pass 2: residuals + squared sum | $4n$ | $n/2$ | ~8× |
| Slope/intercept/bands | 9 | 9 | 1× |

## Resources

- Raff, G. (1991). "Trading the Regression Channel." *Technical Analysis of Stocks & Commodities*.
- Draper, N. & Smith, H. (1998). *Applied Regression Analysis*. Wiley.
- Bulkowski, T. (2005). *Encyclopedia of Chart Patterns*, 2nd ed. Wiley.
- Kaufman, P. (2013). *Trading Systems and Methods*, 5th ed. Wiley.
