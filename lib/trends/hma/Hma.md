# HMA: Hull Moving Average

> "Alan Hull looked at the lag in moving averages and said, 'I can fix that.' And he did, by making the math do gymnastics."

HMA (Hull Moving Average) is a solution to the eternal struggle between smoothness and lag. Most indicators force you to choose one; HMA gives you both. It achieves this by using weighted moving averages (WMAs) in a clever configuration that cancels out lag while maintaining the smoothing properties of the WMA.

## Historical Context

Developed by Alan Hull in 2005, the HMA was designed to be "responsive, accurate, and smooth." Hull realized that lag is essentially a function of the period, and by combining averages of different periods (specifically, a full period and a half period), he could mathematically offset the lag.

## Architecture & Physics

The HMA is built from three Weighted Moving Averages (WMAs):

1. **WMA(n/2)**: A fast WMA of half the period.
2. **WMA(n)**: A slow WMA of the full period.
3. **WMA(sqrt(n))**: A smoothing WMA applied to the difference.

The core logic is: $2 \times \text{WMA}(n/2) - \text{WMA}(n)$.
This operation "over-weights" the recent data, pushing the average forward to align with the current price. The final WMA smooths out the resulting noise.

### Zero-Allocation Design

Our implementation is a composite of three `Wma` instances.

- **Composite Structure**: We manage three internal `Wma` objects.
- **SIMD Acceleration**: The intermediate calculation ($2 \times A - B$) is vectorized using AVX2/AVX-512 where available.
- **Memory Efficiency**: We reuse buffers where possible to minimize footprint.

## Mathematical Foundation

$$ \text{Raw} = 2 \times \text{WMA}(P, \frac{N}{2}) - \text{WMA}(P, N) $$

$$ \text{HMA} = \text{WMA}(\text{Raw}, \sqrt{N}) $$

Where $N$ is the period.

## Performance Profile

HMA is computationally more intensive than a simple WMA due to the three passes, but our implementation optimizes the intermediate step.

| Metric | Complexity | Notes |
| :--- | :--- | :--- |
| **Throughput** | High | 3x WMA cost + vector math |
| **Complexity** | O(1) | Constant time update |
| **Accuracy** | 8/10 | Excellent at tracking price action |
| **Timeliness** | 9/10 | Very responsive, minimal lag |
| **Overshoot** | 5/10 | Prone to overshoot due to lag correction |
| **Smoothness** | 8/10 | Surprisingly smooth given its speed |

## Validation

Validated against Alan Hull's original formula and standard library implementations.

| Provider | Error Tolerance | Notes |
| :--- | :--- | :--- |
| **Fidelity** | $10^{-9}$ | Matches standard HMA |
| **Skender** | $10^{-9}$ | Matches `GetHma` |

### Common Pitfalls

1. **Overshoot**: Like DEMA, HMA can overshoot price turns because of the lag correction.
2. **Period Sensitivity**: The $\sqrt{N}$ smoothing is hardcoded into the definition. You can't easily tweak the smoothing independently of the lag correction without breaking the "Hull" definition.
3. **Integer Math**: The periods $N/2$ and $\sqrt{N}$ are rounded to integers. This can cause slight discrepancies between implementations depending on rounding rules. We use standard integer truncation.
