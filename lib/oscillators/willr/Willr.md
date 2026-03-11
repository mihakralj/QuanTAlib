# WILLR: Williams %R

> "The market tells you where it closed relative to where it traded. That single fact contains more information than most traders realize." -- George Lane

| Property | Value |
|----------|-------|
| **Category** | Oscillator |
| **Inputs** | Bar series (High, Low, Close) |
| **Parameters** | `period` (default 14) |
| **Outputs** | Single series (Williams %R line) |
| **Output range** | $-100$ to $0$ |
| **Warmup** | `period` bars |
| **PineScript**   | [willr.pine](willr.pine)                       |

### Key takeaways

- Measures where the close sits within the highest-high to lowest-low range over a lookback period, scaled to $[-100, 0]$.
- Arithmetically identical to the Fast Stochastic %K minus 100: $\text{WillR} = \text{Stoch \%K} - 100$.
- Uses `MonotonicDeque` pairs for O(1) amortized highest/lowest tracking in streaming mode.
- Zero range (all bars identical) returns $-50$ (midpoint), not NaN.
- Overbought near $0$, oversold near $-100$. The inverted scale catches people off guard.

## Historical Context

Larry Williams introduced Williams %R in *How I Made One Million Dollars Last Year Trading Commodities* (1973). The indicator was designed for quick manual calculation: find the highest high, find the lowest low, see where the close falls in that range. No computers required.

Williams %R and George Lane's Stochastic Oscillator (late 1950s) share identical core logic. The only difference is output mapping. Stochastic scales $[0, 100]$ with overbought at the top; Williams %R scales $[-100, 0]$ with overbought at the top (near zero). Some traders find the inverted scale more intuitive for spotting reversals. Others find it confusing. Neither opinion changes the math.

The indicator remains popular precisely because of its simplicity. No smoothing, no signal line, no parameters beyond the lookback period. It answers one question: where did the close land within the recent range?

## What It Measures and Why It Matters

Williams %R measures the position of the current close relative to the highest high and lowest low over the past $n$ bars, normalized to $[-100, 0]$. A reading of $0$ means the close equals the period high. A reading of $-100$ means the close equals the period low.

The indicator reveals short-term momentum exhaustion. When %R reaches the overbought zone (above $-20$), price is closing near the top of its recent range and may be running out of buying pressure. When it reaches the oversold zone (below $-80$), price is closing near the bottom and selling pressure may be exhausted.

In practice, Williams %R is a leading indicator. It typically reverses direction before price does. That property makes it useful for anticipating turns but dangerous for trading in strong trends, where %R can remain overbought or oversold for extended periods without price reversing.

## Mathematical Foundation

### Core Formula

$$
HH_n = \max(H_i) \quad \text{for } i \in [t - n + 1, \, t]
$$

$$
LL_n = \min(L_i) \quad \text{for } i \in [t - n + 1, \, t]
$$

$$
\text{Williams \%R}_t = -100 \times \frac{HH_n - C_t}{HH_n - LL_n}
$$

**Relationship to Stochastic %K:**

$$
\text{Stoch \%K} = 100 \times \frac{C - LL}{HH - LL}
$$

$$
\text{Williams \%R} = -100 \times \frac{HH - C}{HH - LL} = -100 + 100 \times \frac{C - LL}{HH - LL} = \text{Stoch \%K} - 100
$$

### Parameter Mapping

| Parameter | Code | Default | Constraints |
|-----------|------|---------|-------------|
| Period | `period` | 14 | `> 0` |

### Warmup Period

$$
W = P
$$

The indicator requires $P$ bars to fill the sliding window for highest-high and lowest-low computation.

## Architecture & Physics

### 1. MonotonicDeque Streaming

Two `MonotonicDeque` instances provide O(1) amortized min/max tracking over the sliding window:

- **Max deque**: maintains decreasing order of highs. Front always holds the window maximum.
- **Min deque**: maintains increasing order of lows. Front always holds the window minimum.
- **Circular buffers** (`_hBuf`, `_lBuf`): store raw high/low values for deque rebuild on bar correction.

### 2. State Management

A `record struct State` holds `LastValidHigh`, `LastValidLow`, and `LastValidClose` for NaN/Infinity substitution. The `_s` / `_ps` pattern provides bar correction: `isNew=true` snapshots state; `isNew=false` restores and recomputes.

### 3. Batch Path

`Batch(ReadOnlySpan, ReadOnlySpan, ReadOnlySpan, Span, int)` delegates to `Highest.Batch()` and `Lowest.Batch()` for vectorized sliding min/max. Intermediate buffers use `stackalloc` for inputs $\leq 256$ elements and `ArrayPool<double>` for larger inputs.

### 4. Edge Cases

| Condition | Behavior |
|-----------|----------|
| `period <= 0` | `ArgumentException` with `nameof(period)` |
| `NaN` / `Infinity` input | Substitutes last valid value per channel (H/L/C) |
| All NaN (no valid data yet) | Returns `NaN` |
| Zero range ($HH = LL$) | Returns $-50$ (midpoint) |
| `isNew = false` | Restores `_ps`, rebuilds both deques from circular buffer |

## Interpretation and Signals

### Signal Zones

| Zone | Condition | Interpretation |
|------|-----------|----------------|
| Overbought | `%R > -20` | Close near period high; potential reversal down |
| Neutral | `-80 ≤ %R ≤ -20` | Normal trading range |
| Oversold | `%R < -80` | Close near period low; potential reversal up |

### Signal Patterns

- **Overbought reversal**: %R rises above $-20$ then drops back below. Bearish signal.
- **Oversold reversal**: %R falls below $-80$ then rises back above. Bullish signal.
- **Divergence**: Price makes new highs while %R peaks decline (bearish) or price makes new lows while %R troughs rise (bullish).
- **Failure swing**: %R reaches an extreme, pulls back, fails to re-reach the extreme, then reverses. Stronger signal than simple crossover.

### Practical Notes

- In strong uptrends, Williams %R can remain above $-20$ for extended periods. Treating every overbought reading as a sell signal in a bull market is a reliable way to underperform.
- Use trend filters (ADX, moving average slope) to contextualize overbought/oversold readings.
- Williams %R has no built-in signal line. For smoothed crossover signals, apply an SMA to the output or use Stochastic with %D instead.

## Related Indicators

- [**Stoch**](../stoch/Stoch.md): Same core formula with $[0, 100]$ scale and %D smoothing line.
- [**Stochf**](../stochf/Stochf.md): Fast Stochastic variant, numerically $\text{WillR} + 100$.
- [**KDJ**](../kdj/Kdj.md): Extended stochastic with J-line divergence amplification.
- [**SMI**](../smi/Smi.md): Stochastic Momentum Index, measures distance from midpoint rather than range boundary.

## Validation

| Library | Status | Notes |
|---------|--------|-------|
| Skender | ✅ | `GetWilliamsR(period)` matches within `1e-9` after warmup |
| TA-Lib | ✅ | `WillR(high, low, close, period)` matches within `1e-9` |
| Tulip | ✅ | `willr(high, low, close, period)` matches within `1e-9` |
| Ooples | -- | Not validated |

## Performance Profile

### Key Optimizations

- **O(1) amortized streaming**: `MonotonicDeque` avoids full-window scans for min/max on each bar.
- **Zero allocation**: `Update` uses pre-allocated circular buffers and `record struct State`.
- **Stackalloc/ArrayPool batch**: Intermediate highest/lowest buffers use `stackalloc` for $\leq 256$ elements, `ArrayPool` beyond.
- **Deque rebuild on correction**: `isNew=false` triggers O(period) deque rebuild from circular buffer; infrequent in practice.

### Operation Count (Streaming Mode)

| Operation | Count per bar |
|-----------|---------------|
| Comparisons | 2-3 (deque push amortized) |
| Divisions | 1 |
| Multiplications | 1 |
| NaN checks | 3 (high, low, close) |
| **Total** | **~7 ops** |

### SIMD Analysis (Batch Mode)

| Property | Value |
|----------|-------|
| Vectorizable | Partially (via `Highest.Batch` / `Lowest.Batch`) |
| Final loop | Scalar: `output[i] = -100 * (HH[i] - close[i]) / (HH[i] - LL[i])` |
| Fallback | Scalar with `ArrayPool` for large inputs |

## Common Pitfalls

1. **Inverted scale confusion**: Williams %R uses $[-100, 0]$, not $[0, 100]$. Overbought is near $0$, oversold is near $-100$. Reversing the mental model from Stochastic is the most common mistake.
2. **Zero range returns $-50$**: When all bars in the window share the same high and low, the range is zero. This implementation returns $-50$ (midpoint). Other implementations may return $0$ or NaN.
3. **Overbought does not equal sell**: In trending markets, %R stays overbought/oversold for long stretches. Fading the trend based solely on %R readings without a trend filter leads to significant drawdowns.
4. **Short lookback noise**: Period $< 5$ creates excessive whipsaws. The default 14 balances responsiveness and noise rejection.
5. **No signal line**: Unlike Stochastic, Williams %R has no %D signal line. Traders who want smoothed crossover signals should apply a separate SMA or switch to Stochastic.
6. **Bar correction cost**: Correcting a bar (`isNew=false`) triggers O(period) deque rebuild. Batch-correcting thousands of bars in a tight loop exposes the cost, though in normal streaming this is negligible.

## FAQ

**Q: Why does Williams %R use a negative scale?**
A: Historical convention. Larry Williams chose $[-100, 0]$ to visually distinguish it from Stochastic's $[0, 100]$ scale. The math is identical; only the output mapping differs.

**Q: What happens when range is zero?**
A: This implementation returns $-50$ (midpoint of $[-100, 0]$). Stochastic returns $0$ for the same condition. The choice is arbitrary since zero range means close equals both the high and the low.

**Q: Should I use Williams %R or Stochastic?**
A: If you want a raw, unsmoothed position-in-range reading with no signal line, Williams %R is simpler. If you want %D smoothing and crossover signals, use Stochastic. They measure the same thing.

## References

- Williams, L. *How I Made One Million Dollars Last Year Trading Commodities*. Windsor Books, 1973.
- Lane, G. C. "Lane's Stochastics." *Technical Analysis of Stocks & Commodities*, 1984.
- Murphy, J. J. *Technical Analysis of the Financial Markets*. New York Institute of Finance, 1999.
- Achelis, S. B. *Technical Analysis from A to Z*. McGraw-Hill, 2000.
