# ZSCORE: Z-Score (Population Standard Score)

> "How far from normal is this?" — Every risk manager, every day.

## Introduction

The Z-Score measures how many population standard deviations a value lies from the rolling mean over a lookback window. Unlike the related Standardize indicator (which uses sample standard deviation with Bessel's correction), ZSCORE uses population standard deviation, matching the PineScript `ta.zscore` convention. Output is unbounded, typically ranging from -3 to +3 for normally distributed data. A z-score of 0 means the value equals the window mean; ±2 flags statistical outliers at the 95% level.

## Historical Context

The z-score originates from Karl Pearson's work in the 1890s on the theory of statistics. It transforms any distribution into units of standard deviation, making cross-series comparison possible. In trading, z-scores power mean-reversion strategies (enter when |z| > 2, exit when |z| < 0.5), pairs trading (z-score of spread), and anomaly detection. The population variant (N denominator) is standard in PineScript and most trading platforms because the rolling window IS the population of interest — not a sample from a larger population.

## Architecture and Physics

### 1. Core Formula

$$z = \frac{x - \mu}{\sigma}$$

where:

- $\mu = \frac{1}{N} \sum_{i=1}^{N} x_i$ (population mean over window)
- $\sigma = \sqrt{\frac{1}{N} \sum_{i=1}^{N} (x_i - \mu)^2}$ (population standard deviation)

### 2. Computational Form

Using the identity $\text{Var}(X) = E[X^2] - (E[X])^2$:

$$\sigma = \sqrt{\frac{\sum x_i^2}{N} - \left(\frac{\sum x_i}{N}\right)^2}$$

This avoids a two-pass algorithm. One pass computes both $\sum x_i$ and $\sum x_i^2$.

### 3. Edge Cases

| Condition | Result |
|-----------|--------|
| $N < 2$ | 0.0 |
| $\sigma < 10^{-10}$ | 0.0 (constant data) |
| Input is NaN/Infinity | Substitute last valid value |
| Negative variance (floating-point) | Clamp to 0.0 |

### 4. Population vs Sample

| Variant | Denominator | Use Case |
|---------|-------------|----------|
| ZSCORE (this) | $N$ | Rolling window IS the population |
| Standardize | $N - 1$ | Window is sample from larger population |

Relationship: $z_{\text{pop}} = z_{\text{sample}} \cdot \sqrt{\frac{N}{N-1}}$

### 5. State Management

Uses `RingBuffer` for the sliding window. State rollback via `record struct State` with `_s`/`_ps` pattern for bar correction support.

## Mathematical Foundation

### Z-Score Derivation

Given a window of $N$ values $\{x_1, x_2, \ldots, x_N\}$:

$$\mu = \frac{1}{N} \sum_{i=1}^{N} x_i$$

$$\sigma^2 = \frac{1}{N} \sum_{i=1}^{N} (x_i - \mu)^2 = \frac{1}{N} \sum_{i=1}^{N} x_i^2 - \mu^2$$

$$z = \frac{x_N - \mu}{\sigma}$$

### Scale Invariance

For any linear transform $y = ax + b$ where $a > 0$:

$$z(y) = \frac{(ax + b) - (a\mu + b)}{a\sigma} = \frac{x - \mu}{\sigma} = z(x)$$

Z-scores are invariant under positive linear transformations. This property makes them ideal for comparing series measured in different units.

## Performance Profile

### Operation Count (per Update)

| Operation | Count |
|-----------|-------|
| Additions | $N$ (sum scan) |
| Multiplications | $N$ (sumSq scan) |
| Division | 3 |
| Square root | 1 |
| Comparison | 2 |

### Complexity

| Method | Time | Space |
|--------|------|-------|
| `Update` | $O(N)$ | $O(1)$ auxiliary |
| `Batch(Span)` | $O(N \cdot P)$ | stackalloc or ArrayPool |

### Quality Metrics

| Metric | Score |
|--------|-------|
| Accuracy | 9/10 |
| Numerical stability | 8/10 |
| Memory efficiency | 9/10 |
| SIMD potential | Limited (sequential dependency on current value) |

## Validation

| Library | Status | Notes |
|---------|--------|-------|
| Manual | Verified | Known-value tests match hand computation |
| Standardize | Cross-validated | $z_{\text{pop}} = z_{\text{sample}} \cdot \sqrt{N/(N-1)}$ holds |
| PineScript | Formula match | Population stddev, same edge-case handling |

## Common Pitfalls

1. **Population vs sample confusion.** ZSCORE uses N denominator. Standardize uses N-1. The difference matters for small windows: at period=5, the ratio is $\sqrt{5/4} = 1.118$, an 11.8% discrepancy.

2. **Assuming normality.** Z-scores measure distance in sigma units but don't guarantee the underlying distribution is normal. Fat-tailed financial returns make |z| > 3 more common than the 0.3% a normal distribution predicts.

3. **Constant data edge case.** When all values in the window are identical, $\sigma = 0$ and division is undefined. Implementation returns 0.0.

4. **Floating-point variance.** The formula $E[X^2] - (E[X])^2$ can produce tiny negative values due to floating-point arithmetic. Clamped to zero before taking square root.

5. **Warmup period.** Requires at least 2 data points for meaningful output. During warmup ($N < 2$), returns 0.0.

6. **NaN propagation.** Non-finite inputs are substituted with the last valid value to prevent NaN from contaminating the rolling statistics.

## References

- Pearson, K. (1894). "Contributions to the Mathematical Theory of Evolution." *Philosophical Transactions of the Royal Society.*
- TradingView PineScript Reference: [ta.zscore](https://www.tradingview.com/pine-script-reference/v6/)
- Bollinger, J. (2001). *Bollinger on Bollinger Bands.* McGraw-Hill. (Z-score normalization of Bollinger %B)
