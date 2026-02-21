# CTI: Correlation Trend Indicator

The Correlation Trend Indicator computes the Pearson correlation coefficient between the price series and a linear time index over a rolling window, producing a bounded oscillator in the range $[-1, +1]$. Values near $+1$ indicate a strong linear uptrend, values near $-1$ indicate a strong linear downtrend, and values near zero indicate no linear trend relationship. The implementation achieves O(1) complexity per bar through incremental running sums that avoid recomputing the full correlation on each update.

## Historical Context

The concept of measuring trend strength via linear correlation has roots in classical statistics, where Pearson's $r$ between an ordinal time index and a dependent variable quantifies how well a linear model fits the observed data. John Ehlers popularized this approach in trading contexts, noting that correlation-based trend detection is mathematically equivalent to the R-squared goodness-of-fit measure used in linear regression. CTI differs from slope-based indicators (like TSF or LSMA) by normalizing the result to a fixed $[-1, +1]$ range regardless of price scale or volatility, making it directly comparable across instruments and timeframes. This normalization property makes CTI particularly useful as a regime filter: values above a threshold (typically $\pm 0.5$) indicate trending conditions where trend-following strategies perform well, while values near zero suggest mean-reverting or choppy conditions.

## Architecture & Physics

### Incremental Pearson Correlation

The standard Pearson correlation formula requires $\Sigma x$, $\Sigma y$, $\Sigma x^2$, $\Sigma y^2$, and $\Sigma xy$ over $n$ observations. For CTI, the $x$ values are sequential integers (time indices), which means $\Sigma x$ and $\Sigma x^2$ are deterministic closed-form functions of $n$ and do not require running sums. Only the $y$-dependent sums ($\Sigma y$, $\Sigma y^2$, $\Sigma xy$) need incremental maintenance.

### Running Sum Trick for $\Sigma xy$

The key optimization is the incremental update of $\Sigma xy$. When the window slides forward by one bar:
- The oldest value exits at what was position 0 and all remaining values shift down by one position.
- Rather than recomputing all $x_i \cdot y_i$ products, the implementation subtracts $\Sigma y$ (which shifts all position indices down by 1) and adds $(n-1) \times y_{\text{new}}$ for the new value entering at the highest position.

This reduces the $O(n)$ recomputation to $O(1)$ per bar.

### Clamping and Edge Cases

The output is clamped to $[-1, +1]$ to guard against floating-point drift. When the count is less than 2, the output is `NaN` (insufficient data). When either variance term is non-positive (constant price or constant time, which cannot happen for time), the output is 0.

## Mathematical Foundation

Given source values $y_t$ over a window of $n$ observations with time indices $x_i = 0, 1, \ldots, n-1$:

**Closed-form sums for time indices:**

$$\Sigma_x = \frac{n(n-1)}{2}, \quad \Sigma_{x^2} = \frac{n(n-1)(2n-1)}{6}$$

**Running sums for price:**

$$\Sigma_y = \sum_{i=0}^{n-1} y_i, \quad \Sigma_{y^2} = \sum_{i=0}^{n-1} y_i^2, \quad \Sigma_{xy} = \sum_{i=0}^{n-1} i \cdot y_i$$

**Pearson correlation:**

$$r = \frac{n \cdot \Sigma_{xy} - \Sigma_x \cdot \Sigma_y}{\sqrt{(n \cdot \Sigma_{x^2} - \Sigma_x^2)(n \cdot \Sigma_{y^2} - \Sigma_y^2)}}$$

**O(1) incremental update** (when buffer is full, oldest value $y_{\text{old}}$ exits):

```text
Σy  -= y_old;  Σy  += y_new
Σy² -= y_old²; Σy² += y_new²
Σxy -= Σy_before_removal    // shift all positions down by 1
Σxy += (n-1) × y_new        // new value enters at position n-1

CTI = clamp(r, -1, +1)
```

**Default parameters:** period = 20.

## Resources

- Ehlers, J.F. (2001). *Rocket Science for Traders*. Wiley
- Pearson, K. (1895). "Notes on Regression and Inheritance in the Case of Two Parents." *Proceedings of the Royal Society of London*
- PineScript reference: [`cti.pine`](cti.pine)
