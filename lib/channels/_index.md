# Channels

> "In trending markets, ride the channel. In ranging markets, fade the edges."  Unknown

Channels define dynamic support and resistance. Upper band shows where price tends to find resistance; lower band shows support. Width measures volatility; price position within channel measures momentum and mean-reversion potential.

## Indicator Status

| Indicator | Full Name | Status | Description |
| :--- | :--- | :---: | :--- |
| [ABBER](lib/channels/abber/abber.md) | Aberration Bands |  | Absolute deviation-based volatility bands. More robust than standard deviation. |
| [ACCBANDS](lib/channels/accbands/accbands.md) | Acceleration Bands |  | Volatility-based adaptive channel by Price Headley. Width adapts to momentum. |
| APCHANNEL | Andrews' Pitchfork | =Ë | Three-line channel based on pivot points. Projects trend support/resistance. |
| [APZ](lib/channels/apz/apz.md) | Adaptive Price Zone |  | Double-smoothed EMA volatility channel by Lee Leibfarth. Adapts to recent volatility. |
| ATRBANDS | ATR Bands | =Ë | ATR-based volatility bands around moving average. |
| BBANDS | Bollinger Bands | =Ë | Standard deviation bands around SMA. Classic volatility channel. |
| DCHANNEL | Donchian Channels | =Ë | Highest high and lowest low over N periods. Turtle trading foundation. |
| DECAYCHANNEL | Decay Min-Max Channel | =Ë | Exponentially decaying min-max channel. More responsive than Donchian. |
| FCB | Fractal Chaos Bands | =Ë | Tracks fractal highs and lows. Identifies chaos-based support/resistance. |
| JBANDS | Jurik Volatility Bands | =Ë | JMA-based volatility bands. Low lag with controlled overshoot. |
| KCHANNEL | Keltner Channel | =Ë | EMA with ATR bands. Smoother than Bollinger. |
| MAENV | Moving Average Envelope | =Ë | Fixed percentage bands around moving average. Simple but effective. |
| MMCHANNEL | Min-Max Channel | =Ë | Rolling minimum and maximum over lookback period. |
| PCHANNEL | Price Channel | =Ë | Highest high and lowest low. Similar to Donchian. |
| REGCHANNEL | Regression Channels | =Ë | Linear regression line with standard deviation bands. |
| SDCHANNEL | Standard Deviation Channel | =Ë | Moving average with standard deviation bands. |
| STARCHANNEL | Stoller Average Range Channel | =Ë | ATR-based channel around moving average. Similar to Keltner. |
| STBANDS | Super Trend Bands | =Ë | ATR-based trend-following bands. Flips direction on breakout. |
| UBANDS | Ultimate Bands | =Ë | Composite volatility bands using multiple measures. |
| UCHANNEL | Ultimate Channel | =Ë | Adaptive channel using multiple volatility inputs. |
| VWAPBANDS | VWAP Bands | =Ë | Volatility bands around VWAP. Institutional trading reference. |
| VWAPSD | VWAP Standard Deviation Bands | =Ë | Standard deviation bands around VWAP. |

**Status Key:**  Implemented | =Ë Planned

## Selection Guide

| Use Case | Recommended | Why |
| :--- | :--- | :--- |
| Volatility breakouts | ACCBANDS, BBANDS | Width expansion signals regime change. |
| Mean reversion | APZ, BBANDS | Band touches indicate overextension. |
| Trend riding | DCHANNEL, KCHANNEL | Clear trend direction with dynamic support/resistance. |
| Robust to outliers | ABBER | Absolute deviation less sensitive than standard deviation. |
| Low-lag bands | JBANDS, APZ | JMA/double-smoothed EMA cores reduce lag. |
| Institutional reference | VWAPBANDS | VWAP is common institutional benchmark. |

## Channel Types

| Type | Examples | Volatility Measure | Best For |
| :--- | :--- | :--- | :--- |
| Standard Deviation | BBANDS, SDCHANNEL | Ã of returns | Normal distributions |
| Absolute Deviation | ABBER | Mean absolute deviation | Fat-tailed distributions |
| ATR-based | KCHANNEL, STARCHANNEL | Average True Range | Trend-following |
| Price Range | DCHANNEL, PCHANNEL | High-low range | Breakout systems |
| Adaptive | APZ, ACCBANDS | Dynamic volatility | Regime changes |