# QUANTILE: Rolling Quantile

> "The quantile function is the inverse of the distribution function." — Every probability textbook ever written, and yet somehow it still surprises people.

## Introduction

The Rolling Quantile computes the value below which a given fraction of observations fall within a sliding window. It is mathematically identical to Percentile but uses the statistician's convention of q ∈ [0, 1] instead of the analyst's p ∈ [0, 100]. When q=0.5, it returns the median; q=0 gives the minimum; q=1 gives the maximum. The linear interpolation method matches Excel's PERCENTILE.INC and PineScript's `ta.percentile_linear_interpolation` conventions (Hyndman-Fan Method 7).

## Historical Context

Francis Galton introduced percentiles in 1885. The quantile formulation (0 to 1) gained dominance in mathematical statistics because it maps directly to cumulative distribution functions. In practice, the two are interchangeable: quantile q = percentile(100q). The choice between them is a matter of API convention, not mathematics. Trading platforms tend to use percentiles (0-100 range, more intuitive for non-statisticians); statistical libraries prefer quantiles (0-1 range, composable with CDFs and probability calculations).

Our implementation provides both: `Percentile` for the 0-100 convention, `Quantile` for the 0-1 convention. They share identical algorithms.

## Architecture and Physics

### 1. Sorted Buffer Maintenance

Each `Update` call:

1. **Remove** the oldest value from the sorted buffer (if window full): O(log N) search + O(N) shift.
2. **Insert** the new value into sorted position: O(log N) search + O(N) shift.
3. **Compute** the quantile via linear interpolation: O(1).

Total per-update cost: O(N) for the array shifts, dominated by the `Array.Copy` operations.

### 2. Linear Interpolation (Hyndman-Fan Method 7)

For sorted values $x_0, x_1, \ldots, x_{n-1}$ and quantile level $q \in [0, 1]$:

$$\text{rank} = q \cdot (n - 1)$$

$$\text{result} = x_{\lfloor r \rfloor} + (r - \lfloor r \rfloor) \cdot (x_{\lceil r \rceil} - x_{\lfloor r \rfloor})$$

where $r = \text{rank}$.

Boundary cases:

- $q = 0$: returns $x_0$ (minimum)
- $q = 1$: returns $x_{n-1}$ (maximum)
- $n = 1$: returns the single value regardless of $q$

### 3. Bar Correction

State rollback uses `_p_sortedBuffer` backup arrays, identical to the Percentile, Median, and IQR pattern. When `isNew=false`, the sorted buffer is restored from the backup before applying the correction.

## Mathematical Foundation

The quantile function $Q(q)$ for a discrete sample using Hyndman-Fan Method 7:

$$Q(q) = (1 - g) \cdot x_j + g \cdot x_{j+1}$$

where:

- $j = \lfloor q \cdot (n-1) \rfloor$
- $g = q \cdot (n-1) - j$ (fractional part)

This is equivalent to the FMA form used in implementation:

$$Q(q) = \text{FMA}(g, x_{j+1} - x_j, x_j)$$

Relationship to Percentile: $Q(q) = P(100q)$ where $P$ is the percentile function.

## Performance Profile

| Operation | Cost | Notes |
|-----------|------|-------|
| BinarySearch | O(log N) | `Array.BinarySearch` for insert/remove position |
| Array.Copy (shift) | O(N) | Dominates update cost |
| Interpolation | O(1) | Single FMA operation |
| Bar correction | O(N) | `Array.Copy` for buffer backup/restore |
| Memory | O(2N) | Sorted buffer + backup buffer |

| Quality | Score (1-10) |
|---------|-------------|
| Precision | 10 — exact within IEEE 754 double precision |
| Latency | 7 — O(N) per update, fast for typical periods (5-50) |
| Memory | 8 — two double arrays + RingBuffer |
| Robustness | 9 — NaN/Infinity guarded, bar correction supported |
| SIMD applicability | 2 — comparison-heavy algorithm not vectorizable |

## Validation

| Library | Match | Notes |
|---------|-------|-------|
| PineScript | ✔️ | Source implementation, same linear interpolation |
| Excel PERCENTILE.INC | ✔️ | Same Method 7 interpolation (q = p/100) |
| QuanTAlib Percentile | ✔️ | Cross-validated, Quantile(q) == Percentile(q*100) |
| QuanTAlib Median (q=0.5) | ✔️ | Cross-validated, exact match |
| Wolfram Alpha | ≠ | Uses nearest-rank (Method 1), different by design |

## Common Pitfalls

1. **Parameter range confusion.** Quantile uses q ∈ [0, 1], not [0, 100]. Passing 25 instead of 0.25 will throw `ArgumentException`. Use `Percentile` if you prefer the 0-100 range.

2. **Interpolation method confusion.** Wolfram Alpha, NumPy (`linear`), and Excel (`PERCENTILE.INC`) all use slightly different conventions. Our implementation matches Excel/PineScript (Method 7). Do not validate against Wolfram's nearest-rank results.

3. **Period=1 edge case.** A single value has a defined quantile (itself) for any q in [0, 1]. The implementation handles this correctly.

4. **Window not full.** Before reaching full period, the quantile is computed over the available values. This gives valid but potentially misleading results during warmup.

5. **q=0.5 vs Median.** For even-length windows, Quantile(q=0.5) uses linear interpolation which yields the average of two middle values — identical to Median. For odd-length windows, both return the middle value directly.

6. **Floating-point accumulation.** Since quantile uses direct sorted-buffer access (not running sums), there is no floating-point drift. The result is always computed fresh from the sorted values.

7. **Large periods.** For period > 256, the span batch implementation uses `ArrayPool` instead of `stackalloc` to avoid stack overflow in chained indicator scenarios.

## References

- Hyndman, R.J. and Fan, Y. (1996). "Sample Quantiles in Statistical Packages." *The American Statistician*, 50(4), 361-365.
- Galton, F. (1885). "Some Results of the Anthropometric Laboratory." *Journal of the Anthropological Institute*, 14, 275-287.
- Microsoft Excel Documentation: [PERCENTILE.INC function](https://support.microsoft.com/en-us/office/percentile-inc-function-680f9539-45eb-410b-9a5e-c1355e5fe2ed)
- TradingView PineScript Reference: [ta.percentile_linear_interpolation](https://www.tradingview.com/pine-script-reference/v6/)
