# ERI: Elder Ray Index

> "The job of the indicator is to separate the bulls from the bears. If you can measure their power independently, you can see who is winning before the trend changes." -- Alexander Elder

| Property | Value |
|----------|-------|
| **Category** | Oscillator |
| **Inputs** | Bar series (High, Low, Close) |
| **Parameters** | `period` (default 13) |
| **Outputs** | Dual: Bull Power (primary), Bear Power (property) |
| **Output range** | Unbounded (centered around 0) |
| **Warmup** | `period` bars |

### Key takeaways

- Decomposes buying and selling pressure relative to an EMA trend line: Bull Power = High $-$ EMA, Bear Power = Low $-$ EMA.
- Primary output (`Last`) is Bull Power; Bear Power is accessible via the `BearPower` property.
- Uses EMA with exponential warmup compensation for bias-free early values.
- Bull Power positive means buyers pushed price above the trend; Bear Power negative means sellers pulled price below.
- Best used with a trend filter: take longs when EMA is rising and Bear Power is recovering from below zero.

## Historical Context

Dr. Alexander Elder introduced Bull Power and Bear Power in *Trading for a Living* (1993). Elder, a psychiatrist turned trader, designed the indicators to measure the balance of power between buyers and sellers relative to the prevailing trend, represented by an EMA.

The logic is clinical. The highest price of a bar reflects the maximum power of the bulls. The lowest price reflects the maximum power of the bears. Measuring each against the EMA (the consensus value) yields two independent readings of buying and selling pressure. Elder recommended a 13-period EMA, though any reasonable period works.

Elder Ray is typically plotted as two separate histograms below the price chart. The name "Elder Ray" is a visual metaphor: like X-rays revealing bone structure beneath flesh, the indicator reveals the hidden power structure beneath price action.

## What It Measures and Why It Matters

Bull Power measures how far above the EMA the bulls managed to push price during the bar. Positive Bull Power means buyers controlled the session. Negative Bull Power means even the high of the bar was below the trend line, a deeply bearish condition.

Bear Power measures how far below the EMA the bears managed to push price. Negative Bear Power is normal in uptrends (the low is below the average). When Bear Power turns positive, it means even the low of the bar was above the trend, an extremely bullish condition.

The two powers are complementary. Strong uptrends show large positive Bull Power and small negative Bear Power (recovering toward zero). Strong downtrends show large negative Bear Power and small positive Bull Power (declining toward zero). Divergence between price and either power reading signals potential trend exhaustion.

## Mathematical Foundation

### Core Formula

$$
\text{EMA}_t = \alpha \cdot C_t + (1 - \alpha) \cdot \text{EMA}_{t-1}
$$

$$
\text{Bull Power}_t = H_t - \text{EMA}_t
$$

$$
\text{Bear Power}_t = L_t - \text{EMA}_t
$$

where:

- $C_t$ = close price at bar $t$
- $H_t$ = high price at bar $t$
- $L_t$ = low price at bar $t$
- $\alpha = \frac{2}{N + 1}$ = EMA smoothing factor
- $N$ = period (default 13)

### Warmup Compensation

During warmup, the EMA uses exponential decay correction:

$$
e_t = e_{t-1} \cdot (1 - \alpha), \quad e_0 = 1
$$

$$
\text{EMA}_t^{\text{compensated}} = \frac{\text{EMA}_t^{\text{raw}}}{1 - e_t}
$$

This eliminates startup bias without requiring a seed SMA.

### Parameter Mapping

| Parameter | Symbol | Default | Constraint |
|-----------|--------|---------|------------|
| `period` | $N$ | 13 | $N \geq 1$ |

### Warmup Period

$$
W = N
$$

## Architecture & Physics

### 1. EMA with FMA Optimization

The core EMA update uses `Math.FusedMultiplyAdd` for the standard IIR recursion:

$$
\text{EMA}_t = \text{FMA}(\text{EMA}_{t-1}, \beta, \alpha \cdot C_t) \quad \text{where } \beta = 1 - \alpha
$$

Precomputed `_alpha` and `_decay` constants avoid repeated division in the hot path.

### 2. Multi-Channel NaN Guard

Three independent last-valid substitution channels track `Close`, `High`, and `Low` separately. A NaN high does not contaminate the close or low channels.

### 3. State Management

A `record struct State` holds `Ema`, `E` (warmup decay), `Warmup` flag, `Index`, and three `LastValid` fields plus `BearPower`. The `_s` / `_ps` local-copy pattern enables bar correction and JIT struct promotion.

### 4. TBar vs TValue Input

- `Update(TBar)`: Full H/L/C decomposition for proper Bull and Bear Power.
- `Update(TValue)`: Treats input as close with high = low = close. Useful for chaining but produces degenerate results (Bull Power = Bear Power = value $-$ EMA).

### 5. Edge Cases

| Condition | Behavior |
|-----------|----------|
| `period < 1` | `ArgumentException` with `nameof(period)` |
| `NaN` / `Infinity` input | Per-channel last-valid substitution |
| First bar | EMA = close, Bull Power = high $-$ close, Bear Power = low $-$ close |
| TValue input | Wraps as TBar with OHLC = value |

## Interpretation and Signals

### Signal Patterns

- **Bull divergence**: Price makes lower lows, but Bear Power makes higher lows. Selling pressure is weakening despite new price lows. Bullish signal.
- **Bear divergence**: Price makes higher highs, but Bull Power makes lower highs. Buying pressure is weakening despite new price highs. Bearish signal.
- **Long entry (Elder's rules)**: EMA rising, Bear Power negative but increasing, Bull Power's latest peak exceeds the prior peak.
- **Short entry (Elder's rules)**: EMA declining, Bull Power positive but decreasing, Bear Power's latest trough is lower than the prior trough.

### Practical Notes

Elder recommended combining Bull/Bear Power with a 13-period EMA slope as a trend filter. The indicator works best in trending markets where the power decomposition reveals which side is gaining or losing strength. In range-bound conditions, both powers oscillate around zero without clear directional bias.

## Related Indicators

- [**Fi**](../fi/Fi.md): Force Index, another Elder creation that measures buying/selling pressure using price change times volume.
- [**Stoch**](../stoch/Stoch.md): Also measures position within a range, but uses high-low range rather than EMA deviation.

## Validation

| Library | Batch | Streaming | Span | Notes |
|---------|:-----:|:---------:|:----:|-------|
| **TA-Lib** | -- | -- | -- | No direct ERI function |
| **Skender** | -- | -- | -- | `GetElderRay()` available but not yet validated |
| **Tulip** | -- | -- | -- | Not available |
| **Ooples** | -- | -- | -- | Not available |

Internal consistency validated across streaming, batch, span, and eventing modes.

## Performance Profile

### Key Optimizations

- **FMA in EMA**: `Math.FusedMultiplyAdd(ema, decay, alpha * close)` replaces separate multiply-and-add.
- **Local state copy**: JIT promotes `var s = _s` to registers, avoiding repeated memory loads.
- **Zero allocation**: All state in `record struct`; no heap allocations in `Update`.
- **Aggressive inlining**: `[MethodImpl(AggressiveInlining)]` on `Update(TBar)`.

### Operation Count (Streaming Mode)

| Operation | Count per bar |
|-----------|---------------|
| FMA | 1 (EMA update) |
| MUL | 1 (alpha * close) |
| SUB | 2 (Bull Power, Bear Power) |
| NaN checks | 3 (high, low, close) |
| Conditional | 1 (warmup check) |
| **Total** | **~8 ops** |

## Common Pitfalls

1. **Primary output is Bull Power only**: `Last.Value` returns Bull Power. Bear Power requires accessing the `BearPower` property separately. Forgetting this leads to incomplete analysis.
2. **TValue input is degenerate**: Passing single values (not TBar) sets H = L = C, making Bull Power = Bear Power = C $-$ EMA. Use TBar input for proper decomposition.
3. **Warmup affects EMA quality**: Early EMA values use exponential warmup compensation, but the first few bars are still adapting. Allow at least $2N$ bars for stable readings.
4. **Not bounded**: Unlike oscillators clamped to $[0, 100]$, Bull and Bear Power can take any value. Visual scaling on charts requires attention.
5. **Trend filter is essential**: Elder explicitly designed this as a component of a system. Using Bull/Bear Power without checking EMA direction defeats the design intent.
6. **Bear Power is normally negative**: In healthy uptrends, Bear Power is negative (low is below EMA). It becomes concerning only when it turns increasingly negative during what should be an uptrend.

## References

- Elder, A. *Trading for a Living*. John Wiley & Sons, 1993. Chapter on Elder-Ray.
- Elder, A. *Come Into My Trading Room*. John Wiley & Sons, 2002.
- Achelis, S. B. *Technical Analysis from A to Z*. McGraw-Hill, 2000.
