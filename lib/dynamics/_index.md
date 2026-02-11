# Dynamics

> "The trend is your friend, but only if you know its strength."  Unknown

Dynamics indicators measure trend strength, speed, and direction. Unlike momentum indicators that measure rate of change, dynamics indicators answer: "Is there a trend, and how strong is it?" Critical for filtering signals and avoiding whipsaws in ranging markets.

## Indicators

| Indicator | Full Name | Description |
| :--- | :--- | :--- |
| [ADX](adx/Adx.md) | Average Directional Index | Trend strength 0-100. Direction-agnostic. <20 weak, >40 strong. |
| [ADXR](adxr/Adxr.md) | Average Directional Movement Rating | Smoothed ADX. Average of current and N-period ago ADX. |
| [ALLIGATOR](alligator/Alligator.md) | Williams Alligator | Three SMAs (Jaw, Teeth, Lips). Spread indicates trend strength. |
| [AMAT](amat/Amat.md) | Archer Moving Averages Trends | Multiple EMA alignment. Requires fast/slow EMA plus directional confirmation. |
| [AROON](aroon/Aroon.md) | Aroon | Time since high/low. Aroon Up/Down measure recency of extremes. |
| [AROONOSC](aroonosc/Aroonosc.md) | Aroon Oscillator | Aroon Up minus Aroon Down. Single line: +100 to -100. |
| [CHOP](chop/Chop.md) | Choppiness Index | Trendiness measure. High values = choppy. Low = trending. |
| [DMX](dmx/Dmx.md) | Jurik DMX | Smoothed bipolar DMI using Jurik smoothing. Low noise. |
| [DX](dx/Dx.md) | Directional Movement Index | Raw directional strength. Unsmoothed ADX component. |
| [HT_TRENDMODE](ht_trendmode/Ht_trendmode.md) | HT Trend vs Cycle | Ehlers Hilbert Transform. Binary trend/cycle mode detection. |
| [ICHIMOKU](ichimoku/Ichimoku.cs) | Ichimoku Cloud | Five-line system. Cloud defines support/resistance zones. |
| [IMI](imi/Imi.cs) | Intraday Momentum Index | RSI variant using open-close range. Intraday overbought/oversold. |
| [QSTICK](qstick/Qstick.md) | Qstick | MA of (Close - Open). Positive = buying pressure. |
| [SUPER](super/Super.md) | SuperTrend | ATR-based trailing stop. Flips on breakout. Color-coded direction. |
| [TTM_TREND](ttm_trend/TtmTrend.md) | TTM Trend | Fast 6-period EMA. Color-coded trend from John Carter. |
| [TTM_SQUEEZE](ttm_squeeze/TtmSqueeze.md) | TTM Squeeze | BB inside KC squeeze detection with linear regression momentum. John Carter. |
| [VORTEX](vortex/Vortex.md) | Vortex Indicator | VI+ and VI- measure positive/negative trend movement. |
