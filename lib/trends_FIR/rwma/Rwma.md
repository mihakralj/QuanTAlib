# RWMA: Range Weighted Moving Average

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (FIR MA)                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period` (default 14)                      |
| **Outputs**      | Single series (Rwma)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `> period` bars                          |
| **PineScript**   | [rwma.pine](rwma.pine)                       |

- RWMA weights each bar's contribution to the average by its price range (high minus low), giving greater influence to volatile bars and less to narr...
- Parameterized by `period` (default 14).
- Output range: Tracks input.
- Requires `> period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "Most averages weight by position: recent bars matter more. RWMA weights by volatility: volatile bars matter more. The market spoke loudest when the range was widest, so listen to those bars."

RWMA weights each bar's contribution to the average by its price range (high minus low), giving greater influence to volatile bars and less to narrow-range, indecisive bars. The logic: a bar with a large range represents stronger price discovery and carries more informational content than a low-range doji. This produces a moving average that gravitates toward prices established during high-activity periods, naturally incorporating volatility as a relevance signal without requiring a separate volatility indicator.

## Historical Context

Range-weighted averaging is a practical adaptation of the general concept of precision-weighted means from statistics, where observations are weighted by the inverse of their variance (or, equivalently, by their "importance" or precision). In financial applications, bar range serves as a real-time proxy for intra-bar volatility, available without the computational overhead of standard deviation or ATR calculations.

The concept appears informally in trading literature from the 1990s, often attributed to floor-trader heuristics: "wide-range bars lead price," meaning that the closing prices of high-range bars tend to be more predictive of subsequent direction than those of narrow-range bars. RWMA formalizes this heuristic into a weighted average.

Unlike position-weighted averages (WMA, EMA) where the weighting scheme is fixed by the period, RWMA's weights are data-adaptive. The weight vector changes every bar based on the range profile of the lookback window. This makes RWMA inherently non-stationary: two windows with identical closing prices but different range profiles produce different RWMA values. The data-adaptive property also means RWMA cannot be expressed as a fixed-coefficient FIR filter, though its computation is structurally similar.

RWMA requires high and low price data (TBar inputs), making it inapplicable to single-valued series. When all bars have zero range (constant price), the denominator collapses to zero and the filter falls back to the raw source price.

## Architecture & Physics

### 1. Weight Computation

For each bar $i$ in the lookback window:

$$
w_i = \max(\text{High}_i - \text{Low}_i, 0)
$$

The $\max$ clamp ensures non-negative weights (relevant for synthetic data where high $<$ low might occur due to data errors).

### 2. Weighted Average

$$
\text{RWMA} = \frac{\sum_{i=0}^{N-1} \text{Close}_i \cdot w_i}{\sum_{i=0}^{N-1} w_i}
$$

If $\sum w_i = 0$ (all bars have zero range), the output degenerates to the current source price.

### 3. TBar Requirement

RWMA consumes TBar data (OHLC), not single-valued TValue. The C# implementation should accept `TBar` inputs and route `High`, `Low`, `Close` appropriately.

## Mathematical Foundation

Given a window of $N$ bars with close prices $c_i$, highs $h_i$, and lows $l_i$ (where $i = 0$ is newest):

$$
\text{RWMA}_t = \frac{\sum_{i=0}^{N-1} c_{t-i} \cdot (h_{t-i} - l_{t-i})}{\sum_{i=0}^{N-1} (h_{t-i} - l_{t-i})}
$$

**Properties:**

- **Convex combination:** All weights are non-negative, so the output is bounded by $[\min(c_i), \max(c_i)]$ within the window. No overshoot possible.
- **Adaptive lag:** Lag shifts toward the position of the highest-range bars. If the most volatile bar is recent, lag decreases; if it is old, lag increases.
- **Degeneracy:** When all ranges are zero, $\text{RWMA} = c_t$ (current close).

**Complexity:** O(N) per bar (single pass over the window).

**Default parameters:** `period = 14`, `minPeriod = 1`.

**Pseudo-code (streaming):**

```
sumWV = 0; sumW = 0
for i = 0 to period-1:
    range = max(high[i] - low[i], 0)
    sumWV += close[i] * range
    sumW  += range

if sumW > 0:
    return sumWV / sumW
else:
    return close[0]
```

## Resources

- Bollinger, J. (2001). *Bollinger on Bollinger Bands*. McGraw-Hill. (Discusses range-based volatility measures in the context of band-width indicators.)
- Achelis, S.B. (2000). *Technical Analysis from A to Z*, 2nd ed. McGraw-Hill.
- Garman, M.B. & Klass, M.J. (1980). "On the Estimation of Security Price Volatilities from Historical Data." *Journal of Business*, 53(1), 67-78. (Range-based volatility estimation from OHLC data.)

## Performance Profile

### Operation Count (Streaming Mode)

RWMA(N) maintains two running sums: `SumCR` (close × range) and `SumR` (range). Each bar subtracts the evicted bar's contributions and adds the new bar's. The output is a single division. Requires TBar (OHLCV) input.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Range: max(high − low, 0) | 2 | 1 | ~2 |
| Close × range product | 1 | 3 | ~3 |
| SumCR update (subtract evicted, add new) | 2 | 1 | ~2 |
| SumR update (subtract evicted, add new) | 2 | 1 | ~2 |
| RWMA: SumCR / SumR (with zero-guard) | 1 | 8 | ~8 |
| **Total** | **8** | — | **~17 cycles** |

O(1) per bar. The division is the dominant cost. Resync every 1000 bars prevents floating-point drift in the running sums. WarmupPeriod = N.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| Range computation (H − L) | Yes | `VSUBPD`; element-wise across bar array |
| Close × range product | Yes | `VMULPD`; element-wise |
| Prefix sum of (close × range) | Partial | Sliding window subtraction requires scan; prefix approach viable |
| Prefix sum of range | Partial | Same as above |
| Final division | Yes | `VDIVPD` after prefix sums built; zero-guard via `VCMPPD` + blend |

Both prefix sums can be built with AVX2 prefix-scan kernels. Once built, all N sliding-window divisions can be computed in parallel. Batch speedup: approximately 4× over scalar for large series.
