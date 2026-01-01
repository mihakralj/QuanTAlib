# PCHANNEL: Price Channel

## Overview and Purpose

The Price Channel is a simple volatility-based indicator that plots the highest high and the lowest low over a user-defined lookback period. It is very similar in concept and application to Donchian Channels. The channel visually represents the trading range of an asset over the specified period.

A middle line, typically the average of the upper and lower channel lines, can also be plotted to serve as a mean reference.

## Core Concepts

* **Highest High:** The upper band represents the highest price reached during the lookback period.
* **Lowest Low:** The lower band represents the lowest price reached during thelookback period.
* **Trading Range:** The channel effectively shows the price extremes for the chosen period.
* **Breakout Indication:** Prices moving above the upper channel or below the lower channel can signal potential breakouts and the start of new trends.

## Common Settings and Parameters

| Parameter | Default | Function | When to Adjust |
| :-------- | :------ | :------- | :------------- |
| Length | 20 | Lookback period for determining the highest high and lowest low. | Shorter lengths make the channel more reactive to recent price action; longer lengths create a wider, smoother channel representing longer-term ranges. |

## Calculation and Mathematical Foundation

**Simplified explanation:**
1. For each bar, look back over the specified `Length`.
2. Identify the absolute highest `high` price during that period. This forms the Upper Channel line.
3. Identify the absolute lowest `low` price during that period. This forms the Lower Channel line.
4. (Optional) The Middle Channel line is the average of the Upper and Lower Channel lines: `(Upper Channel + Lower Channel) / 2`.

**Technical formula:**
1. **Upper Channel:**
    `UpperChannel = Highest(High, Length)`

2. **Lower Channel:**
    `LowerChannel = Lowest(Low, Length)`

3. **Middle Channel (optional):**
    `MiddleChannel = (UpperChannel + LowerChannel) / 2`

## Interpretation Details

* **Support and Resistance:** The upper band can act as resistance, and the lower band as support.
* **Breakouts:**
    * A close above the Upper Channel suggests bullish strength and a potential upside breakout.
    * A close below the Lower Channel suggests bearish pressure and a potential downside breakout.
* **Trend Identification:**
    * In an uptrend, prices may consistently touch or "ride" the Upper Channel.
    * In a downtrend, prices may consistently touch or "ride" the Lower Channel.
* **Volatility:** The width of the channel can give an indication of volatility. Wider channels suggest higher volatility over the lookback period.
* **"Turtle Trading" Strategy:** Price Channels (like Donchian Channels) were famously used in the "Turtle Trading" system, where breakouts from the channel were used as entry signals.

## Limitations and Considerations

* **Lag:** Like all indicators based on lookback periods, there's an inherent lag. The channel reflects past price action.
* **Whipsaws:** In choppy, non-trending markets, breakouts can be false, leading to whipsaws.
* **Parameter Choice:** The `Length` parameter is crucial. A length too short may generate many false signals, while one too long may miss timely entries.
* **Not a Standalone System:** Best used in conjunction with other indicators (e.g., volume, trend indicators) or price action analysis for confirmation.

## References

* Donchian, R. D. (Various). (Conceptual basis for channel breakouts).
* Faith, C. (2007). *Way of the Turtle*. McGraw-Hill. (Describes trading systems using similar channels).
