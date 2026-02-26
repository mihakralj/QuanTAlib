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
| **Acceleration Bands** | [AccBands](../lib/channels/accbands/accbands.md) | ✔️ | - | - | ❔ |
| **Acceleration Oscillator** | [Ac](../lib/oscillators/ac/Ac.md) | - | - | - | ❔ |
| **Accumulation/Distribution Line** | [Adl](../lib/volume/adl/adl.md) | ✔️ | ✔️ | ✔️ | ✔️ |
| **Accumulation/Distribution Oscillator** | [Adosc](../lib/volume/adosc/adosc.md) | ✔️ | ✔️ | ✔️ | ✔️ |
| **Adaptive Price Zone** | [Apz](../lib/channels/apz/apz.md) | - | - | - | ❔ |
| **Andrews' Pitchfork** | Apchannel | - | - | ✔️ | - |
| **Archer Moving Averages Trends** | [Amat](../lib/dynamics/amat/Amat.md) | - | - | ✔️ | ✔️ |
| **Archer On-Balance Volume** | [Aobv](../lib/volume/aobv/Aobv.md) | - | - | - | - |
| **Arnaud Legoux Moving Average** | [Alma](../lib/trends_FIR/alma/Alma.md) | - | - | ✔️ | ✔️ |
| **Aroon** | [Aroon](../lib/dynamics/aroon/Aroon.md) | ✔️ | ✔️ | ✔️ | - |
| **Aroon Oscillator** | [AroonOsc](../lib/dynamics/aroonosc/AroonOsc.md) | ✔️ | ✔️ | ✔️ | - |
| **ATR Bands** | Atrbands | ✔️ | - | ✔️ | ❔ |
| **Adaptive FIR Moving Average** | [Afirma](../lib/forecasts/afirma/Afirma.md) | - | - | - | - |
| **Ehlers Adaptive Laguerre Filter** | [ALaguerre](../lib/filters/alaguerre/ALaguerre.md) | - | - | - | - |
| **Ehlers Automatic Gain Control** | [Agc](../lib/filters/agc/Agc.md) | - | - | - | - |
| **Average Daily Range** | [Adr](../lib/volatility/adr/Adr.md) | - | - | - | - |
| **Average Directional Index** | [Adx](../lib/dynamics/adx/Adx.md) | ✔️ | ✔️ | ✔️ | ✔️ |
| **Average Directional Movement Rating** | [Adxr](../lib/dynamics/adxr/Adxr.md) | ✔️ | ✔️ | - | - |
| **Average True Range** | [Atr](../lib/volatility/atr/atr.md) | ✔️ | ✔️ | ✔️ | ✔️ |
| **Average True Range Normalized [0,1]** | [Atrn](../lib/volatility/atrn/Atrn.md) | - | - | ✔️ | - |
| **Average Price** | [Avgprice](../lib/core/avgprice/Avgprice.md) | ✔️ | - | - | - |
| **Awesome Oscillator** | [Ao](../lib/oscillators/ao/Ao.md) | - | ✔️ | ✔️ | ✔️ |
| **Balance of Power** | [Bop](../lib/momentum/bop/Bop.md) | ✔️ | ✔️ | ✔️ | ✔️ |
| **Baxter-King Band-Pass Filter** | [BaxterKing](../lib/filters/baxterking/BaxterKing.md) | - | - | - | - |
| **Christiano-Fitzgerald Filter** | [Cfitz](../lib/filters/cfitz/Cfitz.md) | - | - | - | - |
| **Bollinger Bands** | [Bbands](../lib/channels/bbands/Bbands.md) | ✔️ | ✔️ | ✔️ | ✔️ |
| **Bessel Filter** | [Bessel](../lib/filters/bessel/Bessel.md) | - | - | - | - |
| **Bessel-Weighted MA** | [Bwma](../lib/trends_FIR/bwma/Bwma.md) | - | - | - | - |
| **Beta Coefficient** | [Beta](../lib/statistics/beta/Beta.md) | ❔ | - | ✔️ | - |
| **Beta Distribution** | [Betadist](../lib/numerics/betadist/Betadist.md) | - | - | - | - |
| **Binomial Distribution** | [Binomdist](../lib/numerics/binomdist/Binomdist.md) | - | - | - | - |
| **Exponential Distribution** | [Expdist](../lib/numerics/expdist/Expdist.md) | - | - | - | - |
| **F-Distribution** | [Fdist](../lib/numerics/fdist/Fdist.md) | - | - | - | - |
| **Gamma Distribution** | [Gammadist](../lib/numerics/gammadist/Gammadist.md) | - | - | - | - |
| **Log-Normal Distribution** | [Lognormdist](../lib/numerics/lognormdist/Lognormdist.md) | - | - | - | - |
| **Normal Distribution** | [Normdist](../lib/numerics/normdist/Normdist.md) | - | - | - | - |
| **Poisson Distribution** | [Poissondist](../lib/numerics/poissondist/Poissondist.md) | - | - | - | - |
| **Student's t-Distribution** | [Tdist](../lib/numerics/tdist/Tdist.md) | - | - | - | - |
| **Weibull Distribution** | [Weibulldist](../lib/numerics/weibulldist/Weibulldist.md) | - | - | - | - |
| **Continuous Wavelet Transform** | [Cwt](../lib/numerics/cwt/Cwt.md) | - | - | - | - |
| **Discrete Wavelet Transform** | [Dwt](../lib/numerics/dwt/Dwt.md) | - | - | - | - |
| **Bias** | [Bias](../lib/momentum/bias/Bias.md) | - | - | - | - |
| **Bilateral Filter** | [Bilateral](../lib/filters/bilateral/Bilateral.md) | - | - | - | - |
| **Blackman Window MA** | [Blma](../lib/trends_FIR/blma/Blma.md) | - | - | - | - |
| **Bollinger %B** | [Bbb](../lib/oscillators/bbb/Bbb.md) | - | - | ✔️ | ❔ |
| **Bollinger Band Squeeze** | [Bbs](../lib/oscillators/bbs/Bbs.md) | - | - | ✔️ | ❔ |
| **Bollinger Band Width** | Bbw | - | - | ✔️ | ❔ |
| **Bollinger Band Width Normalized** | Bbwn | - | - | - | - |
| **Bollinger Band Width Percentile** | Bbwp | - | - | - | - |
| **Bollinger Bands** | Bbands | ✔️ | ✔️ | ✔️ | ❔ |
| **Ehlers 2-Pole Butterworth Filter** | [Butter2](../lib/filters/butter2/Butter2.md) | - | - | - | ✔️ |
| **Ehlers 3-Pole Butterworth Filter** | [Butter3](../lib/filters/butter3/Butter3.md) | - | - | - | - |
| **Camarilla Pivot Points** | [Pivotcam](../lib/reversals/pivotcam/Pivotcam.md) | - | - | - | ❔ |
| **Chandelier Exit** | [Chandelier](../lib/reversals/chandelier/Chandelier.md) | - | - | ✔️ | - |
| **Chande Kroll Stop** | [Ckstop](../lib/reversals/ckstop/Ckstop.md) | - | - | - | - |
| **Chaikin Money Flow** | Cmf | - | - | ✔️ | ❔ |
| **Chaikin Volatility** | [Cvi](../lib/volatility/cvi/Cvi.md) | - | ❔ | - | ❔ |
| **Chande Forecast Oscillator** | [Cfo](../lib/oscillators/cfo/Cfo.md) | - | - | ✔️ | ❔ |
| **Chande Momentum Oscillator** | Cmo | - | ✔️ | ✔️ | ❔ |
| **Chebyshev Type I Filter** | Cheby1 | - | - | - | - |
| **Chebyshev Type II Filter** | Cheby2 | - | - | - | - |
| **Choppiness Index** | Chop | - | - | ✔️ | ❔ |
| **Close-to-Close Volatility** | Ccv | - | - | - | - |
| **Cointegration** | Cointegration | - | - | - | - |
| **Commodity Channel Index** | Cci | ✔️ | ✔️ | ✔️ | ❔ |
| **Composite Fractal Behavior** | [Cfb](../lib/momentum/cfb/Cfb.md) | - | - | - | - |
| **Conditional Volatility** | [Cv](../lib/volatility/cv/Cv.md) | - | - | - | - |
| **Convolution Moving Average** | [Conv](../lib/trends_FIR/conv/Conv.md) | ✔️ | ✔️ | ✔️ | ✔️ |
| **Coral Trend Filter** | [Coral](../lib/trends_IIR/coral/Coral.md) | - | - | - | - |
| **Correlation** | Correlation | - | - | ✔️ | - |
| **Cumulative Moving Average** | [Cma](../lib/statistics/cma/Cma.md) | - | - | - | - |
| **Decay Min-Max Channel** | [Decaychannel](../lib/channels/decaychannel/decaychannel.md) | - | - | - | - |
| **Ehlers Decycler** | [Decycler](../lib/trends_IIR/decycler/Decycler.md) | - | - | - | - |
| **DeMark Pivot Points** | [Pivotdem](../lib/reversals/pivotdem/Pivotdem.md) | - | - | - | ❔ |
| **Detrended Price Oscillator** | [Dpo](../lib/oscillators/dpo/Dpo.md) | - | ⚠️ | - | ❔ |
| **Ehlers Detrended Synthetic Price** | Dsp | - | - | - | ❔ |
| **Deviation-Scaled MA** | Dsma | - | - | - | ❔ |
| **Directional Movement Index** | Dx | ✔️ | ✔️ | ✔️ | ✔️ |
| **Directional Movement Index (Jurik)** | [Dmx](../lib/dynamics/dmx/Dmx.md) | - | - | - | - |
| **Dirty Data Detection** | Dirty | - | - | - | - |
| **Donchian Channels** | [Dchannel](../lib/channels/dchannel/Dchannel.md) | - | - | ✔️ | ❔ |
| **Double Exponential Moving Average** | [Dema](../lib/trends_IIR/dema/Dema.md) | ✔️ | ✔️ | ✔️ | ✔️ |
| **Double Weighted Moving Average** | [Dwma](../lib/trends_FIR/dwma/Dwma.md) | ✔️ | ✔️ | ✔️ | - |
| **Ease of Movement** | [Eom](../lib/volume/eom/Eom.md) | - | ✔️ | - | - |
| **Ehlers Autocorrelation Periodogram** | [Eacp](../lib/cycles/eacp/eacp.md) | - | - | - | - |
| **BandPass Filter** | [Bpf](../lib/filters/bpf/Bpf.md) | - | - | - | - |
| **Ehlers Center of Gravity** | Cg | - | - | - | ❔ |
| **Ehlers Correlation Cycle** | [Ccor](../lib/cycles/ccor/Ccor.md) | - | - | - | - |
| **Ehlers Cyber Cycle** | [Ccyc](../lib/cycles/ccyc/Ccyc.md) | - | - | - | ❔ |
| **Ehlers Distance Coefficient Filter** | [Edcf](../lib/filters/edcf/Edcf.md) | - | - | - | - |
| **Ehlers Even Better Sinewave** | [Ebsw](../lib/cycles/ebsw/ebsw.md) | - | - | - | ❔ |
| **Ehlers Fractal Adaptive MA** | [Frama](../lib/trends_IIR/frama/Frama.md) | - | - | - | ❔ |
| **Ehlers Highpass Filter** | [Hpf](../lib/filters/hpf/Hpf.md) | - | - | - | ❔ |
| **Ehlers Phasor Analysis** | Phasor | - | - | - | - |
| **Ehlers Sine Wave** | [HtSine](../lib/cycles/ht_sine/HtSine.md) | ✔️ | - | - | - |
| **Ehlers SSF-Based Detrended Synthetic Price** | Ssfdsp | - | - | - | - |
| **Ehlers 2-Pole Super Smooth Filter** | [Ssf2](../lib/filters/ssf2/Ssf2.md) | - | - | - | ✔️ |
| **Ehlers 3-Pole Super Smooth Filter** | [Ssf3](../lib/filters/ssf3/Ssf3.md) | - | - | - | - |
| **Ehlers Ultrasmooth Filter** | Usf | - | - | - | - |
| **Elliptic (Cauer) Filter** | [Elliptic](../lib/filters/elliptic/Elliptic.md) | - | - | - | ❔ |
| **Exponential Moving Average** | [Ema](../lib/trends_IIR/ema/Ema.md) | ✔️ | ✔️ | ✔️ | ✔️ |
| **Exponential Transformation** | Exptrans | - | - | - | - |
| **Exponential Weighted MA Volatility** | [Ewma](../lib/volatility/ewma/Ewma.md) | - | - | - | ❔ |
| **Elder's Thermometer** | [Etherm](../lib/volatility/etherm/Etherm.md) | - | - | - | - |
| **Extended Traditional Pivots** | [Pivotext](../lib/reversals/pivotext/Pivotext.md) | - | - | - | - |
| **Fibonacci Pivot Points** | Pivotfib | - | - | - | ❔ |
| **Ehlers Fisher Transform** | [Fisher](../lib/oscillators/fisher/Fisher.md) | - | ❔ | ❔ | ❔ |
| **Force Index** | [Efi](../lib/volume/efi/Efi.md) | - | - | ✔️ | ✔️ |
| **Fractal Chaos Bands** | [Fcb](../lib/channels/fcb/fcb.md) | - | - | ✔️ | ❔ |
| **Garman-Klass Volatility** | [Gkv](../lib/volatility/gkv/Gkv.md) | - | - | - | - |
| **Gator Oscillator** | [Gator](../lib/oscillators/gator/Gator.md) | - | - | - | - |
| **Gann High-Low Activator** | [Ghla](../lib/dynamics/ghla/Ghla.md) | - | - | - | - |
| **Gaussian Filter** | [Gauss](../lib/filters/gauss/Gauss.md) | - | - | - | ❔ |
| **Gaussian-Weighted MA** | Gwma | - | - | - | - |
| **Geometric Mean** | [Geomean](../lib/statistics/geomean/Geomean.md) | - | - | - | - |
| **Harmonic Mean** | [Harmean](../lib/statistics/harmean/Harmean.md) | - | - | - | - |
| **Granger Causality Test** | [Granger](../lib/statistics/granger/Granger.md) | - | - | - | - |
| **Hamming Window MA** | Hamma | - | - | - | ❔ |
| **Hann FIR Filter** | [Hann](../lib/filters/hann/Hann.md) | - | - | - | - |
| **Hanning Window MA** | Hanma | - | - | - | ❔ |
| **High-Low Volatility (Parkinson)** | [Hlv](../lib/volatility/hlv/Hlv.md) | - | - | - | - |
| **Highest value** | [Highest](../lib/numerics/highest/Highest.md) | ✔️ | ✔️ | - | - |
| **Ehlers Hilbert Transform Dominant Cycle Period** | [HtDcPeriod](../lib/cycles/ht_dcperiod/ht_dcperiod.md) | ✔️ | - | - | - |
| **Ehlers Hilbert Transform Dominant Cycle Phase** | [HtDcPhase](../lib/cycles/ht_dcphase/ht_dcphase.md) | ✔️ | - | - | - |
| **Ehlers Hilbert Transform Instantaneous Trend** | [Htit](../lib/trends_IIR/htit/Htit.md) | ✔️ | - | ✔️ | ✔️ |
| **Ehlers Hilbert Transform Phasor Components** | [HtPhasor](../lib/cycles/ht_phasor/ht_phasor.md) | ✔️ | - | - | - |
| **Ehlers Hilbert Transform SineWave** | [HtSine](../lib/cycles/ht_sine/ht_sine.md) | ✔️ | - | - | - |
| **Ehlers Hilbert Transform Trend vs Cycle Mode** | Ht_trendmode | ✔️ | - | - | - |
| **Historical Volatility (Close-to-Close)** | [Hv](../lib/volatility/hv/Hv.md) | - | - | - | - |
| **Hodrick-Prescott Filter** | [Hp](../lib/filters/hp/Hp.md) | - | - | - | - |
| **Holt Exponential Smoothing** | [Holt](../lib/trends_IIR/holt/Holt.md) | - | - | - | - |
| **Holt Weighted MA** | Hwma | - | - | - | ❔ |
| **Ehlers Homodyne Discriminator** | [Homod](../lib/cycles/homod/homod.md) | - | - | - | ❔ |
| **Huber Loss** | Huber | - | - | - | - |
| **Hull Exponential MA** | [Hema](../lib/trends_IIR/hema/Hema.md) | - | - | - | - |
| **Hull Moving Average** | [Hma](../lib/trends_FIR/hma/Hma.md) | - | ✔️ | ✔️ | [⚠️](../lib/trends_FIR/hma/Hma.md#external-library-discrepancies) |
| **Hurst Exponent** | Hurst | - | - | - | ❔ |
| **Ichimoku Cloud** | Ichimoku | - | - | ✔️ | ❔ |
| **Impulse (Elder)** | [Impulse](../lib/dynamics/impulse/Impulse.md) | - | - | - | - |
| **Inertia** | [Inertia](../lib/oscillators/inertia/Inertia.md) | - | - | - | ❔ |
| **Interquartile Range** | Iqr | - | - | - | - |
| **Intraday Intensity Index** | [Iii](../lib/volume/iii/Iii.md) | - | - | - | - |
| **Intraday Momentum Index** | Imi | - | - | - | ❔ |
| **Jarque-Bera Test** | [Jb](../lib/statistics/jb/Jb.md) | - | - | - | - |
| **Jurik Moving Average** | [Jma](../lib/trends_IIR/jma/Jma.md) | - | - | - | ❔ |
| **Jurik Volatility** | [Jvolty](../lib/volatility/jvolty/Jvolty.md) | - | - | - | - |
| **Jurik Adaptive Envelope Bands** | [Jbands](../lib/channels/jbands/Jbands.md) | - | - | - | - |
| **Jurik Volatility Normalized [0,100]** | [Jvoltyn](../lib/volatility/jvoltyn/Jvoltyn.md) | - | - | - | - |
| **Kalman Filter** | [Kalman](../lib/filters/kalman/Kalman.md) | - | - | - | - |
| **Kaufman Adaptive Moving Average** | [Kama](../lib/trends_IIR/kama/Kama.md) | ✔️ | ✔️ | ✔️ | ✔️ |
| **KDJ Indicator** | [Kdj](../lib/oscillators/kdj/Kdj.md) | - | - | - | - |
| **Keltner Channel** | [Kchannel](../lib/channels/kchannel/kchannel.md) | - | - | ✔️ | ❔ |
| **Kendall Rank Correlation** | [Kendall](../lib/statistics/kendall/Kendall.md) | - | - | - | - |
| **Klinger Volume Oscillator** | [Kvo](../lib/volume/kvo/Kvo.md) | - | ✔️ | ✔️ | ❔ |
| **Kurtosis** | [Kurtosis](../lib/statistics/kurtosis/Kurtosis.md) | - | - | - | [✔️](../lib/statistics/kurtosis/Kurtosis.md#validation) |
| **Ehlers Laguerre Filter** | [Laguerre](../lib/filters/laguerre/Laguerre.md) | - | - | - | - |
| **Least Mean Squares** | [Lms](../lib/filters/lms/Lms.md) | - | - | - | - |
| **Recursive Least Squares** | [Rls](../lib/filters/rls/Rls.md) | - | - | - | - |
| **Least Squares Moving Average** | [Lsma](../lib/trends_FIR/lsma/Lsma.md) | - | - | ✔️ | ❔ |
| **Linear Regression** | [LinReg](../lib/statistics/linreg/LinReg.md) | - | - | ✔️ | [⚠️](../lib/statistics/linreg/LinReg.md#validation) |
| **Linear Transformation** | Lineartrans | - | - | - | - |
| **Linear Trend MA** | Ltma | - | - | - | - |
| **LOESS/LOWESS Smoothing** | [Loess](../lib/filters/loess/Loess.md) | - | - | - | - |
| **Logarithmic Transformation** | Logtrans | - | - | - | - |
| **Logistic Function** | [Sigmoid](../lib/numerics/sigmoid/Sigmoid.md) | - | - | - | - |
| **Lowest value** | [Lowest](../lib/numerics/lowest/Lowest.md) | ✔️ | ✔️ | - | - |
| **Lunar Phase** | Lunar | - | - | - | - |
| **Lowest value** | [Lowest](../lib/numerics/lowest/Lowest.md) | ✔️ | ✔️ | - | - |
| **Lunar Phase** | Lunar | - | - | - | - |
| **Mass Index** | [Massi](../lib/volatility/massi/Massi.md) | - | - | - | ❔ |
| **McGinley Dynamic** | [Mgdi](../lib/trends_IIR/mgdi/Mgdi.md) | - | - | ✔️ | ✔️ |
| **Mean Absolute Error** | Mae | - | - | - | - |
| **Mean Absolute Percentage Difference** | Mapd | - | - | - | - |
| **Mean Absolute Percentage Error** | Mape | - | - | - | - |
| **Mean Absolute Scaled Error** | Mase | - | - | - | - |
| **Mean Error** | Me | - | - | - | - |
| **Mean Percentage Error** | Mpe | - | - | - | - |
| **Mean Squared Error** | Mse | - | - | - | - |
| **Mean Squared Logarithmic Error** | Msle | - | - | - | - |
| **Ehlers MESA Adaptive Moving Average** | [Mama](../lib/trends_IIR/mama/Mama.md) | - | - | ✔️ | ✔️ |
| **Median Price** | [Medprice](../lib/core/medprice/Medprice.md) | ✔️ | - | - | - |
| **Mid Price** | [Midprice](../lib/core/midprice/Midprice.md) | ✔️ | - | - | - |
| **Midpoint** | [Midpoint](../lib/core/midpoint/Midpoint.md) | ✔️ | - | - | - |
| **Min-Max Channel** | [Mmchannel](../lib/channels/mmchannel/mmchannel.md) | - | - | ✔️ | - |
| **Min-Max Scaling (Normalization)** | [Normalize](../lib/numerics/normalize/Normalize.md) | - | - | - | - |
| **Mode (Most Frequent)** | Mode | - | - | - | - |
| **Modular Filter** | [Modf](../lib/filters/modf/Modf.md) | - | - | - | - |
| **Modified MA** | [Mma](../lib/trends_IIR/mma/Mma.md) | - | - | - | - |
| **Natural Moving Average** | [Nma](../lib/trends_IIR/nma/Nma.md) | - | - | - | - |
| **Momentum** | Mom | ✔️ | ✔️ | ✔️ | ❔ |
| **Momentum change; 2nd derivative** | Accel | - | - | - | - |
| **Money Flow Index** | [Mfi](../lib/volume/mfi/Mfi.md) | - | - | ✔️ | ✔️ |
| **Moon Phase** | Moon | - | - | - | - |
| **Moving Average Convergence/Divergence** | [Macd](../lib/momentum/macd/Macd.md) | ✔️ | ✔️ | ✔️ | ✔️ |
| **Moving Average Envelopes** | [Maenv](../lib/channels/maenv/maenv.md) | - | - | ✔️ | ❔ |
| **Negative Volume Index** | [Nvi](../lib/volume/nvi/Nvi.md) | - | ✔️ | - | - |
| **Normalized Average True Range** | Natr | ✔️ | ✔️ | ✔️ | ✔️ |
| **Normalized Shannon Entropy** | Entropy | - | - | - | - |
| **Notch Filter** | [Notch](../lib/filters/notch/Notch.md) | - | - | - | - |
| **Nadaraya-Watson Estimator** | [Nw](../lib/filters/nw/Nw.md) | - | - | - | - |
| **One Euro Filter** | [OneEuro](../lib/filters/oneeuro/OneEuro.md) | - | - | - | - |
| **On Balance Volume** | [Obv](../lib/volume/obv/Obv.md) | [⚠️](../lib/volume/obv/Obv.md#validation) | ✔️ | ✔️ | [⚠️](../lib/volume/obv/Obv.md#validation) |
| **Parabolic SAR** | [Psar](../lib/reversals/psar/Psar.md) | - | - | ✔️ | ❔ |
| **Pascal Weighted Moving Average** | [Pwma](../lib/trends_FIR/pwma/Pwma.md) | - | - | - | ✔️ |
| **Percentage Change** | [Change](../lib/numerics/change/Change.md) | - | ✔️ | - | - |
| **Percentage Price Oscillator** | Ppo | ✔️ | ✔️ | - | ✔️ |
| **Percentage Volume Oscillator** | [Pvo](../lib/volume/pvo/Pvo.md) | - | - | - | ❔ |
| **Percentile** | Percentile | - | - | - | - |
| **Polarized Fractal Efficiency** | [Pfe](../lib/dynamics/pfe/Pfe.md) | - | - | - | - |
| **Pivot Points** | [Pivot](../lib/reversals/pivot/Pivot.md) | - | - | - | ❔ |
| **Pivot Points (Camarilla)** | [Pivotcam](../lib/reversals/pivotcam/Pivotcam.md) | - | - | - | ❔ |
| **Pivot Points (DeMark)** | [Pivotdem](../lib/reversals/pivotdem/Pivotdem.md) | - | - | - | ❔ |
| **Pivot Points (Extended)** | [Pivotext](../lib/reversals/pivotext/Pivotext.md) | - | - | - | ❔ |
| **Pivot Points (Fibonacci)** | [Pivotfib](../lib/reversals/pivotfib/Pivotfib.md) | - | - | - | ❔ |
| **Positive Volume Index** | [Pvi](../lib/volume/pvi/Pvi.md) | - | ✔️ | - | - |
| **Pretty Good Oscillator** | [Pgo](../lib/oscillators/pgo/Pgo.md) | - | - | - | ❔ |
| **Price Channel** | [Pchannel](../lib/channels/pchannel/pchannel.md) | - | - | ✔️ | - |
| **Price Momentum Oscillator** | Pmo | - | - | ✔️ | ✔️ |
| **Price Relative Strength** | Prs | - | - | ✔️ | - |
| **Price Volume Divergence** | [Pvd](../lib/volume/pvd/Pvd.md) | - | - | - | - |
| **Price Volume Rank** | [Pvr](../lib/volume/pvr/Pvr.md) | - | - | - | - |
| **Price Volume Trend** | [Pvt](../lib/volume/pvt/Pvt.md) | - | - | - | ✔️ |
| **Qstick Indicator** | Qstick | - | - | - | ❔ |
| **Quad Exponential MA** | [Qema](../lib/trends_IIR/qema/Qema.md) | - | - | - | - |
| **Quantile** | Quantile | - | - | - | - |
| **Range Action Verification Index** | [Ravi](../lib/dynamics/ravi/Ravi.md) | - | - | - | - |
| **Rate of acceleration; 3rd derivative** | [Jerk](../lib/numerics/jerk/Jerk.md) | - | - | - | - |
| **Rate of Change** | [Roc](../lib/momentum/roc/Roc.md) | - | ✔️ | ✔️ | ❔ |
| **Rate of change; 1st derivative** | [Slope](../lib/statistics/linreg/LinReg.md) | - | - | ✔️ | ❔ |
| **Rate of Change Percentage** | Rocp | ✔️ | - | - | - |
| **Rate of Change Ratio** | Rocr | ✔️ | ✔️ | - | - |
| **Realized Volatility** | [Rv](../lib/volatility/rv/Rv.md) | - | - | - | - |
| **Rectified Linear Unit** | [Relu](../lib/numerics/relu/Relu.md) | - | - | - | - |
| **Recursive Gaussian MA** | [Rgma](../lib/trends_IIR/rgma/Rgma.md) | - | - | - | - |
| **Ehlers Recursive Median Filter** | [Rmed](../lib/filters/rmed/Rmed.md) | - | - | - | - |
| **Regression Channels** | [Regchannel](../lib/channels/regchannel/regchannel.md) | - | - | - | - |
| **Regularized Exponential MA** | [Rema](../lib/trends_IIR/rema/Rema.md) | - | - | - | ❔ |
| **Relative Absolute Error** | Rae | - | - | - | - |
| **Relative Squared Error** | Rse | - | - | - | - |
| **Relative Strength Index** | [Rsi](../lib/momentum/rsi/Rsi.md) | ✔️ | ✔️ | ✔️ | ✔️ |
| **Relative Strength Quality Index** | [Rsx](../lib/momentum/rsx/Rsx.md) | - | - | - | ❔ |
| **Relative Volatility Index** | [Rvi](../lib/volatility/rvi/Rvi.md) | - | - | - | ❔ |
| **Renko** | - | - | - | ✔️ | - |
| **Rogers-Satchell Volatility** | Rsv | - | - | - | - |
| **Ehlers Roofing Filter** | [Roofing](../lib/filters/roofing/Roofing.md) | - | - | - | ✔️ |
| **Root Mean Squared Error** | Rmse | - | - | - | - |
| **Root Mean Squared Logarithmic Error** | Rmsle | - | - | - | - |
| **R-Squared** | [RSquared](../lib/statistics/linreg/LinReg.md) | - | - | ✔️ | ❔ |
| **Savitzky-Golay Filter** | [Sgf](../lib/filters/sgf/Sgf.md) | - | - | - | - |
| **Savitzky-Golay MA** | [Sgma](../lib/trends_FIR/sgma/Sgma.md) | - | - | - | - |
| **Smoothed Adaptive Momentum** | [Sam](../lib/momentum/sam/Sam.md) | - | - | - | - |
| **Schaff Trend Cycle** | [Stc](../lib/oscillators/stc/stc.md) | - | - | ✔️ | ❔ |
| **Simple Moving Average** | [Sma](../lib/trends_FIR/sma/Sma.md) | ✔️ | ✔️ | ✔️ | ✔️ |
| **Sine-weighted MA** | [Sinema](../lib/trends_FIR/sinema/Sinema.md) | - | - | - | - |
| **Smoothed Moving Average** | [Rma](../lib/trends_IIR/rma/Rma.md) | - | - | ✔️ | ✔️ |
| **Solar Activity Cycle** | Solar | - | - | - | - |
| **Spearman Rank Correlation** | Spearman | - | - | - | ❔ |
| **Ehlers Super Passband Filter** | [Spbf](../lib/filters/spbf/Spbf.md) | - | - | - | - |
| **Square Root Transformation** | [Sqrttrans](../lib/numerics/sqrttrans/Sqrttrans.md) | - | - | - | - |
| **Standard Deviation Channel** | [Sdchannel](../lib/channels/sdchannel/sdchannel.md) | - | - | - | ❔ |
| **Standardization (Z-score)** | [Zscore](../lib/statistics/zscore/Zscore.md) | - | - | - | ❔ |
| **Starc Bands** | Starc | - | - | - | - |
| **Stochastic Fast** | [Stochf](../lib/oscillators/stochf/Stochf.md) | ✔️ | - | ✔️ | ❔ |
| **Stochastic Momentum Index** | [Smi](../lib/oscillators/smi/Smi.md) | - | - | - | ❔ |
| **Stochastic Oscillator** | [Stoch](../lib/oscillators/stoch/Stoch.md) | - | - | ✔️ | - |
| **Stochastic RSI** | [Stochrsi](../lib/oscillators/stochrsi/Stochrsi.md) | ✔️ | - | ✔️ | ✔️ |
| **Stoller Average Range Channel** | [Starchannel](../lib/channels/starchannel/starchannel.md) | - | - | ✔️ | ❔ |
| **Super Trend Bands** | [Stbands](../lib/channels/stbands/Stbands.md) | - | - | - | - |
| **SuperTrend** | [Super](../lib/dynamics/super/Super.md) | - | - | ✔️ | ❔ |
| **Swing High/Low Detection** | [Swings](../lib/reversals/swings/Swings.md) | - | - | - | ❔ |
| **Symmetric Mean Absolute Percentage Error** | Smape | - | - | - | - |
| **T3 Moving Average** | [T3](../lib/trends_IIR/t3/T3.md) | ✔️ | - | ✔️ | ✔️ |
| **Theil Index** | Theil | - | - | - | - |
| **Time Series Forecast** | [Tsf](../lib/trends_FIR/tsf/Tsf.md) | ✔️ | ✔️ | - | ❔ |
| **Time Weighted Average Price** | Twap | - | - | - | - |
| **Trade Volume Index** | Tvi | - | - | - | ❔ |
| **Triangular Moving Average** | [Trima](../lib/trends_FIR/trima/Trima.md) | ✔️ | ✔️ | ✔️ | ❔ |
| **Triple Exponential Average** | [Trix](../lib/oscillators/trix/Trix.md) | ✔️ | ✔️ | ✔️ | ❔ |
| **Triple Exponential Moving Average** | [Tema](../lib/trends_IIR/tema/Tema.md) | ✔️ | ✔️ | ✔️ | ❔ |
| **Trend Regularity Adaptive MA** | [Trama](../lib/trends_IIR/trama/Trama.md) | - | - | - | - |
| **True Range** | Tr | ✔️ | ✔️ | - | - |
| **True Strength Index** | Tsi | - | - | ✔️ | ✔️ |
| **Typical Price** | [Typprice](../lib/core/typprice/Typprice.md) | ✔️ | - | - | - |
| **TTM Trend** | Ttm | - | - | - | - |
| **TTM Scalper Alert** | [TtmScalper](../lib/reversals/ttm_scalper/TtmScalper.md) | - | - | - | - |
| **TTM Wave** | [TtmWave](../lib/oscillators/ttm_wave/TtmWave.md) | - | - | - | - |
| **Two-Argument Arctangent** | Atan2 | - | - | - | - |
| **Ulcer Index** | Ui | - | - | - | ❔ |
| **Ehlers Ultimate Bands** | [Ubands](../lib/channels/ubands/Ubands.md) | - | - | - | - |
| **Ehlers Ultimate Channel** | [Uchannel](../lib/channels/uchannel/Uchannel.md) | - | - | - | - |
| **Ultimate Oscillator** | [Ultosc](../lib/oscillators/ultosc/Ultosc.md) | ✔️ | ✔️ | ✔️ | ✔️ |
| **Variable Index Dynamic Average** | [Vidya](../lib/trends_IIR/vidya/Vidya.md) | - | - | - | ❔ |
| **Velocity (Jurik)** | [Vel](../lib/momentum/vel/Vel.md) | - | - | - | - |
| **Volatility Adjusted Moving Average** | [Vama](../lib/trends_IIR/vama/Vama.md) | - | - | - | ❔ |
| **Volatility of Volatility** | [Vov](../lib/volatility/vov/Vov.md) | - | - | - | - |
| **Volatility Ratio** | [Vr](../lib/volatility/vr/Vr.md) | - | - | - | - |
| **Volume Accumulation** | Va | - | - | - | ❔ |
| **Volume Force** | Vf | - | - | - | - |
| **Volume Oscillator** | Vo | - | - | - | - |
| **Volume Rate of Change** | Vroc | - | - | - | - |
| **Volume Weighted Accumulation/Distribution** | [Vwad](../lib/volume/vwad/Vwad.md) | - | - | - | - |
| **Volume Weighted Average Price** | [Vwap](../lib/volume/vwap/Vwap.md) | - | - | - | - |
| **Volume Weighted Moving Average** | [Vwma](../lib/volume/vwma/Vwma.md) | - | - | ✔️ | - |
| **Vertical Horizontal Filter** | [Vhf](../lib/dynamics/vhf/Vhf.md) | - | - | - | - |
| **Vortex Indicator** | Vortex | - | - | ✔️ | ❔ |
| **Ehlers Voss Predictive Filter** | [Voss](../lib/filters/voss/Voss.md) | - | - | - | ✔️ |
| **VWAP Bands** | [Vwapbands](../lib/channels/vwapbands/Vwapbands.md) | - | - | - | - |
| **VWAP with Standard Deviation Bands** | [Vwapsd](../lib/channels/vwapsd/Vwapsd.md) | - | - | - | - |
| **Wavelet Denoising Filter** | [Wavelet](../lib/filters/wavelet/Wavelet.md) | - | - | - | - |
| **Weighted Moving Average** | [Wma](../lib/trends_FIR/wma/Wma.md) | ✔️ | ✔️ | ✔️ | - |
| **Wiener Filter** | Wiener | - | - | - | - |
| **Williams %R** | [Willr](../lib/oscillators/willr/Willr.md) | ✔️ | ✔️ | ✔️ | ❔ |
| **Weighted Close Price** | [Wclprice](../lib/core/wclprice/Wclprice.md) | ✔️ | - | - | - |
| **Williams Accumulation/Distribution** | [Wad](../lib/volume/wad/Wad.md) | - | ✔️ | - | [⚠️](../lib/volume/wad/Wad.md#validation) |
| **Williams Alligator** | Alligator | - | - | ✔️ | ❔ |
| **Williams Fractal** | [Fractals](../lib/reversals/fractals/Fractals.md) | - | - | ✔️ | ❔ |
| **Woodie's Pivot Points** | [Pivotwood](../lib/reversals/pivotwood/Pivotwood.md) | - | - | - | ❔ |
| **Yang-Zhang Volatility** | Yzv | - | - | - | - |
| **Yang-Zhang Volatility Adjusted MA** | [Yzvama](../lib/trends_IIR/yzvama/Yzvama.md) | - | - | - | - |
| **Zero-Lag Double Exponential MA** | Zldema | - | - | - | - |
| **Zero-Lag Exponential Moving Average** | [Zlema](../lib/trends_IIR/zlema/Zlema.md) | - | - | - | ❔ |
| **Zero-Lag Triple Exponential MA** | Zltema | - | - | - | ❔ |
| **ZigZag** | - | - | - | ✔️ | - |
| **Z-score standardization** | Zscore | - | - | - | ✔️ |
| **Z-Test** | Ztest | - | - | - | - |

## Statistical Indicators

| Indicator | QuanTAlib | MathNet | TA-Lib | Tulip | Skender |
| :-------- | :-------- | :-----: | :----: | :---: | :-----: |
| **Autocorrelation Function** | [Acf](../lib/statistics/acf/Acf.md) | - | - | - | - |
| **Covariance** | [Covariance](../lib/statistics/covariance/Covariance.md) | - | - | - | - |
| **Entropy (Shannon)** | [Entropy](../lib/statistics/entropy/Entropy.md) | - | - | - | - |
| **Geometric Mean** | [Geomean](../lib/statistics/geomean/Geomean.md) | - | - | - | - |
| **Harmonic Mean** | [Harmean](../lib/statistics/harmean/Harmean.md) | - | - | - | - |
| **Hurst Exponent** | [Hurst](../lib/statistics/hurst/Hurst.md) | - | - | - | - |
| **Interquartile Range** | [Iqr](../lib/statistics/iqr/Iqr.md) | - | - | - | - |
| **Granger Causality** | [Granger](../lib/statistics/granger/Granger.md) | - | - | - | - |
| **Jarque-Bera Test** | [Jb](../lib/statistics/jb/Jb.md) | - | - | - | - |
| **Kendall Rank Correlation** | [Kendall](../lib/statistics/kendall/Kendall.md) | - | - | - | - |
| **Median (Statistical)** | [Median](../lib/statistics/median/Median.md) | ✔️ | - | - | - |
| **Mode** | [Mode](../lib/statistics/mode/Mode.md) | - | - | - | - |
| **Percentile** | [Percentile](../lib/statistics/percentile/Percentile.md) | - | - | - | - |
| **Quantile** | [Quantile](../lib/statistics/quantile/Quantile.md) | - | - | - | - |
| **Skewness** | [Skew](../lib/statistics/skew/Skew.md) | ✔️ | - | - | - |
| **Spearman Rank Correlation** | [Spearman](../lib/statistics/spearman/Spearman.md) | - | - | - | - |
| **Standard Deviation** | [StdDev](../lib/statistics/stddev/StdDev.md) | ✔️ | ✔️ | ✔️ | ✔️ |
| **Sum (Rolling)** | [Sum](../lib/statistics/sum/Sum.md) | - | ✔️ | ✔️ | - |
| **Theil T Index** | [Theil](../lib/statistics/theil/Theil.md) | - | - | - | - |
| **Partial Autocorrelation Function** | [Pacf](../lib/statistics/pacf/Pacf.md) | - | - | - | - |
| **Variance** | [Variance](../lib/statistics/variance/Variance.md) | ✔️ | ✔️ | ✔️ | ✔️ |
| **Z-Score** | [Zscore](../lib/statistics/zscore/Zscore.md) | - | - | - | - |
| **Z-Test** | [Ztest](../lib/statistics/ztest/Ztest.md) | - | - | - | - |

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
| **Tukey Loss** | [Tukey](../lib/errors/tukeybiweight/TukeyBiweight.md) | - | No external validation available |
| **Quantile Loss** | [Quantile](../lib/errors/quantileloss/QuantileLoss.md) | - | No external validation available |
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
