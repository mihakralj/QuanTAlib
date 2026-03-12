# TTM_LRC: TTM Linear Regression Channel

> *Linear regression channels project the statistical trend and drape standard deviation curtains around it.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Channel                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period` (default 100)                      |
| **Outputs**      | Multiple series (Midline, Upper1, Lower1, Upper2, Lower2)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |

- TTM Linear Regression Channel plots a least-squares regression line through price data with dual standard deviation bands at $\pm 1\sigma$ and $\pm...
- Parameterized by `period` (default 100).
- Output range: Tracks input.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

TTM Linear Regression Channel plots a least-squares regression line through price data with dual standard deviation bands at $\pm 1\sigma$ and $\pm 2\sigma$. Developed by John Carter as part of his TTM (Trade The Markets) indicator suite, it extends the standard regression channel by providing two band levels that create statistically meaningful trading zones. The algorithm is identical to REGCHANNEL/SDCHANNEL in its regression and residual computation, but uses a longer default period (100) and emits four bands instead of two.

## Historical Context

John Carter developed the TTM LRC as part of his Trade The Markets methodology, popularized in his book *Mastering the Trade* (2005). Carter's approach combines linear regression channels with his TTM Squeeze indicator to identify high-probability setups: the regression channel defines the trend envelope while the squeeze identifies momentum compression within it.

The underlying mathematics (OLS regression + population standard deviation of residuals) dates back to Gauss and Legendre in the early 1800s. Gilbert Raff applied regression channels to trading in the 1990s. Carter's contribution was the dual-band structure and the specific parameter choices (period 100, dual $\sigma$ levels) calibrated for swing trading on daily and intraday charts.

The dual band structure creates five statistical zones. Under normality assumptions, 68% of price action falls within $\pm 1\sigma$ and 95% within $\pm 2\sigma$. Price reaching the $\pm 2\sigma$ band represents a statistically significant deviation from trend, while the $\pm 1\sigma$ bands define the boundaries of "normal" price oscillation.

## Architecture & Physics

### 1. Linear Regression (Midline)

The best-fit line through the lookback window using ordinary least squares:

$$
m = \frac{n \sum x_i y_i - \sum x_i \sum y_i}{n \sum x_i^2 - \left(\sum x_i\right)^2}
$$

$$
b = \frac{\sum y_i - m \sum x_i}{n}
$$

The midline is the regression evaluated at the current bar:

$$
\text{Mid}_t = m \cdot (n - 1) + b
$$

### 2. Residual Standard Deviation

Population standard deviation of the differences between actual and predicted values:

$$
\sigma_t = \sqrt{\frac{1}{n} \sum_{i=0}^{n-1} \left(y_i - (m \cdot i + b)\right)^2}
$$

### 3. Dual Band Construction

Four bands at two statistical distances:

$$
U_{1,t} = \text{Mid}_t + 1 \cdot \sigma_t, \qquad L_{1,t} = \text{Mid}_t - 1 \cdot \sigma_t
$$

$$
U_{2,t} = \text{Mid}_t + k \cdot \sigma_t, \qquad L_{2,t} = \text{Mid}_t - k \cdot \sigma_t
$$

where $k$ is the outer deviation multiplier (default 2.0). The inner bands ($\pm 1\sigma$) are always at one standard deviation.

### 4. Slope and R-Squared

The slope $m$ indicates trend direction and strength. The coefficient of determination $R^2$ measures how well the linear model fits:

$$
R^2 = 1 - \frac{\text{SSR}}{\text{SST}} = 1 - \frac{\sum (y_i - \hat{y}_i)^2}{\sum (y_i - \bar{y})^2}
$$

High $R^2$ (near 1.0) indicates price is moving linearly; low $R^2$ indicates choppy or non-linear behavior.

### 5. Complexity

Per bar: $O(n)$ due to two loops over the window. Memory: a ring buffer of $n$ doubles. The index sums $\sum x$ and $\sum x^2$ are precomputed constants.

## Mathematical Foundation

### Parameters

| Symbol | Name | Default | Constraint | Description |
|--------|------|---------|------------|-------------|
| $n$ | period | 100 | $> 1$ | Lookback window for regression |
| $k$ | deviations | 2.0 | $> 0$ | Outer band stddev multiplier |

### Statistical Zone Interpretation

| Zone | Probability | Interpretation |
|------|------------|----------------|
| Above $+2\sigma$ | ~2.5% | Extremely overbought relative to trend |
| $+1\sigma$ to $+2\sigma$ | ~13.5% | Overbought |
| $-1\sigma$ to $+1\sigma$ | ~68% | Normal oscillation around trend |
| $-2\sigma$ to $-1\sigma$ | ~13.5% | Oversold |
| Below $-2\sigma$ | ~2.5% | Extremely oversold relative to trend |

## Performance Profile

### Operation Count (Streaming Mode)

TTM_LRC extends REGCHANNEL with dual bands and $R^2$ computation. Two $O(n)$ passes plus additional statistics:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ADD (sum_y accumulation, pass 1) | $n$ | 1 | $n$ |
| FMA (i × y for sum_xy, pass 1) | $n$ | 4 | $4n$ |
| MUL + DIV (slope, intercept, mean_y) | 5 | ~9 | 45 |
| FMA (slope × i + intercept, pass 2) | $n$ | 4 | $4n$ |
| SUB (residual, pass 2) | $n$ | 1 | $n$ |
| MUL (residual², pass 2) | $n$ | 3 | $3n$ |
| ADD (ssr accumulation, pass 2) | $n$ | 1 | $n$ |
| SUB + MUL + ADD (sst, pass 2) | $2n$ | 2 | $4n$ |
| DIV (ssr/n, sst check, R²) | 3 | 15 | 45 |
| SQRT (σ) | 1 | 20 | 20 |
| MUL + ADD/SUB (4 bands: ±1σ, ±kσ) | 6 | ~2 | 12 |
| **Total** | **~$9n + 15$** | — | **~$18n + 122$ cycles** |

For period 100: ~1922 cycles/bar. The longer default period (100 vs 20) makes the window scans significantly more expensive than REGCHANNEL.

### Batch Mode (SIMD Analysis)

Both passes iterate over contiguous ring buffer memory, enabling SIMD vectorization:

| Operation | Scalar Ops | SIMD Ops (AVX-512) | Speedup |
| :--- | :---: | :---: | :---: |
| Pass 1: sum_y, sum_xy | $2n$ | $n/8$ | ~16× |
| Pass 2: residuals + ssr + sst | $6n$ | $3n/4$ | ~8× |
| Slope/intercept/bands/R² | 15 | 15 | 1× |

## Resources

- Carter, J. (2005). *Mastering the Trade*. McGraw-Hill.
- Raff, G. (1991). "Trading the Regression Channel." *Technical Analysis of Stocks & Commodities*.
- Draper, N. & Smith, H. (1998). *Applied Regression Analysis*. Wiley.
