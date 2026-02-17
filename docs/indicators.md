# Indicator Catalog

QuanTAlib provides technical indicators organized into mathematical families. Understanding these families helps choose the right tool for the analytical problem at hand. Selecting an indicator without understanding its category leads to confusion at best, losses at worst.

## Category Reference

| Category | What It Measures | Representative Indicators | When to Reach for It |
| :------- | :--------------- | :------------------------ | :------------------- |
| [**Trends (FIR)**](../lib/trends_FIR/_index.md) | Trend direction via finite impulse response filters | SMA, WMA, ALMA, HMA, LSMA | Trend identification with predictable lag and finite memory. Output depends only on a fixed window of past prices. |
| [**Trends (IIR)**](../lib/trends_IIR/_index.md) | Trend direction via infinite impulse response filters | EMA, DEMA, TEMA, JMA, KAMA, MAMA | Trend identification with recursive calculation and theoretically infinite memory. More responsive per unit of smoothness. |
| [**Filters**](../lib/filters/_index.md) | Signal processing filters for noise reduction | Bessel, Butterworth, Super Smoother | Removing noise while preserving trend structure. Designed by engineers, borrowed by traders. |
| [**Oscillators**](../lib/oscillators/_index.md) | Cyclical movement around a baseline | Stochastic, Fisher, UltOsc, Williams %R | Identifying overbought/oversold conditions and potential reversals. Bounded indicators that oscillate. |
| [**Dynamics**](../lib/dynamics/_index.md) | Trend strength and structural changes | ADX, Aroon, SuperTrend, Chop | Determining market regime (trending vs ranging) and measuring trend conviction. |
| [**Momentum**](../lib/momentum/_index.md) | Speed and magnitude of price changes | ROC, RSI, MACD, CMO | Measuring acceleration or deceleration in price. First derivative territory. |
| [**Volatility**](../lib/volatility/_index.md) | Size and variability of price movements | ATR, StdDev, HV, YZV | Position sizing, stop-loss placement, regime identification. How much prices move matters as much as direction. |
| [**Volume**](../lib/volume/_index.md) | Trading activity and price-volume relationships | OBV, VWAP, MFI, CMF | Confirming price movements with participation. Volume validates or contradicts price action. |
| [**Channels**](../lib/channels/_index.md) | Price boundaries and range definitions | Bollinger, Keltner, Donchian | Breakout strategies and range-bound trading. Defining "normal" so abnormal becomes visible. |
| [**Statistics**](../lib/statistics/_index.md) | Mathematical relationships between price series | Correlation, Covariance, Beta, Z-Score | Portfolio analysis, pairs trading, statistical arbitrage. Quantitative analysis beyond single instruments. |
| [**Numerics**](../lib/numerics/_index.md) | Mathematical transformations and signal processing | Slope, Accel, Normalize, Sigmoid | Custom indicator development and advanced signal processing. Building blocks for novel indicators. |
| [**Errors**](../lib/errors/_index.md) | Measurement accuracy and model fit quality | MAE, RMSE, R², Huber | Model validation and forecast assessment. Quantifying wrongness before production quantifies losses. |
| [**Forecasts**](../lib/forecasts/_index.md) | Future price prediction and projection | AFIRMA | Projecting price based on historical patterns. Predictions that invite humility. |
| [**Cycles**](../lib/cycles/_index.md) | Periodic patterns and dominant frequencies | Hilbert Transform, EBSW, STC | Identifying cyclical market behavior. Markets exhibit cycles; detecting them reliably remains hard. |
| [**Reversals**](../lib/reversals/_index.md) | Turning points and stop levels | Pivot Points, PSAR, Chandelier, Swings | Identifying potential trend reversals, computing adaptive stops, and defining support/resistance. |

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
| [**HWMA**](../lib/trends_FIR/hwma/Hwma.md) | Henderson Weighted MA | Henderson curve smoothing |
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
| [**MGDI**](../lib/trends_IIR/mgdi/Mgdi.md) | McGinley Dynamic | Market-speed tracking |
| [**MMA**](../lib/trends_IIR/mma/Mma.md) | Modified MA | Smoothed EMA variant |
| [**QEMA**](../lib/trends_IIR/qema/Qema.md) | Quad Exponential MA | Four-stage exponential |
| [**REMA**](../lib/trends_IIR/rema/Rema.md) | Regularized Exponential MA | Regularization for stability |
| [**RGMA**](../lib/trends_IIR/rgma/Rgma.md) | Recursive Gaussian MA | Gaussian approximation |
| [**RMA**](../lib/trends_IIR/rma/Rma.md) | WildeR MA | Wilder's smoothing (1/n decay) |
| [**T3**](../lib/trends_IIR/t3/T3.md) | Tillson T3 MA | Six-stage DEMA variant |
| [**TEMA**](../lib/trends_IIR/tema/Tema.md) | Triple Exponential MA | Three-stage lag reduction |
| [**VAMA**](../lib/trends_IIR/vama/Vama.md) | Volatility Adjusted MA | ATR-based adaptation |
| [**VIDYA**](../lib/trends_IIR/vidya/Vidya.md) | Variable Index Dynamic | CMO-based adaptation |
| [**YZVAMA**](../lib/trends_IIR/yzvama/Yzvama.md) | Yang-Zhang Vol Adjusted MA | YZ volatility adaptation |
| [**ZLDEMA**](../lib/trends_IIR/zldema/Zldema.md) | Zero-Lag Double Exponential MA | Momentum-compensated DEMA |
| [**ZLEMA**](../lib/trends_IIR/zlema/Zlema.md) | Zero-Lag Exponential MA | Momentum-compensated EMA |
| [**ZLTEMA**](../lib/trends_IIR/zltema/Zltema.md) | Zero-Lag Triple Exponential MA | Momentum-compensated TEMA |

### Filters

Signal processing filters adapted for financial time series. Designed to separate signal from noise with controlled frequency response.

| Indicator | Full Name | Notes |
| :-------- | :-------- | :---- |
| [**BESSEL**](../lib/filters/bessel/Bessel.md) | Bessel Filter | Maximally flat group delay |
| [**BILATERAL**](../lib/filters/bilateral/Bilateral.md) | Bilateral Filter | Edge-preserving smoothing |
| [**BPF**](../lib/filters/bpf/Bpf.md) | BandPass Filter | Frequency band isolation |
| [**BUTTER**](../lib/filters/butter/Butter.md) | Butterworth Filter | Maximally flat passband |
| [**CHEBY1**](../lib/filters/cheby1/Cheby1.md) | Chebyshev Type I | Steeper rolloff with passband ripple |
| [**CHEBY2**](../lib/filters/cheby2/Cheby2.md) | Chebyshev Type II | Steeper rolloff with stopband ripple |
| [**ELLIPTIC**](../lib/filters/elliptic/Elliptic.md) | Elliptic (Cauer) Filter | Sharpest transition, both band ripple |
| [**GAUSS**](../lib/filters/gauss/Gauss.md) | Gaussian Filter | No overshoot, smooth response |
| [**HANN**](../lib/filters/hann/Hann.md) | Hann Filter | Raised cosine window filter |
| [**HP**](../lib/filters/hp/Hp.md) | Hodrick-Prescott Filter | Trend-cycle decomposition |
| [**HPF**](../lib/filters/hpf/Hpf.md) | High Pass Filter | Ehlers high-pass design |
| [**KALMAN**](../lib/filters/kalman/Kalman.md) | Kalman Filter | Optimal recursive estimation |
| [**LOESS**](../lib/filters/loess/Loess.md) | LOESS Smoothing | Local polynomial regression |
| [**NOTCH**](../lib/filters/notch/Notch.md) | Notch Filter | Single frequency rejection |
| [**SGF**](../lib/filters/sgf/Sgf.md) | Savitzky-Golay Filter | Polynomial least-squares fitting |
| [**SSF**](../lib/filters/ssf/Ssf.md) | Super Smooth Filter | Ehlers two-pole design |
| [**USF**](../lib/filters/usf/Usf.md) | Ultimate Smoother | Ehlers high-fidelity filter |
| [**WIENER**](../lib/filters/wiener/Wiener.md) | Wiener Filter | Minimum mean-square error denoising |

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
| [**STOCHF**](../lib/oscillators/stochf/Stochf.md) | Stochastic Fast | Unsmoothed Stochastic (%K/%D, SMA smoothing only) |
| [**STOCHRSI**](../lib/oscillators/stochrsi/Stochrsi.md) | Stochastic RSI | Stochastic applied to RSI (%K/%D) |
| [**TRIX**](../lib/oscillators/trix/Trix.md) | Triple Exponential Average | ROC of triple-smoothed EMA |
| [**TTM_WAVE**](../lib/oscillators/ttm_wave/TtmWave.md) | TTM Wave | Fibonacci-period MACD composite (A/B/C waves) |
| [**ULTOSC**](../lib/oscillators/ultosc/Ultosc.md) | Ultimate Oscillator | Multi-timeframe weighted buying pressure |
| [**WILLR**](../lib/oscillators/willr/Willr.md) | Williams %R | Inverse Stochastic (-100 to 0) |

### Dynamics

Indicators measuring trend strength, regime, and directional movement quality.

| Indicator | Full Name | Notes |
| :-------- | :-------- | :---- |
| [**ADX**](../lib/dynamics/adx/Adx.md) | Average Directional Index | Trend strength 0-100 |
| [**ADXR**](../lib/dynamics/adxr/Adxr.md) | ADX Rating | Smoothed ADX |
| [**ALLIGATOR**](../lib/dynamics/alligator/Alligator.md) | Williams Alligator | Three displaced SMAs for trend detection |
| [**AMAT**](../lib/dynamics/amat/Amat.md) | Archer MA Trends | MA-based trend detection |
| [**AROON**](../lib/dynamics/aroon/Aroon.md) | Aroon | High/low recency |
| [**AROONOSC**](../lib/dynamics/aroonosc/AroonOsc.md) | Aroon Oscillator | Aroon Up minus Down |
| [**CHOP**](../lib/dynamics/chop/Chop.md) | Choppiness Index | ATR sum vs range; trending vs choppy |
| [**DMX**](../lib/dynamics/dmx/Dmx.md) | Jurik DMX | Enhanced directional movement |
| [**DX**](../lib/dynamics/dx/Dx.md) | Directional Movement Index | Raw directional strength |
| [**HT_TRENDMODE**](../lib/dynamics/ht_trendmode/HtTrendmode.md) | Hilbert Transform Trend Mode | Cycle vs trend regime detection |
| [**ICHIMOKU**](../lib/dynamics/ichimoku/Ichimoku.md) | Ichimoku Cloud | Multi-component trend system |
| [**IMI**](../lib/dynamics/imi/Imi.md) | Intraday Momentum Index | Candlestick-based momentum |
| [**IMPULSE**](../lib/dynamics/impulse/Impulse.md) | Elder Impulse System | EMA + MACD-H trend/momentum fusion |
| [**QSTICK**](../lib/dynamics/qstick/Qstick.md) | Qstick | Average close-open difference |
| [**SUPER**](../lib/dynamics/super/Super.md) | SuperTrend | ATR-based trend bands |
| [**TTM_SQUEEZE**](../lib/dynamics/ttm_squeeze/TtmSqueeze.md) | TTM Squeeze | BB inside KC squeeze with momentum |
| [**TTM_TREND**](../lib/dynamics/ttm_trend/TtmTrend.md) | TTM Trend | Bar coloring by close vs midline |
| [**VORTEX**](../lib/dynamics/vortex/Vortex.md) | Vortex Indicator | Uptrend/downtrend movement comparison |

### Momentum

Rate of change and velocity measurements. First derivatives of price.

| Indicator | Full Name | Notes |
| :-------- | :-------- | :---- |
| [**BOP**](../lib/momentum/bop/Bop.md) | Balance of Power | Close position in range |
| [**CCI**](../lib/momentum/cci/Cci.md) | Commodity Channel Index | Mean deviation normalized |
| [**CFB**](../lib/momentum/cfb/Cfb.md) | Composite Fractal Behavior | Jurik fractal momentum |
| [**CMO**](../lib/momentum/cmo/Cmo.md) | Chande Momentum Oscillator | Up/down ratio oscillator |
| [**MACD**](../lib/momentum/macd/Macd.md) | Moving Average Convergence Divergence | EMA crossover system |
| [**MOM**](../lib/momentum/mom/Mom.md) | Momentum | Raw price difference over N periods |
| [**PMO**](../lib/momentum/pmo/Pmo.md) | Price Momentum Oscillator | Double-smoothed ROC |
| [**PPO**](../lib/momentum/ppo/Ppo.md) | Percentage Price Oscillator | Percentage EMA difference |
| [**PRS**](../lib/momentum/prs/Prs.md) | Price Relative Strength | Dual-input ratio comparison |
| [**ROC**](../lib/momentum/roc/Roc.md) | Rate of Change | Absolute price change over N periods |
| [**ROCP**](../lib/momentum/rocp/Rocp.md) | Rate of Change Percentage | Percentage price change over N periods |
| [**ROCR**](../lib/momentum/rocr/Rocr.md) | Rate of Change Ratio | Price ratio over N periods |
| [**RSI**](../lib/momentum/rsi/Rsi.md) | Relative Strength Index | Bounded 0-100 momentum |
| [**RSX**](../lib/momentum/rsx/Rsx.md) | Jurik RSX | Smoothed RSI variant |
| [**TSI**](../lib/momentum/tsi/Tsi.md) | True Strength Index | Double-smoothed momentum oscillator |
| [**VEL**](../lib/momentum/vel/Vel.md) | Jurik Velocity | Adaptive velocity |

### Volatility

Measures of price variability and range. Essential for position sizing and stop placement.

| Indicator | Full Name | Notes |
| :-------- | :-------- | :---- |
| [**ADR**](../lib/volatility/adr/Adr.md) | Average Daily Range | Simple range averaging |
| [**ATR**](../lib/volatility/atr/Atr.md) | Average True Range | Gap-adjusted range |
| [**ATRN**](../lib/volatility/atrn/Atrn.md) | ATR Normalized | ATR scaled to [0,1] |
| [**ATRP**](../lib/volatility/atrp/Atrp.md) | ATR Percent | Percentage-based ATR |
| [**BBW**](../lib/volatility/bbw/Bbw.md) | Bollinger Band Width | Band width as percentage of middle band |
| [**BBWN**](../lib/volatility/bbwn/Bbwn.md) | BB Width Normalized | Band width normalized to [0,1] |
| [**BBWP**](../lib/volatility/bbwp/Bbwp.md) | BB Width Percentile | Band width historical percentile |
| [**CCV**](../lib/volatility/ccv/Ccv.md) | Close-to-Close Volatility | Log-return standard deviation |
| [**CV**](../lib/volatility/cv/Cv.md) | Coefficient of Variation | StdDev / Mean ratio |
| [**CVI**](../lib/volatility/cvi/Cvi.md) | Chaikin Volatility | EMA change of H-L range |
| [**EWMA**](../lib/volatility/ewma/Ewma.md) | EWMA Volatility | Exponentially weighted variance |
| [**GKV**](../lib/volatility/gkv/Gkv.md) | Garman-Klass Volatility | OHLC-based efficiency estimator |
| [**HLV**](../lib/volatility/hlv/Hlv.md) | High-Low Volatility | Parkinson range-based estimator |
| [**HV**](../lib/volatility/hv/Hv.md) | Historical Volatility | Annualized log-return StdDev |
| [**JVOLTY**](../lib/volatility/jvolty/Jvolty.md) | Jurik Volatility | Adaptive volatility measure |
| [**JVOLTYN**](../lib/volatility/jvoltyn/Jvoltyn.md) | Jurik Volatility Normalized | Jurik volatility scaled to [0,100] |
| [**MASSI**](../lib/volatility/massi/Massi.md) | Mass Index | EMA ratio of H-L range |
| [**NATR**](../lib/volatility/natr/Natr.md) | Normalized ATR | ATR as percentage of close |
| [**RSV**](../lib/volatility/rsv/Rsv.md) | Rogers-Satchell Volatility | Drift-independent OHLC estimator |
| [**RV**](../lib/volatility/rv/Rv.md) | Realized Volatility | Sum of squared returns |
| [**RVI**](../lib/volatility/rvi/Rvi.md) | Relative Volatility Index | RSI applied to StdDev |
| [**TR**](../lib/volatility/tr/Tr.md) | True Range | Max(H-L, H-prevC, prevC-L) |
| [**UI**](../lib/volatility/ui/Ui.md) | Ulcer Index | Downside deviation from highs |
| [**VOV**](../lib/volatility/vov/Vov.md) | Volatility of Volatility | Second-order volatility |
| [**VR**](../lib/volatility/vr/Vr.md) | Volatility Ratio | ATR-relative true range |
| [**YZV**](../lib/volatility/yzv/Yzv.md) | Yang-Zhang Volatility | Optimal OHLC estimator |

### Volume

Price-volume relationships and accumulation/distribution measurements.

| Indicator | Full Name | Notes |
| :-------- | :-------- | :---- |
| [**ADL**](../lib/volume/adl/Adl.md) | Accumulation/Distribution Line | Volume-weighted close position |
| [**ADOSC**](../lib/volume/adosc/Adosc.md) | Chaikin A/D Oscillator | ADL momentum (fast EMA - slow EMA) |
| [**AOBV**](../lib/volume/aobv/Aobv.md) | Archer On-Balance Volume | OBV with signal line |
| [**CMF**](../lib/volume/cmf/Cmf.md) | Chaikin Money Flow | Volume-weighted close position over period |
| [**EFI**](../lib/volume/efi/Efi.md) | Elder's Force Index | Price change × volume |
| [**EOM**](../lib/volume/eom/Eom.md) | Ease of Movement | Price movement per unit volume |
| [**III**](../lib/volume/iii/Iii.md) | Intraday Intensity Index | Close position within H-L × volume |
| [**KVO**](../lib/volume/kvo/Kvo.md) | Klinger Volume Oscillator | Trend-volume force oscillator |
| [**MFI**](../lib/volume/mfi/Mfi.md) | Money Flow Index | Volume-weighted RSI |
| [**NVI**](../lib/volume/nvi/Nvi.md) | Negative Volume Index | Cumulative on low-volume days |
| [**OBV**](../lib/volume/obv/Obv.md) | On Balance Volume | Cumulative signed volume |
| [**PVD**](../lib/volume/pvd/Pvd.md) | Price Volume Divergence | Price-volume correlation divergence |
| [**PVI**](../lib/volume/pvi/Pvi.md) | Positive Volume Index | Cumulative on high-volume days |
| [**PVO**](../lib/volume/pvo/Pvo.md) | Percentage Volume Oscillator | Percentage volume MA difference |
| [**PVR**](../lib/volume/pvr/Pvr.md) | Price Volume Rank | Categorical price-volume classification |
| [**PVT**](../lib/volume/pvt/Pvt.md) | Price Volume Trend | ROC-weighted cumulative volume |
| [**TVI**](../lib/volume/tvi/Tvi.md) | Trade Volume Index | Tick-direction cumulative volume |
| [**TWAP**](../lib/volume/twap/Twap.md) | Time Weighted Average Price | Time-equal-weighted price average |
| [**VA**](../lib/volume/va/Va.md) | Volume Accumulation | Cumulative volume by close position |
| [**VF**](../lib/volume/vf/Vf.md) | Volume Force | EMA-smoothed price-volume force |
| [**VO**](../lib/volume/vo/Vo.md) | Volume Oscillator | Short vs long volume MA difference |
| [**VROC**](../lib/volume/vroc/Vroc.md) | Volume Rate of Change | Volume change over lookback period |
| [**VWAD**](../lib/volume/vwad/Vwad.md) | Volume Weighted A/D | Close-position cumulative volume |
| [**VWAP**](../lib/volume/vwap/Vwap.md) | Volume Weighted Average Price | Price × volume / total volume |
| [**VWMA**](../lib/volume/vwma/Vwma.md) | Volume Weighted MA | Volume-weighted moving average |
| [**WAD**](../lib/volume/wad/Wad.md) | Williams A/D | True range-based accumulation |

### Channels

Price envelope and boundary indicators for breakout and mean-reversion strategies.

| Indicator | Full Name | Notes |
| :-------- | :-------- | :---- |
| [**ABBER**](../lib/channels/abber/abber.md) | Aberration Bands | Statistical deviation bands |
| [**ACCBANDS**](../lib/channels/accbands/accbands.md) | Acceleration Bands | Volatility-adjusted envelope |
| [**APCHANNEL**](../lib/channels/apchannel/apchannel.md) | Andrews' Pitchfork | Three-line channel from pivot points |
| [**APZ**](../lib/channels/apz/apz.md) | Adaptive Price Zone | EMA-based volatility zone |
| [**ATRBANDS**](../lib/channels/atrbands/Atrbands.md) | ATR Bands | ATR-based envelope around price |
| [**BBANDS**](../lib/channels/bbands/Bbands.md) | Bollinger Bands | SMA ± StdDev bands |
| [**DCHANNEL**](../lib/channels/dchannel/Dchannel.md) | Donchian Channels | Highest-high / lowest-low breakout bands |
| [**DECAYCHANNEL**](../lib/channels/decaychannel/decaychannel.md) | Decay Min-Max Channel | Exponential decay toward midpoint |
| [**FCB**](../lib/channels/fcb/fcb.md) | Fractal Chaos Bands | Williams fractal-based support/resistance |
| [**JBANDS**](../lib/channels/jbands/Jbands.md) | Jurik Adaptive Bands | Snap-to-extreme, decay-to-price volatility bands |
| [**KCHANNEL**](../lib/channels/kchannel/kchannel.md) | Keltner Channel | EMA with ATR bands; smoother than Bollinger |
| [**MAENV**](../lib/channels/maenv/maenv.md) | Moving Average Envelope | Fixed percentage bands around selectable MA |
| [**MMCHANNEL**](../lib/channels/mmchannel/mmchannel.md) | Min-Max Channel | Rolling highest high / lowest low |
| [**PCHANNEL**](../lib/channels/pchannel/pchannel.md) | Price Channel | Highest high / lowest low with midline |
| [**REGCHANNEL**](../lib/channels/regchannel/regchannel.md) | Regression Channel | Linear regression with StdDev bands |
| [**SDCHANNEL**](../lib/channels/sdchannel/sdchannel.md) | Standard Deviation Channel | MA with standard deviation bands |
| [**STARCHANNEL**](../lib/channels/starchannel/starchannel.md) | Stoller Average Range Channel | SMA with ATR bands |
| [**STBANDS**](../lib/channels/stbands/Stbands.md) | Super Trend Bands | ATR-based SuperTrend envelope |
| [**TTM_LRC**](../lib/channels/ttm_lrc/TtmLrc.md) | TTM Linear Regression Channel | John Carter's regression channel |
| [**UBANDS**](../lib/channels/ubands/Ubands.md) | Ultimate Bands | Ehlers bandpass-based bands |
| [**UCHANNEL**](../lib/channels/uchannel/Uchannel.md) | Ultimate Channel | Ehlers smoothed channel |
| [**VWAPBANDS**](../lib/channels/vwapbands/Vwapbands.md) | VWAP Bands | VWAP with StdDev bands |
| [**VWAPSD**](../lib/channels/vwapsd/Vwapsd.md) | VWAP StdDev Bands | VWAP with standard deviation envelopes |

### Statistics

Mathematical and statistical computations on price series.

| Indicator | Full Name | Notes |
| :-------- | :-------- | :---- |
| [**ACF**](../lib/statistics/acf/Acf.md) | Autocorrelation Function | Lagged self-correlation |
| [**BETA**](../lib/statistics/beta/Beta.md) | Beta Coefficient | Systematic risk measure |
| [**BIAS**](../lib/statistics/bias/Bias.md) | Bias | Percentage deviation from SMA |
| [**CMA**](../lib/statistics/cma/Cma.md) | Cumulative Moving Average | Expanding window average |
| [**COINTEGRATION**](../lib/statistics/cointegration/Cointegration.md) | Cointegration | Engle-Granger two-step with ADF test |
| [**CORRELATION**](../lib/statistics/correlation/Correlation.md) | Pearson Correlation | Linear relationship [-1, +1] |
| [**COVARIANCE**](../lib/statistics/covariance/Covariance.md) | Covariance | Joint variability measure |
| [**ENTROPY**](../lib/statistics/entropy/Entropy.md) | Shannon Entropy | Information content via histogram binning |
| [**GEOMEAN**](../lib/statistics/geomean/Geomean.md) | Geometric Mean | Rolling geometric mean via log-sum |
| [**GRANGER**](../lib/statistics/granger/Granger.md) | Granger Causality | F-statistic testing if X predicts Y |
| [**HARMEAN**](../lib/statistics/harmean/Harmean.md) | Harmonic Mean | Rolling harmonic mean via reciprocal-sum |
| [**HURST**](../lib/statistics/hurst/Hurst.md) | Hurst Exponent | Long-range dependence via R/S analysis |
| [**IQR**](../lib/statistics/iqr/Iqr.md) | Interquartile Range | Robust dispersion (Q3 - Q1) |
| [**JB**](../lib/statistics/jb/Jb.md) | Jarque-Bera Test | Normality test (skewness + kurtosis) |
| [**KENDALL**](../lib/statistics/kendall/Kendall.md) | Kendall Tau-a | Rank-based ordinal association |
| [**KURTOSIS**](../lib/statistics/kurtosis/Kurtosis.md) | Kurtosis | Fourth-moment tail heaviness |
| [**LINREG**](../lib/statistics/linreg/LinReg.md) | Linear Regression | Best-fit line via least squares |
| [**MEDIAN**](../lib/statistics/median/Median.md) | Rolling Median | 50th percentile |
| [**MODE**](../lib/statistics/mode/Mode.md) | Mode | Most frequent value in window |
| [**PACF**](../lib/statistics/pacf/Pacf.md) | Partial Autocorrelation | Direct correlation at lag k |
| [**PERCENTILE**](../lib/statistics/percentile/Percentile.md) | Percentile | Value at given percentile rank |
| [**QUANTILE**](../lib/statistics/quantile/Quantile.md) | Quantile | Value at given quantile (0-1) |
| [**SKEW**](../lib/statistics/skew/Skew.md) | Skewness | Distribution asymmetry |
| [**SPEARMAN**](../lib/statistics/spearman/Spearman.md) | Spearman Rank Correlation | Monotonic association [-1, +1] |
| [**STDDEV**](../lib/statistics/stddev/StdDev.md) | Standard Deviation | Dispersion measure |
| [**SUM**](../lib/statistics/sum/Sum.md) | Rolling Sum | Windowed sum |
| [**THEIL**](../lib/statistics/theil/Theil.md) | Theil T Index | Information-theoretic inequality |
| [**VARIANCE**](../lib/statistics/variance/Variance.md) | Variance | Squared deviation |
| [**ZSCORE**](../lib/statistics/zscore/Zscore.md) | Z-Score | Standard deviations from rolling mean |
| [**ZTEST**](../lib/statistics/ztest/Ztest.md) | Z-Test | One-sample t-statistic |

### Forecasts

Predictive indicators and extrapolation methods.

| Indicator | Full Name | Notes |
| :-------- | :-------- | :---- |
| [**AFIRMA**](../lib/forecasts/afirma/Afirma.md) | Adaptive FIR MA | Predictive FIR filter |

### Cycles

Periodic pattern detection and dominant frequency extraction. Markets exhibit cycles; detecting them reliably remains challenging.

| Indicator | Full Name | Notes |
| :-------- | :-------- | :---- |
| [**CG**](../lib/cycles/cg/Cg.md) | Center of Gravity | Ehlers cycle measurement |
| [**DSP**](../lib/cycles/dsp/Dsp.md) | Detrended Synthetic Price | Cycle-isolated price component |
| [**EACP**](../lib/cycles/eacp/Eacp.md) | Autocorrelation Periodogram | Ehlers dominant cycle detection |
| [**EBSW**](../lib/cycles/ebsw/Ebsw.md) | Even Better Sinewave | Ehlers improved cycle indicator |
| [**HOMOD**](../lib/cycles/homod/Homod.md) | Homodyne Discriminator | Dominant cycle period tracking |
| [**HT_DCPERIOD**](../lib/cycles/ht_dcperiod/HtDcperiod.md) | HT Dominant Cycle Period | Hilbert Transform period estimation |
| [**HT_DCPHASE**](../lib/cycles/ht_dcphase/HtDcphase.md) | HT Dominant Cycle Phase | Hilbert Transform phase angle |
| [**HT_PHASOR**](../lib/cycles/ht_phasor/HtPhasor.md) | HT Phasor Components | In-phase and quadrature components |
| [**HT_SINE**](../lib/cycles/ht_sine/HtSine.md) | HT SineWave | Dominant cycle phase with lead signal |
| [**LUNAR**](../lib/cycles/lunar/Lunar.md) | Lunar Phase | Moon phase cycle |
| [**SINE**](../lib/cycles/sine/Sine.md) | Sine Wave | Periodic sine oscillation |
| [**SOLAR**](../lib/cycles/solar/Solar.md) | Solar Activity Cycle | Solar activity periodicity |
| [**SSFDSP**](../lib/cycles/ssfdsp/Ssfdsp.md) | SSF Detrended Synthetic Price | Dual Super Smoother oscillator |
| [**STC**](../lib/cycles/stc/Stc.md) | Schaff Trend Cycle | MACD-based cycle oscillator |

### Numerics

Mathematical transformations and derivative indicators. Building blocks for analysis.

| Indicator | Full Name | Notes |
| :-------- | :-------- | :---- |
| [**ACCEL**](../lib/numerics/accel/Accel.md) | Acceleration (2nd Derivative) | Change in slope |
| [**CHANGE**](../lib/numerics/change/Change.md) | Percentage Change | Relative price movement |
| [**EXPTRANS**](../lib/numerics/exptrans/Exptrans.md) | Exponential Transform | e^x for log-space reversal |
| [**HIGHEST**](../lib/numerics/highest/Highest.md) | Rolling Maximum | O(1) via monotonic deque |
| [**JERK**](../lib/numerics/jerk/Jerk.md) | Jerk (3rd Derivative) | Change in acceleration |
| [**LINEARTRANS**](../lib/numerics/lineartrans/Lineartrans.md) | Linear Transform | y = ax + b scaling |
| [**LOGTRANS**](../lib/numerics/logtrans/Logtrans.md) | Logarithmic Transform | Natural log for percentage analysis |
| [**LOWEST**](../lib/numerics/lowest/Lowest.md) | Rolling Minimum | O(1) via monotonic deque |
| [**MIDPOINT**](../lib/numerics/midpoint/Midpoint.md) | Rolling Midpoint | (Highest + Lowest) / 2 |
| [**NORMALIZE**](../lib/numerics/normalize/Normalize.md) | Min-Max Normalization | Scale to [0,1] via rolling min/max |
| [**RELU**](../lib/numerics/relu/Relu.md) | Rectified Linear Unit | max(0, x) activation |
| [**SIGMOID**](../lib/numerics/sigmoid/Sigmoid.md) | Logistic Function | 1/(1+e^-x) bounded [0,1] |
| [**SLOPE**](../lib/numerics/slope/Slope.md) | Slope (1st Derivative) | Rate of change; velocity |
| [**SQRTTRANS**](../lib/numerics/sqrttrans/Sqrttrans.md) | Square Root Transform | √x variance-to-StdDev conversion |
| [**STANDARDIZE**](../lib/numerics/standardize/Standardize.md) | Z-Score Normalization | (x - mean) / StdDev scaling |

### Errors

Error metrics and loss functions for model evaluation, forecast assessment, and strategy validation. Quantifying wrongness before production quantifies losses.

| Indicator | Full Name | Notes |
| :-------- | :-------- | :---- |
| [**HUBER**](../lib/errors/huber/Huber.md) | Huber Loss | Quadratic for small errors, linear for large |
| [**LOGCOSH**](../lib/errors/logcosh/LogCosh.md) | Log-Cosh Loss | Smooth Huber approximation |
| [**MAAPE**](../lib/errors/maape/Maape.md) | Mean Arctangent APE | Bounded percentage error |
| [**MAE**](../lib/errors/mae/Mae.md) | Mean Absolute Error | Average absolute deviation |
| [**MAPD**](../lib/errors/mapd/Mapd.md) | Mean Absolute % Deviation | Percentage deviation from mean |
| [**MAPE**](../lib/errors/mape/Mape.md) | Mean Absolute % Error | Percentage prediction error |
| [**MASE**](../lib/errors/mase/Mase.md) | Mean Absolute Scaled Error | Scale-independent accuracy |
| [**MDAE**](../lib/errors/mdae/Mdae.md) | Median Absolute Error | Robust central error |
| [**MDAPE**](../lib/errors/mdape/Mdape.md) | Median Absolute % Error | Robust percentage error |
| [**ME**](../lib/errors/me/Me.md) | Mean Error | Bias direction indicator |
| [**MPE**](../lib/errors/mpe/Mpe.md) | Mean Percentage Error | Percentage bias measure |
| [**MRAE**](../lib/errors/mrae/Mrae.md) | Mean Relative Absolute Error | Benchmark-relative error |
| [**MSE**](../lib/errors/mse/Mse.md) | Mean Squared Error | Variance of residuals |
| [**MSLE**](../lib/errors/msle/Msle.md) | Mean Squared Log Error | Ratio-sensitive error |
| [**PSEUDOHUBER**](../lib/errors/pseudohuber/PseudoHuber.md) | Pseudo-Huber Loss | Differentiable Huber approximation |
| [**QUANTILELOSS**](../lib/errors/quantile/QuantileLoss.md) | Quantile Loss | Asymmetric pinball loss |
| [**RAE**](../lib/errors/rae/Rae.md) | Relative Absolute Error | MAE relative to baseline |
| [**RMSE**](../lib/errors/rmse/Rmse.md) | Root Mean Squared Error | Standard error magnitude |
| [**RMSLE**](../lib/errors/rmsle/Rmsle.md) | Root Mean Squared Log Error | Ratio-sensitive RMSE |
| [**RSE**](../lib/errors/rse/Rse.md) | Relative Squared Error | MSE relative to baseline |
| [**RSQUARED**](../lib/errors/rsquared/Rsquared.md) | R² (Coefficient of Determination) | Explained variance fraction |
| [**SMAPE**](../lib/errors/smape/Smape.md) | Symmetric MAPE | Symmetric percentage error |
| [**THEILU**](../lib/errors/theilu/TheilU.md) | Theil's U Statistic | Forecast accuracy relative to naive |
| [**TUKEY**](../lib/errors/tukey/TukeyBiweight.md) | Tukey Biweight Loss | Robust regression loss |
| [**WMAPE**](../lib/errors/wmape/Wmape.md) | Weighted MAPE | Volume-weighted percentage error |
| [**WRMSE**](../lib/errors/wrmse/Wrmse.md) | Weighted RMSE | Observation-weighted RMSE |

### Reversals

Reversal indicators identify potential turning points, compute adaptive stop levels, and define support/resistance zones. Where trend indicators tell you what is happening, reversal indicators warn you when it might stop.

| Indicator | Full Name | Notes |
| :-------- | :-------- | :---- |
| [**CHANDELIER**](../lib/reversals/chandelier/Chandelier.md) | Chandelier Exit | ATR-based trailing stops from HH/LL; dual ExitLong/ExitShort |
| [**CKSTOP**](../lib/reversals/ckstop/Ckstop.md) | Chande Kroll Stop | ATR-based adaptive trailing stops; dual StopLong/StopShort levels |
| [**FRACTALS**](../lib/reversals/fractals/Fractals.md) | Williams Fractals | Five-bar pattern detecting local highs/lows; dual UpFractal/DownFractal |
| [**PIVOT**](../lib/reversals/pivot/Pivot.md) | Classic Pivot Points | Floor trader pivots: 7 levels (PP, R1-R3, S1-S3) from previous bar's HLC |
| [**PIVOTCAM**](../lib/reversals/pivotcam/Pivotcam.md) | Camarilla Pivot Points | Close-centric pivots: 9 levels (PP, R1-R4, S1-S4); R3/S3 mean-reversion zones |
| [**PIVOTDEM**](../lib/reversals/pivotdem/Pivotdem.md) | DeMark Pivot Points | Conditional pivots: 3 levels (PP, R1, S1); weights OHLC by bar direction |
| [**PIVOTEXT**](../lib/reversals/pivotext/Pivotext.md) | Extended Traditional Pivots | Extended pivots: 11 levels (PP, R1-R5, S1-S5); classic formula with R4/R5/S4/S5 |
| [**PIVOTFIB**](../lib/reversals/pivotfib/Pivotfib.md) | Fibonacci Pivot Points | Fibonacci pivots: 7 levels (PP, R1-R3, S1-S3); ratios 0.382/0.618/1.000 applied to range |
| [**PIVOTWOOD**](../lib/reversals/pivotwood/Pivotwood.md) | Woodie's Pivot Points | Close-weighted pivots: 7 levels (PP, R1-R3, S1-S3); PP = (H+L+2C)/4 biased toward close |
| [**PSAR**](../lib/reversals/psar/Psar.md) | Parabolic Stop And Reverse | Accelerating trailing stop; SAR dots flip on reversal; Welles Wilder (1978) |
| [**SWINGS**](../lib/reversals/swings/Swings.md) | Swing High/Low Detection | Configurable-lookback pattern detector; dual SwingHigh/SwingLow with persistent levels |
| [**TTM_SCALPER**](../lib/reversals/ttm_scalper/TtmScalper.md) | TTM Scalper Alert | 3-bar pivot high/low detection for scalping entries; John Carter |
