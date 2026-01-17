# SINE: Ehlers Sine Wave Indicator

[Pine Script Implementation of SINE](https://github.com/mihakralj/pinescript/blob/main/indicators/cycles/sine.pine)

## Overview and Purpose

The Sine Wave indicator, a foundational concept in John Ehlers' work on cycle analysis, plots a theoretical sinewave based on an assumed dominant cycle period in the market. As Ehlers describes in "Stay in Phase," a cycle can be visualized as a 360-degree rotation, and its **phase** describes the current position within that rotation. The Sine Wave indicator translates this phase into a sinusoidal wave, helping traders visualize cyclical patterns. It typically includes two components: the primary sinewave representing the current phase, and a "lead" sinewave, phase-shifted forward to potentially anticipate cycle turns.

Ehlers emphasizes that while market cycles can be ephemeral, their phase is a measurable parameter that can offer insights into market modes, particularly for identifying trend conditions. This basic version of the Sine Wave indicator relies on the user to specify the dominant cycle period, rather than measuring it directly from price data.

## Core Concepts

* **Assumed Dominant Cycle:** The indicator operates on the premise that a dominant cycle of a specific, user-defined period exists.
* **Phase as a Key Parameter:** Following Ehlers' view, the phase of the cycle is a critical element. A cycle is considered a 360-degree movement, and the phase indicates the location within this cycle.
* **Phase Accumulation:** The indicator tracks the phase of this assumed cycle, incrementing it with each bar. The phase is typically reset or wrapped around after completing 360 degrees to start the next cycle.
* **Sinusoidal Representation:** The current phase is converted into a sinewave value, oscillating between +1 and -1, much like a pen on a rotating shaft (phasor diagram) would draw a wave on paper moving at a uniform rate.
* **Lead Wave:** A second sinewave is generated with a forward phase shift (e.g., 45 degrees), providing a leading indication relative to the primary sinewave. This can help in anticipating changes in the cycle's direction.

## Common Settings and Parameters

| Parameter | Default | Function | When to Adjust |
| --------- | ------- | -------- | -------------- |
| Dominant Cycle Period | 20 | The assumed length of the dominant market cycle in bars. This directly determines the frequency of the sinewave. | This is the most critical parameter. Adjust to match the visually identified dominant cycle length in the market or based on other cycle analysis. |
| Delta | 0.5 | Phase shift multiplier for the lead sinewave (0.5 corresponds to a 45-degree lead as 0.5 * 90 degrees). | Increase for a greater lead, decrease for less. A common value is 0.5. |

**Pro Tip:** The effectiveness of the Sine Wave indicator heavily relies on the accuracy of the `Dominant Cycle Period` input. If the market's actual dominant cycle changes, this parameter needs to be readjusted.

## Calculation and Mathematical Foundation

**Simplified explanation:**
1. Assume a fixed cycle period (e.g., 20 bars).
2. Calculate how much the phase of the cycle should advance with each new bar (e.g., 360 degrees / 20 bars = 18 degrees per bar).
3. Keep track of the cumulative phase, wrapping it around after it completes a full 360-degree cycle.
4. Generate a sinewave value based on the current cumulative phase.
5. Generate a second "lead" sinewave by adding a fixed phase advance (e.g., 45 degrees) to the current phase before calculating its sine value.

**Technical formula:**
1. **Phase Increment per bar:**
    `PhaseIncrement = 360 / DominantCyclePeriod`
2. **Cumulative Phase (dcPhase):**
    `dcPhase_current = (dcPhase_previous + PhaseIncrement) % 360` (modulo 360 ensures wrapping)
3. **Sinewaves:**
    `SineWave = sin(dcPhase_current * PI/180)`
    `LeadSineWave = sin(((dcPhase_current + delta * 90) % 360) * PI/180)` (phase lead also wrapped)

> 🔍 **Technical Note:** This indicator generates a mathematically perfect sinewave based on the input period. It does not adapt to changes in market cycle length unless the `Dominant Cycle Period` parameter is manually changed. The `delta` parameter directly controls the phase lead of the second sinewave. The modulo operation ensures the phase correctly wraps around 360 degrees.

## Interpretation Details

* **Cycle Visualization:** The primary sinewave shows the theoretical position within the assumed market cycle. Peaks indicate potential cycle tops, and troughs indicate potential cycle bottoms.
* **Timing Signals (Lead Wave Crossovers):**
    * When the Lead Sine Wave crosses above the Sine Wave, it can be interpreted as an early signal of an upcoming upward phase in the cycle (potential buy signal).
    * When the Lead Sine Wave crosses below the Sine Wave, it can be interpreted as an early signal of an upcoming downward phase in the cycle (potential sell signal).
* **Zero Line Crossovers:**
    * Sine Wave crossing up through zero: Indicates the theoretical start of an up-cycle.
    * Sine Wave crossing down through zero: Indicates the theoretical start of a down-cycle.
* **Signal Levels (e.g., +/- 0.707):** The levels corresponding to +/- 45 degrees (approximately +/- 0.707) are often watched. The lead wave crossing these levels before the main sinewave can also be used for anticipation.

## Limitations and Considerations

* **Fixed Period:** The primary limitation is its reliance on a fixed, user-defined cycle period. Real market cycles are dynamic and change over time. If the assumed period is incorrect, the indicator will provide misleading information.
* **No Adaptation:** Unlike more advanced Ehlers indicators (like those using Hilbert Transforms or other DSP techniques), this basic Sine Wave does not measure or adapt to the actual dominant cycle in the price data.
* **Lag:** While the lead wave attempts to reduce lag, the fundamental calculation is still based on past data and an assumed cycle.
* **Market Conditions:** Most effective in markets that exhibit relatively regular cyclical behavior. In strongly trending or very choppy markets, its utility diminishes.
* **Subjectivity:** Choosing the correct `Dominant Cycle Period` is subjective and requires careful observation or other analytical methods.

## C# Implementation Considerations

The QuanTAlib implementation of SINE uses an efficient circular buffer approach with the following optimizations:

* **Circular Buffer:** Uses `CircularBuffer` to maintain the phase history with O(1) operations for adding new values and accessing historical data.
* **Incremental Phase Calculation:** The phase is calculated incrementally by adding a fixed phase increment per bar, avoiding recalculation of the entire history.
* **Modulo Wrapping:** Phase values are wrapped using modulo 360 to ensure they stay within the 0-360 degree range.
* **Warmup Handling:** The indicator properly handles the warmup period, requiring at least one data point before producing valid output.
* **Memory Efficiency:** Only stores the minimum required historical data (period + 1 values) rather than the entire price history.

## References

* Ehlers, J. F. (2001). *Rocket Science for Traders: Digital Signal Processing Applications*. John Wiley & Sons.
* Ehlers, J. F. "Stay in Phase." *Technical Analysis of Stocks & Commodities* magazine. (This article provides conceptual background on phase.)