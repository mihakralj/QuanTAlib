# KRI: Kairi Relative Index

> *The simplest measure of overextension is the oldest: how far has price strayed from its average? The Japanese knew this before anyone had a computer.*

| Property | Value |
|----------|-------|
| **Category** | Oscillator |
| **Inputs** | Source (close) |
| **Parameters** | `period` (default 14) |
| **Outputs** | Single series (percentage deviation from SMA) |
| **Output range** | Unbounded (centered around 0) |
| **Warmup** | `period` bars |
| **PineScript**   | [kri.pine](kri.pine)                       |

### Key takeaways

- Measures the percentage deviation of price from its Simple Moving Average: $\text{KRI} = 100 \times (P - \text{SMA}) / \text{SMA}$.
- Positive KRI means price is above its average (bullish bias); negative means below (bearish bias).
- Functionally equivalent to a percentage-normalized price-SMA deviation; simpler than RSI but less bounded.
- Uses a circular buffer with running sum for O(1) per-bar updates.
- Extreme KRI readings suggest overextension and potential mean reversion.

## Historical Context

The Kairi Relative Index originates from the Japanese technical analysis tradition, predating the widespread adoption of Western oscillators. The concept is straightforward: express the distance between the current price and its moving average as a percentage of the average itself.

Before RSI, MACD, and stochastic oscillators became standard, Japanese traders relied on simple deviation measures to gauge overextension. KRI is the percentage form of what is sometimes called the "price oscillator" or "detrended price." The logic is that prices tend to oscillate around their moving averages, and extreme deviations create gravitational pull back toward the mean.

KRI never achieved the fame of RSI or MACD in Western markets, partly because it lacks the elegant bounded range that makes those indicators visually convenient. But its simplicity is also its strength: no smoothing layers, no signal lines, no arbitrary scaling. Just raw deviation from the average, expressed as a percentage.

## What It Measures and Why It Matters

KRI answers one question: how far, in percentage terms, has price moved from its moving average? A KRI of +5 means price is 5% above its SMA. A KRI of $-3$ means price is 3% below.

This makes KRI a mean-reversion detector. When KRI reaches extreme positive values, price is overextended above its average and statistically more likely to pull back. When KRI reaches extreme negative values, price is overextended below and more likely to bounce.

The indicator is instrument-specific. What constitutes "extreme" depends on the asset's typical volatility. A KRI of +2 might be extreme for a low-volatility bond ETF but unremarkable for a high-beta tech stock. Traders must calibrate their thresholds per instrument and timeframe.

## Mathematical Foundation

### Core Formula

$$
\text{SMA}_t = \frac{1}{N} \sum_{i=0}^{N-1} P_{t-i}
$$

$$
\text{KRI}_t = 100 \times \frac{P_t - \text{SMA}_t}{\text{SMA}_t}
$$

where:

- $P_t$ = current price (close)
- $N$ = lookback period (default 14)
- $\text{SMA}_t$ = Simple Moving Average over $N$ bars

### Special Case

$$
\text{If } \text{SMA}_t = 0: \quad \text{KRI}_t = 0
$$

Division by zero returns $0$ rather than NaN.

### Parameter Mapping

| Parameter | Symbol | Default | Constraint |
|-----------|--------|---------|------------|
| `period` | $N$ | 14 | $N \geq 1$ |

### Warmup Period

$$
W = N
$$

The circular buffer must fill with $N$ values before the SMA is computed over the full window.

## Architecture & Physics

### 1. Circular Buffer with Running Sum

A single `RingBuffer` of capacity $N$ stores source values. A running sum tracks the total, enabling O(1) SMA computation: subtract the oldest value (about to be evicted), add the newest, divide by count.

### 2. State Management

A `record struct State` holds `Sum`, `LastValid`, and `Count`. The `_state` / `_p_state` pattern supports bar correction.

### 3. Batch Path

`Batch(ReadOnlySpan, Span, int)` mirrors the streaming logic using a local `RingBuffer` and running sum, producing identical results without instantiating a full indicator.

### 4. Edge Cases

| Condition | Behavior |
|-----------|----------|
| `period <= 0` | `ArgumentException` with `nameof(period)` |
| `NaN` / `Infinity` input | Substitutes last valid value |
| SMA = 0 | Returns $0$ (avoids division by zero) |
| During warmup | SMA computed over available bars (partial window) |

## Interpretation and Signals

### Signal Zones

| Zone | Condition | Interpretation |
|------|-----------|----------------|
| Overbought | KRI >> 0 (instrument-specific) | Price far above average; overextended upside |
| Neutral | KRI near 0 | Price tracking its average closely |
| Oversold | KRI << 0 (instrument-specific) | Price far below average; overextended downside |

### Signal Patterns

- **Mean reversion**: Extreme KRI readings (e.g., beyond $\pm 2\sigma$ of its own distribution) suggest a pullback toward the SMA.
- **Trend confirmation**: Persistently positive KRI confirms an uptrend. Persistently negative KRI confirms a downtrend.
- **Zero-line crossover**: KRI crossing from negative to positive means price has reclaimed its SMA. From positive to negative means it has broken below.
- **Divergence**: Price making new highs while KRI peaks decline suggests weakening momentum relative to the average.

### Practical Notes

KRI thresholds must be calibrated per instrument. For major equity indices, KRI beyond $\pm 5$ is often noteworthy. For cryptocurrencies, $\pm 15$ might be routine. Look at the historical distribution of KRI for the specific asset to determine meaningful extreme levels. Bollinger Bands around KRI itself (KRI $\pm 2\sigma$ of KRI) can automate threshold detection.

## Related Indicators

- [**Pgo**](../pgo/Pgo.md): Pretty Good Oscillator, normalizes deviation from SMA by ATR instead of the SMA value itself.
- [**Fisher**](../fisher/Fisher.md): Fisher Transform, normalizes price position into a bounded Gaussian distribution.
- [**Inertia**](../inertia/Inertia.md): Regression-based trend strength, related but uses slope rather than deviation.

## Validation

| Library | Batch | Streaming | Span | Notes |
|---------|:-----:|:---------:|:----:|-------|
| **TA-Lib** | -- | -- | -- | No direct KRI function |
| **Skender** | -- | -- | -- | Not available |
| **Tulip** | -- | -- | -- | Not available |
| **Ooples** | -- | -- | -- | Not available |

Validated via internal consistency across all four API modes (batch, streaming, span, eventing).

## Performance Profile

### Key Optimizations

- **O(1) streaming**: Running sum avoids window re-summation each bar.
- **Zero allocation**: Pre-allocated `RingBuffer` and `record struct State`.
- **Aggressive inlining**: `[MethodImpl(AggressiveInlining)]` on `Update` and `Batch`.
- **Single buffer**: Unlike ER's dual-buffer design, KRI needs only one circular buffer.

### Operation Count (Streaming Mode)

| Operation | Count per bar |
|-----------|---------------|
| SUB | 1 (remove oldest from sum) |
| ADD | 1 (add newest to sum) |
| DIV | 1 (sum / count = SMA) |
| SUB | 1 (value - SMA) |
| MUL | 1 (100 * deviation) |
| DIV | 1 (deviation / SMA) |
| NaN check | 1 |
| **Total** | **~7 ops** |

## Common Pitfalls

1. **Unbounded output**: KRI has no fixed range like RSI's $[0, 100]$. A KRI of $+20$ is unremarkable for volatile assets but alarming for stable ones. Always calibrate thresholds per instrument.
2. **SMA lag**: KRI inherits the SMA's lag. A 14-period SMA lags about 7 bars, which means KRI reacts to deviations from a lagged average, not the current trend center.
3. **Not a standalone signal**: Extreme KRI readings do not guarantee reversal. In strong trends, KRI can remain extreme for extended periods (trending above the average persistently).
4. **Percentage scaling hides absolute moves**: A KRI of $+5\%$ on a $\$10$ stock is $\$0.50$; on a $\$500$ stock it's $\$25$. The indicator normalizes magnitude but loses absolute context.
5. **Division by zero on zero-price assets**: If SMA = 0 (theoretically possible with synthetic series), KRI returns 0. In practice this only matters in testing.
6. **Partial warmup**: Before the buffer is full, SMA is computed over fewer bars. Values during the warmup phase are less reliable.

## References

- Japanese Technical Analysis tradition, various sources.
- Achelis, S. B. *Technical Analysis from A to Z*. McGraw-Hill, 2000.
- Colby, R. W. *The Encyclopedia of Technical Market Indicators*, 2nd ed. McGraw-Hill, 2003.
