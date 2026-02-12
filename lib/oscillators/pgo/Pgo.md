# PGO: Pretty Good Oscillator

> "Good enough to trade, honest enough not to pretend otherwise." — Mark Johnson (probably)

## Introduction

The Pretty Good Oscillator (PGO) measures how far the current price has deviated from its Simple Moving Average, expressed in Average True Range (ATR) units. A reading of +2.0 means price is two ATRs above the SMA; -3.0 means three ATRs below. The volatility normalization makes PGO readings comparable across instruments and timeframes, unlike raw price-minus-average oscillators that scale with price level.

## Historical Context

Mark Johnson introduced PGO in the late 1990s as a practical alternative to oscillators that produce instrument-dependent readings. The core insight: dividing by ATR creates a dimensionless ratio. A stock at $500 and a penny stock at $2 can both produce a PGO of +3.0, and that reading carries the same statistical meaning in both cases. The name "Pretty Good" reflects Johnson's deliberately modest positioning: not the ultimate oscillator, but a reliable workhorse that normalizes displacement by realized volatility rather than by standard deviation (like Bollinger %B) or by price level (like CFO).

The indicator shares conceptual DNA with z-scores and Bollinger Bands but uses ATR (which captures gap risk through True Range) rather than standard deviation (which doesn't). This makes PGO more responsive to overnight gaps and limit moves.

## Architecture and Physics

### 1. Simple Moving Average (SMA)

Standard arithmetic mean over the lookback period:

$$\text{SMA}_t = \frac{1}{N} \sum_{i=0}^{N-1} \text{Close}_{t-i}$$

Implemented as a running sum with O(1) incremental updates via RingBuffer.

### 2. True Range (TR)

$$\text{TR}_t = \max\bigl(\text{High}_t - \text{Low}_t,\; |\text{High}_t - \text{Close}_{t-1}|,\; |\text{Low}_t - \text{Close}_{t-1}|\bigr)$$

Captures both intrabar range and gap risk from the previous close.

### 3. Average True Range (ATR)

Exponential Moving Average of TR with warmup compensation:

$$\text{ATR}_t = \text{EMA}(\text{TR}, N) \cdot \frac{1}{1 - (1 - \alpha)^t}$$

where $\alpha = 1/N$. The compensation factor corrects the EMA bias during the initial warmup period, converging to 1.0 as $t \to \infty$.

### 4. PGO Computation

$$\text{PGO}_t = \frac{\text{Close}_t - \text{SMA}_t}{\text{ATR}_t}$$

When $\text{ATR} = 0$ (constant price), PGO returns 0.0 by convention.

## Mathematical Foundation

The PGO is a volatility-normalized displacement measure. In continuous terms:

$$\text{PGO} = \frac{P - \bar{P}}{\sigma_{\text{ATR}}}$$

where $\bar{P}$ is the rolling mean and $\sigma_{\text{ATR}}$ is the ATR-based volatility estimate.

### Parameter Mapping

| Parameter | Default | Range | Effect |
| :--- | :--- | :--- | :--- |
| Period ($N$) | 14 | 1-500 | Controls both SMA lookback and ATR smoothing. Larger periods produce smoother, slower oscillations. |

### Transfer Function

PGO has no recursive (IIR) component in its numerator; SMA is pure FIR. The ATR denominator uses EMA (IIR) with transfer function:

$$H(z) = \frac{\alpha}{1 - (1-\alpha)z^{-1}}$$

This gives the denominator exponential decay characteristics while the numerator remains finite-impulse.

## Performance Profile

| Operation | Complexity | Notes |
| :--- | :--- | :--- |
| SMA update | O(1) | RingBuffer running sum |
| TR calculation | O(1) | Three comparisons |
| ATR (EMA) update | O(1) | FMA-optimized IIR |
| PGO computation | O(1) | Single division |
| **Total per bar** | **O(1)** | Zero allocations in hot path |

### Quality Metrics

| Metric | Score (1-10) | Notes |
| :--- | :--- | :--- |
| Noise rejection | 5 | SMA has no frequency selectivity |
| Lag | 6 | SMA lag = (N-1)/2 bars; ATR smoothing adds minimal lag |
| Sensitivity | 7 | ATR normalization adapts to volatility regimes |
| Simplicity | 9 | Two components, one parameter |
| Cross-instrument comparability | 9 | Dimensionless output |

## Interpretation

### Overbought/Oversold Levels

- **Above +3.0**: Price is 3 ATRs above the mean. Statistically extended; reversal probability increases.
- **Below -3.0**: Price is 3 ATRs below the mean. Statistically depressed; bounce probability increases.
- **Between -1.0 and +1.0**: Normal range; no directional bias.

### Zero Line Crossovers

- PGO crosses above zero: price crosses above SMA (bullish momentum shift).
- PGO crosses below zero: price crosses below SMA (bearish momentum shift).

### Divergence Analysis

- **Bullish divergence**: Price makes lower lows while PGO makes higher lows. ATR-normalized displacement is contracting despite new price lows — sellers exhausting.
- **Bearish divergence**: Price makes higher highs while PGO makes lower highs. Despite new highs, displacement relative to volatility is shrinking.

## Validation

| Library | Validated | Notes |
| :--- | :--- | :--- |
| Skender | - | No PGO implementation |
| TA-Lib | - | No PGO implementation |
| Tulip | - | No PGO implementation |
| Ooples | - | Not verified |
| Self-consistency | ✔️ | Batch/streaming/span agree; component identity verified |

Cross-validation: PGO is verified against manual SMA + ATR computation. Streaming, batch (TBarSeries), and span paths produce identical results within floating-point tolerance ($10^{-10}$).

## Common Pitfalls

1. **Ignoring ATR=0**: Constant-price instruments produce zero ATR. Division by zero must be guarded (returns 0.0).
2. **Comparing across periods**: PGO(14) and PGO(50) are not directly comparable. Longer periods smooth more aggressively, producing smaller absolute readings.
3. **Using without OHLC data**: PGO requires High/Low/Close for True Range. Feeding only close prices produces TR=0 (synthetic bars with H=L=C), making the oscillator meaningless.
4. **Fixed overbought/oversold thresholds**: The ±3.0 levels are guidelines. Fat-tailed distributions (common in finance) produce more extreme readings than Gaussian models suggest.
5. **SMA lag in trending markets**: SMA introduces (N-1)/2 bars of lag. In strong trends, PGO may show persistent readings of ±2-4 without mean reversion. This is a feature, not a bug — it confirms trend strength.
6. **Warmup period**: PGO requires N bars to fill the SMA buffer and begin producing valid readings. ATR warmup is handled by exponential compensation but converges asymptotically.
7. **Not a standalone signal**: PGO measures displacement, not direction. Combine with trend filters (e.g., moving average slope) for directional context.

## References

- Johnson, M. "Pretty Good Oscillator." Technical analysis community publication.
- Wilder, J.W. "New Concepts in Technical Trading Systems." Trend Research, 1978. (ATR foundation)
- PineScript reference implementation: `pgo.pine`
