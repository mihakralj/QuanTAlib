# WMA: Weighted Moving Average

> "Because yesterday matters more than last Tuesday. WMA is the linear answer to the question: 'What have you done for me lately?'"

The Weighted Moving Average (WMA) assigns a linearly decreasing weight to data points. The most recent price gets weight $N$, the one before it $N-1$, down to 1. This makes it more responsive to recent price changes than an SMA, but without the infinite tail of an EMA.

## Historical Context

WMA is the "finite impulse response" (FIR) counterpart to the EMA. It was developed to reduce the lag of the SMA while maintaining a finite window of influence.

## Architecture & Physics

A naive WMA implementation is $O(N)$, requiring a full loop over the history window for every update. QuanTAlib uses a dual running-sum algorithm to achieve $O(1)$ complexity.

### The O(1) Algorithm

Two sums are maintained:

1. `Sum`: The simple sum of values (like SMA).
2. `WSum`: The weighted sum.

$$ WSum_{new} = WSum_{old} - Sum_{old} + (N \times Price_{new}) $$
$$ Sum_{new} = Sum_{old} - Price_{oldest} + Price_{new} $$

This allows calculating a WMA(1000) as fast as a WMA(10).

### SIMD Optimization

For batch processing, `Wma.Batch` uses advanced vectorization (AVX2/AVX-512/Neon). It computes prefix sums and weighted updates in parallel, achieving throughputs that scalar code cannot touch.

## Mathematical Foundation

### 1. The Formula

$$ WMA = \frac{\sum_{i=0}^{N-1} (N-i) \times P_{t-i}}{\frac{N(N+1)}{2}} $$

The denominator is the sum of the weights (triangular number).

## Performance Profile

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | 10 | High; O(1) calculation via dual running sums. |
| **Allocations** | 0 | Zero-allocation in hot paths. |
| **Complexity** | O(1) | Constant time regardless of period. |
| **Accuracy** | 10 | Matches TA-Lib exactly. |
| **Timeliness** | 6 | More responsive than SMA due to linear weighting. |
| **Overshoot** | 0 | Never overshoots the input data range. |
| **Smoothness** | 4 | Less smooth than SMA; follows price more closely. |

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **TA-Lib** | ✅ | Matches `TA_WMA` exactly. |
| **Skender** | ✅ | Matches `GetWma` exactly. |
| **Tulip** | ✅ | Matches `wma` exactly. |
| **Ooples** | ✅ | Matches `CalculateWeightedMovingAverage`. |

### Common Pitfalls

1. **Drift**: Like SMA, the O(1) algorithm is susceptible to floating-point drift. QuanTAlib resets the sums every 10,000 ticks to guarantee accuracy.
2. **Aggressiveness**: WMA reacts faster than SMA but can be "twitchy." It is often used as a component in other indicators (e.g., HMA) rather than a standalone trend filter.
3. **Weights**: Users sometimes confuse WMA (linear weights) with EMA (exponential weights) or VWAP (volume weights).
