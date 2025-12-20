# SMA: Simple Moving Average

> "The vanilla ice cream of technical analysis. Boring, ubiquitous, and the only thing your grandfather and your high-frequency trading bot agree on."

The Simple Moving Average (SMA) is the unweighted arithmetic mean of the last $N$ data points. It acts as a low-pass filter, smoothing out high-frequency noise to reveal the underlying trend. While conceptually simple, efficient implementation on modern hardware requires careful attention to memory access patterns and vectorization.

## Historical Context

The concept of a moving average dates back to 1901 (R.H. Hooker) for smoothing weather data, but it became a staple of financial analysis in the mid-20th century. It is the baseline against which all other averages are compared.

## Architecture & Physics

The naive implementation of SMA sums $N$ numbers at every step, resulting in $O(N)$ complexity. QuanTAlib uses an optimized $O(1)$ approach.

### O(1) Running Sum

We maintain a running `Sum` and a `RingBuffer` of history.
$$ Sum_{new} = Sum_{old} - Value_{oldest} + Value_{new} $$
$$ SMA = \frac{Sum_{new}}{N} $$

This ensures that calculating an SMA(200) takes the exact same time as an SMA(10).

### Drift Correction

Floating-point addition is not associative. Repeatedly adding and subtracting values from a running sum introduces cumulative error (drift) over millions of ticks. QuanTAlib implements a periodic **Resync** mechanism (every 1000 ticks) that recalculates the sum from scratch to ensure precision remains within `1e-9` of the true mean.

### SIMD Optimization

For batch processing of large datasets, `Sma.Batch` utilizes `System.Runtime.Intrinsics` (AVX2/AVX-512) to process multiple data points in parallel, significantly outperforming scalar loops.

## Mathematical Foundation

### 1. The Mean

$$ SMA_t = \frac{1}{N} \sum_{i=0}^{N-1} P_{t-i} $$

## Performance Profile

The implementation is optimized for both streaming (latency) and batch (throughput) scenarios.

### Zero-Allocation Design

The `RingBuffer` is pre-allocated at initialization. All updates are performed in-place using scalar operations or SIMD intrinsics, ensuring no heap allocations occur during the hot path.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | High | Optimized running sum |
| **Complexity** | O(1) | Constant time update |
| **Accuracy** | 5/10 | Baseline accuracy, unweighted |
| **Timeliness** | 4/10 | Significant lag (N/2) |
| **Overshoot** | 8/10 | Generally stable, no projection |
| **Smoothness** | 6/10 | Susceptible to "drop-off" effect |

## Validation

Validated against TA-Lib (`TA_SMA`) and Skender.Stock.Indicators.

### Common Pitfalls

1. **Lag**: SMA has the most lag of all moving averages (Lag $\approx N/2$).
2. **Drop-off Effect**: An old, large outlier dropping out of the window causes the SMA to jump, even if the current price is flat. This "Barker effect" is why EMAs are often preferred.
3. **NaN Handling**: A single `NaN` in the history window corrupts the entire SMA. QuanTAlib handles this by substituting the last valid value.
