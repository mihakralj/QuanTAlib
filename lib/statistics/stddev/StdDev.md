# STDDEV: Standard Deviation

> *Volatility is not risk, but it's the only thing we can measure.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Statistic                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`, `isPopulation` (default false)                      |
| **Outputs**      | Single series (StdDev)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [stddev.pine](stddev.pine)                       |

- Standard Deviation measures the amount of variation or dispersion of a set of values.
- **Similar:** [Variance](../variance/Variance.md), [MeanDev](../meandev/MeanDev.md) | **Trading note:** Standard deviation; foundational volatility measure. Used in Bollinger Bands, VaR, and Sharpe ratio.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

Standard Deviation measures the amount of variation or dispersion of a set of values. A low standard deviation indicates that the values tend to be close to the mean (also called the expected value) of the set, while a high standard deviation indicates that the values are spread out over a wider range.

## Historical Context

The concept of standard deviation was introduced by Karl Pearson in 1893. It has since become the most common measure of statistical dispersion in finance, used to quantify volatility and risk.

## Architecture & Physics

`StdDev` is implemented as a wrapper around the highly optimized `Variance` indicator. It leverages the O(1) streaming updates and SIMD-accelerated batch processing of `Variance`, applying a square root transformation to the result.

### Zero-Allocation Design

The implementation ensures zero heap allocations during the `Update` cycle. The `Batch` method operates directly on `Span<double>` using SIMD instructions (AVX2, AVX512, Neon) where available, ensuring maximum throughput for large datasets.

## Mathematical Foundation

Standard Deviation is the square root of Variance.

$$ \sigma = \sqrt{\text{Variance}} $$

Where Variance is calculated as:

$$ \text{Variance} = \frac{\sum_{i=1}^{N} (x_i - \mu)^2}{N} $$

(For Population Standard Deviation)

Or:

$$ \text{Variance} = \frac{\sum_{i=1}^{N} (x_i - \mu)^2}{N-1} $$

(For Sample Standard Deviation)

## Performance Profile

### Operation Count (Streaming Mode)

Standard Deviation uses Welford-style running sums of x and x^2 for exact O(1) update.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Ring buffer add/evict | 1 | 3 cy | ~3 cy |
| Update sum_x and sum_x2 | 2 | 2 cy | ~4 cy |
| Compute variance via shortcut formula | 1 | 5 cy | ~5 cy |
| sqrt (variance -> std dev) | 1 | 14 cy | ~14 cy |
| NaN guard + state update | 1 | 2 cy | ~2 cy |
| **Total** | **O(1)** | — | **~28 cy** |

O(1) per update. sqrt() dominates at ~14 cy. Periodic resync prevents catastrophic cancellation in the shortcut variance formula for near-constant series.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | 1.5ns/bar | SIMD-accelerated batch processing. |
| **Allocations** | 0 | Zero-allocation hot path. |
| **Complexity** | O(1) | Constant time streaming updates. |
| **Accuracy** | 10/10 | Matches iterative calculation with high precision. |

## Validation

Validated against external libraries to ensure correctness.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **Skender** | ✅ | Matches `GetStdDev` (Population). |
| **TA-Lib** | ✅ | Matches `STDDEV` (Population). |
| **Tulip** | ✅ | Matches `stddev` (Population). |

## Usage

```csharp
using QuanTAlib;

// Create a 20-period Standard Deviation (Sample)
var stdDev = new StdDev(20, isPopulation: false);

// Update with a new value
var result = stdDev.Update(new TValue(DateTime.UtcNow, 100.0));

// Get the last value
double value = stdDev.Last.Value;