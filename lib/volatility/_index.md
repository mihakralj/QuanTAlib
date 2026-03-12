# Volatility

Volatility measures the magnitude of price changes, independent of direction. Low volatility indicates consolidation and coiling energy; high volatility indicates explosive movement and trend development. These indicators answer "how much?" and "how fast?", not "which way?".

| Indicator | Full Name | Description |
| :--- | :--- | :--- |
| [ADR](adr/Adr.md) | Average Daily Range | Simple High-Low range without gap adjustment. |
| [ATR](atr/Atr.md) | Average True Range | Standard volatility measure accounting for gaps via True Range. |
| [ATRN](atrn/Atrn.md) | ATR Normalized | ATR normalized to [0,1] based on historical min/max. |
| [BBW](bbw/Bbw.md) | Bollinger Band Width | Distance between upper and lower Bollinger Bands. |
| [BBWN](bbwn/Bbwn.md) | BB Width Normalized | BBW normalized to [0,1] range. |
| [BBWP](bbwp/Bbwp.md) | BB Width Percentile | BBW percentile rank over lookback. |
| [CCV](ccv/Ccv.md) | Close-to-Close Volatility | Annualized volatility from log returns. |
| [CV](cv/Cv.md) | Conditional Volatility | GARCH(1,1) model for time-varying volatility. |
| [CVI](cvi/Cvi.md) | Chaikin Volatility | Rate of change in smoothed High-Low range. |
| [ETHERM](etherm/Etherm.md) | Elder's Thermometer | Absolute bar range in ATR units. Identifies abnormal activity. |
| [EWMA](ewma/Ewma.md) | EWMA Volatility | Exponentially weighted squared returns with bias correction. |
| [GKV](gkv/Gkv.md) | Garman-Klass Volatility | Efficient OHLC-based estimator with RMA smoothing. |
| [HLV](hlv/Hlv.md) | High-Low Volatility (Parkinson) | Range-based volatility using only high-low prices. |
| [HV](hv/Hv.md) | Historical Volatility (Close-to-Close) | Standard deviation of log returns with rolling window. |
| [JVOLTY](jvolty/Jvolty.md) | Jurik Volatility | Adaptive volatility from JMA with 128-bar trimmed mean distribution. |
| [JVOLTYN](jvoltyn/Jvoltyn.md) | Jurik Volatility Normalized | JVOLTY normalized to [0,100] scale. |
| [MASSI](massi/Massi.md) | Mass Index | Range expansion/contraction for reversal detection. |
| [NATR](natr/Natr.md) | Normalized ATR | ATR as percentage of close price. Also known as ATRP. |
| [RSV](rsv/Rsv.md) | Rogers-Satchell Volatility | OHLC estimator with drift adjustment. |
| [RV](rv/Rv.md) | Realized Volatility | High-frequency intraday volatility. |
| [RVI](rvi/Rvi.md) | Relative Volatility Index | Directional volatility measure. |
| [TR](tr/Tr.md) | True Range | Single-bar volatility with gap capture. |
| [UI](ui/Ui.md) | Ulcer Index | Downside risk and drawdown depth/duration. |
| [VOV](vov/Vov.md) | Volatility of Volatility | Second derivative: how fast volatility changes. |
| [VR](vr/Vr.md) | Volatility Ratio | Current TR relative to average TR. |
| [YZV](yzv/Yzv.md) | Yang-Zhang Volatility | OHLC plus overnight gap estimator. |
