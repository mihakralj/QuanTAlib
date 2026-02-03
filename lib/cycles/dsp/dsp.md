# DSP: Detrended Synthetic Price

## Overview and Purpose

The Detrended Synthetic Price (DSP) is a cycle analysis indicator developed by John Ehlers that isolates the cyclical component of price action by subtracting a slower-period EMA from a faster-period EMA. Introduced in his work on digital signal processing for traders, DSP creates a band-pass filter effect that removes both long-term trends and short-term noise, revealing the dominant market cycle.

Unlike traditional detrending methods that use high-pass filters, Ehlers' DSP uses the difference between a quarter-cycle EMA and a half-cycle EMA relative to the dominant cycle period. This creates an in-phase output that oscillates around zero, with the amplitude and frequency revealing information about cycle strength and timing. The quarter-cycle smoother responds quickly to price changes while the half-cycle smoother provides the baseline reference, and their difference creates the band-pass effect.

DSP serves as both a standalone cycle indicator and a foundational component for more advanced Ehlers indicators. By isolating the dominant cycle component, it provides a clearer view of market rhythms without the contamination of longer-term trends or higher-frequency noise.

## Core Concepts

* **Dual-EMA Structure:** Uses two independent EMAs at quarter-cycle (P/4) and half-cycle (P/2) periods derived from the dominant cycle
* **Band-Pass Effect:** Quarter-cycle minus half-cycle creates a filter that passes the dominant cycle while attenuating trends and noise
* **In-Phase Output:** The resulting oscillator is in-phase with the dominant cycle, providing clear timing signals
* **Zero-Crossing Analysis:** Oscillations around zero line reveal cycle phase and potential reversal points
* **Cycle Isolation:** Mathematically isolates the periodic component that matches the specified dominant cycle period

## Common Settings and Parameters

| Parameter | Default | Function | When to Adjust |
| ------ | ------ | ------ | ------ |
| Source | source | Data source for calculation | Use `close` for end-of-bar analysis, `hlc3` for balanced price representation |
| Dominant Cycle Period | 40 | Period used to calculate quarter-cycle and half-cycle EMAs | Should match actual market cycle: 20-30 for faster cycles, 40-50 for standard, 60-80 for slower cycles |

**Pro Tip:** The Dominant Cycle Period should ideally be obtained from HT_DCPERIOD or other cycle measurement tools for adaptive behavior. For fixed analysis, 40 bars works well for daily charts (approximates a 2-month cycle). The quarter-cycle EMA (P/4 = 10) responds to short-term moves while the half-cycle EMA (P/2 = 20) provides the baseline, creating the band-pass effect.

## Calculation and Mathematical Foundation

**Simplified explanation:**
DSP calculates two EMAs at periods that are fractions of the dominant cycle (quarter and half), then subtracts the slower from the faster to create an oscillator that isolates the cyclical component.

**Technical formula:**

1. Calculate quarter-cycle and half-cycle periods from dominant cycle:
   ```
   Fast_Period = round(Period / 4)
   Slow_Period = round(Period / 2)
   ```

2. Calculate alpha values for both EMAs:
   ```
   Alpha_Fast = 2 / (Fast_Period + 1)
   Alpha_Slow = 2 / (Slow_Period + 1)
   ```

3. Apply exponential smoothing with warmup compensation:
   ```
   EMA_Fast = EMA(Price, Fast_Period)
   EMA_Slow = EMA(Price, Slow_Period)
   ```

4. Calculate DSP as the difference:
   ```
   DSP = EMA_Fast - EMA_Slow
   ```

> 🔍 **Technical Note:** The implementation uses unified warmup compensation to ensure both EMAs produce valid outputs from bar 1. The quarter-cycle EMA provides rapid response to price changes while the half-cycle EMA establishes the reference baseline. Their difference creates a band-pass filter centered on the dominant cycle period, effectively removing both low-frequency trends (longer than the cycle) and high-frequency noise (shorter than the cycle).

## Interpretation Details

DSP provides cycle-focused market analysis through the isolated cyclical component:

* **Zero-Line Crossovers:**
  * Cross above zero: Cycle entering positive phase, potential bullish swing point
  * Cross below zero: Cycle entering negative phase, potential bearish swing point
  * Frequency of crossings indicates cycle period accuracy

* **Amplitude Analysis:**
  * Larger oscillations: Stronger cycle component, more pronounced market rhythm
  * Smaller oscillations: Weaker cycle, market transitioning or range-bound
  * Amplitude expansion signals increasing cycle strength
  * Amplitude contraction signals decreasing cycle strength

* **Cycle Phase Identification:**
  * Peak values: Cycle approaching maximum (consider taking profits on longs)
  * Trough values: Cycle approaching minimum (consider taking profits on shorts)
  * Rate of change indicates cycle acceleration/deceleration
  * Zero crossings mark quarter-cycle phase transitions

* **Trend vs Cycle:**
  * Regular oscillations with consistent amplitude: Strong cyclic behavior
  * Irregular oscillations or bias to one side: Trend component present
  * Dampening oscillations: Cycle weakening, possible trend emergence
  * Amplifying oscillations: Cycle strengthening, rhythmic behavior dominant

## Limitations and Considerations

* **Period Dependency:** Effectiveness depends on correct Dominant Cycle Period setting relative to actual market cycles
* **Cycle Variability:** Market cycles are not perfectly periodic; DSP reveals approximate rhythms that can shift over time
* **Trend Sensitivity:** During strong trends, the oscillator may show persistent bias rather than symmetric oscillations
* **Lag Component:** EMAs introduce some lag, though the dual-EMA structure minimizes this compared to single moving averages
* **Requires Cycle Knowledge:** Best results when dominant cycle period is known (use HT_DCPERIOD for adaptive approach)
* **Not Predictive Alone:** Shows current cycle state; combine with other tools for timing and confirmation

## Performance Profile

### Operation Count (Streaming Mode, per Bar)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ADD/SUB | 3 | 1 | 3 |
| MUL | 4 | 3 | 12 |
| **Total** | **7** | — | **~15 cycles** |

**Breakdown:**
- Fast EMA (quarter-cycle): 2 MUL + 1 ADD = 7 cycles
- Slow EMA (half-cycle): 2 MUL + 1 ADD = 7 cycles
- DSP difference: 1 SUB = 1 cycle

### Complexity Analysis

| Mode | Complexity | Notes |
| :--- | :---: | :--- |
| Streaming | O(1) | Two IIR filters, constant time |
| Batch | O(n) | Linear scan, no lookback iteration |

**Memory**: ~24 bytes (2 EMA states × 8 bytes + output)

### SIMD Analysis

| Optimization | Applicable | Notes |
| :--- | :---: | :--- |
| AVX2 vectorization | ❌ | IIR recursion prevents cross-bar parallelism |
| FMA | ✅ | EMA: `α × price + (1-α) × prev` |
| Batch parallelism | ❌ | Sequential dependency on previous EMA state |

**FMA Optimization:** Each EMA can use single FMA instruction: `fma(α, price, (1-α) × prev)`, reducing 2 MUL + 1 ADD to 1 FMA + 1 MUL (~11 cycles total).

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | Band-pass effect isolates dominant cycle |
| **Timeliness** | 8/10 | Dual EMA minimizes lag vs single MA |
| **Overshoot** | 7/10 | EMA smoothing reduces overshoot |
| **Smoothness** | 8/10 | Clean oscillations when cycle present |

## References

* Ehlers, J. F. (2013). *Cycle Analytics for Traders: Advanced Technical Trading Concepts*. Wiley Trading.
* Ehlers, J. F. (2001). *Rocket Science for Traders: Digital Signal Processing Applications*. Wiley Trading.
* Ehlers, J. F. (2004). *Cybernetic Analysis for Stocks and Futures: Cutting-Edge DSP Technology to Improve Your Trading*. Wiley Trading.