# HT_DCPERIOD: Hilbert Transform Dominant Cycle Period

[Pine Script Implementation of HT_DCPERIOD](https://github.com/mihakralj/pinescript/blob/main/indicators/cycles/ht_dcperiod.pine)

## Overview and Purpose

The Hilbert Transform Dominant Cycle Period (HT_DCPERIOD) is an advanced signal processing indicator developed by John Ehlers that identifies the dominant cycle length in price data. Published in his book "Cycle Analytics for Traders" (2013), this indicator uses the Hilbert Transform mathematical technique to detect the current market cycle period in real-time, typically ranging from 6 to 50 bars.

Unlike traditional cycle detection methods that rely on fixed periods, HT_DCPERIOD adapts to changing market conditions by continuously measuring the actual cycle length present in the price data. This adaptive capability makes it invaluable for optimizing other technical indicators and determining appropriate lookback periods for trading systems.

## Core Concepts

* **Hilbert Transform**: A mathematical operation that shifts the phase of a signal by 90 degrees, enabling the separation of trending and cycling components in price data
* **InPhase and Quadrature Components**: Two phase-shifted versions of the price signal that, when combined, reveal the cycle period through their phase relationship
* **Detrending**: Removal of the trending component from price data to isolate the cyclical component for accurate period measurement
* **Adaptive Smoothing**: Dynamic adjustment of smoothing factors based on the detected cycle period to reduce noise while maintaining responsiveness
* **Median Filtering**: Use of a 5-bar moving median to smooth the period output and eliminate outliers caused by market noise

## Common Settings and Parameters

| Parameter | Default | Function | When to Adjust |
| ------ | ------ | ------ | ------ |
| Source | hlc3 | Price data to analyze | Use close for end-of-bar signals, hlc3 for intrabar smoothing |

**Pro Tip:** The indicator automatically adapts to any timeframe. On daily charts, a period of 20 indicates a 20-day cycle (about one month). On hourly charts, 20 indicates a 20-hour cycle. Consider the timeframe when interpreting the cycle length - what matters is the number of bars, not calendar time.

## Calculation and Mathematical Foundation

**Simplified explanation:**
HT_DCPERIOD uses digital signal processing to transform price data into two phase-shifted components (InPhase and Quadrature), then calculates the cycle period from the phase angle between them.

**Technical formula:**

1. **Smooth the price** to reduce high-frequency noise:
   ```
   SmoothPrice = (4×Price + 3×Price[1] + 2×Price[2] + Price[3]) / 10
   ```

2. **Detrend the smoothed price** using a Hilbert Transform finite impulse response filter:
   ```
   Detrender = (0.0962×SP + 0.5769×SP[2] - 0.5769×SP[4] - 0.0962×SP[6]) × (0.075×Period[1] + 0.54)
   ```

3. **Compute InPhase (I1) and Quadrature (Q1) components**:
   ```
   Q1 = (0.0962×DT + 0.5769×DT[2] - 0.5769×DT[4] - 0.0962×DT[6]) × (0.075×Period[1] + 0.54)
   I1 = Detrender[3]
   ```

4. **Advance the phase** of I1 and Q1 by 90 degrees (jI and jQ):
   ```
   jI = (0.0962×I1 + 0.5769×I1[2] - 0.5769×I1[4] - 0.0962×I1[6]) × (0.075×Period[1] + 0.54)
   jQ = (0.0962×Q1 + 0.5769×Q1[2] - 0.5769×Q1[4] - 0.0962×Q1[6]) × (0.075×Period[1] + 0.54)
   ```

5. **Create phasor components I2 and Q2**:
   ```
   I2 = I1 - jQ
   Q2 = Q1 + jI
   Smooth I2 and Q2 with: Value = 0.2×Value + 0.8×Value[1]
   ```

6. **Calculate Real and Imaginary components**:
   ```
   Re = I2×I2[1] + Q2×Q2[1]
   Im = I2×Q2[1] - Q2×I2[1]
   Smooth Re and Im with: Value = 0.2×Value + 0.8×Value[1]
   ```

7. **Compute cycle period from phase angle**:
   ```
   Period = 2π / arctan(Im / Re)
   Clamp: Period = max(6, min(50, Period))
   Smooth: Period = 0.2×Period + 0.8×Period[1]
   ```

8. **Apply exponential smoothing** to final period output:
   ```
   SmoothPeriod = 0.2×Period + 0.8×SmoothPeriod[1]
   ```

> 🔍 **Technical Note:** The adaptive smoothing factor (0.075×Period[1] + 0.54) in the Hilbert Transform filters adjusts the bandwidth based on the current cycle period, ensuring optimal frequency response across different market cycles. The exponential smoothing (alpha=0.2) balances responsiveness with stability while maintaining Ehlers' original algorithm design.

## Interpretation Details

HT_DCPERIOD provides real-time cycle analysis with multiple applications:

* **Cycle Length Identification:**
  * Values 6-15 bars: Short-term cycles, fast market movements
  * Values 15-30 bars: Medium-term cycles, typical trading ranges
  * Values 30-50 bars: Long-term cycles, slower trending movements
  * Stable values indicate consistent cycling behavior
  * Rapidly changing values suggest transitional or chaotic market conditions

* **Indicator Optimization:**
  * Use detected period as lookback length for other indicators
  * Example: If HT_DCPERIOD = 20, use 20-period RSI, 20-period moving averages
  * Automatically adapts indicators to current market rhythm
  * Improves timing and reduces false signals

* **Market State Assessment:**
  * Stable, consistent period readings: Market in well-defined cycle
  * Increasing period length: Market entering longer-term trend or consolidation
  * Decreasing period length: Market becoming more volatile or choppy
  * Erratic period changes: Transitional phase, trend/cycle mode shift

* **Trading System Adaptation:**
  * Short cycles (6-15): Use faster indicators, shorter stops, quicker exits
  * Medium cycles (15-30): Standard trading approaches work well
  * Long cycles (30-50): Use wider stops, longer holding periods, trend-following strategies

## Limitations and Considerations

* **Initialization Period**: Requires approximately 50-60 bars of data before producing stable readings due to the multiple stages of filtering and smoothing
* **Lag Component**: The extensive smoothing needed for stability introduces some lag, meaning detected periods reflect recent rather than current cycle length
* **Range Limitations**: Clamped to 6-50 bars, so cannot detect very short (< 6) or very long (> 50) cycles, which may be present in some markets
* **Trending Markets**: During strong trends with minimal cyclical component, the indicator may produce unstable or meaningless readings as it attempts to find cycles where none exist
* **Complementary Use**: Best used in conjunction with trend-following indicators (like HT_TRENDMODE) to determine when cycle analysis is appropriate vs when trend analysis is more suitable
* **Parameter Sensitivity**: The Ehlers algorithm uses specific mathematical constants that work well for most markets but may not be optimal for all instruments or timeframes

## References

* Ehlers, J. F. (2013). *Cycle Analytics for Traders: Advanced Technical Trading Concepts*. Wiley Trading.
* Ehlers, J. F. (2001). *Rocket Science for Traders: Digital Signal Processing Applications*. Wiley Trading.
* TA-Lib Technical Analysis Library - HT_DCPERIOD implementation
* Mesa Software - MESA Cycle (similar methodology)
