# FI: Force Index

> "Volume is the steam that makes the locomotive run. Price shows direction; volume shows conviction." -- Alexander Elder

| Property | Value |
|----------|-------|
| **Category** | Oscillator |
| **Inputs** | Raw force values (typically $\Delta\text{Close} \times \text{Volume}$) |
| **Parameters** | `period` (default 13) |
| **Outputs** | Single series (EMA-smoothed Force Index) |
| **Output range** | Unbounded (centered around 0) |
| **Warmup** | `period` bars |

### Key takeaways

- Combines price change and volume into a single measure of buying/selling pressure.
- Raw force = (Close $-$ PrevClose) $\times$ Volume; the indicator applies EMA smoothing to the raw force.
- Positive FI means buyers dominate; negative FI means sellers dominate. Magnitude reflects conviction.
- The Quantower adapter handles OHLCV bar decomposition; the core `Update(TValue)` expects pre-computed raw force.
- Uses EMA with exponential warmup compensation for bias-free early values and FMA optimization.

## Historical Context

Alexander Elder introduced the Force Index in *Trading for a Living* (1993). Elder wanted an indicator that captured both the direction and the intensity of market moves. Price change alone tells you direction; volume alone tells you activity. Multiplying the two produces "force": a directional measure weighted by participation.

The raw Force Index is noisy. A single high-volume bar creates a spike that dwarfs surrounding values. Elder's solution was to smooth it with an EMA. A short-period EMA (2) provides a sensitive, fast-reacting version for short-term traders. A longer-period EMA (13) provides a smoother version for identifying intermediate-term trend strength.

The Force Index occupies a niche between pure momentum indicators (ROC, Momentum) and pure volume indicators (OBV, ADL). It explicitly fuses both dimensions, which makes it more informative than either alone but also more dependent on volume data quality.

## What It Measures and Why It Matters

Force Index measures the conviction behind price moves. A 5-point rise on 1 million shares has more "force" than a 5-point rise on 10,000 shares. The indicator quantifies this intuition.

When smoothed with a 13-period EMA, FI reveals the underlying trend of buying or selling pressure. Persistent positive FI means buyers are consistently dominant. A transition from positive to negative signals a shift in control from buyers to sellers. The zero-line crossover is the primary signal.

FI is particularly useful for confirming breakouts. A price breakout accompanied by rising FI suggests genuine buying interest. A breakout with declining FI suggests the move lacks volume support and may fail.

## Mathematical Foundation

### Core Formula

$$
\text{RawForce}_t = (C_t - C_{t-1}) \times V_t
$$

$$
\text{FI}_t = \text{EMA}(\text{RawForce}, N)_t
$$

where:

- $C_t$ = close price at bar $t$
- $V_t$ = volume at bar $t$
- $N$ = EMA smoothing period (default 13)

### EMA with Warmup Compensation

$$
\alpha = \frac{2}{N + 1}, \quad \beta = 1 - \alpha
$$

$$
\text{EMA}_t^{\text{raw}} = \text{FMA}(\text{EMA}_{t-1}^{\text{raw}}, \beta, \alpha \cdot \text{RawForce}_t)
$$

During warmup:

$$
e_t = e_{t-1} \cdot \beta, \quad e_0 = 1
$$

$$
\text{FI}_t = \frac{\text{EMA}_t^{\text{raw}}}{1 - e_t}
$$

### Parameter Mapping

| Parameter | Symbol | Default | Constraint |
|-----------|--------|---------|------------|
| `period` | $N$ | 13 | $N \geq 1$ |

### Warmup Period

$$
W = N
$$

## Architecture & Physics

### 1. Input Model

The core `Update(TValue)` method expects pre-computed raw force as input. In the Quantower adapter, the raw force is computed from OHLCV bars: `(close - prevClose) * volume`. This separation keeps the core indicator clean and reusable for any pre-computed force-like signal.

### 2. EMA with FMA

The IIR recursion uses `Math.FusedMultiplyAdd(ema, beta, alpha * value)` for a single fused operation, eliminating intermediate rounding and improving throughput.

### 3. Warmup Compensation

Exponential decay tracking ($e_t$) provides bias-free early values. When $e_t$ drops below $10^{-10}$, the warmup flag is cleared and the indicator switches to standard EMA output.

### 4. State Management

A `record struct State` holds `Ema`, `E` (warmup decay), `Warmup` flag, `Index`, and `LastValid`. The `_s` / `_ps` local-copy pattern supports bar correction.

### 5. Edge Cases

| Condition | Behavior |
|-----------|----------|
| `period < 1` | `ArgumentException` with `nameof(period)` |
| `NaN` / `Infinity` input | Substitutes last valid value |
| First bar | EMA seeded with the first input value |
| Zero volume | Raw force = 0 (no conviction), EMA converges to zero |

## Interpretation and Signals

### Signal Zones

| Zone | Condition | Interpretation |
|------|-----------|----------------|
| Bullish | FI > 0 | Buyers in control; buying pressure dominates |
| Bearish | FI < 0 | Sellers in control; selling pressure dominates |
| Neutral | FI near 0 | Balance between buyers and sellers |

### Signal Patterns

- **Zero-line crossover**: FI crossing from negative to positive signals a shift to buying dominance. From positive to negative signals selling dominance.
- **Divergence**: Price making new highs while FI peaks decline indicates weakening buying conviction. Price making new lows while FI troughs rise indicates weakening selling pressure.
- **Spike analysis**: Large FI spikes identify climactic buying or selling. These often mark short-term exhaustion points.
- **Trend confirmation**: Rising price with rising FI confirms the trend. Rising price with declining FI warns of potential reversal.

### Practical Notes

Elder recommended using two Force Index timeframes: a 2-period EMA for precise entry timing and a 13-period EMA for intermediate trend assessment. The 2-period version is extremely sensitive and best used with a longer-term trend filter. The 13-period version provides smoother signals suitable for position trading.

## Related Indicators

- [**Eri**](../eri/Eri.md): Elder Ray Index, another Elder creation that measures buying/selling pressure via High/Low relative to EMA, without volume.
- [**Efi**](../../volume/efi/Efi.md): Elder Force Index in the volume category, which handles full OHLCV bar input.
- [**Obv**](../../volume/obv/Obv.md): On-Balance Volume, cumulative volume-direction indicator without price-change weighting.
- [**Mfi**](../../volume/mfi/Mfi.md): Money Flow Index, volume-weighted RSI that also combines price and volume.

## Validation

| Library | Batch | Streaming | Span | Notes |
|---------|:-----:|:---------:|:----:|-------|
| **TA-Lib** | -- | -- | -- | No direct FI function |
| **Skender** | -- | -- | -- | `GetForceIndex()` available but not yet validated |
| **Tulip** | -- | -- | -- | Not available |
| **Ooples** | -- | -- | -- | Not available |

Internal consistency validated across streaming, batch, span, and eventing modes.

## Performance Profile

### Key Optimizations

- **FMA in EMA**: `Math.FusedMultiplyAdd(ema, beta, alpha * value)` for the IIR recursion.
- **Precomputed constants**: `_alpha` and `_decay` are set once in the constructor.
- **Zero allocation**: `record struct State` with local-copy pattern for register promotion.
- **Aggressive inlining**: `[MethodImpl(AggressiveInlining)]` on `Update` and `Calculate`.

### Operation Count (Streaming Mode)

| Operation | Count per bar |
|-----------|---------------|
| FMA | 1 (EMA update) |
| MUL | 1 (alpha * value) |
| NaN check | 1 |
| Conditional | 1 (warmup check) |
| MUL (warmup) | 1 (e *= beta, when active) |
| DIV (warmup) | 1 (1 / (1 - e), when active) |
| **Total** | **~4-6 ops** |

## Common Pitfalls

1. **Input is pre-computed force**: The core `Update(TValue)` expects `(close - prevClose) * volume` as input, not raw close prices. Passing raw close prices produces meaningless results. Use the Quantower adapter or compute raw force before calling.
2. **Volume data quality**: FI is only as good as the volume data. In forex markets where volume is tick-based rather than share-based, FI values are less reliable.
3. **Unbounded output**: FI has no fixed range. Visual scaling varies dramatically between instruments with different volumes and price ranges. Direct comparison across instruments is not meaningful.
4. **Short-period noise**: A 2-period EMA FI is extremely volatile. It is meant for intrabar precision, not standalone signals.
5. **Zero-line is not a standalone signal**: Crossing zero is necessary but not sufficient. Elder required trend confirmation (EMA slope) before acting on FI crossovers.
6. **Warmup period affects early values**: The exponential warmup compensator provides mathematically correct early values, but practical reliability improves after $2N$ bars.

## References

- Elder, A. *Trading for a Living*. John Wiley & Sons, 1993. Chapter on Force Index.
- Elder, A. *Come Into My Trading Room*. John Wiley & Sons, 2002.
- Murphy, J. J. *Technical Analysis of the Financial Markets*. New York Institute of Finance, 1999.
- Achelis, S. B. *Technical Analysis from A to Z*. McGraw-Hill, 2000.
