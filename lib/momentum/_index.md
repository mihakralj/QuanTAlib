# Momentum Indicators

> "Momentum tells you how fast price is moving. Whether that movement has meaning: entirely different question."

Momentum indicators measure the velocity and acceleration of price changes. Unlike trend indicators (which answer "where is price going?"), momentum indicators answer "how fast?" and "is it slowing down?". This distinction matters: a strong trend can have weakening momentum (divergence), and a ranging market can show momentum spikes (false breakouts).

Core momentum concepts:

- **Rate of Change**: Simple difference or ratio between current and historical prices
- **Smoothed Momentum**: Filtered velocity to reduce noise while preserving turning points
- **Bounded Oscillators**: Normalized to fixed range (0-100 or -100 to +100) for threshold-based signals
- **Unbounded Oscillators**: Raw magnitude, requiring context-dependent interpretation

## Implementation Status

| Indicator | Full Name | Status | Description |
| :--- | :--- | :---: | :--- |
| ADX | Average Directional Index | =Ë | Quantifies trend intensity by smoothing the expansion of daily ranges, independent of direction |
| ADXR | ADX Rating | =Ë | Averages current and historical ADX to measure momentum change |
| AMAT | Archer Moving Averages Trends | =Ë | Identifies trend direction and strength using dual EMAs with slope confirmation |
| AO | Awesome Oscillator | =Ë | Measures immediate velocity vs broader trend using fast/slow median-price SMAs |
| APO | Absolute Price Oscillator | =Ë | Absolute difference between two EMAs |
| AROON | Aroon | =Ë | Gauges trend freshness by measuring time elapsed since last high and low |
| AROONOSC | Aroon Oscillator | =Ë | Difference between Aroon Up and Aroon Down |
| [BOP](bop/Bop.md) | Balance of Power |  | Measures buyer/seller strength by comparing close to open relative to range |
| CCI | Commodity Channel Index | =Ë | Measures price deviation from statistical mean, identifies cyclical turns |
| [CFB](cfb/Cfb.md) | Composite Fractal Behavior |  | Measures trend duration and quality via fractal efficiency across 96 time scales |
| CHOP | Choppiness Index | =Ë | Quantifies market choppiness vs trending behavior |
| CMO | Chande Momentum Oscillator | =Ë | Momentum using both up and down changes, bounded but not clamped like RSI |
| DMX | Directional Movement Index | =Ë | Low-lag, bipolar replacement for DMI/ADX combining direction and strength |
| DPO | Detrended Price Oscillator | =Ë | Removes trend to isolate cycles |
| DX | Directional Index | =Ë | Base component for ADX calculation |
| FISHER | Fisher Transform | =Ë | Gaussian normalization for clearer turning points |
| IMI | Intraday Momentum Index | =Ë | Candlestick-based momentum for intraday analysis |
| INERTIA | Inertia | =Ë | Measures resistance to price change |
| KDJ | KDJ Indicator | =Ë | Extended stochastic with J line for divergence |
| [MACD](macd/Macd.md) | Moving Average Convergence Divergence |  | Relationship between two EMAs, identifies momentum and trend direction |
| MOM | Momentum | =Ë | Raw price change over specified period |
| PGO | Pretty Good Oscillator | =Ë | Normalized momentum relative to ATR |
| PMO | Price Momentum Oscillator | =Ë | Double-smoothed ROC oscillator |
| PPO | Percentage Price Oscillator | =Ë | MACD expressed as percentage for cross-instrument comparison |
| PRS | Price Relative Strength | =Ë | Performance ratio between two assets |
| QSTICK | Qstick | =Ë | Quantifies candlestick patterns |
| [ROC](roc/Roc.md) | Rate of Change |  | Absolute price change over N periods |
| ROCP | Rate of Change Percentage | =Ë | Percentage price change over N periods |
| ROCR | Rate of Change Ratio | =Ë | Price ratio over N periods |
| [RSI](rsi/Rsi.md) | Relative Strength Index |  | Speed and change of price movements, bounded 0-100 |
| [RSX](rsx/Rsx.md) | Relative Strength Quality Index |  | Noise-free RSI using cascaded IIR filters, zero lag at turning points |
| SMI | Stochastic Momentum Index | =Ë | Stochastic variant measuring distance from midpoint of range |
| STOCH | Stochastic Oscillator | =Ë | Position within recent range, classic overbought/oversold indicator |
| STOCHF | Stochastic Fast | =Ë | Unsmoothed stochastic for faster signals |
| STOCHRSI | Stochastic RSI | =Ë | Stochastic applied to RSI for faster extremes |
| TRIX | Triple Exponential Average | =Ë | Triple-smoothed rate of change |
| TSI | True Strength Index | =Ë | Double-smoothed momentum oscillator |
| ULTOSC | Ultimate Oscillator | =Ë | Combines three timeframes with weighted averages |
| [VEL](vel/Vel.md) | Jurik Velocity |  | Market acceleration via PWMA vs WMA differential |
| VORTEX | Vortex Indicator | =Ë | Trend direction and strength from true range |
| WILLR | Williams %R | =Ë | Inverse stochastic, measures overbought/oversold |

**Legend**:  Implemented | =Ë Planned

## Indicator Selection Guide

| Use Case | Recommended | Rationale |
| :--- | :--- | :--- |
| Overbought/Oversold | RSI, RSX | Bounded, well-understood thresholds |
| Trend Momentum | MACD, CFB | Combines direction with strength |
| Zero-Lag Signals | RSX, VEL | Jurik filters minimize lag at turning points |
| Divergence Analysis | RSX, MACD | Clear peaks without noise chatter |
| Cross-Instrument | PPO, CMO | Percentage-based for comparability |
| Noise Tolerance | RSX, VEL | Cascaded filtering rejects high-frequency noise |
