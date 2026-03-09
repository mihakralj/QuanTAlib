# Dynamics

> "The trend is your friend, but only if you know its strength."  Unknown

Dynamics indicators measure trend strength, speed, and direction. Unlike momentum indicators that measure rate of change, dynamics indicators answer: "Is there a trend, and how strong is it?" Critical for filtering signals and avoiding whipsaws in ranging markets.

## Indicators

| Indicator                                   | Full Name                                    | Description                                                                   |
| :------------------------------------------ | :------------------------------------------- | :---------------------------------------------------------------------------- |
| [ADX](adx/Adx.md)                           | Average Directional Index                    | Trend strength 0-100. Direction-agnostic. <20 weak, >40 strong.               |
| [ADXR](adxr/Adxr.md)                        | Average Directional Movement Rating          | Smoothed ADX. Average of current and N-period ago ADX.                        |
| [ALLIGATOR](alligator/Alligator.md)         | Williams Alligator                           | Three SMAs (Jaw, Teeth, Lips). Spread indicates trend strength.               |
| [AMAT](amat/Amat.md)                        | Archer Moving Averages Trends                | Multiple EMA alignment. Requires fast/slow EMA plus directional confirmation. |
| [AROON](aroon/Aroon.md)                     | Aroon                                        | Time since high/low. Aroon Up/Down measure recency of extremes.               |
| [AROONOSC](aroonosc/Aroonosc.md)            | Aroon Oscillator                             | Aroon Up minus Aroon Down. Single line: +100 to -100.                         |
| [CHOP](chop/Chop.md)                        | Choppiness Index                             | Trendiness measure. High values = choppy. Low = trending.                     |
| [DMX](dmx/Dmx.md)                           | Jurik DMX                                    | Smoothed bipolar DMI using Jurik smoothing. Low noise.                        |
| [DX](dx/Dx.md)                              | Directional Movement Index                   | Raw directional strength. Unsmoothed ADX component.                           |
| [MINUS_DI](minusdi/MinusDi.md)              | Minus Directional Indicator                  | Downward directional movement as % of true range. 0-100.                      |
| [MINUS_DM](minusdm/MinusDm.md)              | Minus Directional Movement                   | Wilder-smoothed downward directional movement. Price units.                   |
| [HT_TRENDMODE](ht_trendmode/Httrendmode.md) | Ehlers Hilbert Transform Trend vs Cycle Mode | Ehlers Hilbert Transform. Binary trend/cycle mode detection.                  |
| [ICHIMOKU](ichimoku/Ichimoku.md)            | Ichimoku Cloud                               | Five-line system. Cloud defines support/resistance zones.                     |
| [IMPULSE](impulse/Impulse.md)               | Elder Impulse System                         | EMA + MACD histogram alignment. Color-coded trend/momentum filter.            |
| [QSTICK](qstick/Qstick.md)                  | Qstick                                       | MA of (Close - Open). Positive = buying pressure.                             |
| [SUPER](super/Super.md)                     | SuperTrend                                   | ATR-based trailing stop. Flips on breakout. Color-coded direction.            |
| [TTM_TREND](ttm_trend/TtmTrend.md)          | TTM Trend                                    | Fast 6-period EMA. Color-coded trend from John Carter.                        |
| [TTM_SQUEEZE](ttm_squeeze/TtmSqueeze.md)    | TTM Squeeze                                  | BB inside KC squeeze detection with linear regression momentum. John Carter.  |
| [VORTEX](vortex/Vortex.md)                  | Vortex Indicator                             | VI+ and VI- measure positive/negative trend movement.                         |
| [GHLA](ghla/Ghla.md)                       | Gann High-Low Activator                      | SMA(High)/SMA(Low) alternating on crossover.                                  |
| [PFE](pfe/Pfe.md)                          | Polarized Fractal Efficiency                 | Trend efficiency: straight-line / total path distance.                        |
| [PLUS_DI](plusdi/PlusDi.md)                 | Plus Directional Indicator                   | Upward directional movement as % of true range. 0-100.                        |
| [PLUS_DM](plusdm/PlusDm.md)                 | Plus Directional Movement                    | Wilder-smoothed upward directional movement. Price units.                     |
| [RAVI](ravi/Ravi.md)                        | Chande Range Action Verification Index       | \|SMA(short) − SMA(long)\| / SMA(long) × 100.                                 |
| [VHF](vhf/Vhf.md)                          | Vertical Horizontal Filter                   | Max-min range / sum of absolute changes.                                      |
