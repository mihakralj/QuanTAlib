# HURST: Hurst Exponent

> "The past is not dead. In fact, it's not even past." — William Faulkner, and also every mean-reverting time series that refuses to forget.

## Introduction

The Hurst Exponent ($H$) quantifies long-range dependence in a time series through Rescaled Range (R/S) analysis. Where autocorrelation decays and dies, the Hurst exponent measures the memory that persists across scales. $H > 0.5$ signals persistence (trending behavior), $H < 0.5$ signals anti-persistence (mean-reversion), and $H = 0.5$ represents the memoryless random walk that efficient market theorists insist you should believe in.

This implementation uses the classical R/S method with ordinary least squares regression on log-log coordinates, matching the PineScript reference. Period must be at least 20 to provide meaningful sub-period decomposition.

## Historical Context

Harold Edwin Hurst spent decades measuring Nile River flood levels before publishing his seminal 1951 paper. He discovered that natural phenomena exhibit stronger clustering than Brownian motion predicts. Benoit Mandelbrot later formalized this as "fractional Brownian motion" and connected it to self-similar processes.

The R/S statistic was the original method. Later alternatives (DFA, wavelet-based estimators, variance-ratio) address known biases in R/S for short series, but R/S remains the most widely implemented in trading platforms due to its intuitive decomposition and computational transparency.

The key insight: for a fractal process, $E[R/S] \propto n^H$, so regressing $\ln(R/S)$ against $\ln(n)$ yields $H$ as the slope. A pure random walk gives $H = 0.5$ (the "expected" value under no memory), while real markets routinely produce values between 0.55 and 0.75 on daily data, suggesting persistent trends are not noise.

## Architecture and Physics

### 1. Log Return Computation

Raw prices are converted to log returns:

$$r_t = \ln\left(\frac{P_t}{P_{t-1}}\right)$$

Log returns are additive, stationary in mean, and scale-independent. The Hurst analysis operates on a sliding window of $L$ consecutive log returns.

### 2. Sub-Period Decomposition

For each sub-period size $n$ where $10 \leq n \leq \lfloor L/2 \rfloor$:

- Divide the $L$ log returns into $\lfloor L/n \rfloor$ non-overlapping blocks
- Each block contains exactly $n$ consecutive log returns

### 3. Rescaled Range for Each Block

For a block of $n$ log returns $\{r_1, r_2, \ldots, r_n\}$:

**Mean:**

$$\bar{r} = \frac{1}{n}\sum_{i=1}^{n} r_i$$

**Cumulative deviations:**

$$Y_k = \sum_{i=1}^{k}(r_i - \bar{r}), \quad k = 1, \ldots, n$$

**Range:**

$$R = \max(Y_1, \ldots, Y_n) - \min(Y_1, \ldots, Y_n)$$

**Standard deviation (population):**

$$S = \sqrt{\frac{1}{n}\sum_{i=1}^{n}(r_i - \bar{r})^2}$$

**Rescaled range:**

$$\frac{R}{S} = \frac{R}{S}, \quad S > 0$$

### 4. OLS Log-Log Regression

Average $R/S$ across all blocks for each $n$, then regress:

$$\ln\left(\overline{R/S}\right) = H \cdot \ln(n) + c$$

The slope $H$ is computed via ordinary least squares:

$$H = \frac{m\sum x_i y_i - \sum x_i \sum y_i}{m\sum x_i^2 - (\sum x_i)^2}$$

where $x_i = \ln(n_i)$, $y_i = \ln(\overline{R/S}_i)$, and $m$ is the number of valid $(n, R/S)$ pairs.

## Mathematical Foundation

### Hurst's Law

For a self-similar process with Hurst parameter $H$:

$$E\left[\frac{R(n)}{S(n)}\right] = c \cdot n^H$$

where $c$ is a constant depending on the distribution. Taking logarithms:

$$\ln E[R/S] = H \ln n + \ln c$$

### Interpretation

| Range | Interpretation | Market Behavior |
|-------|---------------|-----------------|
| $H = 0.5$ | Random walk | No memory, efficient market |
| $0.5 < H < 1.0$ | Persistent | Trends tend to continue |
| $0.0 < H < 0.5$ | Anti-persistent | Reversals more likely |
| $H = 1.0$ | Perfect persistence | Deterministic trend |
| $H = 0.0$ | Perfect anti-persistence | Deterministic oscillation |

### Parameter Mapping

| Parameter | Pine | C# | Default | Constraint |
|-----------|------|----|---------|------------|
| Lookback | `length` | `period` | 100 | $\geq 20$ |
| Min sub-period | `min_n` | `MinSubPeriod` | 10 | Fixed |
| Max sub-period | `max_n` | `period / 2` | 50 | Derived |

## Performance Profile

### Operation Count (Streaming Mode)

Hurst uses the Rescaled Range (R/S) statistic over the full lookback period — O(N) per bar.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Ring buffer add/evict | 1 | 3 cy | ~3 cy |
| Compute mean of N values | N | 2 cy | ~2N cy |
| Compute deviations + cumulative sum | N | 3 cy | ~3N cy |
| Range (max - min cumulative) | N | 2 cy | ~2N cy |
| Std deviation | N | 3 cy | ~3N cy |
| log(R/S) / log(N) | 2 | 8 cy | ~16 cy |
| **Total (N=100)** | **O(N)** | — | **~1016 cy** |

O(N) per update — expensive for large lookbacks. Practical throughput ~100 ns/bar at N=100. Fixed-period batch computation preferred for research workflows.

| Operation | Complexity | Notes |
|-----------|-----------|-------|
| Log return computation | $O(1)$ per bar | Single division + `Math.Log` |
| R/S per sub-period size $n$ | $O(L)$ | Iterates blocks of size $n$ |
| All sub-period sizes | $O(L \cdot (L/2 - 10))$ | $\approx O(L^2)$ |
| OLS regression | $O(m)$ | $m \leq L/2 - 9$ pairs |
| **Total per Update** | **$O(L^2)$** | Dominated by sub-period sweep |

### SIMD Analysis

The inner loops (mean, cumulative deviation, variance) operate on variable-length sub-periods making SIMD vectorization impractical for the streaming path. The Batch path uses `stackalloc` for small buffers and `ArrayPool` for large ones.

### Quality Metrics

| Metric | Score (1-10) | Notes |
|--------|-------------|-------|
| Accuracy | 7 | R/S has known bias for short series; adequate for $L \geq 100$ |
| Responsiveness | 5 | Inherently lagging (needs full window rebuild) |
| Smoothness | 6 | Stable once window fills; sensitive to outliers in small windows |
| Computational cost | 4 | $O(L^2)$ is expensive; consider reducing period for real-time |

## Validation

No external library provides a direct R/S-based Hurst exponent for cross-validation. Validation relies on known mathematical properties:

| Test | Expected | Tolerance |
|------|----------|-----------|
| Constant series | $H = 0.5$ (degenerate) | Exact |
| Random walk (GBM, $\mu = 0$) | $H \approx 0.5$ | $\pm 0.25$ |
| Deterministic (same input) | Identical output | $10^{-15}$ |
| Batch vs streaming | Identical | $10^{-12}$ |
| Span vs TSeries | Identical | $10^{-10}$ |

## Common Pitfalls

1. **Period too short**: With $L < 40$, the range of sub-period sizes ($10$ to $L/2$) is too narrow for reliable regression. Use $L \geq 100$ for production. Impact: $\pm 0.15$ bias.

2. **Confusing trending with persistence**: A series can have $H > 0.5$ even during drawdowns. The Hurst exponent measures autocorrelation structure, not direction. Misinterpretation leads to false directional signals.

3. **Non-stationarity**: The R/S method assumes locally stationary returns. Regime changes (volatility shifts, structural breaks) invalidate the power-law assumption. Impact: $H$ estimates become unreliable across breakpoints.

4. **R/S bias for finite samples**: Anis and Lloyd (1976) showed that $E[R/S]$ for i.i.d. normal data is not exactly $n^{0.5}$ but involves Gamma function corrections. For $n < 20$, this bias can shift $H$ by $0.05$-$0.15$.

5. **Zero-variance blocks**: If all returns in a sub-period are identical (e.g., during market halts), $S = 0$ and $R/S$ is undefined. These blocks are excluded from the average, reducing the effective sample size.

6. **Computational cost**: $O(L^2)$ per bar is expensive. For real-time streaming with $L = 500$, each update processes $\sim 125{,}000$ operations. Consider caching or reducing the update frequency. A period of 100 costs $\sim 4{,}000$ operations per update.

7. **Overfitting the exponent**: Point estimates of $H$ without confidence intervals invite overconfidence. The OLS slope has estimation error that grows as $m$ (number of log-log points) shrinks.

## References

- Hurst, H.E. (1951). "Long-term storage capacity of reservoirs." *Transactions of the American Society of Civil Engineers*, 116, 770-808.
- Mandelbrot, B.B. and Wallis, J.R. (1969). "Robustness of the rescaled range R/S in the measurement of noncyclic long run statistical dependence." *Water Resources Research*, 5(5), 967-988.
- Peters, E.E. (1994). *Fractal Market Analysis*. Wiley.
- Anis, A.A. and Lloyd, E.H. (1976). "The expected value of the adjusted rescaled Hurst range of independent normal summands." *Biometrika*, 63(1), 111-116.
- Lo, A.W. (1991). "Long-term memory in stock market prices." *Econometrica*, 59(5), 1279-1313.
