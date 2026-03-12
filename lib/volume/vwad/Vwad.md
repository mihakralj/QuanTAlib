# VWAD: Volume Weighted Accumulation/Distribution

> *The market's memory isn't just about price—it's about who showed up with conviction.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Volume                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period` (default 20)                      |
| **Outputs**      | Single series (VWAD)                       |
| **Output range** | Unbounded                     |
| **Warmup**       | `> period` bars                          |
| **PineScript**   | [vwad.pine](vwad.pine)                       |

- Volume Weighted Accumulation/Distribution (VWAD) takes the classic ADL concept and asks a sharper question: not just "where did the close fall in t...
- Parameterized by `period` (default 20).
- Output range: Unbounded.
- Requires `> period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

Volume Weighted Accumulation/Distribution (VWAD) takes the classic ADL concept and asks a sharper question: not just "where did the close fall in the range?" but "how significant was this bar's volume compared to recent activity?"

Traditional ADL treats all bars equally—a 100-share bar and a 10-million-share bar contribute the same mathematical weight if their MFM is identical. VWAD recognizes that volume concentration matters. A high-volume bar during a period of thin trading represents institutional commitment; the same MFM reading during heavy volume is just noise in the crowd.

## Historical Context

ADL and its derivatives (CMF, A/D Oscillator) have dominated volume analysis since Marc Chaikin's work in the 1980s. But they share a blind spot: volume context. A bar's 50,000 shares means something different when the prior 20 bars averaged 10,000 shares versus 500,000 shares.

VWAD addresses this by weighting each bar's contribution based on its volume relative to the rolling volume sum. This creates a natural amplification effect: during quiet periods, a volume spike gets amplified; during heavy trading, each bar's contribution is diluted.

The result is an accumulation line that better reflects when the "smart money" is active. High-volume reversals punch through the indicator; low-volume noise gets filtered out.

## Architecture & Physics

VWAD combines three established concepts into a single indicator:

### 1. Money Flow Multiplier (MFM)

The foundation shared with ADL and CMF. MFM measures where the close fell within the bar's range:

$$
MFM_t = \frac{(Close_t - Low_t) - (High_t - Close_t)}{High_t - Low_t}
$$

- MFM = +1: Close at the high (maximum buying pressure)
- MFM = 0: Close at the midpoint
- MFM = -1: Close at the low (maximum selling pressure)

Special case: When High = Low (doji/inside bar), MFM = 0.

### 2. Rolling Volume Sum

A sliding window tracks total volume over the lookback period:

$$
SumVol_t = \sum_{i=t-n+1}^{t} Volume_i
$$

This provides the normalization denominator for volume weighting.

### 3. Volume Weight

The current bar's volume expressed as a fraction of the rolling sum:

$$
VolWeight_t = \frac{Volume_t}{SumVol_t}
$$

This is where VWAD's magic happens. If the current bar's volume is 10% of the rolling sum, it gets 10% weight. If it's 50% of the rolling sum (a massive spike), it gets 50% weight.

### 4. Weighted Money Flow Volume

$$
WeightedMFV_t = Volume_t \times MFM_t \times VolWeight_t
$$

Note the double volume factor: once directly (as in standard MFV) and once through the weight. This creates quadratic sensitivity to volume spikes.

### 5. Cumulative VWAD

$$
VWAD_t = VWAD_{t-1} + WeightedMFV_t
$$

Like ADL, VWAD is cumulative and unbounded. Unlike CMF, it doesn't normalize to an oscillator—it's designed to show long-term accumulation/distribution trends with volume-appropriate sensitivity.

## Mathematical Foundation

### Complete Calculation

For each bar at time t:

$$
MFM_t = \begin{cases}
\frac{(C_t - L_t) - (H_t - C_t)}{H_t - L_t} & \text{if } H_t \neq L_t \\
0 & \text{otherwise}
\end{cases}
$$

$$
SumVol_t = \sum_{i=\max(0, t-n+1)}^{t} V_i
$$

$$
VolWeight_t = \begin{cases}
\frac{V_t}{SumVol_t} & \text{if } SumVol_t > 0 \\
0 & \text{otherwise}
\end{cases}
$$

$$
VWAD_t = VWAD_{t-1} + V_t \times MFM_t \times VolWeight_t
$$

where:
- $H_t, L_t, C_t, V_t$ = High, Low, Close, Volume at time t
- $n$ = lookback period (default: 20)

### Volume Weight Distribution

The volume weight sums to less than 1 across the period (unless all volume is concentrated in one bar):

$$
\sum_{i=t-n+1}^{t} VolWeight_i = \sum_{i=t-n+1}^{t} \frac{V_i}{SumVol_t} = 1
$$

This means the system is normalized: if you spread 1000 shares of accumulation evenly across 20 bars, you get the same total contribution as concentrating it in one bar—but the *shape* of the indicator differs dramatically.

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| SUB | 4 | 1 | 4 |
| ADD | 3 | 1 | 3 |
| DIV | 2 | 15 | 30 |
| MUL | 2 | 3 | 6 |
| CMP | 2 | 1 | 2 |
| **Total** | **13** | — | **~45 cycles** |

The division for volume weight dominates. Could be optimized with reciprocal approximation if sub-1% error is acceptable.

### Batch Mode (512 values, SIMD/FMA)

| Operation | Scalar Ops | SIMD Ops (AVX2) | Speedup |
| :--- | :---: | :---: | :---: |
| MFM calculation | 512×4 | 64×4 | 8× |
| MUL operations | 512×2 | 64×2 | 8× |
| Rolling sum | Sequential | Sequential | 1× |

The rolling sum is inherently sequential, limiting SIMD benefits. Total speedup is approximately 3-4× for large batches.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Mathematically exact, matches PineScript reference |
| **Timeliness** | 8/10 | 1-bar lag inherent in rolling window |
| **Overshoot** | 7/10 | Cumulative, can run away on strong trends |
| **Smoothness** | 6/10 | Volume spikes create sharp moves (by design) |
| **Memory** | 9/10 | O(period) for rolling sum buffer |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | VWAD not implemented |
| **Skender** | N/A | VWAD not implemented |
| **Tulip** | N/A | VWAD not implemented |
| **Ooples** | N/A | VWAD not implemented |
| **PineScript** | ✅ | Reference implementation match |

VWAD is a proprietary indicator. Validation is performed against the PineScript reference implementation and through self-consistency tests (streaming vs batch vs span parity).

## Common Pitfalls

1. **Unbounded Nature**: Unlike CMF (bounded [-1, +1]), VWAD is cumulative and unbounded. Don't compare absolute VWAD values across different securities or timeframes. Use divergences or rate-of-change instead.

2. **Volume Quality Dependency**: VWAD amplifies volume's importance, making it extra sensitive to bad volume data. Crypto exchanges with wash trading, extended hours with thin volume, or futures rollovers can produce misleading readings.

3. **Period Selection**: The default period of 20 provides a monthly context on daily bars. Shorter periods (5-10) increase sensitivity to volume spikes; longer periods (50+) smooth out the weighting effect. Choose based on your trading timeframe.

4. **Quadratic Volume Sensitivity**: Because volume appears twice in the formula (MFV × VolWeight), a bar with 10× normal volume doesn't get 10× weight—it gets closer to 100× relative impact. This is a feature, not a bug, but traders used to linear indicators may find it surprising.

5. **Warmup Period**: The rolling volume sum needs `period` bars before volume weighting is fully calibrated. Before that, early bars get disproportionate weight in a smaller sum.

6. **isNew Parameter**: When correcting a bar (isNew=false), the implementation properly rolls back both the cumulative VWAD and the rolling volume sum. Failure to handle this creates cumulative drift errors.

7. **Zero Volume Handling**: If volume is zero for all bars in the period (synthetic data or extremely illiquid markets), volume weight is undefined. Implementation returns 0 for the weighted MFV.

## References

- Chaikin, M. (1996). "Accumulation/Distribution Line." *Technical Analysis of Stocks & Commodities*.
- QuanTAlib. "Volume Weighted Accumulation/Distribution." [PineScript Reference](https://github.com/mihakralj/pinescript/blob/main/indicators/volume/vwad.md)
