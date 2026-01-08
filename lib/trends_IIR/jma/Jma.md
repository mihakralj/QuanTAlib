# JMA: Jurik Moving Average

> "The Ferrari of moving averages. Fast, smooth, and expensive (computationally). It tracks price like a heat-seeking missile."

JMA (Jurik Moving Average) is widely considered the gold standard for adaptive smoothing. Developed by Mark Jurik, it offers an unparalleled combination of noise reduction and minimal lag. It achieves this through a complex, multi-stage algorithm that adapts its internal parameters based on the fractal dimension and volatility of the data.

## Historical Context

Mark Jurik kept the JMA algorithm a trade secret for years. It was sold as a "black box" library. Eventually, reverse-engineered versions appeared, revealing a sophisticated mix of volatility-adjusted smoothing and Kalman-like filtering. The QuanTAlib implementation is based on these high-fidelity reconstructions.

## Architecture & Physics

JMA is not a simple FIR or IIR filter. It's a dynamic system.

1. **Volatility Assessment**: It calculates a 10-bar SMA of local deviation and compares it to a 128-bar volatility history (using a trimmed mean).
2. **Fractal Efficiency**: It computes a dynamic exponent based on the ratio of current change to historical volatility.
3. **Adaptive Smoothing**: It uses this exponent to drive a 2-pole IIR filter that speeds up when the market moves and slows down when it chops.

## Mathematical Foundation

The core update logic involves a dynamic alpha $\alpha$:

$$ \text{Ratio} = \frac{\text{AbsDiff}}{\text{Volatility}} $$

$$ d = \text{Ratio}^{\text{Power}} $$

$$ \alpha = \text{LengthDivider}^d $$

$$ \text{JMA}_t = (1 - \alpha) P_t + \alpha \text{JMA}_{t-1} + \dots $$

(The full formula involves multiple feedback loops and phase adjustments).

## Performance Profile

JMA is computationally expensive compared to an EMA, but still fast enough for real-time use.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | [N] ns/bar | Complex algorithm |
| **Allocations** | 0 | Stack-based calculations only |
| **Complexity** | O(1) | Constant time update |
| **Accuracy** | 9/10 | Tracks price action with high fidelity |
| **Timeliness** | 9/10 | Minimal lag due to adaptive phase |
| **Overshoot** | 8/10 | Controlled overshoot, adjustable via phase |
| **Smoothness** | 9/10 | Exceptional noise reduction |

## Validation

Validated against known JMA outputs from other platforms (e.g., AmiBroker, NinjaTrader).

| Library | Status | Notes |
| :--- | :--- | :--- |
| **TA-Lib** | N/A | Not implemented |
| **Skender** | N/A | Not implemented |
| **Reverse Eng.** | ✅ | Matches standard decompiled logic |

| **Tulip** | N/A | Not implemented. |
| **Ooples** | N/A | Not implemented. |
### Common Pitfalls

1. **Phase Parameter**: The `phase` parameter controls overshoot. Positive values (up to 100) make it overshoot like a DEMA. Negative values make it lag more but smoother. 0 is neutral.
2. **Warmup**: JMA needs a *long* warmup (65+ bars) to build its volatility history. Do not trust the first 100 bars.
3. **Complexity**: This is the most complex moving average in the library. If you need simple, use EMA. If you need magic, use JMA.
