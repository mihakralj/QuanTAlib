# HT_SINE: Hilbert Transform - SineWave

[Pine Script Implementation of HT_SINE](https://github.com/mihakralj/pinescript/blob/main/indicators/cycles/ht_sine.pine)

## Overview and Purpose

The Hilbert Transform SineWave (HT_SINE) is a cycle visualization indicator developed by John Ehlers that generates sine and lead-sine wave plots based on the dominant market cycle identified through Hilbert Transform analysis. Unlike simple sine wave indicators that assume a fixed cycle period, HT_SINE adapts to the actual dominant cycle present in the market, providing a dynamic representation of cyclical behavior. The lead-sine component leads the sine wave, offering early signals of potential cycle turning points.

This indicator transforms the complex phase information from Hilbert Transform analysis into intuitive sine wave visualizations that oscillate between -1 and +1. By plotting both the sine wave (current cycle position) and lead-sine wave (advanced cycle position), traders can identify cycle peaks, troughs, and transitions. Crossovers between the sine and lead-sine waves often coincide with significant price turning points, making this a valuable tool for timing entries and exits in cyclical markets.

## Core Concepts

* **Sine Wave**: Visual representation of the dominant cycle position; oscillates smoothly between -1 and +1
* **Lead Sine Wave**: Phase-advanced version of sine wave; leads by delta_phase/period for early signals
* **Dynamic Phase**: Uses instantaneous phase from Hilbert Transform rather than fixed cycle assumption
* **Adaptive Cycle**: Automatically adjusts to dominant cycle period detected in price data
* **Crossover Signals**: Sine/LeadSine crossovers indicate potential cycle turning points

## Common Settings and Parameters

| Parameter | Default | Function | When to Adjust |
| ------ | ------ | ------ | ------ |
| Source | source | Data source for cycle analysis | Use close for simpler signals; hlc3 for smoother, more comprehensive cycle detection |

**Pro Tip:** Watch for crossovers between the sine and lead-sine waves as potential cycle reversal signals. When lead-sine crosses above sine near the trough (-1), it suggests an upcoming cycle bottom. When lead-sine crosses below sine near the peak (+1), it suggests an upcoming cycle top. The indicator works best in ranging or cyclical markets; strong trends can produce less reliable signals as the cycle assumption breaks down.

## Calculation and Mathematical Foundation

**Simplified explanation:**
HT_SINE uses Hilbert Transform to determine the dominant cycle's phase, then generates sine and lead-sine waves based on that phase for visual cycle representation.

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

3. Calculate Quadrature and In-phase components:
   ```
   Q1 = Hilbert_FIR(Detrender) × Bandwidth
   I1 = Detrender[3]
   ```

4. Apply Hilbert Transform:
   ```
   jI = Hilbert_FIR(I1) × Bandwidth
   jQ = Hilbert_FIR(Q1) × Bandwidth
   ```

5. Compute smoothed I2 and Q2:
   ```
   I2 = I1 - jQ
   Q2 = Q1 + jI
   I2 = 0.2×I2 + 0.8×I2[1]
   Q2 = 0.2×Q2 + 0.8×Q2[1]
   ```

6. Calculate phase using four-quadrant arctangent:
   ```
   if I2 > 0:
       Phase = atan(Q2 / I2)
   else if I2 < 0:
       Phase = atan(Q2 / I2) ± π
   else:
       Phase = ±π/2
   ```

7. Compute phase change and alpha:
   ```
   DeltaPhase = max(Phase[1] - Phase, 1.0)
   Alpha = DeltaPhase / Period
   ```

8. Generate sine waves:
   ```
   Sine = sin(Phase)
   LeadSine = sin(Phase + Alpha)
   ```

Where `Hilbert_FIR` is a finite impulse response filter with coefficients [0.0962, 0.5769, 0, -0.5769, -0.0962].

> 🔍 **Technical Note:** The lead-sine component is phase-advanced by alpha (DeltaPhase/Period), causing it to lead the sine wave. The minimum DeltaPhase constraint of 1.0 prevents division issues when phase changes slowly. The sine waves are bounded between -1 and +1, providing normalized cycle visualization regardless of price magnitude.

## Interpretation Details

HT_SINE provides cycle visualization and timing signals through multiple perspectives:

* **Wave Position:**
  * Sine ≈ +1: Cycle peak (potential sell zone)
  * Sine ≈ 0: Mid-cycle (transition zone)
  * Sine ≈ -1: Cycle trough (potential buy zone)
  * Regular oscillation indicates clean cyclical behavior

* **Crossover Signals:**
  * LeadSine crosses above Sine: Potential bullish reversal signal
  * LeadSine crosses below Sine: Potential bearish reversal signal
  * Crossovers near extremes (+1 or -1) are most reliable
  * Multiple rapid crossovers suggest choppy, non-cyclical conditions

* **Wave Separation:**
  * Wide separation: Strong, clear cycle in progress
  * Narrow separation: Weak or transitioning cycle
  * Consistent spacing: Steady cycle frequency
  * Erratic spacing: Cycle instability or trend dominance

* **Extreme Levels:**
  * Both waves at +1: Confirmed cycle peak
  * Both waves at -1: Confirmed cycle trough
  * Failure to reach extremes: Weakening cycle or trend emergence
  * Extended time at extremes: Possible trend rather than cycle

* **Lead-Lag Relationship:**
  * Lead-sine consistently ahead: Normal cycle mode
  * Lead-sine loses leadership: Cycle breaking down
  * Waves synchronizing: Transitioning to trend mode
  * Lead reversing direction first: Early warning signal

## Performance Profile

### Operation Count (Streaming Mode, per Bar)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ADD/SUB | 30 | 1 | 30 |
| MUL | 34 | 3 | 102 |
| DIV | 3 | 15 | 45 |
| ATAN | 1 | 80 | 80 |
| SIN | 2 | 40 | 80 |
| CMP/MAX | 2 | 1 | 2 |
| **Total** | **72** | — | **~339 cycles** |

**Breakdown:**

- **Hilbert Transform pipeline**: ~154 cycles (same as HT_PHASOR)
  - Price smoothing (WMA-4): ~20 cycles
  - 4× Hilbert FIR applications: ~64 cycles
  - Bandwidth adaptation + I2/Q2: ~25 cycles
  - EMA smoothing (×2): ~16 cycles
  - Period calculation: ~29 cycles
- **Phase calculation** (atan with quadrant logic): ~85 cycles
  - Division (Q2/I2): 15 cycles
  - ATAN: 80 cycles (includes quadrant handling)
- **DeltaPhase + Alpha**: 3 MUL + 2 ADD + 1 DIV + 1 MAX = ~25 cycles
- **Sine wave generation**: 2 SIN = ~80 cycles
  - sin(Phase): 40 cycles
  - sin(Phase + Alpha): 40 cycles

### Complexity Analysis

| Mode | Complexity | Notes |
| :--- | :---: | :--- |
| Streaming | O(1) | Fixed operations per bar |
| Batch | O(n) | Linear scan over price bars |

**Memory**: ~136 bytes (HT state + phase tracking + previous sine values)

### SIMD Analysis

| Optimization | Applicable | Notes |
| :--- | :---: | :--- |
| AVX2 vectorization | ❌ | Recursive IIR dependencies throughout |
| FMA | ✅ | EMA smoothing patterns |
| Batch parallelism | ❌ | State-dependent recursion |
| SVML sin | ✅ | Batch sin() calls can use SVML intrinsics |

**Optimization notes:**

- Sin calculations dominate output stage; SVML can accelerate batch processing
- Phase unwrapping requires sequential processing
- FMA applicable to EMA smoothing stages

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 8/10 | Precise cycle representation via HT |
| **Timeliness** | 7/10 | LeadSine provides early warning signals |
| **Smoothness** | 9/10 | Sine waves naturally smooth; bounded [-1, +1] |
| **Signal Clarity** | 7/10 | Clear crossover signals; can whipsaw in trends |

## Limitations and Considerations

* **Cycle Assumption:** Assumes market is in cyclical mode; less reliable during strong trends
* **Lag Component:** Despite "lead-sine," overall indicator lags actual price action due to Hilbert Transform smoothing
* **False Signals:** Can generate whipsaws in choppy, non-cyclical markets
* **Trend Weakness:** Strong directional moves violate cycle assumptions, producing unreliable waves
* **Period Dependency:** Relies on accurate dominant cycle detection; errors in period affect wave quality
* **Visual Tool:** Best used as confirmation with other indicators rather than standalone timing tool

## References

* Ehlers, J. F. (2004). "Cybernetic Analysis for Stocks and Futures." John Wiley & Sons.
* Ehlers, J. F. (2001). "Rocket Science for Traders: Digital Signal Processing Applications." John Wiley & Sons.
* Ehlers, J. F. (2013). "Cycle Analytics for Traders: Advanced Technical Trading Concepts." John Wiley & Sons.