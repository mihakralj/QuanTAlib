# BBB: Bollinger %B

> "Price oscillates, but %B tells you where it lives inside the band." -- John Bollinger, paraphrased

| Property     | Value |
|--------------|-------|
| Category     | Oscillator |
| Inputs       | Source (close) |
| Parameters   | `period` (int, default: 20, valid: > 0), `multiplier` (double, default: 2.0, valid: > 0) |
| Outputs      | double (single value) |
| Output range | Typically 0 to 1, can exceed bounds on breakouts |
| Warmup       | `period` bars (default: 20) |

### Key takeaways

- %B normalizes a price's position within Bollinger Bands to a 0-1 scale: 0 at the lower band, 1 at the upper band, 0.5 at the SMA midline.
- Primary use: identifying overbought/oversold conditions relative to recent volatility, and detecting band breakouts when %B exceeds 0 or 1.
- Unlike Stochastic (which normalizes against a fixed high-low range), %B adapts to volatility because the bands themselves expand and contract with standard deviation.
- %B can exceed the 0-1 range: values above 1.0 indicate price above the upper band (breakout), values below 0.0 indicate price below the lower band (breakdown).
- When band width collapses to zero (constant price), %B returns 0.5 (neutral) rather than producing a division-by-zero error.

## Historical Context

John Bollinger introduced Bollinger Bands in the 1980s and formalized them in *Bollinger on Bollinger Bands* (2001). While the bands themselves (upper, lower, midline) are widely implemented, %B -- the normalized oscillator derived from the bands -- is less commonly found as a standalone indicator in third-party libraries. Bollinger designed %B specifically to convert the visual "where is price within the bands?" question into a quantitative answer suitable for systematic trading rules.

Most libraries implement Bollinger Bands as a channel indicator (upper/lower/middle), and %B is typically computed as a derived output. Skender implements it as the `PercentB` property of `GetBollingerBands()`. TA-Lib, Tulip, and Ooples provide the bands but not %B directly. QuanTAlib implements %B as a standalone oscillator (`Bbb`) with its own O(1) rolling variance computation, avoiding the overhead of constructing the full band output.

The formula is straightforward: `%B = (Price - Lower) / (Upper - Lower)`. But the implementation matters because computing standard deviation naively over a sliding window is O(N) per update. QuanTAlib uses the algebraic identity `Var(X) = E[X²] - E[X]²` with running sums to achieve O(1) per update, with periodic resynchronization to limit floating-point drift.

## What It Measures and Why It Matters

%B answers a specific question: where does the current price sit relative to its recent volatility envelope? A reading of 0.5 means price is at the SMA midline. A reading of 0.8 means price is 80% of the way from the lower band to the upper band. A reading of 1.2 means price has exceeded the upper band by 20% of the band width.

This normalization is what makes %B useful. Raw Bollinger Bands are visual tools -- you can see whether price is "near the top" or "near the bottom" of the envelope, but quantifying "near" is subjective. %B provides an exact number. A systematic rule like "buy when %B drops below 0.0 and reverses back above" is precise in a way that "buy when price touches the lower band" is not, because %B accounts for the band width.

The limitation is that %B inherits the lag of its SMA component. With a 20-period lookback, %B reflects the trailing 20-bar price distribution. In fast-moving markets, the bands lag behind price, causing %B readings above 1.0 or below 0.0 that persist until the bands catch up. This is informative (it confirms a breakout is underway) but not predictive.

## Mathematical Foundation

### Core Formula

%B is computed in four steps:

**Step 1: Simple Moving Average (Basis)**

$$
\text{Basis}_t = \frac{1}{N} \sum_{i=0}^{N-1} P_{t-i}
$$

**Step 2: Rolling Standard Deviation**

$$
\sigma_t = \sqrt{\frac{1}{N} \sum_{i=0}^{N-1} P_{t-i}^2 - \text{Basis}_t^2}
$$

**Step 3: Bollinger Bands**

$$
\text{Upper}_t = \text{Basis}_t + k \cdot \sigma_t
$$

$$
\text{Lower}_t = \text{Basis}_t - k \cdot \sigma_t
$$

**Step 4: Percent B**

$$
\%B_t = \frac{P_t - \text{Lower}_t}{\text{Upper}_t - \text{Lower}_t}
$$

where:

- $P_t$ = close price at bar $t$
- $N$ = lookback period (default 20)
- $k$ = standard deviation multiplier (default 2.0)

When $\text{Upper}_t = \text{Lower}_t$ (bandwidth is zero), $\%B = 0.5$.

### Parameter Mapping

| Parameter | Symbol | Default | Constraint |
|-----------|--------|---------|------------|
| `period` | $N$ | 20 | $N > 0$ |
| `multiplier` | $k$ | 2.0 | $k > 0$ |

### Warmup Period

$$
\text{WarmupPeriod} = N
$$

The `IsHot` flag activates when the internal `RingBuffer` is full (after `period` bars have been added).

## Architecture & Physics

BBB uses a `record struct State` with O(1) rolling variance via the `E[X²] - E[X]²` identity.

```
Source ──→ RingBuffer(period) ──→ Rolling Sum/SumSq ──→ Mean, StdDev ──→ Bands ──→ %B
```

### 1. Rolling Variance Without Division-by-N Recomputation

The state tracks `Sum` and `SumSq` (sum of squares). When a new value enters the ring buffer, the oldest value's contribution is subtracted and the new value's contribution is added. Mean and variance are computed from these running totals in constant time. Variance uses `Max(0, SumSq/N - Mean²)` to guard against negative values from floating-point rounding.

### 2. Periodic Resynchronization

Floating-point drift accumulates in running sums. Every 1,000 ticks (controlled by `ResyncInterval`), the sums are recalculated from scratch by iterating the ring buffer. This bounds the maximum accumulated error to approximately 1,000 additions' worth of ULP drift.

### 3. Bar Correction

`isNew=true` saves `_p_state = _state` before advancing. `isNew=false` restores `_state = _p_state`, updates the newest buffer entry via `UpdateNewest`, and recalculates sums from scratch to ensure correctness after the replacement.

### 4. Edge Cases

- **NaN/Infinity inputs**: Substituted with `LastValid` (or 0.0 if no valid input has been seen).
- **Zero bandwidth**: When all prices in the window are identical, `Upper = Lower` and %B returns 0.5 (neutral).
- **Empty buffer**: Returns 0.5 before any data arrives.

## Interpretation and Signals

### Signal Zones

| Zone | Level | Interpretation |
|------|-------|----------------|
| Upper band breakout | %B > 1.0 | Price above upper band; strong momentum or volatility expansion |
| Overbought zone | %B > 0.8 | Price near upper band; potential mean reversion |
| Neutral | %B ≈ 0.5 | Price at SMA midline |
| Oversold zone | %B < 0.2 | Price near lower band; potential mean reversion |
| Lower band breakdown | %B < 0.0 | Price below lower band; strong downward momentum |

### Signal Patterns

- **Band walk**: %B staying above 0.8 (or below 0.2) for extended periods indicates a trending market, not an overbought/oversold condition. Mean-reversion strategies fail during band walks.
- **W-bottom**: %B drops below 0.0, recovers above 0.0, pulls back to near 0.2 without going negative again, then rallies. The second low being higher (in %B terms) confirms a reversal pattern.
- **M-top**: Mirror of W-bottom above the upper band. %B exceeds 1.0, drops, approaches 1.0 again without exceeding it, then declines.

### Practical Notes

%B is most effective as a mean-reversion indicator in ranging markets and as a breakout confirmation in trending markets. The challenge is knowing which regime you are in. Pair %B with Bandwidth (BBS) to distinguish: narrow bandwidth + %B breakout suggests a trend initiation (Bollinger Squeeze), while wide bandwidth + extreme %B suggests mean reversion is more likely. The default 20/2.0 parameters work well on daily charts; tighter bands (1.5 multiplier) increase signal frequency at the cost of more false breakouts.

## Related Indicators

- **[BBS](../bbs/Bbs.md)**: Bollinger Bandwidth. Measures the width of the bands (volatility). Use BBS alongside %B to distinguish squeeze breakouts from range-bound oscillation.
- **[Stoch](../stoch/Stoch.md)**: Stochastic Oscillator. Similar normalization concept but uses highest-high/lowest-low instead of standard deviation bands. Stochastic is bounded [0, 100]; %B is not bounded.
- **[RSI](../../momentum/rsi/Rsi.md)**: Relative Strength Index. Both identify overbought/oversold, but RSI measures relative strength of up vs down moves, while %B measures position relative to a volatility envelope.

## Validation

Validated against external libraries in [`Bbb.Validation.Tests.cs`](Bbb.Validation.Tests.cs).

| Library | Status | Notes |
|---------|:------:|-------|
| **Skender** | ✓ | `GetBollingerBands().PercentB`, multiple periods (5, 10, 20, 50, 100) |
| **Self-consistency** | ✓ | Streaming, batch, and span modes agree to 1e-9 |
| **TA-Lib** | -- | Provides bands only, not %B directly |
| **Tulip** | -- | Provides bands only, not %B directly |
| **Ooples** | -- | Provides bands only, not %B directly |

Skender validation tests run across five different period settings (5, 10, 20, 50, 100) with a 2.0 multiplier, comparing QuanTAlib's `Bbb` output against Skender's `PercentB` property.

## Performance Profile

### Key Optimizations

- **O(1) rolling variance**: Running `Sum` and `SumSq` avoid recomputing over the entire window each update.
- **Resync guard**: Every 1,000 ticks, sums are recalculated from the ring buffer to bound floating-point drift.
- **Aggressive inlining**: `Update`, `Handle`, and `Batch(Span)` are decorated with `[MethodImpl(MethodImplOptions.AggressiveInlining)]`.
- **SkipLocalsInit**: Class-level `[SkipLocalsInit]` avoids zero-initialization overhead.
- **State local copy pattern**: `_state`/`_p_state` record struct enables rollback for bar correction.

### Operation Count (Streaming Mode)

| Operation | Count | Cost (cycles) | Subtotal |
|-----------|------:|:-------------:|:--------:|
| ADD/SUB   | 4     | 1             | 4        |
| MUL       | 2     | 3             | 6        |
| DIV       | 2     | 15            | 30       |
| SQRT      | 1     | 15            | 15       |
| CMP       | 2     | 1             | 2        |
| **Total** | **11** | --           | **~57**  |

The `Sqrt` and two DIVs (mean, %B normalization) dominate the per-bar cost.

### SIMD Analysis (Batch Mode)

| Operation | Vectorizable? | Reason |
|-----------|:-------------:|--------|
| Rolling sum maintenance | No | Sequential ring buffer dependency |
| Mean/variance computation | No | Depends on running sums |
| %B normalization | No | Depends on per-element mean and stddev |

BBB is fully sequential: the rolling sum depends on the previous state, and all subsequent computations depend on the rolling sum. No SIMD vectorization is possible for the core algorithm.

## Common Pitfalls

1. **%B is not bounded to [0, 1]**: Unlike Stochastic or RSI, %B regularly exceeds 1.0 or drops below 0.0. Trading systems that assume a fixed range will mishandle breakout conditions.

2. **Warmup is 20 bars with defaults**: The ring buffer needs `period` bars to fill. Pre-warmup values use partial-window statistics, which underestimate standard deviation and produce misleading %B values.

3. **Band walks create false signals**: During strong trends, %B can persist above 0.8 or below 0.2 for dozens of bars. Mean-reversion trades triggered by extreme %B values during trends produce losses. Use Bandwidth (BBS) or a trend filter to avoid.

4. **Zero-bandwidth edge case**: If all prices in the window are identical, both bands collapse to the mean, bandwidth is zero, and %B returns 0.5. This is mathematically correct (the price is at the center of a zero-width band) but can surprise logic that expects %B to reflect price movement.

5. **Floating-point drift in running sums**: The O(1) running sum accumulates rounding errors over thousands of updates. The implementation resyncs every 1,000 ticks, but validation tests should use moderate dataset sizes (< 10,000 bars) for exact comparisons.

6. **isNew=false triggers full recalculation**: Bar correction cannot incrementally update the running sums after replacing a value, so it recalculates from the ring buffer. In high-frequency scenarios with many corrections per bar, this can degrade to O(N) per update.

## FAQ

**Q: Why does QuanTAlib implement %B as a separate indicator instead of a property on Bollinger Bands?**
A: Standalone implementation avoids forcing users to compute upper/lower/middle bands when they only need %B. The O(1) rolling variance implementation is self-contained and does not depend on a full Bollinger Bands calculation.

**Q: How does %B differ from Stochastic?**
A: Both normalize price position, but Stochastic uses the highest-high and lowest-low over the lookback period, while %B uses SMA ± k × standard deviation. Stochastic is bounded [0, 100]; %B is unbounded. Stochastic responds to price extremes; %B responds to volatility.

**Q: What multiplier should I use?**
A: The default 2.0 captures approximately 95% of price action within the bands (assuming normal distribution). Use 1.5 for tighter bands (more signals, more false positives) or 2.5-3.0 for wider bands (fewer signals, higher confidence).

## References

- Bollinger, J. (2001). *Bollinger on Bollinger Bands*. McGraw-Hill. Chapter on %B and Bandwidth.
- [Investopedia: Bollinger Bands](https://www.investopedia.com/terms/b/bollingerbands.asp) -- overview of bands, %B, and Bandwidth.
- [StockCharts: Bollinger %B](https://school.stockcharts.com/doku.php?id=technical_indicators:bollinger_band_pct_b) -- %B interpretation and signals.
- [PineScript reference](bbb.pine) -- original implementation source.
