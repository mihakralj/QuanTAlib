# Oscillators

> "Oscillators tell you when to act, not which direction to trade."  Unknown

Oscillators fluctuate above and below a centerline or within bounded ranges. Useful for identifying overbought/oversold conditions, momentum shifts, and divergences. Best in ranging markets; trend-following indicators work better in trending markets.

## Indicator Status

| Indicator | Full Name | Status | Description |
| :--- | :--- | :---: | :--- |
| AC | Acceleration Oscillator | =� | Second derivative of AO. Measures acceleration of market driving force. |
| [AO](lib/oscillators/ao/ao.md) | Awesome Oscillator |  | 5-period SMA minus 34-period SMA of bar midpoint. Bill Williams creation. |
| [APO](lib/oscillators/apo/Apo.md) | Absolute Price Oscillator |  | Raw currency difference between fast and slow EMAs. Unbounded. |
| BBB | Bollinger %B | =� | Position within Bollinger Bands. 0=lower band, 1=upper band. || BBI | Bulls Bears Index | ≡ | Measures the relative strength of bulls and bears based on price action. || BBS | Bollinger Band Squeeze | =� | BB width < KC width indicates consolidation. Breakout imminent. || BOP | Balance of Power | ≡ | Measures the strength of buyers vs. sellers by relating price change to the trading range. |
| BRAR | BRAR | ≡ | Combines AR (sentiment) and BR (momentum) indicators to gauge market mood. |
| CCI | Commodity Channel Index | ≡ | Measures price deviation from its statistical mean, identifies cyclical turns. |
| COPPOCK | Coppock Curve | ≡ | Long-term momentum oscillator used primarily for identifying major market bottoms. |
| CRSI | Connors RSI | ≡ | Composite indicator combining RSI, Up/Down Streak Length, and Rate-of-Change. |
| CTI | Correlation Trend Indicator | ≡ | Measures the correlation between price and time to determine trend strength. |
| DOSC | Derivative Oscillator | ≡ | Measures the difference between a double-smoothed RSI and its signal line. || CFO | Chande Forecast Oscillator | =� | Percentage difference between price and linear regression forecast. |
| DPO | Detrended Price Oscillator | =� | Removes trend via displaced SMA. Reveals cycles. || ER | Efficiency Ratio | ≡ | Measures price efficiency by comparing net price movement to total price movement (KAMA component). |
| ERI | Elder Ray Index | ≡ | Measures buying (Bull Power) and selling (Bear Power) pressure relative to an EMA. || FISHER | Fisher Transform | =� | Converts prices to Gaussian distribution. Sharp reversals. || FOSC | Forecast Oscillator | ≡ | Plots the percentage difference between a forecast price (e.g., linear regression) and the actual price. || INERTIA | Inertia | =� | Trend strength from distance to linear regression line. |
| KDJ | KDJ Indicator | =� | Enhanced Stochastic. J = 3K - 2D provides leading signal. || KRI | Kairi Relative Index | ≡ | Measures the deviation of the current price from its simple moving average. |
| KST | KST Oscillator | ≡ | Smoothed, weighted Rate-of-Change oscillator combining multiple timeframes. || PGO | Pretty Good Oscillator | =� | Distance from SMA normalized by ATR. Units: ATR multiples. || PSL | Psychological Line | ≡ | Measures percentage of days closing up over a specified period, gauges sentiment. |
| QQE | Quantitative Qualitative Estimation | ≡ | Smoothing technique applied to RSI, providing trade signals via signal line crossovers. |
| RVGI | Relative Vigor Index | ≡ | Compares closing price to trading range. || SMI | Stochastic Momentum Index | =� | Distance from range midpoint. More sensitive than classic Stochastic. || SQUEEZE | Squeeze | ≡ | Identifies periods of low volatility (Bollinger Bands inside Keltner Channels) for potential breakouts. || STOCH | Stochastic Oscillator | =� | Close position within N-period high-low range. Classic overbought/oversold. |
| STOCHF | Stochastic Fast | =� | Unsmoothed Stochastic. Faster but noisier. |
| STOCHRSI | Stochastic RSI | =� | Stochastic applied to RSI. More sensitive than either alone. || TD_SEQ | TD Sequential | ≡ | Identifies potential price exhaustion points and reversals based on price bar counting. || TRIX | Triple Exponential Average | =� | ROC of triple EMA. Filters noise through three smoothings. |
| [ULTOSC](lib/oscillators/ultosc/ultosc.md) | Ultimate Oscillator |  | Multi-timeframe oscillator. Combines 7, 14, 28 period buying pressure. |
| WILLR | Williams %R | =� | Inverse Stochastic. -100 to 0 range. Overbought/oversold. |

**Status Key:**  Implemented | =� Planned

## Selection Guide

| Use Case | Recommended | Why |
| :--- | :--- | :--- |
| Momentum confirmation | AO, APO | AO for bar midpoint. APO for close price. Both unbounded. |
| Overbought/oversold | STOCH, WILLR, STOCHRSI | Bounded 0-100 or -100 to 0. Classic mean reversion signals. |
| Multi-timeframe analysis | ULTOSC | Combines three periods. Reduces false signals. |
| Cycle detection | DPO | Removes trend to reveal underlying cycles. |
| Leading signals | KDJ, FISHER | J-line leads K and D. Fisher provides sharp turns. |
| Noise filtering | TRIX | Triple smoothing removes most short-term noise. |
| Volatility-normalized | PGO | ATR normalization makes signals comparable across instruments. |

## Oscillator Types

| Type | Examples | Range | Best For |
| :--- | :--- | :--- | :--- |
| Bounded (0-100) | STOCH, STOCHRSI | 0 to 100 | Overbought/oversold zones |
| Bounded (-100 to 0) | WILLR | -100 to 0 | Mean reversion |
| Bounded (-1 to +1) | FISHER | - to + (practical: �3) | Sharp reversal signals |
| Unbounded | AO, APO, DPO | - to + | Trend momentum |
| Normalized | PGO, CFO | ATR or % units | Cross-market comparison |

## Divergence Analysis

Oscillator divergence signals potential reversals:

| Price Action | Oscillator Action | Signal |
| :--- | :--- | :--- |
| Higher high | Lower high | Bearish divergence. Weakening momentum. |
| Lower low | Higher low | Bullish divergence. Strengthening support. |
| Higher high | Higher high | Confirmation. Trend intact. |
| Lower low | Lower low | Confirmation. Trend intact. |

Divergences work best with bounded oscillators (STOCH, RSI, WILLR) where extremes are well-defined.