# VWMA: Volume Weighted Moving Average

> *VWMA reveals where the smart money traded—not just where price went, but where conviction backed the moves.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Volume                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period` (default 20)                      |
| **Outputs**      | Single series (VWMA)                       |
| **Output range** | Unbounded                     |
| **Warmup**       | `> period` bars                          |
| **PineScript**   | [vwma.pine](vwma.pine)                       |

- VWMA (Volume Weighted Moving Average) calculates a moving average where each price is weighted by its corresponding volume over a specified lookbac...
- **Similar:** [EVWMA](../evwma/Evwma.md), [SMA](../../trends_FIR/sma/Sma.md) | **Complementary:** OBV | **Trading note:** Volume-Weighted MA; weights price by volume. More responsive during high-volume bars.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

VWMA (Volume Weighted Moving Average) calculates a moving average where each price is weighted by its corresponding volume over a specified lookback period. Unlike VWAP which accumulates from a reset point, VWMA uses a sliding window that continuously drops old values, making it a true moving average. Bars with higher volume contribute more to the average, surfacing price levels where institutional activity concentrated.

## Historical Context

Volume-weighted calculations predate modern technical analysis, with floor traders intuitively weighting their mental price averages by the volume they observed at each level. The formalization of VWMA emerged alongside computing power in the 1970s-80s when chartists could finally automate what was previously impossible to calculate by hand.

VWMA gained popularity as an alternative to simple moving averages (SMA) after practitioners noticed that treating all bars equally ignored crucial market information. A bar where 10 million shares traded at $100 conveys far more information about fair value than a bar where 10,000 shares traded at $105. SMA treats them identically; VWMA does not.

The distinction from VWAP is critical: VWAP resets at session boundaries and accumulates indefinitely, while VWMA maintains a fixed lookback window. This makes VWMA more responsive to recent price action and suitable for trend-following applications where you want volume confirmation without anchoring bias.

## Architecture & Physics

VWMA operates as a sliding window weighted average with circular buffer state management.

### 1. Sliding Window Design

Unlike cumulative indicators, VWMA must track and remove old values as new ones arrive:

$$
VWMA_t = \frac{\sum_{i=t-period+1}^{t} (P_i \times V_i)}{\sum_{i=t-period+1}^{t} V_i}
$$

This requires maintaining both sums and the individual values that contributed to them, enabling O(1) updates.

### 2. Circular Buffer State

The implementation uses arrays with head pointer for O(1) operations:

```
_pvBuffer[period]  // Price × Volume values
_vBuffer[period]   // Volume values
_head              // Current insertion point
_count             // Bars accumulated (≤ period)
```

### 3. Running Sum Management

On each new bar:
1. Remove old contribution: `sumPV -= _pvBuffer[head]`, `sumVol -= _vBuffer[head]`
2. Store new contribution: `_pvBuffer[head] = price × volume`, `_vBuffer[head] = volume`
3. Advance head: `head = (head + 1) % period`
4. Add new contribution: `sumPV += newPV`, `sumVol += newVol`

### 4. Division Safety

When total volume in window is zero:

$$
VWMA_t = \begin{cases}
\frac{\sum PV}{\sum V} & \text{if } \sum V > 0 \\
P_t & \text{if } \sum V = 0
\end{cases}
$$

## Mathematical Foundation

### Weighted Moving Average Form

VWMA is a specific case of the weighted moving average where weights equal volume:

$$
VWMA_t = \frac{\sum_{i=0}^{n-1} w_i \cdot P_{t-i}}{\sum_{i=0}^{n-1} w_i}
$$

where $w_i = V_{t-i}$ and $n = period$.

### Properties

- **Bounded**: $\min(P_{window}) \leq VWMA \leq \max(P_{window})$
- **Adaptive**: Higher volume bars pull VWMA toward their price
- **Responsive**: Old values drop out immediately when window slides

### Comparison with VWAP

| Property | VWMA | VWAP |
| :--- | :--- | :--- |
| Window | Fixed sliding | Cumulative from reset |
| Memory | O(period) | O(1) |
| Sensitivity | Constant responsiveness | Decreasing over time |
| Use case | Trend following | Execution benchmark |

### Incremental Update Derivation

Let $S_{pv}^{(t)}$ denote the sum of price×volume at time $t$:

$$
S_{pv}^{(t)} = S_{pv}^{(t-1)} - (P_{t-period} \times V_{t-period}) + (P_t \times V_t)
$$

This maintains O(1) complexity regardless of period length.

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ADD/SUB | 4 | 1 | 4 |
| MUL | 1 | 3 | 3 |
| DIV | 1 | 15 | 15 |
| MOD | 1 | 10 | 10 |
| Array access | 4 | 2 | 8 |
| **Total** | **11** | — | **~40 cycles** |

### Memory Footprint

- **State struct**: 40 bytes
- **Buffers**: 2 × period × 8 bytes = 16 × period bytes
- **Period 20 (default)**: 40 + 320 = 360 bytes
- **Period 200**: 40 + 3200 = 3240 bytes

Buffer memory scales linearly with period—this is unavoidable for sliding window semantics.

### SIMD Potential (Batch Mode)

For batch calculation from scratch, SIMD can parallelize:
- Price × Volume multiplication: 8× speedup (AVX2 double)
- Prefix sums: Limited by data dependency

| Operation | Scalar | SIMD (AVX2) | Speedup |
| :--- | :---: | :---: | :---: |
| P×V products | N×period | N×period/8 | 8× |
| Window sums | N | N | 1× |

**Net batch improvement**: ~25-30% due to multiplication dominating early bars.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact weighted average |
| **Timeliness** | 8/10 | Lags by ~period/2 bars |
| **Overshoot** | 2/10 | Minimal overshoot |
| **Smoothness** | 7/10 | Smoother than SMA when volume varies |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | Not implemented |
| **Skender** | ✅ | Matches `GetVwma(period)` within tolerance |
| **Tulip** | N/A | Not implemented |
| **Ooples** | N/A | Not implemented |
| **Self-consistency** | ✅ | Streaming/Batch/Span modes match |

## Common Pitfalls

1. **Warmup Period**: First `period-1` bars use partial window. `IsHot` becomes true only after `period` bars accumulated. Expect different values during warmup vs full window operation.

2. **Memory Scaling**: Unlike cumulative indicators, VWMA requires O(period) memory. Very large periods (>10,000) should consider memory implications: 10,000 period ≈ 160KB per instance.

3. **Zero Volume Handling**: When total volume in window is zero, VWMA returns current price. This is rare in liquid markets but can occur with filtered or synthetic data.

4. **VWAP Confusion**: VWMA uses sliding window (drops old values); VWAP uses cumulative window (never drops). They serve different purposes—don't interchange them.

5. **TBar vs TValue**: `Update(TBar)` uses close price and bar volume. `Update(TValue)` uses value as price with synthetic volume=1, losing volume-weighting benefits. Prefer TBar input for meaningful VWMA.

6. **Circular Buffer State**: Bar correction (`isNew=false`) restores previous state completely. Multiple corrections on same bar work correctly.

## References

- Arms, R. (1989). "Volume Cycles in the Stock Market." Equis International.
- Achelis, S. (2000). "Technical Analysis from A to Z." McGraw-Hill.
- TradingView. "Pine Script VWMA Reference." [tradingview.com](https://www.tradingview.com/pine-script-reference/v5/#fun_ta.vwma)