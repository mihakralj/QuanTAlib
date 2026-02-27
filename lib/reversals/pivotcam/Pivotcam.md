# PIVOTCAM: Camarilla Pivot Points

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Reversal                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | None                      |
| **Outputs**      | Single series (PIVOTCAM)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `2` bars                          |

### TL;DR

- Camarilla Pivot Points calculate nine horizontal support and resistance levels from the previous bar's high, low, and close.
- No configurable parameters; computation is stateless per bar.
- Output range: Varies (see docs).
- Requires `2` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "The Camarilla trader does not care where the market opens. The trader cares how far price strays from yesterday's close, and whether it returns."

Camarilla Pivot Points calculate nine horizontal support and resistance levels from the previous bar's high, low, and close. Unlike classic floor trader pivots that radiate from the PP midpoint, Camarilla levels radiate symmetrically from the previous close using fixed fractions of the prior range. The R3/S3 levels serve as the primary mean-reversion zone; breakouts beyond R4/S4 signal trend continuation. Developed by Nick Scott in 1989 using bond market data, the equation was originally distributed as a shareware Excel plugin.

## Historical Context

Nick Scott developed the Camarilla Equation in 1989 while trading bonds. The name references the "Camarilla" (a group of secret advisors), reflecting Scott's belief that institutional traders used similar range-fraction calculations internally. The formula was originally sold as a $50 Excel plug-in, one of the earliest examples of retail algorithmic trading tools.

The key insight behind Camarilla differs from classic pivots in a fundamental way. Classic pivots treat the prior bar's PP (mean of HLC) as the center of gravity. Camarilla treats the prior close as the center, reasoning that the close represents the market's final consensus. Support and resistance levels are then computed as fixed fractions of the prior range added to or subtracted from the close.

The specific multiplier constants (1.0833/12, 1.1666/12, 1.25/12, 1.5/12) were derived empirically from bond market data. They produce levels that are tighter than classic pivots, making them more suited to mean-reversion strategies. The R3/S3 levels correspond roughly to the boundaries where intraday price tends to reverse; R4/S4 mark breakout thresholds.

Classic pivot variants (Woodie, DeMark, Fibonacci) all derive levels from the PP center. Camarilla stands alone in using the close as the anchor point, which makes it inherently different from all other pivot formulations. This close-centric design means Camarilla levels shift when the close changes even if the range stays constant, while classic pivots shift when the range midpoint changes.

## Architecture and Physics

### 1. Previous Bar's HLC

The indicator stores the high ($H$), low ($L$), and close ($C$) of the most recently completed bar. On each new bar, these stored values become the basis for computing the current bar's pivot levels, and the new bar's HLC replaces them for the next computation.

### 2. Central Pivot Point (PP)

$$PP = \frac{H_{prev} + L_{prev} + C_{prev}}{3}$$

The arithmetic mean of the previous bar's HLC. Identical to classic pivot PP. Included for reference and compatibility, though Camarilla levels do not derive from PP.

### 3. Range

$$range = H_{prev} - L_{prev}$$

The previous bar's trading range, used as the scaling factor for all support and resistance levels.

### 4. Camarilla Multiplier Constants

| Level | Numerator | Divisor | Effective Multiplier |
| :--- | :---: | :---: | :---: |
| R1 / S1 | 1.0833 | 12 | 0.090275 |
| R2 / S2 | 1.1666 | 12 | 0.097217 |
| R3 / S3 | 1.2500 | 12 | 0.104167 |
| R4 / S4 | 1.5000 | 12 | 0.125000 |

### 5. Resistance Levels

$$R_1 = C_{prev} + range \times \frac{1.0833}{12}$$

$$R_2 = C_{prev} + range \times \frac{1.1666}{12}$$

$$R_3 = C_{prev} + range \times \frac{1.2500}{12}$$

$$R_4 = C_{prev} + range \times \frac{1.5000}{12}$$

### 6. Support Levels

$$S_1 = C_{prev} - range \times \frac{1.0833}{12}$$

$$S_2 = C_{prev} - range \times \frac{1.1666}{12}$$

$$S_3 = C_{prev} - range \times \frac{1.2500}{12}$$

$$S_4 = C_{prev} - range \times \frac{1.5000}{12}$$

### 7. Level Ordering Invariant

For any bar where $H_{prev} > L_{prev}$ (non-degenerate range):

$$S_4 < S_3 < S_2 < S_1 < C_{prev} < R_1 < R_2 < R_3 < R_4$$

Note that PP may be above or below $C_{prev}$ depending on whether the close was nearer the high or low. The support and resistance levels are always ordered by their multiplier magnitude.

### 8. Nine Outputs

All nine levels are computed simultaneously and remain constant until a new bar arrives. The primary output (`Last.Val`) returns PP; individual properties expose all nine levels.

### Signal Interpretation

| Condition | Interpretation |
| :--- | :--- |
| Price between S1 and R1 | Normal range; no signal |
| Price tests R3 from below | Mean-reversion short entry zone |
| Price tests S3 from above | Mean-reversion long entry zone |
| Price breaks above R4 | Bullish breakout; trend continuation |
| Price breaks below S4 | Bearish breakdown; trend continuation |
| R3/S3 rejected | High-probability reversal setup |
| Levels cluster tightly | Low volatility prior bar; expect range expansion |

## Mathematical Foundation

### Parameters

Camarilla Pivot Points has no configurable parameters. The formula constants are fixed by definition.

| Parameter | Value | Notes |
| :--- | :---: | :--- |
| Inputs | H, L, C | Previous bar's high, low, close |
| Outputs | 9 | PP, R1, R2, R3, R4, S1, S2, S3, S4 |
| Parameters | 0 | No tuning required |

### Warmup Period

$$W = 2$$

The indicator requires 2 bars: the first bar provides HLC for storage; the second bar triggers computation from the stored values. Prior to warmup completion, all outputs are NaN.

### Derivation Notes

The Camarilla multipliers are not derived from mathematical first principles. They are empirical constants fitted to bond market data by Nick Scott. The progression (1.0833, 1.1666, 1.25, 1.5) divided by 12 creates four concentric bands around the close. The spacing between levels is not uniform: the gap between R3/S3 and R4/S4 is wider than between R1/S1 and R2/S2, creating a natural "breakout zone" at the extremes.

All levels use `Math.FusedMultiplyAdd` for the `close + range * constant` computation, providing single-rounding precision.

## Performance Profile

### Operation Count (Streaming Mode)

Camarilla Pivot uses a fixed multiplier series (1.1/12, 1.1/6, ...) applied to previous-bar range — O(1).

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Store prev OHLC | 4 | 1 cy | ~4 cy |
| Range = H - L | 1 | 1 cy | ~1 cy |
| R1..R4 via FMA (C + k*range) | 4 | 1 cy | ~4 cy |
| S1..S4 via FMA (C - k*range) | 4 | 1 cy | ~4 cy |
| NaN guard + state update | 1 | 2 cy | ~2 cy |
| **Total** | **O(1)** | — | **~15 cy** |

O(1) pure arithmetic. Precomputed Camarilla multipliers [1.1/12, 1.1/6, 1.1/4, 1.1/2] applied via FMA(C, 1, k*range).

### Implementation Design

Pure arithmetic with no loops, no buffers, no auxiliary data structures. Each `Update` call performs 1 division (PP), 8 FMA operations, and 3 comparisons for NaN validation.

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

Not applicable for streaming (single bar computation). The `BatchAll` span API processes multiple bars but the per-bar computation is too simple (9 arithmetic operations) to benefit from vectorization overhead.

### FMA Usage

The implementation uses `Math.FusedMultiplyAdd` for all eight R/S level computations (R1-R4, S1-S4), providing both precision benefit (single rounding instead of two) and potential performance benefit on hardware with FMA support.

## Validation

Self-consistency validation confirms all API modes produce identical results:

| Mode | Status | Notes |
| :--- | :--- | :--- |
| **Streaming** (`Update`) | Passed | Bar-by-bar with `isNew` support |
| **Batch** (`Batch(TBarSeries)`) | Passed | PP values match streaming |
| **Span** (`Batch(Span)`) | Passed | PP values match streaming |
| **BatchAll** (`BatchAll(Span)`) | Passed | All 9 levels match streaming |
| **Event** (`Pub` subscription) | Passed | Fires on every update |

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | Passed | All modes self-consistent; level ordering invariant holds |
| **Skender** | N/A | Does not implement Camarilla variant |
| **TA-Lib** | N/A | Not implemented |
| **Tulip** | N/A | Not implemented |
| **Ooples** | N/A | Not validated |

Mathematical correctness is validated by computing expected values from the Camarilla formula for each bar and comparing against the indicator output.

## Common Pitfalls

1. **First bar returns NaN.** The indicator needs the previous bar's HLC to compute pivots. The first bar stores HLC but produces no output. This is correct behavior, not a bug. `WarmupPeriod = 2`.

2. **Levels radiate from close, not PP.** Unlike classic pivots where R/S levels are derived from PP, Camarilla levels are offsets from the previous close. PP is provided for reference only. Do not expect R1 = f(PP) as in classic pivots.

3. **R3/S3 are the primary trading levels.** The Camarilla system treats R3/S3 as mean-reversion entry zones and R4/S4 as breakout confirmation. R1/S1 and R2/S2 are intermediate levels with less trading significance in the original system.

4. **Zero-range bars collapse all levels to the close.** When $H_{prev} = L_{prev}$ (doji or single-print bar), all eight R/S levels equal the close, and PP equals the close. This is mathematically correct.

5. **PP may be above R1 or below S1.** Because R/S levels radiate from the close but PP is based on HLC/3, unusual close positions can cause PP to fall outside the S1-R1 range. This is not a bug; it reflects the different anchoring of classic PP vs. Camarilla levels.

6. **TValue input collapses range to zero.** When updating with `TValue` instead of `TBar`, all OHLC fields equal the single price, producing zero range and all levels equal to that price. Use `TBar` input for meaningful pivot calculations.

7. **Multiplier constants are empirical, not mathematical.** The 1.0833/12, 1.1666/12, 1.25/12, 1.5/12 values are fitted constants from bond market data. They have no derivation from probability theory or signal processing. Their effectiveness depends on market microstructure alignment.

## References

- Scott, N. (1989). *The Camarilla Equation*. Originally distributed as Excel shareware.
- Person, J. L. (2004). *A Complete Guide to Technical Trading Tactics: How to Profit Using Pivot Points, Candlesticks & Other Indicators*. John Wiley and Sons.
- Wikipedia: [Pivot point (technical analysis)](https://en.wikipedia.org/wiki/Pivot_point_(technical_analysis))
- TradingView: [Camarilla Pivot Points](https://www.tradingview.com/support/solutions/43000521824-pivot-points-standard/)
