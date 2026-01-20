# Volatility

> "Volatility is the price of admission. The question is whether the ride is worth it."

Volatility measures the magnitude of price changes, independent of direction. Low volatility indicates consolidation and coiling energy; high volatility indicates explosive movement and trend development. These indicators answer "how much?" and "how fast?", not "which way?".

| Indicator | Full Name | Description |
| :--- | :--- | :--- |
| [ADR](lib/volatility/adr/Adr.md) | Average Daily Range | Simple High-Low range without gap adjustment. |
| [ATR](lib/volatility/atr/Atr.md) | Average True Range | Standard volatility measure accounting for gaps via True Range. |
| [ATRN](lib/volatility/atrn/Atrn.md) | ATR Normalized | ATR normalized to [0,1] based on historical min/max. |
| [ATRP](lib/volatility/atrp/Atrp.md) | ATR Percent | ATR as percentage of close price. |
| BBW | Bollinger Band Width | Distance between upper and lower Bollinger Bands. |
| BBWN | BB Width Normalized | BBW normalized to [0,1] range. |
| BBWP | BB Width Percentile | BBW percentile rank over lookback. |
| CCV | Close-to-Close Volatility | Annualized volatility from log returns. |
| CV | Conditional Volatility | GARCH(1,1) model for time-varying volatility. |
| CVI | Chaikin Volatility | Rate of change in smoothed High-Low range. |
| EWMA | EWMA Volatility | Exponentially weighted squared returns. |
| GKV | Garman-Klass Volatility | Efficient OHLC-based estimator. |
| HLV | High-Low Volatility | Range-based volatility without close. |
| HV | Historical Volatility | Standard deviation of returns. |
| JVOLTY | Jurik Volatility | Low-lag, smooth Jurik volatility. |
| JVOLTYN | Jurik Volatility Normalized | JVOLTY normalized to [0,1]. |
| MASSI | Mass Index | Range expansion/contraction for reversal detection. |
| NATR | Normalized ATR | ATR as percentage (equivalent to ATRP). |
| PV | Parkinson Volatility | High-Low estimator assuming no drift. |
| RSV | Rogers-Satchell Volatility | OHLC estimator with drift adjustment. |
| RV | Realized Volatility | High-frequency intraday volatility. |
| RVI | Relative Volatility Index | Directional volatility measure. |
| TR | True Range | Single-bar volatility with gap capture. |
| UI | Ulcer Index | Downside risk and drawdown depth/duration. |
| VOV | Volatility of Volatility | Second derivative: how fast volatility changes. |
| VR | Volatility Ratio | Current TR relative to average TR. |
| YZV | Yang-Zhang Volatility | OHLC plus overnight gap estimator. |
