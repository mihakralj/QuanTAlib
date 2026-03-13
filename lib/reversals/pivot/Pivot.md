# PIVOT: Classic Pivot Points (Floor Trader Pivots)

> *The floor traders had it figured out before the quants arrived. Three numbers from yesterday's bar, seven levels for today. No optimization, no curve fitting, no excuses.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Reversal                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | None                      |
| **Outputs**      | Single series (PIVOT)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `2` bars                          |
| **PineScript**   | [pivot.pine](pivot.pine)                       |

- Classic Pivot Points calculate seven horizontal support and resistance levels from the previous bar's high, low, and close.
- No configurable parameters; computation is stateless per bar.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

Classic Pivot Points calculate seven horizontal support and resistance levels from the previous bar's high, low, and close. The central pivot point (PP) is the arithmetic mean of HLC; three resistance levels (R1-R3) and three support levels (S1-S3) are derived from PP and the prior bar's range. The formula has been in continuous use since the 1930s among floor traders at commodity exchanges. Zero parameters, zero lag, zero ambiguity.

## Historical Context

Floor traders at the Chicago Board of Trade developed pivot points as a pre-session planning tool. Before electronic markets, traders needed levels they could calculate by hand during the commute to work. The HLC average was the simplest possible summary of the prior session; the support and resistance levels followed from elementary arithmetic on the range.

The method spread through oral tradition among pit traders for decades before appearing in print. Neil Weintraub documented the technique in *Tricks of the Floor Trader* (1996), and John Person expanded on it in *A Complete Guide to Technical Trading Tactics* (2004). By then, pivot points were already embedded in virtually every trading terminal.

The beauty of the formula is its universality. Unlike moving averages (which require choosing a period), Bollinger Bands (which require choosing a standard deviation multiplier), or Fibonacci retracements (which require choosing swing points), pivot points have no parameters. Every trader using the same prior bar's HLC computes identical levels. This made them natural Schelling points: self-fulfilling prophecies where enough participants watched the same numbers to create genuine support and resistance.

Several variants emerged over the decades: Woodie (weighting close double), Camarilla (using range fractions), DeMark (conditional on open-close relationship), and Fibonacci (applying golden ratios to range). This implementation covers the original "Standard" or "Floor Trader" formulation only. The variants differ in the derivation of R/S levels but share the core concept of previous-bar HLC as input.

## Architecture and Physics

### 1. Previous Bar's HLC

The indicator stores the high ($H$), low ($L$), and close ($C$) of the most recently completed bar. On each new bar, these stored values become the basis for computing the current bar's pivot levels, and the new bar's HLC replaces the stored values for the next computation.

### 2. Central Pivot Point (PP)

$$PP = \frac{H_{prev} + L_{prev} + C_{prev}}{3}$$

The arithmetic mean of the previous bar's high, low, and close. This represents the "fair value" or equilibrium price implied by the prior period's trading range.

### 3. Support and Resistance Levels

First-level support and resistance reflect the previous range from PP:

$$R_1 = 2 \cdot PP - L_{prev}$$

$$S_1 = 2 \cdot PP - H_{prev}$$

Second-level support and resistance add the full range:

$$R_2 = PP + (H_{prev} - L_{prev})$$

$$S_2 = PP - (H_{prev} - L_{prev})$$

Third-level support and resistance extend from the extremes:

$$R_3 = H_{prev} + 2 \cdot (PP - L_{prev})$$

$$S_3 = L_{prev} - 2 \cdot (H_{prev} - PP)$$

### 4. Level Ordering Invariant

For any bar where $H_{prev} > L_{prev}$ (non-degenerate range):

$$S_3 < S_2 < S_1 < PP < R_1 < R_2 < R_3$$

When $H_{prev} = L_{prev}$ (zero range), all seven levels collapse to a single value equal to the close.

### 5. Seven Outputs

All seven levels are computed simultaneously and remain constant until a new bar arrives. The primary output (`Last.Val`) returns PP; individual properties expose all seven levels.

### Signal Interpretation

| Condition | Interpretation |
| :--- | :--- |
| Price above PP | Bullish bias for current bar |
| Price below PP | Bearish bias for current bar |
| Price tests R1 | First resistance; potential reversal or breakout level |
| Price tests S1 | First support; potential bounce or breakdown level |
| Price reaches R3/S3 | Extended move; third-level tests rare, indicate strong momentum |
| All levels cluster tightly | Low volatility prior bar; expect range expansion |
| Wide level spacing | High volatility prior bar; wider intraday range expected |

## Mathematical Foundation

### Parameters

Classic Pivot Points has no configurable parameters. The formula is fixed by definition.

| Parameter | Value | Notes |
| :--- | :---: | :--- |
| Inputs | H, L, C | Previous bar's high, low, close |
| Outputs | 7 | PP, R1, R2, R3, S1, S2, S3 |
| Parameters | 0 | No tuning required |

### Warmup Period

$$W = 2$$

The indicator requires 2 bars: the first bar provides HLC for storage; the second bar triggers computation from the stored values. Prior to warmup completion, all outputs are NaN.

### Derivation Notes

The R1/S1 formulas can be rewritten to show the geometric relationship:

$$R_1 = PP + (PP - L_{prev}) \quad \text{(PP reflected above its distance to the low)}$$

$$S_1 = PP - (H_{prev} - PP) \quad \text{(PP reflected below its distance to the high)}$$

R2/S2 add the full range to/from PP. R3/S3 extend beyond the previous extremes by the distance from PP to the opposite extreme.

## Performance Profile

### Operation Count (Streaming Mode)

Classic Pivot Points compute PP and 6 support/resistance levels from previous bar HLC — O(1).

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Store prev HLC | 3 | 1 cy | ~3 cy |
| PP = (H + L + C) / 3 | 1 | 2 cy | ~2 cy |
| R1 = 2*PP - L | 1 | 2 cy | ~2 cy |
| S1 = 2*PP - H | 1 | 2 cy | ~2 cy |
| R2 = PP + (H - L) | 1 | 2 cy | ~2 cy |
| S2 = PP - (H - L) | 1 | 2 cy | ~2 cy |
| R3/S3 extensions | 2 | 2 cy | ~4 cy |
| NaN guard + state update | 1 | 2 cy | ~2 cy |
| **Total** | **O(1)** | — | **~19 cy** |

Cheapest O(1) pivot variant — pure previous-bar arithmetic, no smoothing, no buffers beyond a 1-bar state.

### Implementation Design

Pure arithmetic with no loops, no buffers, no auxiliary data structures. Each `Update` call performs 3 divisions (via the single division in PP), 6 multiplications/additions, and 3 comparisons for NaN validation.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Complexity** | O(1) | Fixed arithmetic; no iteration |
| **Allocations** | 0 | Hot path is allocation-free |
| **Warmup** | 2 bars | Minimum possible |
| **Accuracy** | 10/10 | Exact arithmetic; no approximation |
| **Timeliness** | 10/10 | No lag; levels available immediately on new bar |
| **Smoothness** | N/A | Discrete levels; smooth/noisy not applicable |

### State Management

Internal state uses a `record struct` with local copy pattern for JIT struct promotion. The state tracks previous bar's HLC and last-valid values for NaN/Infinity input substitution. Bar correction via `isNew` flag enables same-timestamp rewrites without state corruption.

### SIMD Applicability

Not applicable for streaming (single bar computation). The `BatchAll` span API processes multiple bars but the per-bar computation is too simple (7 arithmetic operations) to benefit from vectorization overhead. The `Batch` span API for PP-only output could theoretically use SIMD but the division-heavy computation and small operation count make the benefit negligible.

### FMA Usage

The implementation uses `Math.FusedMultiplyAdd` for R1, S1, R3, and S3 computations, providing both a minor precision benefit (single rounding instead of two) and potential performance benefit on hardware with FMA support.

## Validation

Self-consistency validation confirms all API modes produce identical results:

| Mode | Status | Notes |
| :--- | :--- | :--- |
| **Streaming** (`Update`) | Passed | Bar-by-bar with `isNew` support |
| **Batch** (`Batch(TBarSeries)`) | Passed | PP values match streaming |
| **Span** (`Batch(Span)`) | Passed | PP values match streaming |
| **BatchAll** (`BatchAll(Span)`) | Passed | All 7 levels match streaming |
| **Event** (`Pub` subscription) | Passed | Fires on every update |

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | Passed | All modes self-consistent; level ordering invariant holds |
| **Skender** | N/A | Uses calendar-window periods (Day/Week/Month); conceptually different |
| **TA-Lib** | N/A | Not implemented |
| **Tulip** | N/A | Not implemented |
| **Ooples** | N/A | Not validated |

Skender.Stock.Indicators provides `ToPivotPoints()` which computes pivot levels over calendar windows (daily, weekly, monthly). This is a fundamentally different granularity from our bar-to-bar implementation. Skender summarizes an entire period's HLC into one set of pivots for the next period; our implementation uses each individual bar's HLC for the subsequent bar's levels. Both approaches are valid floor trader pivot calculations at different time scales. Direct numerical comparison is not meaningful.

Mathematical correctness is validated by computing expected values from the formula for each bar and comparing against the indicator output at precision 10.

## Common Pitfalls

1. **First bar returns NaN.** The indicator needs the previous bar's HLC to compute pivots. The first bar stores HLC but produces no output. This is correct behavior, not a bug. `WarmupPeriod = 2`.

2. **Levels are constant within a bar.** Pivot levels do not change as the current bar's price moves. They change only when a new bar starts (providing new "previous" HLC). Multiple `isNew=false` corrections on the current bar do not alter the pivot levels because they are derived from the already-stored previous bar.

3. **Skender comparison is not applicable.** Skender.Stock.Indicators uses calendar-period windows (Day/Week/Month). Our implementation is bar-to-bar. Comparing numbers directly will produce mismatches that are not errors.

4. **Zero-range bars collapse all levels.** When $H_{prev} = L_{prev}$ (a doji or single-print bar), all seven levels equal the close. This is mathematically correct but may surprise users expecting spread levels.

5. **PP is not the midpoint of High and Low.** PP includes the close, weighting it equally with high and low. For bars where close is near the high, PP shifts upward; near the low, PP shifts downward. This is intentional and reflects the market's closing sentiment.

6. **TValue input uses price as all four OHLC fields.** When updating with `TValue` instead of `TBar`, the single price value is used for open, high, low, and close. This means $range = 0$ and all levels collapse to the price. Use `TBar` input for meaningful pivot calculations.

7. **NaN/Infinity inputs use last-valid substitution.** If any of H, L, C is NaN or Infinity, the last valid value for that field is substituted. This prevents NaN propagation but may produce stale levels. Monitor data quality upstream.

## References

- Weintraub, N. (1996). *Tricks of the Floor Trader*. McGraw-Hill.
- Person, J. L. (2004). *A Complete Guide to Technical Trading Tactics: How to Profit Using Pivot Points, Candlesticks & Other Indicators*. John Wiley and Sons.
- Wikipedia: [Pivot point (technical analysis)](https://en.wikipedia.org/wiki/Pivot_point_(technical_analysis))
- TradingView PineScript Reference: [`ta.pivothigh()`](https://www.tradingview.com/pine-script-reference/v5/#fun_ta.pivothigh), [`ta.pivotlow()`](https://www.tradingview.com/pine-script-reference/v5/#fun_ta.pivotlow)