# SMI: Stochastic Momentum Index

> "The stochastic tells you where price is in the range. The SMI tells you how enthusiastically it got there." — William Blau (paraphrased)

## Introduction

The Stochastic Momentum Index (SMI) measures where the close sits relative to the midpoint of the recent high-low range, then double-smooths the result with cascaded EMAs. Unlike the classic Stochastic Oscillator which measures distance from the low, SMI measures distance from the midpoint. This centering around zero produces cleaner crossover signals and reduces false readings during trending markets. Range: -100 to +100, with values beyond ±40 indicating extreme momentum.

## Historical Context

William Blau introduced the SMI in his 1995 book "Momentum, Direction, and Divergence" as an improvement over George Lane's classic Stochastic Oscillator. Blau's key insight: measuring distance from the range midpoint rather than from the low eliminates the asymmetric bias that plagues traditional stochastics. When price closes at the exact middle of its range, classic Stochastic reads 50 — an arbitrary number that says nothing. SMI reads 0 — neutral, centered, semantically honest.

Tushar Chande and Stanley Kroll proposed a variant in "The New Technical Trader" (1994) that smooths numerator and denominator separately before computing the ratio. This subtle difference in order of operations produces different behavior during volatile periods: Blau's method smooths the ratio directly, which can compress extreme values; Chande/Kroll's method preserves the ratio's sensitivity by smoothing its components independently.

QuanTAlib implements both methods via the `blau` parameter (default: `true` for Blau's method).

## Architecture and Physics

### 1. Rolling Highest High and Lowest Low

Using O(1) amortized MonotonicDeque for the kPeriod window:

$$HH_t = \max_{i=0}^{N-1} \text{High}_{t-i}$$

$$LL_t = \min_{i=0}^{N-1} \text{Low}_{t-i}$$

### 2. Midpoint and Half-Range

$$\text{midpoint}_t = \frac{HH_t + LL_t}{2}$$

$$\text{rangeHalf}_t = \frac{HH_t - LL_t}{2}$$

### 3. Blau Method (Default)

Compute the raw ratio first, then double-smooth:

$$\text{raw}_t = \begin{cases} 100 \times \frac{\text{Close}_t - \text{midpoint}_t}{\text{rangeHalf}_t} & \text{if } \text{rangeHalf}_t > 0 \\ 0 & \text{otherwise} \end{cases}$$

$$K_t = \text{EMA}_2(\text{EMA}_1(\text{raw}_t, \text{kSmooth}), \text{kSmooth})$$

$$D_t = \text{EMA}(K_t, \text{dSmooth})$$

### 4. Chande/Kroll Method

Smooth numerator and denominator separately, then compute the ratio:

$$\text{num}_t = \text{Close}_t - \text{midpoint}_t$$

$$\text{den}_t = \text{rangeHalf}_t$$

$$K_t = 100 \times \frac{\text{EMA}_2(\text{EMA}_1(\text{num}))}{\text{EMA}_2(\text{EMA}_1(\text{den}))}$$

$$D_t = \text{EMA}(K_t, \text{dSmooth})$$

### 5. EMA with Warmup Compensation

Each EMA stage uses exponential warmup compensation:

$$\alpha = \frac{2}{N + 1}, \quad d = 1 - \alpha$$

$$\text{EMA}_t = d \cdot \text{EMA}_{t-1} + \alpha \cdot x_t$$

$$e_t = d \cdot e_{t-1}, \quad c_t = \frac{1}{1 - e_t}$$

$$\text{compensated}_t = \text{EMA}_t \cdot c_t$$

The compensator corrects the initialization bias during warmup, converging to 1.0 as $e_t \to 0$.

## Mathematical Foundation

### Blau's Z-Domain Transfer Function

The double-EMA smoothing of the raw ratio has transfer function:

$$H(z) = \left(\frac{\alpha}{1 - dz^{-1}}\right)^2$$

This is a cascade of two identical first-order IIR sections, providing $-12$ dB/octave rolloff in the stopband. The cascade attenuates noise more aggressively than a single EMA of equivalent period, at the cost of additional group delay.

### Chande/Kroll Ratio Properties

The separate smoothing approach preserves a fundamental property: when numerator and denominator oscillate at the same frequency, their ratio remains unattenuated. Blau's method, by smoothing the ratio directly, can compress oscillations that the Chande/Kroll approach preserves.

### Parameter Mapping

| Parameter | Default | Range | Effect |
| :--- | :--- | :--- | :--- |
| kPeriod ($N$) | 10 | 1-500 | Lookback window for highest/lowest. Larger values produce a wider reference range, reducing sensitivity. |
| kSmooth | 3 | 1-100 | EMA period for the double-smoothing of K. Larger values smooth more aggressively, increasing lag. |
| dSmooth | 3 | 1-100 | EMA period for the signal line D. Controls signal line responsiveness. |
| blau | true | bool | `true` for Blau method (smooth ratio); `false` for Chande/Kroll (smooth components). |

### Warmup Period

$$\text{WarmupPeriod} = \text{kPeriod} + \text{kSmooth} + \text{dSmooth}$$

The indicator becomes `IsHot` after `kPeriod` bars (sufficient for the deque window). Full convergence of all three EMA stages requires the full warmup period.

## Performance Profile

| Operation | Complexity | Notes |
| :--- | :--- | :--- |
| MonotonicDeque push | O(1) amortized | Deque maintenance for highest/lowest |
| Midpoint/rangeHalf | O(1) | Two arithmetic operations |
| EMA stage 1 | O(1) | FMA-optimized |
| EMA stage 2 | O(1) | FMA-optimized |
| Signal EMA | O(1) | FMA-optimized |
| **Total per bar** | **O(1)** | Zero allocations in hot path |

### SIMD Analysis

SIMD is not applied in streaming `Update` due to the recursive EMA dependencies. The `Batch` span API delegates highest/lowest computation to their respective SIMD-enabled `Batch` methods, then processes the EMA cascade sequentially.

### Quality Metrics

| Metric | Score (1-10) | Notes |
| :--- | :--- | :--- |
| Noise rejection | 7 | Double EMA smoothing provides good noise attenuation |
| Lag | 5 | Three cascaded EMA stages accumulate group delay |
| Sensitivity | 8 | Midpoint centering produces sharper zero crossings than classic Stochastic |
| Range bound | 9 | Naturally bounded -100 to +100 by construction |
| Cross-instrument | 8 | Percentage-based output is comparable across instruments |

## Interpretation

### Overbought/Oversold Levels

- **Above +40**: Overbought zone. Close is significantly above the range midpoint. Reversal probability increases.
- **Below -40**: Oversold zone. Close is significantly below the range midpoint. Bounce probability increases.
- **Between -20 and +20**: Neutral zone. No strong momentum bias.

### K and D Crossovers

- **K crosses above D**: Bullish momentum shift. Momentum is accelerating upward.
- **K crosses below D**: Bearish momentum shift. Momentum is decelerating or reversing.

### Divergence Analysis

- **Bullish divergence**: Price makes lower lows while SMI K makes higher lows. Range-normalized momentum is contracting despite new price lows.
- **Bearish divergence**: Price makes higher highs while SMI K makes lower highs. Despite new highs, momentum relative to range is weakening.

### Blau vs Chande/Kroll Selection

- **Blau (default)**: Better for trend-following. Smoother output, fewer whipsaws. The ratio compression during high volatility acts as a natural dampener.
- **Chande/Kroll**: Better for mean-reversion. Preserves component oscillation sensitivity. More responsive during volatile reversals but noisier in trends.

## Validation

| Library | Validated | Notes |
| :--- | :--- | :--- |
| Skender | ✔️ | SMI available via `GetSmi()` |
| TA-Lib | - | No SMI implementation |
| Tulip | - | No SMI implementation |
| Ooples | - | Not verified |
| Self-consistency | ✔️ | Batch/streaming/span agree within $10^{-6}$ tolerance |

Cross-validation: Streaming, batch (TBarSeries), and span paths produce identical results. Both Blau and Chande/Kroll methods are verified independently.

## Common Pitfalls

1. **Confusing SMI with classic Stochastic**: SMI measures distance from midpoint (range: -100 to +100). Classic Stochastic measures distance from the low (range: 0 to 100). Using Stochastic thresholds (20/80) for SMI produces incorrect signals.
2. **Ignoring the method parameter**: Blau and Chande/Kroll produce meaningfully different results. Switching methods mid-analysis invalidates comparisons.
3. **Zero range handling**: When highest equals lowest (constant price over kPeriod), rangeHalf is zero. Division by zero is guarded (returns 0.0), but a sustained zero reading may mask meaningful price action outside the deque window.
4. **Cascaded EMA warmup**: Three EMA stages each need convergence time. The first few values after `IsHot` are less reliable than values after the full `WarmupPeriod`. Trading signals should wait for full convergence.
5. **Period selection interaction**: kPeriod controls the reference range width; kSmooth controls noise filtering of K; dSmooth controls signal line lag. These three parameters interact. Increasing kPeriod without adjusting smoothing produces a wider range reference with insufficient filtering, yielding noisy K values.
6. **Not a standalone signal**: SMI measures momentum position within a range. Combine with trend filters for directional context. SMI works best in ranging markets; in strong trends, it can remain in overbought/oversold territory for extended periods.
7. **Bar correction with MonotonicDeque**: The `isNew=false` path rebuilds the deque from the circular buffer. Frequent corrections (high-frequency bar updates) are supported but carry O(N) rebuild cost per correction, where N is kPeriod.

## References

- Blau, William. "Momentum, Direction, and Divergence." Wiley, 1995.
- Chande, Tushar S. and Kroll, Stanley. "The New Technical Trader." Wiley, 1994.
- Lane, George. "Stochastics." Technical Analysis of Stocks & Commodities, 1984. (Original Stochastic Oscillator)
- PineScript reference implementation: `smi.pine`
