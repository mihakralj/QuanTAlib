# Channels

> "In trending markets, ride the channel. In ranging markets, fade the edges."  Unknown

Channels define dynamic support and resistance. Upper band shows where price tends to find resistance; lower band shows support. Width measures volatility; price position within channel measures momentum and mean-reversion potential.

## Indicators

| Indicator | Full Name | Description |
| :--- | :--- | :--- |
| [ABBER](lib/channels/abber/Abber.md) | Aberration Bands | Absolute deviation-based volatility bands. More robust than standard deviation. |
| [ACCBANDS](lib/channels/accbands/Accbands.md) | Acceleration Bands | Volatility-based adaptive channel by Price Headley. Width adapts to momentum. |
| [APCHANNEL](lib/channels/apchannel/Apchannel.md) | Andrews' Pitchfork | Three-line channel based on pivot points. Projects trend support/resistance. |
| [APZ](lib/channels/apz/Apz.md) | Adaptive Price Zone | Double-smoothed EMA volatility channel by Lee Leibfarth. Adapts to recent volatility. |
| [ATRBANDS](lib/channels/atrbands/Atrbands.md) | ATR Bands | ATR-based volatility bands around moving average. |
| BBANDS | Bollinger Bands | Standard deviation bands around SMA. Classic volatility channel. |
| [DCHANNEL](lib/channels/dchannel/Dchannel.md) | Donchian Channels | Highest high and lowest low over N periods. Turtle trading foundation. |
| [DECAYCHANNEL](lib/channels/decaychannel/decaychannel.md) | Decay Min-Max Channel | Exponentially decaying min-max channel. Half-life decay toward midpoint. |
| [FCB](lib/channels/fcb/fcb.md) | Fractal Chaos Bands | Tracks fractal highs and lows. Identifies chaos-based support/resistance. |
| [JBANDS](lib/channels/jbands/Jbands.md) | Jurik Adaptive Envelope Bands | JMA's internal adaptive envelopes. Snap to extremes, decay toward price. |
| [KCHANNEL](lib/channels/kchannel/kchannel.md) | Keltner Channel | EMA with ATR bands. Smoother than Bollinger. |
| [MAENV](lib/channels/maenv/maenv.md) | Moving Average Envelope | Fixed percentage bands around moving average. Simple but effective. |
| [MMCHANNEL](lib/channels/mmchannel/mmchannel.md) | Min-Max Channel | Rolling highest high / lowest low using O(1) monotonic deques. |
| [PCHANNEL](lib/channels/pchannel/pchannel.md) | Price Channel | Highest high and lowest low. Identical to Donchian Channels. |
| [REGCHANNEL](lib/channels/regchannel/regchannel.md) | Linear Regression Channel | Linear regression line with standard deviation bands. |
| [SDCHANNEL](lib/channels/sdchannel/sdchannel.md) | Standard Deviation Channel | Moving average with standard deviation bands. |
| [STARCHANNEL](lib/channels/starchannel/starchannel.md) | Stoller Average Range Channel | SMA with ATR bands. Similar to Keltner but uses SMA instead of EMA. |
| STBANDS | Super Trend Bands | ATR-based trend-following bands. Flips direction on breakout. |
| UBANDS | Ultimate Bands | Composite volatility bands using multiple measures. |
| UCHANNEL | Ultimate Channel | Adaptive channel using multiple volatility inputs. |
| VWAPBANDS | VWAP Bands | Volatility bands around VWAP. Institutional trading reference. |
| VWAPSD | VWAP Standard Deviation Bands | Standard deviation bands around VWAP. |
