# HT_DCPHASE: Hilbert Transform - Dominant Cycle Phase

[Pine Script Implementation of HT_DCPHASE](https://github.com/mihakralj/pinescript/blob/main/indicators/cycles/ht_dcphase.pine)

## Overview and Purpose

The Hilbert Transform Dominant Cycle Phase (HT_DCPHASE) is an advanced cycle analysis indicator developed by John Ehlers that identifies the current phase position within the dominant market cycle. By applying Hilbert Transform mathematics to price data, this indicator extracts the phase angle of the dominant cycle, revealing where the market currently sits within its cyclical pattern. This information is invaluable for timing entries and exits, as it shows whether the cycle is in accumulation, markup, distribution, or markdown phases.

HT_DCPHASE works by computing the In-phase (I) and Quadrature (Q) components through Hilbert Transform analysis, then calculating the phase angle as the arctangent of Q/I. The result is a continuous phase measurement in radians ranging from -π to π, providing a precise indication of cycle position. This makes it particularly useful for identifying cycle turning points and anticipating trend changes before they become apparent in price action.

## Core Concepts

* **Phase Angle**: Measures position within cycle using arctangent of Q/I components; ranges from -π to π radians
* **Hilbert Transform**: Mathematical technique that creates 90-degree phase-shifted version of price for quadrature analysis
* **I and Q Components**: In-phase and Quadrature components represent cycle's position in two-dimensional phase space
* **Cycle Position**: Phase angle indicates whether market is in trough (-π), peak (0), or transition phases (±π/2)
* **Adaptive Bandwidth**: Uses dominant cycle period to adjust filter bandwidth for optimal detrending

## Common Settings and Parameters

| Parameter | Default | Function | When to Adjust |
| ------ | ------ | ------ | ------ |
| Source | hlc3 | Price data for analysis | Use close for simpler signals; hlc3 for smoother, more comprehensive cycle detection |

**Pro Tip:** HT_DCPHASE is most effective when used in conjunction with HT_DCPERIOD to understand both the cycle length and current position. Phase crossings through zero often correspond to significant trend changes. The indicator works best on instruments with clear cyclical behavior - sideways or ranging markets provide cleaner signals than strongly trending markets.

## Calculation and Mathematical Foundation

**Simplified explanation:**
HT_DCPHASE applies Hilbert Transform mathematics to extract the phase angle of the dominant market cycle, indicating the current position within the cycle.

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

7. Calculate phase angle:
   ```
   Phase = atan(Q2 / I2)
   ```

Where `Hilbert_FIR` is a finite impulse response filter with coefficients [0.0962, 0.5769, 0, -0.5769, -0.0962].

> 🔍 **Technical Note:** The phase calculation uses arctangent to convert the I and Q components from Cartesian to polar coordinates. The dominant cycle period (calculated from Re and Im) is used to adapt the filter bandwidth, ensuring the phase measurement tracks the actual market cycle rather than noise or shorter-term fluctuations.

## Interpretation Details

HT_DCPHASE provides cycle phase analysis through several interpretive lenses:

* **Phase Position:**
  * Phase ≈ -π: Cycle trough (potential buy zone)
  * Phase ≈ -π/2: Rising from trough (early uptrend)
  * Phase ≈ 0: Cycle peak (potential sell zone)
  * Phase ≈ π/2: Declining from peak (early downtrend)

* **Phase Levels:**
  * Phase = 0: Cycle peak reached (distribution zone)
  * Phase = ±π: Cycle trough reached (accumulation zone)
  * Phase transitions through these levels indicate cycle progression
  * Watch for price behavior at these phase extremes

* **Phase Velocity:**
  * Rapid phase changes indicate strong momentum
  * Slow phase progression suggests consolidation
  * Stalled phase can indicate cycle transition or mode change

* **Cycle Synchronization:**
  * Use with HT_DCPERIOD to confirm cycle consistency
  * Phase leads price by design, providing early signals
  * Most reliable in ranging or cyclical market conditions

* **Quadrant Analysis:**
  * Quadrant I (0 to π/2): Early decline phase
  * Quadrant II (π/2 to π): Late decline phase
  * Quadrant III (-π to -π/2): Late rise phase
  * Quadrant IV (-π/2 to 0): Early rise phase

## Limitations and Considerations

* **Trend Dependence:** Less reliable in strong trending markets; works best in cyclical or ranging conditions
* **Phase Wrapping:** Discontinuities at ±π boundaries require careful interpretation of phase transitions
* **Lag Component:** Smoothing introduces slight lag; phase leads price but not instantaneously
* **Noise Sensitivity:** Can produce erratic signals in highly volatile or choppy markets without clear cycles
* **Cycle Assumption:** Assumes presence of dominant cycle; may give spurious signals in random walk conditions
* **Parameter Adaptation:** Uses previous period for bandwidth calculation; may lag during rapid cycle changes

## Performance Profile

### Operation Count (Streaming Mode, per Bar)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ADD/SUB | ~24 | 1 | 24 |
| MUL | ~28 | 3 | 84 |
| DIV | 1 | 15 | 15 |
| ATAN | 1 | 80 | 80 |
| **Total** | **~54** | — | **~203 cycles** |

**Breakdown:**
- Weighted smooth (4-point): 3 MUL + 3 ADD = 12 cycles
- Detrender FIR (4 taps × bandwidth): 5 MUL + 3 ADD = 18 cycles
- Q1 FIR: 5 MUL + 3 ADD = 18 cycles
- jI/jQ phase advance FIRs: 10 MUL + 6 ADD = 36 cycles
- I2/Q2 phasor smoothing: 4 MUL + 4 ADD = 16 cycles
- Phase = atan(Q2/I2): 1 DIV + 1 ATAN = 95 cycles

### Complexity Analysis

| Mode | Complexity | Notes |
| :--- | :---: | :--- |
| Streaming | O(1) | Fixed 6-bar FIR history + IIR states |
| Batch | O(n) | Linear scan, constant work per bar |

**Memory**: ~120 bytes (6-bar history buffers + IIR states)

### SIMD Analysis

| Optimization | Applicable | Notes |
| :--- | :---: | :--- |
| AVX2 vectorization | Limited | FIR taps vectorizable, IIRs sequential |
| FMA | ✅ | Hilbert FIR: `0.0962×x + 0.5769×x[2] - ...` |
| Batch parallelism | ❌ | IIR feedback prevents cross-bar parallelism |

**Optimization Notes:** Nearly identical to HT_DCPERIOD. Atan dominates cost (~39%). Phase output is simpler than period conversion.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | Phase derived from mathematically exact HT |
| **Timeliness** | 7/10 | ~3 bar delay from FIR kernel |
| **Overshoot** | 7/10 | Phase wrapping at ±π can cause jumps |
| **Smoothness** | 7/10 | IIR smoothing helps, but wrapping remains |

## References

* Ehlers, J. F. (2004). "Cybernetic Analysis for Stocks and Futures." John Wiley & Sons.
* Ehlers, J. F. (2001). "Rocket Science for Traders: Digital Signal Processing Applications." John Wiley & Sons.
* Ehlers, J. F. (2013). "Cycle Analytics for Traders: Advanced Technical Trading Concepts." John Wiley & Sons.