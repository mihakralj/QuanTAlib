# PSL: Psychological Line

> "Markets are crowds, and crowds have moods. Count the up days; you will know the mood." -- Japanese proverb (adapted)

| Property | Value |
|----------|-------|
| **Category** | Oscillator |
| **Inputs** | Source (close) |
| **Parameters** | `period` (default 12) |
| **Outputs** | Single series (percentage of up-bars) |
| **Output range** | $0$ to $100$ |
| **Warmup** | `period` bars |

### Key takeaways

- Counts the percentage of bars where price closed higher than the previous bar over a lookback window.
- Output of $100$ means every bar in the window was an up-bar; $0$ means every bar was a down-bar or unchanged.
- A purely sentiment-driven indicator: it measures market psychology (bullish/bearish streak) rather than magnitude of moves.
- Uses a circular buffer storing $1.0$ (up) or $0.0$ (down/unchanged) with a running sum for O(1) updates.
- Readings above $75$ suggest excessive optimism; below $25$ suggest excessive pessimism.

## Historical Context

The Psychological Line (PSL) comes from the Japanese technical analysis tradition, where it has been used for decades as a simple gauge of market sentiment. The idea is rooted in crowd psychology: when too many consecutive bars close higher, the crowd is euphorically bullish and likely to reverse. When too many close lower, the crowd is excessively bearish and due for a bounce.

PSL is one of the simplest possible oscillators. It ignores the magnitude of price changes entirely, caring only about direction. A 0.01% gain counts the same as a 5% gain. This deliberate blindness to magnitude is the indicator's defining characteristic: it measures mood, not movement.

The indicator never gained wide adoption in Western technical analysis, where RSI and Stochastic dominate. But it fills a unique niche. No other standard oscillator measures purely the fraction of positive bars in a window.

## What It Measures and Why It Matters

PSL answers one question: what percentage of the last $N$ bars were up-bars? An up-bar is defined as one where the close exceeds the previous close. Down-bars and unchanged bars both count as "not up."

A PSL of $75$ means three out of four recent bars closed higher. This does not tell you *how much* price rose, only that the direction was consistently up. The value lies in identifying streaks. Markets that have been consistently closing higher (or lower) without interruption tend to be overextended in that direction.

PSL is a contrarian indicator at extremes. When PSL exceeds $75$, the market has been relentlessly bullish: historically, such streaks tend to exhaust themselves. When PSL drops below $25$, the consistent selling may be running out of momentum. Between those thresholds, PSL provides less actionable information.

## Mathematical Foundation

### Core Formula

Define:

$$
U_t = \begin{cases} 1 & \text{if } P_t > P_{t-1} \\ 0 & \text{otherwise} \end{cases}
$$

$$
\text{PSL}_t = 100 \times \frac{\sum_{i=0}^{N-1} U_{t-i}}{N}
$$

where:

- $P_t$ = current price (close)
- $N$ = lookback period (default 12)
- $U_t$ = up-bar indicator (binary: 1 or 0)

### Parameter Mapping

| Parameter | Symbol | Default | Constraint |
|-----------|--------|---------|------------|
| `period` | $N$ | 12 | $N \geq 1$ |

### Warmup Period

$$
W = N
$$

The buffer must fill with $N$ up/down classifications before the percentage is computed over the full window.

## Architecture & Physics

### 1. Binary Circular Buffer

A `RingBuffer` of capacity $N$ stores $1.0$ (up-bar) or $0.0$ (down/unchanged). A running sum (`UpSum`) counts the number of up-bars currently in the window. The percentage is simply $100 \times \text{UpSum} / \text{Count}$.

### 2. Up-Bar Classification

The comparison `value > PrevValue` determines the binary classification. The first bar has no previous value, so it defaults to $0.0$ (not an up-bar). `PrevValue` is tracked in the state struct.

### 3. State Management

A `record struct State` holds `UpSum`, `PrevValue`, `LastValid`, and `Count`. The `_state` / `_p_state` pattern supports bar correction via `isNew` flag.

### 4. Batch Path

`Batch(ReadOnlySpan, Span, int)` uses a local `RingBuffer` and running sum, producing identical results without indicator instantiation.

### 5. Edge Cases

| Condition | Behavior |
|-----------|----------|
| `period <= 0` | `ArgumentException` with `nameof(period)` |
| `NaN` / `Infinity` input | Substitutes last valid value |
| No previous value (first bar) | Classified as $0$ (not up) |
| All bars identical | All classified as $0$ (not up); PSL = $0$ |
| During warmup | Percentage computed over available bars |

## Interpretation and Signals

### Signal Zones

| Zone | Condition | Interpretation |
|------|-----------|----------------|
| Excessive optimism | PSL > 75 | Market has been consistently closing higher; bullish exhaustion likely |
| Neutral | 25 - 75 | Mixed direction; no clear sentiment extreme |
| Excessive pessimism | PSL < 25 | Market has been consistently closing lower; bearish exhaustion likely |

### Signal Patterns

- **Overbought reversal**: PSL rises above $75$ then drops back below. The streak of up-bars has broken, suggesting selling pressure is emerging.
- **Oversold reversal**: PSL falls below $25$ then rises back above. The streak of down-bars has broken, suggesting buying interest is returning.
- **Divergence**: Price making new highs while PSL is declining means the highs are being achieved with fewer consecutive up-bars, suggesting fatigue.
- **Extreme readings**: PSL of $100$ (every bar up) or $0$ (every bar down) are rare and typically mark climactic moves.

### Practical Notes

PSL works best on daily timeframes where the "close" concept is well-defined. On intraday data, the up-bar/down-bar classification becomes noisier and less meaningful. Combine PSL with a volatility indicator (ATR, Bollinger Width) to distinguish between genuine sentiment extremes and low-volatility drift where price ticks up marginally each bar without real conviction.

## Related Indicators

- [**Stoch**](../stoch/Stoch.md): Measures position within range rather than direction frequency.
- [**Willr**](../willr/Willr.md): Williams %R, measures close relative to range; complementary to PSL's direction-counting approach.
- [**Er**](../er/Er.md): Efficiency Ratio, measures directional efficiency but considers magnitude, not just direction.

## Validation

| Library | Batch | Streaming | Span | Notes |
|---------|:-----:|:---------:|:----:|-------|
| **TA-Lib** | -- | -- | -- | No PSL function |
| **Skender** | -- | -- | -- | Not available |
| **Tulip** | -- | -- | -- | Not available |
| **Ooples** | -- | -- | -- | Not available |

Validated via internal consistency across all four API modes (batch, streaming, span, eventing).

## Performance Profile

### Key Optimizations

- **O(1) streaming**: Running sum of binary values avoids window re-counting each bar.
- **Binary buffer**: Stores only $1.0$ or $0.0$, minimizing computation per element.
- **Zero allocation**: Pre-allocated `RingBuffer` and `record struct State`.
- **Aggressive inlining**: `[MethodImpl(AggressiveInlining)]` on `Update` and `Batch`.

### Operation Count (Streaming Mode)

| Operation | Count per bar |
|-----------|---------------|
| Comparison | 1 (value > prevValue) |
| SUB | 1 (remove oldest from sum) |
| ADD | 1 (add newest to sum) |
| MUL | 1 (100 * upSum) |
| DIV | 1 (/ count) |
| NaN check | 1 |
| **Total** | **~6 ops** |

## Common Pitfalls

1. **Magnitude blindness**: A PSL of $80$ does not mean the market rose significantly. It means 80% of bars were technically up-bars, even if each up-bar moved only a fraction of a point. Always check actual price change alongside PSL.
2. **Unchanged bars count as "not up"**: If price closes exactly flat versus the prior bar, it counts as $0$ (not up). In illiquid instruments with many unchanged closes, PSL will structurally read lower.
3. **First bar is always $0$**: With no previous value to compare, the first bar is classified as "not up." This is a design choice, not a bug.
4. **Period sensitivity**: Short periods (e.g., 5) make PSL jumpy. Each bar flips between 80 and 60 with a single direction change. The default 12 is a reasonable balance.
5. **Not a trend indicator**: PSL of $70$ does not mean the trend is up. It means recent bars were mostly up. In a volatile range, you can have 70% up-bars while price goes nowhere.
6. **Daily data is ideal**: PSL was designed for daily charts where "up day" and "down day" are meaningful concepts. On tick data, the up/down classification becomes noise.

## References

- Japanese Technical Analysis tradition, various sources.
- Colby, R. W. *The Encyclopedia of Technical Market Indicators*, 2nd ed. McGraw-Hill, 2003.
- Achelis, S. B. *Technical Analysis from A to Z*. McGraw-Hill, 2000.
