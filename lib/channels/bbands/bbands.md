# BBANDS: Bollinger Bands

## Overview and Purpose

Bollinger Bands are a technical analysis tool developed by John Bollinger in the 1980s. They consist of a middle band (typically a simple moving average) with an upper and lower band set at standard deviation levels above and below the middle band. Bollinger Bands adapt to market volatility by widening during volatile periods and contracting during less volatile periods, creating a dynamic range within which prices typically oscillate. This adaptive nature makes them useful for identifying potential overbought and oversold conditions relative to recent price action.

## Core Concepts

* **Volatility measurement:** Bollinger Bands expand and contract based on market volatility, providing a visual representation of dynamic market conditions
* **Market application:** Particularly useful for identifying potential price reversals, breakouts, and "squeeze" conditions that often precede significant price movements
* **Timeframe suitability:** **Multiple timeframes** are effective, with shorter periods (10-20) for short-term trading and longer periods (20-50) for position trading

Bollinger Bands combine two powerful technical concepts—moving averages and volatility—creating a comprehensive tool that helps traders identify not just trend direction but also potential extremes relative to recent price behavior.

## Common Settings and Parameters

| Parameter | Default | Function | When to Adjust |
| --------- | ------- | -------- | -------------- |
| Period | 20 | Controls the lookback window for both the middle band (SMA) and standard deviation calculation | Decrease for faster response in active markets, increase for smoother signals in choppy conditions |
| Source | Close | Data point used for calculation | Change to HL2 or HLC3 for more balanced readings in volatile markets |
| Multiplier | 2.0 | Determines the distance of the upper and lower bands from the middle band | Increase to 2.5-3.0 to reduce false signals, decrease to 1.5-1.8 for earlier signals |

**Pro Tip:** The "Bollinger Band Squeeze" occurs when volatility reaches a low point and the bands narrow significantly. This compression often precedes major price moves, making it a powerful setup for breakout traders when combined with increasing volume.

## Calculation and Mathematical Foundation

**Simplified explanation:**
Bollinger Bands consist of three lines: a middle band (typically a 20-period simple moving average), an upper band (middle band plus two standard deviations), and a lower band (middle band minus two standard deviations). As price volatility increases, the bands widen; as volatility decreases, they contract.

**Technical formula:**
Middle Band = SMA(source, period)
Upper Band = Middle Band + (multiplier × StdDev(source, period))
Lower Band = Middle Band - (multiplier × StdDev(source, period))

Where:
* SMA is the Simple Moving Average
* StdDev is the Standard Deviation
* source is typically the closing price
* period is the lookback window (usually 20)
* multiplier is typically 2

> 🔍 **Technical Note:** The implementation uses a single-pass algorithm with a circular buffer for efficiency, avoiding the need to recalculate the entire sum for each new bar. This approach significantly improves performance for longer lookback periods.

## Interpretation Details

Bollinger Bands provide multiple trading signals and insights:

* **Bollinger Bounce:** Prices tend to return to the middle band, creating potential mean-reversion trades when price touches the outer bands in ranging markets
* **Bollinger Squeeze:** When bands narrow significantly (low volatility), it often precedes a sharp price movement and potential breakout opportunity
* **Walking the Band:** During strong trends, price may "walk" along an outer band, indicating trend continuation rather than reversal
* **Double Bottoms/Tops:** More reliable when the second bottom/top occurs outside the band but the indicator shows decreasing momentum

Traders should pay attention to where price closes relative to the bands rather than just touches, as closes beyond the bands are often more significant signals.

## Limitations and Considerations

* **Market conditions:** Less effective in directionless, choppy markets with frequent small reversals
* **Lag factor:** The SMA middle band introduces some lag, potentially delaying signals in fast-moving markets
* **False signals:** Outer band touches don't always indicate reversals, especially in strongly trending markets
* **Complementary tools:** Best combined with non-correlated indicators like volume, momentum oscillators (RSI, Stochastic), or candlestick patterns for confirmation

## References

* Bollinger, J. (2002). Bollinger on Bollinger Bands. McGraw-Hill Education.
* Murphy, J. J. (1999). Technical Analysis of the Financial Markets. New York Institute of Finance.
