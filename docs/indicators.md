# Indicator Catalog

QuanTAlib provides technical indicators organized into mathematical families. Understanding these families helps choose the right tool for the analytical problem you're actually solving.

## Full Category Table

| Category | What It Measures | Representative Indicators | When You Need It |
| -------- | ---------------- | ------------------------- | ---------------- |
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

## Implemented Indicators

### Momentum

- [**ADX**](../lib/momentum/adx/Adx.md) - Average Directional Index
- [**ADXR**](../lib/momentum/adxr/Adxr.md) - Average Directional Movement Rating
- [**AO**](../lib/momentum/ao/Ao.md) - Awesome Oscillator
- [**AROON**](../lib/momentum/aroon/Aroon.md) - Aroon
- [**AROONOSC**](../lib/momentum/aroonosc/AroonOsc.md) - Aroon Oscillator
- [**CFB**](../lib/momentum/cfb/Cfb.md) - Jurik Composite Fractal Behavior
- [**DMX**](../lib/momentum/dmx/Dmx.md) - Jurik Directional Movement Index
- [**RSX**](../lib/momentum/rsx/Rsx.md) - Jurik Relative Strength Quality Index
- [**VEL**](../lib/momentum/vel/Vel.md) - Jurik Velocity

### Trends

- [**ALMA**](../lib/trends/alma/Alma.md) - Arnaud Legoux MA
- [**BESSEL**](../lib/trends/bessel/Bessel.md) - Bessel Filter
- [**BILATERAL**](../lib/trends/bilateral/Bilateral.md) - Bilateral Filter
- [**BLMA**](../lib/trends/blma/Blma.md) - Blackman Window MA
- [**CONV**](../lib/trends/conv/Conv.md) - Convolution MA
- [**DEMA**](../lib/trends/dema/Dema.md) - Double Exponential MA
- [**DWMA**](../lib/trends/dwma/Dwma.md) - Double Weighted MA
- [**EMA**](../lib/trends/ema/Ema.md) - Exponential MA
- [**HMA**](../lib/trends/hma/Hma.md) - Hull MA
- [**HTIT**](../lib/trends/htit/Htit.md) - Hilbert Transform Instantaneous Trend
- [**JMA**](../lib/trends/jma/Jma.md) - Jurik MA
- [**KAMA**](../lib/trends/kama/Kama.md) - Kaufman Adaptive MA
- [**LSMA**](../lib/trends/lsma/Lsma.md) - Least Squares MA
- [**MAMA**](../lib/trends/mama/Mama.md) - MESA Adaptive MA
- [**MGDI**](../lib/trends/mgdi/Mgdi.md) - McGinley Dynamic
- [**PWMA**](../lib/trends/pwma/Pwma.md) - Pascal Weighted MA
- [**RMA**](../lib/trends/rma/Rma.md) - wildeR MA
- [**SMA**](../lib/trends/sma/Sma.md) - Simple MA
- [**SSF**](../lib/trends/ssf/Ssf.md) - Ehlers Super Smooth Filter
- [**SUPER**](../lib/trends/super/Super.md) - SuperTrend
- [**T3**](../lib/trends/t3/T3.md) - Tillson T3 MA
- [**TEMA**](../lib/trends/tema/Tema.md) - Triple Exponential MA
- [**TRIMA**](../lib/trends/trima/Trima.md) - Triangular MA
- [**USF**](../lib/trends/usf/Usf.md) - Ehlers Ultimate Smoother Filter
- [**VIDYA**](../lib/trends/vidya/Vidya.md) - Variable Index Dynamic Average
- [**WMA**](../lib/trends/wma/Wma.md) - Weighted MA

### Volatility

- [**ATR**](../lib/volatility/atr/Atr.md) - Average True Range

### Volume

- [**ADL**](../lib/volume/adl/Adl.md) - Accumulation/Distribution Line
- [**ADOSC**](../lib/volume/adosc/Adosc.md) - Chaikin A/D Oscillator
