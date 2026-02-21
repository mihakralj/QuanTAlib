# VORTEX: Vortex Indicator

> "When bulls and bears clash, the Vortex measures the violence."

The Vortex Indicator measures upward and downward trend momentum by computing the ratio of positive and negative vortex movements to true range over a rolling window. VI+ captures the distance from current high to previous low (upward force); VI- captures the distance from current low to previous high (downward force). Both are normalized by summed true range, producing two lines that oscillate around 1.0. Crossovers signal trend changes. The implementation uses three ring buffers with running sums for O(1) streaming updates.

## Historical Context

Etienne Botes and Douglas Siepman introduced the Vortex Indicator in a January 2010 article for *Technical Analysis of Stocks and Commodities*. Inspired by Viktor Schauberger's observations of natural vortex patterns in water flow, they designed a dual-line indicator that captures directional momentum through geometric relationships between consecutive bars. The indicator is conceptually related to Wilder's Directional Movement (DM) system but uses a simpler construction: raw absolute distances rather than conditional directional selection. This makes Vortex more responsive to sharp reversals but more susceptible to gap noise. The typical period range is 14-21 bars, with 14 being the most common default.

## Architecture & Physics

### 1. Vortex Movement

Positive vortex movement measures the "reach" of bullish activity:

$$VM^+_t = |H_t - L_{t-1}|$$

Negative vortex movement measures the "reach" of bearish activity:

$$VM^-_t = |L_t - H_{t-1}|$$

In a strong uptrend, the current high is far from the previous low ($VM^+$ large). In a strong downtrend, the current low is far from the previous high ($VM^-$ large).

### 2. True Range Normalization

$$TR_t = \max(H_t - L_t,\ |H_t - C_{t-1}|,\ |L_t - C_{t-1}|)$$

True range serves as the denominator that normalizes vortex movements to the prevailing volatility regime.

### 3. Vortex Indicators

$$VI^+_t = \frac{\sum_{i=1}^{N} VM^+_{t-i+1}}{\sum_{i=1}^{N} TR_{t-i+1}}$$

$$VI^-_t = \frac{\sum_{i=1}^{N} VM^-_{t-i+1}}{\sum_{i=1}^{N} TR_{t-i+1}}$$

The summation window creates period-based smoothing without introducing the lag of recursive (IIR) filters.

### 4. Running Sum Implementation

Three ring buffers store $VM^+$, $VM^-$, and $TR$ values. Running sums update incrementally:

```
sum_vm_plus  += new_vm_plus  - oldest_vm_plus
sum_vm_minus += new_vm_minus - oldest_vm_minus
sum_tr       += new_tr       - oldest_tr
```

This yields O(1) per-bar updates after the initial warmup fill.

### 5. Complexity

| Metric | Value |
|:-------|:------|
| Time | O(1) per bar (running sum updates) |
| Space | O(N) (three ring buffers of size N) |
| Warmup | N bars |
| Allocations | Zero in hot path |

## Mathematical Foundation

### Parameters

| Parameter | Type | Default | Constraint | Description |
|:----------|:-----|:--------|:-----------|:------------|
| period | int | 14 | > 0 | Rolling window for VM and TR sums |

### Pseudo-code

```
VORTEX(bar, period=14):

  // Vortex movements (require previous bar)
  vm_plus  = abs(bar.High - prev_bar.Low)
  vm_minus = abs(bar.Low  - prev_bar.High)

  // True range
  tr = max(bar.High - bar.Low,
           abs(bar.High - prev_bar.Close),
           abs(bar.Low  - prev_bar.Close))

  // Ring buffer updates with running sums
  if buffer_full:
    sum_vm_plus  -= vm_plus_buffer.oldest
    sum_vm_minus -= vm_minus_buffer.oldest
    sum_tr       -= tr_buffer.oldest

  vm_plus_buffer.add(vm_plus)
  vm_minus_buffer.add(vm_minus)
  tr_buffer.add(tr)

  sum_vm_plus  += vm_plus
  sum_vm_minus += vm_minus
  sum_tr       += tr

  // Vortex indicators
  if sum_tr > 0:
    vi_plus  = sum_vm_plus  / sum_tr
    vi_minus = sum_vm_minus / sum_tr
  else:
    vi_plus  = 0
    vi_minus = 0

  return (vi_plus, vi_minus)
```

### Reference Line at 1.0

The value 1.0 serves as a natural reference:

- $VI^+ > 1$: Upward reach exceeds average true range (strong bullish pressure)
- $VI^- > 1$: Downward reach exceeds average true range (strong bearish pressure)
- Both < 1: Subdued directional activity

### Crossover Signal

$$\text{Bullish} = VI^+ > VI^- \quad (\text{and } VI^+_{\text{prev}} \leq VI^-_{\text{prev}})$$

$$\text{Bearish} = VI^- > VI^+ \quad (\text{and } VI^-_{\text{prev}} \leq VI^+_{\text{prev}})$$

Period selection: too short (< 7) creates noise; too long (> 28) introduces excessive lag. The 14-21 range balances responsiveness and stability.

## Resources

- Botes, E. & Siepman, D. (2010). "The Vortex Indicator." *Technical Analysis of Stocks and Commodities*, January 2010.
