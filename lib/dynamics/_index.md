# Dynamics

> "The trend is your friend, but only if you know its strength."  Unknown

Dynamics indicators measure trend strength, speed, and direction. Unlike momentum indicators that measure rate of change, dynamics indicators answer: "Is there a trend, and how strong is it?" Critical for filtering signals and avoiding whipsaws in ranging markets.

## Indicators

| Indicator | Full Name | Description |
| :--- | :--- | :--- |
| [ADX](lib/dynamics/adx/Adx.md) | Average Directional Index | Trend strength 0-100. Direction-agnostic. <20 weak, >40 strong. |
| [ADXR](lib/dynamics/adxr/Adxr.md) | Average Directional Movement Rating | Smoothed ADX. Average of current and N-period ago ADX. |
| ALLIGATOR | Williams Alligator | Three SMAs (Jaw, Teeth, Lips). Spread indicates trend strength. |
| [AMAT](lib/dynamics/amat/Amat.md) | Archer Moving Averages Trends | Multiple EMA alignment. Requires fast/slow EMA plus directional confirmation. |
| [AROON](lib/dynamics/aroon/Aroon.md) | Aroon | Time since high/low. Aroon Up/Down measure recency of extremes. |
| [AROONOSC](lib/dynamics/aroonosc/Aroonosc.md) | Aroon Oscillator | Aroon Up minus Aroon Down. Single line: +100 to -100. |
| CHOP | Choppiness Index | Trendiness measure. High values = choppy. Low = trending. |
| [DMX](lib/dynamics/dmx/Dmx.md) | Jurik DMX | Smoothed bipolar DMI using Jurik smoothing. Low noise. |
| DX | Directional Movement Index | Raw directional strength. Unsmoothed ADX component. |
| HT_TRENDMODE | HT Trend vs Cycle | Ehlers Hilbert Transform. Binary trend/cycle mode detection. |
| ICHIMOKU | Ichimoku Cloud | Five-line system. Cloud defines support/resistance zones. |
| IMI | Intraday Momentum Index | RSI variant using open-close range. Intraday overbought/oversold. |
| QSTICK | Qstick | MA of (Close - Open). Positive = buying pressure. |
| SUPER | SuperTrend | ATR-based trailing stop. Flips on breakout. Color-coded direction. |
| TTM | TTM Trend | Fast 6-period EMA. Color-coded trend from John Carter. |
| VORTEX | Vortex Indicator | VI+ and VI- measure positive/negative trend movement. |
| IMPULSE | Elder Impulse System | Combines EMA and MACD histogram; color-coded system (green=bullish, red=bearish, blue=neutral) |