# Validation Across TA Libraries

> "Trust, but verify." — Russian proverb (applicable to both Cold War diplomacy and technical indicator libraries)

Every indicator implementation makes implicit claims about correctness. QuanTAlib validates these claims by comparing outputs against established libraries: TA-Lib, Tulip, Skender.Stock.Indicators, OoplesFinance, and pandas-ta. Where implementations diverge, the differences get documented.

## Reading the Matrix

| Symbol | Meaning |
| :----: | :------ |
| ✔️ | Validated: outputs match within floating-point tolerance (1e-9) |
| ⚠️ | Partial match: minor discrepancies documented in indicator notes |
| ❔ | Implementation exists but not validated |
| - | No implementation in that library |

**Tolerance rationale:** Financial data uses double precision. Differences below 1e-9 stem from floating-point arithmetic order, not algorithmic divergence.

## Technical Indicators

| Indicator | QuanTAlib | TA-Lib | Tulip | Skender | Ooples | pandas-ta |
| :-------- | :-------- | :----: | :---: | :-----: | :----: | :-------: |
| **Aberration Bands** | [Aberr](../lib/channels/aberr/aberr.md) | - | - | - | - | ⚠️ |
| **Absolute Price Oscillator** | [Apo](../lib/oscillators/apo/Apo.md) | [✔️](../lib/oscillators/apo/Apo.md#validation) | [✔️](../lib/oscillators/apo/Apo.md#validation) | - | [✔️](../lib/oscillators/apo/Apo.md#validation) | ✔️ |
| **Acceleration Bands** | [AccBands](../lib/channels/accbands/accbands.md) | [✔️](../lib/channels/accbands/accbands.md#validation) | - | - | - | ❔ |
| **Acceleration Oscillator** | [Ac](../lib/oscillators/ac/Ac.md) | - | - | - | - | - |
| **Accumulation/Distribution Line** | [Adl](../lib/volume/adl/adl.md) | [✔️](../lib/volume/adl/adl.md#validation) | [✔️](../lib/volume/adl/adl.md#validation) | [✔️](../lib/volume/adl/adl.md#validation) | [✔️](../lib/volume/adl/adl.md#validation) | ❔ |
| **Accumulation/Distribution Oscillator** | [Adosc](../lib/volume/adosc/adosc.md) | [✔️](../lib/volume/adosc/adosc.md#validation) | [✔️](../lib/volume/adosc/adosc.md#validation) | [✔️](../lib/volume/adosc/adosc.md#validation) | [✔️](../lib/volume/adosc/adosc.md#validation) | ❔ |
| **Accumulation Swing Index** | [Asi](../lib/momentum/asi/Asi.md) | - | - | - | - | - |
| **Adaptive Price Zone** | [Apz](../lib/channels/apz/apz.md) | - | - | - | - | - |
| **Andrews' Pitchfork** | [Apchannel](../lib/channels/apchannel/apchannel.md) | - | - | [✔️](../lib/channels/apchannel/apchannel.md#validation) | - | - |
| **Archer Moving Averages Trends** | [Amat](../lib/dynamics/amat/Amat.md) | - | - | [✔️](../lib/dynamics/amat/Amat.md#validation) | [✔️](../lib/dynamics/amat/Amat.md#validation) | ❔ |
| **Archer On-Balance Volume** | [Aobv](../lib/volume/aobv/Aobv.md) | - | - | - | - | ⚠️ |
| **Arnaud Legoux Moving Average** | [Alma](../lib/trends_FIR/alma/Alma.md) | - | - | [✔️](../lib/trends_FIR/alma/Alma.md#validation) | [✔️](../lib/trends_FIR/alma/Alma.md#validation) | ⚠️ |
| **Aroon** | [Aroon](../lib/dynamics/aroon/Aroon.md) | [✔️](../lib/dynamics/aroon/Aroon.md#validation) | [✔️](../lib/dynamics/aroon/Aroon.md#validation) | [✔️](../lib/dynamics/aroon/Aroon.md#validation) | - | ❔ |
| **Aroon Oscillator** | [AroonOsc](../lib/dynamics/aroonosc/AroonOsc.md) | [✔️](../lib/dynamics/aroonosc/AroonOsc.md#validation) | [✔️](../lib/dynamics/aroonosc/AroonOsc.md#validation) | [✔️](../lib/dynamics/aroonosc/AroonOsc.md#validation) | - | - |
| **ATR Bands** | [Atrbands](../lib/channels/atrbands/atrbands.md) | [✔️](../lib/channels/atrbands/atrbands.md#validation) | - | [✔️](../lib/channels/atrbands/atrbands.md#validation) | - | - |
| **Adaptive FIR Moving Average** | [Afirma](../lib/forecasts/afirma/Afirma.md) | - | - | - | - | - |
| **Ehlers Adaptive Laguerre Filter** | [ALaguerre](../lib/filters/alaguerre/ALaguerre.md) | - | - | - | - | - |
| **Ehlers Automatic Gain Control** | [Agc](../lib/filters/agc/Agc.md) | - | - | - | - | - |
| **Average Daily Range** | [Adr](../lib/volatility/adr/Adr.md) | - | - | - | - | - |
| **Average Directional Index** | [Adx](../lib/dynamics/adx/Adx.md) | [✔️](../lib/dynamics/adx/Adx.md#validation) | [✔️](../lib/dynamics/adx/Adx.md#validation) | [✔️](../lib/dynamics/adx/Adx.md#validation) | [✔️](../lib/dynamics/adx/Adx.md#validation) | ❔ |
| **Average Directional Movement Rating** | [Adxr](../lib/dynamics/adxr/Adxr.md) | [✔️](../lib/dynamics/adxr/Adxr.md#validation) | [✔️](../lib/dynamics/adxr/Adxr.md#validation) | - | - | - |
| **Average True Range** | [Atr](../lib/volatility/atr/atr.md) | [✔️](../lib/volatility/atr/atr.md#validation) | [✔️](../lib/volatility/atr/atr.md#validation) | [✔️](../lib/volatility/atr/atr.md#validation) | [✔️](../lib/volatility/atr/atr.md#validation) | ❔ |
| **Average True Range Normalized [0,1]** | [Atrn](../lib/volatility/atrn/Atrn.md) | - | - | [✔️](../lib/volatility/atrn/Atrn.md#validation) | - | - |
| **Average Price** | [Avgprice](../lib/core/avgprice/Avgprice.md) | [✔️](../lib/core/avgprice/Avgprice.md#validation) | - | - | - | - |
| **Awesome Oscillator** | [Ao](../lib/oscillators/ao/Ao.md) | - | [✔️](../lib/oscillators/ao/Ao.md#validation) | [✔️](../lib/oscillators/ao/Ao.md#validation) | [✔️](../lib/oscillators/ao/Ao.md#validation) | ❔ |
| **Balance of Power** | [Bop](../lib/momentum/bop/Bop.md) | [✔️](../lib/momentum/bop/Bop.md#validation) | [✔️](../lib/momentum/bop/Bop.md#validation) | [✔️](../lib/momentum/bop/Bop.md#validation) | [✔️](../lib/momentum/bop/Bop.md#validation) | ❔ |
| **Baxter-King Band-Pass Filter** | [BaxterKing](../lib/filters/baxterking/BaxterKing.md) | - | - | - | - | - |
| **Christiano-Fitzgerald Filter** | [Cfitz](../lib/filters/cfitz/Cfitz.md) | - | - | - | - | - |
| **Bollinger Bands** | [Bbands](../lib/channels/bbands/Bbands.md) | [✔️](../lib/channels/bbands/Bbands.md#validation) | [✔️](../lib/channels/bbands/Bbands.md#validation) | [✔️](../lib/channels/bbands/Bbands.md#validation) | [✔️](../lib/channels/bbands/Bbands.md#validation) | ⚠️ |
| **Bessel Filter** | [Bessel](../lib/filters/bessel/Bessel.md) | - | - | - | - | - |
| **Bessel-Weighted MA** | [Bwma](../lib/trends_FIR/bwma/Bwma.md) | - | - | - | - | - |
| **Beta Coefficient** | [Beta](../lib/statistics/beta/Beta.md) | [⚠️](../lib/statistics/beta/Beta.md#validation "TALib uses different return-series formula; diverges after warmup") | - | [✔️](../lib/statistics/beta/Beta.md#validation) | - | - |
| **Beta Distribution** | [Betadist](../lib/numerics/betadist/Betadist.md) | - | - | - | - | - |
| **Binomial Distribution** | [Binomdist](../lib/numerics/binomdist/Binomdist.md) | - | - | - | - | - |
| **Exponential Distribution** | [Expdist](../lib/numerics/expdist/Expdist.md) | - | - | - | - | - |
| **F-Distribution** | [Fdist](../lib/numerics/fdist/Fdist.md) | - | - | - | - | - |
| **Gamma Distribution** | [Gammadist](../lib/numerics/gammadist/Gammadist.md) | - | - | - | - | - |
| **Log-Normal Distribution** | [Lognormdist](../lib/numerics/lognormdist/Lognormdist.md) | - | - | - | - | - |
| **Normal Distribution** | [Normdist](../lib/numerics/normdist/Normdist.md) | - | - | - | - | - |
| **Poisson Distribution** | [Poissondist](../lib/numerics/poissondist/Poissondist.md) | - | - | - | - | - |
| **Student's t-Distribution** | [Tdist](../lib/numerics/tdist/Tdist.md) | - | - | - | - | - |
| **Weibull Distribution** | [Weibulldist](../lib/numerics/weibulldist/Weibulldist.md) | - | - | - | - | - |
| **Continuous Wavelet Transform** | [Cwt](../lib/numerics/cwt/Cwt.md) | - | - | - | - | - |
| **Discrete Wavelet Transform** | [Dwt](../lib/numerics/dwt/Dwt.md) | - | - | - | - | - |
| **Bias** | [Bias](../lib/momentum/bias/Bias.md) | - | - | - | - | ⚠️ |
| **Bilateral Filter** | [Bilateral](../lib/filters/bilateral/Bilateral.md) | - | - | - | - | - |
| **Blackman Window MA** | [Blma](../lib/trends_FIR/blma/Blma.md) | - | - | - | - | - |
| **Bollinger %B** | [Bbb](../lib/oscillators/bbb/Bbb.md) | - | - | [✔️](../lib/oscillators/bbb/Bbb.md#validation) | [⚠️](../lib/oscillators/bbb/Bbb.md#validation "structural test only; Ooples uses different %B band formula") | - |
| **Bollinger Band Squeeze** | [Bbs](../lib/oscillators/bbs/Bbs.md) | - | - | [✔️](../lib/oscillators/bbs/Bbs.md#validation) | [⚠️](../lib/oscillators/bbs/Bbs.md#validation "structural test only; Ooples measures different squeeze ratio") | - |
| **Bollinger Band Width** | [Bbw](../lib/volatility/bbw/Bbw.md) | - | - | [✔️](../lib/volatility/bbw/Bbw.md#validation) | [⚠️](../lib/volatility/bbw/Bbw.md#validation "structural test only; Ooples measures absolute width not ratio") | - |
| **Bollinger Band Width Normalized** | [Bbwn](../lib/volatility/bbwn/Bbwn.md) | - | - | - | - | - |
| **Bollinger Band Width Percentile** | [Bbwp](../lib/volatility/bbwp/Bbwp.md) | - | - | - | - | - |
| **BRAR** | [Brar](../lib/oscillators/brar/Brar.md) | - | - | - | - | ⚠️ |
| **Ehlers 2-Pole Butterworth Filter** | [Butter2](../lib/filters/butter2/Butter2.md) | - | - | - | [✔️](../lib/filters/butter2/Butter2.md#validation) | - |
| **Ehlers 3-Pole Butterworth Filter** | [Butter3](../lib/filters/butter3/Butter3.md) | - | - | - | - | - |
| **Camarilla Pivot Points** | [Pivotcam](../lib/reversals/pivotcam/Pivotcam.md) | - | - | - | - | - |
| **Chandelier Exit** | [Chandelier](../lib/reversals/chandelier/Chandelier.md) | - | - | [✔️](../lib/reversals/chandelier/Chandelier.md#validation) | - | ❔ |
| **Chande Kroll Stop** | [Ckstop](../lib/reversals/ckstop/Ckstop.md) | - | - | - | - | ❔ |
| **Chaikin Money Flow** | [Cmf](../lib/volume/cmf/Cmf.md) | - | - | [✔️](../lib/volume/cmf/Cmf.md#validation) | [⚠️](../lib/volume/cmf/Cmf.md#validation "structural test only; Ooples uses different lookback period") | ⚠️ |
| **Chaikin Volatility** | [Cvi](../lib/volatility/cvi/Cvi.md) | - | [⚠️](../lib/volatility/cvi/Cvi.md#validation "Tulip implements Chande VIDA; QuanTAlib implements Chaikin volatility") | - | [⚠️](../lib/volatility/cvi/Cvi.md#validation "Ooples implements Chande VIDA; QuanTAlib implements Chaikin volatility") | - |
| **Chande Forecast Oscillator** | [Cfo](../lib/oscillators/cfo/Cfo.md) | - | - | [✔️](../lib/oscillators/cfo/Cfo.md#validation) | - | ✔️ |
| **Chande Momentum Oscillator** | [Cmo](../lib/momentum/cmo/Cmo.md) | [⚠️](../lib/momentum/cmo/Cmo.md#validation "TALib uses Wilder smoothing; QuanTAlib uses simple sum") | [✔️](../lib/momentum/cmo/Cmo.md#validation) | [✔️](../lib/momentum/cmo/Cmo.md#validation) | [✔️](../lib/momentum/cmo/Cmo.md#validation) | ✔️ |
| **Chebyshev Type I Filter** | [Cheby1](../lib/filters/cheby1/Cheby1.md) | - | - | - | - | - |
| **Chebyshev Type II Filter** | [Cheby2](../lib/filters/cheby2/Cheby2.md) | - | - | - | - | - |
| **Choppiness Index** | [Chop](../lib/dynamics/chop/Chop.md) | - | - | [✔️](../lib/dynamics/chop/Chop.md#validation) | [⚠️](../lib/dynamics/chop/Chop.md#validation "structural test only; Ooples uses different ATR normalization") | ❔ |
| **Close-to-Close Volatility** | [Ccv](../lib/volatility/ccv/Ccv.md) | - | - | - | - | - |
| **Cointegration** | [Cointegration](../lib/statistics/cointegration/Cointegration.md) | - | - | - | - | - |
| **Commodity Channel Index** | [Cci](../lib/momentum/cci/Cci.md) | [✔️](../lib/momentum/cci/Cci.md#validation) | [✔️](../lib/momentum/cci/Cci.md#validation) | [✔️](../lib/momentum/cci/Cci.md#validation) | - | ❔ |
| **Composite Fractal Behavior** | [Cfb](../lib/momentum/cfb/Cfb.md) | - | - | - | - | - |
| **Conditional Volatility** | [Cv](../lib/volatility/cv/Cv.md) | - | - | - | - | - |
| **Connor's RSI** | [Crsi](../lib/oscillators/crsi/Crsi.md) | - | - | - | - | ⚠️ |
| **Convolution Moving Average** | [Conv](../lib/trends_FIR/conv/Conv.md) | [✔️](../lib/trends_FIR/conv/Conv.md#validation) | [✔️](../lib/trends_FIR/conv/Conv.md#validation) | [✔️](../lib/trends_FIR/conv/Conv.md#validation) | [✔️](../lib/trends_FIR/conv/Conv.md#validation) | - |
| **Coppock Curve** | [Coppock](../lib/oscillators/coppock/Coppock.md) | - | - | - | - | ❔ |
| **Coral Trend Filter** | [Coral](../lib/trends_IIR/coral/Coral.md) | - | - | - | - | - |
| **Correlation** | [Correlation](../lib/statistics/correlation/Correlation.md) | [✔️](../lib/statistics/correlation/Correlation.md#validation) | - | [✔️](../lib/statistics/correlation/Correlation.md#validation) | - | - |
| **Correlation Trend Indicator** | [Cti](../lib/oscillators/cti/Cti.md) | - | - | - | - | ⚠️ |
| **Cumulative Moving Average** | [Cma](../lib/statistics/cma/Cma.md) | - | - | - | - | - |
| **Decay Min-Max Channel** | [Decaychannel](../lib/channels/decaychannel/decaychannel.md) | - | - | - | - | - |
| **Ehlers Decycler** | [Decycler](../lib/trends_IIR/decycler/Decycler.md) | - | - | - | - | - |
| **DeMark Pivot Points** | [Pivotdem](../lib/reversals/pivotdem/Pivotdem.md) | - | - | - | - | - |
| **DeMarker Oscillator** | [Dem](../lib/oscillators/dem/Dem.md) | - | - | - | [⚠️](../lib/oscillators/dem/Dem.md#validation "structural test only; Ooples DeMarker uses different smoothing") | - |
| **Detrended Price Oscillator** | [Dpo](../lib/oscillators/dpo/Dpo.md) | - | [⚠️](../lib/oscillators/dpo/Dpo.md#validation "Tulip shifts n/2+1 bars; QuanTAlib shifts period/2 bars") | [✔️](../lib/oscillators/dpo/Dpo.md#validation) | [⚠️](../lib/oscillators/dpo/Dpo.md#validation "structural test only; period alignment differs from Ooples") | ✔️ |
| **Ehlers Detrended Synthetic Price** | [Dsp](../lib/cycles/dsp/Dsp.md) | - | - | - | [⚠️](../lib/cycles/dsp/Dsp.md#validation "structural test only; Ooples DSP uses different detrending") | - |
| **Deviation-Scaled MA** | [Dsma](../lib/trends_IIR/dsma/Dsma.md) | - | - | - | [⚠️](../lib/trends_IIR/dsma/Dsma.md#validation "structural test only; Ooples uses different deviation scaling") | - |
| **Directional Movement** | [Dm](../lib/dynamics/dm/Dm.md) | - | - | - | - | ❔ |
| **Directional Movement Index** | [Dx](../lib/dynamics/dx/Dx.md) | [✔️](../lib/dynamics/dx/Dx.md#validation) | [✔️](../lib/dynamics/dx/Dx.md#validation) | [✔️](../lib/dynamics/dx/Dx.md#validation) | [✔️](../lib/dynamics/dx/Dx.md#validation) | - |
| **Directional Movement Index (Jurik)** | [Dmx](../lib/dynamics/dmx/Dmx.md) | - | - | - | - | - |
| **Dirty Data Detection** | Dirty | - | - | - | - | - |
| **Donchian Channels** | [Dchannel](../lib/channels/dchannel/Dchannel.md) | - | - | [✔️](../lib/channels/dchannel/Dchannel.md#validation) | [⚠️](../lib/channels/dchannel/Dchannel.md#validation "structural test only; Ooples Donchian uses different channel width") | ❔ |
| **Double Exponential Moving Average** | [Dema](../lib/trends_IIR/dema/Dema.md) | [✔️](../lib/trends_IIR/dema/Dema.md#validation) | [✔️](../lib/trends_IIR/dema/Dema.md#validation) | [✔️](../lib/trends_IIR/dema/Dema.md#validation) | [✔️](../lib/trends_IIR/dema/Dema.md#validation) | ⚠️ |
| **Double Weighted Moving Average** | [Dwma](../lib/trends_FIR/dwma/Dwma.md) | [✔️](../lib/trends_FIR/dwma/Dwma.md#validation) | [✔️](../lib/trends_FIR/dwma/Dwma.md#validation) | [✔️](../lib/trends_FIR/dwma/Dwma.md#validation) | - | - |
| **Dynamic Momentum Index** | [Dymoi](../lib/oscillators/dymoi/Dymoi.md) | - | - | - | [⚠️](../lib/oscillators/dymoi/Dymoi.md#validation "structural test only; Ooples uses different dynamic period logic") | - |
| **Ease of Movement** | [Eom](../lib/volume/eom/Eom.md) | - | [✔️](../lib/volume/eom/Eom.md#validation) | - | - | ⚠️ |
| **Efficiency Ratio** | [Er](../lib/oscillators/er/Er.md) | - | - | - | - | ⚠️ |
| **Ehlers Autocorrelation Periodogram** | [Eacp](../lib/cycles/eacp/eacp.md) | - | - | - | - | - |
| **BandPass Filter** | [Bpf](../lib/filters/bpf/Bpf.md) | - | - | - | - | - |
| **Ehlers Center of Gravity** | [Cg](../lib/cycles/cg/Cg.md) | - | - | - | [⚠️](../lib/cycles/cg/Cg.md#validation "structural test only; Ooples CG uses different weighting scheme") | ⚠️ |
| **Ehlers Correlation Cycle** | [Ccor](../lib/cycles/ccor/Ccor.md) | - | - | - | - | - |
| **Ehlers Cyber Cycle** | [Ccyc](../lib/cycles/ccyc/Ccyc.md) | - | - | - | [⚠️](../lib/cycles/ccyc/Ccyc.md#validation "structural test only; Ooples Cyber Cycle uses different alpha") | - |
| **Ehlers Distance Coefficient Filter** | [Edcf](../lib/filters/edcf/Edcf.md) | - | - | - | - | - |
| **Ehlers Even Better Sinewave** | [Ebsw](../lib/cycles/ebsw/ebsw.md) | - | - | - | [⚠️](../lib/cycles/ebsw/ebsw.md#validation "structural test only; Ooples EBSW uses different pole filter") | - |
| **Ehlers Fractal Adaptive MA** | [Frama](../lib/trends_IIR/frama/Frama.md) | - | - | - | [⚠️](../lib/trends_IIR/frama/Frama.md#validation "structural test only; Ooples FRAMA uses different fractal calc") | - |
| **Ehlers Highpass Filter** | [Hpf](../lib/filters/hpf/Hpf.md) | - | - | - | [⚠️](../lib/filters/hpf/Hpf.md#validation "structural test only; Ooples HPF uses different cutoff mapping") | - |
| **Ehlers Phasor Analysis** | [Phasor](../lib/cycles/ht_phasor/HtPhasor.md) | - | - | - | - | - |
| **Ehlers Sine Wave** | [HtSine](../lib/cycles/ht_sine/HtSine.md) | [✔️](../lib/cycles/ht_sine/HtSine.md#validation) | - | - | - | - |
| **Ehlers SSF-Based Detrended Synthetic Price** | [Ssfdsp](../lib/cycles/ssfdsp/Ssfdsp.md) | - | - | - | - | - |
| **Ehlers 2-Pole Super Smooth Filter** | [Ssf2](../lib/filters/ssf2/Ssf2.md) | - | - | - | [✔️](../lib/filters/ssf2/Ssf2.md#validation) | ❔ |
| **Ehlers 3-Pole Super Smooth Filter** | [Ssf3](../lib/filters/ssf3/Ssf3.md) | - | - | - | - | ❔ |
| **Ehlers Ultrasmooth Filter** | [Usf](../lib/filters/usf/Usf.md) | - | - | - | - | - |
| **Elder Ray Index** | [Eri](../lib/oscillators/eri/Eri.md) | - | - | - | - | ❔ |
| **Elliptic (Cauer) Filter** | [Elliptic](../lib/filters/elliptic/Elliptic.md) | - | - | - | - | - |
| **Exponential Moving Average** | [Ema](../lib/trends_IIR/ema/Ema.md) | [✔️](../lib/trends_IIR/ema/Ema.md#validation) | [✔️](../lib/trends_IIR/ema/Ema.md#validation) | [✔️](../lib/trends_IIR/ema/Ema.md#validation) | [✔️](../lib/trends_IIR/ema/Ema.md#validation) | ⚠️ |
| **Exponential Transformation** | [Exptrans](../lib/numerics/exptrans/Exptrans.md) | - | - | - | - | - |
| **Exponential Weighted MA Volatility** | [Ewma](../lib/volatility/ewma/Ewma.md) | - | - | - | - | - |
| **Elder's Thermometer** | [Etherm](../lib/volatility/etherm/Etherm.md) | - | - | - | - | ⚠️ |
| **Elastic Volume Weighted MA** | [Evwma](../lib/volume/evwma/Evwma.md) | - | - | - | - | - |
| **Extended Traditional Pivots** | [Pivotext](../lib/reversals/pivotext/Pivotext.md) | - | - | - | - | - |
| **Fibonacci Pivot Points** | [Pivotfib](../lib/reversals/pivotfib/Pivotfib.md) | - | - | - | - | - |
| **Fibonacci Weighted MA** | [Fwma](../lib/trends_FIR/fwma/Fwma.md) | - | - | - | - | ❔ |
| **Ehlers Fisher Transform (2002)** | [Fisher](../lib/oscillators/fisher/Fisher.md) | - | [⚠️](../lib/oscillators/fisher/Fisher.md#validation "Tulip uses same Ehlers 2002 algorithm but takes high/low arrays; input type mismatch prevents numeric comparison") | [✔️](../lib/oscillators/fisher/Fisher.md#validation) | [⚠️](../lib/oscillators/fisher/Fisher.md#validation "Ooples uses same Ehlers 2002 algorithm but takes OHLCV; input type mismatch prevents numeric comparison") | ⚠️ |
| **Ehlers Fisher Transform (2004)** | [Fisher04](../lib/oscillators/fisher04/Fisher04.md) | - | - | - | - | - |
| **Force Index** | [Efi](../lib/volume/efi/Efi.md) | - | - | [✔️](../lib/volume/efi/Efi.md#validation) | [✔️](../lib/volume/efi/Efi.md#validation) | ⚠️ |
| **Fractal Chaos Bands** | [Fcb](../lib/channels/fcb/fcb.md) | - | - | [✔️](../lib/channels/fcb/fcb.md#validation) | [⚠️](../lib/channels/fcb/fcb.md#validation "structural test only; Ooples FCB uses different fractal width") | - |
| **Garman-Klass Volatility** | [Gkv](../lib/volatility/gkv/Gkv.md) | - | - | - | - | - |
| **Gator Oscillator** | [Gator](../lib/oscillators/gator/Gator.md) | - | - | - | - | - |
| **Gann High-Low Activator** | [Ghla](../lib/dynamics/ghla/Ghla.md) | - | - | - | - | ❔ |
| **Gaussian Filter** | [Gauss](../lib/filters/gauss/Gauss.md) | - | - | - | [⚠️](../lib/filters/gauss/Gauss.md#validation "structural test only; Ooples Gaussian uses different sigma mapping") | - |
| **Gaussian-Weighted MA** | [Gwma](../lib/trends_FIR/gwma/Gwma.md) | - | - | - | - | - |
| **Geometric Mean** | [Geomean](../lib/statistics/geomean/Geomean.md) | - | - | - | - | - |
| **Harmonic Mean** | [Harmean](../lib/statistics/harmean/Harmean.md) | - | - | - | - | - |
| **Granger Causality Test** | [Granger](../lib/statistics/granger/Granger.md) | - | - | - | - | - |
| **Hamming Window MA** | [Hamma](../lib/trends_FIR/hamma/Hamma.md) | - | - | - | - | - |
| **Hann FIR Filter** | [Hann](../lib/filters/hann/Hann.md) | - | - | - | - | - |
| **Hanning Window MA** | [Hanma](../lib/trends_FIR/hanma/Hanma.md) | - | - | - | - | - |
| **Heikin-Ashi** | [Ha](../lib/core/ha/Ha.md) | - | - | - | - | ❔ |
| **High-Low Volatility (Parkinson)** | [Hlv](../lib/volatility/hlv/Hlv.md) | - | - | - | - | - |
| **Highest value** | [Highest](../lib/numerics/highest/Highest.md) | [✔️](../lib/numerics/highest/Highest.md#validation) | [✔️](../lib/numerics/highest/Highest.md#validation) | - | - | - |
| **Ehlers Hilbert Transform Dominant Cycle Period** | [HtDcPeriod](../lib/cycles/ht_dcperiod/HtDcperiod.md) | [✔️](../lib/cycles/ht_dcperiod/HtDcperiod.md#validation) | - | - | - | - |
| **Ehlers Hilbert Transform Dominant Cycle Phase** | [HtDcPhase](../lib/cycles/ht_dcphase/HtDcphase.md) | [✔️](../lib/cycles/ht_dcphase/HtDcphase.md#validation) | - | - | - | - |
| **Ehlers Hilbert Transform Instantaneous Trend** | [Htit](../lib/trends_IIR/htit/Htit.md) | [✔️](../lib/trends_IIR/htit/Htit.md#validation) | - | [✔️](../lib/trends_IIR/htit/Htit.md#validation) | [✔️](../lib/trends_IIR/htit/Htit.md#validation) | ❔ |
| **Ehlers Hilbert Transform Phasor Components** | [HtPhasor](../lib/cycles/ht_phasor/HtPhasor.md) | [✔️](../lib/cycles/ht_phasor/HtPhasor.md#validation) | - | - | - | - |
| **Ehlers Hilbert Transform SineWave** | [HtSine](../lib/cycles/ht_sine/HtSine.md) | [✔️](../lib/cycles/ht_sine/HtSine.md#validation) | - | - | - | - |
| **Ehlers Hilbert Transform Trend vs Cycle Mode** | [Ht_trendmode](../lib/dynamics/ht_trendmode/HtTrendmode.md) | [✔️](../lib/dynamics/ht_trendmode/HtTrendmode.md#validation) | - | - | - | - |
| **Historical Volatility (Close-to-Close)** | [Hv](../lib/volatility/hv/Hv.md) | - | - | [✔️](../lib/volatility/hv/Hv.md#validation "validated on log-return series via GetStdDev with sample→population conversion; annualized and non-annualized") | - | - |
| **Hodrick-Prescott Filter** | [Hp](../lib/filters/hp/Hp.md) | - | - | - | - | - |
| **Holt Exponential Smoothing** | [Holt](../lib/trends_IIR/holt/Holt.md) | - | - | - | - | - |
| **Holt Weighted MA** | [Hwma](../lib/trends_IIR/hwma/Hwma.md) | - | - | - | - | ❔ |
| **Ehlers Homodyne Discriminator** | [Homod](../lib/cycles/homod/homod.md) | - | - | - | [⚠️](../lib/cycles/homod/homod.md#validation "structural test only; Ooples Homodyne uses different discriminator") | - |
| **Huber Loss** | [Huber](../lib/errors/huber/Huber.md) | - | - | - | - | - |
| **Hull Exponential MA** | [Hema](../lib/trends_IIR/hema/Hema.md) | - | - | - | - | - |
| **Hull Moving Average** | [Hma](../lib/trends_FIR/hma/Hma.md) | - | [✔️](../lib/trends_FIR/hma/Hma.md#validation) | [✔️](../lib/trends_FIR/hma/Hma.md#validation) | [⚠️](../lib/trends_FIR/hma/Hma.md#external-library-discrepancies) | ⚠️ |
| **Hurst Exponent** | [Hurst](../lib/statistics/hurst/Hurst.md) | - | - | [⚠️](../lib/statistics/hurst/Hurst.md#validation "structural test; different R/S subdivision strategies") | - | - |
| **Ichimoku Cloud** | [Ichimoku](../lib/dynamics/ichimoku/Ichimoku.md) | - | - | [✔️](../lib/dynamics/ichimoku/Ichimoku.md#validation) | [⚠️](../lib/dynamics/ichimoku/Ichimoku.md#validation "structural test only; Ooples cloud calc uses different shift period") | ❔ |
| **Impulse (Elder)** | [Impulse](../lib/dynamics/impulse/Impulse.md) | - | - | - | - | - |
| **Inertia** | [Inertia](../lib/oscillators/inertia/Inertia.md) | - | - | - | [⚠️](../lib/oscillators/inertia/Inertia.md#validation "structural test only; Ooples Inertia uses different regression length") | ⚠️ |
| **Interquartile Range** | [Iqr](../lib/statistics/iqr/Iqr.md) | - | - | - | - | - |
| **Intraday Intensity Index** | [Iii](../lib/volume/iii/Iii.md) | - | - | - | - | - |
| **Intraday Momentum Index** | Imi | - | - | - | - | - |
| **Jarque-Bera Test** | [Jb](../lib/statistics/jb/Jb.md) | - | - | - | - | - |
| **Jurik Moving Average** | [Jma](../lib/trends_IIR/jma/Jma.md) | - | - | - | [⚠️](../lib/trends_IIR/jma/Jma.md#validation "structural test only; Ooples JMA uses different phase parameter") | ❔ |
| **Jurik Volatility** | [Jvolty](../lib/volatility/jvolty/Jvolty.md) | - | - | - | - | - |
| **Jurik Adaptive Envelope Bands** | [Jbands](../lib/channels/jbands/Jbands.md) | - | - | - | - | - |
| **Jurik Volatility Normalized [0,100]** | [Jvoltyn](../lib/volatility/jvoltyn/Jvoltyn.md) | - | - | - | - | - |
| **Kalman Filter** | [Kalman](../lib/filters/kalman/Kalman.md) | - | - | - | - | - |
| **Kaufman Adaptive Moving Average** | [Kama](../lib/trends_IIR/kama/Kama.md) | [✔️](../lib/trends_IIR/kama/Kama.md#validation) | [✔️](../lib/trends_IIR/kama/Kama.md#validation) | [✔️](../lib/trends_IIR/kama/Kama.md#validation) | [✔️](../lib/trends_IIR/kama/Kama.md#validation) | ❔ |
| **KDJ Indicator** | [Kdj](../lib/oscillators/kdj/Kdj.md) | - | - | [⚠️](../lib/oscillators/kdj/Kdj.md#validation "structural test via GetStoch; KDJ uses RMA smoothing vs SMA") | - | ❔ |
| **Keltner Channel** | [Kchannel](../lib/channels/kchannel/kchannel.md) | - | - | [✔️](../lib/channels/kchannel/kchannel.md#validation) | [⚠️](../lib/channels/kchannel/kchannel.md#validation "structural test only; Ooples Keltner uses different ATR multiplier") | ❔ |
| **Kendall Rank Correlation** | [Kendall](../lib/statistics/kendall/Kendall.md) | - | - | - | - | - |
| **Klinger Volume Oscillator** | [Kvo](../lib/volume/kvo/Kvo.md) | - | [✔️](../lib/volume/kvo/Kvo.md#validation) | [✔️](../lib/volume/kvo/Kvo.md#validation) | [⚠️](../lib/volume/kvo/Kvo.md#validation "structural test only; Ooples KVO uses different volume weighting") | ❔ |
| **Know Sure Thing** | [Kst](../lib/oscillators/kst/Kst.md) | - | - | - | - | ❔ |
| **Kurtosis** | [Kurtosis](../lib/statistics/kurtosis/Kurtosis.md) | - | - | - | [✔️](../lib/statistics/kurtosis/Kurtosis.md#validation) | ❔ |
| **Ehlers Laguerre Filter** | [Laguerre](../lib/filters/laguerre/Laguerre.md) | - | - | - | - | - |
| **Ehlers Laguerre RSI** | [Lrsi](../lib/oscillators/lrsi/Lrsi.md) | - | - | - | [⚠️](../lib/oscillators/lrsi/Lrsi.md#validation "structural test only; Ooples Laguerre RSI uses different gamma") | - |
| **Least Mean Squares** | [Lms](../lib/filters/lms/Lms.md) | - | - | - | - | - |
| **Recursive Least Squares** | [Rls](../lib/filters/rls/Rls.md) | - | - | - | - | - |
| **Least Squares Moving Average** | [Lsma](../lib/trends_FIR/lsma/Lsma.md) | - | - | [✔️](../lib/trends_FIR/lsma/Lsma.md#validation) | [⚠️](../lib/trends_FIR/lsma/Lsma.md#validation "structural test only; Ooples LSMA uses different regression offset") | ⚠️ |
| **Linear Regression** | [LinReg](../lib/statistics/linreg/LinReg.md) | - | - | [✔️](../lib/statistics/linreg/LinReg.md#validation) | [⚠️](../lib/statistics/linreg/LinReg.md#validation) | - |
| **Linear Transformation** | [Lineartrans](../lib/numerics/lineartrans/Lineartrans.md) | - | - | - | - | - |
| **Linear Trend MA** | [Ltma](../lib/trends_IIR/ltma/Ltma.md) | - | - | - | - | - |
| **LOESS/LOWESS Smoothing** | [Loess](../lib/filters/loess/Loess.md) | - | - | - | - | - |
| **Logarithmic Transformation** | [Logtrans](../lib/numerics/logtrans/Logtrans.md) | - | - | - | - | - |
| **Logistic Function** | [Sigmoid](../lib/numerics/sigmoid/Sigmoid.md) | - | - | - | - | - |
| **Lowest value** | [Lowest](../lib/numerics/lowest/Lowest.md) | [✔️](../lib/numerics/lowest/Lowest.md#validation) | [✔️](../lib/numerics/lowest/Lowest.md#validation) | - | - | - |
| **Lunar Phase** | [Lunar](../lib/cycles/lunar/Lunar.md) | - | - | - | - | - |
| **MACD** | [Macd](../lib/momentum/macd/Macd.md) | [✔️](../lib/momentum/macd/Macd.md#validation) | [✔️](../lib/momentum/macd/Macd.md#validation) | [✔️](../lib/momentum/macd/Macd.md#validation) | [✔️](../lib/momentum/macd/Macd.md#validation) | ❔ |
| **Market Facilitation Index** | [Marketfi](../lib/oscillators/marketfi/Marketfi.md) | - | [✔️](../lib/oscillators/marketfi/Marketfi.md#validation) | - | - | - |
| **Mass Index** | [Massi](../lib/volatility/massi/Massi.md) | - | [⚠️](../lib/volatility/massi/Massi.md#validation "structural test only; Tulip Mass Index uses single EMA period vs dual") | [⚠️](../lib/volatility/massi/Massi.md#validation "structural test only; Skender Mass Index uses different EMA seeding") | [⚠️](../lib/volatility/massi/Massi.md#validation "structural test only; Ooples Mass Index uses different EMA seeding") | ❔ |
| **McGinley Dynamic** | [Mgdi](../lib/trends_IIR/mgdi/Mgdi.md) | - | - | [✔️](../lib/trends_IIR/mgdi/Mgdi.md#validation) | [✔️](../lib/trends_IIR/mgdi/Mgdi.md#validation) | ❔ |
| **Mean Absolute Error** | [Mae](../lib/errors/mae/Mae.md) | - | - | - | - | - |
| **Mean Absolute Percentage Difference** | [Mapd](../lib/errors/mapd/Mapd.md) | - | - | - | - | - |
| **Mean Absolute Percentage Error** | [Mape](../lib/errors/mape/Mape.md) | - | - | - | - | - |
| **Mean Absolute Scaled Error** | [Mase](../lib/errors/mase/Mase.md) | - | - | - | - | - |
| **Mean Error** | [Me](../lib/errors/me/Me.md) | - | - | - | - | - |
| **Mean Percentage Error** | [Mpe](../lib/errors/mpe/Mpe.md) | - | - | - | - | - |
| **Mean Squared Error** | [Mse](../lib/errors/mse/Mse.md) | - | - | - | - | - |
| **Mean Squared Logarithmic Error** | [Msle](../lib/errors/msle/Msle.md) | - | - | - | - | - |
| **Ehlers MESA Adaptive Moving Average** | [Mama](../lib/trends_IIR/mama/Mama.md) | [⚠️](../lib/trends_IIR/mama/Mama.md#validation "TALib uses Atan half-quadrant approximation; QuanTAlib uses Atan2 full-quadrant") | - | [✔️](../lib/trends_IIR/mama/Mama.md#validation) | [✔️](../lib/trends_IIR/mama/Mama.md#validation) | ❔ |
| **Median Price** | [Medprice](../lib/core/medprice/Medprice.md) | [✔️](../lib/core/medprice/Medprice.md#validation) | - | - | - | ⚠️ |
| **Mid Price** | [Midprice](../lib/core/midprice/Midprice.md) | [✔️](../lib/core/midprice/Midprice.md#validation) | - | - | - | ⚠️ |
| **Midpoint** | [Midpoint](../lib/core/midpoint/Midpoint.md) | [✔️](../lib/core/midpoint/Midpoint.md#validation) | - | - | - | ❔ |
| **Min-Max Channel** | [Mmchannel](../lib/channels/mmchannel/mmchannel.md) | - | - | [✔️](../lib/channels/mmchannel/mmchannel.md#validation) | - | - |
| **Min-Max Scaling (Normalization)** | [Normalize](../lib/numerics/normalize/Normalize.md) | - | - | - | - | - |
| **Mode (Most Frequent)** | [Mode](../lib/statistics/mode/Mode.md) | - | - | - | - | - |
| **Modified MA** | [Mma](../lib/trends_IIR/mma/Mma.md) | - | - | - | - | ❔ |
| **Modular Filter** | [Modf](../lib/filters/modf/Modf.md) | - | - | - | - | - |
| **Money Flow Index** | [Mfi](../lib/volume/mfi/Mfi.md) | [✔️](../lib/volume/mfi/Mfi.md#validation) | - | [✔️](../lib/volume/mfi/Mfi.md#validation) | [✔️](../lib/volume/mfi/Mfi.md#validation) | ✔️ |
| **Momentum** | [Mom](../lib/momentum/mom/Mom.md) | [✔️](../lib/momentum/mom/Mom.md#validation) | [✔️](../lib/momentum/mom/Mom.md#validation) | [✔️](../lib/momentum/mom/Mom.md#validation) | [⚠️](../lib/momentum/mom/Mom.md#validation "Ooples Mom = n-bar change multiplied by 100 vs absolute change") | ⚠️ |
| **Momentum change; 2nd derivative** | [Accel](../lib/numerics/accel/Accel.md) | - | - | - | - | - |
| **Moon Phase** | [Moon](../lib/cycles/moon/Moon.md)  | - | - | - | - | - |
| **Moving Average Envelopes** | [Maenv](../lib/channels/maenv/maenv.md) | - | - | [✔️](../lib/channels/maenv/maenv.md#validation) | [⚠️](../lib/channels/maenv/maenv.md#validation "structural test only; Ooples MA Envelopes uses different percentage band") | - |
| **Natural Moving Average** | [Nma](../lib/trends_IIR/nma/Nma.md) | - | - | - | - | - |
| **Negative Volume Index** | [Nvi](../lib/volume/nvi/Nvi.md) | - | [✔️](../lib/volume/nvi/Nvi.md#validation) | - | - | ⚠️ |
| **Normalized Average True Range** | [Natr](../lib/volatility/natr/Natr.md) | [✔️](../lib/volatility/natr/Natr.md#validation) | [✔️](../lib/volatility/natr/Natr.md#validation) | [✔️](../lib/volatility/natr/Natr.md#validation) | [✔️](../lib/volatility/natr/Natr.md#validation) | ❔ |
| **Normalized Shannon Entropy** | [Entropy](../lib/statistics/entropy/Entropy.md) | - | - | - | - | ⚠️ |
| **Notch Filter** | [Notch](../lib/filters/notch/Notch.md) | - | - | - | - | - |
| **Nadaraya-Watson Estimator** | [Nw](../lib/filters/nw/Nw.md) | - | - | - | - | - |
| **Open-Close Average** | [Midbody](../lib/core/midbody/Midbody.md) | - | - | [✔️](../lib/core/midbody/Midbody.md#validation) | - | - |
| **One Euro Filter** | [OneEuro](../lib/filters/oneeuro/OneEuro.md) | - | - | - | - | - |
| **On Balance Volume** | [Obv](../lib/volume/obv/Obv.md) | [⚠️](../lib/volume/obv/Obv.md#validation) | [✔️](../lib/volume/obv/Obv.md#validation) | [✔️](../lib/volume/obv/Obv.md#validation) | [⚠️](../lib/volume/obv/Obv.md#validation) | ✔️ |
| **Parabolic SAR** | [Psar](../lib/reversals/psar/Psar.md) | [✔️](../lib/reversals/psar/Psar.md#validation) | - | [✔️](../lib/reversals/psar/Psar.md#validation) | [⚠️](../lib/reversals/psar/Psar.md#validation "minor SAR initialization differences prevent numeric match") | ❔ |
| **Pascal Weighted Moving Average** | [Pwma](../lib/trends_FIR/pwma/Pwma.md) | - | - | - | [✔️](../lib/trends_FIR/pwma/Pwma.md#validation) | ❔ |
| **Percentage Change** | [Change](../lib/numerics/change/Change.md) | - | [✔️](../lib/numerics/change/Change.md#validation) | - | - | - |
| **Percentage Price Oscillator** | [Ppo](../lib/momentum/ppo/Ppo.md) | [✔️](../lib/momentum/ppo/Ppo.md#validation) | [✔️](../lib/momentum/ppo/Ppo.md#validation) | - | [✔️](../lib/momentum/ppo/Ppo.md#validation) | ❔ |
| **Percentage Volume Oscillator** | [Pvo](../lib/volume/pvo/Pvo.md) | - | - | [✔️](../lib/volume/pvo/Pvo.md#validation) | [⚠️](../lib/volume/pvo/Pvo.md#validation "structural test only; Ooples PVO uses different EMA periods") | ✔️ |
| **Percentile** | [Percentile](../lib/statistics/percentile/Percentile.md) | - | - | - | - | - |
| **Polarized Fractal Efficiency** | [Pfe](../lib/dynamics/pfe/Pfe.md) | - | - | - | - | - |
| **Pivot Points** | [Pivot](../lib/reversals/pivot/Pivot.md) | - | - | - | - | ❔ |
| **Pivot Points (Camarilla)** | [Pivotcam](../lib/reversals/pivotcam/Pivotcam.md) | - | - | - | - | - |
| **Pivot Points (DeMark)** | [Pivotdem](../lib/reversals/pivotdem/Pivotdem.md) | - | - | - | - | - |
| **Pivot Points (Extended)** | [Pivotext](../lib/reversals/pivotext/Pivotext.md) | - | - | - | - | - |
| **Pivot Points (Fibonacci)** | [Pivotfib](../lib/reversals/pivotfib/Pivotfib.md) | - | - | - | - | - |
| **Positive Volume Index** | [Pvi](../lib/volume/pvi/Pvi.md) | - | [✔️](../lib/volume/pvi/Pvi.md#validation) | - | - | ⚠️ |
| **Pretty Good Oscillator** | [Pgo](../lib/oscillators/pgo/Pgo.md) | - | - | - | [⚠️](../lib/oscillators/pgo/Pgo.md#validation "structural test only; Ooples PGO uses different ATR normalization") | ❔ |
| **Price Channel** | [Pchannel](../lib/channels/pchannel/pchannel.md) | - | - | [✔️](../lib/channels/pchannel/pchannel.md#validation) | - | - |
| **Price Momentum Oscillator** | [Pmo](../lib/momentum/pmo/Pmo.md) | - | - | [✔️](../lib/momentum/pmo/Pmo.md#validation) | [✔️](../lib/momentum/pmo/Pmo.md#validation) | - |
| **Price Relative Strength** | [Prs](../lib/momentum/prs/Prs.md) | - | - | [✔️](../lib/momentum/prs/Prs.md#validation) | - | - |
| **Price Volume Divergence** | [Pvd](../lib/volume/pvd/Pvd.md) | - | - | - | - | - |
| **Price Volume Rank** | [Pvr](../lib/volume/pvr/Pvr.md) | - | - | - | - | ⚠️ |
| **Price Volume Trend** | [Pvt](../lib/volume/pvt/Pvt.md) | - | - | - | [✔️](../lib/volume/pvt/Pvt.md#validation) | ⚠️ |
| **Psychological Line** | [Psl](../lib/oscillators/psl/Psl.md) | - | - | - | - | ⚠️ |
| **Qstick Indicator** | [Qstick](../lib/dynamics/qstick/Qstick.md) | - | - | - | - | ❔ |
| **Quad Exponential MA** | [Qema](../lib/trends_IIR/qema/Qema.md) | - | - | - | - | - |
| **QQE Indicator** | [Qqe](../lib/oscillators/qqe/Qqe.md) | - | - | - | - | ❔ |
| **Quantile** | [Quantile](../lib/statistics/quantile/Quantile.md) | - | - | - | - | ❔ |
| **Range Action Verification Index** | [Ravi](../lib/dynamics/ravi/Ravi.md) | - | - | - | - | - |
| **Rate of acceleration; 3rd derivative** | [Jerk](../lib/numerics/jerk/Jerk.md) | - | - | - | - | - |
| **Rate of Change** | [Roc](../lib/momentum/roc/Roc.md) | [✔️](../lib/momentum/roc/Roc.md#validation) | [✔️](../lib/momentum/roc/Roc.md#validation) | [✔️](../lib/momentum/roc/Roc.md#validation) | [⚠️](../lib/momentum/roc/Roc.md#validation "Ooples ROC returns percentage; QuanTAlib ROC returns absolute change") | ⚠️ |
| **Rate of change; 1st derivative** | [Slope](../lib/statistics/linreg/LinReg.md) | - | - | [✔️](../lib/statistics/linreg/LinReg.md#validation) | - | ⚠️ |
| **Rate of Change Percentage** | [Rocp](../lib/momentum/rocp/Rocp.md) | [✔️](../lib/momentum/rocp/Rocp.md#validation) | - | - | - | - |
| **Rate of Change Ratio** | [Rocr](../lib/momentum/rocr/Rocr.md) | [✔️](../lib/momentum/rocr/Rocr.md#validation) | [✔️](../lib/momentum/rocr/Rocr.md#validation) | - | - | - |
| **Realized Volatility** | [Rv](../lib/volatility/rv/Rv.md) | - | - | - | - | - |
| **Rectified Linear Unit** | [Relu](../lib/numerics/relu/Relu.md) | - | - | - | - | - |
| **Recursive Gaussian MA** | [Rgma](../lib/trends_IIR/rgma/Rgma.md) | - | - | - | - | - |
| **Ehlers Recursive Median Filter** | [Rmed](../lib/filters/rmed/Rmed.md) | - | - | - | - | - |
| **Reflex** | [Reflex](../lib/oscillators/reflex/Reflex.md) | - | - | - | - | - |
| **Regression Channels** | [Regchannel](../lib/channels/regchannel/regchannel.md) | - | - | - | - | - |
| **Regularized Exponential MA** | [Rema](../lib/trends_IIR/rema/Rema.md) | - | - | - | [⚠️](../lib/trends_IIR/rema/Rema.md#validation "structural test only; Ooples REMA uses different regularization lambda") | - |
| **Relative Absolute Error** | [Rae](../lib/errors/rae/Rae.md) | - | - | - | - | - |
| **Relative Squared Error** | [Rse](../lib/errors/rse/Rse.md) | - | - | - | - | - |
| **Relative Strength Index** | [Rsi](../lib/momentum/rsi/Rsi.md) | [✔️](../lib/momentum/rsi/Rsi.md#validation) | [✔️](../lib/momentum/rsi/Rsi.md#validation) | [✔️](../lib/momentum/rsi/Rsi.md#validation) | [✔️](../lib/momentum/rsi/Rsi.md#validation) | ⚠️ |
| **Relative Strength Quality Index** | [Rsx](../lib/momentum/rsx/Rsx.md) | - | - | - | [⚠️](../lib/momentum/rsx/Rsx.md#validation "structural test only; Ooples RSX uses different smoothing constants") | ✔️ |
| **Relative Volatility Index** | [Rvi](../lib/volatility/rvi/Rvi.md) | - | - | - | - | ❔ |
| **RVGI** | [Rvgi](../lib/oscillators/rvgi/Rvgi.md) | - | - | - | - | ❔ |
| **Renko** | - | - | - | ✔️ | - | - |
| **Rogers-Satchell Volatility** | [Rsv](../lib/volatility/rsv/Rsv.md) | - | - | - | - | - |
| **Ehlers Roofing Filter** | [Roofing](../lib/filters/roofing/Roofing.md) | - | - | - | [✔️](../lib/filters/roofing/Roofing.md#validation) | - |
| **Root Mean Squared Error** | [Rmse](../lib/errors/rmse/Rmse.md) | - | - | - | - | - |
| **Root Mean Squared Logarithmic Error** | [Rmsle](../lib/errors/rmsle/Rmsle.md) | - | - | - | - | - |
| **R-Squared** | [RSquared](../lib/statistics/linreg/LinReg.md) | - | - | [✔️](../lib/statistics/linreg/LinReg.md#validation) | - | - |
| **Savitzky-Golay Filter** | [Sgf](../lib/filters/sgf/Sgf.md) | - | - | - | - | - |
| **Savitzky-Golay MA** | [Sgma](../lib/trends_FIR/sgma/Sgma.md) | - | - | - | - | - |
| **Schaff Trend Cycle** | [Stc](../lib/oscillators/stc/stc.md) | - | - | [✔️](../lib/oscillators/stc/stc.md#validation) | [⚠️](../lib/oscillators/stc/stc.md#validation "structural test only; Ooples STC uses different stochastic smoothing") | ❔ |
| **Simple Moving Average** | [Sma](../lib/trends_FIR/sma/Sma.md) | [✔️](../lib/trends_FIR/sma/Sma.md#validation) | [✔️](../lib/trends_FIR/sma/Sma.md#validation) | [✔️](../lib/trends_FIR/sma/Sma.md#validation) | [✔️](../lib/trends_FIR/sma/Sma.md#validation) | ⚠️ |
| **Sine-weighted MA** | [Sinema](../lib/trends_FIR/sinema/Sinema.md) | - | - | - | - | ⚠️ |
| **Smoothed Adaptive Momentum** | [Sam](../lib/momentum/sam/Sam.md) | - | - | - | - | - |
| **Smoothed Moving Average** | [Rma](../lib/trends_IIR/rma/Rma.md) | - | - | [✔️](../lib/trends_IIR/rma/Rma.md#validation) | [✔️](../lib/trends_IIR/rma/Rma.md#validation) | ❔ |
| **SMI** | [Smi](../lib/oscillators/smi/Smi.md) | - | - | [⚠️](../lib/oscillators/smi/Smi.md#validation "structural test; different smoothing parameters") | [⚠️](../lib/oscillators/smi/Smi.md#validation "structural test only; Ooples SMI uses different double-smoothing") | ❔ |
| **Solar Activity Cycle** | [Solar](../lib/cycles/solar/Solar.md) | - | - | - | - | - |
| **Spearman Rank Correlation** | [Spearman](../lib/statistics/spearman/Spearman.md) | - | - | - | [⚠️](../lib/statistics/spearman/Spearman.md#validation "structural test only; Ooples Spearman uses different rank-tie handling") | - |
| **Ehlers Super Passband Filter** | [Spbf](../lib/filters/spbf/Spbf.md) | - | - | - | - | - |
| **Squeeze** | [Squeeze](../lib/oscillators/squeeze/Squeeze.md) | - | - | - | - | ❔ |
| **Square Root Transformation** | [Sqrttrans](../lib/numerics/sqrttrans/Sqrttrans.md) | - | - | - | - | - |
| **Standard Deviation Channel** | [Sdchannel](../lib/channels/sdchannel/sdchannel.md) | - | - | - | [⚠️](../lib/channels/sdchannel/sdchannel.md#validation "structural test only; Ooples SD Channel uses different multiplier") | - |
| **Standardization (Z-score)** | [Zscore](../lib/statistics/zscore/Zscore.md) | - | - | - | [⚠️](../lib/statistics/zscore/Zscore.md#validation "structural test only; Ooples Z-Score uses population vs sample stddev") | ⚠️ |
| **Starc Bands** | Starc | - | - | - | - | - |
| **Stochastic Fast** | [Stochf](../lib/oscillators/stochf/Stochf.md) | [✔️](../lib/oscillators/stochf/Stochf.md#validation) | - | [✔️](../lib/oscillators/stochf/Stochf.md#validation) | [⚠️](../lib/oscillators/stochf/Stochf.md#validation "structural test only; Ooples StochFast uses different smoothing period") | ❔ |
| **Stochastic Momentum Index** | [Smi](../lib/oscillators/smi/Smi.md) | - | - | [⚠️](../lib/oscillators/smi/Smi.md#validation "structural test; different smoothing parameters") | [⚠️](../lib/oscillators/smi/Smi.md#validation "structural test only; Ooples SMI uses different double-smoothing") | ❔ |
| **Stochastic Oscillator** | [Stoch](../lib/oscillators/stoch/Stoch.md) | [✔️](../lib/oscillators/stoch/Stoch.md#validation) | - | [✔️](../lib/oscillators/stoch/Stoch.md#validation) | - | ❔ |
| **Stochastic RSI** | [Stochrsi](../lib/oscillators/stochrsi/Stochrsi.md) | [✔️](../lib/oscillators/stochrsi/Stochrsi.md#validation) | - | [✔️](../lib/oscillators/stochrsi/Stochrsi.md#validation) | [✔️](../lib/oscillators/stochrsi/Stochrsi.md#validation) | ❔ |
| **Stoller Average Range Channel** | [Starchannel](../lib/channels/starchannel/starchannel.md) | - | - | [✔️](../lib/channels/starchannel/starchannel.md#validation) | [⚠️](../lib/channels/starchannel/starchannel.md#validation "structural test only; Ooples STARC uses different ATR multiplier") | - |
| **Super Trend Bands** | [Stbands](../lib/channels/stbands/Stbands.md) | - | - | - | - | - |
| **SuperTrend** | [Super](../lib/dynamics/super/Super.md) | - | - | [✔️](../lib/dynamics/super/Super.md#validation) | - | ❔ |
| **Swing High/Low Detection** | [Swings](../lib/reversals/swings/Swings.md) | - | - | - | - | - |
| **Symmetric Mean Absolute Percentage Error** | [Smape](../lib/errors/smape/Smape.md) | - | - | - | - | - |
| **Symmetric Weighted Moving Average** | [Swma](../lib/trends_FIR/swma/Swma.md) | - | - | - | - | ⚠️ |
| **T3 Moving Average** | [T3](../lib/trends_IIR/t3/T3.md) | [✔️](../lib/trends_IIR/t3/T3.md#validation) | - | [✔️](../lib/trends_IIR/t3/T3.md#validation) | [✔️](../lib/trends_IIR/t3/T3.md#validation) | ❔ |
| **Theil Index** | [Theil](../lib/statistics/theil/Theil.md) | - | - | - | - | - |
| **Time Series Forecast** | [Tsf](../lib/trends_FIR/tsf/Tsf.md) | [✔️](../lib/trends_FIR/tsf/Tsf.md#validation) | [✔️](../lib/trends_FIR/tsf/Tsf.md#validation) | [✔️](../lib/trends_FIR/tsf/Tsf.md#validation) | [⚠️](../lib/trends_FIR/tsf/Tsf.md#validation "bar alignment differs; Ooples default period=500 shifts output") | - |
| **Time Weighted Average Price** | [Twap](../lib/volume/twap/Twap.md) | - | - | - | - | - |
| **Trade Volume Index** | [Tvi](../lib/volume/tvi/Tvi.md) | - | - | - | [⚠️](../lib/volume/tvi/Tvi.md#validation "structural test only; Ooples TVI uses different tick threshold") | - |
| **TrendFlex** | [Trendflex](../lib/oscillators/trendflex/Trendflex.md) | - | - | - | - | ⚠️ |
| **Triangular Moving Average** | [Trima](../lib/trends_FIR/trima/Trima.md) | [✔️](../lib/trends_FIR/trima/Trima.md#validation) | [✔️](../lib/trends_FIR/trima/Trima.md#validation) | [✔️](../lib/trends_FIR/trima/Trima.md#validation) | [⚠️](../lib/trends_FIR/trima/Trima.md#validation "structural test only; Ooples TRIMA uses different triangle weighting") | ⚠️ |
| **Triple Exponential Average** | [Trix](../lib/oscillators/trix/Trix.md) | [✔️](../lib/oscillators/trix/Trix.md#validation) | [✔️](../lib/oscillators/trix/Trix.md#validation) | [✔️](../lib/oscillators/trix/Trix.md#validation) | [⚠️](../lib/oscillators/trix/Trix.md#validation "structural test only; Ooples TRIX uses different signal smoothing") | ✔️ |
| **Triple Exponential Moving Average** | [Tema](../lib/trends_IIR/tema/Tema.md) | [✔️](../lib/trends_IIR/tema/Tema.md#validation) | [✔️](../lib/trends_IIR/tema/Tema.md#validation) | [✔️](../lib/trends_IIR/tema/Tema.md#validation) | [⚠️](../lib/trends_IIR/tema/Tema.md#validation "diverges for large periods; Ooples uses different EMA initialization") | ⚠️ |
| **Trend Regularity Adaptive MA** | [Trama](../lib/trends_IIR/trama/Trama.md) | - | - | - | - | - |
| **True Range** | [Tr](../lib/volatility/tr/Tr.md) | [✔️](../lib/volatility/tr/Tr.md#validation) | [✔️](../lib/volatility/tr/Tr.md#validation) | [✔️](../lib/volatility/tr/Tr.md#validation) | - | ✔️ |
| **True Strength Index** | [Tsi](../lib/momentum/tsi/Tsi.md) | - | - | [✔️](../lib/momentum/tsi/Tsi.md#validation) | [✔️](../lib/momentum/tsi/Tsi.md#validation) | ⚠️ |
| **Typical Price** | [Typprice](../lib/core/typprice/Typprice.md) | [✔️](../lib/core/typprice/Typprice.md#validation) | - | - | - | ⚠️ |
| **TTM Trend** | [Ttm](../lib/dynamics/ttm_trend/TtmTrend.md) | - | - | - | - | ❔ |
| **TTM Scalper Alert** | [TtmScalper](../lib/reversals/ttm_scalper/TtmScalper.md) | - | - | - | - | - |
| **TTM Wave** | [TtmWave](../lib/oscillators/ttm_wave/TtmWave.md) | - | - | - | - | - |
| **Ulcer Index** | [Ui](../lib/volatility/ui/Ui.md) | - | - | - | [⚠️](../lib/volatility/ui/Ui.md#validation "structural test only; Ooples Ulcer Index uses different drawdown calc") | ❔ |
| **Ehlers Ultimate Bands** | [Ubands](../lib/channels/ubands/Ubands.md) | - | - | - | - | - |
| **Ehlers Ultimate Channel** | [Uchannel](../lib/channels/uchannel/Uchannel.md) | - | - | - | - | - |
| **Ultimate Oscillator** | [Ultosc](../lib/oscillators/ultosc/Ultosc.md) | [✔️](../lib/oscillators/ultosc/Ultosc.md#validation) | [✔️](../lib/oscillators/ultosc/Ultosc.md#validation) | [✔️](../lib/oscillators/ultosc/Ultosc.md#validation) | [✔️](../lib/oscillators/ultosc/Ultosc.md#validation) | ❔ |
| **Variable Index Dynamic Average** | [Vidya](../lib/trends_IIR/vidya/Vidya.md) | - | - | - | - | ❔ |
| **Velocity (Jurik)** | [Vel](../lib/momentum/vel/Vel.md) | - | - | - | - | - |
| **Vertical Horizontal Filter** | [Vhf](../lib/dynamics/vhf/Vhf.md) | - | - | [⚠️](../lib/dynamics/vhf/Vhf.md#validation "Tulip window = n+1 bars; QuanTAlib window = n bars (~5% divergence)") | - | ❔ |
| **Volatility Adjusted Moving Average** | [Vama](../lib/trends_IIR/vama/Vama.md) | - | - | - | - | - |
| **Volatility of Volatility** | [Vov](../lib/volatility/vov/Vov.md) | - | - | - | - | - |
| **Volatility Ratio** | [Vr](../lib/volatility/vr/Vr.md) | - | - | - | - | - |
| **Volume Accumulation** | [Va](../lib/volume/va/Va.md) | - | - | - | - | - |
| **Volume Force** | [Vf](../lib/volume/vf/Vf.md) | - | - | - | - | - |
| **Volume Oscillator** | [Vo](../lib/volume/vo/Vo.md) | - | [✔️](../lib/volume/vo/Vo.md#validation) | - | - | - |
| **Volume Rate of Change** | [Vroc](../lib/volume/vroc/Vroc.md) | - | - | - | - | - |
| **Volume Weighted Accumulation/Distribution** | [Vwad](../lib/volume/vwad/Vwad.md) | - | - | - | - | - |
| **Volume Weighted Average Price** | [Vwap](../lib/volume/vwap/Vwap.md) | - | - | - | - | ❔ |
| **Volume Weighted Moving Average** | [Vwma](../lib/volume/vwma/Vwma.md) | - | - | [✔️](../lib/volume/vwma/Vwma.md#validation) | [✔️](../lib/volume/vwma/Vwma.md#validation) | ⚠️ |
| **Vortex Indicator** | [Vortex](../lib/dynamics/vortex/Vortex.md) | - | - | [✔️](../lib/dynamics/vortex/Vortex.md#validation) | [⚠️](../lib/dynamics/vortex/Vortex.md#validation "structural test only; Ooples Vortex uses different ATR normalization") | ❔ |
| **Ehlers Voss Predictive Filter** | [Voss](../lib/filters/voss/Voss.md) | - | - | - | [✔️](../lib/filters/voss/Voss.md#validation) | - |
| **VWAP Bands** | [Vwapbands](../lib/channels/vwapbands/Vwapbands.md) | - | - | - | - | - |
| **VWAP with Standard Deviation Bands** | [Vwapsd](../lib/channels/vwapsd/Vwapsd.md) | - | - | - | - | - |
| **Wavelet Denoising Filter** | [Wavelet](../lib/filters/wavelet/Wavelet.md) | - | - | - | - | - |
| **Weighted Close Price** | [Wclprice](../lib/core/wclprice/Wclprice.md) | [✔️](../lib/core/wclprice/Wclprice.md#validation) | - | - | - | ⚠️ |
| **Weighted Moving Average** | [Wma](../lib/trends_FIR/wma/Wma.md) | [✔️](../lib/trends_FIR/wma/Wma.md#validation) | [✔️](../lib/trends_FIR/wma/Wma.md#validation) | [✔️](../lib/trends_FIR/wma/Wma.md#validation) | - | ⚠️ |
| **Wiener Filter** | [Wiener](../lib/filters/wiener/Wiener.md) | - | - | - | - | - |
| **Williams %R** | [Willr](../lib/oscillators/willr/Willr.md) | [✔️](../lib/oscillators/willr/Willr.md#validation) | [✔️](../lib/oscillators/willr/Willr.md#validation) | [✔️](../lib/oscillators/willr/Willr.md#validation) | [⚠️](../lib/oscillators/willr/Willr.md#validation "structural test only; Ooples %R uses different lookback period") | ❔ |
| **Williams Accumulation/Distribution** | [Wad](../lib/volume/wad/Wad.md) | - | [✔️](../lib/volume/wad/Wad.md#validation) | - | [⚠️](../lib/volume/wad/Wad.md#validation) | - |
| **Williams Alligator** | [Alligator](../lib/dynamics/alligator/Alligator.md) | - | - | [✔️](../lib/dynamics/alligator/Alligator.md#validation) | [⚠️](../lib/dynamics/alligator/Alligator.md#validation "structural test only; Ooples Alligator uses different SMMA seeding") | ❔ |
| **Williams Fractal** | [Fractals](../lib/reversals/fractals/Fractals.md) | - | - | [✔️](../lib/reversals/fractals/Fractals.md#validation) | - | - |
| **Woodie's Pivot Points** | [Pivotwood](../lib/reversals/pivotwood/Pivotwood.md) | - | - | - | - | - |
| **Yang-Zhang Volatility** | [Yzv](../lib/volatility/yzv/Yzv.md) | - | - | - | - | - |
| **Yang-Zhang Volatility Adjusted MA** | [Yzvama](../lib/trends_IIR/yzvama/Yzvama.md) | - | - | - | - | - |
| **Zero-Lag Double Exponential MA** | [Zldema](../lib/trends_IIR/zldema/Zldema.md) | - | - | - | - | - |
| **Zero-Lag Exponential Moving Average** | [Zlema](../lib/trends_IIR/zlema/Zlema.md) | - | - | [⚠️](../lib/trends_IIR/zlema/Zlema.md#validation "structural test only; Tulip ZLEMA period alignment differs") | - | ❔ |
| **Zero-Lag Triple Exponential MA** | [Zltema](../lib/trends_IIR/zltema/Zltema.md) | - | - | - | [⚠️](../lib/trends_IIR/zltema/Zltema.md#validation "structural test only; Ooples ZLTEMA uses different lag compensation") | - |
| **ZigZag** | - | - | - | ✔️ | - | ❔ |
| **Z-score standardization** | [Zscore](../lib/statistics/zscore/Zscore.md) | - | - | - | [✔️](../lib/statistics/zscore/Zscore.md#validation) | ⚠️ |
| **Z-Test** | [Ztest](../lib/statistics/ztest/Ztest.md) | - | - | - | - | - |

## Statistical Indicators

| Indicator | QuanTAlib | MathNet | TA-Lib | Tulip | Skender | pandas-ta |
| :-------- | :-------- | :-----: | :----: | :---: | :-----: | :-------: |
| **Autocorrelation Function** | [Acf](../lib/statistics/acf/Acf.md) | - | - | - | - | - |
| **Covariance** | [Covariance](../lib/statistics/covariance/Covariance.md) | - | - | - | - | - |
| **Entropy (Shannon)** | [Entropy](../lib/statistics/entropy/Entropy.md) | - | - | - | - | ⚠️ |
| **Geometric Mean** | [Geomean](../lib/statistics/geomean/Geomean.md) | - | - | - | - | - |
| **Harmonic Mean** | [Harmean](../lib/statistics/harmean/Harmean.md) | - | - | - | - | - |
| **Hurst Exponent** | [Hurst](../lib/statistics/hurst/Hurst.md) | - | - | [⚠️](../lib/statistics/hurst/Hurst.md#validation "structural test; different R/S subdivision strategies") | - | - |
| **Interquartile Range** | [Iqr](../lib/statistics/iqr/Iqr.md) | - | - | - | - | - |
| **Granger Causality** | [Granger](../lib/statistics/granger/Granger.md) | - | - | - | - | - |
| **Jarque-Bera Test** | [Jb](../lib/statistics/jb/Jb.md) | - | - | - | - | - |
| **Kendall Rank Correlation** | [Kendall](../lib/statistics/kendall/Kendall.md) | - | - | - | - | - |
| **Median (Statistical)** | [Median](../lib/statistics/median/Median.md) | [✔️](../lib/statistics/median/Median.md#validation) | - | - | - | ❔ |
| **Mode** | [Mode](../lib/statistics/mode/Mode.md) | - | - | - | - | - |
| **Percentile** | [Percentile](../lib/statistics/percentile/Percentile.md) | - | - | - | - | - |
| **Quantile** | [Quantile](../lib/statistics/quantile/Quantile.md) | - | - | - | - | ❔ |
| **Skewness** | [Skew](../lib/statistics/skew/Skew.md) | [✔️](../lib/statistics/skew/Skew.md#validation) | - | - | - | ❔ |
| **Spearman Rank Correlation** | [Spearman](../lib/statistics/spearman/Spearman.md) | - | - | - | - | - |
| **Standard Deviation** | [StdDev](../lib/statistics/stddev/StdDev.md) | [✔️](../lib/statistics/stddev/StdDev.md#validation) | [✔️](../lib/statistics/stddev/StdDev.md#validation) | [✔️](../lib/statistics/stddev/StdDev.md#validation) | [✔️](../lib/statistics/stddev/StdDev.md#validation) | ⚠️ |
| **Sum (Rolling)** | [Sum](../lib/statistics/sum/Sum.md) | - | [✔️](../lib/statistics/sum/Sum.md#validation) | [✔️](../lib/statistics/sum/Sum.md#validation) | - | - |
| **Theil T Index** | [Theil](../lib/statistics/theil/Theil.md) | - | - | - | - | - |
| **Partial Autocorrelation Function** | [Pacf](../lib/statistics/pacf/Pacf.md) | - | - | - | - | - |
| **Variance** | [Variance](../lib/statistics/variance/Variance.md) | [✔️](../lib/statistics/variance/Variance.md#validation) | [✔️](../lib/statistics/variance/Variance.md#validation) | [✔️](../lib/statistics/variance/Variance.md#validation) | [✔️](../lib/statistics/variance/Variance.md#validation) | ⚠️ |
| **Mean Absolute Deviation** | [MeanDev](../lib/statistics/meandev/MeanDev.md) | - | - | [✔️](../lib/statistics/meandev/MeanDev.md#validation) | - | ❔ |
| **Standard Error of Regression** | [Stderr](../lib/statistics/stderr/Stderr.md) | - | - | [⚠️](../lib/statistics/stderr/Stderr.md#validation "Tulip stderr = StdDev/sqrt(n) (SE of mean); QuanTAlib = sqrt(SSR/(n-2)) (SE of OLS regression)") | - | - |
| **Z-Score** | [Zscore](../lib/statistics/zscore/Zscore.md) | - | - | [⚠️](../lib/statistics/zscore/Zscore.md#validation "structural test via GetStdDev; no native GetZScore in Skender v2") | - | ⚠️ |
| **Z-Test** | [Ztest](../lib/statistics/ztest/Ztest.md) | - | - | - | - | - |

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

## pandas-ta Indicators Not in QuanTAlib

The following 30 indicators are available in [pandas-ta](https://github.com/twopirllc/pandas_ta) but have no equivalent implementation in the QuanTAlib C# library. These represent potential future additions.

### Candle Patterns (5)

| pandas-ta | Description |
| :-------- | :---------- |
| `cdl` | Candlestick pattern detection (multi-pattern) |
| `cdl_doji` | Doji candle detection |
| `cdl_inside` | Inside bar detection |
| `cdl_pattern` | Named candlestick pattern matching |
| `cdl_z` | Z-score candle analysis |

### Performance (3)

| pandas-ta | Description |
| :-------- | :---------- |
| `drawdown` | Maximum drawdown analysis |
| `log_return` | Logarithmic returns |
| `percent_return` | Percentage returns |

### Momentum (5)

| pandas-ta | Description |
| :-------- | :---------- |
| `exhc` | Exhaustion candles |
| `smc` | Squeeze Momentum Composite |
| `squeeze_pro` | TTM Squeeze Pro (extended) |
| `tmo` | True Momentum Oscillator |
| `dm` | Directional Movement (+DI/-DI separate) |

### Trend (8)

| pandas-ta | Description |
| :-------- | :---------- |
| `decay` | Linear decay function |
| `decreasing` | Decreasing trend detection |
| `increasing` | Increasing trend detection |
| `long_run` | Long run length analysis |
| `short_run` | Short run length analysis |
| `rwi` | Random Walk Index |
| `zigzag` | ZigZag pivot detection |
| `alphatrend` | Alpha Trend |

### Volatility (4)

| pandas-ta | Description |
| :-------- | :---------- |
| `atrts` | ATR Trailing Stop |
| `hwc` | Holt-Winter Channel |
| `pdist` | Price Distance |
| `thermo` | Thermometer indicator |

### Volume (4)

| pandas-ta | Description |
| :-------- | :---------- |
| `pvol` | Price Volume oscillator |
| `tsv` | Time Segmented Volume |
| `vhm` | Volume Heat Map |
| `vp` | Volume Profile |

### Statistics (1)

| pandas-ta | Description |
| :-------- | :---------- |
| `tos_stdevall` | ThinkOrSwim StdDev All |

## Validation Libraries

| Library | Language | License | Notes |
| :------ | :------- | :------ | :---- |
| [TA-Lib](https://ta-lib.org/) | C (via .NET wrapper) | BSD | Industry standard. C implementation, battle-tested. |
| [Tulip](https://tulipindicators.org/) | C (via .NET wrapper) | LGPL | Lightweight, well-documented. |
| [Skender.Stock.Indicators](https://dotnet.stockindicators.dev/) | C# | MIT | Pure .NET. Active development. |
| [OoplesFinance](https://github.com/ooples/OoplesFinance.StockIndicators) | C# | Apache 2.0 | Large indicator collection. Validation coverage varies. |
| [MathNet.Numerics](https://numerics.mathdotnet.com/) | C# | MIT | Statistical functions, not TA-specific. |
| [pandas-ta](https://github.com/twopirllc/pandas-ta) | Python | MIT | 130+ indicators. Python-native with optional TA-Lib acceleration. |


## Validation Philosophy

Three levels of confidence:

**Level 1: Cross-Library Agreement**
Multiple independent implementations produce identical results. Highest confidence. Most mainstream indicators (SMA, EMA, RSI, MACD) fall here.

**Level 2: Original Source Agreement**
No cross-library validation available, but implementation matches original research paper or patent description. JMA, various proprietary indicators fall here.

**Level 3: Mathematical Correctness Only**
No external reference exists. Implementation verified through unit tests, edge case handling, and mathematical properties (e.g., filter stability, energy preservation). Novel or obscure indicators fall here.

## Discrepancy Investigation

When validation fails:

1. **Check parameter mapping.** TA-Lib uses 0-based indexing for some parameters. Skender uses 1-based.
2. **Check warmup handling.** Different libraries handle the first N values differently.
3. **Check smoothing assumptions.** Some libraries use SMA for initial EMA seed. Others use the first value.
4. **Check edge cases.** NaN handling, zero division, and boundary conditions vary.

Discrepancies are documented in the indicator's markdown file under a "Validation Notes" section. The goal is not to match every library exactly. The goal is to understand why differences exist and document them.
