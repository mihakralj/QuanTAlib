# Oscillators

> "Oscillators tell you when to act, not which direction to trade."  Unknown

Oscillators fluctuate above and below a centerline or within bounded ranges. Useful for identifying overbought/oversold conditions, momentum shifts, and divergences. Best in ranging markets; trend-following indicators work better in trending markets.

| Indicator | Full Name | Description |
| :--- | :--- | :--- |
| AC | Acceleration Oscillator | Second derivative of AO. Measures acceleration of market driving force. |
| [AO](lib/oscillators/ao/Ao.md) | Awesome Oscillator | 5-period SMA minus 34-period SMA of bar midpoint. Bill Williams creation. |
| APO | Absolute Price Oscillator | Raw currency difference between fast and slow EMAs. Unbounded. |
| BBB | Bollinger %B | Position within Bollinger Bands. 0=lower band, 1=upper band. |
| BBS | Bollinger Band Squeeze | BB width < KC width indicates consolidation. Breakout imminent. |
| CFO | Chande Forecast Oscillator | Percentage difference between price and linear regression forecast. |
| DPO | Detrended Price Oscillator | Removes trend via displaced SMA. Reveals cycles. |
| FISHER | Fisher Transform | Converts prices to Gaussian distribution. Sharp reversals. |
| INERTIA | Inertia | Trend strength from distance to linear regression line. |
| KDJ | KDJ Indicator | Enhanced Stochastic. J = 3K - 2D provides leading signal. |
| PGO | Pretty Good Oscillator | Distance from SMA normalized by ATR. Units: ATR multiples. |
| SMI | Stochastic Momentum Index | Distance from range midpoint. More sensitive than classic Stochastic. |
| STOCH | Stochastic Oscillator | Close position within N-period high-low range. Classic overbought/oversold. |
| STOCHF | Stochastic Fast | Unsmoothed Stochastic. Faster but noisier. |
| STOCHRSI | Stochastic RSI | Stochastic applied to RSI. More sensitive than either alone. |
| TRIX | Triple Exponential Average | ROC of triple EMA. Filters noise through three smoothings. |
| [ULTOSC](lib/oscillators/ultosc/Ultosc.md) | Ultimate Oscillator | Multi-timeframe oscillator. Combines 7, 14, 28 period buying pressure. |
| WILLR | Williams %R | Inverse Stochastic. -100 to 0 range. Overbought/oversold. |
| BBI | Bulls Bears Index | Measures relative strength of bulls and bears based on price action. |
| BOP | Balance of Power | Measures strength of buyers vs. sellers by relating price change to trading range. |
| BRAR | BRAR | Combines AR (sentiment) and BR (momentum) indicators to gauge market mood. |
| CCI | Commodity Channel Index | Measures price deviation from statistical mean, identifies cyclical turns. |
| COPPOCK | Coppock Curve | Long-term momentum oscillator for identifying major market bottoms. |
| CRSI | Connors RSI | Composite indicator combining RSI, Up/Down Streak Length, and Rate-of-Change. |
| CTI | Correlation Trend Indicator | Measures correlation between price and time to determine trend strength. |
| DOSC | Derivative Oscillator | Measures difference between double-smoothed RSI and its signal line. |
| ER | Efficiency Ratio | Measures price efficiency by comparing net to total price movement. |
| ERI | Elder Ray Index | Measures buying (Bull Power) and selling (Bear Power) pressure relative to EMA. |
| FOSC | Forecast Oscillator | Percentage difference between forecast price and actual price. |
| KRI | Kairi Relative Index | Measures deviation of current price from its simple moving average. |
| KST | KST Oscillator | Smoothed, weighted Rate-of-Change combining multiple timeframes. |
| PSL | Psychological Line | Percentage of days closing up over period, gauges sentiment. |
| QQE | Quantitative Qualitative Estimation | Smoothing technique applied to RSI with signal line crossovers. |
| RVGI | Relative Vigor Index | Compares closing price to trading range. |
| SQUEEZE | Squeeze | Identifies low volatility when BB inside KC for potential breakouts. |
| TD_SEQ | TD Sequential | Identifies price exhaustion points and reversals via bar counting. |
