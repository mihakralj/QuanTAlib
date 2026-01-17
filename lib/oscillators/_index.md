# Oscillators

> "Oscillators tell you when to act, not which direction to trade."  Unknown

Oscillators fluctuate above and below a centerline or within bounded ranges. Useful for identifying overbought/oversold conditions, momentum shifts, and divergences. Best in ranging markets; trend-following indicators work better in trending markets.

## Indicator Status

| Indicator | Full Name | Status | Description |
| :--- | :--- | :---: | :--- |
| AC | Acceleration Oscillator | =Ë | Second derivative of AO. Measures acceleration of market driving force. |
| [AO](lib/oscillators/ao/ao.md) | Awesome Oscillator |  | 5-period SMA minus 34-period SMA of bar midpoint. Bill Williams creation. |
| [APO](lib/oscillators/apo/Apo.md) | Absolute Price Oscillator |  | Raw currency difference between fast and slow EMAs. Unbounded. |
| BBB | Bollinger %B | =Ë | Position within Bollinger Bands. 0=lower band, 1=upper band. |
| BBS | Bollinger Band Squeeze | =Ë | BB width < KC width indicates consolidation. Breakout imminent. |
| CFO | Chande Forecast Oscillator | =Ë | Percentage difference between price and linear regression forecast. |
| DPO | Detrended Price Oscillator | =Ë | Removes trend via displaced SMA. Reveals cycles. |
| FISHER | Fisher Transform | =Ë | Converts prices to Gaussian distribution. Sharp reversals. |
| INERTIA | Inertia | =Ë | Trend strength from distance to linear regression line. |
| KDJ | KDJ Indicator | =Ë | Enhanced Stochastic. J = 3K - 2D provides leading signal. |
| PGO | Pretty Good Oscillator | =Ë | Distance from SMA normalized by ATR. Units: ATR multiples. |
| SMI | Stochastic Momentum Index | =Ë | Distance from range midpoint. More sensitive than classic Stochastic. |
| STOCH | Stochastic Oscillator | =Ë | Close position within N-period high-low range. Classic overbought/oversold. |
| STOCHF | Stochastic Fast | =Ë | Unsmoothed Stochastic. Faster but noisier. |
| STOCHRSI | Stochastic RSI | =Ë | Stochastic applied to RSI. More sensitive than either alone. |
| TRIX | Triple Exponential Average | =Ë | ROC of triple EMA. Filters noise through three smoothings. |
| [ULTOSC](lib/oscillators/ultosc/ultosc.md) | Ultimate Oscillator |  | Multi-timeframe oscillator. Combines 7, 14, 28 period buying pressure. |
| WILLR | Williams %R | =Ë | Inverse Stochastic. -100 to 0 range. Overbought/oversold. |

**Status Key:**  Implemented | =Ë Planned

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
| Bounded (-1 to +1) | FISHER | - to + (practical: ±3) | Sharp reversal signals |
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