# VORTEX: Vortex Indicator

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Dynamic                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period` (default 14)                      |
| **Outputs**      | Multiple series (ViPlus, ViMinus)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period` bars                          |

### TL;DR

- The Vortex Indicator measures upward and downward trend momentum by computing the ratio of positive and negative vortex movements to true range ove...
- Parameterized by `period` (default 14).
- Output range: Varies (see docs).
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

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

## Performance Profile

### Operation Count (Streaming Mode)

Vortex tracks rolling sums of VM+ and VM− (directional bar movements) and TR over N bars using O(1) running sums backed by RingBuffers.

**Post-warmup steady state (per bar):**

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ABS × 2 (VM+ = |High − PrevLow|, VM− = |Low − PrevHigh|) | 2 | 1 | 2 |
| TR computation (SUB×3, ABS×2, MAX×2) | 7 | 1 | 7 |
| SUB × 3 (subtract oldest from sums) | 3 | 1 | 3 |
| ADD × 3 (add new to sums) | 3 | 1 | 3 |
| RingBuffer writes × 3 | 3 | 1 | 3 |
| DIV × 2 (VI+ = sumVM+/sumTR, VI− = sumVM−/sumTR) | 2 | 15 | 30 |
| CMP (sumTR > 0 guard) | 1 | 1 | 1 |
| **Total** | **21** | — | **~49 cycles** |

Three parallel O(1) running sums with RingBuffers. For default $N=14$: ~49 cycles per bar. Batch mode pre-computes per-bar vectors then applies sliding sums.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| VM+ / VM− computation | Yes | VSUBPD + VABSPD — fully independent per bar |
| TR computation | Yes | VSUBPD + VABSPD + VMAXPD — independent per bar |
| Prefix sum (VM+, VM−, TR) | Partial | Inclusive prefix sum; SIMD assist with subtract-lag |
| Division (VI+, VI−) | Yes | VDIVPD on prefix-sum results |

All individual-bar computations are independent and SIMD-friendly. The prefix-sum step benefits from AVX2 vectorization. For $N=14$ and arrays of 1000+ bars, batch SIMD achieves ~3–4× throughput over scalar streaming.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact arithmetic; O(1) running sums avoid floating-point drift |
| **Timeliness** | 7/10 | N-bar window; responds within one period to directional change |
| **Smoothness** | 6/10 | Rolling sum provides moderate smoothing; no additional filter |
| **Noise Rejection** | 6/10 | N-period window averages out individual bar noise; no adaptive bandwidth |

## Resources

- Botes, E. & Siepman, D. (2010). "The Vortex Indicator." *Technical Analysis of Stocks and Commodities*, January 2010.
