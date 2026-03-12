# WMA: Weighted Moving Average

> *Because yesterday matters more than last Tuesday. WMA is the linear answer to the question: 'What have you done for me lately?'*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (FIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Wma)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [wma.pine](wma.pine)                       |
| **Signature**    | [wma_signature](wma_signature.md) |

- The Weighted Moving Average (WMA) assigns a linearly decreasing weight to data points.
- Parameterized by `period`.
- Output range: Tracks input.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

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

### Operation Count (Streaming Mode, Scalar)

The O(1) algorithm eliminates the $O(N)$ weighted sum on each bar:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ADD/SUB | 4 | 1 | 4 |
| MUL | 1 | 3 | 3 |
| DIV | 1 | 15 | 15 |
| **Total** | **6** | — | **~22 cycles** |

**Hot path breakdown:**
- `WSum_new = WSum_old - Sum_old + (N × Price_new)`: 2 SUB + 1 MUL
- `Sum_new = Sum_old - Price_oldest + Price_new`: 2 SUB
- `WMA = WSum / divisor`: 1 DIV (divisor is precomputed constant)

**Comparison with naive O(N) implementation:**

| Mode | Complexity | Cycles (Period=100) |
| :--- | :---: | :---: |
| Naive (recalculate) | O(N) | ~400 cycles |
| QuanTAlib O(1) | O(1) | ~22 cycles |
| **Improvement** | **—** | **~18× faster** |

### Batch Mode (SIMD/FMA)

WMA batch uses prefix sums for both `Sum` and `WSum`, enabling vectorization:

| Operation | Scalar Ops (512 bars) | SIMD Ops (AVX2) | Speedup |
| :--- | :---: | :---: | :---: |
| Prefix sum (Sum) | 512 | 64 | 8× |
| Weighted prefix sum | 512 | 64 | 8× |
| Final divisions | 512 | 64 | 8× |

The batch path achieves near-linear scaling for large datasets.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Matches TA-Lib, Skender, Tulip exactly |
| **Timeliness** | 6/10 | Linear weighting improves responsiveness over SMA |
| **Overshoot** | 10/10 | Never overshoots input data range (FIR property) |
| **Smoothness** | 4/10 | Less smooth than SMA; follows price closely |

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **TA-Lib** | ✅ | Matches `TA_WMA` exactly. |
| **Skender** | ✅ | Matches `GetWma` exactly. |
| **Tulip** | ✅ | Matches `wma` exactly. |
| **Ooples** | ✅ | Matches `CalculateWeightedMovingAverage`. |
