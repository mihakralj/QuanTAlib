# SSF2: Ehlers 2-Pole Super Smoother Filter

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Filter                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Ssf2)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |
| **Signature**    | [ssf2_signature](ssf2_signature.md) |

### TL;DR

- The 2-Pole Super Smooth Filter (SSF2) is a 2-pole Butterworth filter designed by John Ehlers.
- Parameterized by `period`.
- Output range: Tracks input.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "Noise is the enemy of the trend follower. The Super Smooth Filter is the silencer."

The 2-Pole Super Smooth Filter (SSF2) is a 2-pole Butterworth filter designed by John Ehlers. It offers superior noise reduction compared to standard moving averages while maintaining minimal lag. By using complex conjugate poles, it achieves a "maximally flat" response in the passband, meaning it preserves the trend signal with high fidelity while aggressively suppressing high-frequency noise.

## Historical Context

John Ehlers introduced the Super Smooth Filter to address the limitations of traditional filters like the EMA and SMA, which often sacrifice responsiveness for smoothness. The SSF2 uses digital signal processing (DSP) principles to achieve an optimal balance, making it a favorite among quantitative traders who need clean signals for algorithmic systems.

## Architecture & Physics

The SSF2 is an Infinite Impulse Response (IIR) filter.

* **2-Pole Design**: Uses two poles in the Z-domain to create a sharper cutoff than single-pole filters (like EMA).
* **Butterworth Characteristic**: Maximally flat passband response, minimizing distortion of the trend.
* **Minimal Lag**: Despite its smoothing power, it reacts relatively quickly to significant price changes.

## Mathematical Foundation

The filter coefficients are derived from the desired cutoff period:

$$ \text{arg} = \frac{\pi \sqrt{2}}{N} $$

$$ c_2 = 2 e^{-\text{arg}} \cos(\text{arg}) $$

$$ c_3 = -e^{-2 \cdot \text{arg}} $$

$$ c_1 = 1 - c_2 - c_3 $$

The recursive formula for the filter is:

$$ \text{SSF2}_t = c_1 \cdot \frac{P_t + P_{t-1}}{2} + c_2 \cdot \text{SSF2}_{t-1} + c_3 \cdot \text{SSF2}_{t-2} $$

Where:

* $P_t$ is the current price.
* $P_{t-1}$ is the previous price.
* $\text{SSF2}_{t-1}$ and $\text{SSF2}_{t-2}$ are the previous filter outputs.

> **Note:** This implementation uses high-precision constants (`Math.Sqrt(2)` and `Math.PI`) rather than the approximations (`1.414` and `3.14159`) found in some reference implementations.

## Performance Profile

### Operation Count (Streaming Mode)

Two-pole Super Smooth Filter (SSF2): Ehlers 2nd-order IIR low-pass smoother. Three FMA operations per bar.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Input combination | 1 | ~2 cy | ~2 cy |
| FMA output: c1*(x+x1) + c2*y1 + c3*y2 | 3 | ~4 cy | ~12 cy |
| State update | 2 | ~1 cy | ~2 cy |
| **Total** | **6** | — | **~16 cycles** |

O(1) per bar. Coefficients derived from period parameter; precomputed. ~16 cycles/bar.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| SSF2 recursion | No | y[n] depends on y[n-1] and y[n-2] |

Batch throughput: ~16 cy/bar.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | 10 | Very high; few multiplications and additions per bar. |
| **Allocations** | 0 | Zero-allocation in hot paths. |
| **Complexity** | O(1) | Recursive calculation. |
| **Accuracy** | 9 | Excellent noise suppression. |
| **Timeliness** | 8 | Low lag for the amount of smoothing. |
| **Overshoot** | 8 | Minimal overshoot due to Butterworth design. |
| **Smoothness** | 9 | Superior to EMA/SMA. |

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Validated. |
| **TA-Lib** | N/A | Not implemented. |
| **Skender** | N/A | Not implemented. |
| **Tulip** | N/A | Not implemented. |
| **Ooples** | ⚠️ | Matches `CalculateEhlersSuperSmootherFilter` with deviation due to our use of high-precision constants (`Math.Sqrt(2)`, `Math.PI`) vs Ooples' shallow approximations (`1.414`, `3.14159`). |

### Common Pitfalls

1. **Initialization**: The filter requires a few bars to stabilize. Per Ehlers' design, the output is set to the input price for the first 4 bars.
2. **Period Selection**: Unlike an SMA, the "Period" $N$ in SSF2 refers to the cutoff wavelength. A period of 10 means it filters out cycles shorter than 10 bars. It is roughly comparable to an EMA of the same length but smoother.
