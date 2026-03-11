# ZLDEMA: Zero-Lag Double Exponential Moving Average

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (IIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Zldema)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `Math.Max(lag + 1, EstimateWarmupPeriod(beta))` bars                          |
| **PineScript**   | [zldema.pine](zldema.pine)                       |
| **Signature**    | [zldema_signature](zldema_signature.md) |

- ZLDEMA takes a standard DEMA and feeds it a **zero-lag signal**: current price minus a lagged price.
- Parameterized by `period`.
- Output range: Tracks input.
- Requires `Math.Max(lag + 1, EstimateWarmupPeriod(beta))` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "ZLDEMA combines the speed of zero-lag prediction with the smoothness of double exponential averaging. You get faster response than ZLEMA, with better trend-following than DEMA."

## DEMA with lag compensation via a zero-lag signal


ZLDEMA takes a standard DEMA and feeds it a **zero-lag signal**: current price minus a lagged price. This produces a smoother that responds faster than DEMA without going fully raw. The dual EMA cascade provides additional noise rejection while the zero-lag preprocessing maintains responsiveness.

## Historical Context

ZLDEMA extends the zero-lag concept from ZLEMA to double exponential moving averages. Where ZLEMA applies lag compensation to a single EMA, ZLDEMA applies it to a two-stage EMA cascade using the DEMA formula (2*EMA1 - EMA2). This combination targets the middle ground between ZLEMA's speed and TEMA's smoothness.

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

5. **DEMA output**

$$\text{ZLDEMA}_t = 2 \cdot \text{EMA1}_t - \text{EMA2}_t$$

### Warmup compensation

ZLDEMA uses EMA bias compensation during warmup on both EMA stages:

$$y_t^{*} = \frac{y_t}{1 - (1 - \alpha)^t}$$

This avoids the early-stage bias toward zero and makes the first values usable.

## Math Foundation

**EMA update:**

$$y_t = y_{t-1} + \alpha (s_t - y_{t-1})$$

**Zero-lag signal:**

$$s_t = 2 \cdot x_t - x_{t-\text{lag}}$$

**DEMA formula:**

$$\text{DEMA}_t = 2 \cdot \text{EMA1}_t - \text{EMA2}_t$$

**Alpha from period:**

$$\alpha = \frac{2}{N + 1}$$

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

**Hot path (after warmup, compensation complete):**

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| FMA | 4 | 4 | 16 |
| MUL | 2 | 3 | 6 |
| **Total** | **6** |  | **~22 cycles** |

The hot path consists of:

1. Zero-lag signal: `FMA(2.0, val, -lagged)` - 1 FMA
2. EMA1 core: `FMA(ema1Raw, beta, alpha * signal)` - 1 FMA + 1 MUL
3. EMA2 core: `FMA(ema2Raw, beta, alpha * ema1)` - 1 FMA + 1 MUL
4. DEMA output: `FMA(2.0, ema1, -ema2)` - 1 FMA

**Warmup path (with bias compensation):**

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| FMA | 4 | 4 | 16 |
| MUL | 4 | 3 | 12 |
| DIV | 1 | 15 | 15 |
| CMP | 2 | 1 | 2 |
| **Total** | **11** |  | **~45 cycles** |

Additional warmup operations:

- Decay tracking: `e *= beta` - 1 MUL
- Compensator calc: `1 / (1 - e)` - 1 DIV
- Bias compensation: `ema1Raw * compensator`, `ema2Raw * compensator` - 2 MUL
- Hot/compensated checks - 2 CMP

### Batch Mode (SIMD Analysis)

ZLDEMA is an IIR filter with lag buffer dependency - not directly vectorizable across bars. However, within-bar operations use FMA intrinsics.

| Optimization | Benefit |
| :--- | :--- |
| FMA instructions | ~22 cycles vs ~28 scalar |
| stackalloc buffer | Zero heap allocation for lag ≤256 |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 8/10 | Matches PineScript reference |
| **Timeliness** | 9/10 | Faster response than DEMA, comparable to ZLEMA |
| **Overshoot** | 5/10 | Predictive signal plus DEMA amplification causes overshoot |
| **Smoothness** | 7/10 | Smoother than ZLEMA due to dual EMA cascade |

## Validation

ZLDEMA is validated against a PineScript reference implementation.

| Library | Status | Tolerance | Notes |
|:---|:---|:---|:---|
| **TA-Lib** | N/A | - | No ZLDEMA in TA-Lib |
| **Skender** | N/A | - | No ZLDEMA in Skender |
| **Tulip** | N/A | - | No ZLDEMA in Tulip |
| **Ooples** | N/A | - | No ZLDEMA in Ooples |
| **PineScript** | ✓ Passed | 1e-10 | Matches `lib/trends_IIR/zldema/zldema.pine` |

## Common Pitfalls

1. **Increased overshoot on turns**

   The zero-lag signal is a forward estimate, and the DEMA formula (2*EMA1 - EMA2) further amplifies deviations. Expect more overshoot than ZLEMA when price reverses sharply.

2. **Period semantics**

   ZLDEMA uses EMA alpha; the lag term is derived from period but not equivalent to a window length. Do not compare ZLDEMA period directly to SMA window length.

3. **Warmup discipline**

   Use `IsHot` / `WarmupPeriod` before acting on signals. Early values are bias-corrected but still unstable. The dual EMA cascade requires longer warmup than single-stage ZLEMA.

4. **Non-finite data**

   NaN or Infinity is replaced with the last valid value. Before the first valid sample, output is `NaN`.

5. **DEMA vs ZLDEMA**

   ZLDEMA is not simply DEMA with a different alpha. The zero-lag preprocessing fundamentally changes the input signal, making ZLDEMA more responsive but also more prone to overshoot than standard DEMA.
