# Dynamics

Dynamics indicators measure the strength, speed, and direction of price movement. These tools help identify whether a market is trending or ranging.

| Indicator | Full Name | Description |
| :--- | :--- | :--- |
| [ADX](lib/dynamics/adx/Adx.md) | Average Directional Index | Measures trend strength, regardless of direction (uses +DI and -DI). |
| [ADXR](lib/dynamics/adxr/Adxr.md) | Average Directional Movement Rating | Smoothed version of ADX, often used in conjunction with ADX for signals. |
| ALLIGATOR | Williams Alligator | Uses three smoothed moving averages (Jaw, Teeth, Lips) to identify trends and trading ranges. |
| [AMAT](lib/dynamics/amat/Amat.md) | Archer Moving Averages Trends | Trend identification using multiple EMAs; requires alignment of fast/slow EMAs and directional movement. |
| [AROON](lib/dynamics/aroon/Aroon.md) | Aroon | Identifies trend direction and strength by measuring time since price recorded new highs/lows. |
| [AROONOSC](lib/dynamics/aroonosc/AroonOsc.md) | Aroon Oscillator | Single-line oscillator (Aroon Up - Aroon Down); positive = bullish, negative = bearish. |
| CHOP | Choppiness Index | Non-directional indicator measuring market trendiness; higher values indicate choppy sideways markets. |
| [DMX](lib/dynamics/dmx/Dmx.md) | Jurik Directional Movement Index | Smoothed Bipolar Directional Movement Index (DMI). |
| DX | Directional Movement Index | Measures directional strength; unsmoothed component of ADX. |
| HT_TRENDMODE | Hilbert Transform - Trend vs Cycle Mode | Uses Hilbert Transform to determine if the market is in a trending or cycling phase. |
| ICHIMOKU | Ichimoku Cloud | Comprehensive system with 5 components (Tenkan, Kijun, Senkou A/B, Chikou); cloud shows support/resistance zones. |
| IMI | Intraday Momentum Index | RSI-like indicator using intraday ranges (open vs close); identifies overbought/oversold conditions. |
| QSTICK | Qstick Indicator | Measures buying/selling pressure through moving average of close-open differences; positive = bullish, negative = bearish. |
| [SUPER](lib/dynamics/super/Super.md) | SuperTrend | ATR-based trend following with dynamic support/resistance; green line below price = bullish, red above = bearish. |
| TTM | TTM Trend | Fast 6-period EMA with color-coded trend; green = bullish, red = bearish; part of John Carter's TTM system. |
| VORTEX | Vortex Indicator | Uses VI+ and VI- to identify trend direction and strength based on vortex price movement. |