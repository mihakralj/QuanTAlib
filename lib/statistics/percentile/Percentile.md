# PERCENTILE: Rolling Percentile

> "There are three kinds of lies: lies, damned lies, and statistics." — Mark Twain.
> But percentiles, at least, tell you exactly where you stand.

## Introduction

The Rolling Percentile computes the value below which a given percentage of observations fall within a sliding window. Unlike fixed percentile calculations over static datasets, the rolling variant maintains a sorted buffer that updates in O(N) per bar, providing real-time distributional context. When p=50, it reduces to the Median; when p=0 or p=100, it returns the window minimum or maximum respectively. The PERCENTILE.INC (inclusive) interpolation method matches Excel and PineScript conventions.

## Historical Context

Percentile calculations date to Francis Galton's work on anthropometric data in the 1880s. The rolling variant emerged with computerized trading systems in the 1990s, where traders needed to know where the current price sits relative to its recent distribution. Multiple interpolation methods exist (nearest-rank, exclusive, inclusive); this implementation uses the inclusive linear interpolation method (C=1 in Hyndman and Fan's taxonomy, Method 7), which matches Excel's `PERCENTILE.INC` and PineScript's `percentile()`.

The distinction matters: Wolfram Alpha uses nearest-rank by default, producing integer-indexed results. Our linear interpolation smoothly transitions between adjacent sorted values, yielding fractional results that better serve continuous financial data.

## Architecture and Physics

### 1. Sorted Buffer Maintenance

The indicator maintains a `double[]` sorted buffer alongside a `RingBuffer` for the sliding window. Each update:

1. **Remove** the oldest value from the sorted buffer (if window full): O(log N) search + O(N) shift.
2. **Insert** the new value into sorted position: O(log N) search + O(N) shift.
3. **Compute** the percentile via linear interpolation: O(1).

Total per-update cost: O(N) for the array shifts, dominated by the `Array.Copy` operations.

### 2. Linear Interpolation (PERCENTILE.INC)

For sorted values $x_0, x_1, \ldots, x_{n-1}$ and percentile $p \in [0, 100]$:

$$\text{rank} = \frac{p}{100} \cdot (n - 1)$$

$$\text{result} = x_{\lfloor r \rfloor} + (r - \lfloor r \rfloor) \cdot (x_{\lceil r \rceil} - x_{\lfloor r \rfloor})$$

where $r = \text{rank}$.

Boundary cases:
- $p = 0$: returns $x_0$ (minimum)
- $p = 100$: returns $x_{n-1}$ (maximum)
- $n = 1$: returns the single value regardless of $p$

### 3. Bar Correction

State rollback uses `_p_sortedBuffer` backup arrays, identical to the Median and IQR pattern. When `isNew=false`, the sorted buffer is restored from the backup before applying the correction.

## Mathematical Foundation

The PERCENTILE.INC formula (Hyndman and Fan Method 7):

$$Q(p) = (1 - g) \cdot x_j + g \cdot x_{j+1}$$

where:
- $j = \lfloor p \cdot (n-1) / 100 \rfloor$
- $g = p \cdot (n-1) / 100 - j$ (fractional part)

This is equivalent to the FMA form used in implementation:

$$Q(p) = \text{FMA}(g, x_{j+1} - x_j, x_j)$$

## Performance Profile

### Operation Count (Streaming Mode)

Percentile maintains a sorted buffer for O(log N) insert and O(1) lookup of the target rank.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Ring buffer evict oldest | 1 | 3 cy | ~3 cy |
| Binary search + array shift insert | log N + N/2 | 2 cy | ~N cy |
| Index computation for rank | 1 | 2 cy | ~2 cy |
| NaN guard + state update | 1 | 2 cy | ~2 cy |
| **Total (N=20)** | **O(N)** | — | **~27 cy** |

O(N) per update due to sorted-array shift. A skip-list or order-statistics tree would achieve O(log N), but for periods ≤500 the sorted array is faster in practice due to cache locality.

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
| PineScript | ✔️ | Source implementation, PERCENTILE.INC interpolation |
| Excel PERCENTILE.INC | ✔️ | Same Method 7 interpolation |
| QuanTAlib Median (p=50) | ✔️ | Cross-validated, exact match |
| Wolfram Alpha | ≠ | Uses nearest-rank (Method 1), different by design |

## Common Pitfalls

1. **Interpolation method confusion.** Wolfram Alpha, NumPy (`linear`), and Excel (`PERCENTILE.INC`) all use slightly different conventions. Our implementation matches Excel/PineScript (Method 7). Do not validate against Wolfram's nearest-rank results.

2. **Period=1 edge case.** A single value has a defined percentile (itself) for any p in [0, 100]. The implementation handles this correctly.

3. **Window not full.** Before reaching full period, the percentile is computed over the available values. This gives valid but potentially misleading results during warmup.

4. **Percent=50 vs Median.** For even-length windows, Percentile(p=50) uses linear interpolation which yields the average of two middle values — identical to Median. For odd-length windows, both return the middle value directly.

5. **NaN propagation.** NaN inputs are replaced with the last valid value. This prevents NaN from contaminating the sorted buffer and producing incorrect percentiles.

6. **Floating-point accumulation.** Since percentile uses direct sorted-buffer access (not running sums), there is no floating-point drift. The result is always computed fresh from the sorted values.

7. **Large periods.** For period > 256, the span batch implementation uses `ArrayPool` instead of `stackalloc` to avoid stack overflow in chained indicator scenarios.

## References

- Hyndman, R.J. and Fan, Y. (1996). "Sample Quantiles in Statistical Packages." _The American Statistician_, 50(4), 361-365.
- Galton, F. (1885). "Some Results of the Anthropometric Laboratory." _Journal of the Anthropological Institute_, 14, 275-287.
- Microsoft Excel Documentation: [PERCENTILE.INC function](https://support.microsoft.com/en-us/office/percentile-inc-function-680f9539-45eb-410b-9a5e-c1355e5fe2ed)
- TradingView PineScript Reference: [ta.percentile_linear_interpolation](https://www.tradingview.com/pine-script-reference/v6/)
