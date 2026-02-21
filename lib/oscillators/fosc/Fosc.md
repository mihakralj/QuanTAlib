# FOSC: Forecast Oscillator

The Forecast Oscillator measures the percentage deviation of the current price from its linear regression forecast value, producing a zero-centered oscillator that quantifies how far price has moved beyond what a least-squares trend projection would predict. Positive values indicate price is above the regression forecast (bullish divergence from trend), negative values indicate price is below (bearish divergence), and zero crossings mark the points where price meets its statistically expected value. The implementation achieves O(1) per-bar complexity through incremental running sums for the linear regression calculation.

## Historical Context

The Forecast Oscillator was introduced by Tushar Chande in his exploration of regression-based indicators during the 1990s, published in *Technical Analysis of Stocks & Commodities* magazine and later in his book on technical analysis. The indicator builds on the Time Series Forecast (TSF) concept but reframes it as an oscillator by expressing the relationship as a percentage deviation rather than an absolute price level. This normalization makes FOSC comparable across instruments of different price scales. Chande positioned FOSC as a complementary tool to his other regression-based indicators (R-Squared, Linear Regression Slope), where R-Squared measures trend quality, slope measures trend direction, and FOSC measures the current price's position relative to trend extrapolation. The percentage formulation also makes FOSC functionally similar to a detrended price series, connecting it to the broader family of detrending oscillators used in cycle analysis.

## Architecture & Physics

### Linear Regression via Running Sums

FOSC requires computing a linear regression forecast at each bar. The standard OLS formula needs $\Sigma x$, $\Sigma x^2$, $\Sigma y$, and $\Sigma xy$. Since the $x$ values are sequential integers, $\Sigma x$ and $\Sigma x^2$ are closed-form functions of $n$. Only $\Sigma y$ and $\Sigma xy$ require incremental maintenance via circular buffer.

The key optimization for $\Sigma xy$ is identical to CTI: when the window slides, subtracting $\Sigma y$ (before removal) shifts all position indices down by one, and adding $(n-1) \times y_{\text{new}}$ places the new value at the highest position. This avoids recomputing $n$ products per bar.

### Forecast Point

The linear regression yields slope $m$ and intercept $b$. The forecast value is evaluated at the endpoint of the window: $\hat{y} = m \cdot (n-1) + b$. This represents the trend-projected value for the current bar.

### Percentage Deviation

The oscillator output is $\frac{x_t - \hat{y}}{x_t} \times 100$, which normalizes the deviation by the current price. Division by zero is guarded when source equals zero.

## Mathematical Foundation

Given source values $y_t$ over a window of $n$ observations with indices $x_i = 0, 1, \ldots, n-1$:

**Closed-form time sums:**

$$\Sigma_x = \frac{n(n-1)}{2}, \quad \Sigma_{x^2} = \frac{n(n-1)(2n-1)}{6}$$

**Running sums (O(1) incremental):**

$$\Sigma_y = \sum y_i, \quad \Sigma_{xy} = \sum i \cdot y_i$$

**OLS linear regression:**

$$m = \frac{n \cdot \Sigma_{xy} - \Sigma_x \cdot \Sigma_y}{n \cdot \Sigma_{x^2} - \Sigma_x^2}, \quad b = \frac{\Sigma_y - m \cdot \Sigma_x}{n}$$

**Forecast at endpoint:**

$$\hat{y}_t = m \cdot (n - 1) + b$$

**Forecast Oscillator:**

$$FOSC_t = \frac{y_t - \hat{y}_t}{y_t} \times 100$$

**Streaming update pseudo-code:**

```text
// When buffer full, oldest y_old exits:
Σy  -= y_old;   Σxy -= Σy_before
Σy  += y_new;   Σxy += (n-1) × y_new

m = (n×Σxy - Σx×Σy) / (n×Σx² - Σx²)
b = (Σy - m×Σx) / n
forecast = m×(n-1) + b
FOSC = (y_new ≠ 0) ? (y_new - forecast) / y_new × 100 : 0
```

**Default parameters:** period = 14.

## Resources

- Chande, T.S. (1997). *Beyond Technical Analysis*. Wiley
- Chande, T.S. & Kroll, S. (1994). *The New Technical Trader*. Wiley
- PineScript reference: [`fosc.pine`](fosc.pine)
