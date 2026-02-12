# Indicator Catalog

QuanTAlib provides technical indicators organized into mathematical families. Understanding these families helps choose the right tool for the analytical problem at hand. Selecting an indicator without understanding its category leads to confusion at best, losses at worst.

## Category Reference

| Category | What It Measures | Representative Indicators | When to Reach for It |
| :------- | :--------------- | :------------------------ | :------------------- |
| [**Trends (FIR)**](../lib/trends_FIR/_index.md) | Trend direction via finite impulse response filters | SMA, WMA, ALMA, HMA, LSMA | Trend identification with predictable lag and finite memory. Output depends only on a fixed window of past prices. |
| [**Trends (IIR)**](../lib/trends_IIR/_index.md) | Trend direction via infinite impulse response filters | EMA, DEMA, TEMA, JMA, KAMA, MAMA | Trend identification with recursive calculation and theoretically infinite memory. More responsive per unit of smoothness. |
| [**Filters**](../lib/filters/_index.md) | Signal processing filters for noise reduction | Bessel, Butterworth, Super Smoother | Removing noise while preserving trend structure. Designed by engineers, borrowed by traders. |
| [**Oscillators**](../lib/oscillators/_index.md) | Cyclical movement around a baseline | RSI, MACD, AO, UltOsc | Identifying overbought/oversold conditions and potential reversals. Bounded indicators that oscillate. |
| [**Dynamics**](../lib/dynamics/_index.md) | Trend strength and structural changes | ADX, Aroon, SuperTrend, Chop | Determining market regime (trending vs ranging) and measuring trend conviction. |
| [**Momentum**](../lib/momentum/_index.md) | Speed and magnitude of price changes | Momentum, ROC, Velocity | Measuring acceleration or deceleration in price. First derivative territory. |
| [**Volatility**](../lib/volatility/_index.md) | Size and variability of price movements | ATR, StdDev, Bollinger Bands | Position sizing, stop-loss placement, regime identification. How much prices move matters as much as direction. |
| [**Volume**](../lib/volume/_index.md) | Trading activity and price-volume relationships | OBV, VWAP, A/D | Confirming price movements with participation. Volume validates or contradicts price action. |
| [**Channels**](../lib/channels/_index.md) | Price boundaries and range definitions | Donchian, Keltner, Bollinger | Breakout strategies and range-bound trading. Defining "normal" so abnormal becomes visible. |
| [**Statistics**](../lib/statistics/_index.md) | Mathematical relationships between price series | Correlation, Covariance, Beta, Z-Score | Portfolio analysis, pairs trading, statistical arbitrage. Quantitative analysis beyond single instruments. |
| [**Numerics**](../lib/numerics/_index.md) | Mathematical transformations and signal processing | Convolution, Integration, Differentiation | Custom indicator development and advanced signal processing. Building blocks for novel indicators. |
| [**Errors**](../lib/errors/_index.md) | Measurement accuracy and model fit quality | MAE, RMSE, Residuals, R² | Model validation and forecast assessment. Quantifying wrongness before production quantifies losses. |
| [**Forecasts**](../lib/forecasts/_index.md) | Future price prediction and projection | Linear regression extrapolation, adaptive prediction | Projecting price based on historical patterns. Predictions that invite humility. |
| [**Cycles**](../lib/cycles/_index.md) | Periodic patterns and dominant frequencies | Hilbert Transform, Dominant Cycle | Identifying cyclical market behavior. Markets exhibit cycles; detecting them reliably remains hard. |

## Selection by Experience Level

**Beginning technical analysis?** Start with **Trends (FIR/IIR)**, **Volatility**, and **Oscillators**. SMA teaches moving average fundamentals. ATR teaches volatility measurement. RSI teaches bounded oscillators. These provide foundation for everything else.

**Building a trading strategy?** Add **Volume** for confirmation, **Channels** for breakouts, **Dynamics** for regime detection. Volume validates price moves. Channels define breakout boundaries. ADX distinguishes trending from ranging markets.

**Quantitative development?** **Filters**, **Statistics**, **Numerics**, and **Errors** provide tools for signal processing, model validation, and custom indicator construction. Butterworth filters for noise reduction. Correlation for pairs trading. Error metrics for model selection.

## Implemented Indicators

### Trends (FIR)

Finite Impulse Response filters. Output depends only on a fixed window of inputs. Always stable. Predictable lag characteristics.

| Indicator | Full Name | Notes |
| :-------- | :-------- | :---- |
| [**ALMA**](../lib/trends_FIR/alma/Alma.md) | Arnaud Legoux MA | Gaussian-weighted with offset parameter |
| [**BLMA**](../lib/trends_FIR/blma/Blma.md) | Blackman Window MA | Spectral leakage reduction |
| [**BWMA**](../lib/trends_FIR/bwma/Bwma.md) | Bessel-Weighted MA | Linear phase response |
| [**CONV**](../lib/trends_FIR/conv/Conv.md) | Convolution MA | Arbitrary kernel support |
| [**DWMA**](../lib/trends_FIR/dwma/Dwma.md) | Double Weighted MA | WMA applied twice |
| [**GWMA**](../lib/trends_FIR/gwma/Gwma.md) | Gaussian Weighted MA | Normal distribution weights |
| [**HAMMA**](../lib/trends_FIR/hamma/Hamma.md) | Hamming Weighted MA | Spectral analysis window |
| [**HANMA**](../lib/trends_FIR/hanma/Hanma.md) | Hanning Weighted MA | Cosine-based window |
| [**HMA**](../lib/trends_FIR/hma/Hma.md) | Hull MA | Reduced lag via WMA differencing |
| [**HWMA**](../lib/trends_FIR/hwma/Hwma.md) | Holt-Winters MA | Triple exponential smoothing |
| [**LSMA**](../lib/trends_FIR/lsma/Lsma.md) | Least Squares MA | Linear regression endpoint |
| [**PWMA**](../lib/trends_FIR/pwma/Pwma.md) | Pascal Weighted MA | Binomial coefficient weights |
| [**SGMA**](../lib/trends_FIR/sgma/Sgma.md) | Savitzky-Golay MA | Polynomial smoothing |
| [**SINEMA**](../lib/trends_FIR/sinema/Sinema.md) | Sine-Weighted MA | Sinusoidal weight distribution |
| [**SMA**](../lib/trends_FIR/sma/Sma.md) | Simple MA | Equal weights, the baseline |
| [**TRIMA**](../lib/trends_FIR/trima/Trima.md) | Triangular MA | Double-smoothed SMA |
| [**WMA**](../lib/trends_FIR/wma/Wma.md) | Weighted MA | Linear weight decay |

### Trends (IIR)

Infinite Impulse Response filters. Output depends on current input and past outputs. Recursive structure. More responsive but requires stability analysis.

| Indicator | Full Name | Notes |
| :-------- | :-------- | :---- |
| [**DEMA**](../lib/trends_IIR/dema/Dema.md) | Double Exponential MA | EMA of EMA with lag compensation |
| [**DSMA**](../lib/trends_IIR/dsma/Dsma.md) | Deviation-Scaled MA | Volatility-adaptive smoothing |
| [**EMA**](../lib/trends_IIR/ema/Ema.md) | Exponential MA | The fundamental IIR filter |
| [**FRAMA**](../lib/trends_IIR/frama/Frama.md) | Fractal Adaptive MA | Dimension-based adaptation |
| [**HEMA**](../lib/trends_IIR/hema/Hema.md) | Hull Exponential MA | Hull concept with EMA |
| [**HTIT**](../lib/trends_IIR/htit/Htit.md) | Hilbert Instantaneous Trend | Dominant cycle extraction |
| [**JMA**](../lib/trends_IIR/jma/Jma.md) | Jurik MA | Adaptive, low-lag, proprietary algorithm |
| [**KAMA**](../lib/trends_IIR/kama/Kama.md) | Kaufman Adaptive MA | Efficiency ratio adaptation |
| [**MAMA**](../lib/trends_IIR/mama/Mama.md) | MESA Adaptive MA | Homodyne discriminator based |
| [**MMA**](../lib/trends_IIR/mma/Mma.md) | Modified MA | Smoothed EMA variant |
| [**MGDI**](../lib/trends_IIR/mgdi/Mgdi.md) | McGinley Dynamic | Market-speed tracking |
| [**QEMA**](../lib/trends_IIR/qema/Qema.md) | Quad Exponential MA | Four-stage exponential |
| [**RGMA**](../lib/trends_IIR/rgma/Rgma.md) | Recursive Gaussian MA | Gaussian approximation |
| [**REMA**](../lib/trends_IIR/rema/Rema.md) | Regularized Exponential MA | Regularization for stability |
| [**RMA**](../lib/trends_IIR/rma/Rma.md) | WildeR MA | Wilder's smoothing (1/n decay) |
| [**T3**](../lib/trends_IIR/t3/T3.md) | Tillson T3 MA | Six-stage DEMA variant |
| [**TEMA**](../lib/trends_IIR/tema/Tema.md) | Triple Exponential MA | Three-stage lag reduction |
| [**VAMA**](../lib/trends_IIR/vama/Vama.md) | Volatility Adjusted MA | ATR-based adaptation |
| [**VIDYA**](../lib/trends_IIR/vidya/Vidya.md) | Variable Index Dynamic | CMO-based adaptation |
| [**YZVAMA**](../lib/trends_IIR/yzvama/Yzvama.md) | Yang-Zhang Vol Adjusted | YZ volatility adaptation |
| [**ZLEMA**](../lib/trends_IIR/zlema/Zlema.md) | Zero-Lag Exponential MA | Momentum-compensated EMA |

### Filters

Signal processing filters adapted for financial time series. Designed to separate signal from noise with controlled frequency response.

| Indicator | Full Name | Notes |
| :-------- | :-------- | :---- |
| [**BESSEL**](../lib/filters/bessel/Bessel.md) | Bessel Filter | Maximally flat group delay |
| [**BILATERAL**](../lib/filters/bilateral/Bilateral.md) | Bilateral Filter | Edge-preserving smoothing |
| [**BPF**](../lib/filters/bpf/Bpf.md) | BandPass Filter | Frequency band isolation |
| [**BUTTER**](../lib/filters/butter/Butter.md) | Butterworth Filter | Maximally flat passband |
| [**CHEBY1**](../lib/filters/cheby1/Cheby1.md) | Chebyshev Type I | Steeper rolloff with ripple |
| [**SSF**](../lib/filters/ssf/Ssf.md) | Super Smooth Filter | Ehlers two-pole design |
| [**USF**](../lib/filters/usf/Usf.md) | Ultimate Smoother | Ehlers high-fidelity filter |

### Oscillators

Bounded indicators that oscillate around a centerline or between fixed extremes. Useful for mean-reversion signals.

| Indicator | Full Name | Notes |
| :-------- | :-------- | :---- |
| [**AC**](../lib/oscillators/ac/Ac.md) | Acceleration Oscillator | AO acceleration (2nd derivative) |
| [**AO**](../lib/oscillators/ao/Ao.md) | Awesome Oscillator | Midpoint momentum |
| [**APO**](../lib/oscillators/apo/Apo.md) | Absolute Price Oscillator | EMA difference |
| [**BBB**](../lib/oscillators/bbb/Bbb.md) | Bollinger %B | Position within Bollinger Bands |
| [**BBS**](../lib/oscillators/bbs/Bbs.md) | Bollinger Band Squeeze | BB inside KC squeeze detection |
| [**CFO**](../lib/oscillators/cfo/Cfo.md) | Chande Forecast Oscillator | Forecast error percentage |
| [**DPO**](../lib/oscillators/dpo/Dpo.md) | Detrended Price Oscillator | Displaced SMA trend removal |
| [**FISHER**](../lib/oscillators/fisher/Fisher.md) | Fisher Transform | Gaussian-normalized price reversal |
| [**INERTIA**](../lib/oscillators/inertia/Inertia.md) | Inertia | Linear regression residual |
| [**KDJ**](../lib/oscillators/kdj/Kdj.md) | KDJ Indicator | Enhanced Stochastic (J = 3K − 2D) |
| [**PGO**](../lib/oscillators/pgo/Pgo.md) | Pretty Good Oscillator | ATR-normalized SMA displacement |
| [**SMI**](../lib/oscillators/smi/Smi.md) | Stochastic Momentum Index | Distance from range midpoint (K/D lines) |
| [**STOCH**](../lib/oscillators/stoch/Stoch.md) | Stochastic Oscillator | Close within N-period H/L range (%K/%D) |
| [**MACD**](../lib/momentum/macd/Macd.md) | MACD | EMA crossover system |
| [**RSI**](../lib/momentum/rsi/Rsi.md) | Relative Strength Index | Bounded 0-100 momentum |
| [**ULTOSC**](../lib/oscillators/ultosc/Ultosc.md) | Ultimate Oscillator | Multi-timeframe weighted |

### Dynamics

Indicators measuring trend strength, regime, and directional movement quality.

| Indicator | Full Name | Notes |
| :-------- | :-------- | :---- |
| [**ADX**](../lib/dynamics/adx/Adx.md) | Average Directional Index | Trend strength 0-100 |
| [**ADXR**](../lib/dynamics/adxr/Adxr.md) | ADX Rating | Smoothed ADX |
| [**AMAT**](../lib/dynamics/amat/Amat.md) | Archer MA Trends | MA-based trend detection |
| [**AROON**](../lib/dynamics/aroon/Aroon.md) | Aroon | High/low recency |
| [**AROONOSC**](../lib/dynamics/aroonosc/AroonOsc.md) | Aroon Oscillator | Aroon Up minus Down |
| [**DMX**](../lib/dynamics/dmx/Dmx.md) | Jurik DMX | Enhanced directional movement |
| [**SUPER**](../lib/dynamics/super/Super.md) | SuperTrend | ATR-based trend bands |

### Momentum

Rate of change and velocity measurements. First derivatives of price.

| Indicator | Full Name | Notes |
| :-------- | :-------- | :---- |
| [**BOP**](../lib/momentum/bop/Bop.md) | Balance of Power | Close position in range |
| [**CFB**](../lib/momentum/cfb/Cfb.md) | Composite Fractal Behavior | Jurik fractal momentum |
| [**ROC**](../lib/momentum/roc/Roc.md) | Rate of Change | Absolute price change over N periods |
| [**ROCP**](../lib/momentum/rocp/Rocp.md) | Rate of Change Percentage | Percentage price change over N periods |
| [**ROCR**](../lib/momentum/rocr/Rocr.md) | Rate of Change Ratio | Price ratio over N periods |
| [**PRS**](../lib/momentum/prs/Prs.md) | Price Relative Strength | Dual-input ratio comparison |
| [**RSX**](../lib/momentum/rsx/Rsx.md) | Jurik RSX | Smoothed RSI variant |
| [**TSI**](../lib/momentum/tsi/Tsi.md) | True Strength Index | Double-smoothed momentum oscillator |
| [**VEL**](../lib/momentum/vel/Vel.md) | Jurik Velocity | Adaptive velocity |

### Volatility

Measures of price variability and range. Essential for position sizing and stop placement.

| Indicator | Full Name | Notes |
| :-------- | :-------- | :---- |
| [**ADR**](../lib/volatility/adr/Adr.md) | Average Daily Range | Simple range averaging |
| [**ATR**](../lib/volatility/atr/Atr.md) | Average True Range | Gap-adjusted range |
| [**ATRP**](../lib/volatility/atrp/Atrp.md) | ATR Percent | Normalized ATR |

### Volume

Price-volume relationships and accumulation/distribution measurements.

| Indicator | Full Name | Notes |
| :-------- | :-------- | :---- |
| [**ADL**](../lib/volume/adl/Adl.md) | Accumulation/Distribution | Volume-weighted close position |
| [**ADOSC**](../lib/volume/adosc/Adosc.md) | Chaikin A/D Oscillator | ADL momentum |
| [**TWAP**](../lib/volume/twap/Twap.md) | Time Weighted Average Price | Time-equal-weighted price average |
| [**VA**](../lib/volume/va/Va.md) | Volume Accumulation | Cumulative volume by close position |
| [**VF**](../lib/volume/vf/Vf.md) | Volume Force | EMA-smoothed price-volume force |
| [**VO**](../lib/volume/vo/Vo.md) | Volume Oscillator | Short vs long volume MA difference |
| [**VROC**](../lib/volume/vroc/Vroc.md) | Volume Rate of Change | Volume change over lookback period |

### Channels

Price envelope and boundary indicators for breakout and mean-reversion strategies.

| Indicator | Full Name | Notes |
| :-------- | :-------- | :---- |
| [**ABBER**](../lib/channels/abber/abber.md) | Aberration Bands | Statistical deviation bands |
| [**ACCBANDS**](../lib/channels/accbands/accbands.md) | Acceleration Bands | Volatility-adjusted envelope |
| [**DCHANNEL**](../lib/channels/dchannel/Dchannel.md) | Donchian Channels | Highest-high / lowest-low breakout bands |
| [**DECAYCHANNEL**](../lib/channels/decaychannel/decaychannel.md) | Decay Min-Max Channel | Exponential decay toward midpoint |
| [**FCB**](../lib/channels/fcb/fcb.md) | Fractal Chaos Bands | Williams fractal-based support/resistance |
| [**JBANDS**](../lib/channels/jbands/Jbands.md) | Jurik Adaptive Envelope Bands | Snap-to-extreme, decay-to-price volatility bands |
| [**KCHANNEL**](../lib/channels/kchannel/kchannel.md) | Keltner Channel | EMA with ATR bands; smoother than Bollinger |
| [**MAENV**](../lib/channels/maenv/maenv.md) | Moving Average Envelope | Fixed percentage bands around selectable MA type |
| [**MMCHANNEL**](../lib/channels/mmchannel/mmchannel.md) | Min-Max Channel | Rolling highest high / lowest low; O(1) monotonic deques |
| [**PCHANNEL**](../lib/channels/pchannel/pchannel.md) | Price Channel | Highest high / lowest low; identical to Donchian |
| [**REGCHANNEL**](../lib/channels/regchannel/regchannel.md) | Linear Regression Channel | Linear regression line with standard deviation bands |
| [**SDCHANNEL**](../lib/channels/sdchannel/sdchannel.md) | Standard Deviation Channel | Moving average with standard deviation bands |
| [**STARCHANNEL**](../lib/channels/starchannel/starchannel.md) | Stoller Average Range Channel | SMA with ATR bands; similar to Keltner but uses SMA |

### Statistics

Mathematical and statistical computations on price series.

| Indicator | Full Name | Notes |
| :-------- | :-------- | :---- |
| [**BIAS**](../lib/statistics/bias/Bias.md) | Bias | Percentage deviation from SMA |
| [**COINTEGRATION**](../lib/statistics/cointegration/Cointegration.md) | Cointegration | Engle-Granger two-step method with ADF test |
| [**CORRELATION**](../lib/statistics/correlation/Correlation.md) | Pearson Correlation | Linear relationship between two series [-1, +1] |
| [**CMA**](../lib/statistics/cma/Cma.md) | Cumulative Moving Average | Expanding window average |
| [**COVARIANCE**](../lib/statistics/covariance/Covariance.md) | Covariance | Joint variability |
| [**LINREG**](../lib/statistics/linreg/LinReg.md) | Linear Regression | Best-fit line |
| [**MEDIAN**](../lib/statistics/median/Median.md) | Rolling Median | 50th percentile |
| [**SKEW**](../lib/statistics/skew/Skew.md) | Skewness | Distribution asymmetry |
| [**STDDEV**](../lib/statistics/stddev/StdDev.md) | Standard Deviation | Dispersion measure |
| [**SUM**](../lib/statistics/sum/Sum.md) | Rolling Sum | Windowed sum |
| [**VARIANCE**](../lib/statistics/variance/Variance.md) | Variance | Squared deviation |

### Forecasts

Predictive indicators and extrapolation methods.

| Indicator | Full Name | Notes |
| :-------- | :-------- | :---- |
| [**AFIRMA**](../lib/forecasts/afirma/Afirma.md) | Adaptive FIR MA | Predictive FIR filter |

### Cycles

Periodic pattern detection and dominant frequency extraction. Markets exhibit cycles; detecting them reliably remains challenging.

| Indicator | Full Name | Notes |
| :-------- | :-------- | :---- |
| [**HT_SINE**](../lib/cycles/ht_sine/HtSine.md) | Hilbert Transform SineWave | Dominant cycle phase with 45° lead signal |
| [**SSFDSP**](../lib/cycles/ssfdsp/Ssfdsp.md) | SSF Detrended Synthetic Price | Dual Super Smoother Filter oscillator |

### Numerics

Mathematical transformations and derivative indicators. Building blocks for analysis.

| Indicator | Full Name | Notes |
| :-------- | :-------- | :---- |
| [**ACCEL**](../lib/numerics/accel/Accel.md) | Acceleration (2nd Derivative) | Change in slope; momentum |
| [**CHANGE**](../lib/numerics/change/Change.md) | Percentage Change | Relative price movement (current - past) / past |
| [**EXPTRANS**](../lib/numerics/exptrans/Exptrans.md) | Exponential Transform | e^x transform for log-space reversal |
| [**HIGHEST**](../lib/numerics/highest/Highest.md) | Rolling Maximum | O(1) amortized via monotonic deque |
| [**JERK**](../lib/numerics/jerk/Jerk.md) | Jerk (3rd Derivative) | Change in acceleration |
| [**LINEARTRANS**](../lib/numerics/lineartrans/Lineartrans.md) | Linear Transform | y = ax + b scaling transformation |
| [**LOGTRANS**](../lib/numerics/logtrans/Logtrans.md) | Logarithmic Transform | Natural log for percentage analysis |
| [**LOWEST**](../lib/numerics/lowest/Lowest.md) | Rolling Minimum | O(1) amortized via monotonic deque |
| [**MIDPOINT**](../lib/numerics/midpoint/Midpoint.md) | Rolling Midpoint | (Highest + Lowest) / 2 |
| [**NORMALIZE**](../lib/numerics/normalize/Normalize.md) | Min-Max Normalization | Scale to [0,1] via rolling min/max |
| [**RELU**](../lib/numerics/relu/Relu.md) | Rectified Linear Unit | max(0, x); activation function |
| [**SIGMOID**](../lib/numerics/sigmoid/Sigmoid.md) | Logistic Function | 1/(1+e^-x); bounded [0,1] transform |
| [**SLOPE**](../lib/numerics/slope/Slope.md) | Slope (1st Derivative) | Rate of change; velocity |
| [**SQRTTRANS**](../lib/numerics/sqrttrans/Sqrttrans.md) | Square Root Transform | √x; variance to standard deviation conversion |

### Errors

Error metrics and loss functions for model evaluation, forecast assessment, and strategy validation. Quantifying wrongness before production quantifies losses.

| Indicator | Full Name | Notes |
| :-------- | :-------- | :---- |
| [**WRMSE**](../lib/errors/wrmse/Wrmse.md) | Weighted Root Mean Squared Error | Custom observation weighting for error emphasis |
