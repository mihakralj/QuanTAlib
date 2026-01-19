# Validation Across TA Libraries

> "Trust, but verify." — Russian proverb (applicable to both Cold War diplomacy and technical indicator libraries)

Every indicator implementation makes implicit claims about correctness. QuanTAlib validates these claims by comparing outputs against established libraries: TA-Lib, Tulip, Skender.Stock.Indicators, and OoplesFinance. Where implementations diverge, the differences get documented.

## Reading the Matrix

| Symbol | Meaning |
| :----: | :------ |
| ✔️ | Validated: outputs match within floating-point tolerance (1e-9) |
| ⚠️ | Partial match: minor discrepancies documented in indicator notes |
| ❔ | Implementation exists but not validated |
| - | No implementation in that library |

**Tolerance rationale:** Financial data uses double precision. Differences below 1e-9 stem from floating-point arithmetic order, not algorithmic divergence.

## Validation Philosophy

Three levels of confidence:

**Level 1: Cross-Library Agreement**
Multiple independent implementations produce identical results. Highest confidence. Most mainstream indicators (SMA, EMA, RSI, MACD) fall here.

**Level 2: Original Source Agreement**
No cross-library validation available, but implementation matches original research paper or patent description. JMA, various proprietary indicators fall here.

**Level 3: Mathematical Correctness Only**
No external reference exists. Implementation verified through unit tests, edge case handling, and mathematical properties (e.g., filter stability, energy preservation). Novel or obscure indicators fall here.

## Technical Indicators

| Indicator | QuanTAlib | TA-Lib | Tulip | Skender | Ooples |
| :-------- | :-------- | :----: | :---: | :-----: | :----: |
| **Aberration Bands** | [Abber](../lib/channels/abber/abber.md) | - | - | - | - |
| **Absolute Price Oscillator** | [Apo](../lib/momentum/apo/apo.md) | ✔️ | ✔️ | - | ✔️ |
| **Acceleration Bands** | [AccBands](../lib/channels/accbands/accbands.md) | - | - | - | - |
| **Acceleration Oscillator** | Ac | - | - | - | ❔ |
| **Accumulation/Distribution Line** | [Adl](../lib/volume/adl/adl.md) | ✔️ | ✔️ | ✔️ | ✔️ |
| **Accumulation/Distribution Oscillator** | [Adosc](../lib/volume/adosc/adosc.md) | ✔️ | ✔️ | ✔️ | ✔️ |
| **Adaptive Price Zone** | [Apz](../lib/channels/apz/apz.md) | - | - | - | ❔ |
| **Andrews' Pitchfork** | Apchannel | - | - | - | - |
| **Archer Moving Averages Trends** | [Amat](../lib/momentum/amat/Amat.md) | - | - | ✔️ | ✔️ |
| **Archer On-Balance Volume** | Aobv | - | - | - | - |
| **Arnaud Legoux Moving Average** | [Alma](../lib/trends/alma/alma.md) | - | - | ✔️ | ✔️ |
| **Aroon** | [Aroon](../lib/momentum/aroon/aroon.md) | ✔️ | ✔️ | ✔️ | - |
| **Aroon Oscillator** | [AroonOsc](../lib/momentum/aroonosc/AroonOsc.md) | ✔️ | ✔️ | ✔️ | - |
| **ATR Bands** | Atrbands | - | - | - | ❔ |
| **Adaptive FIR Moving Average** | [Afirma](../lib/forecasts/afirma/Afirma.md) | - | - | - | - |
| **Average Daily Range** | [Adr](../lib/volatility/adr/Adr.md) | - | - | - | - |
| **Average Directional Index** | [Adx](../lib/momentum/adx/adx.md) | ✔️ | ✔️ | ✔️ | ✔️ |
| **Average Directional Movement Rating** | [Adxr](../lib/momentum/adxr/Adxr.md) | ✔️ | ✔️ | - | - |
| **Average True Range** | [Atr](../lib/volatility/atr/atr.md) | ✔️ | ✔️ | ✔️ | ✔️ |
| **Average True Range Normalized [0,1]** | [Atrn](../lib/volatility/atrn/Atrn.md) | - | - | - | - |
| **Average True Range Percent** | [Atrp](../lib/volatility/atrp/Atrp.md) | ✔️ | ✔️ | ✔️ | ✔️ |
| **Awesome Oscillator** | [Ao](../lib/momentum/ao/ao.md) | - | ✔️ | ✔️ | ✔️ |
| **Balance of Power** | [Bop](../lib/momentum/bop/Bop.md) | ✔️ | ✔️ | ✔️ | ✔️ |
| **Bessel Filter** | [Bessel](../lib/trends/bessel/Bessel.md) | - | - | - | - |
| **Bessel-Weighted MA** | [Bwma](../lib/trends_FIR/bwma/Bwma.md) | - | - | - | - |
| **Beta Coefficient** | [Beta](../lib/statistics/beta/Beta.md) | ✔️ | - | ✔️ | - |
| **Bias** | Bias | - | - | - | - |
| **Bilateral Filter** | [Bilateral](../lib/trends/bilateral/Bilateral.md) | - | - | - | - |
| **Blackman Window MA** | [Blma](../lib/trends/blma/Blma.md) | - | - | - | - |
| **Bollinger %B** | Bbb | - | - | - | ❔ |
| **Bollinger Band Squeeze** | Bbs | - | - | - | - |
| **Bollinger Band Width** | Bbw | - | - | - | ❔ |
| **Bollinger Band Width Normalized** | Bbwn | - | - | - | - |
| **Bollinger Band Width Percentile** | Bbwp | - | - | - | - |
| **Bollinger Bands** | Bbands | ✔️ | ✔️ | ✔️ | ❔ |
| **Butterworth Filter** | [Butter](../lib/trends/butter/Butter.md) | - | - | - | ✔️ |
| **Camarilla Pivot Points** | Pivotcam | - | - | - | ❔ |
| **Chaikin Money Flow** | Cmf | - | - | ✔️ | ❔ |
| **Chaikin Volatility** | Cvi | - | ✔️ | - | ❔ |
| **Chande Forecast Oscillator** | Cfo | - | - | - | ❔ |
| **Chande Momentum Oscillator** | Cmo | ✔️ | ✔️ | ✔️ | ❔ |
| **Chebyshev Type I Filter** | Cheby1 | - | - | - | - |
| **Chebyshev Type II Filter** | Cheby2 | - | - | - | - |
| **Choppiness Index** | Chop | - | - | ✔️ | ❔ |
| **Close-to-Close Volatility** | Ccv | - | - | - | - |
| **Cointegration** | Cointegration | - | - | - | - |
| **Commodity Channel Index** | Cci | ✔️ | ✔️ | ✔️ | ❔ |
| **Composite Fractal Behavior** | [Cfb](../lib/momentum/cfb/cfb.md) | - | - | - | - |
| **Conditional Volatility** | Cv | - | - | - | - |
| **Convolution Moving Average** | [Conv](../lib/trends/conv/conv.md) | ✔️ | ✔️ | ✔️ | ✔️ |
| **Correlation** | Correlation | ✔️ | - | ✔️ | - |
| **Cumulative Moving Average** | [Cma](../lib/statistics/cma/Cma.md) | - | - | - | - |
| **Decay Min-Max Channel** | Decaychannel | - | - | - | - |
| **DeMark Pivot Points** | Pivotdem | - | - | - | ❔ |
| **Detrended Price Oscillator** | Dpo | - | ✔️ | ✔️ | ❔ |
| **Detrended Synthetic Price** | Dsp | - | - | - | ❔ |
| **Deviation-Scaled MA** | Dsma | - | - | - | ❔ |
| **Directional Movement Index** | Dx | ✔️ | ✔️ | - | - |
| **Directional Movement Index (Jurik)** | [Dmx](../lib/momentum/dmx/dmx.md) | - | - | - | - |
| **Dirty Data Detection** | Dirty | - | - | - | - |
| **Donchian Channels** | Dchannel | - | - | ✔️ | ❔ |
| **Double Exponential Moving Average** | [Dema](../lib/trends/dema/dema.md) | ✔️ | ✔️ | ✔️ | ✔️ |
| **Double Weighted Moving Average** | [Dwma](../lib/trends/dwma/dwma.md) | - | - | - | - |
| **Ease of Movement** | Eome | - | - | - | ❔ |
| **Ehlers Autocorrelation Periodogram** | Eacp | - | - | - | ❔ |
| **BandPass Filter** | [Bpf](../lib/filters/bpf/Bpf.md) | ✔️ | - | - | - |
| **Ehlers Center of Gravity** | Cg | - | - | - | ❔ |
| **Ehlers Even Better Sinewave** | Ebsw | - | - | - | ❔ |
| **Ehlers Fractal Adaptive MA** | [Frama](../lib/trends_IIR/frama/Frama.md) | - | - | - | ❔ |
| **Ehlers Highpass Filter** | [Hpf](../lib/filters/hpf/Hpf.md) | - | - | - | ❔ |
| **Ehlers Phasor Analysis** | Phasor | - | - | - | - |
| **Ehlers Sine Wave** | Sine | - | - | - | ❔ |
| **Ehlers SSF-Based Detrended Synthetic Price** | Ssfdsp | - | - | - | - |
| **Ehlers Super Smooth Filter** | [Ssf](../lib/trends/ssf/Ssf.md) | - | - | - | ✔️ |
| **Ehlers Ultrasmooth Filter** | Usf | - | - | - | - |
| **Elliptic (Cauer) Filter** | [Elliptic](../lib/filters/elliptic/Elliptic.md) | - | - | - | ❔ |
| **Exponential Moving Average** | [Ema](../lib/trends/ema/ema.md) | ✔️ | ✔️ | ✔️ | ✔️ |
| **Exponential Transformation** | Exptrans | - | - | - | - |
| **Exponential Weighted MA Volatility** | Ewma | - | - | - | - |
| **Extended Traditional Pivots** | Pivotext | - | - | - | - |
| **Fibonacci Pivot Points** | Pivotfib | - | - | - | ❔ |
| **Fisher Transform** | Fisher | - | ✔️ | ✔️ | ❔ |
| **Force Index** | Efi | - | - | ✔️ | ❔ |
| **Fractal Chaos Bands** | Fcb | - | - | ✔️ | ❔ |
| **Garman-Klass Volatility** | Gkv | - | - | - | ❔ |
| **Gaussian Filter** | [Gauss](../lib/filters/gauss/Gauss.md) | - | - | - | ❔ |
| **Gaussian-Weighted MA** | Gwma | - | - | - | - |
| **Geometric Mean** | Geomean | - | - | - | - |
| **Granger Causality Test** | Granger | - | - | - | - |
| **Hamming Window MA** | Hamma | - | - | - | ❔ |
| **Hann FIR Filter** | [Hann](../lib/filters/hann/Hann.md) | - | - | - | - |
| **Hanning Window MA** | Hanma | - | - | - | ❔ |
| **Harmonic Mean** | Harmean | - | - | - | - |
| **High-Low Volatility** | Hlv | - | - | - | - |
| **Highest value** | [Highest](../lib/numerics/highest/Highest.md) | ✔️ | ✔️ | - | - |
| **Hilbert Transform Dominant Cycle Period** | Ht_dcperiod | ✔️ | - | - | - |
| **Hilbert Transform Dominant Cycle Phase** | Ht_dcphase | ✔️ | - | - | - |
| **Hilbert Transform Instantaneous Trend** | [Htit](../lib/trends/htit/htit.md) | ✔️ | - | ✔️ | ✔️ |
| **Hilbert Transform Phasor** | Ht_phasor | ✔️ | - | - | - |
| **Hilbert Transform Sine Wave** | Ht_sine | ✔️ | ✔️ | - | - |
| **Hilbert Transform Trend Mode** | Ht_trendmode | ✔️ | - | - | - |
| **Historical Volatility** | Hv | - | - | - | ❔ |
| **Hodrick-Prescott Filter** | [Hp](../lib/filters/hp/Hp.md) | - | - | - | - |
| **Holt Weighted MA** | Hwma | - | - | - | ❔ |
| **Homodyne Discriminator Dominant Cycle** | Homod | - | - | - | ❔ |
| **Huber Loss** | Huber | - | - | - | - |
| **Hull Exponential MA** | [Hema](../lib/trends_IIR/hema/Hema.md) | - | - | - | - |
| **Hull Moving Average** | [Hma](../lib/trends/hma/hma.md) | - | ✔️ | ✔️ | [⚠️](../lib/trends/hma/hma.md#external-library-discrepancies) |
| **Hurst Exponent** | Hurst | - | - | ✔️ | ❔ |
| **Ichimoku Cloud** | Ichimoku | - | - | ✔️ | ❔ |
| **Inertia** | Inertia | - | - | - | ❔ |
| **Interquartile Range** | Iqr | - | - | - | - |
| **Intraday Intensity Index** | Iii | - | - | - | - |
| **Intraday Momentum Index** | Imi | - | - | - | ❔ |
| **Jarque-Bera Test** | Jb | - | - | - | - |
| **Jurik Moving Average** | [Jma](../lib/trends/jma/jma.md) | - | - | - | ❔ |
| **Jurik Volatility** | Jvolty | - | - | - | - |
| **Jurik Volatility Bands** | Jbands | - | - | - | - |
| **Jurik Volatility Normalized [0,1]** | Jvoltyn | - | - | - | - |
| **Kalman Filter** | [Kalman](../lib/filters/kalman/Kalman.md) | - | - | - | - |
| **Kaufman Adaptive Moving Average** | [Kama](../lib/trends/kama/kama.md) | ✔️ | ✔️ | ✔️ | ✔️ |
| **KDJ Indicator** | Kdj | - | - | - | - |
| **Keltner Channel** | Kchannel | - | - | ✔️ | ❔ |
| **Kendall Rank Correlation** | Kendall | - | - | - | ❔ |
| **Klinger Volume Oscillator** | Kvo | - | ✔️ | ✔️ | ❔ |
| **Kurtosis** | Kurtosis | - | - | - | ❔ |
| **Least Squares Moving Average** | [Lsma](../lib/trends/lsma/lsma.md) | ✔️ | - | ✔️ | ❔ |
| **Linear Regression** | [LinReg](../lib/statistics/linreg/LinReg.md) | ✔️ | ✔️ | ✔️ | [⚠️](../lib/statistics/linreg/LinReg.md#validation) |
| **Linear Transformation** | Lineartrans | - | - | - | - |
| **Linear Trend MA** | Ltma | - | - | - | - |
| **LOESS/LOWESS Smoothing** | [Loess](../lib/filters/loess/Loess.md) | - | - | - | - |
| **Logarithmic Transformation** | Logtrans | - | - | - | - |
| **Logistic Function** | [Sigmoid](../lib/numerics/sigmoid/Sigmoid.md) | - | - | - | - |
| **Lowest value** | [Lowest](../lib/numerics/lowest/Lowest.md) | ✔️ | ✔️ | - | - |
| **Lunar Phase** | Lunar | - | - | - | - |
| **Lowest value** | [Lowest](../lib/numerics/lowest/Lowest.md) | ✔️ | ✔️ | - | - |
| **Lunar Phase** | Lunar | - | - | - | - |
| **Mass Index** | Mass | - | ✔️ | - | ❔ |
| **McGinley Dynamic** | [Mgdi](../lib/trends/mgdi/mgdi.md) | - | - | ✔️ | ✔️ |
| **Mean Absolute Error** | Mae | - | - | - | - |
| **Mean Absolute Percentage Difference** | Mapd | - | - | - | - |
| **Mean Absolute Percentage Error** | Mape | - | - | - | - |
| **Mean Absolute Scaled Error** | Mase | - | - | - | - |
| **Mean Error** | Me | - | - | - | - |
| **Mean Percentage Error** | Mpe | - | - | - | - |
| **Mean Squared Error** | Mse | - | - | - | - |
| **Mean Squared Logarithmic Error** | Msle | - | - | - | - |
| **MESA Adaptive Moving Average** | [Mama](../lib/trends/mama/mama.md) | ✔️ | - | ✔️ | ✔️ |
| **Midpoint** | [Midpoint](../lib/numerics/midpoint/Midpoint.md) | ✔️ | - | - | - |
| **Min-Max Channel** | Mmchannel | - | - | - | - |
| **Min-Max Scaling (Normalization)** | [Normalize](../lib/numerics/normalize/Normalize.md) | - | - | - | - |
| **Mode (Most Frequent)** | Mode | - | - | - | - |
| **Modified MA** | [Mma](../lib/trends_IIR/mma/Mma.md) | - | - | - | - |
| **Momentum** | Mom | ✔️ | ✔️ | - | ❔ |
| **Momentum change; 2nd derivative** | Accel | - | - | - | - |
| **Money Flow Index** | Mfi | ✔️ | ✔️ | ✔️ | ❔ |
| **Moon Phase** | Moon | - | - | - | - |
| **Moving Average Convergence/Divergence** | [Macd](../lib/momentum/macd/Macd.md) | ✔️ | ✔️ | ✔️ | ❔ |
| **Moving Average Envelopes** | Maenv | - | - | ✔️ | ❔ |
| **Negative Volume Index** | Nvi | - | ✔️ | - | ❔ |
| **Normalized Average True Range** | Natr | ✔️ | ✔️ | - | - |
| **Normalized Shannon Entropy** | Entropy | - | - | - | - |
| **Notch Filter** | [Notch](../lib/filters/notch/Notch.md) | - | - | - | - |
| **On Balance Volume** | Obv | ✔️ | ✔️ | ✔️ | ❔ |
| **Parabolic SAR** | Psar | ✔️ | ✔️ | ✔️ | ❔ |
| **Parkinson Volatility** | Pv | - | - | - | - |
| **Pascal Weighted Moving Average** | [Pwma](../lib/trends/pwma/pwma.md) | - | - | - | - |
| **Percentage Change** | [Change](../lib/numerics/change/Change.md) | ✔️ | - | - | - |
| **Percentage Price Oscillator** | Ppo | ✔️ | ✔️ | - | ❔ |
| **Percentage Volume Oscillator** | Pvo | - | - | ✔️ | ❔ |
| **Percentile** | Percentile | - | - | - | - |
| **Pivot Points** | Pivot | - | - | ✔️ | ❔ |
| **Positive Volume Index** | Pvi | - | ✔️ | - | ❔ |
| **Pretty Good Oscillator** | Pgo | - | - | - | ❔ |
| **Price Channel** | Pchannel | - | - | - | ❔ |
| **Price Momentum Oscillator** | Pmo | - | - | ✔️ | ❔ |
| **Price Relative Strength** | Prs | - | - | ✔️ | - |
| **Price Volume Divergence** | Pvd | - | - | - | - |
| **Price Volume Rank** | Pvr | - | - | - | ❔ |
| **Price Volume Trend** | Pvt | - | - | - | ❔ |
| **Qstick Indicator** | Qstick | - | - | - | ❔ |
| **Quad Exponential MA** | [Qema](../lib/trends_IIR/qema/Qema.md) | - | - | - | - |
| **Quantile** | Quantile | - | - | - | - |
| **Rate of acceleration; 3rd derivative** | [Jerk](../lib/numerics/jerk/Jerk.md) | - | - | - | - |
| **Rate of Change** | [Roc](../lib/momentum/roc/Roc.md) | ✔️ | ✔️ | ✔️ | ❔ |
| **Rate of change; 1st derivative** | [Slope](../lib/statistics/linreg/LinReg.md) | ✔️ | ✔️ | ✔️ | ❔ |
| **Rate of Change Percentage** | Rocp | ✔️ | - | - | - |
| **Rate of Change Ratio** | Rocr | ✔️ | ✔️ | - | - |
| **Realized Volatility** | Rv | - | - | - | - |
| **Rectified Linear Unit** | [Relu](../lib/numerics/relu/Relu.md) | - | - | - | - |
| **Recursive Gaussian MA** | [Rgma](../lib/trends_IIR/rgma/Rgma.md) | - | - | - | - |
| **Regression Channels** | Regchannel | - | - | - | - |
| **Regularized Exponential MA** | [Rema](../lib/trends_IIR/rema/Rema.md) | - | - | - | ❔ |
| **Relative Absolute Error** | Rae | - | - | - | - |
| **Relative Squared Error** | Rse | - | - | - | - |
| **Relative Strength Index** | [Rsi](../lib/momentum/rsi/Rsi.md) | ✔️ | ✔️ | ✔️ | ✔️ |
| **Relative Strength Quality Index** | [Rsx](../lib/momentum/rsx/rsx.md) | - | - | - | ❔ |
| **Relative Volatility Index** | Rvi | - | - | - | ❔ |
| **Renko** | - | - | - | ✔️ | - |
| **Rogers-Satchell Volatility** | Rsv | - | - | - | - |
| **Root Mean Squared Error** | Rmse | - | - | - | - |
| **Root Mean Squared Logarithmic Error** | Rmsle | - | - | - | - |
| **R-Squared** | [RSquared](../lib/statistics/linreg/LinReg.md) | - | - | ✔️ | ❔ |
| **Savitzky-Golay Filter** | [Sgf](../lib/filters/sgf/Sgf.md) | - | - | - | - |
| **Savitzky-Golay MA** | [Sgma](../lib/trends_FIR/sgma/Sgma.md) | - | - | - | - |
| **Schaff Trend Cycle** | [Stc](../lib/cycles/stc/Stc.md) | - | - | ✔️ | ❔ |
| **Simple Moving Average** | [Sma](../lib/trends/sma/sma.md) | ✔️ | ✔️ | ✔️ | ✔️ |
| **Sine-weighted MA** | [Sinema](../lib/trends_FIR/sinema/Sinema.md) | - | - | - | - |
| **Smoothed Moving Average** | [Rma](../lib/trends/rma/rma.md) | - | ✔️ | ✔️ | ✔️ |
| **Solar Activity Cycle** | Solar | - | - | - | - |
| **Spearman Rank Correlation** | Spearman | - | - | - | ❔ |
| **Square Root Transformation** | [Sqrttrans](../lib/numerics/sqrttrans/Sqrttrans.md) | - | - | - | - |
| **Standard Deviation Channel** | Sdchannel | - | - | - | ❔ |
| **Standardization (Z-score)** | Standardize | - | - | - | ❔ |
| **Starc Bands** | Starc | - | - | - | - |
| **Stochastic Fast** | Stochf | ✔️ | - | - | ❔ |
| **Stochastic Momentum Index** | Smi | - | - | ✔️ | ❔ |
| **Stochastic Oscillator** | Stoch | ✔️ | ✔️ | ✔️ | ❔ |
| **Stochastic RSI** | Stochrsi | ✔️ | ✔️ | ✔️ | ❔ |
| **Stoller Average Range Channel** | Starchannel | - | - | - | ❔ |
| **Super Trend Bands** | Stbands | - | - | - | - |
| **SuperTrend** | [Super](../lib/trends/super/super.md) | - | - | ✔️ | ❔ |
| **Swing High/Low Detection** | Swings | - | - | - | - |
| **Symmetric Mean Absolute Percentage Error** | Smape | - | - | - | - |
| **T3 Moving Average** | [T3](../lib/trends/t3/t3.md) | ✔️ | - | ✔️ | ✔️ |
| **Theil Index** | Theil | - | - | - | - |
| **Time Series Forecast** | Tsf | ✔️ | ✔️ | - | ❔ |
| **Time Weighted Average Price** | Twap | - | - | - | - |
| **Trade Volume Index** | Tvi | - | - | - | ❔ |
| **Triangular Moving Average** | [Trima](../lib/trends/trima/trima.md) | ✔️ | ✔️ | ✔️ | ❔ |
| **Triple Exponential Average** | Trix | ✔️ | ✔️ | ✔️ | ❔ |
| **Triple Exponential Moving Average** | [Tema](../lib/trends/tema/tema.md) | ✔️ | ✔️ | ✔️ | ❔ |
| **True Range** | Tr | ✔️ | ✔️ | ✔️ | - |
| **True Strength Index** | Tsi | - | - | ✔️ | ❔ |
| **TTM Trend** | Ttm | - | - | - | - |
| **Two-Argument Arctangent** | Atan2 | - | - | - | - |
| **Ulcer Index** | Ui | - | - | ✔️ | ❔ |
| **Ultimate Bands** | Ubands | - | - | - | ❔ |
| **Ultimate Channel** | Uchannel | - | - | - | - |
| **Ultimate Oscillator** | [Ultosc](../lib/momentum/ultosc/Ultosc.md) | ✔️ | ✔️ | ✔️ | ✔️ |
| **Variable Index Dynamic Average** | [Vidya](../lib/trends/vidya/vidya.md) | - | ✔️ | - | ❔ |
| **Velocity (Jurik)** | [Vel](../lib/momentum/vel/vel.md) | - | - | - | - |
| **Volatility Adjusted Moving Average** | [Vama](../lib/trends_IIR/vama/Vama.md) | - | - | - | ❔ |
| **Volatility of Volatility** | Vov | - | - | - | - |
| **Volatility Ratio** | Vr | - | - | - | ❔ |
| **Volume Accumulation** | Va | - | - | - | ❔ |
| **Volume Force** | Vf | - | - | - | - |
| **Volume Oscillator** | Vo | - | ✔️ | - | - |
| **Volume Rate of Change** | Vroc | - | - | - | - |
| **Volume Weighted Accumulation/Distribution** | Vwad | - | - | - | - |
| **Volume Weighted Average Price** | Vwap | - | - | ✔️ | ❔ |
| **Volume Weighted Moving Average** | Vwma | - | ✔️ | ✔️ | ❔ |
| **Vortex Indicator** | Vortex | - | - | ✔️ | ❔ |
| **VWAP Bands** | Vwapbands | - | - | - | - |
| **VWAP with Standard Deviation Bands** | Vwapsd | - | - | - | - |
| **Weighted Moving Average** | [Wma](../lib/trends/wma/wma.md) | ✔️ | ✔️ | ✔️ | ✔️ |
| **Wiener Filter** | Wiener | - | - | - | - |
| **Williams %R** | Willr | ✔️ | ✔️ | ✔️ | ❔ |
| **Williams Accumulation/Distribution** | Wad | - | ✔️ | - | ❔ |
| **Williams Alligator** | Alligator | - | - | ✔️ | ❔ |
| **Williams Fractal** | Fractals | - | - | ✔️ | ❔ |
| **Woodie's Pivot Points** | Pivotwood | - | - | - | ❔ |
| **Yang-Zhang Volatility** | Yzv | - | - | - | - |
| **Yang-Zhang Volatility Adjusted MA** | [Yzvama](../lib/trends_IIR/yzvama/Yzvama.md) | - | - | - | - |
| **Zero-Lag Double Exponential MA** | Zldema | - | - | - | - |
| **Zero-Lag Exponential Moving Average** | [Zlema](../lib/trends_IIR/zlema/Zlema.md) | - | ✔️ | - | ❔ |
| **Zero-Lag Triple Exponential MA** | Zltema | - | - | - | ❔ |
| **ZigZag** | - | - | - | ✔️ | - |
| **Z-score standardization** | Zscore | - | - | - | ❔ |
| **Z-Test** | Ztest | - | - | - | - |

## Statistical Indicators

| Indicator | QuanTAlib | MathNet | TA-Lib | Tulip | Skender |
| :-------- | :-------- | :-----: | :----: | :---: | :-----: |
| **Covariance** | [Covariance](../lib/statistics/covariance/Covariance.md) | - | - | - | - |
| **Median (Statistical)** | [Median](../lib/statistics/median/Median.md) | ✔️ | - | - | - |
| **Skewness** | [Skew](../lib/statistics/skew/Skew.md) | ✔️ | - | - | - |
| **Standard Deviation** | [StdDev](../lib/statistics/stddev/StdDev.md) | ✔️ | ✔️ | ✔️ | ✔️ |
| **Sum (Rolling)** | [Sum](../lib/statistics/sum/Sum.md) | - | ✔️ | ✔️ | - |
| **Variance** | [Variance](../lib/statistics/variance/Variance.md) | ✔️ | ✔️ | ✔️ | ✔️ |

## Error Metrics

| Indicator | QuanTAlib | MathNet | Notes |
| :-------- | :-------- | :-----: | :---- |
| **Mean Absolute Error** | [Mae](../lib/errors/mae/Mae.md) | ✔️ | Validated via `Distance.MAE()` |
| **Mean Squared Error** | [Mse](../lib/errors/mse/Mse.md) | ✔️ | Validated via `Distance.MSE()` |
| **Root Mean Squared Error** | [Rmse](../lib/errors/rmse/Rmse.md) | ✔️ | Validated via `sqrt(Distance.MSE())` |
| **R-Squared** | [Rsquared](../lib/errors/rsquared/Rsquared.md) | - | Uses streaming-optimized TSS calculation |
| **Huber Loss** | [Huber](../lib/errors/huber/Huber.md) | - | No external validation available |
| **Pseudo-Huber Loss** | [PseudoHuber](../lib/errors/pseudohuber/PseudoHuber.md) | - | No external validation available |
| **Log-Cosh Loss** | [LogCosh](../lib/errors/logcosh/LogCosh.md) | - | No external validation available |
| **Tukey Loss** | [Tukey](../lib/errors/tukey/Tukey.md) | - | No external validation available |
| **Quantile Loss** | [Quantile](../lib/errors/quantile/Quantile.md) | - | No external validation available |
| **MAPE** | [Mape](../lib/errors/mape/Mape.md) | - | No external validation available |
| **SMAPE** | [Smape](../lib/errors/smape/Smape.md) | - | No external validation available |
| **MAAPE** | [Maape](../lib/errors/maape/Maape.md) | - | No external validation available |
| **MASE** | [Mase](../lib/errors/mase/Mase.md) | - | No external validation available |
| **MSLE** | [Msle](../lib/errors/msle/Msle.md) | - | No external validation available |
| **RMSLE** | [Rmsle](../lib/errors/rmsle/Rmsle.md) | - | No external validation available |
| **Theil U** | [TheilU](../lib/errors/theilu/TheilU.md) | - | No external validation available |
| **Mean Error** | [Me](../lib/errors/me/Me.md) | - | No external validation available |
| **MPE** | [Mpe](../lib/errors/mpe/Mpe.md) | - | No external validation available |
| **RSE** | [Rse](../lib/errors/rse/Rse.md) | - | No external validation available |
| **RAE** | [Rae](../lib/errors/rae/Rae.md) | - | No external validation available |
| **MRAE** | [Mrae](../lib/errors/mrae/Mrae.md) | - | No external validation available |
| **MdAE** | [MdAE](../lib/errors/mdae/MdAE.md) | - | No external validation available |
| **MdAPE** | [MdAPE](../lib/errors/mdape/MdAPE.md) | - | No external validation available |
| **MAPD** | [Mapd](../lib/errors/mapd/Mapd.md) | - | No external validation available |
| **WMAPE** | [Wmape](../lib/errors/wmape/Wmape.md) | - | No external validation available |
| **WRMSE** | [Wrmse](../lib/errors/wrmse/Wrmse.md) | - | Validated via internal RMSE equivalence (uniform weights) |

## Validation Libraries

| Library | Language | License | Notes |
| :------ | :------- | :------ | :---- |
| [TA-Lib](https://ta-lib.org/) | C (via .NET wrapper) | BSD | Industry standard. C implementation, battle-tested. |
| [Tulip](https://tulipindicators.org/) | C (via .NET wrapper) | LGPL | Lightweight, well-documented. |
| [Skender.Stock.Indicators](https://dotnet.stockindicators.dev/) | C# | MIT | Pure .NET. Active development. |
| [OoplesFinance](https://github.com/ooples/OoplesFinance.StockIndicators) | C# | Apache 2.0 | Large indicator collection. Validation coverage varies. |
| [MathNet.Numerics](https://numerics.mathdotnet.com/) | C# | MIT | Statistical functions, not TA-specific. |

## Running Validation Tests

```bash
# All validation tests
dotnet test lib/QuanTAlib.Tests.csproj --filter "Category=Validation"

# Specific library comparison
dotnet test lib/QuanTAlib.Tests.csproj --filter "FullyQualifiedName~TalibValidation"
dotnet test lib/QuanTAlib.Tests.csproj --filter "FullyQualifiedName~SkenderValidation"

# Single indicator validation
dotnet test lib/QuanTAlib.Tests.csproj --filter "FullyQualifiedName~EmaValidation"
```

## Discrepancy Investigation

When validation fails:

1. **Check parameter mapping.** TA-Lib uses 0-based indexing for some parameters. Skender uses 1-based.
2. **Check warmup handling.** Different libraries handle the first N values differently.
3. **Check smoothing assumptions.** Some libraries use SMA for initial EMA seed. Others use the first value.
4. **Check edge cases.** NaN handling, zero division, and boundary conditions vary.

Discrepancies get documented in the indicator's markdown file under a "Validation Notes" section. The goal is not to match every library exactly. The goal is to understand why differences exist and document them.

## References

- [TA-Lib Documentation](https://ta-lib.org/d_api/d_api.html)
- [Skender.Stock.Indicators Wiki](https://dotnet.stockindicators.dev/guide/)
- [Tulip Indicators Reference](https://tulipindicators.org/list)