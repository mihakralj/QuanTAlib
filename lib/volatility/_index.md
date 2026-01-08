# Volatility

> "The market is a pendulum that swings between unsustainable optimism and unjustified pessimism." — Benjamin Graham

Volatility is the pulse of the market. It measures the rate and magnitude of price changes, regardless of direction. In low volatility, markets consolidate and coil; in high volatility, they explode and trend.

These indicators don't tell you where the price is going. They tell you how scared or greedy the participants are while it gets there.

| Indicator | Full Name | Description |
| :--- | :--- | :--- |
| ADR | Average Daily Range | Measures the average daily price movement range over a specified period. |
| [ATR](atr/Atr.md) | Average True Range | The standard for measuring market "heat." Decomposes range to account for gaps. |
| ATRN | Average True Range Normalized [0,1] | ATR normalized to a 0-1 scale for comparative analysis. |
| ATRP | Average True Range Percent | ATR expressed as a percentage of the closing price. |
| BBW | Bollinger Band Width | Measures the difference between the upper and lower Bollinger Bands. |
| BBWN | Bollinger Band Width Normalized | Measures the current Bollinger Band Width relative to its historical range in [0.0,1.0] normalized space. |
| BBWP | Bollinger Band Width Percentile | Measures the current Bollinger Band Width relative to its historical range. |
| CCV | Close-to-Close Volatility | Measures annualized volatility using log returns of closing prices. |
| CV | Conditional Volatility | Implements GARCH(1,1) model to capture time-varying volatility. |
| CVI | Chaikin's Volatility | Measures volatility as the rate of change in smoothed high-low range using EMA and ROC. |
| EWMA | Exponential Weighted MA Volatility | Volatility calculated using an exponentially weighted moving average of squared returns. |
| GKV | Garman-Klass Volatility | Volatility estimator using high, low, open, and close prices for improved efficiency. |
| HLV | High-Low Volatility | Volatility measure based solely on the range between high and low prices. |
| HV | Historical Volatility | Standard deviation of price returns over a historical period. |
| JVOLTY | Jurik Volatility | Low-lag, smooth volatility measure developed by Mark Jurik. |
| JVOLTYN | Jurik Volatility Normalized [0,1] | Jurik Volatility normalized to a 0-1 scale. |
| MASSI | Mass Index | Predicts trend reversals by analyzing the narrowing and widening of price ranges. |
| NATR | Normalized Average True Range | ATR expressed as a percentage of close price for cross-market comparison. |
| PV | Parkinson Volatility | Volatility estimator using high and low prices, assuming no drift. |
| RSV | Rogers-Satchell Volatility | Volatility estimator incorporating high, low, open, and close prices. |
| RV | Realized Volatility | Volatility calculated from high-frequency intra-day data. |
| RVI | Relative Volatility Index | Measures the direction of volatility based on standard deviations of price changes. |
| TR | True Range | Single-bar volatility measurement capturing gaps between sessions. |
| UI | Ulcer Index | Measures downside risk and depth/duration of price drawdowns. |
| VOV | Volatility of Volatility | Measures the rate of change in volatility itself. |
| VR | Volatility Ratio | Compares the current true range to the average true range over a longer period. |
| YZV | Yang-Zhang Volatility | Volatility estimator combining open, high, low, close, and overnight gaps. |
