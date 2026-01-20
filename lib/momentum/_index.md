# Momentum

> "Momentum tells you how fast price is moving. Whether that movement has meaning: entirely different question."  Unknown

Momentum indicators measure the velocity and acceleration of price changes. Unlike trend indicators (which answer "where is price going?"), momentum indicators answer "how fast?" and "is it slowing down?". This distinction matters: a strong trend can have weakening momentum (divergence), and a ranging market can show momentum spikes (false breakouts).

## Indicators

| Indicator | Full Name | Description |
| :--- | :--- | :--- |
| APO | Absolute Price Oscillator | Absolute difference between two EMAs. |
| [BOP](lib/momentum/bop/Bop.md) | Balance of Power | Measures buyer/seller strength by comparing close to open relative to range. |
| CCI | Commodity Channel Index | Measures price deviation from statistical mean, identifies cyclical turns. |
| [CFB](lib/momentum/cfb/Cfb.md) | Composite Fractal Behavior | Measures trend duration and quality via fractal efficiency across 96 time scales. |
| CMO | Chande Momentum Oscillator | Momentum using both up and down changes, bounded but not clamped like RSI. |
| [MACD](lib/momentum/macd/Macd.md) | Moving Average Convergence Divergence | Relationship between two EMAs, identifies momentum and trend direction. |
| MOM | Momentum | Raw price change over specified period. |
| PMO | Price Momentum Oscillator | Double-smoothed ROC oscillator. |
| PPO | Percentage Price Oscillator | MACD expressed as percentage for cross-instrument comparison. |
| PRS | Price Relative Strength | Performance ratio between two assets. |
| [ROC](lib/momentum/roc/Roc.md) | Rate of Change | Absolute price change over N periods. |
| ROCP | Rate of Change Percentage | Percentage price change over N periods. |
| ROCR | Rate of Change Ratio | Price ratio over N periods. |
| [RSI](lib/momentum/rsi/Rsi.md) | Relative Strength Index | Speed and change of price movements, bounded 0-100. |
| [RSX](lib/momentum/rsx/Rsx.md) | Relative Strength Quality Index | Noise-free RSI using cascaded IIR filters, zero lag at turning points. |
| TSI | True Strength Index | Double-smoothed momentum oscillator. |
| [VEL](lib/momentum/vel/Vel.md) | Jurik Velocity | Market acceleration via PWMA vs WMA differential. |
