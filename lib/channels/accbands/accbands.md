# ACCBANDS: Acceleration Bands

## Overview and Purpose

Acceleration Bands are a volatility-based indicator developed by Price Headley that creates an adaptive price envelope around a moving average. Unlike static percentage-based bands, Acceleration Bands dynamically adjust their width based on the spread between the high and low moving averages, making them responsive to changing market conditions. This approach allows the bands to expand during volatile periods and contract during consolidation, providing traders with a visual representation of potential support and resistance levels that adapt to market volatility.

The implementation provided uses efficient circular buffers for SMA calculations, ensuring optimal performance while properly handling data gaps. By creating a channel that widens during increased volatility and narrows during reduced volatility, Acceleration Bands offer traders a framework for identifying potential reversal points and measuring trend strength based on a security's natural price rhythm rather than arbitrary fixed percentages.

## Core Concepts

* **Volatility-adaptive channels:** Bands automatically widen during volatile markets and narrow during calm periods
* **Moving average foundation:** Uses simple moving averages of high, low, and close prices as the basis for calculations
* **Dynamic bandwidth:** Band width determined by the difference between high and low SMAs, adjusted by a multiplier
* **Symmetrical envelope:** Equal expansion above and below the centerline for balanced support/resistance identification

Acceleration Bands stand apart from other channel indicators by directly incorporating the natural range of price movement (high-low differential) into their width calculation. This creates a more market-adaptive envelope that responds to the inherent volatility characteristics of each security, rather than applying a uniform volatility measure across different instruments.

## Common Settings and Parameters

| Parameter | Default | Function | When to Adjust |
| --------- | ------- | -------- | -------------- |
| Period | 20 | Lookback period for all SMA calculations | Shorter for more sensitivity to recent price action; longer for smoother, less reactive bands |
| Factor | 2.0 | Multiplier for band width | Higher values for wider bands that trigger fewer signals; lower values for tighter bands with more frequent signals |
| Sources | High, Low, Close | Price data components | Rarely needs adjustment unless analyzing specific price aspects |

**Pro Tip:** Try using a band factor of 1.0 for shorter-term trading and 2.0-3.0 for longer-term analysis. The sweet spot often lies where the bands contain approximately 85-90% of price action, with only significant moves breaking beyond the bands.

## Calculation and Mathematical Foundation

**Simplified explanation:**
Acceleration Bands calculate a middle line as the SMA of closing prices, then create upper and lower bands by adding or subtracting the high-low differential (multiplied by a factor) to or from this middle line.

**Technical formula:**

Middle Band = SMA(Close, Period)
Upper Band = SMA(High, Period) + [SMA(High, Period) - SMA(Low, Period)] × Factor
Lower Band = SMA(Low, Period) - [SMA(High, Period) - SMA(Low, Period)] × Factor

Where:

* SMA = Simple Moving Average
* Period = Lookback period for calculations
* Factor = Multiplier for the band width

> 🔍 **Technical Note:** The implementation uses circular buffers to efficiently maintain running sums for all three SMAs (high, low, close), ensuring O(1) computational complexity regardless of the lookback period. This approach prevents recalculating entire sums each bar while properly handling NA values that may appear in the source data.

## Interpretation Details

Acceleration Bands provide several analytical perspectives:

* **Overbought/oversold conditions:** Price reaching or exceeding the upper band suggests potentially overbought conditions; touching or breaking below the lower band indicates potentially oversold conditions
* **Trend strength assessment:** Price persistently touching or moving beyond the bands in the direction of the trend indicates strong momentum
* **Volatility measurement:** The distance between bands provides a visual representation of current market volatility
* **Support and resistance levels:** During uptrends, the middle and lower bands often act as support; during downtrends, the middle and upper bands frequently serve as resistance
* **Mean reversion signals:** Moves beyond the bands followed by reversals back inside often signal potential mean reversion opportunities
* **Convergence/divergence patterns:** Narrowing bands indicate decreasing volatility, often preceding significant price moves; widening bands suggest increasing volatility

## Limitations and Considerations

* **Lagging component:** As a moving average-based indicator, Acceleration Bands exhibit some lag, potentially missing the initial stages of significant moves
* **Parameter sensitivity:** Results can vary significantly based on period and factor settings
* **False signals:** During strong trends, the bands may generate false reversal signals
* **Ineffectiveness in trendless markets:** May produce excessive signals in consolidating or choppy markets
* **Extreme volatility handling:** During periods of extremely high volatility, the bands may widen excessively, reducing their usefulness for near-term reversal identification
* **Complementary tool:** Works best when combined with other technical indicators for confirmation
* **Timeframe dependence:** Optimal parameters vary across different timeframes

## Performance Profile

### Operation Count (Streaming Mode, per Bar)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ADD/SUB | 8 | 1 | 8 |
| MUL | 2 | 3 | 6 |
| DIV | 3 | 15 | 45 |
| **Total** | **13** | — | **~59 cycles** |

**Breakdown:**
- SMA(High): 2 ADD + 1 DIV = 17 cycles (running sum)
- SMA(Low): 2 ADD + 1 DIV = 17 cycles (running sum)
- SMA(Close): 2 ADD + 1 DIV = 17 cycles (running sum)
- Band width: 1 SUB = 1 cycle
- Upper band: 1 MUL + 1 ADD = 4 cycles
- Lower band: 1 MUL + 1 SUB = 4 cycles

### Complexity Analysis

| Mode | Complexity | Notes |
| :--- | :---: | :--- |
| Streaming | O(1) | Three running sums with circular buffers |
| Batch | O(n) | Linear scan, n = series length |

**Memory**: ~192 bytes (three circular buffers for high, low, close SMAs).

### SIMD Analysis

| Optimization | Applicable | Notes |
| :--- | :---: | :--- |
| AVX2 vectorization | Partial | Band calc vectorizable; SMA recursion blocks full SIMD |
| FMA | ✅ | `SMA(High) ± Factor × (SMA(High) - SMA(Low))` |
| Batch parallelism | Partial | Three independent SMAs can be computed in parallel |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact computation |
| **Timeliness** | 5/10 | SMA lag (period/2 bars typical) |
| **Overshoot** | 4/10 | Adapts to high-low spread, not pure volatility |
| **Smoothness** | 7/10 | SMA-based, inherently smooth |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | Not implemented |
| **Skender** | ✅ | Validated against Skender.Stock.Indicators |
| **Tulip** | N/A | Not implemented |
| **Ooples** | N/A | Not implemented |
| **Internal** | ✅ | Mode consistency verified |

## References

* Headley, P. (2002). Big Trends in Trading: Strategies for Maximum Market Returns. John Wiley & Sons.
* Kaufman, P. J. (2013). Trading Systems and Methods (5th ed.). John Wiley & Sons.
* Murphy, J. J. (1999). Technical Analysis of the Financial Markets. New York Institute of Finance.
* Pring, M. J. (2002). Technical Analysis Explained. McGraw-Hill.