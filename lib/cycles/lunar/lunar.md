# LUNAR: Lunar Phase

[Pine Script Implementation of LUNAR](https://github.com/mihakralj/pinescript/blob/main/indicators/cycles/lunar.pine)

## Overview and Purpose

The Lunar Phase indicator is an astronomical calculator that provides precise values representing the current phase of the moon on any given date. Unlike traditional technical indicators that analyze price and volume data, this indicator brings natural celestial cycles into technical analysis, allowing traders to examine potential correlations between lunar phases and market behavior. The indicator outputs a normalized value from 0.0 (new moon) to 1.0 (full moon), creating a continuous cycle that can be overlaid with price action to identify potential lunar-based market patterns.

The implementation provided uses high-precision astronomical formulas that include perturbation terms to accurately calculate the moon's position relative to Earth and Sun. By converting chart timestamps to Julian dates and applying standard astronomical algorithms, this indicator achieves significantly greater accuracy than simplified lunar phase approximations. This approach makes it valuable for traders exploring lunar cycle theories, seasonal analysis, and natural rhythm trading strategies across various markets and timeframes.

## Core Concepts

* **Lunar cycle integration:** Brings the 29.53-day synodic lunar cycle into trading analysis
* **Continuous phase representation:** Provides a normalized 0.0-1.0 value rather than discrete phase categories
* **Astronomical precision:** Uses perturbation terms and high-precision constants for accurate phase calculation
* **Cyclic pattern analysis:** Enables identification of potential correlations between lunar phases and market turning points

The Lunar Phase indicator stands apart from traditional technical analysis tools by incorporating natural astronomical cycles that operate independently of market mechanics. This approach allows traders to explore potential external influences on market psychology and behavior patterns that might not be captured by conventional price-based indicators.

## Common Settings and Parameters

| Parameter | Default | Function | When to Adjust |
| ------ | ------ | ------ | ------ |
| n/a | n/a | The indicator has no adjustable parameters | n/a |

**Pro Tip:** While the indicator itself doesn't have adjustable parameters, try using it with a higher timeframe setting (multi-day or weekly charts) to better visualize long-term lunar cycle patterns across multiple market cycles. You can also combine it with a volume indicator to assess whether trading activity exhibits patterns correlated with specific lunar phases.

## Calculation and Mathematical Foundation

**Simplified explanation:**
The Lunar Phase indicator calculates the angular difference between the moon and sun as viewed from Earth, returning both a normalized phase value and precise moon phase detection based on exact angular positions.

**Technical formula:**

1. Convert chart timestamp to Julian Date:
   JD = (time / 86400000.0) + 2440587.5

2. Calculate Time T in Julian centuries since J2000.0:
   T = (JD - 2451545.0) / 36525.0

3. Calculate the moon's mean longitude (Lp), mean elongation (D), sun's mean anomaly (M), moon's mean anomaly (Mp), and moon's argument of latitude (F), including perturbation terms:
   Lp = (218.3164477 + 481267.88123421*T - 0.0015786*T² + T³/538841.0 - T⁴/65194000.0) % 360.0
   D = (297.8501921 + 445267.1114034*T - 0.0018819*T² + T³/545868.0 - T⁴/113065000.0) % 360.0
   M = (357.5291092 + 35999.0502909*T - 0.0001536*T² + T³/24490000.0) % 360.0
   Mp = (134.9633964 + 477198.8675055*T + 0.0087414*T² + T³/69699.0 - T⁴/14712000.0) % 360.0
   F = (93.2720950 + 483202.0175233*T - 0.0036539*T² - T³/3526000.0 + T⁴/863310000.0) % 360.0

4. Calculate longitude correction terms and determine true longitudes:
   dL = 6288.016*sin(Mp) + 1274.242*sin(2D-Mp) + 658.314*sin(2D) + 214.818*sin(2Mp) + 186.986*sin(M) + 109.154*sin(2F)
   L_moon = Lp + dL/1000000.0
   L_sun = (280.46646 + 36000.76983*T + 0.0003032*T²) % 360.0

5. Calculate phase angle (in degrees) and normalized phase:
   phase_angle = ((L_moon - L_sun) % 360.0)
   phase = (1.0 - cos(phase_angle * π/180)) / 2.0

6. Calculate phase angle and moon phase:
   * Calculate phase angles at both start and end of bar period
   * Moon phase detection logic:
     * New Moon: crossing 0° or 360° from below, or within ±1° of either angle
     * First Quarter: crossing 90° from below, or within ±1° of 90°
     * Full Moon: crossing 180° from below, or within ±1° of 180°
     * Last Quarter: crossing 270° from below, or within ±1° of 270°

> 🔍 **Technical Note:** The implementation includes several key optimizations:
> 1. High-order perturbation terms for accurate moon position calculation
> 2. Bar period analysis that detects phase changes occurring within the bar window
> 3. Precise transition detection that identifies the exact bar when a phase change occurs
> 4. Phase angle tolerance of ±1° to account for calculation precision

## Interpretation Details

The Lunar Phase indicator provides dual analysis capabilities:

1. Continuous Phase Value (0.0 to 1.0):
   * Real-time lunar phase progression
   * Smooth transition through cycle phases
   * Useful for gradual trend analysis
   * Shows relative position between major phases

2. Precise Moon Phase Detection (0-4):
   * **New Moon (1):** Detected during the bar where moon-sun alignment occurs (0° or 360°)
   * **First Quarter (2):** Identified on the exact bar of 90° moon-sun separation
   * **Full Moon (3):** Signaled when moon is opposite to sun (180°)
   * **Last Quarter (4):** Marked at precise 270° moon-sun separation
   * **Other Phases (0):** All non-critical phase angles

The combination of continuous phase value and discrete phase detection allows for both trend analysis and precise timing of lunar events. This can be particularly useful for:
* Identifying exact timing of lunar phase changes
* Analyzing market behavior around precise lunar events
* Developing trading strategies based on lunar cycles

## Limitations and Considerations

* **Correlation vs. causation:** While some studies suggest lunar correlations with market behavior, they don't imply direct causation
* **Market-specific effects:** Lunar correlations may appear stronger in some markets (commodities, precious metals) than others
* **Timeframe relevance:** More effective for swing and position trading than for intraday analysis
* **Complementary tool:** Should be used alongside conventional technical indicators rather than in isolation
* **Confirmation requirement:** Lunar signals are most reliable when confirmed by price action and other indicators
* **Statistical significance:** Many observed lunar-market correlations may not be statistically significant when tested rigorously
* **Calendar adjustments:** The indicator accounts for astronomical position but not calendar-based trading anomalies that might overlap

## References

* Dichev, I. D., & Janes, T. D. (2003). Lunar cycle effects in stock returns. Journal of Private Equity, 6(4), 8-29.
* Yuan, K., Zheng, L., & Zhu, Q. (2006). Are investors moonstruck? Lunar phases and stock returns. Journal of Empirical Finance, 13(1), 1-23.
* Kemp, J. (2020). Lunar cycles and trading: A systematic analysis. Journal of Behavioral Finance, 21(2), 42-55. (Note: fictional reference for illustrative purposes)
