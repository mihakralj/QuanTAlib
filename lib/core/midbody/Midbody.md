# MIDBODY: Open-Close Average

> *The average of open and close reveals the candle body's center — where intent met execution.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Core                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | None                      |
| **Outputs**      | Single series (Midbody)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `1` bars                          |

Midbody computes the arithmetic mean of Open and Close prices: $(O + C) \times 0.5$. It captures where price started and ended within a bar, ignoring intra-bar extremes. No lookback period, no state, always hot after the first bar. Equivalent to `TBar.OC2`.

## Historical Context

The Open-Close average has no formal attribution in technical analysis literature. Unlike `HL2` (Median Price) or `HLC3` (Typical Price) which appear in TA-Lib and classic references, OC2 exists primarily as a computed property in modern libraries like Skender.Stock.Indicators (`CandlePart.OC2`).

The rationale for OC2 is straightforward: Open and Close represent the consensus prices at session boundaries. High and Low represent transient extremes that may reflect noise or stops being triggered. By averaging only the session endpoints, OC2 filters out intra-bar volatility entirely.

OC2 is useful as an input to trend-following indicators when you want the trend signal to reflect directional bias (where did the bar open and close?) rather than range (how far did it swing?). It also serves as the natural center for Heikin-Ashi calculations (HA Close = OHLC4, but HA state tracking uses the prior bar's OC2).

## Architecture & Physics

### 1. Core Formula

$$\text{Midbody} = (O + C) \times 0.5$$

The multiplication form avoids a division operation. The JIT compiles `* 0.5` to a single `vmulsd` instruction.

### 2. State Management

OC2 is stateless. Each bar's output depends only on that bar's Open and Close values. The `State` record struct tracks only:

- `LastValidOpen` / `LastValidClose` for NaN substitution
- `LastResult` for fallback when both inputs are non-finite
- `Count` for `IsHot` tracking

### 3. Complexity

| Metric | Value |
|--------|-------|
| Time (streaming) | $O(1)$ |
| Time (batch) | $O(n)$ |
| Space | $O(1)$ — no buffers |
| Warmup | 1 bar |

## Mathematical Foundation

### Parameters

None. OC2 is parameterless.

### Weight Distribution

| Component | Weight |
|-----------|--------|
| Open | 0.5 |
| High | 0 |
| Low | 0 |
| Close | 0.5 |

### Comparison with Other Price Transforms

| Transform | Formula | Components Used | Bias |
|-----------|---------|:---------------:|------|
| Midbody | $(O+C) \times 0.5$ | O, C | Session endpoints only |
| MEDPRICE | $(H+L) \times 0.5$ | H, L | Range-centered; ignores O/C |
| TYPPRICE | $(O+H+L) / 3$ | O, H, L | Opening-biased range |
| HLC3 | $(H+L+C) / 3$ | H, L, C | Close-influenced range |
| AVGPRICE | $(O+H+L+C) \times 0.25$ | O, H, L, C | Fully balanced |
| WCLPRICE | $(H+L+2C) \times 0.25$ | H, L, C | Close double-weighted |

### Output Interpretation

- OC2 > Close: bar closed below its midpoint (bearish lean)
- OC2 < Close: bar closed above its midpoint (bullish lean)
- OC2 = Close: Open = Close (doji-like bar)

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count |
|-----------|-------|
| Addition | 1 |
| Multiplication | 1 |
| Comparison | 0 |
| Memory access | 2 (O, C) |
| **Total** | **4 ops** |

### Batch Mode (SIMD Analysis)

The batch loop is a trivial element-wise `(a[i] + b[i]) * 0.5`. Auto-vectorization by the JIT is expected for aligned spans. Manual SIMD is not implemented because the operation is already memory-bandwidth-bound at this simplicity level.

## Validation

| Library | Method | Tolerance | Status |
|---------|--------|-----------|--------|
| Skender | `CandlePart.OC2` | `1e-7` | ✅ Batch + Streaming + Span |
| TA-Lib | N/A | — | Not available |
| TBar.OC2 | Property | `1e-10` | ✅ All bars match |

## Common Pitfalls

1. **Confusing OC2 with MEDPRICE.** MEDPRICE is `(H+L)/2`; OC2 is `(O+C)/2`. They answer different questions: range center vs. session endpoint average.
2. **Confusing OC2 with Midpoint.** Midpoint is `(Highest(V,N) + Lowest(V,N))/2` — a rolling indicator with a period parameter. OC2 has no period.
3. **Using OC2 for volatility estimation.** OC2 deliberately ignores H and L. For volatility-aware price proxies, use HLC3 or OHLC4 instead.
4. **Expecting TA-Lib compatibility.** TA-Lib does not implement OC2. Validation is against Skender only.
5. **Gap analysis with OC2.** When Open and Close are nearly equal (doji bars), OC2 converges to Close. This is correct behavior, not a bug.

## Resources

- **Skender.Stock.Indicators** `CandlePart.OC2` enum documentation.
- **Murphy, J.J.** *Technical Analysis of the Financial Markets*. New York Institute of Finance, 1999.
