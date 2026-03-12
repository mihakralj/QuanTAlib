# PIVOTEXT: Extended Traditional Pivot Points

> *Classic pivots tell you where the crowd expects the market to pause. Extended pivots tell you where the crowd starts to panic.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Reversal                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | None                      |
| **Outputs**      | Single series (PIVOTEXT)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `2` bars                          |
| **PineScript**   | [pivotext.pine](pivotext.pine)                       |

- Extended Traditional Pivot Points calculate eleven horizontal support and resistance levels from the previous bar's high, low, and close.
- No configurable parameters; computation is stateless per bar.
- Output range: Varies (see docs).
- Requires `2` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

Extended Traditional Pivot Points calculate eleven horizontal support and resistance levels from the previous bar's high, low, and close. The core levels (PP, R1-R3, S1-S3) are identical to classic floor trader pivots. The extension adds R4/R5 and S4/S5 levels that project further beyond the prior bar's range, covering extreme move scenarios such as gap opens, news-driven spikes, and trend continuation through multiple prior-range increments. The formula is pure arithmetic with zero parameters.

## Historical Context

Floor trader pivots date to the 1930s when pit traders computed PP = (H + L + C) / 3 and derived three symmetric support/resistance levels from it. The formula was simple enough to compute by hand before the market open, making it one of the earliest systematic approaches to intraday level identification.

Classic pivots (R1-R3, S1-S3) cover the range from roughly 1x to 2x the prior bar's range projected from the high or low. In practice, large gap opens or momentum-driven moves routinely exceed R3/S3. Traders discovered they needed additional levels to bracket these extreme scenarios without switching to entirely different frameworks (Fibonacci extensions, measured moves, etc.).

The extended formula simply continues the same arithmetic progression. R3 uses a 2x multiplier on (PP - L) added to H; R4 uses 3x; R5 uses 4x. The symmetry holds for support levels. This mechanical extension preserves the simplicity of the original system while providing reference levels for moves that exceed the "normal" 1-3 range pivots.

Unlike Fibonacci pivots, Camarilla pivots, or DeMark pivots, the extended traditional formula makes no claim about specific retracement ratios or market microstructure. The levels are pure geometric projections of the prior range. Their value lies in consensus: enough traders watch these levels that they become self-reinforcing reference points.

## Architecture and Physics

### 1. Previous Bar's HLC

The indicator stores the high ($H$), low ($L$), and close ($C$) of the most recently completed bar. On each new bar, these stored values become the basis for computing the current bar's pivot levels, and the new bar's HLC replaces them for the next computation.

### 2. Central Pivot Point (PP)

$$PP = \frac{H_{prev} + L_{prev} + C_{prev}}{3}$$

The arithmetic mean of the previous bar's HLC. This is the gravitational center of the level system. All other levels derive from PP and the prior range.

### 3. Range and Intermediate Values

$$range = H_{prev} - L_{prev}$$

$$ppMinusL = PP - L_{prev}$$

$$hMinusPP = H_{prev} - PP$$

The range scales the distance between levels. The asymmetric terms $ppMinusL$ and $hMinusPP$ determine how far resistance extends above H and support extends below L.

### 4. Classic Levels (R1-R3, S1-S3)

$$R_1 = 2 \cdot PP - L_{prev} \qquad S_1 = 2 \cdot PP - H_{prev}$$

$$R_2 = PP + range \qquad S_2 = PP - range$$

$$R_3 = H_{prev} + 2 \cdot (PP - L_{prev}) \qquad S_3 = L_{prev} - 2 \cdot (H_{prev} - PP)$$

### 5. Extended Levels (R4-R5, S4-S5)

$$R_4 = H_{prev} + 3 \cdot (PP - L_{prev}) \qquad S_4 = L_{prev} - 3 \cdot (H_{prev} - PP)$$

$$R_5 = H_{prev} + 4 \cdot (PP - L_{prev}) \qquad S_5 = L_{prev} - 4 \cdot (H_{prev} - PP)$$

The progression is arithmetic: each successive level adds one more $ppMinusL$ (resistance) or $hMinusPP$ (support) increment.

### 6. Level Ordering Invariant

For any bar where $H_{prev} > L_{prev}$ (non-degenerate range):

$$S_5 < S_4 < S_3 < S_2 < S_1 \leq PP \leq R_1 < R_2 < R_3 < R_4 < R_5$$

When the close is exactly at the midpoint of the range, $PP = S_1 = R_1$ (all collapse to the midpoint), and the S/R levels fan out symmetrically. In general, the close's position within the range determines the asymmetry between resistance and support spacing.

### 7. Eleven Outputs

All eleven levels are computed simultaneously and remain constant until a new bar arrives. The primary output (`Last.Val`) returns PP; individual properties expose all eleven levels.

### Signal Interpretation

| Condition | Interpretation |
| :--- | :--- |
| Price between S1 and R1 | Normal range; no directional bias |
| Price tests R2 from below | First extension test; watch for rejection |
| Price tests S2 from above | First support extension; potential bounce |
| Price reaches R3/S3 | Classic extreme; high-probability reversal zone |
| Price breaks R4/S4 | Significant momentum; extended trend likely |
| Price reaches R5/S5 | Rare extreme; potential exhaustion or blow-off |
| Levels cluster tightly | Prior bar had low range; expect volatility expansion |
| Wide level spacing | Prior bar was volatile; levels may be less precise |

## Mathematical Foundation

### Parameters

Extended Traditional Pivot Points has no configurable parameters. The formula is fixed by definition.

| Parameter | Value | Notes |
| :--- | :---: | :--- |
| Inputs | H, L, C | Previous bar's high, low, close |
| Outputs | 11 | PP, R1, R2, R3, R4, R5, S1, S2, S3, S4, S5 |
| Parameters | 0 | No tuning required |

### Warmup Period

$$W = 2$$

The indicator requires 2 bars: the first bar provides HLC for storage; the second bar triggers computation from the stored values. Prior to warmup completion, all outputs are NaN.

### Derivation Notes

The classic pivot formula is not derived from statistical theory. It is an empirical heuristic that became standardized through widespread adoption. The extension to R4/R5 and S4/S5 follows the same arithmetic progression pattern already established by R3/S3. No new constants or empirical fitting are introduced.

R3 and S3 use a coefficient of 2 on the asymmetric terms. R4/S4 use 3. R5/S5 use 4. The progression could continue indefinitely, but levels beyond R5/S5 are rarely referenced in practice.

All R/S level computations use `Math.FusedMultiplyAdd` for the `multiplier * offset + base` pattern, providing single-rounding precision.

## Performance Profile

### Operation Count (Streaming Mode)

Extended Pivot Points adds R4/S4 levels beyond Classic — O(1) with 4 support/resistance pairs.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Store prev HLC | 3 | 1 cy | ~3 cy |
| PP = (H + L + C) / 3 | 1 | 2 cy | ~2 cy |
| R1..R4 arithmetic + S1..S4 | 8 | 2 cy | ~16 cy |
| NaN guard + state update | 1 | 2 cy | ~2 cy |
| **Total** | **O(1)** | — | **~23 cy** |

O(1) pure arithmetic. Extended variant generates 4 pairs vs Classic 3 pairs, adding ~4 cy. All levels SIMD-parallel in batch mode.

### Implementation Design

Pure arithmetic with no loops, no buffers, no auxiliary data structures. Each `Update` call performs 1 division (PP), 1 subtraction (range), 2 subtractions (ppMinusL, hMinusPP), 2 additions (R2, S2), and 8 FMA operations (R1, S1, R3-R5, S3-S5), plus 3 comparisons for NaN validation.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Complexity** | O(1) | Fixed arithmetic; no iteration |
| **Allocations** | 0 | Hot path is allocation-free |
| **Warmup** | 2 bars | Minimum possible |
| **Accuracy** | 10/10 | Exact arithmetic via FMA; no approximation |
| **Timeliness** | 10/10 | No lag; levels available immediately on new bar |
| **Smoothness** | N/A | Discrete levels; smooth/noisy not applicable |

### State Management

Internal state uses a `record struct` with local copy pattern for JIT struct promotion. The state tracks previous bar's HLC and last-valid values for NaN/Infinity input substitution. Bar correction via `isNew` flag enables same-timestamp rewrites without state corruption.

### SIMD Applicability

Not applicable for streaming (single bar computation). The `BatchAll` span API processes multiple bars but the per-bar computation (13 arithmetic operations) is too simple to benefit from vectorization overhead.

### FMA Usage

The implementation uses `Math.FusedMultiplyAdd` for eight of the eleven R/S level computations. R1 and S1 use `FMA(2, pp, -pL)` and `FMA(2, pp, -pH)`. R3-R5 and S3-S5 use FMA with the precomputed `ppMinusL` and `hMinusPP` intermediate values. R2 and S2 are simple additions/subtractions that do not benefit from FMA.

## Validation

Self-consistency validation confirms all API modes produce identical results:

| Mode | Status | Notes |
| :--- | :--- | :--- |
| **Streaming** (`Update`) | Passed | Bar-by-bar with `isNew` support |
| **Batch** (`Batch(TBarSeries)`) | Passed | PP values match streaming |
| **Span** (`Batch(Span)`) | Passed | PP values match streaming |
| **BatchAll** (`BatchAll(Span)`) | Passed | All 11 levels match streaming |
| **Event** (`Pub` subscription) | Passed | Fires on every update |

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | Passed | All modes self-consistent; level ordering invariant holds |
| **Skender** | N/A | Does not implement extended traditional variant |
| **TA-Lib** | N/A | Not implemented |
| **Tulip** | N/A | Not implemented |
| **Ooples** | N/A | Not validated |

Mathematical correctness is validated by computing expected values from the extended formula for each bar and comparing against the indicator output at precision 10.

## Common Pitfalls

1. **First bar returns NaN.** The indicator needs the previous bar's HLC to compute pivots. The first bar stores HLC but produces no output. This is correct behavior, not a bug. `WarmupPeriod = 2`.

2. **R1-R3/S1-S3 are identical to classic PIVOT.** The extended indicator adds R4/R5/S4/S5 but does not modify the classic levels. If you only need 7 levels, use the `Pivot` class instead to avoid computing unused outputs.

3. **R4/R5 and S4/S5 project far from the current range.** These levels represent 3x and 4x prior-range extensions. For low-volatility instruments, they may be so distant as to be meaningless. For high-volatility instruments or gap scenarios, they provide the only pre-computed reference levels.

4. **Level spacing is asymmetric when close is not at the range midpoint.** When the close is near the high, resistance levels are spaced more tightly than support levels, and vice versa. This is by design: the formula reflects where the close sits within the prior range.

5. **Zero-range bars collapse all levels to a single price.** When $H_{prev} = L_{prev}$ (doji or single-print bar), all eleven levels equal the prior close, and PP equals the prior close. This is mathematically correct but provides no useful levels.

6. **TValue input collapses range to zero.** When updating with `TValue` instead of `TBar`, all OHLC fields equal the single price, producing zero range and all levels equal to that price. Use `TBar` input for meaningful pivot calculations.

7. **R5/S5 are rarely reached.** In typical market conditions, price reaching R5 or S5 represents approximately a 4x prior-range move. This occurs during panic selling, short squeezes, or major news events. Do not expect these levels to act as regular support/resistance.

## References

- Person, J. L. (2004). *A Complete Guide to Technical Trading Tactics: How to Profit Using Pivot Points, Candlesticks & Other Indicators*. John Wiley and Sons.
- Wikipedia: [Pivot point (technical analysis)](https://en.wikipedia.org/wiki/Pivot_point_(technical_analysis))
- TradingView: [Pivot Points Standard](https://www.tradingview.com/support/solutions/43000521824-pivot-points-standard/)
- Nison, S. (2001). *Japanese Candlestick Charting Techniques*. Prentice Hall Press. (Discussion of floor trader pivot methodology.)
