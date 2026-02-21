# TTM_LRC: TTM Linear Regression Channel

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

### Pseudo-code

```
function ttm_lrc(source[], period, deviations):
    buf = ring_buffer(period)
    sum_x  = period * (period - 1) / 2
    sum_x2 = period * (period - 1) * (2 * period - 1) / 6
    denom  = period * sum_x2 - sum_x * sum_x

    for each bar t:
        buf.add(source[t])
        n = buf.count

        // pass 1: regression
        sum_y = 0, sum_xy = 0
        for i = 0 to n-1:
            y = buf[i]
            sum_y  += y
            sum_xy += i * y

        slope     = (n * sum_xy - sum_x * sum_y) / denom
        intercept = (sum_y - slope * sum_x) / n
        midline   = slope * (n - 1) + intercept

        // pass 2: residuals
        ssr = 0, sst = 0
        mean_y = sum_y / n
        for i = 0 to n-1:
            predicted = slope * i + intercept
            residual  = buf[i] - predicted
            ssr += residual * residual
            sst += (buf[i] - mean_y)^2

        stddev   = sqrt(ssr / n)
        r_squared = sst > 0 ? 1 - ssr / sst : 0

        upper1 = midline + 1.0 * stddev
        lower1 = midline - 1.0 * stddev
        upper2 = midline + deviations * stddev
        lower2 = midline - deviations * stddev

        emit (midline, upper1, lower1, upper2, lower2, slope, r_squared)
```

### Statistical Zone Interpretation

| Zone | Probability | Interpretation |
|------|------------|----------------|
| Above $+2\sigma$ | ~2.5% | Extremely overbought relative to trend |
| $+1\sigma$ to $+2\sigma$ | ~13.5% | Overbought |
| $-1\sigma$ to $+1\sigma$ | ~68% | Normal oscillation around trend |
| $-2\sigma$ to $-1\sigma$ | ~13.5% | Oversold |
| Below $-2\sigma$ | ~2.5% | Extremely oversold relative to trend |

## Resources

- Carter, J. (2005). *Mastering the Trade*. McGraw-Hill.
- Raff, G. (1991). "Trading the Regression Channel." *Technical Analysis of Stocks & Commodities*.
- Draper, N. & Smith, H. (1998). *Applied Regression Analysis*. Wiley.
