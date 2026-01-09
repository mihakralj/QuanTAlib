# Moving Average Envelope

Moving Average Envelope consists of three lines: a moving average in the middle and two lines plotted at a fixed percentage above and below it. The envelope provides a simple way to identify potential support and resistance levels based on a percentage deviation from the average price.

## Calculation

```
Middle = MA(Source, Length)
Upper = Middle + (Middle × Percentage/100)
Lower = Middle - (Middle × Percentage/100)
```

Where:
* MA = Moving Average (can be SMA, EMA, or WMA)
* Source = Price series (typically close price)
* Length = Lookback period for moving average
* Percentage = Fixed percentage for band width

## Parameters

* Source (default: close) - Price series used for the moving average
* Length (default: 20) - Period used for moving average calculation
* Percentage (default: 1.0) - Fixed percentage distance from MA to bands
* MA Type (default: 1) - Moving average type: 0:SMA, 1:EMA, or 2:WMA

## Interpretation

* The middle line shows the average price trend
* Upper and lower bands create a channel based on fixed percentage
* Price reaching the bands may indicate overbought/oversold conditions
* Unlike volatility-based bands, envelope width changes proportionally with price
* Band penetration may signal potential trend reversals
* Works best in trending markets with consistent volatility

## Implementation

The implementation includes:
* Choice of three moving average types (SMA, EMA, WMA)
* Optimized calculations for each MA type
* Circular buffer for efficient SMA calculation
* Alpha smoothing for EMA
* Linear weighting for WMA
* Proper handling of NA values
* Input validation
* Percentage-based band width calculation
