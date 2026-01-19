# Dynamics

> "The trend is your friend, but only if you know its strength."  Unknown

Dynamics indicators measure trend strength, speed, and direction. Unlike momentum indicators that measure rate of change, dynamics indicators answer: "Is there a trend, and how strong is it?" Critical for filtering signals and avoiding whipsaws in ranging markets.

## Indicator Status

| Indicator | Full Name | Status | Description |
| :--- | :--- | :---: | :--- |
| [ADX](lib/dynamics/adx/Adx.md) | Average Directional Index |  | Trend strength 0-100. Direction-agnostic. <20 weak, >40 strong. |
| [ADXR](lib/dynamics/adxr/Adxr.md) | Average Directional Movement Rating |  | Smoothed ADX. Average of current and N-period ago ADX. |
| ALLIGATOR | Williams Alligator | =Ë | Three SMAs (Jaw, Teeth, Lips). Spread indicates trend strength. |
| [AMAT](lib/dynamics/amat/Amat.md) | Archer Moving Averages Trends |  | Multiple EMA alignment. Requires fast/slow EMA plus directional confirmation. |
| [AROON](lib/dynamics/aroon/Aroon.md) | Aroon |  | Time since high/low. Aroon Up/Down measure recency of extremes. |
| [AROONOSC](lib/dynamics/aroonosc/AroonOsc.md) | Aroon Oscillator |  | Aroon Up minus Aroon Down. Single line: +100 to -100. |
| CHOP | Choppiness Index | =Ë | Trendiness measure. High values = choppy. Low = trending. |
| [DMX](lib/dynamics/dmx/Dmx.md) | Jurik DMX |  | Smoothed bipolar DMI using Jurik smoothing. Low noise. |
| DX | Directional Movement Index | =Ë | Raw directional strength. Unsmoothed ADX component. |
| HT_TRENDMODE | HT Trend vs Cycle | =Ë | Ehlers Hilbert Transform. Binary trend/cycle mode detection. |
| ICHIMOKU | Ichimoku Cloud | =Ë | Five-line system. Cloud defines support/resistance zones. |
| IMI | Intraday Momentum Index | =Ë | RSI variant using open-close range. Intraday overbought/oversold. |
| QSTICK | Qstick | =Ë | MA of (Close - Open). Positive = buying pressure. |
| [SUPER](lib/dynamics/super/Super.md) | SuperTrend |  | ATR-based trailing stop. Flips on breakout. Color-coded direction. |
| TTM | TTM Trend | =Ë | Fast 6-period EMA. Color-coded trend from John Carter. |
| VORTEX | Vortex Indicator | =Ë | VI+ and VI- measure positive/negative trend movement. |

**Status Key:**  Implemented | =Ë Planned

## Selection Guide

| Use Case | Recommended | Why |
| :--- | :--- | :--- |
| Trend strength filter | ADX | Industry standard. <20 avoid trend trades; >40 strong trend. |
| Trend direction + strength | AROON, AROONOSC | Measures how recently price made new highs vs lows. |
| Trend following stops | SUPER | ATR-based dynamic support/resistance. Clear entry/exit. |
| Low-noise direction | DMX | Jurik smoothing reduces whipsaws vs standard DMI. |
| Trend confirmation | AMAT | Multiple timeframe EMA alignment required for signal. |
| Choppy market detection | CHOP, ADX | CHOP high or ADX low means avoid trend strategies. |

## ADX Interpretation

| ADX Value | Trend Strength | Recommended Action |
| :---: | :--- | :--- |
| 0-20 | Absent or weak | Avoid trend-following. Use mean reversion. |
| 20-25 | Emerging | Early trend possible. Confirm with direction. |
| 25-40 | Strong | Trend-following strategies work well. |
| 40-50 | Very strong | Trend mature. Watch for exhaustion. |
| 50+ | Extreme | Unsustainable. Reversal risk increases. |

ADX tells strength, not direction. Use +DI/-DI or other direction indicators alongside.

## Dynamics vs Momentum

| Aspect | Dynamics | Momentum |
| :--- | :--- | :--- |
| Measures | Trend existence/strength | Rate of price change |
| Direction | Often direction-agnostic | Usually directional |
| Best for | Filtering | Timing |
| Examples | ADX, CHOP, AROON | RSI, MACD, ROC |

Use dynamics to filter when to trade. Use momentum to time entries/exits.