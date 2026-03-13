# SMA: Simple Moving Average

> *The vanilla ice cream of technical analysis. Boring, ubiquitous, and the only thing your grandfather and your high-frequency trading bot agree on.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (FIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Sma)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [sma.pine](sma.pine)                       |
| **Signature**    | [sma_signature](sma_signature.md) |

- The Simple Moving Average (SMA) is the unweighted arithmetic mean of the last $N$ data points.
- **Similar:** [EMA](../../trends_IIR/ema/ema.md), [WMA](../wma/wma.md) | **Complementary:** ATR for Keltner-style bands | **Trading note:** Simple Moving Average; equal-weight FIR filter. Most basic and widely used MA. Foundation of many composite indicators.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The Simple Moving Average (SMA) is the unweighted arithmetic mean of the last $N$ data points. It acts as a low-pass filter, smoothing out high-frequency noise to reveal the underlying trend. While conceptually simple, efficient implementation on modern hardware requires careful attention to memory access patterns and vectorization.

## Historical Context

The concept of a moving average dates back to 1901 (R.H. Hooker) for smoothing weather data, but it became a staple of financial analysis in the mid-20th century. It is the baseline against which all other averages are compared.

## Architecture & Physics

The naive implementation of SMA sums $N$ numbers at every step, resulting in $O(N)$ complexity. QuanTAlib uses an optimized $O(1)$ approach.

### O(1) Running Sum

A running `Sum` and a `RingBuffer` of history are maintained.
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

### Operation Count (Streaming Mode, O(1) Running Sum)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| SUB (Sum - oldest) | 1 | 1 | 1 |
| ADD (Sum + newest) | 1 | 1 | 1 |
| DIV (Sum / N) | 1 | 15 | 15 |
| **Total (hot)** | **3** | — | **~17 cycles** |

Every 1000 bars, a resync recalculates the sum to prevent drift:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ADD (N values) | N | 1 | N |
| DIV (Sum / N) | 1 | 15 | 15 |
| **Resync cost** | **N+1** | — | **~N+15 cycles** |

**Amortized cost:** ~17 + (N+15)/1000 ≈ **~17 cycles/bar** for typical use.

### Batch Mode (SIMD Analysis)

SMA batch processing is highly vectorizable using running sum + prefix sum techniques:

| Operation | Scalar Ops | SIMD Ops (AVX2) | Speedup |
| :--- | :---: | :---: | :---: |
| Initial N-sum | N | N/8 | 8× |
| Running update (per bar) | 3 | ~1 | ~3× |
| Division | 1 | 1/8 (batched) | 8× |

For 512 bars:

| Mode | Cycles/bar | Total | Notes |
| :--- | :---: | :---: | :--- |
| Scalar streaming | ~17 | ~8,700 | O(1) per bar |
| SIMD batch | ~3 | ~1,500 | Vectorized running sum |
| **Improvement** | **5.8×** | — | Batch wins for large N |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact arithmetic mean |
| **Timeliness** | 3/10 | Significant lag (~N/2 bars) |
| **Overshoot** | 10/10 | Never overshoots input range |
| **Smoothness** | 5/10 | Smooth but susceptible to drop-off jumps |

### Benchmark Results

| Metric | Value | Notes |
| :--- | :--- | :--- |
| **Throughput** | ~100M bars/sec | SIMD batch mode |
| **Allocations** | 0 bytes | Zero-allocation in hot paths |
| **Complexity** | O(1) | Constant time regardless of period N |
| **State Size** | 8 + 8N bytes | Sum + RingBuffer |

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **TA-Lib** | ✅ | Matches `TA_SMA` exactly. |
| **Skender** | ✅ | Matches `GetSma` exactly. |
| **Tulip** | ✅ | Matches `sma` exactly. |
| **Ooples** | ✅ | Matches `CalculateSimpleMovingAverage`. |