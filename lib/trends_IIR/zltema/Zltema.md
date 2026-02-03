# ZLTEMA: Zero-Lag Triple Exponential Moving Average

## TEMA with lag compensation via a zero-lag signal

> "ZLTEMA combines the speed of zero-lag prediction with the smoothness of triple exponential averaging. You get the fastest response in the zero-lag family, with the best noise rejection from the TEMA cascade."

ZLTEMA takes a standard TEMA and feeds it a **zero-lag signal**: current price minus a lagged price. This produces a smoother that responds faster than TEMA without going fully raw. The triple EMA cascade provides maximum noise rejection in the exponential family while the zero-lag preprocessing maintains responsiveness.

## Historical Context

ZLTEMA extends the zero-lag concept from ZLEMA to triple exponential moving averages. Where ZLEMA applies lag compensation to a single EMA and ZLDEMA to a double cascade, ZLTEMA applies it to a three-stage EMA cascade using the TEMA formula (3*EMA1 - 3*EMA2 + EMA3). This combination targets the extreme end: maximum smoothness with minimal lag.

## Architecture & Physics

### Pipeline

1. **Lag estimate**

$$\text{lag} = \max(1, \text{round}((N-1)/2))$$

2. **Zero-lag signal**

$$s_t = 2 \cdot x_t - x_{t-\text{lag}}$$

3. **First EMA stage**

$$\text{EMA1}_t = \text{EMA}(s_t, \alpha)$$

4. **Second EMA stage**

$$\text{EMA2}_t = \text{EMA}(\text{EMA1}_t, \alpha)$$

5. **Third EMA stage**

$$\text{EMA3}_t = \text{EMA}(\text{EMA2}_t, \alpha)$$

6. **TEMA output**

$$\text{ZLTEMA}_t = 3 \cdot \text{EMA1}_t - 3 \cdot \text{EMA2}_t + \text{EMA3}_t$$

### Warmup compensation

ZLTEMA uses EMA bias compensation during warmup on all three EMA stages:

$$y_t^{*} = \frac{y_t}{1 - (1 - \alpha)^t}$$

This avoids the early-stage bias toward zero and makes the first values usable.

## Math Foundation

**EMA update:**

$$y_t = y_{t-1} + \alpha (s_t - y_{t-1})$$

**Zero-lag signal:**

$$s_t = 2 \cdot x_t - x_{t-\text{lag}}$$

**TEMA formula:**

$$\text{TEMA}_t = 3 \cdot \text{EMA1}_t - 3 \cdot \text{EMA2}_t + \text{EMA3}_t$$

**Alpha from period:**

$$\alpha = \frac{2}{N + 1}$$

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

**Hot path (after warmup, compensation complete):**

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| FMA | 6 | 4 | 24 |
| MUL | 3 | 3 | 9 |
| **Total** | **9** |  | **~33 cycles** |

The hot path consists of:

1. Zero-lag signal: `FMA(2.0, val, -lagged)` - 1 FMA
2. EMA1 core: `FMA(ema1Raw, beta, alpha * signal)` - 1 FMA + 1 MUL
3. EMA2 core: `FMA(ema2Raw, beta, alpha * ema1)` - 1 FMA + 1 MUL
4. EMA3 core: `FMA(ema3Raw, beta, alpha * ema2)` - 1 FMA + 1 MUL
5. TEMA output: `FMA(3.0, ema1, FMA(-3.0, ema2, ema3))` - 2 FMA (nested)

**Warmup path (with bias compensation):**

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| FMA | 6 | 4 | 24 |
| MUL | 5 | 3 | 15 |
| DIV | 1 | 15 | 15 |
| CMP | 2 | 1 | 2 |
| **Total** | **14** |  | **~56 cycles** |

Additional warmup operations:

- Decay tracking: `e *= beta` - 1 MUL
- Compensator calc: `1 / (1 - e)` - 1 DIV
- Bias compensation: `ema1Raw * compensator`, `ema2Raw * compensator`, `ema3Raw * compensator` - 3 MUL
- Hot/compensated checks - 2 CMP

### Batch Mode (SIMD Analysis)

ZLTEMA is an IIR filter with lag buffer dependency - not directly vectorizable across bars. However, within-bar operations use FMA intrinsics.

| Optimization | Benefit |
| :--- | :--- |
| FMA instructions | ~33 cycles vs ~42 scalar |
| stackalloc buffer | Zero heap allocation for lag ≤256 |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 8/10 | Matches PineScript reference |
| **Timeliness** | 10/10 | Fastest response in ZL family |
| **Overshoot** | 4/10 | Predictive signal plus TEMA amplification causes significant overshoot |
| **Smoothness** | 8/10 | Smoothest in ZL family due to triple EMA cascade |

## Validation

ZLTEMA is validated against a PineScript reference implementation.

| Library | Status | Tolerance | Notes |
|:---|:---|:---|:---|
| **TA-Lib** | N/A | - | No ZLTEMA in TA-Lib |
| **Skender** | N/A | - | No ZLTEMA in Skender |
| **Tulip** | N/A | - | No ZLTEMA in Tulip |
| **Ooples** | N/A | - | No ZLTEMA in Ooples |
| **PineScript** | ✓ Passed | 1e-10 | Matches `lib/trends_IIR/zltema/zltema.pine` |

## Common Pitfalls

1. **Maximum overshoot on turns**

   The zero-lag signal is a forward estimate, and the TEMA formula (3*EMA1 - 3*EMA2 + EMA3) has the highest amplification in the exponential family. Expect more overshoot than ZLDEMA or ZLEMA when price reverses sharply.

2. **Period semantics**

   ZLTEMA uses EMA alpha; the lag term is derived from period but not equivalent to a window length. Do not compare ZLTEMA period directly to SMA window length.

3. **Warmup discipline**

   Use `IsHot` / `WarmupPeriod` before acting on signals. Early values are bias-corrected but still unstable. The triple EMA cascade requires longer warmup than ZLDEMA or ZLEMA.

4. **Non-finite data**

   NaN or Infinity is replaced with the last valid value. Before the first valid sample, output is `NaN`.

5. **TEMA vs ZLTEMA**

   ZLTEMA is not simply TEMA with a different alpha. The zero-lag preprocessing fundamentally changes the input signal, making ZLTEMA more responsive but also more prone to overshoot than standard TEMA.

6. **ZLDEMA vs ZLTEMA**

   ZLTEMA adds a third EMA stage over ZLDEMA. This provides additional smoothing at the cost of more overshoot during reversals. Use ZLDEMA when overshoot is more concerning than noise; use ZLTEMA when maximum smoothness is required.