# CHANDELIER: Chandelier Exit

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Reversal                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period` (default 22), `multiplier` (default 3.0)                      |
| **Outputs**      | Single series (Chandelier)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period + 1` bars                          |

### TL;DR

- The Chandelier Exit computes ATR-based trailing stop levels that hang from the highest high (for longs) or rise from the lowest low (for shorts) ov...
- Parameterized by `period` (default 22), `multiplier` (default 3.0).
- Output range: Varies (see docs).
- Requires `period + 1` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "The exit is more important than the entry. Everyone knows where to get in; getting out alive is the real trick."

The Chandelier Exit computes ATR-based trailing stop levels that hang from the highest high (for longs) or rise from the lowest low (for shorts) over a lookback period. It produces two overlay lines: ExitLong (trailing stop for long positions) and ExitShort (trailing stop for short positions). Developed by Charles Le Beau and popularized by Alexander Elder. Default parameters: period 22, multiplier 3.0.

## Historical Context

Charles Le Beau introduced the Chandelier Exit concept in the early 1990s, named because it "hangs down from the ceiling" of the market, like a chandelier. The idea was formalized in his work on systematic trading exits and later popularized by Dr. Alexander Elder in *Come Into My Trading Room* (2002).

The design addresses the fundamental problem of fixed-point exits: a $2 stop on a $10 stock (20%) is aggressive, while the same $2 stop on a $200 stock (1%) is pathologically tight. By anchoring exits to ATR, the Chandelier Exit auto-calibrates to each instrument's volatility regime.

The indicator is closely related to SuperTrend (which uses ATR bands around HL2 with state flipping) and Chande Kroll Stop (which adds a second smoothing stage). Where SuperTrend provides a single trend-following line with binary state, Chandelier Exit provides two independent trailing stops without trend state. Where Chande Kroll Stop smooths through a second rolling window, Chandelier Exit provides raw ATR-offset extremes, favoring responsiveness over smoothness.

Most implementations use Wilder's ATR (SMA-seeded RMA), matching the methodology used by Skender.Stock.Indicators and TradingView's reference implementations. This QuanTAlib implementation uses inline SMA-seeded Wilder's smoothing rather than bias-compensated EMA, ensuring exact match with Skender at machine precision.

## Architecture and Physics

The computation proceeds in two stages:

### 1. True Range and ATR

True Range captures gap-adjusted volatility:

$$ TR_t = \max(H_t - L_t,\ |H_t - C_{t-1}|,\ |L_t - C_{t-1}|) $$

ATR uses SMA-seeded Wilder's RMA. The first bar's TR is skipped (set to 0). Bars 2 through $N+1$ accumulate TR into an SMA seed. After seeding:

$$ ATR_t = \frac{ATR_{t-1} \times (N-1) + TR_t}{N} $$

This is equivalent to EMA with $\alpha = 1/N$ but with an explicit SMA seed rather than bias compensation. The two approaches converge asymptotically but differ during warmup and early post-warmup bars.

### 2. Chandelier Exits

Exit levels anchor to rolling extremes offset by scaled ATR:

$$ \text{ExitLong}_t = \max(H_{t-N+1}, \ldots, H_t) - m \times ATR_t $$

$$ \text{ExitShort}_t = \min(L_{t-N+1}, \ldots, L_t) + m \times ATR_t $$

ExitLong trails below the highest high: if price falls below this level, the uptrend may be exhausted. ExitShort trails above the lowest low: if price rises above this level, the downtrend may be reversing.

### Signal Interpretation

| Condition | Interpretation |
| :--- | :--- |
| Price > ExitLong | Uptrend intact; hold long position |
| Price < ExitLong | Potential long exit; uptrend may be over |
| Price < ExitShort | Downtrend intact; hold short position |
| Price > ExitShort | Potential short exit; downtrend may be over |
| ExitLong rising | Strengthening uptrend; new highs being set |
| ExitShort falling | Strengthening downtrend; new lows being set |

## Mathematical Foundation

### Parameters

| Parameter | Symbol | Default | Range | Effect |
| :--- | :---: | :---: | :--- | :--- |
| Period | $N$ | 22 | $\geq 1$ | Lookback for ATR and rolling HH/LL window |
| Multiplier | $m$ | 3.0 | $> 0$ | ATR scaling factor; larger = wider stops |

### Warmup Period

$$ W = N $$

The indicator requires $N$ bars to establish ATR and rolling extremes. With defaults: $W = 22$. However, the SMA-seeded ATR does not produce valid values until bar $N+1$, meaning the first $N$ bars' ATR reads as 0.

### Parameter Sensitivity

**Period ($N$)**: Controls the lookback window for both ATR calculation and highest-high/lowest-low tracking. Shorter periods make exits more reactive (tighter stops) but increase whipsaw risk. Longer periods provide more stable exits but lag trend changes. Common ranges: 14-28 for equities, 7-14 for crypto.

**Multiplier ($m$)**: Directly scales stop distance. At $m = 1.0$, stops sit one ATR from the extreme. At $m = 3.0$ (default), stops allow three ATRs of breathing room. The relationship is linear: doubling $m$ doubles the stop distance from the extreme. Higher multiplier means ExitLong drops lower and ExitShort rises higher, widening the gap between them.

## Performance Profile

### Operation Count (Streaming Mode)

Chandelier Exit uses rolling ATR + highest high / lowest low tracking — O(1) per bar.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| True range computation (3 comparisons) | 3 | 2 cy | ~6 cy |
| EMA-smoothed ATR update (Wilder) | 1 | 3 cy | ~3 cy |
| RingBuffer highest-high update | 1 | 4 cy | ~4 cy |
| RingBuffer lowest-low update | 1 | 4 cy | ~4 cy |
| Long stop = highest - mult*ATR | 1 | 2 cy | ~2 cy |
| Short stop = lowest + mult*ATR | 1 | 2 cy | ~2 cy |
| NaN guard + state update | 1 | 2 cy | ~2 cy |
| **Total** | **O(1)** | — | **~23 cy** |

O(1) per bar. ATR uses Wilder smoothing (RMA). Highest/lowest tracked via O(1) RingBuffer max/min monotonic deque.

### Implementation Design

The implementation uses two monotonic deques for O(1) amortized rolling max/min operations (highest high, lowest low) with corresponding circular buffers. ATR is computed inline using SMA-seeded Wilder's smoothing with FMA optimization, eliminating the need for a child RMA indicator.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Complexity** | O(1) amortized | Monotonic deque operations; O(n) worst-case per element but amortized constant |
| **Allocations** | 0 | Hot path is allocation-free; all buffers pre-allocated |
| **Warmup** | $N$ bars | 22 bars with defaults |
| **Accuracy** | 10/10 | Exact match with Skender at precision 8 |
| **Timeliness** | 7/10 | Default period of 22 introduces moderate lag |
| **Smoothness** | 6/10 | Single-stage ATR offset; no secondary smoothing |

### State Management

Internal state uses a `record struct` with local copy pattern for JIT struct promotion. The state tracks previous close (for TR calculation), ATR accumulator (SumTr), current ATR value, and last-valid substitution values for NaN/Infinity robustness. Bar correction via `isNew` flag enables same-timestamp rewrites without state corruption.

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
| **Skender** | ✅ | Exact match via `GetChandelier()` at precision 8 (both Long and Short) |
| **TA-Lib** | N/A | Not implemented |
| **Tulip** | N/A | Not implemented |
| **Ooples** | N/A | Not implemented |

Cross-validation with Skender.Stock.Indicators confirms exact numerical match for both ExitLong (`ChandelierType.Long`) and ExitShort (`ChandelierType.Short`) using `GetChandelier(22, 3.0)` with tolerance of $10^{-8}$.

## Common Pitfalls

1. **ATR smoothing method matters.** Using bias-compensated EMA (standard `Rma` class) instead of SMA-seeded Wilder RMA produces a systematic ATR offset (~0.5 for typical equity data) that persists indefinitely. This implementation uses inline SMA-seeded ATR to match Skender exactly. If you see persistent 1-2 point differences with reference implementations, check your ATR seeding method.

2. **Multiplier direction is intuitive here (unlike CKSTOP).** Increasing $m$ widens the gap between ExitLong and ExitShort. ExitLong drops lower (more room below HH); ExitShort rises higher (more room above LL). This is the opposite of Chande Kroll Stop, where higher multiplier narrows the gap.

3. **First bar's TR is skipped.** The SMA-seeded Wilder convention treats bar 1's TR as 0 and begins accumulation from bar 2. This is a seeding convention, not a bug. It matches Skender, TradingView, and most standard implementations.

4. **ExitLong is not always below price.** In a strong downtrend, the highest high over 22 bars may be far above current price, but ATR is also elevated. The ExitLong line can sit well above current price in such conditions, which is correct behavior (it signals the long position should have already been exited).

5. **No trend state.** Unlike SuperTrend which flips between bullish/bearish bands, Chandelier Exit always outputs both lines. The trader decides which line is relevant based on their position direction. This makes it more flexible but requires active interpretation.

6. **Period 22 is not arbitrary.** It represents approximately one trading month (22 business days). For crypto (24/7 markets), consider period 30. For weekly charts, period 4-5 captures one month.

7. **Gap sensitivity in thin markets.** True Range includes gap components ($|H - C_{prev}|$ and $|L - C_{prev}|$). In illiquid instruments with frequent gaps, ATR may be dominated by gap noise rather than intrabar volatility. Consider longer periods to smooth this out.

## References

- Le Beau, C., & Lucas, D. (1992). *Technical Traders Guide to Computer Analysis of the Futures Market*. McGraw-Hill.
- Elder, A. (2002). *Come Into My Trading Room: A Complete Guide to Trading*. John Wiley & Sons.
- Skender.Stock.Indicators: [`GetChandelier()`](https://dotnet.stockindicators.dev/indicators/Chandelier/)
