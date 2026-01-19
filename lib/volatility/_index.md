# Volatility Indicators

> "Volatility is the price of admission. The question is whether the ride is worth it."

Volatility measures the magnitude of price changes, independent of direction. Low volatility indicates consolidation and coiling energy; high volatility indicates explosive movement and trend development. These indicators answer "how much?" and "how fast?", not "which way?".

Core volatility concepts:

- **Range-Based**: High minus Low, with or without gap adjustment (TR, ATR)
- **Return-Based**: Standard deviation of log returns (HV, EWMA)
- **Estimator-Based**: Statistical models using OHLC combinations (Garman-Klass, Yang-Zhang)
- **Normalized**: Percentage or [0,1] scaled for cross-asset comparison (ATRP, ATRN)

## Implementation Status

| Indicator | Full Name | Status | Description |
| :--- | :--- | :---: | :--- |
| [ADR](adr/Adr.md) | Average Daily Range | ✅ | Simple High-Low range without gap adjustment |
| [ATR](atr/Atr.md) | Average True Range | ✅ | Standard volatility measure accounting for gaps via True Range |
| [ATRN](atrn/Atrn.md) | ATR Normalized | ✅ | ATR normalized to [0,1] based on historical min/max |
| [ATRP](atrp/Atrp.md) | ATR Percent | ✅ | ATR as percentage of close price |
| BBW | Bollinger Band Width | 📋 | Distance between upper and lower Bollinger Bands |
| BBWN | BB Width Normalized | 📋 | BBW normalized to [0,1] range |
| BBWP | BB Width Percentile | 📋 | BBW percentile rank over lookback |
| CCV | Close-to-Close Volatility | 📋 | Annualized volatility from log returns |
| CV | Conditional Volatility | 📋 | GARCH(1,1) model for time-varying volatility |
| CVI | Chaikin Volatility | 📋 | Rate of change in smoothed High-Low range |
| EWMA | EWMA Volatility | 📋 | Exponentially weighted squared returns |
| GKV | Garman-Klass Volatility | 📋 | Efficient OHLC-based estimator |
| HLV | High-Low Volatility | 📋 | Range-based volatility without close |
| HV | Historical Volatility | 📋 | Standard deviation of returns |
| JVOLTY | Jurik Volatility | 📋 | Low-lag, smooth Jurik volatility |
| JVOLTYN | Jurik Volatility Normalized | 📋 | JVOLTY normalized to [0,1] |
| MASSI | Mass Index | 📋 | Range expansion/contraction for reversal detection |
| NATR | Normalized ATR | 📋 | ATR as percentage (equivalent to ATRP) |
| PV | Parkinson Volatility | 📋 | High-Low estimator assuming no drift |
| RSV | Rogers-Satchell Volatility | 📋 | OHLC estimator with drift adjustment |
| RV | Realized Volatility | 📋 | High-frequency intraday volatility |
| RVI | Relative Volatility Index | 📋 | Directional volatility measure |
| TR | True Range | 📋 | Single-bar volatility with gap capture |
| UI | Ulcer Index | 📋 | Downside risk and drawdown depth/duration |
| VOV | Volatility of Volatility | 📋 | Second derivative: how fast volatility changes |
| VR | Volatility Ratio | 📋 | Current TR relative to average TR |
| YZV | Yang-Zhang Volatility | 📋 | OHLC plus overnight gap estimator |

**Legend**: ✅ Implemented | 📋 Planned

## Indicator Selection Guide

| Use Case | Recommended | Rationale |
| :--- | :--- | :--- |
| Position Sizing | ATR, ATRP | Standard for risk-based sizing |
| Stop Loss Distance | ATR | Absolute measure in price units |
| Cross-Asset Comparison | ATRP, ATRN | Normalized for different price scales |
| Regime Detection | ATRN | [0,1] scale with clear thresholds |
| Intraday Analysis | ADR | Gaps irrelevant for same-session |
| Gap-Sensitive Analysis | ATR | True Range captures overnight gaps |

## Volatility Regime Interpretation

| ATRN Range | ATRP Typical | Regime | Implications |
| :---: | :---: | :--- | :--- |
| 0.8 - 1.0 | > 5% | Crisis/Extreme | Widen stops, reduce size, expect whipsaws |
| 0.5 - 0.8 | 2-5% | Elevated | Trending conditions, standard trend-following |
| 0.2 - 0.5 | 1-2% | Normal | Balanced conditions, mixed strategies |
| 0.0 - 0.2 | < 1% | Compressed | Consolidation, mean-reversion, breakout setups |

## ATR Family Comparison

| Indicator | Output | Use Case |
| :--- | :--- | :--- |
| ATR | Absolute price units | Stop distance, position sizing in same asset |
| ATRP | Percentage (0-100%) | Cross-asset comparison, percentage-based sizing |
| ATRN | Normalized [0,1] | Regime detection, volatility ranking |
| ADR | Absolute price units | Intraday analysis, gap-insensitive |