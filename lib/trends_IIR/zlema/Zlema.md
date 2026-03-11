# ZLEMA: Zero-Lag Exponential Moving Average

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (IIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Zlema)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `Math.Max(lag + 1, EstimateWarmupPeriod(beta))` bars                          |
| **PineScript**   | [zlema.pine](zlema.pine)                       |
| **Signature**    | [zlema_signature](zlema_signature.md) |

- ZLEMA takes a standard EMA and feeds it a **zero-lag signal**: current price minus a lagged price.
- Parameterized by `period`.
- Output range: Tracks input.
- Requires `Math.Max(lag + 1, EstimateWarmupPeriod(beta))` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "ZLEMA does not erase lag. It predicts just enough to act early, then pays the price in overshoot."

## EMA with lag compensation via a zero-lag signal


ZLEMA takes a standard EMA and feeds it a **zero-lag signal**: current price minus a lagged price. This produces a smoother that responds faster than EMA without going fully raw. It is not magic. It shifts some lag into controlled overshoot.

## Historical Context

ZLEMA is a widely used variation on EMA intended to reduce delay without abandoning exponential smoothing. It appears in multiple technical analysis toolkits and is often described as a "predictive EMA." The prediction is simple: extrapolate the current price by subtracting a lagged value.

## Architecture & Physics

### Pipeline

1. **Lag estimate**

$$\text{lag} = \max(1, \text{round}((N-1)/2))$$

2. **Zero-lag signal**

$$s_t = 2 \cdot x_t - x_{t-\text{lag}}$$

3. **EMA smoothing**

$$\text{ZLEMA}_t = \text{EMA}(s_t, \alpha)$$

### Warmup compensation

ZLEMA uses EMA bias compensation during warmup:

$$y_t^{*} = \frac{y_t}{1 - (1 - \alpha)^t}$$

This avoids the early-stage bias toward zero and makes the first values usable.

## Math Foundation

**EMA update:**

$$y_t = y_{t-1} + \alpha (s_t - y_{t-1})$$

**Zero-lag signal:**

$$s_t = 2 \cdot x_t - x_{t-\text{lag}}$$

**Alpha from period:**

$$\alpha = \frac{2}{N + 1}$$

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

**Hot path (after warmup, compensation complete):**

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| FMA | 2 | 4 | 8 |
| MUL | 1 | 3 | 3 |
| **Total** | **3** |  | **~11 cycles** |

The hot path consists of:
1. Zero-lag signal: `FMA(2.0, val, -lagged)`  1 FMA
2. EMA core: `FMA(zlemaRaw, beta, alpha * signal)`  1 FMA + 1 MUL

**Warmup path (with bias compensation):**

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| FMA | 2 | 4 | 8 |
| MUL | 2 | 3 | 6 |
| DIV | 1 | 15 | 15 |
| CMP | 2 | 1 | 2 |
| **Total** | **7** |  | **~31 cycles** |

Additional warmup operations:
- Decay tracking: `e *= beta`  1 MUL
- Bias compensation: `zlemaRaw / (1 - e)`  1 DIV
- Hot/compensated checks  2 CMP

### Batch Mode (SIMD Analysis)

ZLEMA is an IIR filter with lag buffer dependency  not directly vectorizable across bars. However, within-bar operations use FMA intrinsics.

| Optimization | Benefit |
| :--- | :--- |
| FMA instructions | ~11 cycles vs ~14 scalar |
| stackalloc buffer | Zero heap allocation for lag d256 |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 8/10 | Matches PineScript reference |
| **Timeliness** | 8/10 | Faster response than EMA |
| **Overshoot** | 6/10 | Predictive signal causes overshoot on reversals |
| **Smoothness** | 7/10 | Between EMA and raw price |

## Validation

ZLEMA is validated against a PineScript reference implementation.

| Library | Status | Tolerance | Notes |
|:---|:---|:---|:---|
| **TA-Lib** | N/A | - | No ZLEMA in TA-Lib |
| **Skender** | N/A | - | No ZLEMA in Skender |
| **Tulip** | Partial | - | Tulip has `zlema` but not used here |
| **Ooples** | N/A | - | No ZLEMA in Ooples |
| **PineScript** | ? Passed | 1e-10 | Matches `lib/trends_IIR/zlema/zlema.pine` |

## Common Pitfalls

1. **Overshoot on turns**

   The zero-lag signal is a forward estimate. It can overshoot when price reverses sharply. This is expected behavior.

2. **Period semantics**

   ZLEMA uses EMA alpha; the lag term is derived from period but not equivalent to a window length. Do not compare ZLEMA period directly to SMA window length.

3. **Warmup discipline**

   Use `IsHot` / `WarmupPeriod` before acting on signals. Early values are bias-corrected but still unstable.

4. **Non-finite data**

   NaN or Infinity is replaced with the last valid value. Before the first valid sample, output is `NaN`.
