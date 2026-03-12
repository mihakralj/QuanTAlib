# PIVOTDEM: DeMark Pivot Points

> *Most pivot formulas treat every bar the same. DeMark looked at the open-close relationship and asked: why would a bearish bar predict the same levels as a bullish one?*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Reversal                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | None                      |
| **Outputs**      | Single series (PIVOTDEM)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `2` bars                          |
| **PineScript**   | [pivotdem.pine](pivotdem.pine)                       |

- DeMark Pivot Points calculate three horizontal support and resistance levels from the previous bar's open, high, low, and close.
- No configurable parameters; computation is stateless per bar.
- Output range: Varies (see docs).
- Requires `2` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

DeMark Pivot Points calculate three horizontal support and resistance levels from the previous bar's open, high, low, and close. The defining characteristic is a conditional intermediate value X that changes its weighting depending on whether the prior bar closed below, above, or equal to its open. Bearish bars weight the low; bullish bars weight the high; doji bars weight the close. Three levels (PP, R1, S1) emerge from this single conditional calculation. The only pivot variant that uses the open price.

## Historical Context

Tom DeMark introduced his pivot point variant as part of a broader system of conditional indicators published in *The New Science of Technical Analysis* (1994) and *New Market Timing Techniques* (1997). Where floor trader pivots summarize the prior bar with an equal-weight HLC average, DeMark argued that the relationship between open and close carries directional information that should influence the levels.

The logic is straightforward: if the bar closed below the open (bearish), the low was more "tested" and should carry more weight. If the bar closed above the open (bullish), the high was more relevant. If open equals close (a doji), the close itself — representing the equilibrium point where neither bulls nor bears won — gets the extra weight.

This conditional approach differs from all other pivot variants (Classic, Woodie, Camarilla, Fibonacci) which apply the same formula regardless of bar direction. DeMark's innovation was treating the prior bar as a signal, not just a data source.

The tradeoff is minimalism: DeMark produces only 3 levels (PP, R1, S1) compared to the Classic formula's 7 or Camarilla's 9. What you lose in level density you gain in directional sensitivity. The formula adapts to bar structure rather than imposing a fixed geometry.

## Architecture and Physics

### 1. Previous Bar's OHLC

The indicator stores the open ($O$), high ($H$), low ($L$), and close ($C$) of the most recently completed bar. On each new bar, these stored values become the basis for computing the current bar's pivot levels. This is the only pivot variant that requires the open.

### 2. Conditional Intermediate Value X

The core innovation is the conditional calculation of $X$:

$$X = \begin{cases} H_{prev} + 2 \cdot L_{prev} + C_{prev} & \text{if } C_{prev} < O_{prev} \text{ (bearish)} \\ 2 \cdot H_{prev} + L_{prev} + C_{prev} & \text{if } C_{prev} > O_{prev} \text{ (bullish)} \\ H_{prev} + L_{prev} + 2 \cdot C_{prev} & \text{if } C_{prev} = O_{prev} \text{ (doji)} \end{cases}$$

Each case sums four price components but doubles one of them:

- **Bearish bar** doubles the low (the level that absorbed selling pressure)
- **Bullish bar** doubles the high (the level that absorbed buying pressure)
- **Doji bar** doubles the close (the neutral equilibrium)

### 3. Pivot Levels

From the intermediate value $X$:

$$PP = \frac{X}{4}$$

$$R_1 = \frac{X}{2} - L_{prev}$$

$$S_1 = \frac{X}{2} - H_{prev}$$

### 4. Level Ordering Invariant

For any bar where $H_{prev} > L_{prev}$ (non-degenerate range):

$$S_1 < PP < R_1$$

This holds regardless of the bar direction, since $R_1 - PP = PP - S_1 = \frac{H_{prev} - L_{prev}}{4}$ is always positive for non-zero range. The range between R1 and S1 equals $\frac{H_{prev} - L_{prev}}{2}$, exactly half the prior bar's range.

### 5. Three Outputs

All three levels are computed simultaneously and remain constant until a new bar arrives. The primary output (`Last.Val`) returns PP; individual properties expose PP, R1, and S1.

### Signal Interpretation

| Condition | Interpretation |
| :--- | :--- |
| Price above PP | Bullish bias; prior bar direction influences level placement |
| Price below PP | Bearish bias for current bar |
| Price tests R1 | Resistance; level is higher after bullish prior bar (H weighted) |
| Price tests S1 | Support; level is lower after bearish prior bar (L weighted) |
| Bearish prior bar | Levels shift downward (low weighted), implying defensive positioning |
| Bullish prior bar | Levels shift upward (high weighted), implying aggressive positioning |
| Doji prior bar | Levels center on close (neutral), tightest level spacing |

## Mathematical Foundation

### Parameters

DeMark Pivot Points has no configurable parameters. The conditional formula is fixed by definition.

| Parameter | Value | Notes |
| :--- | :---: | :--- |
| Inputs | O, H, L, C | Previous bar's open, high, low, close |
| Outputs | 3 | PP, R1, S1 |
| Parameters | 0 | No tuning required |

### Warmup Period

$$W = 2$$

The indicator requires 2 bars: the first bar provides OHLC for storage; the second bar triggers computation from the stored values. Prior to warmup completion, all outputs are NaN.

### Derivation Notes

The R1 and S1 formulas can be rewritten to show their relationship to PP:

$$R_1 = PP + \frac{H_{prev} - L_{prev}}{4}$$

$$S_1 = PP - \frac{H_{prev} - L_{prev}}{4}$$

This means R1 and S1 are always equidistant from PP, separated by one-quarter of the prior bar's range on each side. The conditional logic affects where PP itself sits (closer to the low for bearish bars, closer to the high for bullish), but the R1-PP and PP-S1 distances are always identical.

## Performance Profile

### Operation Count (Streaming Mode)

DeMark Pivot uses a conditional pivot formula based on whether Open == Close vs C vs O > C — O(1).

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Store prev OHLC | 4 | 1 cy | ~4 cy |
| Conditional X formula (3-way branch) | 1 | 4 cy | ~4 cy |
| PP = X / 4 | 1 | 2 cy | ~2 cy |
| R1 = X/2 - L, S1 = X/2 - H | 2 | 2 cy | ~4 cy |
| NaN guard + state update | 1 | 2 cy | ~2 cy |
| **Total** | **O(1)** | — | **~16 cy** |

O(1) arithmetic with one 3-way conditional on price relationship. Branch predictor will learn the dominant market regime quickly.

### Implementation Design

Pure arithmetic with no loops, no buffers, no auxiliary data structures. Each `Update` call performs one conditional branch, 3 multiplications, 3 additions/subtractions, and 4 comparisons for NaN validation.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Complexity** | O(1) | Fixed arithmetic; one branch |
| **Allocations** | 0 | Hot path is allocation-free |
| **Warmup** | 2 bars | Minimum possible |
| **Accuracy** | 10/10 | Exact arithmetic; no approximation |
| **Timeliness** | 10/10 | No lag; levels available immediately on new bar |
| **Smoothness** | N/A | Discrete levels; smooth/noisy not applicable |

### State Management

Internal state uses a `record struct` with local copy pattern for JIT struct promotion. The state tracks previous bar's OHLC and last-valid values for NaN/Infinity input substitution. Bar correction via `isNew` flag enables same-timestamp rewrites without state corruption.

### SIMD Applicability

Not applicable for streaming (single bar computation). The `BatchAll` span API could theoretically vectorize but the conditional branch per bar prevents efficient SIMD. The arithmetic is too simple (3 operations after the branch) to justify vectorization overhead.

### FMA Usage

Not used. The per-level computation (`x * 0.5 - L` or `x * 0.25`) involves only one multiply and one subtract, which does not form an `a*b + c` pattern that benefits from FMA.

## Validation

Self-consistency validation confirms all API modes produce identical results:

| Mode | Status | Notes |
| :--- | :--- | :--- |
| **Streaming** (`Update`) | Passed | Bar-by-bar with `isNew` support |
| **Batch** (`Batch(TBarSeries)`) | Passed | PP values match streaming |
| **Span** (`Batch(Span)`) | Passed | PP values match streaming |
| **BatchAll** (`BatchAll(Span)`) | Passed | All 3 levels match streaming |
| **Event** (`Pub` subscription) | Passed | Fires on every update |

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | Passed | All modes self-consistent; level ordering invariant holds |
| **Skender** | N/A | Uses calendar-window periods; conceptually different |
| **TA-Lib** | N/A | Not implemented |
| **Tulip** | N/A | Not implemented |
| **Ooples** | N/A | Not validated |

No external libraries implement bar-to-bar DeMark pivot points. Mathematical correctness is validated by computing expected values from the conditional formula for each bar and comparing against the indicator output at precision 10.

## Common Pitfalls

1. **First bar returns NaN.** The indicator needs the previous bar's OHLC to compute pivots. The first bar stores OHLC but produces no output. This is correct behavior. `WarmupPeriod = 2`.

2. **Open price is required.** Unlike Classic Pivot Points (which use only HLC), DeMark requires the open to determine which branch of the conditional to take. Using `TValue` input (which sets all four OHLC fields to the same price) always triggers the doji branch ($C = O$). Use `TBar` input for meaningful DeMark calculations.

3. **Levels are constant within a bar.** Pivot levels do not change as the current bar's price moves. They change only when a new bar starts. Multiple `isNew=false` corrections on the current bar do not alter the pivot levels.

4. **Only 3 levels, not 7.** DeMark produces PP, R1, and S1 only. There are no R2/R3/S2/S3 levels. If you need more levels, use Classic Pivot Points or Camarilla.

5. **Floating-point equality for doji detection.** The doji branch triggers when `Close == Open` exactly. In practice with real market data, exact equality is rare. The bearish and bullish branches handle the vast majority of bars. The doji branch matters most for synthetic data or instruments with minimum tick sizes that create frequent doji bars.

6. **NaN/Infinity inputs use last-valid substitution.** If any of O, H, L, C is NaN or Infinity, the last valid value for that field is substituted. This prevents NaN propagation but may produce stale levels.

7. **Level spacing is always half the prior range.** R1 minus S1 always equals $(H_{prev} - L_{prev}) / 2$, regardless of bar direction. The conditional logic shifts PP up or down but does not change the R1-S1 width. Low-range prior bars produce tightly clustered levels.

## References

- DeMark, T. R. (1994). *The New Science of Technical Analysis*. John Wiley and Sons.
- DeMark, T. R. (1997). *New Market Timing Techniques: Innovative Studies in Market Rhythm and Price Exhaustion*. John Wiley and Sons.
- Person, J. L. (2004). *A Complete Guide to Technical Trading Tactics*. John Wiley and Sons.
- Wikipedia: [Pivot point (technical analysis)](https://en.wikipedia.org/wiki/Pivot_point_(technical_analysis))
