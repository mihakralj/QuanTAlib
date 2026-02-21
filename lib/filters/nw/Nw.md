# NW: Nadaraya-Watson Kernel Regression

> "Nadaraya and Watson independently discovered the same thing in 1964: weight each observation by how close it is, normalize, and average. Fifty years later, it became one of the most popular nonparametric smoothers on TradingView. The math did not change; only our ability to compute it in real time."

NW computes the Nadaraya-Watson kernel regression estimator with a Gaussian kernel, producing a nonparametric smooth of the price series. For each bar, every observation in the lookback window is weighted by a Gaussian function of its temporal distance, with the bandwidth parameter $h$ controlling the effective smoothing radius. Small $h$ tracks price tightly (low bias, high variance); large $h$ smooths heavily (high bias, low variance). This implementation is non-repainting (backward-looking only).

## Historical Context

Elizbar Nadaraya (1964) and Geoffrey Watson (1964) independently published the same kernel regression estimator, now universally known as the Nadaraya-Watson estimator. It is the foundational method of nonparametric regression: given paired observations $(x_i, y_i)$, estimate $\hat{y}(x) = \sum w_i y_i / \sum w_i$ where $w_i = K((x - x_i)/h)$ and $K$ is a kernel function.

In the time-series context, the $x$-values are bar indices and the kernel reduces to a temporal weighting function. The Gaussian kernel $K(u) = e^{-u^2/2}$ produces smooth, infinitely differentiable output and has the theoretical property of minimizing the asymptotic mean integrated squared error (MISE) under certain regularity conditions.

The NW estimator became popular on TradingView around 2022, when several prominent indicator authors (including LuxAlgo) published implementations. Most TradingView versions use centered or forward-looking kernels that repaint as new bars arrive. This implementation is strictly backward-looking (endpoint mode), meaning the estimate at bar $t$ uses only bars $t, t-1, \ldots, t-N+1$. The nonrepainting property is essential for backtesting validity.

The bandwidth $h$ plays the role that "period" plays in traditional MAs, but with different semantics: $h$ controls the width of the Gaussian bell, and bars beyond $\sim 3h$ contribute negligible weight regardless of the lookback window size.

## Architecture & Physics

### 1. Gaussian Kernel Weights

For each bar $i$ in the lookback window (where $i = 0$ is newest):

$$
w_i = \exp\!\left(-\frac{i^2}{2h^2}\right)
$$

### 2. Normalized Weighted Average

$$
\text{NW}_t = \frac{\sum_{i=0}^{N-1} w_i \cdot x_{t-i}}{\sum_{i=0}^{N-1} w_i}
$$

### 3. Bandwidth-Period Relationship

Observations beyond $3h$ bars of lag contribute $< 1.1\%$ of peak weight. Setting $N \geq 4h$ captures $> 99.97\%$ of the kernel mass.

## Mathematical Foundation

The Nadaraya-Watson estimator for time series:

$$
\hat{m}(t) = \frac{\sum_{i=0}^{N-1} K_h(i) \cdot x_{t-i}}{\sum_{i=0}^{N-1} K_h(i)}
$$

with Gaussian kernel:

$$
K_h(u) = \frac{1}{\sqrt{2\pi}h}\exp\!\left(-\frac{u^2}{2h^2}\right)
$$

(The normalizing constant $1/(\sqrt{2\pi}h)$ cancels in the ratio and is omitted in practice.)

**Bias-variance trade-off:**

| $h$ (relative to $N$) | Bias | Variance | Behavior |
| :---: | :---: | :---: | :--- |
| $h \ll N$ | Low | High | Tracks noise, overfits |
| $h \approx N/4$ | Balanced | Balanced | Good default |
| $h \gg N$ | High | Low | Over-smooths, flat |

**Effective number of observations:** The kernel entropy $N_{\text{eff}} = (\sum w_i)^2 / \sum w_i^2 \approx \sqrt{2\pi} \cdot h$ for the Gaussian.

**Group delay:** Approximately $h \cdot \sqrt{\pi/2} \approx 1.25h$ for the Gaussian kernel.

**Default parameters:** `period = 64`, `bandwidth = 8.0`, `minPeriod = 1`.

**Pseudo-code (streaming):**

```
h2x2 = 2 * bandwidth²
num = 0; den = 0

for i = 0 to min(bar_count, period) - 1:
    w = exp(-i² / h2x2)
    num += w * source[i]
    den += w

return den > 0 ? num/den : source
```

## Resources

- Nadaraya, E.A. (1964). "On Estimating Regression." *Theory of Probability and Its Applications*, 9(1), 141-142.
- Watson, G.S. (1964). "Smooth Regression Analysis." *Sankhyā: The Indian Journal of Statistics*, Series A, 26(4), 359-372.
- Wand, M.P. & Jones, M.C. (1995). *Kernel Smoothing*. Chapman & Hall/CRC. Chapter 2: The Density Estimator.
