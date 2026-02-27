# ER: Efficiency Ratio

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Oscillator                       |
| **Inputs**       | Source (close)                   |
| **Parameters**   | `period` (default 10)            |
| **Outputs**      | Single series (Efficiency Ratio) |
| **Output range** | $0$ to $1$                       |
| **Warmup**       | `period + 1` bars                |

### TL;DR

- ER measures the signal-to-noise ratio of price movement: net directional change divided by total path length.
- Clamped to $[0, 1]$; division by zero (zero noise) returns $0$.
- Output of $1.0$ means price moved in a perfectly straight line (pure trend). Output of $0.0$ means all movement cancelled out (pure noise).
- It is core component of KAMA (Kaufman's Adaptive Moving Average), where ER dynamically adjusts the smoothing constant.
- Not available and therefore not validated against any other TA library

> "The best trades move in a straight line. The worst ones wander. ER tells you which kind you're looking at." -- Perry Kaufman

## Historical Context

Perry Kaufman introduced the Efficiency Ratio in *Trading Systems and Methods* (1995) as part of his Adaptive Moving Average (KAMA) framework. The idea was straightforward: an ideal trend indicator should react quickly in trending markets and slowly in choppy ones. ER provides the adaptive signal that tells KAMA how to behave.

The concept borrows from signal processing. Engineers measure signal-to-noise ratio to assess transmission quality. Kaufman applied the same logic to price: the "signal" is net directional movement, the "noise" is total bar-to-bar movement. A high ratio means the market is moving efficiently in one direction. A low ratio means the market is churning.

ER stands on its own as an oscillator, independent of KAMA. Traders use it to classify market regimes (trending vs. ranging) and to filter trade entries: take trend-following signals when ER is high, take mean-reversion signals when ER is low.

## What It Measures and Why It Matters

ER answers a specific question: over the last $N$ bars, how much of the total price movement was directional? If price rose 10 points but the cumulative absolute bar-to-bar changes totaled 10 points, the movement was perfectly efficient (ER = 1). If the cumulative changes totaled 100 points to achieve the same 10-point move, ER = 0.1.

This makes ER a regime classifier. High ER values (above 0.6) indicate strong directional trends with minimal retracement. Low ER values (below 0.3) indicate consolidation, choppy markets, or range-bound conditions. The crossover between these zones is where most adaptive strategies make their decisions.

Unlike ADX, which measures trend strength through directional movement calculations involving highs and lows, ER uses only closing prices and simple arithmetic. The simplicity is a feature: fewer assumptions, fewer opportunities for the math to mislead.

## Mathematical Foundation

### Core Formula

$$
\text{Signal}_t = |P_t - P_{t-N}|
$$

$$
\text{Noise}_t = \sum_{i=1}^{N} |P_i - P_{i-1}|
$$

$$
\text{ER}_t = \frac{\text{Signal}_t}{\text{Noise}_t}
$$

where:

- $P_t$ = current price (close)
- $N$ = lookback period (default 10)
- $\text{Signal}$ = absolute net price change over the period
- $\text{Noise}$ = sum of absolute bar-to-bar changes over the period

### Parameter Mapping

| Parameter | Symbol | Default | Constraint |
|-----------|--------|---------|------------|
| `period` | $N$ | 10 | $N \geq 1$ |

### Warmup Period

$$
W = N + 1
$$

The close buffer requires $N + 1$ values to compute $|P_t - P_{t-N}|$; the noise buffer requires $N$ absolute changes.

## Architecture & Physics

### 1. Dual Circular Buffer Design

Two `RingBuffer` instances provide O(1) per-bar computation:

- **Close buffer** (capacity $N + 1$): stores source values. Signal = $|\text{newest} - \text{oldest}|$.
- **Noise buffer** (capacity $N$): stores $|P_i - P_{i-1}|$ values. Running sum tracks total noise.

### 2. Running Noise Sum

Instead of re-summing $N$ absolute changes each bar, the implementation maintains a running sum: subtract the oldest absolute change (about to be evicted), add the newest. This reduces streaming complexity from O(N) to O(1).

### 3. State Management

A `record struct State` holds `NoiseSum`, `PrevValue`, `LastValid`, and `Count`. The `_state` / `_p_state` pattern supports bar correction: `isNew=true` snapshots state before advancing; `isNew=false` restores the snapshot and recomputes.

### 4. Edge Cases

| Condition | Behavior |
|-----------|----------|
| `period <= 0` | `ArgumentException` with `nameof(period)` |
| `NaN` / `Infinity` input | Substitutes last valid value |
| Zero noise (flat market) | Returns $0.0$ |
| Output domain | Clamped to $[0, 1]$ via `Math.Clamp` |

## Interpretation and Signals

### Signal Zones

| Zone | Condition | Interpretation |
|------|-----------|----------------|
| Strong trend | ER > 0.6 | Price moving efficiently; trend-following strategies favored |
| Moderate | 0.3 - 0.6 | Transitional; trend may be forming or fading |
| Choppy/Range | ER < 0.3 | High noise relative to direction; mean-reversion strategies favored |

### Signal Patterns

- **Regime filter**: Use ER > 0.5 as a gate for trend-following entries. Below 0.5, switch to range-bound strategies or stand aside.
- **KAMA integration**: Feed ER into Kaufman's smoothing constant formula: $\text{SC} = [\text{ER} \times (\text{fast} - \text{slow}) + \text{slow}]^2$.
- **Divergence**: Rising ER with declining price (or vice versa) can signal a new trend emerging from consolidation.

### Practical Notes

ER works best as a filter, not a standalone trading signal. Pair it with a trend indicator for direction (e.g., SMA slope) and use ER to decide how aggressively to follow that direction. Extremely low ER readings often precede breakouts, as tight consolidation compresses the noise.

## Related Indicators

- [**KAMA**](../../trends_IIR/kama/Kama.md): Kaufman's Adaptive Moving Average, which uses ER as its core input.
- [**Inertia**](../inertia/Inertia.md): Smoothed regression slope, another approach to measuring trend efficiency.
- [**Fisher**](../fisher/Fisher.md): Transforms price position into a Gaussian distribution, different approach to regime detection.

## Validation

| Library | Batch | Streaming | Span | Notes |
|---------|:-----:|:---------:|:----:|-------|
| **TA-Lib** | -- | -- | -- | No direct ER function |
| **Skender** | -- | -- | -- | Not available as standalone |
| **Tulip** | -- | -- | -- | Not available |
| **Ooples** | -- | -- | -- | Not available |

ER is validated indirectly through KAMA tests and internal consistency checks across all four API modes (batch, streaming, span, eventing).

## Performance Profile

### Key Optimizations

- **O(1) streaming**: Running noise sum avoids re-scanning the window each bar.
- **Zero allocation**: Pre-allocated `RingBuffer` instances and `record struct State`.
- **Dual buffer architecture**: Separates close history from noise history for independent O(1) access.
- **Aggressive inlining**: `Update` and `Batch` decorated with `[MethodImpl(AggressiveInlining)]`.

### Operation Count (Streaming Mode)

| Operation | Count per bar |
|-----------|---------------|
| ABS | 2 (signal + noise change) |
| SUB | 3 (signal diff, noise diff, running sum adjust) |
| ADD | 1 (running sum) |
| DIV | 1 (ER = signal/noise) |
| Clamp | 1 |
| NaN check | 1 |
| **Total** | **~9 ops** |

## Common Pitfalls

1. **Warmup requires $N + 1$ bars**: The close buffer needs one extra bar beyond the period to compute the net price change. `IsHot` becomes `true` when the close buffer is full.
2. **Zero noise does not mean trending**: Zero noise means price has not moved at all bar-to-bar over the window. ER returns $0$, not $1$. A perfectly flat market is not trending.
3. **ER is not directional**: ER = 0.8 tells you the market is trending efficiently. It does not tell you which direction. Always pair with a directional indicator.
4. **Short periods amplify noise**: Period $< 5$ makes ER excessively reactive. The default of 10 balances responsiveness and stability.
5. **Not suitable for mean-reversion entry**: ER measures efficiency, not overbought/oversold. Low ER signals a choppy market, not a reversal point.
6. **Bar correction cost**: `isNew=false` restores state and recomputes. Acceptable for infrequent corrections; not designed for high-frequency bar rewrites.

## References

- Kaufman, P. *Trading Systems and Methods*, 5th ed. John Wiley & Sons, 2013.
- Kaufman, P. *Smarter Trading: Improving Performance in Changing Markets*. McGraw-Hill, 1995.
- Achelis, S. B. *Technical Analysis from A to Z*. McGraw-Hill, 2000.
