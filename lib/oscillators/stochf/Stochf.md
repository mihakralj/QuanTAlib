# STOCHF: Stochastic Fast Oscillator

> *Speed kills in traffic. In markets, it merely whipsaws.*

| Property | Value |
|----------|-------|
| **Category** | Oscillator |
| **Inputs** | Bar series (High, Low, Close) |
| **Parameters** | `kLength` (default 5), `dPeriod` (default 3) |
| **Outputs** | Dual series (%K line, %D signal line) |
| **Output range** | $0$ to $100$ |
| **Warmup** | `kLength` bars |
| **PineScript**   | [stochf.pine](stochf.pine)                       |

### Key takeaways

- The unsmoothed (raw) variant of the Stochastic Oscillator. %K has no additional SMA smoothing applied.
- Default `kLength` is 5 (shorter than Stoch's 14), making it more responsive and noisier.
- Uses `MonotonicDeque` pairs for O(1) amortized min/max tracking, identical architecture to Stoch.
- Zero range (all bars identical) returns $0$ for %K.
- Matches TA-Lib's dedicated `STOCHF` function, which separates Fast from Slow Stochastic explicitly.

## Historical Context

George C. Lane's Stochastic Oscillator (late 1950s) was originally this: the raw, unsmoothed position-in-range calculation with a simple SMA signal line. The "Fast" label was applied retroactively when traders began smoothing %K with an additional SMA to create the "Slow" variant. What Lane invented is what we now call Fast Stochastic.

TA-Lib codified the distinction by providing separate functions: `STOCH` (slow, with configurable smoothing on %K) and `STOCHF` (fast, raw %K). QuanTAlib follows this convention. The `Stoch` class defaults to `kLength=14`; the `Stochf` class defaults to `kLength=5` for faster response. Both produce raw %K internally; the difference is the default parameterization and the explicit naming that signals intent.

The shorter default period makes Stochf more reactive to short-term price action. That reactivity is simultaneously its strength (early signals) and its weakness (more false signals in choppy markets). Traders who want the responsiveness of a 5-period lookback but less noise typically apply additional smoothing externally rather than switching to the Slow variant.

## What It Measures and Why It Matters

Stochf measures where the current close sits within the highest-high to lowest-low range over the past `kLength` bars, expressed as a percentage from $0$ to $100$. The %D line is the SMA of %K over `dPeriod` bars.

The indicator prioritizes speed over smoothness. Because %K is unsmoothed and the default lookback is only 5 bars, Stochf reacts to price changes faster than its Slow Stochastic counterpart. This makes it useful for short-term trading where early detection of momentum shifts matters more than filtering noise.

The trade-off is straightforward: faster response means more false signals. In trending markets, Stochf whipsaws through overbought/oversold zones rapidly. In range-bound markets, the quick response helps identify turning points before slower indicators confirm. Knowing which regime you are trading determines whether Stochf helps or hurts.

## Mathematical Foundation

### Core Formula

$$
HH_n = \max(H_i) \quad \text{for } i \in [t - n + 1, \, t]
$$

$$
LL_n = \min(L_i) \quad \text{for } i \in [t - n + 1, \, t]
$$

$$
\%K_t = 100 \times \frac{C_t - LL_n}{HH_n - LL_n}
$$

$$
\%D_t = \text{SMA}(\%K, d)
$$

where $n$ is `kLength` and $d$ is `dPeriod`.

### Parameter Mapping

| Parameter | Code | Default | Constraints |
|-----------|------|---------|-------------|
| K Length | `kLength` | 5 | `> 0` |
| D Period | `dPeriod` | 3 | `> 0` |

### Warmup Period

$$
W = n
$$

The indicator requires $n$ bars to fill the sliding window. The %D SMA pre-fills its buffer with the first %K value (PineScript convention), producing output from bar 0.

## Architecture & Physics

### 1. MonotonicDeque Streaming

Identical architecture to `Stoch`: two `MonotonicDeque` instances (max for highs, min for lows) provide O(1) amortized sliding min/max. Circular buffers (`_hBuf`, `_lBuf`) store raw H/L values for deque rebuild on bar correction.

### 2. %D Signal Line

A circular buffer (`_dBuf`) with running sum computes the SMA of %K in O(1). First bar pre-fills the entire buffer with the initial %K value; subsequent bars replace the oldest entry.

### 3. Batch Path

`Batch(ReadOnlySpan, ..., Span, Span, int, int)` delegates to `Highest.Batch()` and `Lowest.Batch()`. Intermediate buffers use `stackalloc` for $\leq 256$ elements and `ArrayPool<double>` beyond that threshold.

### 4. Edge Cases

| Condition | Behavior |
|-----------|----------|
| `kLength <= 0` or `dPeriod <= 0` | `ArgumentException` with `nameof()` |
| `NaN` / `Infinity` input | Substitutes last valid value per channel (H/L/C) |
| All NaN (no valid data yet) | Returns `NaN` for both %K and %D |
| Zero range ($HH = LL$) | %K returns $0$ |
| `isNew = false` | Restores `_ps`, rebuilds both deques from circular buffer |

## Interpretation and Signals

### Signal Zones

| Zone | Condition | Interpretation |
|------|-----------|----------------|
| Overbought | `%K > 80` | Close near period high; potential reversal down |
| Neutral | `20 ≤ %K ≤ 80` | Normal trading range |
| Oversold | `%K < 20` | Close near period low; potential reversal up |

### Signal Patterns

- **%K/%D crossover**: Bullish when %K crosses above %D; bearish when %K crosses below %D.
- **Divergence**: Price makes new highs while %K fails to confirm (bearish) or price makes new lows while %K holds (bullish).
- **Hook reversal**: %K reverses sharply at an extreme without completing a full crossover. Common with the fast variant's responsiveness.

### Practical Notes

- Stochf generates more crossover signals than Stoch due to the shorter default period and lack of %K smoothing. Filter with trend context.
- In strong trends, %K oscillates rapidly near 100 (uptrend) or 0 (downtrend). These are not reversal signals; they confirm trend strength.
- Consider using Stochf for entry timing within a trend identified by a slower indicator (ADX, SMA slope).

## Related Indicators

- [**Stoch**](../stoch/Stoch.md): Same formula with default `kLength=14`; often used with additional %K smoothing for the "Slow" variant.
- [**Willr**](../willr/Willr.md): Identical math with inverted $[-100, 0]$ scale.
- [**KDJ**](../kdj/Kdj.md): Extended stochastic with J-line divergence amplification.
- [**StochRSI**](../stochrsi/Stochrsi.md): Applies the stochastic formula to RSI output instead of price.

## Validation

| Library | Status | Notes |
|---------|--------|-------|
| Skender | ✅ | `GetStoch(kLength, dPeriod, smoothPeriods=1)` matches within `1e-6` |
| TA-Lib | ✅ | `StochF(high, low, close, kLength, dPeriod)` matches within `1e-6` |
| Tulip | -- | Not directly validated |
| Ooples | -- | Not validated |

## Performance Profile

### Key Optimizations

- **O(1) amortized streaming**: `MonotonicDeque` avoids full-window scans for min/max.
- **O(1) %D SMA**: Circular buffer with running sum.
- **Zero allocation**: `Update` uses pre-allocated circular buffers and `record struct State`.
- **Stackalloc/ArrayPool batch**: Intermediate buffers use `stackalloc` for $\leq 256$ elements, `ArrayPool` beyond.

### Operation Count (Streaming Mode)

| Operation | Count per bar |
|-----------|---------------|
| Comparisons | 2-3 (deque push amortized) |
| Divisions | 2 (range normalization + %D SMA) |
| Multiplications | 1 (`100 *`) |
| Additions/Subtractions | 2 (%D running sum update) |
| NaN checks | 3 (high, low, close) |
| **Total** | **~10 ops** |

### SIMD Analysis (Batch Mode)

| Property | Value |
|----------|-------|
| Vectorizable | Partially (via `Highest.Batch` / `Lowest.Batch`) |
| %K final loop | Scalar: `100 * (close[i] - LL[i]) / (HH[i] - LL[i])` |
| %D computation | Scalar circular buffer with running sum |

## Common Pitfalls

1. **Fast vs Slow confusion**: StochF is the unsmoothed variant. TA-Lib's `STOCH` applies %K smoothing; `STOCHF` does not. Comparing outputs without matching smoothing parameters produces mismatches.
2. **Default period difference**: StochF defaults to `kLength=5`, not 14. Comparing directly to `Stoch(14, 3)` produces different results even though the formula is identical.
3. **More whipsaws**: The shorter lookback and lack of smoothing generate more %K/%D crossovers. Most are noise in trending markets.
4. **Zero range returns 0**: When all bars in the window have identical H/L, %K returns $0$. Williams %R returns $-50$ for the same condition.
5. **%D warmup convention**: First %D value pre-fills the SMA buffer with the initial %K, matching PineScript behavior. Other implementations may emit NaN until `dPeriod` bars of %K are available.
6. **Overbought persistence**: In strong trends, %K stays near extremes. The fast response makes this more pronounced than with Slow Stochastic.

## FAQ

**Q: What is the difference between Stochf and Stoch?**
A: Identical formula, different defaults. Stochf defaults to `kLength=5` for faster response. Stoch defaults to `kLength=14`. Neither applies additional smoothing to %K in this implementation. The "Slow Stochastic" convention requires smoothing %K with an SMA, which is a separate operation.

**Q: Why does TA-Lib separate STOCH and STOCHF?**
A: TA-Lib's `STOCH` function includes a `smoothK` parameter that applies SMA smoothing to %K before computing %D (Slow Stochastic). `STOCHF` omits that smoothing step entirely. QuanTAlib's `Stoch` and `Stochf` both output raw %K; the distinction is in default parameters and naming convention.

**Q: When should I prefer Stochf over Stoch?**
A: When you need faster signal detection and can tolerate more false positives. Typical use cases: scalping, intraday mean reversion, or as a timing tool within a larger trend-following system.

## References

- Lane, G. C. "Lane's Stochastics." *Technical Analysis of Stocks & Commodities*, 1984.
- Murphy, J. J. *Technical Analysis of the Financial Markets*. New York Institute of Finance, 1999.
- Achelis, S. B. *Technical Analysis from A to Z*. McGraw-Hill, 2000.
