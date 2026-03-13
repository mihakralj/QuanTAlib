# KURTOSIS: Excess Kurtosis

> *Normal is getting dressed in clothes that you buy for work and driving through traffic in a car that you are still paying for, in order to get to the job you need to pay for the clothes and the car.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Statistic                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`, `isPopulation` (default false)                      |
| **Outputs**      | Single series (Kurtosis)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [kurtosis.pine](kurtosis.pine)                       |

- Kurtosis measures the **tailedness** of a probability distribution.
- **Similar:** [Skew](../skew/Skew.md), [JB](../jb/Jb.md) | **Trading note:** Excess kurtosis; >0 = fat tails (leptokurtic), <0 = thin tails. Measures tail risk in returns.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

## Introduction

Kurtosis measures the **tailedness** of a probability distribution. Specifically, this implementation calculates *excess kurtosis*, which subtracts 3 from the raw kurtosis so that a normal distribution has excess kurtosis of zero. A positive value (leptokurtic) indicates fatter tails than normal, meaning more frequent extreme events. A negative value (platykurtic) indicates thinner tails, fewer surprises. Financial returns consistently exhibit positive excess kurtosis, which is why "once in a century" events happen every decade.

## Historical Context

Karl Pearson introduced kurtosis in 1905 as one of his system of statistical moments for classifying probability distributions. The term derives from the Greek *kyrtos* (curved). Fisher and Cornish later refined the sample correction formula, and the "excess" convention (subtracting 3) became standard to reference the normal distribution as baseline.

The financial community adopted kurtosis after Mandelbrot's 1963 observation that cotton prices exhibited "fat tails" inconsistent with Gaussian models. Every subsequent market crash reinforced the point. Risk management frameworks (VaR, CVaR, stress testing) now routinely incorporate kurtosis as a measure of tail risk that variance alone ignores.

## Architecture

### Computation Pipeline

1. **Running sums**: Maintain four accumulators: $\sum x$, $\sum x^2$, $\sum x^3$, $\sum x^4$
2. **Sliding window**: RingBuffer manages the lookback period; old values are subtracted from sums
3. **Central moments**: Derived from raw moments using the expansion formulas
4. **Excess kurtosis**: $g_2 = m_4/m_2^2 - 3$
5. **Fisher correction**: Applied for sample kurtosis to remove bias

### State Management

- `_sum`, `_sumSq`, `_sumCu`, `_sumQu`: Running power sums
- Bar correction via snapshot/restore pattern (`isNew` flag)
- Periodic resync every 1000 updates to limit floating-point drift

## Mathematical Foundation

### Central Moments from Raw Moments

Given running sums $S_1 = \sum x_i$, $S_2 = \sum x_i^2$, $S_3 = \sum x_i^3$, $S_4 = \sum x_i^4$:

$$\mu = \frac{S_1}{n}$$

$$m_2 = \frac{S_2}{n} - \mu^2$$

The fourth central moment expands as:

$$(x - \mu)^4 = x^4 - 4x^3\mu + 6x^2\mu^2 - 4x\mu^3 + \mu^4$$

Summing and dividing by $n$:

$$m_4 = \frac{S_4}{n} - 4\mu \cdot \frac{S_3}{n} + 6\mu^2 \cdot \frac{S_2}{n} - 3\mu^4$$

### Population Excess Kurtosis

$$g_2 = \frac{m_4}{m_2^2} - 3$$

### Sample Excess Kurtosis (Fisher's Correction)

$$G_2 = \frac{(n-1)}{(n-2)(n-3)} \left[ (n+1) \cdot g_2 + 6 \right]$$

### Reference Values

| Distribution | Excess Kurtosis |
|---|---|
| Normal | 0 |
| Uniform | -1.2 |
| Laplace | 3 |
| Student's t(5) | 6 |
| Exponential | 6 |

## Performance Profile

### Operation Count (Streaming Mode)

Kurtosis uses running sums of powers 1–4 over the sliding window for O(1) update (excess kurtosis formula).

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Ring buffer add/evict | 1 | 3 cy | ~3 cy |
| Update 4 power sums (x, x^2, x^3, x^4) | 4 | 3 cy | ~12 cy |
| Compute excess kurtosis formula | 1 | 8 cy | ~8 cy |
| NaN guard + N >= 4 guard | 1 | 2 cy | ~2 cy |
| **Total** | **O(1)** | — | **~25 cy** |

O(1) per update using 4th-moment running sums. Numerically sensitive — periodic resync every 1000+ bars prevents power-sum drift.

| Operation | Complexity | Notes |
|---|---|---|
| `Update(TValue)` | O(1) | Running sums, no iteration |
| `Batch(Span)` scalar | O(n) | Single pass with resync |
| `Batch(Span)` AVX2 | O(n/4) | 4-wide prefix sum on 4 accumulators |
| Memory | O(period) | Single RingBuffer |
| Allocations | 0 | Hot path is allocation-free |

### Quality Metrics

| Metric | Score (1-10) |
|---|---|
| Lag | 10 (lookback-dependent, not recursive) |
| Noise sensitivity | 4 (4th power amplifies outliers) |
| Computational cost | 7 (four running sums) |
| Numerical stability | 8 (resync every 1000 ticks) |
| SIMD amenability | 9 (prefix sum pattern, no data-dependent branching) |

## Validation

| Library | Method | Tolerance | Status |
|---|---|---|---|
| MathNet.Numerics | `Statistics.Kurtosis()` | 1e-6 | Validated (sample) |
| MathNet.Numerics | `Statistics.PopulationKurtosis()` | 1e-6 | Validated (population) |
| PineScript | `kurtosis.pine` | Manual | Matches (population excess) |

## Common Pitfalls

1. **Confusing kurtosis types**: Raw kurtosis vs excess kurtosis vs sample-corrected. This implementation returns *excess* kurtosis (normal = 0). MathNet and most statistical packages also use excess kurtosis.

2. **Period too small**: Kurtosis requires at least 4 data points. With small windows, the estimate is extremely noisy. Periods below 20 produce unreliable estimates.

3. **Outlier sensitivity**: The fourth power amplifies outliers dramatically. A single extreme value in the window can dominate the result. Consider robust alternatives (L-kurtosis) for contaminated data.

4. **Sample vs population**: Fisher's correction matters for small samples. For $n < 30$, the difference between sample and population excess kurtosis exceeds 1.0.

5. **Floating-point drift**: Four running sums of increasing power accumulate error. The resync mechanism (every 1000 ticks) recalculates from the buffer to bound drift.

6. **Interpretation trap**: High kurtosis does not mean "peaked." It means "fat tails." A distribution can be flat-topped and still leptokurtic.

7. **Non-stationarity**: Kurtosis assumes a stationary window. In trending markets, the sliding window conflates trend with tail behavior.

## References

- Pearson, K. (1905). "Das Fehlergesetz und seine Verallgemeinerungen durch Fechner und Pearson." *Biometrika*, 4(1-2), 169-212.
- Mandelbrot, B. (1963). "The Variation of Certain Speculative Prices." *The Journal of Business*, 36(4), 394-419.
- Joanes, D. N., & Gill, C. A. (1998). "Comparing measures of sample skewness and kurtosis." *Journal of the Royal Statistical Society: Series D*, 47(1), 183-189.
- DeCarlo, L. T. (1997). "On the meaning and use of kurtosis." *Psychological Methods*, 2(3), 292-307.