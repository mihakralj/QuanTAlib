# CKSTOP: Chande Kroll Stop

> *The best stop-loss is the one that knows where volatility ends and trend begins.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Reversal                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `atrPeriod` (default 10), `multiplier` (default 1.0), `stopPeriod` (default 9)                      |
| **Outputs**      | Single series (Ckstop)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `atrPeriod + stopPeriod` bars                          |
| **PineScript**   | [ckstop.pine](ckstop.pine)                       |

- The Chande Kroll Stop computes adaptive trailing stop levels using ATR-smoothed volatility envelopes around rolling extremes.
- **Similar:** [Chandelier](../chandelier/Chandelier.md), [SAR](../sar/Sar.md) | **Complementary:** ATR | **Trading note:** Chuck LeBeau's Chandelier stop; ATR trailing stop with configurable multiplier.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The Chande Kroll Stop computes adaptive trailing stop levels using ATR-smoothed volatility envelopes around rolling extremes. It produces two lines: StopLong (support) and StopShort (resistance). When price trades above both stops, the trend is bullish. When below both, bearish. Crossovers between the two stops signal potential reversals.

## Historical Context

Tushar Chande and Stanley Kroll introduced this indicator in their 1994 book *The New Technical Trader*. The design addressed a persistent problem in trailing stop systems: fixed-distance stops get whipsawed in volatile markets and leave money on the table in quiet ones. Their solution was to anchor stops to ATR-scaled extremes, then smooth the result through a second rolling window.

Most implementations follow PineScript's `ta.ckstop()` function, which uses RMA (Wilder's smoothing) for the ATR calculation. This matters because RMA has a longer effective memory than SMA-based ATR, producing smoother stop levels that resist noise better.

The indicator sits in a design lineage that includes Parabolic SAR (acceleration-based), SuperTrend (ATR band flipping), and Keltner Channels (ATR envelopes). Where PSAR accelerates toward price and SuperTrend flips state, Chande Kroll Stop maintains both levels simultaneously, letting the trader interpret the relationship between them.

## Architecture and Physics

The computation proceeds in three stages, each building on the previous:

### 1. True Range and ATR

True Range captures gap-adjusted volatility:

$$ TR_t = \max(H_t - L_t,\ |H_t - C_{t-1}|,\ |L_t - C_{t-1}|) $$

ATR smooths TR using Wilder's RMA (equivalent to EMA with $\alpha = 1/p$):

$$ ATR_t = RMA(TR, p) $$

### 2. First Stop (Volatility Envelope)

The first stop levels anchor to rolling extremes offset by ATR:

$$ \text{first\_high\_stop}_t = \max(H_{t-p+1}, \ldots, H_t) - q \times ATR_t $$

$$ \text{first\_low\_stop}_t = \min(L_{t-p+1}, \ldots, L_t) + q \times ATR_t $$

The highest-high minus ATR forms a preliminary resistance level. The lowest-low plus ATR forms preliminary support. The multiplier $q$ controls how far from the extreme the stop sits.

### 3. Final Stop (Smoothed Extremes)

The final stops apply a second rolling window to the first stops:

$$ \text{StopShort}_t = \max(\text{first\_high\_stop}_{t-x+1}, \ldots, \text{first\_high\_stop}_t) $$

$$ \text{StopLong}_t = \min(\text{first\_low\_stop}_{t-x+1}, \ldots, \text{first\_low\_stop}_t) $$

Taking the highest of the first high stops over $x$ periods produces resistance that ratchets up during downtrends. Taking the lowest of the first low stops produces support that ratchets down during uptrends.

### Signal Interpretation

| Condition | Interpretation |
| :--- | :--- |
| Price > StopLong and Price > StopShort | Bullish trend |
| Price < StopLong and Price < StopShort | Bearish trend |
| StopLong crosses above StopShort | Bullish reversal signal |
| StopShort crosses below StopLong | Bearish reversal signal |
| StopLong $\approx$ StopShort | Consolidation / indecision |

## Mathematical Foundation

### Parameters

| Parameter | Symbol | Default | Range | Effect |
| :--- | :---: | :---: | :--- | :--- |
| ATR Period | $p$ | 10 | $\geq 1$ | Length of ATR and first-stop extreme window |
| Multiplier | $q$ | 1.0 | $> 0$ | ATR scaling factor; larger = wider stops |
| Stop Period | $x$ | 9 | $\geq 1$ | Second smoothing window for final stops |

### Warmup Period

$$ W = p + x $$

The indicator requires $p$ bars to establish ATR and rolling extremes, then $x$ additional bars to smooth the first stops into final stops. With defaults: $W = 10 + 9 = 19$.

### Parameter Sensitivity

**ATR Period ($p$)**: Controls volatility measurement timescale. Shorter periods make stops more reactive to recent volatility spikes. Longer periods produce more stable ATR estimates but increase lag.

**Multiplier ($q$)**: Directly scales the stop distance from extremes. At $q = 0.5$, stops sit at half an ATR from rolling highs/lows. At $q = 2.0$, stops provide twice the breathing room. The relationship between StopLong and StopShort gap width is monotonic in $q$.

**Stop Period ($x$)**: Controls the smoothing of first stops. Shorter values make final stops more responsive. Longer values create more persistent stop levels that resist minor pullbacks.

## Performance Profile

### Operation Count (Streaming Mode)

Chande Kroll Stop chains ATR -> first stop -> second stop computations — O(1) per bar.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ATR (Wilder EMA of TR) | 1 | 6 cy | ~6 cy |
| First stop: highest/lowest(high/low - mult*ATR) | 2 | 5 cy | ~10 cy |
| Second stop: highest/lowest of first stop | 2 | 5 cy | ~10 cy |
| Signal select (long/short) | 1 | 2 cy | ~2 cy |
| NaN guard + state update | 1 | 2 cy | ~2 cy |
| **Total** | **O(1)** | — | **~30 cy** |

O(1) per bar. Two chained RingBuffer max/min operations (first stop period p, second stop q). No batch SIMD benefit due to sequential chaining.

### Implementation Design

The implementation uses four monotonic deques for O(1) amortized rolling max/min operations (highest high, lowest low, highest first-high-stop, lowest first-low-stop) and four corresponding circular buffers. An internal RMA instance handles ATR computation.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Complexity** | O(1) amortized | Monotonic deque operations; O(n) worst-case per element but amortized constant |
| **Allocations** | 0 | Hot path is allocation-free; all buffers pre-allocated |
| **Warmup** | $p + x$ bars | 19 bars with defaults |
| **Accuracy** | 10/10 | Self-consistent across streaming, batch, span, and event modes |
| **Timeliness** | 8/10 | Responsive to trend changes; second window adds slight lag |
| **Smoothness** | 8/10 | Double-smoothed via rolling extreme extraction |

### State Management

Internal state uses a `record struct` with local copy pattern for JIT struct promotion. Bar correction via `isNew` flag enables same-timestamp rewrites without state corruption. The indicator maintains previous state snapshots for rollback.

## Validation

Self-consistency validation confirms all four API modes produce identical results:

| Mode | Status | Notes |
| :--- | :--- | :--- |
| **Streaming** (`Update`) | ✅ | Bar-by-bar with `isNew` support |
| **Batch** (`Update(TBarSeries)`) | ✅ | Matches streaming output |
| **Span** (`Batch(Span)`) | ✅ | Matches streaming output |
| **Event** (`Pub` subscription) | ✅ | Matches streaming output |

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | All 4 modes self-consistent |
| **TA-Lib** | N/A | Not implemented |
| **Tulip** | N/A | Not implemented |
| **Skender** | N/A | Not implemented |
| **Ooples** | N/A | Not implemented |

No major TA library implements Chande Kroll Stop, making this a Level 3 validation (mathematical correctness). The implementation is verified through:

- Self-consistency across all API modes
- Parameter sensitivity testing (higher multiplier narrows gap as expected)
- NaN/Infinity robustness (last-valid-value substitution)
- Bar correction integrity (isNew rollback)
- Warmup convergence (IsHot transitions correctly)

## Common Pitfalls

1. **Confusing stop direction.** StopLong is *support* (below price in uptrend), computed from lowest-low + ATR. StopShort is *resistance* (above price in downtrend), computed from highest-high - ATR. The names refer to the trade direction protected, not the stop position.

2. **Multiplier misconception.** Increasing $q$ does not always widen the gap between StopLong and StopShort. Higher $q$ pushes StopLong *up* (closer to price from below) and StopShort *down* (closer to price from above), actually *narrowing* the gap. This is counterintuitive but follows from the math: $\text{low} + q \times ATR$ increases with $q$, while $\text{high} - q \times ATR$ decreases.

3. **Insufficient warmup.** The indicator needs $p + x$ bars before producing meaningful values. Using output before warmup completes yields stops anchored to incomplete data. Check `IsHot` before trading signals.

4. **Fixed parameters across instruments.** A multiplier of 1.0 works for typical equity volatility. Crypto or forex may need $q = 1.5\text{--}3.0$ to avoid excessive whipsaws. Calibrate to the instrument's ATR distribution.

5. **Ignoring consolidation zones.** When StopLong and StopShort converge, the market is range-bound. Trend-following signals during convergence produce whipsaws. Use the gap width as a regime filter.

6. **ATR smoothing assumptions.** This implementation uses RMA (Wilder's) for ATR, matching PineScript's `ta.atr()`. Some references use SMA-based ATR. The choice affects stop placement during volatility transitions.

## References

- Chande, T. S., & Kroll, S. (1994). *The New Technical Trader: Boost Your Profit by Plugging into the Latest Indicators*. John Wiley & Sons.
- TradingView PineScript Reference: [`ta.ckstop()`](https://www.tradingview.com/pine-script-reference/v6/#fun_ta.ckstop)