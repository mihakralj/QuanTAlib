# ZLEMA: Zero-Lag Exponential Moving Average

## EMA with lag compensation via a zero-lag signal

> "ZLEMA does not erase lag. It predicts just enough to act early, then pays the price in overshoot."

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

| Metric | Score | Notes |
|:---|:---|:---|
| **Throughput** | TBD | Benchmark not captured yet |
| **Allocations** | 0 | Streaming update is allocation-free |
| **Complexity** | O(1) | Constant work per update |
| **Accuracy** | 8/10 | Matches PineScript reference |
| **Timeliness** | 8/10 | Faster response than EMA |
| **Overshoot** | 6/10 | More reactive, less stable |
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
