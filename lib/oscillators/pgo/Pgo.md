# PGO: Pretty Good Oscillator

> *Good enough to trade, honest enough not to pretend otherwise.*

| Property | Value |
|----------|-------|
| **Category** | Oscillator |
| **Inputs** | High, Low, Close (TBar) |
| **Parameters** | `period` (default 14) |
| **Outputs** | PGO value (`Last`) |
| **Output range** | Unbounded (typically -5 to +5, in ATR multiples) |
| **Warmup period** | `period` |

### Key takeaways

- Measures price displacement from its SMA, **normalized by ATR**, producing a dimensionless ratio comparable across instruments and timeframes.
- A reading of +2.0 means price is two ATRs above the mean; -3.0 means three ATRs below. The same threshold carries the same statistical weight regardless of price level.
- Uses **True Range** (which captures gap risk) rather than standard deviation, making it more responsive to overnight gaps and limit moves than Bollinger-based alternatives.
- Single-parameter design (both SMA and ATR share the same period) keeps configuration simple while the ATR denominator automatically adapts to volatility regimes.
- EMA-based ATR with **warmup compensation** ensures accurate normalization from the first bar.

## Historical Context

Mark Johnson introduced the Pretty Good Oscillator in the late 1990s as a practical alternative to oscillators that produce instrument-dependent readings. The core insight: dividing by ATR creates a dimensionless ratio. A $500 stock and a $2 penny stock can both produce a PGO of +3.0, and that reading carries the same statistical meaning. The deliberately modest name reflects Johnson's positioning: not the ultimate oscillator, but a reliable workhorse that normalizes displacement by realized volatility.

The indicator shares conceptual DNA with z-scores and Bollinger %B but makes a different normalization choice. Bollinger uses standard deviation, which treats upside and downside gaps identically and ignores the previous close. PGO uses ATR, which incorporates the gap between the previous close and the current bar's range. In markets where overnight gaps represent real risk (futures, individual stocks), this distinction matters.

## What It Measures and Why It Matters

PGO answers a simple question: "How far is price from its average, measured in units of typical bar-to-bar movement?" A reading of +3.0 means price has moved three typical ranges above its mean. This is statistically unusual regardless of whether the instrument is a penny stock or a major index, which is the indicator's principal advantage over raw price-minus-average oscillators.

The ATR denominator automatically adjusts for volatility regime changes. During quiet periods, ATR contracts and the same price displacement produces a larger PGO reading, flagging the move as statistically significant. During volatile periods, ATR expands and price needs to move further from the mean to register the same PGO level. This self-calibration means fixed overbought/oversold thresholds (±3.0) are more stable across market conditions than thresholds on non-normalized oscillators.

## Mathematical Foundation

### Core Formula

Simple Moving Average of Close:

$$SMA_t = \frac{1}{N} \sum_{i=0}^{N-1} Close_{t-i}$$

True Range:

$$TR_t = \max(H_t - L_t, \; |H_t - C_{t-1}|, \; |L_t - C_{t-1}|)$$

Average True Range via EMA with warmup compensation:

$$ATR_t = EMA(TR, N) \cdot \frac{1}{1 - (1-\alpha)^t}$$

where $\alpha = 1/N$.

Pretty Good Oscillator:

$$PGO_t = \frac{Close_t - SMA_t}{ATR_t}$$

When $ATR = 0$ (constant price), PGO returns 0.0 by convention.

### Parameter Mapping

| Parameter | Formula role | Default | Constraint |
|-----------|-------------|---------|------------|
| `period` | Lookback for SMA and smoothing factor for ATR EMA | 14 | > 0 |

### Warmup Period

$$W = period$$

Default configuration (period=14) warms up in 14 bars.

## Architecture & Physics

### 1. SMA via Running Sum

The [`RingBuffer`](lib/oscillators/pgo/Pgo.cs:26) maintains a circular buffer of close prices. [`SmaSum`](lib/oscillators/pgo/Pgo.cs:29) tracks the running total with O(1) incremental updates: subtract the oldest value, add the new value.

### 2. ATR via Compensated EMA

True Range is computed from High, Low, Close, and previous Close using three comparisons. The EMA of TR uses [`Math.FusedMultiplyAdd`](lib/oscillators/pgo/Pgo.cs:158) for the `alpha * (tr - ema) + ema` pattern. During warmup, the geometric decay factor $e = e \times (1 - \alpha)$ provides bias correction.

### 3. Single-Parameter Design

Both SMA lookback and ATR smoothing use the same `period`, which means a single parameter controls the entire indicator. This simplifies optimization and reduces overfitting risk compared to dual-parameter designs.

### 4. Dual Update Overloads

[`Update(TBar)`](lib/oscillators/pgo/Pgo.cs:115) provides full OHLC context for proper True Range computation. [`Update(TValue)`](lib/oscillators/pgo/Pgo.cs:176) creates a synthetic bar with H=L=C=value, producing TR=0, which is documented as suboptimal.

### 5. Edge Cases

- **Zero ATR**: When all bars have identical OHLC, ATR = 0 and PGO returns 0.0.
- **NaN/Infinity inputs**: Last-valid substitution for close; high/low fall back to close.
- **No previous close**: First bar uses close as the previous close, producing TR = High - Low only.
- **Correction (isNew=false)**: SMA sum is recalculated from the buffer to maintain accuracy.

## Interpretation and Signals

### Signal Zones

| Zone | PGO value | Meaning |
|------|-----------|---------|
| Extreme overbought | > +3.0 | Price is 3+ ATRs above mean; statistically extended |
| Overbought | +2.0 to +3.0 | Above-average displacement; caution warranted |
| Neutral | -1.0 to +1.0 | Normal range; no directional bias |
| Oversold | -3.0 to -2.0 | Below-average displacement; caution warranted |
| Extreme oversold | < -3.0 | Price is 3+ ATRs below mean; statistically depressed |

### Signal Patterns

- **Zero-line crossover (bullish)**: PGO crosses above zero; price crosses above SMA.
- **Zero-line crossover (bearish)**: PGO crosses below zero; price crosses below SMA.
- **Bullish divergence**: Price makes lower lows while PGO makes higher lows; ATR-normalized displacement is contracting despite new price lows.
- **Bearish divergence**: Price makes higher highs while PGO makes lower highs; displacement relative to volatility is shrinking.
- **Extreme reversion**: PGO beyond ±3.0 reverting toward zero suggests mean reversion is underway.

### Practical Notes

- The ±3.0 thresholds are guidelines, not hard rules. Fat-tailed distributions (common in finance) produce more extreme readings than Gaussian models suggest.
- In strong trends, PGO may persist at ±2 to ±4 for extended periods. This confirms trend strength rather than signaling imminent reversal.
- PGO requires proper OHLC data for meaningful ATR computation. Feeding close-only data (H=L=C) produces TR=0 and a meaningless oscillator.

## Related Indicators

- [**Bollinger %B**](../bbb/Bbb.md): Normalizes price position by standard deviation (Bollinger Bands) rather than ATR.
- [**CFO**](../cfo/Cfo.md): Normalizes regression residual by price level rather than volatility.
- [**Williams %R**](../willr/Willr.md): Normalizes price position within the high-low range, bounded to [-100, 0].

## Validation

No external library provides a PGO implementation. Validation uses component identity checks and cross-mode consistency.

| Check | Status | Notes |
|-------|--------|-------|
| Streaming vs Batch vs Span | ✅ | All three modes agree within 1e-10 |
| Constant price | ✅ | Constant OHLC bars produce PGO = 0.0 |
| Rising prices | ✅ | Produces positive PGO |
| Falling prices | ✅ | Produces negative PGO |
| Component identity | ✅ | Manual SMA + ATR computation matches PGO within 1e-10 |
| Multi-period consistency | ✅ | Different periods produce distinct results |
| Determinism | ✅ | Two identical runs produce bit-identical results |

## Performance Profile

### Key Optimizations

- **O(1) SMA update**: Running sum with RingBuffer avoids re-scanning the window.
- **FMA in ATR EMA**: `Math.FusedMultiplyAdd(alpha, tr - ema, ema)` for the EMA update.
- **Precomputed constants**: `_alpha` and `_decay` computed once in constructor.
- **Warmup compensation**: Geometric decay factor corrects EMA bias without extra state.
- **State copy pattern**: `_s`/`_ps` record struct for zero-allocation bar correction.

### Operation Count (Streaming Mode)

| Operation | Count per bar |
|-----------|--------------|
| Comparisons | 3 (TR components) |
| Additions | 3 (SMA sum, EMA, PGO) |
| Subtractions | 2 (oldest SMA removal, close - SMA) |
| Multiplications | 1 (warmup compensator) |
| FMA calls | 1 (ATR EMA) |
| Divisions | 2 (SMA average, PGO normalization) |
| Abs | 2 (TR components) |
| Max | 2 (TR selection) |

### SIMD Analysis (Batch Mode)

| Aspect | Status |
|--------|--------|
| SMA running sum | Scalar (sequential accumulation) |
| TR computation | Scalar (3 comparisons + abs per bar) |
| ATR EMA | Scalar (IIR recursion, sequential dependency) |
| PGO division | Scalar (data-dependent divisor) |
| Vectorization potential | Low — IIR chain and conditional TR prevent SIMD |

## Common Pitfalls

1. **Using without OHLC data.** PGO requires High/Low/Close for True Range. Feeding close-only data produces TR=0, making ATR=0 and PGO=0 (worthless). Always provide full bar data.
2. **Fixed thresholds on fat-tailed data.** The ±3.0 levels assume approximately normal displacement. Financial returns have fat tails; PGO can reach ±5 or beyond during tail events. Calibrate thresholds per instrument.
3. **Fading strong trends.** PGO at +3.0 in a strong uptrend is a sign of strength, not an automatic sell signal. Combine with trend direction indicators before trading mean reversion.
4. **Comparing across different periods.** PGO(14) and PGO(50) are not directly comparable. Longer periods produce smoother, smaller-magnitude readings.
5. **Ignoring SMA lag.** SMA introduces (N-1)/2 bars of lag. In fast-moving markets, the SMA component drags the PGO reading, making it slow to register sharp reversals.
6. **Assuming ATR is volatility.** ATR measures range, not statistical volatility. It can underestimate risk in markets with large intrabar reversals that end near unchanged.

## FAQ

**Q: Why use ATR instead of standard deviation for normalization?**
A: ATR captures gap risk (via True Range's inclusion of the previous close) that standard deviation misses. In overnight-gap-prone instruments, ATR gives a more complete picture of actual price movement than close-to-close standard deviation.

**Q: What happens when I feed TValue instead of TBar?**
A: The `Update(TValue)` overload creates a synthetic bar with H=L=C=value, which produces TR=0. This means ATR decays toward zero and PGO becomes unreliable. Always prefer the `Update(TBar)` overload for meaningful results.

**Q: How does PGO compare to Bollinger %B?**
A: Both normalize price displacement against a volatility measure. Bollinger %B uses standard deviation (Gaussian assumption, ignores gaps) and outputs [0, 1] typically. PGO uses ATR (captures gaps) and outputs unbounded ATR multiples. PGO is more suitable for gapping instruments; %B is more suitable when you want a bounded indicator.

## References

- Johnson, Mark. "Pretty Good Oscillator." Technical analysis community publication.
- Wilder, J. Welles. *New Concepts in Technical Trading Systems*. Trend Research, 1978. (ATR foundation)
- [PineScript reference](pgo.pine)
