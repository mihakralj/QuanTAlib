# CMA: Cumulative Moving Average

> "The running average that never forgets. Every single tick you've ever fed it? Still in there, affecting the result. It's like the elephant of technical indicators."

The Cumulative Moving Average (CMA) calculates the arithmetic mean of ALL data points seen so far, not just a fixed window. Unlike SMA or EMA which use a sliding window, CMA treats every historical value with equal weight. As the sample size grows, each new value has diminishing impact on the average.

## Historical Context

The concept of a running mean is fundamental to statistics and was formalized by B. P. Welford in 1962 for numerically stable computation. Donald Knuth popularized it in *The Art of Computer Programming*. While not a traditional trading indicator, CMA is essential for scenarios requiring the true average of all observed data: calculating session VWAP from scratch, averaging tick counts, or computing lifetime average fill prices.

## Architecture & Physics

The naive approach (sum all values, divide by count) works for small datasets but fails at scale. After millions of ticks, the running sum can overflow or lose precision.

### Welford's Algorithm with FMA

QuanTAlib uses Welford's numerically stable update, enhanced with Fused Multiply-Add (FMA) for maximum precision:

$$ M_n = M_{n-1} + \alpha \cdot (x_n - M_{n-1}) \quad \text{where } \alpha = \frac{1}{n} $$

Implemented as:

```csharp
double alpha = 1.0 / n;
double delta = x - mean;
mean = Math.FusedMultiplyAdd(alpha, delta, mean);
```

This formulation:

1. Keeps intermediate values near the scale of the actual mean (no overflow)
2. Requires only O(1) memory (just count and mean)
3. Achieves O(1) time complexity per update
4. Uses FMA for single-rounding precision (avoids rounding `alpha * delta` before adding to `mean`)
5. Is mathematically equivalent to $M_n = \frac{(n-1) \cdot M_{n-1} + x_n}{n}$

### Why Not Just Sum?

Consider averaging 10 million tick prices around 50,000 (a futures contract). The naive sum exceeds $5 \times 10^{11}$, approaching the precision limits of `double`. Welford's algorithm keeps the working value around 50,000 throughout, maintaining full precision.

### The Diminishing Return Problem

As $n$ grows large, each new value contributes only $\frac{1}{n}$ to the mean. After 1 million samples, a new tick moves the average by roughly 0.0001% of the difference from the current mean. This is mathematically correct but may not be what traders want for responsiveness (use EMA or SMA for that).

## Mathematical Foundation

### 1. Incremental Update (Welford)

$$ M_n = M_{n-1} + \frac{x_n - M_{n-1}}{n} $$

Where:

* $M_n$ = cumulative mean after $n$ values
* $M_{n-1}$ = previous cumulative mean
* $x_n$ = new value
* $n$ = total count of values

### 2. Algebraic Equivalence

$$ M_n = \frac{1}{n} \sum_{i=1}^{n} x_i = \frac{(n-1) \cdot M_{n-1} + x_n}{n} $$

## Performance Profile

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | ~5 ns/bar | Single division per update. |
| **Allocations** | 0 | Zero-allocation in hot paths. |
| **Complexity** | O(1) | Constant time regardless of history length. |
| **Accuracy** | 10 | Welford's algorithm ensures numerical stability. |
| **Timeliness** | 1 | Maximum lag; every historical value affects output. |
| **Overshoot** | 0 | Never overshoots the input data range. |
| **Smoothness** | 10 | Extremely smooth as $n$ grows (almost constant). |

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **TA-Lib** | N/A | No CMA function. |
| **Skender** | N/A | No CMA function. |
| **Tulip** | N/A | No CMA function. |
| **Mathematical** | ✅ | Validated against known formulas. |

CMA is a fundamental statistical operation rather than a standard TA library indicator. QuanTAlib validates against mathematical proofs: arithmetic progressions, geometric series, and direct sum/count calculations.

## Use Cases

1. **Session VWAP**: Calculate volume-weighted average price from session start
2. **Lifetime Averages**: Average fill price across all trades
3. **Quality Metrics**: Average latency, slippage, or fill rate over time
4. **Baseline Comparison**: Compare current price to "all-time average"

## Common Pitfalls

1. **Responsiveness**: CMA becomes nearly unresponsive after many values. For a reactive average, use SMA or EMA instead.
2. **Memory of Bad Data**: A single extreme outlier early in the stream permanently affects the average. Consider filtering before feeding CMA.
3. **No Period Parameter**: Unlike SMA/EMA, CMA has no period. It always includes all data. This is by design.
4. **Session Resets**: If you need per-session averages, call `Reset()` at session boundaries.