# SSF-DSP: Super Smooth Filter Based Detrended Synthetic Price

[Pine Script Implementation of SSF-DSP](https://github.com/mihakralj/pinescript/blob/main/indicators/cycles/ssfdsp.pine)

## Overview and Purpose

The Super Smooth Filter Based Detrended Synthetic Price (SSF-DSP) is an enhanced variant of John Ehlers' Detrended Synthetic Price that replaces the traditional EMA filters with Super Smooth Filters. This advanced implementation provides superior noise reduction and cleaner passband characteristics while maintaining the core band-pass filtering concept. By using SSF's optimized pole placement with complex conjugates, SSF-DSP achieves exceptional cycle isolation with minimal waveform distortion, making it particularly valuable for identifying dominant market cycles in moderately noisy conditions.

The indicator calculates the difference between a quarter-cycle SSF and a half-cycle SSF, creating a band-pass filter that isolates the dominant cycle component while removing both high-frequency noise and low-frequency trend. This mathematical relationship effectively detrends the price data, revealing the underlying cyclic structure that drives market oscillations.

## Core Concepts

* **Dual SSF Structure:** Uses two independent Super Smooth Filters at quarter-cycle and half-cycle periods derived from the dominant cycle
* **Band-Pass Filtering:** The difference between fast and slow SSFs creates a filter that passes the dominant cycle frequency while rejecting noise and trend
* **Enhanced Smoothing:** SSF's Butterworth-style response provides cleaner filtering than EMA-based DSP with better roll-off characteristics
* **Cycle Isolation:** Reveals the pure cyclic component of price movement by removing both short-term noise and long-term trend
* **Reduced Lag:** Despite heavier smoothing, SSF maintains reasonable lag characteristics due to optimized coefficient design

## Common Settings and Parameters

| Parameter | Default | Function | When to Adjust |
| ------ | ------ | ------ | ------ |
| Source | hlc3 | Price data used for calculation | hlc3 provides balanced price representation; close for directional bias |
| Period | 40 | Dominant cycle period in bars | Match to your identified dominant cycle (typically 20-50 bars for daily charts) |

**Pro Tip:** SSF-DSP provides cleaner signals than EMA-based DSP with ~1.5-2x more smoothing. If you use period=40 for regular DSP, try period=30-35 for SSF-DSP to achieve similar responsiveness with better noise rejection.

## Calculation and Mathematical Foundation

**Simplified explanation:**
SSF-DSP applies two Super Smooth Filters to the price data—one tuned to quarter of the dominant cycle period and one to half the period. The difference between these two filtered signals creates a band-pass effect that isolates the dominant cycle frequency while removing both high-frequency noise and low-frequency trend components.

**Technical formula:**

1. Calculate quarter-cycle and half-cycle periods:
   ```
   Fast Period = Period / 4
   Slow Period = Period / 2
   ```

2. Calculate SSF coefficients for each filter:
   ```
   arg = √2π / Period
   exp_arg = exp(-arg)
   c2 = 2 × exp_arg × cos(arg)
   c3 = -exp_arg²
   c1 = 1 - c2 - c3
   ```

3. Apply SSF recursion for both filters:
   ```
   SSF_fast = c1_fast × Price + c2_fast × SSF_fast[1] + c3_fast × SSF_fast[2]
   SSF_slow = c1_slow × Price + c2_slow × SSF_slow[1] + c3_slow × SSF_slow[2]
   ```

4. Calculate the difference:
   ```
   SSF-DSP = SSF_fast - SSF_slow
   ```

> 🔍 **Technical Note:** The √2 factor in SSF coefficient calculations creates a maximally flat Butterworth magnitude response, providing optimal smoothness in the passband. This results in cleaner cycle isolation compared to EMA-based DSP, which uses simple exponential weighting.

## Interpretation Details

SSF-DSP provides enhanced cycle analysis capabilities:

* **Zero-Line Crossovers:**
  * Crossing above zero: Indicates cycle is in upward phase with improving momentum
  * Crossing below zero: Indicates cycle is in downward phase with weakening momentum
  * More reliable than EMA-DSP due to superior noise rejection

* **Peak and Trough Identification:**
  * Peaks indicate cycle tops with cleaner signals than EMA-DSP
  * Troughs indicate cycle bottoms with reduced false positives
  * Peak-to-peak distance estimates the current cycle period

* **Amplitude Analysis:**
  * Larger swings indicate stronger cyclic component in the market
  * Decreasing amplitude suggests cycle is weakening or market entering consolidation
  * Cleaner amplitude measurement than EMA-DSP

* **Divergence Detection:**
  * Price making new highs while SSF-DSP makes lower highs: bearish divergence
  * Price making new lows while SSF-DSP makes higher lows: bullish divergence
  * More reliable divergence signals due to superior noise filtering

* **Cycle Phase Tracking:**
  * Monitor position relative to zero to determine cycle phase
  * Use in conjunction with HT_DCPERIOD for adaptive period selection
  * Cleaner phase identification than EMA-based variant

## Limitations and Considerations

* **Increased Lag:** SSF introduces ~1.5-2x more lag than EMA while providing superior smoothing—may delay signals in fast-moving markets
* **Period Dependency:** Requires accurate dominant cycle period estimate for optimal performance
* **Initialization Period:** Needs more bars than EMA-DSP to stabilize (approximately 2× the period setting)
* **Computational Complexity:** Slightly more intensive than EMA-DSP due to trigonometric coefficient calculations (though still O(1) per bar)
* **Oversmoothing Risk:** In very choppy markets, excessive smoothing may reduce signal responsiveness
* **Best Suited For:** Moderately noisy markets where clean cycle isolation is priority over minimal lag

## Comparison to EMA-Based DSP

| Characteristic | SSF-DSP | EMA-DSP |
| ------ | ------ | ------ |
| Noise Rejection | Excellent | Good |
| Lag | Moderate | Low |
| Passband Ripple | Minimal | Moderate |
| Roll-off | Sharp | Gradual |
| Best For | Clean cycle isolation | Responsive trading |
| Computational | O(1) with trig | O(1) simple |

## References

* Ehlers, J.F. "Cycle Analytics for Traders," Wiley, 2013
* Ehlers, J.F. "Rocket Science for Traders," Wiley, 2001
* Ehlers, J.F. "Cybernetic Analysis for Stocks and Futures," Wiley, 2004
