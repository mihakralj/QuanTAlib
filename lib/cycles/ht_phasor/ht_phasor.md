# HT_PHASOR: Hilbert Transform - Phasor Components

[Pine Script Implementation of HT_PHASOR](https://github.com/mihakralj/pinescript/blob/main/indicators/cycles/ht_phasor.pine)

## Overview and Purpose

The Hilbert Transform Phasor Components (HT_PHASOR) is an advanced cycle analysis indicator developed by John Ehlers that provides direct access to the In-phase (I) and Quadrature (Q) components of the dominant market cycle. Unlike HT_DCPHASE which derives the phase angle from these components, HT_PHASOR exposes the raw I and Q values themselves, allowing traders and analysts to construct custom cycle indicators or perform advanced signal processing techniques.

The phasor components represent the cycle in two-dimensional phase space, where the I component is the detrended price delayed by a quarter cycle, and the Q component is a 90-degree phase-shifted version of the detrended price. Together, these components form a complex phasor that rotates through phase space as the market cycles, with the magnitude representing cycle amplitude and the angle representing phase position. This dual representation is invaluable for understanding both the strength and position of market cycles.

## Core Concepts

* **In-Phase Component (I)**: The detrended price delayed by quarter cycle; represents the "real" part of the cycle phasor
* **Quadrature Component (Q)**: 90-degree phase-shifted detrended price; represents the "imaginary" part of the cycle phasor
* **Phasor Representation**: I and Q together form a rotating vector in 2D phase space tracking cycle evolution
* **Complex Analysis**: Enables computation of amplitude (√(I²+Q²)), phase (atan2(Q,I)), and frequency
* **Adaptive Processing**: Uses dominant cycle period to adjust bandwidth for optimal component extraction

## Common Settings and Parameters

| Parameter | Default | Function | When to Adjust |
| ------ | ------ | ------ | ------ |
| Source | source | Data source for analysis | Use close for simpler signals; hlc3 for smoother, more comprehensive cycle detection |

**Pro Tip:** HT_PHASOR is primarily useful for custom indicator development and advanced cycle analysis. The I and Q components can be used to calculate amplitude (cycle strength), phase (cycle position), and instantaneous frequency. When I and Q oscillate with constant magnitude, the market is in a strong cyclical mode. When their magnitudes vary significantly, the market may be transitioning between cycle and trend modes.

## Calculation and Mathematical Foundation

**Simplified explanation:**
HT_PHASOR applies Hilbert Transform mathematics to extract the In-phase and Quadrature components, which represent the dominant cycle as a rotating vector in 2D phase space.

**Technical formula:**

1. Smooth the price data:
   ```
   SmoothPrice = (4×Price + 3×Price[1] + 2×Price[2] + Price[3]) / 10
   ```

2. Detrend with adaptive bandwidth:
   ```
   Bandwidth = 0.075 × Period[1] + 0.54
   Detrender = Hilbert_FIR(SmoothPrice) × Bandwidth
   ```

3. Calculate Quadrature component (90° phase shift):
   ```
   Q1 = Hilbert_FIR(Detrender) × Bandwidth
   ```

4. Calculate In-phase component (delayed detrend):
   ```
   I1 = Detrender[3]
   ```

5. Apply Hilbert Transform to get jI and jQ:
   ```
   jI = Hilbert_FIR(I1) × Bandwidth
   jQ = Hilbert_FIR(Q1) × Bandwidth
   ```

6. Compute smoothed I2 and Q2:
   ```
   I2 = I1 - jQ
   Q2 = Q1 + jI
   I2 = 0.2×I2 + 0.8×I2[1]  (smooth)
   Q2 = 0.2×Q2 + 0.8×Q2[1]  (smooth)
   ```

7. Return both components:
   ```
   return [I2, Q2]
   ```

Where `Hilbert_FIR` is a finite impulse response filter with coefficients [0.0962, 0.5769, 0, -0.5769, -0.0962].

> 🔍 **Technical Note:** The I and Q components form a complex number representation of the cycle. The dominant cycle period is calculated internally and used to adapt the bandwidth, but the phasor components themselves are the primary output. These can be used to derive amplitude (magnitude = √(I²+Q²)), phase (angle = atan2(Q,I)), and rate of change of phase (instantaneous frequency).

## Interpretation Details

HT_PHASOR provides direct access to cycle components for advanced analysis:

* **Component Oscillation:**
  * Both I and Q oscillate around zero
  * Amplitude of oscillation indicates cycle strength
  * Regular sinusoidal patterns indicate clean cycles
  * Irregular patterns suggest trending or transitional periods

* **Phasor Magnitude (√(I²+Q²)):**
  * Large magnitude: Strong cyclical behavior
  * Small magnitude: Weak cycle or trending phase
  * Constant magnitude: Pure cycle mode
  * Varying magnitude: Mixed cycle/trend mode

* **Phase Angle (atan2(Q,I)):**
  * Derived phase ranges from -π to π
  * Constant rotation rate indicates steady cycle
  * Accelerating rotation suggests cycle compression
  * Decelerating rotation suggests cycle expansion

* **Component Relationships:**
  * I and Q approximately 90° out of phase in clean cycles
  * Loss of quadrature relationship indicates trend dominance
  * Relative magnitudes reveal cycle shape distortions
  * Sign changes indicate cycle progression through quadrants

* **Custom Indicator Construction:**
  * Amplitude: `sqrt(I² + Q²)` for cycle strength
  * Phase: `atan2(Q, I)` for cycle position
  * Frequency: Rate of change of phase angle
  * Power: `I² + Q²` for energy without sqrt overhead

## Performance Profile

### Operation Count (Streaming Mode, per Bar)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ADD/SUB | 28 | 1 | 28 |
| MUL | 32 | 3 | 96 |
| DIV | 2 | 15 | 30 |
| **Total** | **62** | — | **~154 cycles** |

**Breakdown:**

- **Price smoothing** (WMA-4): 4 MUL + 3 ADD + 1 DIV = ~20 cycles
- **Hilbert FIR (Detrender)**: 4 MUL + 4 ADD = ~16 cycles
- **Bandwidth adaptation**: 2 MUL + 1 ADD = ~7 cycles
- **Hilbert FIR (Q1)**: 4 MUL + 4 ADD = ~16 cycles
- **Hilbert FIR (jI)**: 4 MUL + 4 ADD = ~16 cycles
- **Hilbert FIR (jQ)**: 4 MUL + 4 ADD = ~16 cycles
- **I2/Q2 computation**: 2 ADD/SUB = ~2 cycles
- **EMA smoothing (×2)**: 4 MUL + 4 ADD = ~16 cycles
- **Period calculation** (internal): ~45 cycles (includes atan)

Note: Unlike HT_DCPHASE, HT_PHASOR outputs raw I/Q components without final atan2, saving ~80 cycles. Period calculation is internal for bandwidth adaptation.

### Complexity Analysis

| Mode | Complexity | Notes |
| :--- | :---: | :--- |
| Streaming | O(1) | Fixed Hilbert FIR taps, EMA smoothing |
| Batch | O(n) | Linear scan over price bars |

**Memory**: ~120 bytes (state variables for 4 Hilbert FIRs, smoothed I2/Q2, period tracking)

### SIMD Analysis

| Optimization | Applicable | Notes |
| :--- | :---: | :--- |
| AVX2 vectorization | ❌ | Recursive IIR (EMA) dependencies |
| FMA | ✅ | EMA smoothing: `prev * 0.8 + curr * 0.2` |
| Batch parallelism | ❌ | State-dependent recursion |

**FMA opportunities:**

- EMA smoothing uses `a*b + c` pattern
- Hilbert FIR coefficients are fixed, enabling compile-time optimization

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 8/10 | High-quality phasor extraction via Hilbert Transform |
| **Timeliness** | 6/10 | ~3-bar inherent delay from FIR smoothing |
| **Flexibility** | 9/10 | Raw I/Q enables custom amplitude/phase derivation |
| **Smoothness** | 7/10 | EMA smoothing reduces noise; some jitter in transitions |

## Limitations and Considerations

* **Raw Components:** Less intuitive than derived metrics (phase, amplitude); requires understanding of complex analysis
* **Trend Dependence:** Component values less meaningful in strong trending markets
* **Computation Required:** User must compute derived metrics (amplitude, phase) from I and Q components
* **Noise Sensitivity:** Can show erratic behavior in choppy markets without clear cycles
* **Cycle Assumption:** Assumes dominant cycle exists; questionable in random walk conditions
* **Advanced Tool:** Primarily for custom indicator development and algorithmic trading applications

## References

* Ehlers, J. F. (2004). "Cybernetic Analysis for Stocks and Futures." John Wiley & Sons.
* Ehlers, J. F. (2001). "Rocket Science for Traders: Digital Signal Processing Applications." John Wiley & Sons.
* Ehlers, J. F. (2013). "Cycle Analytics for Traders: Advanced Technical Trading Concepts." John Wiley & Sons.