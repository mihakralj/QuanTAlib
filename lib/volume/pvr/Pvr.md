# PVR: Price Volume Rank

> "The relationship between price and volume reveals the conviction behind market moves." — Technical Analysis Axiom

Price Volume Rank distills the price-volume relationship into a simple categorical indicator. Rather than producing a continuous value, PVR returns one of five discrete states (0-4) that classify the current bar's price and volume behavior relative to the previous bar. This creates an instant "market condition" snapshot.

The elegance of PVR lies in its simplicity: it answers two questions simultaneously—is price rising or falling, and is volume supporting that move? The four non-zero categories represent the classic volume confirmation matrix, while zero indicates price equilibrium.

## Historical Context

Price Volume Rank emerged from the fundamental volume analysis principle that volume confirms price. The concept builds on work by technical analysts like Joseph Granville (OBV), Larry Williams (Accumulation/Distribution), and Marc Chaikin, who all emphasized the importance of volume in validating price movements.

Unlike cumulative indicators (OBV, PVT) or ratio-based indicators (PVO, CMF), PVR takes a categorical approach. Each bar is classified independently, producing a discrete signal rather than a continuous value. This makes PVR particularly useful for:

- Pattern recognition algorithms
- Market regime classification
- Volume confirmation at a glance
- Integration with rule-based trading systems

The categorical nature eliminates scale ambiguity—a PVR of 1 always means the same thing regardless of the security, timeframe, or market conditions.

## Architecture & Physics

PVR operates as a stateless classifier that examines the current bar relative to the previous bar. The classification matrix:

| Price Direction | Volume Direction | PVR Value | Interpretation |
| :--- | :--- | :---: | :--- |
| Up | Up | 1 | Strong Bullish |
| Up | Down | 2 | Weak Bullish |
| Down | Down | 3 | Weak Bearish |
| Down | Up | 4 | Strong Bearish |
| Unchanged | Any | 0 | Neutral |

### Component Breakdown

1. **Price Comparison**: Current close vs previous close
2. **Volume Comparison**: Current volume vs previous volume
3. **Category Assignment**: 2x2 matrix lookup plus neutral case

### State Requirements

| Component | Type | Purpose |
| :--- | :--- | :--- |
| PrevPrice | double | Previous bar's price for comparison |
| PrevVolume | double | Previous bar's volume for comparison |
| LastValidPrice | double | Fallback for NaN/Infinity handling |
| LastValidVolume | double | Fallback for NaN/Infinity handling |

## Mathematical Foundation

### Core Formula

$$
PVR_t = \begin{cases}
1 & \text{if } P_t > P_{t-1} \land V_t > V_{t-1} \\
2 & \text{if } P_t > P_{t-1} \land V_t \leq V_{t-1} \\
3 & \text{if } P_t < P_{t-1} \land V_t < V_{t-1} \\
4 & \text{if } P_t < P_{t-1} \land V_t \geq V_{t-1} \\
0 & \text{if } P_t = P_{t-1}
\end{cases}
$$

where:

- $P_t$ = Current price (typically close)
- $P_{t-1}$ = Previous price
- $V_t$ = Current volume
- $V_{t-1}$ = Previous volume

### Category Semantics

**PVR = 1 (Strong Bullish)**: Price rises on increasing volume. Classic confirmation of buying pressure—institutional money likely entering. The most bullish single-bar signal.

**PVR = 2 (Weak Bullish)**: Price rises on decreasing volume. The advance lacks conviction. Could be short covering, thin trading, or distribution into strength.

**PVR = 3 (Weak Bearish)**: Price falls on decreasing volume. The decline lacks selling conviction. Could be profit-taking, thin trading, or accumulation into weakness.

**PVR = 4 (Strong Bearish)**: Price falls on increasing volume. Classic confirmation of selling pressure—institutional money likely exiting. The most bearish single-bar signal.

**PVR = 0 (Neutral)**: Price unchanged. Volume direction is irrelevant when price hasn't moved.

### Volume Edge Cases

The formula uses asymmetric comparisons for volume:
- Bullish categories (1,2): volume comparison is strictly greater/not greater
- Bearish categories (3,4): volume comparison is strictly less/not less

This ensures mutual exclusivity across all price-down scenarios and handles equal volume consistently.

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Notes |
| :--- | :---: | :--- |
| CMP | 4 | Price >, Price <, Volume >, Volume < |
| Branch | 2-3 | Nested conditionals |
| **Total** | 6-7 | Per bar, O(1) |

PVR is extremely lightweight—a handful of comparisons per bar with no arithmetic operations.

### Batch Mode (SIMD)

| Operation | Vectorizable | Notes |
| :--- | :---: | :--- |
| Price differences | ✅ | P[i] - P[i-1] |
| Volume differences | ✅ | V[i] - V[i-1] |
| Sign extraction | ✅ | ConditionalSelect for >0, <0 |
| Category assignment | ✅ | Bitwise combination |

Unlike cumulative indicators, PVR is fully vectorizable because each bar's calculation is independent. SIMD can process 4-8 bars simultaneously.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact integer classification |
| **Timeliness** | 10/10 | Zero lag—responds immediately |
| **Interpretability** | 10/10 | Discrete categories, clear meaning |
| **Noise Resistance** | 5/10 | Single-bar; no smoothing |
| **Memory** | 10/10 | O(1) state: 4 scalar values |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | Not implemented |
| **Skender** | N/A | Not implemented |
| **Tulip** | N/A | Not implemented |
| **Ooples** | N/A | Not implemented |
| **PineScript** | ✅ | Reference implementation matched |

PVR is a proprietary QuanTAlib indicator. The implementation was validated against the PineScript reference to ensure identical categorical assignments across all test cases.

## Common Pitfalls

1. **Not a Trading Signal**: PVR provides market condition classification, not buy/sell signals. Use it as one input among many in a trading system.

2. **Single-Bar Noise**: Because PVR examines only the current and previous bar, it's susceptible to noise. Consider aggregating multiple bars (e.g., count of PVR=1 over last N bars) for robust signals.

3. **Equal Prices Are Neutral**: When price is unchanged, volume direction is ignored. This can be frustrating on consolidation days with significant volume.

4. **Volume Quality**: PVR depends on accurate volume data. After-hours data, exchange-specific feeds, or estimated volume can produce misleading classifications.

5. **Asymmetric Volume Rules**: Volume ties (current = previous) resolve to "not increasing" for bullish moves and "not decreasing" for bearish moves. This is intentional but worth understanding.

6. **TValue Limitations**: The `Update(TValue)` method cannot classify without volume data. Use `Update(price, volume, time)` or `Update(TBar)` for proper calculation.

7. **isNew Parameter**: For bar correction (isNew=false), the implementation properly restores previous state. Incorrect handling causes state inconsistency.

8. **First Bar Behavior**: The first bar comparison uses itself as "previous," resulting in PVR=0 (price unchanged). This is correct initialization behavior.

## Interpretation Guide

### Volume Confirmation Matrix

| | Volume Up | Volume Down |
| :--- | :---: | :---: |
| **Price Up** | ✅ Strong (1) | ⚠️ Weak (2) |
| **Price Down** | ⚠️ Strong (4) | ✅ Weak (3) |

Green checkmarks indicate "confirmed" moves; yellow warnings indicate potential divergence.

### Pattern Recognition

**Accumulation Pattern**: Multiple PVR=3 bars (price down, volume down) followed by PVR=1 (breakout on volume).

**Distribution Pattern**: Multiple PVR=2 bars (price up, volume down) followed by PVR=4 (breakdown on volume).

**Trend Strength**: Consecutive PVR=1 bars indicate sustained buying pressure. Consecutive PVR=4 bars indicate sustained selling pressure.

**Exhaustion Warning**: PVR transitioning from 1→2 (bullish to weak bullish) or 4→3 (bearish to weak bearish) may signal trend weakening.

### Statistical Analysis

Track PVR distribution over rolling windows:

| Metric | Calculation | Interpretation |
| :--- | :--- | :--- |
| Bullish Ratio | (PVR=1 + PVR=2) / N | % of up bars |
| Strong Ratio | (PVR=1 + PVR=4) / N | % of volume-confirmed bars |
| Conviction | (PVR=1 - PVR=4) / N | Net strong sentiment |

## References

- Granville, J. (1963). *Granville's New Key to Stock Market Profits*. Prentice Hall.
- Arms, R. (1989). *Volume Cycles in the Stock Market*. Equis International.
- Blau, W. (1995). *Momentum, Direction, and Divergence*. Wiley.
- Elder, A. (1993). *Trading for a Living*. Wiley.
- Murphy, J. (1999). *Technical Analysis of the Financial Markets*. New York Institute of Finance.