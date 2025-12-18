# Indicator Catalog

QuanTAlib provides technical indicators organized into mathematical families. Understanding these families helps choose the right tool for the analytical problem you're actually solving.

## Full Category Table

| Category | What It Measures | Representative Indicators | When You Need It |
|----------|------------------|---------------------------|------------------|
| [**Trends**](../lib/trends/_index.md) | Direction and strength of price movement through smoothing and filtering | SMA, EMA, WMA, HMA, JMA, KAMA, ALMA, DEMA, TEMA, T3 | Starting point for most analysis. Simpler variants (SMA, EMA) work for trend identification. Exotic ones (Jurik, Ehlers) trade CPU cycles for reduced lag. |
| [**Volatility**](../lib/volatility/_index.md) | Size and variability of price movements | ATR, StdDev, Bollinger Bands, Keltner Channels, Historical Volatility | Position sizing, stop-loss placement, and understanding market regime. ATR tells you how much instruments typically move. |
| [**Momentum**](../lib/momentum/_index.md) | Speed and magnitude of price changes | RSI, Stochastic, CCI, Williams %R, MACD, Momentum, ROC | Identifying overbought/oversold conditions and divergences. RSI oscillates between 0-100 by construction. |
| [**Volume**](../lib/volume/_index.md) | Trading activity and price-volume relationships | OBV, VWAP, Volume ROC, A/D, MFI | Confirming price movements with volume participation. VWAP shows where institutional traders executed. |
| [**Channels**](../lib/channels/_index.md) | Price boundaries and range definitions | Donchian Channels, Keltner Channels, Price Channels | Breakout strategies and range-bound trading. Donchian Channels mark highest high and lowest low. |
| [**Statistics**](../lib/statistics/_index.md) | Mathematical relationships between price series | Correlation, Covariance, Beta, Z-Score, Linear Regression | Portfolio analysis, pairs trading, and statistical arbitrage. Correlation measures how two instruments move together. |
| [**Numerics**](../lib/numerics/_index.md) | Mathematical transformations and signal processing | Convolution, Filters, Integration, Differentiation, Smoothing | Custom indicator development and advanced signal processing. Toolkit for building indicators rather than using them. |
| [**Errors**](../lib/errors/_index.md) | Measurement accuracy and model fit quality | MAE, RMSE, Residuals, R-Squared | Model validation and forecast quality assessment. Critical for anyone building quantitative strategies. |
| [**Forecasts**](../lib/forecasts/_index.md) | Future price prediction and projection | Linear Regression Forecast, Moving Average Projection | Predictive modeling. Projects price based on historical patterns. |
| [**Cycles**](../lib/cycles/_index.md) | Periodic patterns and dominant frequencies | Hilbert Transform, Dominant Cycle, Instantaneous Phase, Sine Wave | Identifying and trading cyclical market behavior. Works beautifully when markets are cyclical. |

## When to Use Each Category

The categories aren't rigid boundaries—many indicators could fit multiple categories. KAMA is both a trend indicator and uses momentum calculations. Keltner Channels combine trends (moving average centerline) with volatility (ATR bands). The organization helps you understand what analytical problem each indicator solves rather than memorizing which arbitrary category someone assigned it to.

- **New to TA?** Start with **Trends**, **Volatility**, and **Momentum**. These provide the foundation most traders need.
- **Building a Strategy?** Use **Statistics** for pairs trading, **Volume** for confirmation, and **Channels** for breakouts.
- **Advanced Quant?** **Numerics**, **Errors**, and **Cycles** provide the raw mathematical tools for custom signal processing and model validation.

## Mathematical Families Explanation

### Moving Averages (Trends)
Moving averages are low-pass filters. They remove high-frequency noise (random price fluctuations) to reveal the underlying low-frequency signal (trend).
- **SMA**: Equal weight to all points. Slowest to react.
- **EMA/WMA**: More weight to recent data. Faster reaction.
- **HMA/JMA/ALMA**: Advanced math to reduce lag while maintaining smoothness.

### Oscillators (Momentum)
Oscillators measure the velocity of price changes. They are typically bounded (e.g., 0-100) or centered around zero.
- **RSI**: Ratio of average gains to average losses.
- **MACD**: Difference between two moving averages (convergence/divergence).

### Dispersion (Volatility)
These measure the spread of data points around the mean.
- **StdDev**: Standard statistical measure of variance.
- **ATR**: Volatility measure that accounts for gaps (high-low range).
