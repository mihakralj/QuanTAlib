# MeanDev: Mean Deviation (Average Absolute Deviation)

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Statistic                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (MeanDev)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period` bars                          |

### TL;DR

- ````markdown
- Parameterized by `period`.
- Output range: Varies (see docs).
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "Not all dispersion is created equal — some prefer robustness over elegance."

Mean Deviation (also known as Mean Absolute Deviation or Average Absolute Deviation) measures the average of the absolute deviations from the mean. Unlike Standard Deviation, it does not square the deviations, making it more robust to outliers and more intuitive to interpret.

## Historical Context

Mean Deviation was one of the earliest measures of dispersion, predating Standard Deviation. Karl Pearson famously argued for the superiority of Standard Deviation in the early 20th century due to its mathematical tractability, but Mean Deviation has seen renewed interest in robust statistics and financial applications. It is a core component of the Commodity Channel Index (CCI), introduced by Donald Lambert in 1980.

## Architecture & Physics

`MeanDev` uses a sliding window (RingBuffer) to maintain the last `N` data points. For each update, it calculates the arithmetic mean and then averages the absolute deviations from that mean across the window.

### Key Design Principles

* **O(N) per update**: Since the mean changes with each new data point, the absolute deviations must be recalculated across the window.
* **Circular Buffer**: Uses a ring buffer of size `Period` for efficient sliding window management.
* **Robustness**: Less sensitive to outliers than variance-based measures because deviations are not squared.

## Mathematical Foundation

The Mean Deviation is defined as:

$$ MD = \frac{1}{N} \sum_{i=1}^{N} |x_i - \bar{x}| $$

Where:

* $x_i$ is each observed value in the window.
* $\bar{x}$ is the arithmetic mean of the window: $\bar{x} = \frac{1}{N} \sum_{i=1}^{N} x_i$
* $N$ is the number of data points (period).

### Relationship to Standard Deviation

For a normal distribution:

$$ MD \approx \sqrt{\frac{2}{\pi}} \cdot \sigma \approx 0.7979 \cdot \sigma $$

Mean Deviation is always less than or equal to Standard Deviation for the same dataset.

## Performance Profile

### Operation Count (Streaming Mode)

Mean Deviation (MAD about the mean) requires computing the window mean first, then summing absolute deviations — O(N) per bar.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Ring buffer add/evict | 1 | 3 cy | ~3 cy |
| Compute window mean (running sum) | 1 | 2 cy | ~2 cy |
| Sum absolute deviations | N | 3 cy | ~3N cy |
| Divide by N | 1 | 4 cy | ~4 cy |
| NaN guard + state update | 1 | 2 cy | ~2 cy |
| **Total (N=14)** | **O(N)** | — | **~53 cy** |

O(N) per update — no O(1) formulation for mean absolute deviation (unlike variance). The abs() required for each deviation prevents the running-sum trick.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | Moderate | O(N) per update for deviation calculation. |
| **Allocations** | 0 | Zero-allocation hot path with ring buffer. |
| **Complexity** | O(N) | Must iterate window for absolute deviations. |
| **Accuracy** | High | Straightforward calculation with no numerical pitfalls. |

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **Excel** | ✅ | Matches `AVEDEV` function. |
| **NumPy** | ✅ | Matches manual `mean(abs(x - mean(x)))`. |

## Usage

```csharp
using QuanTAlib;

// Create a 14-period Mean Deviation
var meanDev = new MeanDev(14);

// Update with a new value
var result = meanDev.Update(new TValue(DateTime.UtcNow, 100.0));

// Get the last value
double value = meanDev.Last.Value;
```

## See Also

* **StdDev** — Standard Deviation (quadratic weighting of deviations).
* **Variance** — Variance (squared deviations from mean).
* **Cci** — Commodity Channel Index (uses Mean Deviation as a normalizer).
````
