# BW_MFI: Bill Williams Market Facilitation Index

> *The market facilitates price movement when it wants to — volume tells you how hard it tried.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Oscillators                      |
| **Inputs**       | OHLCV bar (TBar)                 |
| **Parameters**   | None                             |
| **Outputs**      | Dual series (Mfi, Zone)          |
| **Output range** | MFI: $\geq 0$; Zone: {0,1,2,3,4} |
| **Warmup**       | 2 bars                           |
| **PineScript**   | [bw_mfi.pine](bw_mfi.pine)      |

- Bill Williams' Market Facilitation Index measures price movement efficiency per unit of volume, then classifies each bar into one of four zones based on MFI and volume direction changes.
- **Similar:** [MARKETFI](../marketfi/Marketfi.md) (MFI value only, no zones) | **Complementary:** [OBV](../../volume/obv/Obv.md), [FI](../fi/Fi.md) | **Trading note:** Zone 4 (Squat) often precedes breakouts; Zone 1 (Green) confirms trend strength.
- Self-validated against direct formula computation. MARKETFI provides the same MFI value; zones are the distinguishing feature.

The Bill Williams Market Facilitation Index extends the basic MFI calculation $\text{MFI} = (H - L) / V$ with a four-zone classification system that compares current MFI and volume to previous bar values. This classification transforms a simple efficiency measure into an actionable market state detector. Zone 4 (Squat) — high volume with compressed range — is Williams' most important signal, indicating a battle between bulls and bears that typically resolves with a breakout. The dual-output design (continuous MFI value plus discrete zone) enables both quantitative analysis and visual bar coloring.

## Historical Context

Bill Williams introduced the Market Facilitation Index in *Trading Chaos* (1995), as part of his broader "Profitunity" trading system. Williams argued that traditional volume analysis was incomplete: knowing that volume increased tells you nothing without understanding whether the market *used* that volume to move price. The MFI answers this question directly — it measures how many price points the market moved per unit of volume traded.

The four-zone classification system was Williams' key innovation over raw MFI. By cross-referencing MFI direction with volume direction, he created a 2×2 matrix that categorizes every bar into one of four market states. This framework appears in both *Trading Chaos* (1995) and *New Trading Dimensions* (1998). The zone names (Green, Fade, Fake, Squat) became part of the standard Williams lexicon and are implemented in most professional trading platforms including MetaTrader, TradingView, and Bloomberg Terminal.

## Architecture & Physics

### 1. MFI Calculation

$$
\text{MFI}_t = \frac{H_t - L_t}{V_t}
$$

where $H_t$, $L_t$, $V_t$ are the high, low, and volume of bar $t$. Zero-volume guard returns 0.0 (no facilitation when no trades occurred). The MFI value is unbounded above and represents price range per unit of volume — higher values indicate more efficient price movement.

### 2. Zone Classification Matrix

The zone is determined by comparing current MFI and volume to the previous bar:

$$
\text{Zone}_t = \begin{cases}
1 \text{ (Green)} & \text{if } \text{MFI}_t > \text{MFI}_{t-1} \text{ and } V_t > V_{t-1} \\
2 \text{ (Fade)}  & \text{if } \text{MFI}_t \leq \text{MFI}_{t-1} \text{ and } V_t \leq V_{t-1} \\
3 \text{ (Fake)}  & \text{if } \text{MFI}_t > \text{MFI}_{t-1} \text{ and } V_t \leq V_{t-1} \\
4 \text{ (Squat)} & \text{if } \text{MFI}_t \leq \text{MFI}_{t-1} \text{ and } V_t > V_{t-1}
\end{cases}
$$

### 3. Zone Interpretation

| Zone | Name  | MFI | Volume | Market State |
| :--: | :---- | :-: | :----: | :----------- |
| 1    | Green | ↑   | ↑      | Trend continuation — market moves efficiently with increasing participation |
| 2    | Fade  | ↓   | ↓      | Fading momentum — traders losing interest, trend exhaustion |
| 3    | Fake  | ↑   | ↓      | Fake breakout — price moves on declining volume, unsupported |
| 4    | Squat | ↓   | ↑      | Accumulation — high volume absorbed by range compression, breakout imminent |

### 4. Complexity

O(1) per bar — single division plus two comparisons. No buffers, no period parameter. The zone classification adds only two boolean comparisons to the base MFI calculation.

## Mathematical Foundation

### Parameters

No configurable parameters. MFI is a pure bar-level computation.

### Output Interpretation

| Output | Type | Range | Description |
| :----- | :--- | :---- | :---------- |
| MFI    | double | $\geq 0$ | Price range per unit of volume |
| Zone   | int    | {0,1,2,3,4} | Market state classification (0 = first bar, insufficient data) |

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Cost (cycles) | Subtotal |
| :-------- | ----: | ------------: | -------: |
| SUB       | 1     | 1             | 1        |
| DIV       | 1     | 15            | 15       |
| CMP       | 2     | 1             | 2        |
| **Total** |       |               | **18**   |

### SIMD Analysis

| Operation | Vectorizable? | Notes |
| :-------- | :-----------: | :---- |
| MFI = (H-L)/V | Yes | Element-wise arithmetic |
| Zone comparison | Limited | Sequential dependency on previous bar |
| Batch MFI only | Full SIMD | No inter-element dependency |

### Quality Metrics

| Metric | Score | Notes |
| :----- | :---: | :---- |
| Accuracy | 10/10 | Exact formula, no approximation |
| Timeliness | 10/10 | Zero lag — current bar only |
| Smoothness | 3/10 | No smoothing — raw bar-level measure |
| Signal clarity | 7/10 | Discrete zones are unambiguous |
| Memory | 10/10 | O(1) — four scalar values |

## Common Pitfalls

1. **Zero volume bars:** Holiday/pre-market bars with zero volume produce MFI = 0 and can skew zone classification on the next bar. Filter these bars or use minimum volume thresholds.

2. **MFI scale varies by instrument:** Raw MFI values are not comparable across instruments with different price levels or volume scales. Use percentage-based normalization for cross-instrument comparison.

3. **Equal values edge case:** When MFI or volume exactly equals the previous bar, the implementation treats this as "not up" — resulting in Zone 2 (Fade) when both are equal, Zone 4 (Squat) when only volume increases, or Zone 3 (Fake) when only MFI increases.

4. **First bar has no zone:** Zone 0 indicates insufficient data (first bar). Ensure downstream logic handles this sentinel value.

## Resources

- **Williams, B.** *Trading Chaos*. Wiley, 1995. Chapter on Market Facilitation Index.
- **Williams, B.** *New Trading Dimensions*. Wiley, 1998. Extended MFI zone analysis.
- **Williams, B.** *Trading Chaos: Second Edition*. Wiley, 2004. Updated zone interpretations.
