# HARMEAN: Harmonic Mean

> "The harmonic mean is never greater than the geometric mean, which is never greater than the arithmetic mean." - The Mean Inequality, a mathematical fact older than calculus

The Harmonic Mean computes the reciprocal of the arithmetic mean of reciprocals over a sliding window. It is the correct average for quantities defined in terms of rates or ratios (speed, P/E ratios, yield). For financial time series, the harmonic mean gives the largest discount to outliers, making it the most conservative of the three Pythagorean means.

## Historical Context

The harmonic mean appears in Archimedes' work on means (ca. 225 BCE) and was one of the three "Pythagorean means" studied by ancient Greek mathematicians alongside the arithmetic and geometric means. The name "harmonic" comes from its connection to musical intervals: the harmonic mean of two string lengths produces a note that is harmonically related to both. In modern finance, the harmonic mean surfaces when averaging price-to-earnings ratios across a portfolio (where arithmetic averaging systematically overstates the aggregate P/E), when computing average cost basis for dollar-cost averaging, and when combining rates that apply to equal fixed quantities.

Consider dollar-cost averaging: investing $1000/month into a stock at prices $50, $100, and $200 buys 20, 10, and 5 shares respectively. The average cost per share is $3000/35 = $85.71, which is the harmonic mean of {50, 100, 200}. The arithmetic mean ($116.67) would overstate your cost basis by 36%.

## Architecture and Physics

`Harmean` extends `AbstractBase` for single-value input streaming. Instead of recomputing reciprocals across the entire window each tick, it maintains a running sum of reciprocals using Kahan-Babuska compensated summation.

### Design Decisions

1. **Reciprocal-sum approach**: Maintains $\sum 1/x_i$ as a running accumulator. The harmonic mean is simply $n / \sum(1/x_i)$. This enables O(1) updates: add $1/x_{\text{new}}$, subtract $1/x_{\text{old}}$.

2. **O(1) streaming updates**: Uses a `RingBuffer` to track which raw values are in the window. When computing the reciprocal of an exiting value, it reads from the buffer rather than storing reciprocals separately. This avoids double-inversion numerical error.

3. **Periodic resync**: Every 1000 ticks, the running reciprocal sum is recomputed from scratch to bound floating-point drift. Sequential add/subtract cycles accumulate error proportional to the number of updates without this safeguard.

4. **Non-positive value handling**: Values $\leq 0$ produce undefined or negative reciprocals that break the mean. The indicator substitutes the last valid positive value, matching the PineScript reference behavior.

5. **No SIMD in Update**: The streaming path is inherently sequential (running compensated sum with state). The static `Batch(Span)` method uses a scalar circular buffer for maximum throughput.

## Mathematical Foundation

For $n$ positive values $x_1, x_2, \ldots, x_n$, the harmonic mean is:

$$ H = \frac{n}{\sum_{i=1}^{n} \frac{1}{x_i}} $$

Equivalently:

$$ \frac{1}{H} = \frac{1}{n} \sum_{i=1}^{n} \frac{1}{x_i} $$

The harmonic mean is the reciprocal of the arithmetic mean of reciprocals.

### Mean Inequality (HM-GM-AM)

For positive real numbers, the three Pythagorean means satisfy:

$$ H \leq G \leq A $$

where $H$ is the harmonic mean, $G$ is the geometric mean, and $A$ is the arithmetic mean. Equality holds if and only if all values are identical. This property is validated in the test suite.

### Kahan-Babuska Compensation

The running reciprocal-sum uses second-order compensation:

$$
\begin{aligned}
y &= \frac{1}{x_{\text{new}}} - c \\
t &= S + y \\
c &= (t - S) - y \\
S &= t
\end{aligned}
$$

This bounds the accumulated error to $O(\varepsilon)$ rather than $O(n\varepsilon)$ for naive summation, where $\varepsilon$ is machine epsilon.

## Performance Profile

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | ~5ns/bar | O(1) reciprocal-add/subtract per update. |
| **Allocations** | 0 | RingBuffer pre-allocated; no heap allocation in Update. |
| **Complexity** | O(1) streaming | Amortized O(1) with periodic O(period) resync every 1000 ticks. |
| **Accuracy** | 9/10 | Kahan-Babuska compensation + periodic resync. |

## Validation

Self-validated against mathematical properties and Wolfram Alpha known values.

| Property | Status | Notes |
| :--- | :--- | :--- |
| **Known values** | ✅ | harmean({2, 8}) = 16/5 = 3.2; harmean({2, 4, 8}) = 24/7 ≈ 3.4286. |
| **Constant series** | ✅ | Returns the constant value exactly. |
| **HM ≤ GM ≤ AM** | ✅ | Validated across 500 GBM bars, period 20. |
| **Batch == Streaming** | ✅ | All modes produce identical results within 1e-8. |
| **Near-constant** | ✅ | Very low variance input converges to the value. |

## Common Pitfalls

1. **Non-positive inputs**: The harmonic mean is undefined for zero or negative values. The indicator substitutes the last valid positive value, but feeding predominantly non-positive data produces meaningless results.

2. **Extreme outliers**: The harmonic mean is heavily influenced by small values (since their reciprocals are large). A single near-zero value can drag the harmonic mean close to zero even if all other values are large.

3. **Sparse data**: With fewer values than the period, the harmonic mean uses whatever count is available. It becomes "hot" only when the full window is populated.

4. **Floating-point drift**: Without periodic resync, long-running streams accumulate reciprocal-sum errors. The 1000-tick resync interval bounds this drift.

5. **Comparison with arithmetic mean**: The harmonic mean is always less than or equal to the arithmetic mean. When they diverge significantly, the data has high variance in its reciprocals. This divergence itself can be a useful volatility signal.

## References

- Bullen, P.S. "Handbook of Means and Their Inequalities." Kluwer Academic Publishers, 2003.
- Ferger, W.F. "The Nature and Use of the Harmonic Mean." Journal of the American Statistical Association, 1931.
- PineScript reference: `lib/statistics/harmean/harmean.pine`
