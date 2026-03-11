# SMI: Stochastic Momentum Index

> "The stochastic tells you where price is in the range. The SMI tells you how enthusiastically it got there." — William Blau

| Property | Value |
|----------|-------|
| **Category** | Oscillator |
| **Inputs** | Bar series (High, Low, Close) |
| **Parameters** | `kPeriod` (default 10), `kSmooth` (default 3), `dSmooth` (default 3), `blau` (default true) |
| **Outputs** | Dual series (K line, D signal line) |
| **Output range** | $-100$ to $+100$ |
| **Warmup** | `kPeriod + kSmooth + dSmooth` bars |
| **PineScript**   | [smi.pine](smi.pine)                       |

### Key takeaways

- Measures where the close sits relative to the midpoint of the recent range, not the boundary. Zero means neutral; positive means above midpoint; negative means below.
- Two methods: Blau (default) smooths the ratio; Chande/Kroll smooths numerator and denominator separately before computing the ratio.
- Double EMA smoothing with warmup compensation produces clean crossover signals with controlled lag.
- Unlike classic Stochastic ($[0, 100]$), SMI ranges $[-100, +100]$, centered on zero. Traditional Stochastic thresholds do not apply.
- Uses `MonotonicDeque` for O(1) amortized highest/lowest tracking and `Math.FusedMultiplyAdd` for EMA stages.

## Historical Context

William Blau introduced the Stochastic Momentum Index in *Momentum, Direction, and Divergence* (1995) as an improvement over George Lane's classic Stochastic Oscillator. Blau's key insight: measuring distance from the range midpoint rather than from the low eliminates the asymmetric bias inherent in traditional stochastics. When price closes at the exact middle of its range, classic Stochastic reads 50—an arbitrary number that says nothing. SMI reads 0—neutral, centered, semantically honest.

Tushar Chande and Stanley Kroll proposed a variant in *The New Technical Trader* (1994) that smooths numerator and denominator separately before computing the ratio. This subtle difference in order of operations produces different behavior during volatile periods: Blau's method smooths the ratio directly, which compresses extreme values; Chande/Kroll preserves the ratio's sensitivity by smoothing its components independently.

QuanTAlib implements both methods via the `blau` parameter. The Blau method (default) suits trend-following; the Chande/Kroll method suits mean-reversion. Neither is universally better.

## What It Measures and Why It Matters

SMI measures the closing price's distance from the midpoint of the highest-high to lowest-low range, normalized by half that range, then double-smoothed with cascaded EMAs. The result is a zero-centered oscillator bounded by $[-100, +100]$.

The centering around zero gives SMI cleaner semantics than classic Stochastic. Positive values mean the close is above the range midpoint; negative values mean it is below. The magnitude indicates the strength of the displacement. Values beyond $\pm 40$ indicate extreme momentum; values near zero indicate no meaningful displacement.

The double EMA smoothing ($-12$ dB/octave rolloff) attenuates noise more aggressively than a single EMA of equivalent period, at the cost of additional group delay. This makes SMI better at filtering whipsaws than raw Stochastic while remaining responsive enough for momentum detection.

## Mathematical Foundation

### Core Formula

**Rolling extremes:**

$$
HH_t = \max_{i=0}^{N-1} H_{t-i}, \quad LL_t = \min_{i=0}^{N-1} L_{t-i}
$$

**Midpoint and half-range:**

$$
\text{mid}_t = \frac{HH_t + LL_t}{2}, \quad \text{rh}_t = \frac{HH_t - LL_t}{2}
$$

**Blau method** (smooth the ratio):

$$
\text{raw}_t = \begin{cases} 100 \times \frac{C_t - \text{mid}_t}{\text{rh}_t} & \text{if } \text{rh}_t > 0 \\ 0 & \text{otherwise} \end{cases}
$$

$$
K_t = \text{EMA}_2(\text{EMA}_1(\text{raw}_t))
$$

**Chande/Kroll method** (smooth the components):

$$
K_t = 100 \times \frac{\text{EMA}_2(\text{EMA}_1(C_t - \text{mid}_t))}{\text{EMA}_2(\text{EMA}_1(\text{rh}_t))}
$$

**Signal line (both methods):**

$$
D_t = \text{EMA}(K_t, d)
$$

All EMA stages use $\alpha = 2/(N+1)$ with exponential warmup compensation.

### Parameter Mapping

| Parameter | Code | Default | Constraints |
|-----------|------|---------|-------------|
| K Period | `kPeriod` | 10 | `> 0` |
| K Smoothing | `kSmooth` | 3 | `> 0` |
| D Smoothing | `dSmooth` | 3 | `> 0` |
| Method | `blau` | `true` | Blau or Chande/Kroll |

### Warmup Period

$$
W = N + k_s + d_s
$$

`IsHot` fires after `kPeriod` bars (sufficient for the deque window). Full EMA convergence requires the complete warmup period.

## Architecture & Physics

### 1. MonotonicDeque Streaming

Two `MonotonicDeque` instances track the sliding highest-high and lowest-low over `kPeriod` bars. Circular buffers (`_hBuf`, `_lBuf`) store raw H/L values for deque rebuild on bar correction.

### 2. Dual-Path EMA Cascade

**Blau path**: Computes `raw = 100 * (close - mid) / rh`, then applies two cascaded EMAs with the same alpha (kSmooth), followed by an EMA with dSmooth alpha for the signal line.

**Chande/Kroll path**: Maintains four EMA accumulators (two per layer) for numerator and denominator independently, then computes the ratio.

### 3. Warmup Compensation

Each EMA stage uses exponential warmup compensators ($e_t = d \cdot e_{t-1}$, $c_t = 1/(1 - e_t)$) that correct initialization bias. The compensator converges to 1.0 as $e_t \to 0$, after which the hot path skips compensation for performance.

### 4. Edge Cases

| Condition | Behavior |
|-----------|----------|
| Any parameter `<= 0` | `ArgumentException` with `nameof()` |
| `NaN` / `Infinity` input | Substitutes last valid value per channel (H/L/C) |
| All NaN (no valid data yet) | Returns `NaN` for K and D |
| Zero range ($HH = LL$) | Returns $0$ (Blau) or $0$ (Chande/Kroll when denominator is $0$) |
| `isNew = false` | Restores `_ps`, rebuilds both deques from circular buffer |

## Interpretation and Signals

### Signal Zones

| Zone | Condition | Interpretation |
|------|-----------|----------------|
| Overbought | `K > +40` | Close significantly above range midpoint |
| Neutral | `-20 ≤ K ≤ +20` | No strong momentum bias |
| Oversold | `K < -40` | Close significantly below range midpoint |

### Signal Patterns

- **K/D crossover**: K crossing above D signals bullish momentum shift; K crossing below D signals bearish shift.
- **Zero-line crossover**: K crossing above zero confirms upward momentum; crossing below confirms downward.
- **Divergence**: Price makes new highs while K makes lower highs (bearish) or price makes new lows while K makes higher lows (bullish).

### Practical Notes

- **Blau** (default): Better for trend-following. Smoother output, fewer whipsaws. The ratio compression during high volatility acts as a natural dampener.
- **Chande/Kroll**: Better for mean-reversion. Preserves component oscillation sensitivity. More responsive during volatile reversals but noisier in trends.
- Do not use classic Stochastic thresholds (20/80) for SMI. The $[-100, +100]$ range centered on zero requires $\pm 40$ thresholds.

## Related Indicators

- [**Stoch**](../stoch/Stoch.md): Measures distance from range boundary ($[0, 100]$); SMI measures distance from midpoint ($[-100, +100]$).
- [**KDJ**](../kdj/Kdj.md): Extended stochastic with J-line divergence amplification.
- [**StochRSI**](../stochrsi/Stochrsi.md): Applies stochastic formula to RSI instead of price.
- [**Willr**](../willr/Willr.md): Measures distance from high, inverted scale $[-100, 0]$.

## Validation

| Library | Status | Notes |
|---------|--------|-------|
| Skender | ✅ | `GetSmi()` matches within `1e-6` |
| Self-consistency | ✅ | Streaming/batch/span agree within `1e-6`; both Blau and Chande/Kroll verified |
| TA-Lib | -- | No SMI implementation |
| Tulip | -- | No SMI implementation |

## Performance Profile

### Key Optimizations

- **O(1) amortized streaming**: `MonotonicDeque` for highest/lowest tracking.
- **FMA-optimized EMAs**: `Math.FusedMultiplyAdd` for all EMA accumulations reduces rounding error and improves throughput on FMA-capable hardware.
- **Warmup phase elimination**: Once all compensators converge ($e < 10^{-10}$), the hot path skips compensation arithmetic.
- **Zero allocation**: `Update` uses pre-allocated buffers and `record struct State`.

### Operation Count (Streaming Mode)

| Operation | Count per bar |
|-----------|---------------|
| Deque push (amortized) | 2-3 comparisons |
| Midpoint/rangeHalf | 2 ops |
| EMA stage 1 (FMA) | 1 FMA |
| EMA stage 2 (FMA) | 1 FMA |
| Signal EMA (FMA) | 1 FMA |
| Warmup compensators | 3-6 (only during warmup) |
| **Total** | **~10 ops (hot), ~16 ops (warmup)** |

### SIMD Analysis (Batch Mode)

| Property | Value |
|----------|-------|
| Vectorizable | Partially (via `Highest.Batch` / `Lowest.Batch`) |
| EMA cascade | Scalar (recursive dependency) |
| Fallback | Scalar loop with FMA for EMA stages |

## Common Pitfalls

1. **Confusing SMI with classic Stochastic**: SMI ranges $[-100, +100]$ centered on zero. Classic Stochastic ranges $[0, 100]$. Using Stochastic thresholds (20/80) for SMI produces incorrect signals.
2. **Ignoring the method parameter**: Blau and Chande/Kroll produce meaningfully different results. Switching methods mid-analysis invalidates comparisons.
3. **Zero range handling**: When $HH = LL$, `rangeHalf` is zero. The division guard returns $0$, but sustained zero readings may mask meaningful price action outside the deque window.
4. **Cascaded EMA warmup**: Three EMA stages each need convergence time. The first values after `IsHot` are less reliable than values after the full warmup period.
5. **Parameter interaction**: `kPeriod` controls reference range width; `kSmooth` controls noise filtering; `dSmooth` controls signal line lag. Increasing `kPeriod` without adjusting smoothing produces a wider reference range with insufficient filtering.
6. **Bar correction cost**: `isNew=false` triggers O(kPeriod) deque rebuild. Infrequent in normal streaming but visible with rapid corrections.

## FAQ

**Q: Should I use Blau or Chande/Kroll?**
A: Blau (default) for trend-following. Chande/Kroll for mean-reversion in volatile markets. Blau compresses extreme ratio values through smoothing; Chande/Kroll preserves component oscillation by smoothing numerator and denominator independently.

**Q: Why does SMI use ±40 thresholds instead of 20/80?**
A: Because SMI is centered on zero with range $[-100, +100]$. The ±40 thresholds correspond to approximately the same distance from neutral as 20/80 in classic Stochastic's $[0, 100]$ range.

**Q: How does SMI compare to Stochastic for crossover signals?**
A: SMI's zero-centered output produces cleaner crossovers because the neutral point is semantically meaningful (close equals range midpoint). Classic Stochastic's 50 level has no equivalent semantic clarity.

## References

- Blau, W. *Momentum, Direction, and Divergence*. Wiley, 1995.
- Chande, T. S.; Kroll, S. *The New Technical Trader*. Wiley, 1994.
- Lane, G. C. "Lane's Stochastics." *Technical Analysis of Stocks & Commodities*, 1984.
