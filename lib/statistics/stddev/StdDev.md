# STDDEV: Standard Deviation

> "Volatility is not risk, but it's the only thing we can measure."

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
