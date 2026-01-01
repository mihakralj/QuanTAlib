# APCHANNEL: Andrews' Pitchfork

## Overview and Purpose

Andrews' Pitchfork is a technical analysis tool used to identify potential support and resistance levels and forecast future price movements. It consists of three parallel trendlines drawn from three user-selected pivot points on a price chart. The central trendline (median line) is drawn from the first pivot, bisecting the line connecting the second and third pivots. The other two lines are drawn parallel to this median line, passing through the second and third pivot points, forming a channel. This tool, developed by Dr. Alan Andrews, helps traders visualize potential price paths and areas where price might react.

## Core Concepts

* **Median Line Theory:** Suggests that price will gravitate towards the median line and that this line can act as a significant support or resistance level.
* **Channel Projection:** The upper and lower parallel lines create a channel that can define the boundaries of a trend.
* **Pivot Point Selection:** The effectiveness of the pitchfork heavily relies on the correct identification of significant swing highs and lows (pivots P1, P2, P3).
* **Dynamic Support/Resistance:** The lines of the pitchfork provide dynamic levels that adjust with the slope of the trend.

## Common Settings and Parameters

| Parameter | Default | Function                                                                 | When to Adjust                                                                                                    |
| :-------- | :------ | :----------------------------------------------------------------------- | :------------------------------------------------------------------------------------------------------------     |
| P1_back   | 45      | Bars back from the current bar to locate the first pivot point (P1).     | Adjust to select a significant starting pivot for the pitchfork (e.g., a major swing high or low).                |
| P2_back   | 30      | Bars back from the current bar to locate the second pivot point (P2).    | Adjust to select a subsequent reaction high/low after P1.                                                         |
| P3_back   | 15      | Bars back from the current bar to locate the third pivot point (P3).     | Adjust to select another reaction low/high after P2, forming the initial channel width with P2.                   |
| Source    | close   | Price source for P1 (P2 uses high, P3 uses low by default in this impl.) | Typically `close` for P1, `high` for P2, and `low` for P3 are standard, but can be adapted for specific analysis. |

**Note:** The implementation requires `P1_back > P2_back > P3_back` to ensure points are selected in chronological order from oldest (P1) to newest (P3).

## Calculation and Mathematical Foundation

**Simplified explanation:**
The pitchfork is constructed by:

1. Identifying three pivot points (P1, P2, P3) on the chart.
2. Drawing a median line from P1 through the midpoint of the line segment connecting P2 and P3.
3. Drawing two additional lines parallel to the median line, one passing through P2 (upper line) and the other through P3 (lower line).

**Technical formula:**
Let P1 = (t₁, p₁), P2 = (t₂, p₂), P3 = (t₃, p₃) where 't' is time (bar index) and 'p' is price.

1. **Midpoint M between P2 and P3:**
    * M_time = (t₂ + t₃) / 2
    * M_price = (p₂ + p₃) / 2

2. **Slope of the Median Line (from P1 to M):**
    * Slope = (M_price - p₁) / (M_time - t₁)
    * If M_time - t₁ is zero, slope is handled as vertical or near-vertical.

3. **Median Line Equation (for current bar 't_current'):**
    * MedianLine_price = p₁ + Slope × (t_current - t₁)

4. **Upper Parallel Line (through P2):**
    * UpperLine_price = p₂ + Slope × (t_current - t₂)

5. **Lower Parallel Line (through P3):**
    * LowerLine_price = p₃ + Slope × (t_current - t₃)

> 🔍 **Technical Note:** The provided Pine Script implementation selects P1 based on `close` price, P2 based on `high` price, and P3 based on `low` price at their respective `*_back` bar indices. It includes validation for chronological order of points and handles potential numerical issues like division by zero or extreme values.

## Interpretation Details

* **Trend Direction:** The general slope of the pitchfork indicates the prevailing trend direction.
* **Support and Resistance:**
  * The median line often acts as a central support/resistance axis. Price may find support on it during uptrends or resistance during downtrends.
  * The upper and lower parallel lines can act as boundaries for price movement, providing potential areas for entries or exits.
* **Price Behavior:**
  * Price tends to gravitate towards the median line.
  * If price fails to reach the median line after touching an outer line, it might signal a weakening trend.
  * A decisive break outside the pitchfork channel can indicate a potential trend reversal or acceleration.
* **Trade Signals:**
  * Bounces off the lower line in an uptrend can be buying opportunities.
  * Rejections from the upper line in a downtrend can be selling opportunities.
  * Breakouts from the channel can signal new trends.

## Limitations and Considerations

* **Subjectivity in Pivot Selection:** The effectiveness of Andrews' Pitchfork is highly dependent on the choice of the three pivot points (P1, P2, P3). Different traders may choose different pivots, leading to different pitchforks and interpretations.
* **Lagging Nature:** Like many trend-following tools, it is based on past price action and may lag in identifying new trends or reversals.
* **Not Always Respected:** Price will not always adhere to the pitchfork lines perfectly. False breakouts or failures to reach expected lines are common.
* **Repainting (if pivots are not fixed):** If the pivot points are chosen based on rules that can change as new bars form (e.g., "highest high in X bars"), the pitchfork can repaint. The current implementation uses fixed lookback periods from the current bar, which means the historical part of the pitchfork will change as new bars appear. For stable historical analysis, pivots should be anchored to specific dates/times.
* **Market Conditions:** Works best in trending markets. In choppy or sideways markets, it may provide less reliable signals.

## References

* Andrews, A. W. (Date unknown). *The Andrews Method*. (Original course materials)
* Babson, R. W. (1935). *Finding a new way to make money*. (Precursor concepts)
* Skinner, T. (2008). *Trading the Pitchforks*. Harriman House.
