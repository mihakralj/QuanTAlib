# Indicator Catalog

QuanTAlib provides technical indicators organized into mathematical families. Understanding these families helps choose the right tool for the analytical problem you're actually solving.

## Full Category Table

| Category | What It Measures | Representative Indicators | When You Need It |
| -------- | ---------------- | ------------------------- | ---------------- |
| [**Trends (FIR)**](../lib/trends_FIR/_index.md) | Trend direction using finite impulse response filters | SMA, WMA, ALMA, HMA, LSMA | Trend identification with predictable lag characteristics and finite memory. |
| [**Trends (IIR)**](../lib/trends_IIR/_index.md) | Trend direction using infinite impulse response filters | EMA, DEMA, TEMA, JMA, KAMA, MAMA | Trend identification with efficient recursive calculation and infinite memory. |
| [**Filters**](../lib/filters/_index.md) | Signal processing filters for noise reduction | Bessel, Butterworth, Super Smoother | Removing noise while preserving signal (trend) structure. |
| [**Oscillators**](../lib/oscillators/_index.md) | Cyclical movement around a baseline | RSI, MACD, AO, UltOsc | Identifying overbought/oversold conditions and potential reversals. |
| [**Dynamics**](../lib/dynamics/_index.md) | Trend strength and structural changes | ADX, Aroon, SuperTrend, Chop | Determining market regime (trending vs ranging) and trend strength. |
| [**Momentum**](../lib/momentum/_index.md) | Speed and magnitude of price changes | Momentum, ROC, Velocity | Measuring the rate of acceleration or deceleration in price. |
| [**Volatility**](../lib/volatility/_index.md) | Size and variability of price movements | ATR, StdDev, Bollinger Bands | Position sizing, stop-loss placement, and understanding market regime. |
| [**Volume**](../lib/volume/_index.md) | Trading activity and price-volume relationships | OBV, VWAP, A/D | Confirming price movements with volume participation. |
| [**Channels**](../lib/channels/_index.md) | Price boundaries and range definitions | Donchian Channels, Keltner Channels | Breakout strategies and range-bound trading. |
| [**Statistics**](../lib/statistics/_index.md) | Mathematical relationships between price series | Correlation, Covariance, Beta, Z-Score | Portfolio analysis, pairs trading, and statistical arbitrage. |
| [**Numerics**](../lib/numerics/_index.md) | Mathematical transformations and signal processing | Convolution, Filters, Integration, Differentiation | Custom indicator development and advanced signal processing. |
| [**Errors**](../lib/errors/_index.md) | Measurement accuracy and model fit quality | MAE, RMSE, Residuals, R-Squared | Model validation and forecast quality assessment. |
| [**Forecasts**](../lib/forecasts/_index.md) | Future price prediction and projection | Prediction based on models | Predictive modeling/projecting price based on historical patterns. |
| [**Cycles**](../lib/cycles/_index.md) | Periodic patterns and dominant frequencies | Hilbert Transform, Dominant Cycle | Identifying and trading cyclical market behavior. |

## When to Use Each Category

The categories help you understand what analytical problem each indicator solves.

* **New to TA?** Start with **Trends (FIR/IIR)**, **Volatility**, and **Oscillators**. These provide the foundation most traders need.
* **Building a Strategy?** Use **Statistics** for pairs trading, **Volume** for confirmation, and **Channels** for breakouts.
* **Advanced Quant?** **Filters**, **Dynamics**, **Numerics**, and **Errors** provide tools for custom signal processing and model validation.

## Implemented Indicators

### Trends (FIR)

* [**ALMA**](../lib/trends_FIR/alma/Alma.md) - Arnaud Legoux MA
* [**BLMA**](../lib/trends_FIR/blma/Blma.md) - Blackman Window MA
* [**CONV**](../lib/trends_FIR/conv/Conv.md) - Convolution MA
* [**DWMA**](../lib/trends_FIR/dwma/Dwma.md) - Double Weighted MA
* [**HMA**](../lib/trends_FIR/hma/Hma.md) - Hull MA
* [**LSMA**](../lib/trends_FIR/lsma/Lsma.md) - Least Squares MA
* [**PWMA**](../lib/trends_FIR/pwma/Pwma.md) - Pascal Weighted MA
* [**SMA**](../lib/trends_FIR/sma/Sma.md) - Simple MA
* [**TRIMA**](../lib/trends_FIR/trima/Trima.md) - Triangular MA
* [**WMA**](../lib/trends_FIR/wma/Wma.md) - Weighted MA

### Trends (IIR)

* [**DEMA**](../lib/trends_IIR/dema/Dema.md) - Double Exponential MA
* [**DSMA**](../lib/trends_IIR/dsma/Dsma.md) - Deviation-Scaled MA
* [**EMA**](../lib/trends_IIR/ema/Ema.md) - Exponential MA
* [**FRAMA**](../lib/trends_IIR/frama/Frama.md) - Fractal Adaptive MA
* [**HEMA**](../lib/trends_IIR/hema/Hema.md) - Hull Exponential MA
* [**HTIT**](../lib/trends_IIR/htit/Htit.md) - Hilbert Transform Instantaneous Trend
* [**JMA**](../lib/trends_IIR/jma/Jma.md) - Jurik MA
* [**KAMA**](../lib/trends_IIR/kama/Kama.md) - Kaufman Adaptive MA
* [**MAMA**](../lib/trends_IIR/mama/Mama.md) - MESA Adaptive MA
* [**MMA**](../lib/trends_IIR/mma/Mma.md) - Modified MA
* [**MGDI**](../lib/trends_IIR/mgdi/Mgdi.md) - McGinley Dynamic
* [**QEMA**](../lib/trends_IIR/qema/Qema.md) - Quad Exponential MA
* [**RMA**](../lib/trends_IIR/rma/Rma.md) - wildeR MA
* [**T3**](../lib/trends_IIR/t3/T3.md) - Tillson T3 MA
* [**TEMA**](../lib/trends_IIR/tema/Tema.md) - Triple Exponential MA
* [**VIDYA**](../lib/trends_IIR/vidya/Vidya.md) - Variable Index Dynamic Average
* [**ZLEMA**](../lib/trends_IIR/zlema/Zlema.md) - Zero-Lag Exponential MA

### Filters

* [**BESSEL**](../lib/filters/bessel/Bessel.md) - Bessel Filter
* [**BILATERAL**](../lib/filters/bilateral/Bilateral.md) - Bilateral Filter
* [**BPF**](../lib/filters/bpf/Bpf.md) - BandPass Filter
* [**BUTTER**](../lib/filters/butter/Butter.md) - Butterworth Filter
* [**CHEBY1**](../lib/filters/cheby1/Cheby1.md) - Chebyshev Type I Filter
* [**SSF**](../lib/filters/ssf/Ssf.md) - Ehlers Super Smooth Filter
* [**USF**](../lib/filters/usf/Usf.md) - Ehlers Ultimate Smoother Filter

### Oscillators

* [**AO**](../lib/oscillators/ao/Ao.md) - Awesome Oscillator
* [**APO**](../lib/oscillators/apo/Apo.md) - Absolute Price Oscillator
* [**MACD**](../lib/momentum/macd/Macd.md) - Moving Average Convergence Divergence
* [**RSI**](../lib/momentum/rsi/Rsi.md) - Relative Strength Index
* [**ULTOSC**](../lib/oscillators/ultosc/Ultosc.md) - Ultimate Oscillator

*(Note: MACD and RSI are currently still located in the momentum folder in source but functionally are oscillators)*

### Dynamics

* [**ADX**](../lib/dynamics/adx/Adx.md) - Average Directional Index
* [**ADXR**](../lib/dynamics/adxr/Adxr.md) - Average Directional Movement Rating
* [**AMAT**](../lib/dynamics/amat/Amat.md) - Archer Moving Averages Trends
* [**AROON**](../lib/dynamics/aroon/Aroon.md) - Aroon
* [**AROONOSC**](../lib/dynamics/aroonosc/AroonOsc.md) - Aroon Oscillator
* [**DMX**](../lib/dynamics/dmx/Dmx.md) - Jurik Directional Movement Index
* [**SUPER**](../lib/dynamics/super/Super.md) - SuperTrend

### Momentum

* [**BOP**](../lib/momentum/bop/Bop.md) - Balance of Power
* [**CFB**](../lib/momentum/cfb/Cfb.md) - Jurik Composite Fractal Behavior
* [**RSX**](../lib/momentum/rsx/Rsx.md) - Jurik Relative Strength Quality Index
* [**VEL**](../lib/momentum/vel/Vel.md) - Jurik Velocity

### Volatility

* [**ATR**](../lib/volatility/atr/Atr.md) - Average True Range

### Volume

* [**ADL**](../lib/volume/adl/Adl.md) - Accumulation/Distribution Line
* [**ADOSC**](../lib/volume/adosc/Adosc.md) - Chaikin A/D Oscillator

### Channels

* [**ABBER**](../lib/channels/abber/abber.md) - Aberration Bands
* [**ACCBANDS**](../lib/channels/accbands/accbands.md) - Acceleration Bands

### Statistics

* [**CMA**](../lib/statistics/cma/Cma.md) - Cumulative Moving Average
* [**COVARIANCE**](../lib/statistics/covariance/Covariance.md) - Covariance
* [**LINREG**](../lib/statistics/linreg/LinReg.md) - Linear Regression Curve
* [**MEDIAN**](../lib/statistics/median/Median.md) - Rolling Median
* [**SKEW**](../lib/statistics/skew/Skew.md) - Skewness
* [**STDDEV**](../lib/statistics/stddev/StdDev.md) - Standard Deviation
* [**SUM**](../lib/statistics/sum/Sum.md) - Rolling Sum
* [**VARIANCE**](../lib/statistics/variance/Variance.md) - Population and Sample Variance

### Forecasts

* [**AFIRMA**](../lib/forecasts/afirma/Afirma.md) - Adaptive FIR Moving Average
