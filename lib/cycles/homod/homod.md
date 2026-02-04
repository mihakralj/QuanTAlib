# HOMOD: Homodyne Discriminator

> "The homodyne discriminator reveals instantaneous frequency by multiplying a signal with its delayed self — the phase rotation between samples directly encodes the cycle period."

The Homodyne Discriminator, developed by John Ehlers, estimates the dominant cycle period in market data using homodyne multiplication and phase angle measurement. Unlike spectral methods that analyze frequency bins, homodyne detection measures the instantaneous phase change between consecutive samples, providing responsive and noise-resistant cycle detection.

## Historical Context

John Ehlers introduced the Homodyne Discriminator as part of his work on applying communications signal processing to financial markets. The term "homodyne" comes from radio engineering, where it describes a detection method that multiplies a signal with a locally generated reference at the same frequency.

In Ehlers' adaptation, the indicator generates its own reference signals (I and Q components) using Hilbert Transform approximations, then multiplies the analytic signal with its delayed version. The resulting real and imaginary components encode the instantaneous phase difference, from which the cycle period is extracted.

The key innovation is that homodyne detection measures phase rate of change directly, rather than inferring it from spectral peaks. This makes the algorithm more responsive to cycle changes while maintaining noise immunity through multiple smoothing stages.

This implementation follows Ehlers' PineScript formulation, which includes:

- 4-bar weighted moving average for input smoothing
- Hilbert Transform via FIR coefficients [0.0962, 0, 0.5769, 0, -0.5769, 0, -0.0962]
- Bandwidth adaptation based on estimated period
- Homodyne mixing with 1-bar delay
- Multiple EMA smoothing stages (α = 0.2 and α = 0.33)
- Exponential warmup compensation

## Architecture & Physics

### 1. Input Smoothing (4-bar WMA)

The first stage smooths the input price using a weighted moving average:

$$
\text{Smooth}_t = \frac{4P_t + 3P_{t-1} + 2P_{t-2} + P_{t-3}}{10}
$$

This removes high-frequency noise while introducing minimal phase shift in the cycle detection range.

### 2. Bandwidth Calculation

The Hilbert Transform coefficients are scaled by a bandwidth factor that adapts to the estimated period:

$$
\text{BW}_t = 0.075 \cdot \text{SmoothPeriod}_{t-1} + 0.54
$$

This creates a feedback loop where the bandwidth narrows as shorter cycles are detected and widens for longer cycles, improving detection accuracy.

### 3. Hilbert Transform (Detrender)

The detrender applies the Hilbert Transform coefficients to the smoothed price:

$$
\text{Det}_t = (0.0962 \cdot S_t + 0.5769 \cdot S_{t-2} - 0.5769 \cdot S_{t-4} - 0.0962 \cdot S_{t-6}) \cdot \text{BW}
$$

where $S_t$ is the smoothed price. This produces the in-phase (I) component with approximately 90° phase shift.

### 4. Quadrature Component (Q1)

The quadrature component applies the same Hilbert Transform to the detrender:

$$
Q1_t = (0.0962 \cdot D_t + 0.5769 \cdot D_{t-2} - 0.5769 \cdot D_{t-4} - 0.0962 \cdot D_{t-6}) \cdot \text{BW}
$$

The in-phase component is simply the detrender delayed by 3 bars:

$$
I1_t = D_{t-3}
$$

### 5. Phase Rotation (JI and JQ)

Additional Hilbert Transforms compute the phase-rotated versions:

$$
JI_t = (0.0962 \cdot I1_t + 0.5769 \cdot I1_{t-2} - 0.5769 \cdot I1_{t-4} - 0.0962 \cdot I1_{t-6}) \cdot \text{BW}
$$

$$
JQ_t = (0.0962 \cdot Q1_t + 0.5769 \cdot Q1_{t-2} - 0.5769 \cdot Q1_{t-4} - 0.0962 \cdot Q1_{t-6}) \cdot \text{BW}
$$

### 6. Analytic Signal (I2 and Q2)

The final I and Q components combine the original and rotated signals:

$$
I2_{\text{raw}} = I1 - JQ
$$

$$
Q2_{\text{raw}} = Q1 + JI
$$

These are smoothed with an EMA (α = 0.2):

$$
I2_t = 0.2 \cdot I2_{\text{raw}} + 0.8 \cdot I2_{t-1}
$$

$$
Q2_t = 0.2 \cdot Q2_{\text{raw}} + 0.8 \cdot Q2_{t-1}
$$

### 7. Homodyne Multiplication

The homodyne discriminator multiplies the current analytic signal with its previous value:

$$
\text{Re}_{\text{raw}} = I2_t \cdot I2_{t-1} + Q2_t \cdot Q2_{t-1}
$$

$$
\text{Im}_{\text{raw}} = I2_t \cdot Q2_{t-1} - Q2_t \cdot I2_{t-1}
$$

Smoothed with EMA (α = 0.2):

$$
\text{Re}_t = 0.2 \cdot \text{Re}_{\text{raw}} + 0.8 \cdot \text{Re}_{t-1}
$$

$$
\text{Im}_t = 0.2 \cdot \text{Im}_{\text{raw}} + 0.8 \cdot \text{Im}_{t-1}
$$

### 8. Period Extraction

The instantaneous angular frequency is extracted from the phase angle:

$$
\theta = \text{atan2}(\text{Im}, \text{Re})
$$

$$
\text{Period}_{\text{candidate}} = \frac{2\pi}{\theta}
$$

The period is clamped and smoothed:

$$
\text{Period}_t = 0.2 \cdot \text{clamp}(|\text{candidate}|, \text{minPeriod}, \text{maxPeriod}) + 0.8 \cdot \text{Period}_{t-1}
$$

### 9. Final Smoothing

An additional EMA with α = 0.33 provides the final output:

$$
\text{SmoothPeriod}_t = \text{SmoothPeriod}_{t-1} + 0.33 \cdot (\text{Period}_t - \text{SmoothPeriod}_{t-1})
$$

### 10. Warmup Compensation

During warmup, exponential compensation accelerates convergence:

$$
\text{decay}_t = \text{decay}_{t-1} \cdot (1 - \alpha)
$$

$$
\text{Result}_t = \frac{\text{SmoothPeriod}_t}{1 - \text{decay}_t}
$$

## Mathematical Foundation

### Homodyne Detection Principle

In communications, homodyne detection multiplies a received signal $s(t)$ with a local oscillator at the same frequency $\omega_0$:

$$
s(t) \cdot \cos(\omega_0 t) = A(t) \cos(\omega_0 t + \phi(t)) \cdot \cos(\omega_0 t)
$$

Using the product-to-sum identity:

$$
= \frac{A(t)}{2}[\cos(\phi(t)) + \cos(2\omega_0 t + \phi(t))]
$$

Low-pass filtering removes the double-frequency term, leaving the phase information.

### Analytic Signal Representation

The analytic signal $z(t)$ is the original signal plus $j$ times its Hilbert transform:

$$
z(t) = x(t) + jH\{x(t)\} = A(t)e^{j\phi(t)}
$$

Multiplying consecutive samples:

$$
z(t) \cdot z^*(t-\Delta t) = A(t)A(t-\Delta t)e^{j[\phi(t) - \phi(t-\Delta t)]}
$$

The phase difference $\Delta\phi = \phi(t) - \phi(t-\Delta t)$ directly encodes the instantaneous frequency:

$$
\omega = \frac{\Delta\phi}{\Delta t}
$$

### Hilbert Transform Approximation

The FIR coefficients [0.0962, 0, 0.5769, 0, -0.5769, 0, -0.0962] approximate the ideal Hilbert transform:

$$
H(\omega) = \begin{cases}
-j & \omega > 0 \\
+j & \omega < 0
\end{cases}
$$

The zeros at odd indices ensure only 90° phase shift without amplitude distortion at the center frequency.

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| 4-bar WMA | 4 MUL, 3 ADD, 1 DIV | 20 | 20 |
| Bandwidth calc | 2 MUL, 1 ADD | 7 | 7 |
| Detrender (HT) | 4 MUL, 3 ADD | 15 | 15 |
| Q1 (HT) | 4 MUL, 3 ADD | 15 | 15 |
| JI, JQ (HT×2) | 8 MUL, 6 ADD | 30 | 30 |
| I2, Q2 (EMA×2) | 4 MUL, 2 ADD | 14 | 14 |
| Re, Im (homodyne) | 4 MUL, 2 ADD/SUB | 14 | 14 |
| Re, Im (EMA×2) | 4 MUL, 2 ADD | 14 | 14 |
| atan2 | 1 DIV, 1 ATAN, CMP | 25 | 25 |
| Period calc | 1 DIV, 2 MUL, ADD | 25 | 25 |
| Smooth period (EMA) | 2 MUL, 2 ADD | 8 | 8 |
| Warmup comp | 2 MUL, 1 DIV, CMP | 20 | 20 |
| **Total** | — | — | **~220 cycles** |

The homodyne discriminator is computationally efficient at O(1) per bar, dominated by the atan2 calculation and multiple Hilbert Transforms.

### Batch Mode (512 values, SIMD/FMA)

The recursive nature of EMA smoothing limits SIMD applicability. However:

| Operation | Scalar Ops | SIMD Potential | Notes |
| :--- | :---: | :---: | :--- |
| Hilbert coeffs | 4 MUL + 3 ADD | Partially | Indexed memory limits gains |
| EMA smoothing | Sequential | None | Data dependency chain |
| atan2 | 1 per bar | None | Scalar intrinsic |

**Expected SIMD speedup:** ~1.1x (marginal due to recursion)

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 7/10 | Good for clean cycles; degrades with noise |
| **Timeliness** | 8/10 | More responsive than spectral methods |
| **Overshoot** | 8/10 | Clamping prevents extreme values |
| **Smoothness** | 8/10 | Multiple EMA stages reduce jitter |
| **Noise Rejection** | 7/10 | Adaptive bandwidth provides moderate filtering |

## Validation

HOMOD is a proprietary Ehlers indicator with limited external implementations.

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | Not implemented |
| **Skender** | N/A | Not implemented |
| **Tulip** | N/A | Not implemented |
| **Ooples** | N/A | Not implemented |
| **PineScript** | ✅ | Reference implementation |
| **MQL5** | ✅ | Adaptive Lookback Homodyne variant |

Validation is performed against:

- Mathematical properties (bounded output, sine wave detection)
- PineScript formula verification
- Streaming vs batch consistency
- Mode parity (TSeries, Span, events)

## Common Pitfalls

1. **Warmup Period**: HOMOD requires approximately 2×maxPeriod bars to stabilize. The exponential warmup compensation helps but does not eliminate bias. Always check `IsHot` before using results for trading decisions.

2. **Constant Input Handling**: With constant price input, the Hilbert Transform outputs approach zero, making the atan2 calculation undefined. The implementation guards against this with magnitude checks (> 1e-10).

3. **Parameter Range**: The minPeriod/maxPeriod range must bracket the expected cycle. Unlike spectral methods, homodyne detection has no frequency bins — it produces a single period estimate. If the true cycle is far outside the range, the clamping will bias results toward the boundary.

4. **Trending Markets**: Strong trends produce low-frequency bias in the analytic signal. The period estimate will tend toward maxPeriod during sustained moves. Use additional trend filters if cycle detection during trends is required.

5. **Memory Footprint**: Each instance maintains ~40 state variables for the cascaded filters and history buffers. Per-instance memory is approximately 320 bytes.

6. **Atan2 Implementation**: The custom atan2 function matches PineScript behavior for consistency. Standard library atan2 may differ at edge cases (both arguments zero). This implementation returns 0 for robustness.

## References

- Ehlers, J.F. (2001). "Rocket Science for Traders." Wiley.
- Ehlers, J.F. (2004). "Cybernetic Analysis for Stocks and Futures." Wiley.
- Ehlers, J.F. "Homodyne Discriminator." Technical Analysis of Stocks & Commodities.
- Lyons, R.G. (2011). "Understanding Digital Signal Processing." 3rd ed. Prentice Hall.
- Oppenheim, A.V., Schafer, R.W. (2010). "Discrete-Time Signal Processing." 3rd ed. Pearson.