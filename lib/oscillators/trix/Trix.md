# TRIX: Triple Exponential Average Oscillator

> "The best filter is the one that removes what you don't want while keeping what you do." -- Jack Hutson

## Overview

The **Triple Exponential Average Oscillator (TRIX)** measures the percentage rate of change of a triple-smoothed exponential moving average. By passing price through three cascaded EMA stages before computing the rate of change, TRIX eliminates short-term noise that plagues single-EMA oscillators. The result is a zero-centered momentum indicator that responds only to sustained directional moves, making whipsaws from random price fluctuations structurally unlikely.

## Historical Context

Jack Hutson introduced TRIX in the early 1980s in *Stocks & Commodities* magazine. The core insight was simple: a single EMA still tracks noise. Running it through three smoothing passes produces a curve so smooth that its first derivative (rate of change) reliably identifies trend direction without the lag-versus-responsiveness tradeoff that haunts simpler oscillators.

Most implementations use a naive EMA (seed with first value, no compensation), which produces a warmup bias that takes roughly $3 \times \text{period}$ bars to dissipate. QuanTAlib eliminates this artifact using warmup-compensated EMA, yielding accurate values from bar 1.

## Architecture

```
Source ──→ CompensatedEMA₁ ──→ CompensatedEMA₂ ──→ CompensatedEMA₃ ──→ ROC% ──→ TRIX
           [α smoothing]        [α smoothing]        [α smoothing]     [100×Δ/prev]
```

### Streaming (O(1) per bar)

Each EMA stage maintains a raw EMA (`rema`) and a compensation factor (`e`):

| Component | Role |
|-----------|------|
| `Rema1/2/3` | Raw recursive EMA accumulators per stage |
| `E1/2/3` | Warmup compensation factors: $e_i = e_i \times (1 - \alpha)$ |
| `PrevEma3` | Previous bar's compensated EMA₃ for rate-of-change calculation |
| `Count` | Bar counter for `IsHot` determination |

### Compensated EMA

During warmup ($e > 10^{-10}$), the compensated value is:

$$
\text{ema}_i = \frac{\text{rema}_i}{1 - e_i}
$$

Once $e_i \leq 10^{-10}$, compensation converges to unity and is bypassed.

### Bar Correction

Uses `_s` / `_ps` state snapshot pair. On `isNew = true`, previous state is saved; on `isNew = false`, state rolls back before recomputing.

### Warmup

`WarmupPeriod = period * 3`. Three cascaded EMA stages each need approximately `period` bars to stabilize.

`IsHot` fires when `Count > period` (the compensation factors make the indicator usable earlier than uncompensated implementations).

## Mathematical Foundation

### Smoothing coefficient

$$
\alpha = \frac{2}{\text{period} + 1}
$$

### Triple EMA with warmup compensation

For each bar $n$ and each EMA stage $i \in \{1, 2, 3\}$:

$$
\text{rema}_i[n] = \alpha \cdot x_i[n] + (1 - \alpha) \cdot \text{rema}_i[n-1]
$$

$$
e_i[n] = e_i[n-1] \cdot (1 - \alpha)
$$

$$
\text{ema}_i[n] = \frac{\text{rema}_i[n]}{1 - e_i[n]}
$$

Where $x_1 = \text{source}$, $x_2 = \text{ema}_1$, $x_3 = \text{ema}_2$.

### TRIX output

$$
\text{TRIX}[n] = 100 \times \frac{\text{ema}_3[n] - \text{ema}_3[n-1]}{\text{ema}_3[n-1]}
$$

When $\text{ema}_3[n-1] = 0$, TRIX returns 0 (division guard).

### FMA optimization

Hot-path EMA update uses fused multiply-add:

$$
\text{rema} = \text{FMA}(\text{rema}_{\text{prev}}, 1-\alpha, \alpha \cdot x)
$$

Measured 15-25% speedup over separate multiply-then-add in tight update loops.

## Performance Profile

| Metric | Value |
|--------|-------|
| Time complexity | O(1) per bar (streaming) |
| Space complexity | O(1) (no buffers, scalar state only) |
| Allocations | Zero per update |
| NaN handling | Last valid value substitution |
| SIMD | Span-based `Batch()` with scalar fallback (recursive dependency prevents vectorization) |
| FMA | Yes, in all three EMA stages |

| Quality Metric | Score (1-10) |
|----------------|-------------|
| Smoothness | 9 |
| Lag | 6 (high smoothing = moderate lag) |
| Noise rejection | 10 |
| Whipsaw resistance | 9 |
| Trend detection | 8 |

## Validation

Cross-validated against four independent implementations:

| Library | Mode | Tolerance | Status | Notes |
|---------|------|-----------|--------|-------|
| Skender | Batch | 1e-9 | Pass | Exact match after warmup |
| Skender | Streaming | 1e-9 | Pass | Bar-by-bar verification |
| Skender | Span | 1e-9 | Pass | Span API consistency |
| TA-Lib | Span | 1e-9 | Pass | Lookback-aligned comparison |
| TA-Lib | Streaming | 1e-9 | Pass | Sequential verification |
| Tulip | Span | 5e-4 | Pass | Compensated vs uncompensated EMA divergence |
| Tulip | Batch | 1e-3 | Pass | Compensation difference accumulates over warmup |
| Tulip | Streaming | 1e-3 | Pass | Same compensation divergence pattern |

Tulip uses traditional uncompensated EMA. The compensation difference is structural, not a bug. Skender and TA-Lib use compatible warmup handling, producing tight matches.

Self-consistency validated across all four API modes (streaming, batch, span, eventing) with exact match verification.

## Common Pitfalls

1. **Ignoring warmup bias.** Uncompensated implementations produce startup transients for roughly $3 \times \text{period}$ bars. QuanTAlib's compensation eliminates this, but comparing against uncompensated libraries during warmup will show expected divergence.

2. **Confusing smoothness with accuracy.** TRIX's triple smoothing means it responds slowly to genuine reversals. A 14-period TRIX effectively has the lag characteristics of a 42-period single EMA applied to rate of change.

3. **Using TRIX as a standalone signal.** Zero-line crossovers are reliable but late. Pair with faster indicators (RSI, price action) for entry timing.

4. **Short periods amplify noise.** Below period 5, the triple-smoothing advantage degrades. The three cascaded EMAs need sufficient period to differentiate signal from noise.

5. **Division-by-zero edge case.** When EMA₃ equals zero (typically only with synthetic data), TRIX returns 0. Production price data never hits this case, but test harnesses should account for it.

6. **Misinterpreting Tulip validation gaps.** The 1e-3 tolerance against Tulip is not imprecision. It reflects the fundamental difference between compensated and uncompensated EMA warmup strategies.

## Usage

```csharp
// Streaming
var trix = new Trix(period: 14);
TValue result = trix.Update(new TValue(time, price));

// Event-based chaining
var source = new TSeries();
var trix = new Trix(source, period: 14);

// Batch (TSeries)
TSeries results = Trix.Batch(source, period: 14);

// Batch (Span)
Trix.Batch(sourceSpan, outputSpan, period: 14);

// Calculate (returns indicator for state inspection)
var (results, indicator) = Trix.Calculate(source, period: 14);
```

## Interpretation

- **Zero Line Crossovers:**
  - TRIX crosses above zero: Triple-smoothed EMA is rising (bullish momentum)
  - TRIX crosses below zero: Triple-smoothed EMA is falling (bearish momentum)

- **Signal Line:**
  - A short-period EMA of TRIX can serve as a signal line (similar to MACD)
  - Crossovers of TRIX above/below its signal line generate trade signals

- **Divergence:**
  - Bullish: Price makes lower lows while TRIX makes higher lows
  - Bearish: Price makes higher highs while TRIX makes lower highs
  - Triple smoothing makes TRIX divergences more reliable than single-EMA divergences

- **Trend Strength:**
  - Rising TRIX above zero: Strengthening uptrend
  - Falling TRIX below zero: Strengthening downtrend
  - TRIX near zero with small oscillations: Sideways/consolidating market

## Parameters

| Parameter | Type | Default | Range | Description |
|-----------|------|---------|-------|-------------|
| `period` | int | 14 | > 0 | EMA period for each of the three smoothing stages |

## References

- Jack Hutson, "TRIX - Triple Exponential Smoothing Oscillator," *Technical Analysis of Stocks & Commodities*, 1983
- Jack Hutson, *Charting the Stock Market: The Wyckoff Method*, 1986
- Steven Achelis, *Technical Analysis from A to Z*, 2nd ed., McGraw-Hill, 2001
- PineScript reference: `trix.pine`
