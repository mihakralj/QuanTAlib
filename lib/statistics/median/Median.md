# MEDIAN: Rolling Median

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Statistic                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Median)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period` bars                          |

### TL;DR

- The Rolling Median is a robust statistic that represents the middle value of a dataset within a moving window.
- Parameterized by `period`.
- Output range: Varies (see docs).
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "The average is easily influenced by outliers; the median stands its ground."

The Rolling Median is a robust statistic that represents the middle value of a dataset within a moving window. Unlike the Simple Moving Average (SMA), which can be skewed by extreme values, the Median provides a more stable measure of central tendency, making it particularly useful for filtering noise in volatile markets.

## Historical Context

The concept of the median dates back to Edward Wright in 1599, but its application in time-series analysis became prominent with the rise of robust statistics in the 20th century. In technical analysis, it is often used as a replacement for moving averages to identify trends without the lag induced by averaging large deviations.

## Architecture & Physics

The Median calculation requires maintaining a sorted view of the data window.

* **Inertia**: High. A single new data point rarely shifts the median significantly unless it crosses the middle threshold.
* **Stability**: Extremely robust against outliers. A price spike of 1000% has the same effect on the median as a spike of 1%.
* **Complexity**: $O(N \log N)$ per update due to sorting, where $N$ is the period. For typical trading periods ($N < 200$), this is negligible on modern CPUs.

## Mathematical Foundation

For a window of $N$ values $X = \{x_1, x_2, ..., x_N\}$ sorted in ascending order:

### 1. Odd Period

If $N$ is odd, the median is the middle element:
$$ \text{Median} = X_{(N+1)/2} $$

### 2. Even Period

If $N$ is even, the median is the average of the two middle elements:
$$ \text{Median} = \frac{X_{N/2} + X_{(N/2)+1}}{2} $$

## Performance Profile

### Operation Count (Streaming Mode)

Median maintains a sorted buffer; each bar requires a binary-search insert plus array shift.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Ring buffer evict oldest | 1 | 3 cy | ~3 cy |
| Binary search + array shift insert | log N + N/2 | 2 cy | ~N cy |
| Extract middle element(s) | 1 | 1 cy | ~1 cy |
| NaN guard + state update | 1 | 2 cy | ~2 cy |
| **Total (N=14)** | **O(N)** | — | **~20 cy** |

O(N) per update. For large N, a dual-heap (min-heap + max-heap) O(log N) structure would be faster, but for typical periods (≤200) the sorted-array approach is cache-friendly.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | High | $O(N \log N)$ is fast for small $N$. |
| **Allocations** | 0 | Uses pre-allocated buffers and in-place sorting. |
| **Complexity** | $O(N \log N)$ | Sorting dominates the cost. |
| **Accuracy** | 10/10 | Exact calculation. |
| **Timeliness** | Medium | Lags similar to SMA but handles steps differently. |
| **Smoothness** | High | Filters out noise effectively. |

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **Math.NET** | ✅ | Matches statistical definition. |
| **Excel** | ✅ | Matches `MEDIAN()` function. |
| **Python** | ✅ | Matches `numpy.median`. |

### Common Pitfalls

* **Quantization**: The median moves in discrete steps (jumps from one value to another) rather than smoothly like an average.
* **Flatlining**: In periods of low volatility, the median can remain constant for many bars, which may be interpreted as a lack of trend.
