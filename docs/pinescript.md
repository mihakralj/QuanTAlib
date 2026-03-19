# PineScript Guide: QuanTAlib Indicators for TradingView

> "You do not need to understand the math. But the math does not care whether you understand it."

## Welcome, Brave Copy-Paster

You are here because you want an indicator on your TradingView chart. Maybe someone on Crypto Twitter posted a screenshot with colored lines and you thought "I need that." Maybe you googled "best RSI Pine Script" at 2 AM. Maybe you clicked a link by accident. All valid paths to enlightenment.

Here is the good news: QuanTAlib provides **Pine Script v6 source code** for every one of its 393 indicators. Each script is self-contained, tested against the C# reference implementation, and ready to paste into TradingView's Pine Editor. No dependencies. No imports. No subscription to someone's Discord.

Here is the less-good news: the scripts contain actual mathematics. You do not have to read it. But it is there, silently judging.

## How to Use a Pine Script (The Short Version)

1. Find the indicator you want (see tables below)
2. Click the link to open the `.pine` file
3. Copy the entire file contents
4. Open TradingView. Click "Pine Editor" at the bottom
5. Delete whatever is in there. Paste the code
6. Click "Add to chart"
7. Adjust the inputs in the indicator settings panel

That is it. You have deployed a mathematically rigorous indicator without writing a single line of code. Your ancestors would be proud.

## Finding Your Indicator

Every indicator has a `.pine` file sitting next to its C# implementation and documentation. The folder structure is predictable:

``` shell
lib/
  trends_IIR/
    ema/
      Ema.cs          ← the actual engine (C#, you can ignore this)
      Ema.md          ← documentation (math, history, validation)
      ema.pine        ← THIS IS WHAT YOU WANT
```

### By category

Pick your category. Find your indicator. Click the `.pine` link. Copy. Paste. Done.

---

#### Core (price transforms)

These turn OHLC data into derived price series. If you do not know what "typical price" means, you probably want `close` instead.

| Indicator | What It Does | Pine Script |
| :--- | :--- | :--- |
| AVGPRICE | Average Price | [avgprice.pine](../lib/core/avgprice/avgprice.pine) |
| HA | Heikin-Ashi | [ha.pine](../lib/core/ha/ha.pine) |
| MEDPRICE | Median Price | [medprice.pine](../lib/core/medprice/medprice.pine) |
| MIDPOINT | Rolling Range Midpoint | [midpoint.pine](../lib/core/midpoint/midpoint.pine) |
| MIDPRICE | Midpoint Price | [midprice.pine](../lib/core/midprice/midprice.pine) |
| TYPPRICE | Typical Price | [typprice.pine](../lib/core/typprice/typprice.pine) |
| WCLPRICE | Weighted Close Price | [wclprice.pine](../lib/core/wclprice/wclprice.pine) |

---

#### Moving Averages: FIR (the "simple" ones)

FIR stands for Finite Impulse Response. It means the average uses a fixed window of bars. SMA is an FIR filter. You have been using FIR filters this whole time.

| Indicator | What It Does | Pine Script |
| :--- | :--- | :--- |
| ALMA | Arnaud Legoux MA | [alma.pine](../lib/trends_FIR/alma/alma.pine) |
| BLMA | Blackman Window MA | [blma.pine](../lib/trends_FIR/blma/blma.pine) |
| BWMA | Bessel-Weighted MA | [bwma.pine](../lib/trends_FIR/bwma/bwma.pine) |
| CONV | Convolution MA | [conv.pine](../lib/trends_FIR/conv/conv.pine) |
| CRMA | Cubic Regression MA | [crma.pine](../lib/trends_FIR/crma/crma.pine) |
| DWMA | Double Weighted MA | [dwma.pine](../lib/trends_FIR/dwma/dwma.pine) |
| FWMA | Fibonacci Weighted MA | [fwma.pine](../lib/trends_FIR/fwma/fwma.pine) |
| GWMA | Gaussian Weighted MA | [gwma.pine](../lib/trends_FIR/gwma/gwma.pine) |
| HAMMA | Hamming MA | [hamma.pine](../lib/trends_FIR/hamma/hamma.pine) |
| HANMA | Hanning MA | [hanma.pine](../lib/trends_FIR/hanma/hanma.pine) |
| HEND | Henderson Moving Average | [hend.pine](../lib/trends_FIR/hend/hend.pine) |
| HMA | Hull MA | [hma.pine](../lib/trends_FIR/hma/hma.pine) |
| ILRS | Integral of LinReg Slope | [ilrs.pine](../lib/trends_FIR/ilrs/ilrs.pine) |
| KAISER | Kaiser Window MA | [kaiser.pine](../lib/trends_FIR/kaiser/kaiser.pine) |
| LANCZOS | Lanczos (sinc) Window MA | [lanczos.pine](../lib/trends_FIR/lanczos/lanczos.pine) |
| LSMA | Least Squares MA | [lsma.pine](../lib/trends_FIR/lsma/lsma.pine) |
| NLMA | Non-Lag MA | [nlma.pine](../lib/trends_FIR/nlma/nlma.pine) |
| NYQMA | Nyquist MA | [nyqma.pine](../lib/trends_FIR/nyqma/nyqma.pine) |
| PARZEN | Parzen Window MA | [parzen.pine](../lib/trends_FIR/parzen/parzen.pine) |
| PMA | Predictive Moving Average | [pma.pine](../lib/trends_FIR/pma/pma.pine) |
| PWMA | Pascal Weighted MA | [pwma.pine](../lib/trends_FIR/pwma/pwma.pine) |
| QRMA | Quadratic Regression MA | [qrma.pine](../lib/trends_FIR/qrma/qrma.pine) |
| RAIN | Rainbow MA | [rain.pine](../lib/trends_FIR/rain/rain.pine) |
| RWMA | Range Weighted MA | [rwma.pine](../lib/trends_FIR/rwma/rwma.pine) |
| SGMA | Savitzky-Golay MA | [sgma.pine](../lib/trends_FIR/sgma/sgma.pine) |
| SINEMA | Sine Weighted MA | [sinema.pine](../lib/trends_FIR/sinema/sinema.pine) |
| SMA | Simple MA | [sma.pine](../lib/trends_FIR/sma/sma.pine) |
| SP15 | Spencer's 15-Point MA | [sp15.pine](../lib/trends_FIR/sp15/sp15.pine) |
| SWMA | Symmetric Weighted MA | [swma.pine](../lib/trends_FIR/swma/swma.pine) |
| TRIMA | Triangular MA | [trima.pine](../lib/trends_FIR/trima/trima.pine) |
| TSF | Time Series Forecast | [tsf.pine](../lib/trends_FIR/tsf/tsf.pine) |
| TUKEY_W | Tukey (Tapered Cosine) Window MA | [tukey_w.pine](../lib/trends_FIR/tukey_w/tukey_w.pine) |
| WMA | Weighted MA | [wma.pine](../lib/trends_FIR/wma/wma.pine) |

---

#### Moving Averages: IIR (the "smart" ones)

IIR stands for Infinite Impulse Response. These use recursive feedback: the previous output affects the current output. Generally smoother, lower lag, and harder to understand. The indicator descriptions link to documentation if curiosity ever strikes.

| Indicator | What It Does | Pine Script |
| :--- | :--- | :--- |
| ADXVMA | ADX Variable MA | [adxvma.pine](../lib/trends_IIR/adxvma/adxvma.pine) |
| AHRENS | Ahrens MA | [ahrens.pine](../lib/trends_IIR/ahrens/ahrens.pine) |
| CORAL | Coral Trend Filter | [coral.pine](../lib/trends_IIR/coral/coral.pine) |
| DECYCLER | Ehlers Decycler | [decycler.pine](../lib/trends_IIR/decycler/decycler.pine) |
| DEMA | Double Exponential MA | [dema.pine](../lib/trends_IIR/dema/dema.pine) |
| DSMA | Deviation-Scaled MA | [dsma.pine](../lib/trends_IIR/dsma/dsma.pine) |
| EMA | Exponential MA | [ema.pine](../lib/trends_IIR/ema/ema.pine) |
| FRAMA | Ehlers Fractal Adaptive MA | [frama.pine](../lib/trends_IIR/frama/frama.pine) |
| GDEMA | Generalized DEMA | [gdema.pine](../lib/trends_IIR/gdema/gdema.pine) |
| HEMA | Hull Exponential MA | [hema.pine](../lib/trends_IIR/hema/hema.pine) |
| HOLT | Holt Exponential Smoothing | [holt.pine](../lib/trends_IIR/holt/holt.pine) |
| HT_TRENDLINE | Ehlers Hilbert Transform Instant Trendline | [ht_trendline.pine](../lib/trends_IIR/ht_trendline/ht_trendline.pine) |
| HWMA | Holt-Winters MA | [hwma.pine](../lib/trends_IIR/hwma/hwma.pine) |
| JMA | Jurik MA | [jma.pine](../lib/trends_IIR/jma/jma.pine) |
| KAMA | Kaufman Adaptive MA | [kama.pine](../lib/trends_IIR/kama/kama.pine) |
| LEMA | Leader EMA | [lema.pine](../lib/trends_IIR/lema/lema.pine) |
| LTMA | Linear Trend MA | [ltma.pine](../lib/trends_IIR/ltma/ltma.pine) |
| MAMA | Ehlers MESA Adaptive MA | [mama.pine](../lib/trends_IIR/mama/mama.pine) |
| MAVP | Moving Average Variable Period | [mavp.pine](../lib/trends_IIR/mavp/mavp.pine) |
| MCNMA | McNicholl EMA | [mcnma.pine](../lib/trends_IIR/mcnma/mcnma.pine) |
| MGDI | McGinley Dynamic | [mgdi.pine](../lib/trends_IIR/mgdi/mgdi.pine) |
| MMA | Modified MA | [mma.pine](../lib/trends_IIR/mma/mma.pine) |
| NMA | Natural Moving Average | [nma.pine](../lib/trends_IIR/nma/nma.pine) |
| QEMA | Quadruple Exponential MA | [qema.pine](../lib/trends_IIR/qema/qema.pine) |
| REMA | Regularized Exponential MA | [rema.pine](../lib/trends_IIR/rema/rema.pine) |
| RGMA | Recursive Gaussian MA | [rgma.pine](../lib/trends_IIR/rgma/rgma.pine) |
| RMA | wildeR MA | [rma.pine](../lib/trends_IIR/rma/rma.pine) |
| T3 | Tillson T3 MA | [t3.pine](../lib/trends_IIR/t3/t3.pine) |
| TEMA | Triple Exponential MA | [tema.pine](../lib/trends_IIR/tema/tema.pine) |
| TRAMA | Trend Regularity Adaptive MA | [trama.pine](../lib/trends_IIR/trama/trama.pine) |
| VAMA | Volatility Adjusted MA | [vama.pine](../lib/trends_IIR/vama/vama.pine) |
| VIDYA | Variable Index Dynamic Average | [vidya.pine](../lib/trends_IIR/vidya/vidya.pine) |
| YZVAMA | Yang-Zhang Volatility Adjusted MA | [yzvama.pine](../lib/trends_IIR/yzvama/yzvama.pine) |
| ZLDEMA | Zero-Lag Double Exponential MA | [zldema.pine](../lib/trends_IIR/zldema/zldema.pine) |
| ZLEMA | Zero-Lag Exponential MA | [zlema.pine](../lib/trends_IIR/zlema/zlema.pine) |
| ZLTEMA | Zero-Lag Triple Exponential MA | [zltema.pine](../lib/trends_IIR/zltema/zltema.pine) |

---

#### Filters (signal processing)

These are the heavy artillery. Kalman filters, Butterworth filters, wavelets. If you do not know what a transfer function is, start with the moving averages above and come back when you are ready. No judgment. (Some judgment.)

| Indicator | What It Does | Pine Script |
| :--- | :--- | :--- |
| AGC | Ehlers Automatic Gain Control | [agc.pine](../lib/filters/agc/agc.pine) |
| ALAGUERRE | Ehlers Adaptive Laguerre Filter | [alaguerre.pine](../lib/filters/alaguerre/alaguerre.pine) |
| BAXTERKING | Baxter-King Band-Pass Filter | [baxterking.pine](../lib/filters/baxterking/baxterking.pine) |
| BESSEL | Bessel Filter | [bessel.pine](../lib/filters/bessel/bessel.pine) |
| BILATERAL | Bilateral Filter | [Bilateral.pine](../lib/filters/bilateral/Bilateral.pine) |
| BPF | Bandpass Filter | [bpf.pine](../lib/filters/bpf/bpf.pine) |
| BUTTER2 | Ehlers 2-Pole Butterworth Filter | [butter2.pine](../lib/filters/butter2/butter2.pine) |
| BUTTER3 | Ehlers 3-Pole Butterworth Filter | [butter3.pine](../lib/filters/butter3/butter3.pine) |
| CFITZ | Christiano-Fitzgerald Filter | [cfitz.pine](../lib/filters/cfitz/cfitz.pine) |
| CHEBY1 | Chebyshev Type I | [cheby1.pine](../lib/filters/cheby1/cheby1.pine) |
| CHEBY2 | Chebyshev Type II | [cheby2.pine](../lib/filters/cheby2/cheby2.pine) |
| EDCF | Ehlers Distance Coefficient Filter | [edcf.pine](../lib/filters/edcf/edcf.pine) |
| ELLIPTIC | Elliptic Filter | [elliptic.pine](../lib/filters/elliptic/elliptic.pine) |
| GAUSS | Gaussian Filter | [gauss.pine](../lib/filters/gauss/gauss.pine) |
| HANN | Hann Filter | [hann.pine](../lib/filters/hann/hann.pine) |
| HP | Hodrick-Prescott Filter | [hp.pine](../lib/filters/hp/hp.pine) |
| HPF | Ehlers Highpass Filter | [hpf.pine](../lib/filters/hpf/hpf.pine) |
| KALMAN | Kalman Filter | [kalman.pine](../lib/filters/kalman/kalman.pine) |
| LAGUERRE | Ehlers Laguerre Filter | [laguerre.pine](../lib/filters/laguerre/laguerre.pine) |
| LMS | Least Mean Squares | [lms.pine](../lib/filters/lms/lms.pine) |
| LOESS | LOESS Smoothing | [loess.pine](../lib/filters/loess/loess.pine) |
| MODF | Modular Filter | [modf.pine](../lib/filters/modf/modf.pine) |
| NET | Ehlers Noise Elimination Technology | [net.pine](../lib/filters/net/net.pine) |
| NOTCH | Notch Filter | [notch.pine](../lib/filters/notch/notch.pine) |
| NW | Nadaraya-Watson Estimator | [nw.pine](../lib/filters/nw/nw.pine) |
| ONEEURO | One Euro Filter | [oneeuro.pine](../lib/filters/oneeuro/oneeuro.pine) |
| RLS | Recursive Least Squares | [rls.pine](../lib/filters/rls/rls.pine) |
| RMED | Ehlers Recursive Median Filter | [rmed.pine](../lib/filters/rmed/rmed.pine) |
| ROOFING | Ehlers Roofing Filter | [roofing.pine](../lib/filters/roofing/roofing.pine) |
| SAK | Ehlers Swiss Army Knife | [sak.pine](../lib/filters/sak/sak.pine) |
| SGF | Savitzky-Golay Filter | [sgf.pine](../lib/filters/sgf/sgf.pine) |
| SPBF | Ehlers Super Passband Filter | [spbf.pine](../lib/filters/spbf/spbf.pine) |
| TBF | Ehlers Truncated Bandpass Filter | [tbf.pine](../lib/filters/tbf/tbf.pine) |
| SSF2 | Ehlers 2-Pole Super Smoother Filter | [ssf2.pine](../lib/filters/ssf2/ssf2.pine) |
| SSF3 | Ehlers 3-Pole Super Smoother Filter | [ssf3.pine](../lib/filters/ssf3/ssf3.pine) |
| USF | Ehlers Ultimate Smoother Filter | [usf.pine](../lib/filters/usf/usf.pine) |
| VOSS | Ehlers Voss Predictive Filter | [voss.pine](../lib/filters/voss/voss.pine) |
| WAVELET | Wavelet Denoising Filter | [wavelet.pine](../lib/filters/wavelet/wavelet.pine) |
| WIENER | Wiener Filter | [wiener.pine](../lib/filters/wiener/wiener.pine) |

---

#### Oscillators

Numbers that bounce between limits. Overbought, oversold, divergence. You know the drill.

| Indicator | What It Does | Pine Script |
| :--- | :--- | :--- |
| AC | Acceleration Oscillator | [ac.pine](../lib/oscillators/ac/ac.pine) |
| AO | Awesome Oscillator | [ao.pine](../lib/oscillators/ao/ao.pine) |
| APO | Absolute Price Oscillator | [apo.pine](../lib/oscillators/apo/apo.pine) |
| BBB | Bollinger %B | [bbb.pine](../lib/oscillators/bbb/bbb.pine) |
| BBI | Bulls Bears Index | [bbi.pine](../lib/oscillators/bbi/bbi.pine) |
| BBS | Bollinger Band Squeeze | [bbs.pine](../lib/oscillators/bbs/bbs.pine) |
| BRAR | Bull-Bear Power Ratio | [brar.pine](../lib/oscillators/brar/brar.pine) |
| CFO | Chande Forecast Oscillator | [cfo.pine](../lib/oscillators/cfo/cfo.pine) |
| COPPOCK | Coppock Curve | [coppock.pine](../lib/oscillators/coppock/coppock.pine) |
| CRSI | Connors RSI | [crsi.pine](../lib/oscillators/crsi/crsi.pine) |
| CTI | Correlation Trend Indicator | [cti.pine](../lib/oscillators/cti/cti.pine) |
| DECO | Ehlers Decycler Oscillator | [deco.pine](../lib/oscillators/deco/deco.pine) |
| DEM | DeMarker Oscillator | [dem.pine](../lib/oscillators/dem/dem.pine) |
| DOSC | Derivative Oscillator | [dosc.pine](../lib/oscillators/dosc/dosc.pine) |
| DSO | Ehlers Deviation-Scaled Oscillator | [dso.pine](../lib/oscillators/dso/dso.pine) |
| DPO | Detrended Price Oscillator | [dpo.pine](../lib/oscillators/dpo/dpo.pine) |
| DYMI | Dynamic Momentum Index | [dymi.pine](../lib/oscillators/dymi/dymi.pine) |
| EEO | Ehlers Elegant Oscillator | [eeo.pine](../lib/oscillators/eeo/eeo.pine) |
| ER | Efficiency Ratio | [er.pine](../lib/oscillators/er/er.pine) |
| ERI | Elder Ray Index | [eri.pine](../lib/oscillators/eri/eri.pine) |
| FI | Force Index | [fi.pine](../lib/oscillators/fi/fi.pine) |
| FISHER | Ehlers Fisher Transform | [fisher.pine](../lib/oscillators/fisher/fisher.pine) |
| FISHER04 | Ehlers Fisher Transform (2004) | [fisher04.pine](../lib/oscillators/fisher04/fisher04.pine) |
| GATOR | Williams Gator Oscillator | [gator.pine](../lib/oscillators/gator/gator.pine) |
| IMI | Intraday Momentum Index | [imi.pine](../lib/oscillators/imi/imi.pine) |
| INERTIA | Inertia | [inertia.pine](../lib/oscillators/inertia/inertia.pine) |
| KDJ | KDJ Indicator | [kdj.pine](../lib/oscillators/kdj/kdj.pine) |
| KRI | Kairi Relative Index | [kri.pine](../lib/oscillators/kri/kri.pine) |
| KST | Know Sure Thing Oscillator | [kst.pine](../lib/oscillators/kst/kst.pine) |
| LRSI | Ehlers Laguerre RSI | [lrsi.pine](../lib/oscillators/lrsi/lrsi.pine) |
| MADH | Ehlers Moving Average Difference with Hann | [madh.pine](../lib/oscillators/madh/madh.pine) |
| MARKETFI | Market Facilitation Index | [marketfi.pine](../lib/oscillators/marketfi/marketfi.pine) |
| MSTOCH | Ehlers MESA Stochastic | [mstoch.pine](../lib/oscillators/mstoch/mstoch.pine) |
| PGO | Pretty Good Oscillator | [pgo.pine](../lib/oscillators/pgo/pgo.pine) |
| PSL | Psychological Line | [psl.pine](../lib/oscillators/psl/psl.pine) |
| QQE | Quantitative Qualitative Estimation | [qqe.pine](../lib/oscillators/qqe/qqe.pine) |
| REFLEX | Ehlers Reflex | [reflex.pine](../lib/oscillators/reflex/reflex.pine) |
| REVERSEEMA | Ehlers Reverse EMA | [reverseema.pine](../lib/oscillators/reverseema/reverseema.pine) |
| RVGI | Relative Vigor Index | [rvgi.pine](../lib/oscillators/rvgi/rvgi.pine) |
| RSIH | Ehlers Hann-Windowed RSI | [rsih.pine](../lib/oscillators/rsih/rsih.pine) |
| SMI | Stochastic Momentum Index | [smi.pine](../lib/oscillators/smi/smi.pine) |
| SQUEEZE | Squeeze Momentum | [squeeze.pine](../lib/oscillators/squeeze/squeeze.pine) |
| STC | Schaff Trend Cycle | [stc.pine](../lib/oscillators/stc/stc.pine) |
| STOCH | Stochastic Oscillator | [stoch.pine](../lib/oscillators/stoch/stoch.pine) |
| STOCHF | Stochastic Fast | [stochf.pine](../lib/oscillators/stochf/stochf.pine) |
| STOCHRSI | Stochastic RSI | [stochrsi.pine](../lib/oscillators/stochrsi/stochrsi.pine) |
| TD_SEQ | TD Sequential | [td_seq.pine](../lib/oscillators/td_seq/td_seq.pine) |
| TRENDFLEX | Ehlers Trendflex | [trendflex.pine](../lib/oscillators/trendflex/trendflex.pine) |
| TRIX | Triple Exponential Average | [trix.pine](../lib/oscillators/trix/trix.pine) |
| TTM_WAVE | TTM Wave | [ttm_wave.pine](../lib/oscillators/ttm_wave/ttm_wave.pine) |
| ULTOSC | Ultimate Oscillator | [ultosc.pine](../lib/oscillators/ultosc/ultosc.pine) |
| USI | Ehlers Ultimate Strength Index | [usi.pine](../lib/oscillators/usi/usi.pine) |
| WILLR | Williams %R | [willr.pine](../lib/oscillators/willr/willr.pine) |

---

#### Dynamics (trend strength)

Is there a trend? How strong? These indicators answer that.

| Indicator | What It Does | Pine Script |
| :--- | :--- | :--- |
| ADX | Average Directional Index | [adx.pine](../lib/dynamics/adx/adx.pine) |
| ADXR | Average Directional Movement Rating | [adxr.pine](../lib/dynamics/adxr/adxr.pine) |
| ALLIGATOR | Williams Alligator | [alligator.pine](../lib/dynamics/alligator/alligator.pine) |
| AMAT | Archer Moving Averages Trends | [amat.pine](../lib/dynamics/amat/amat.pine) |
| AROON | Aroon | [aroon.pine](../lib/dynamics/aroon/aroon.pine) |
| AROONOSC | Aroon Oscillator | [aroonosc.pine](../lib/dynamics/aroonosc/aroonosc.pine) |
| CHOP | Choppiness Index | [chop.pine](../lib/dynamics/chop/chop.pine) |
| DMH | Ehlers Directional Movement with Hann | [dmh.pine](../lib/dynamics/dmh/dmh.pine) |
| DMX | Jurik Directional Movement Index | [dmx.pine](../lib/dynamics/dmx/dmx.pine) |
| DX | Directional Movement Index | [dx.pine](../lib/dynamics/dx/dx.pine) |
| GHLA | Gann High-Low Activator | [ghla.pine](../lib/dynamics/ghla/ghla.pine) |
| HT_TRENDMODE | Ehlers Hilbert Transform Trend vs Cycle Mode | [ht_trendmode.pine](../lib/dynamics/ht_trendmode/ht_trendmode.pine) |
| ICHIMOKU | Ichimoku Cloud | [ichimoku.pine](../lib/dynamics/ichimoku/ichimoku.pine) |
| MINUS_DI | Minus Directional Indicator | [minusdi.pine](../lib/dynamics/minusdi/minusdi.pine) |
| MINUS_DM | Minus Directional Movement | [minusdm.pine](../lib/dynamics/minusdm/minusdm.pine) |
| PFE | Polarized Fractal Efficiency | [pfe.pine](../lib/dynamics/pfe/pfe.pine) |
| PLUS_DI | Plus Directional Indicator | [plusdi.pine](../lib/dynamics/plusdi/plusdi.pine) |
| PLUS_DM | Plus Directional Movement | [plusdm.pine](../lib/dynamics/plusdm/plusdm.pine) |
| PTA | Ehlers Precision Trend Analysis | [pta.pine](../lib/dynamics/pta/pta.pine) |
| QSTICK | Qstick Indicator | [qstick.pine](../lib/dynamics/qstick/qstick.pine) |
| RAVI | Chande Range Action Verification Index | [ravi.pine](../lib/dynamics/ravi/ravi.pine) |
| SUPER | SuperTrend | [super.pine](../lib/dynamics/super/super.pine) |
| TTM_TREND | TTM Trend | [TtmTrend.pine](../lib/dynamics/ttm_trend/TtmTrend.pine) |
| VHF | Vertical Horizontal Filter | [vhf.pine](../lib/dynamics/vhf/vhf.pine) |
| VORTEX | Vortex Indicator | [vortex.pine](../lib/dynamics/vortex/vortex.pine) |

---

#### Momentum

How fast price is moving. Direction matters here.

| Indicator | What It Does | Pine Script |
| :--- | :--- | :--- |
| ASI | Accumulation Swing Index | [asi.pine](../lib/momentum/asi/asi.pine) |
| BIAS | Bias / Disparity Index | [bias.pine](../lib/momentum/bias/bias.pine) |
| BOP | Balance of Power | [bop.pine](../lib/momentum/bop/bop.pine) |
| CCI | Commodity Channel Index | [cci.pine](../lib/momentum/cci/cci.pine) |
| CFB | Jurik Composite Fractal Behavior | [cfb.pine](../lib/momentum/cfb/cfb.pine) |
| CMO | Chande Momentum Oscillator | [cmo.pine](../lib/momentum/cmo/cmo.pine) |
| MACD | Moving Average Convergence Divergence | [macd.pine](../lib/momentum/macd/macd.pine) |
| MOM | Momentum | [mom.pine](../lib/momentum/mom/mom.pine) |
| PMO | Price Momentum Oscillator | [pmo.pine](../lib/momentum/pmo/pmo.pine) |
| PPO | Percentage Price Oscillator | [ppo.pine](../lib/momentum/ppo/ppo.pine) |
| RS | Price Relative Strength | [rs.pine](../lib/momentum/rs/rs.pine) |
| ROC | Rate of Change | [roc.pine](../lib/momentum/roc/roc.pine) |
| ROCP | Rate of Change Percentage | [rocp.pine](../lib/momentum/rocp/rocp.pine) |
| ROCR | Rate of Change Ratio | [rocr.pine](../lib/momentum/rocr/rocr.pine) |
| RSI | Relative Strength Index | [rsi.pine](../lib/momentum/rsi/rsi.pine) |
| RSX | Jurik Relative Strength X | [rsx.pine](../lib/momentum/rsx/rsx.pine) |
| SAM | Smoothed Adaptive Momentum | [sam.pine](../lib/momentum/sam/sam.pine) |
| TSI | True Strength Index | [tsi.pine](../lib/momentum/tsi/tsi.pine) |
| VEL | Jurik Velocity | [vel.pine](../lib/momentum/vel/vel.pine) |

---

#### Volatility

How much price moves. Not direction. Just magnitude.

| Indicator | What It Does | Pine Script |
| :--- | :--- | :--- |
| ADR | Average Daily Range | [adr.pine](../lib/volatility/adr/adr.pine) |
| ATR | Average True Range | [atr.pine](../lib/volatility/atr/atr.pine) |
| ATRN | ATR Normalized | [atrn.pine](../lib/volatility/atrn/atrn.pine) |
| BBW | Bollinger Band Width | [bbw.pine](../lib/volatility/bbw/bbw.pine) |
| BBWN | Bollinger Band Width Normalized | [bbwn.pine](../lib/volatility/bbwn/bbwn.pine) |
| BBWP | Bollinger Band Width Percentile | [bbwp.pine](../lib/volatility/bbwp/bbwp.pine) |
| CCV | Close-to-Close Volatility | [ccv.pine](../lib/volatility/ccv/ccv.pine) |
| CV | Coefficient of Variation | [cv.pine](../lib/volatility/cv/cv.pine) |
| CVI | Chaikin's Volatility | [cvi.pine](../lib/volatility/cvi/cvi.pine) |
| ETHERM | Elder's Thermometer | [etherm.pine](../lib/volatility/etherm/etherm.pine) |
| EWMA | Exponential Weighted MA Volatility | [ewma.pine](../lib/volatility/ewma/ewma.pine) |
| GKV | Garman-Klass Volatility | [gkv.pine](../lib/volatility/gkv/gkv.pine) |
| HLV | High-Low Volatility | [hlv.pine](../lib/volatility/hlv/hlv.pine) |
| HV | Historical Volatility | [hv.pine](../lib/volatility/hv/hv.pine) |
| JVOLTY | Jurik Volatility | [jvolty.pine](../lib/volatility/jvolty/jvolty.pine) |
| JVOLTYN | Jurik Volatility Normalized | [jvoltyn.pine](../lib/volatility/jvoltyn/jvoltyn.pine) |
| MASSI | Mass Index | [massi.pine](../lib/volatility/massi/massi.pine) |
| NATR | Normalized ATR | [natr.pine](../lib/volatility/natr/natr.pine) |
| RSV | Rogers-Satchell Volatility | [rsv.pine](../lib/volatility/rsv/rsv.pine) |
| RV | Realized Volatility | [rv.pine](../lib/volatility/rv/rv.pine) |
| RVI | Relative Volatility Index | [rvi.pine](../lib/volatility/rvi/rvi.pine) |
| TR | True Range | [tr.pine](../lib/volatility/tr/tr.pine) |
| UI | Ulcer Index | [ui.pine](../lib/volatility/ui/ui.pine) |
| VOV | Volatility of Volatility | [vov.pine](../lib/volatility/vov/vov.pine) |
| VR | Volatility Ratio | [vr.pine](../lib/volatility/vr/vr.pine) |
| YZV | Yang-Zhang Volatility | [yzv.pine](../lib/volatility/yzv/yzv.pine) |

---

#### Volume

What the crowd is doing with their money.

| Indicator | What It Does | Pine Script |
| :--- | :--- | :--- |
| AD | Accumulation/Distribution Line | [ad.pine](../lib/volume/ad/ad.pine) |
| ADOSC | Chaikin A/D Oscillator | [adosc.pine](../lib/volume/adosc/adosc.pine) |
| AOBV | Archer On-Balance Volume | [aobv.pine](../lib/volume/aobv/aobv.pine) |
| CMF | Chaikin Money Flow | [cmf.pine](../lib/volume/cmf/cmf.pine) |
| EFI | Elder's Force Index | [efi.pine](../lib/volume/efi/efi.pine) |
| EOM | Ease of Movement | [eom.pine](../lib/volume/eom/eom.pine) |
| EVWMA | Elastic Volume Weighted MA | [evwma.pine](../lib/volume/evwma/evwma.pine) |
| III | Intraday Intensity Index | [iii.pine](../lib/volume/iii/iii.pine) |
| KVO | Klinger Volume Oscillator | [kvo.pine](../lib/volume/kvo/kvo.pine) |
| MFI | Money Flow Index | [mfi.pine](../lib/volume/mfi/mfi.pine) |
| NVI | Negative Volume Index | [nvi.pine](../lib/volume/nvi/nvi.pine) |
| OBV | On Balance Volume | [obv.pine](../lib/volume/obv/obv.pine) |
| PVD | Price Volume Divergence | [pvd.pine](../lib/volume/pvd/pvd.pine) |
| PVI | Positive Volume Index | [pvi.pine](../lib/volume/pvi/pvi.pine) |
| PVO | Percentage Volume Oscillator | [pvo.pine](../lib/volume/pvo/pvo.pine) |
| PVR | Price Volume Rank | [pvr.pine](../lib/volume/pvr/pvr.pine) |
| PVT | Price Volume Trend | [pvt.pine](../lib/volume/pvt/pvt.pine) |
| TVI | Trade Volume Index | [tvi.pine](../lib/volume/tvi/tvi.pine) |
| TWAP | Time Weighted Average Price | [twap.pine](../lib/volume/twap/twap.pine) |
| VA | Volume Accumulation | [va.pine](../lib/volume/va/va.pine) |
| VF | Volume Force | [vf.pine](../lib/volume/vf/vf.pine) |
| VO | Volume Oscillator | [vo.pine](../lib/volume/vo/vo.pine) |
| VROC | Volume Rate of Change | [vroc.pine](../lib/volume/vroc/vroc.pine) |
| VWAD | Volume Weighted A/D | [vwad.pine](../lib/volume/vwad/vwad.pine) |
| VWAP | Volume Weighted Average Price | [vwap.pine](../lib/volume/vwap/vwap.pine) |
| VWMA | Volume Weighted MA | [vwma.pine](../lib/volume/vwma/vwma.pine) |
| WAD | Williams A/D | [wad.pine](../lib/volume/wad/wad.pine) |

---

#### Channels (bands around price)

Upper band, lower band, sometimes a middle. Price bounces between them. In theory.

| Indicator | What It Does | Pine Script |
| :--- | :--- | :--- |
| ABERR | Aberration Bands | [aberr.pine](../lib/channels/aberr/aberr.pine) |
| ACCBANDS | Acceleration Bands | [accbands.pine](../lib/channels/accbands/accbands.pine) |
| APCHANNEL | Andrews' Pitchfork | [apchannel.pine](../lib/channels/apchannel/apchannel.pine) |
| APZ | Adaptive Price Zone | [apz.pine](../lib/channels/apz/apz.pine) |
| ATRBANDS | ATR Bands | [atrbands.pine](../lib/channels/atrbands/atrbands.pine) |
| BBANDS | Bollinger Bands | [bbands.pine](../lib/channels/bbands/bbands.pine) |
| DC | Donchian Channels | [dc.pine](../lib/channels/dc/dc.pine) |
| DECAYCHANNEL | Decay Min-Max Channel | [decaychannel.pine](../lib/channels/decaychannel/decaychannel.pine) |
| FCB | Fractal Chaos Bands | [fcb.pine](../lib/channels/fcb/fcb.pine) |
| JBANDS | Jurik Volatility Bands | [Jbands.pine](../lib/channels/jbands/Jbands.pine) |
| KC | Keltner Channel | [kc.pine](../lib/channels/kc/kc.pine) |
| MAENV | Moving Average Envelope | [maenv.pine](../lib/channels/maenv/maenv.pine) |
| MMCHANNEL | Min-Max Channel | [mmchannel.pine](../lib/channels/mmchannel/mmchannel.pine) |
| PC | Price Channel | [pc.pine](../lib/channels/pc/pc.pine) |
| REGCHANNEL | Regression Channels | [regchannel.pine](../lib/channels/regchannel/regchannel.pine) |
| SDCHANNEL | Standard Deviation Channel | [sdchannel.pine](../lib/channels/sdchannel/sdchannel.pine) |
| STARCHANNEL | Stoller Average Range Channel | [starchannel.pine](../lib/channels/starchannel/starchannel.pine) |
| STBANDS | Super Trend Bands | [stbands.pine](../lib/channels/stbands/stbands.pine) |
| UBANDS | Ehlers Ultimate Bands | [ubands.pine](../lib/channels/ubands/ubands.pine) |
| UCHANNEL | Ehlers Ultimate Channel | [uchannel.pine](../lib/channels/uchannel/uchannel.pine) |
| VWAPBANDS | VWAP Bands | [vwapbands.pine](../lib/channels/vwapbands/vwapbands.pine) |
| VWAPSD | VWAP with Standard Deviation Bands | [vwapsd.pine](../lib/channels/vwapsd/vwapsd.pine) |

---

#### Cycles

Markets oscillate. These indicators try to measure the oscillation itself — the period, phase, and amplitude of dominant cycles.

| Indicator | What It Does | Pine Script |
| :--- | :--- | :--- |
| AMFM | Ehlers AM Detector / FM Demodulator | [amfm.pine](../lib/cycles/amfm/amfm.pine) |
| CCOR | Ehlers Correlation Cycle | [ccor.pine](../lib/cycles/ccor/ccor.pine) |
| CCYC | Ehlers Cyber Cycle | [ccyc.pine](../lib/cycles/ccyc/ccyc.pine) |
| CG | Ehlers Center of Gravity | [cg.pine](../lib/cycles/cg/cg.pine) |
| DSP | Ehlers Detrended Synthetic Price | [dsp.pine](../lib/cycles/dsp/dsp.pine) |
| EPA | Ehlers Phasor Analysis | [epa.pine](../lib/cycles/epa/epa.pine) |
| FSI | Ehlers Fourier Series Indicator | [fsi.pine](../lib/cycles/fsi/fsi.pine) |
| ACP | Ehlers Autocorrelation Periodogram | [acp.pine](../lib/cycles/acp/acp.pine) |
| EBSW | Ehlers Even Better Sinewave | [ebsw.pine](../lib/cycles/ebsw/ebsw.pine) |
| HOMOD | Ehlers Homodyne Discriminator | [homod.pine](../lib/cycles/homod/homod.pine) |
| HT_DCPERIOD | Ehlers Hilbert Transform Dominant Cycle Period | [ht_dcperiod.pine](../lib/cycles/ht_dcperiod/ht_dcperiod.pine) |
| HT_DCPHASE | Ehlers Hilbert Transform Dominant Cycle Phase | [ht_dcphase.pine](../lib/cycles/ht_dcphase/ht_dcphase.pine) |
| HT_PHASOR | Ehlers Hilbert Transform Phasor Components | [phasor.pine](../lib/cycles/ht_phasor/phasor.pine) |
| HT_SINE | Ehlers Hilbert Transform SineWave | [ht_sine.pine](../lib/cycles/ht_sine/ht_sine.pine) |
| LPF | Ehlers Linear Predictive Filter | [lpf.pine](../lib/cycles/lpf/lpf.pine) |
| LUNAR | Lunar Phase | [lunar.pine](../lib/cycles/lunar/lunar.pine) |
| SOLAR | Solar Activity Cycle | [solar.pine](../lib/cycles/solar/solar.pine) |
| SSFDSP | Ehlers SSF Detrended Synthetic Price | [ssfdsp.pine](../lib/cycles/ssfdsp/ssfdsp.pine) |

---

#### Reversals

Where price might turn around. Pivots, stops, and fractal patterns.

| Indicator | What It Does | Pine Script |
| :--- | :--- | :--- |
| CHANDELIER | Chandelier Exit | [chandelier.pine](../lib/reversals/chandelier/chandelier.pine) |
| CKSTOP | Chande Kroll Stop | [ckstop.pine](../lib/reversals/ckstop/ckstop.pine) |
| FRACTALS | Williams Fractals | [fractals.pine](../lib/reversals/fractals/fractals.pine) |
| PIVOT | Pivot Points | [pivot.pine](../lib/reversals/pivot/pivot.pine) |
| PIVOTCAM | Camarilla Pivot Points | [pivotcam.pine](../lib/reversals/pivotcam/pivotcam.pine) |
| PIVOTDEM | DeMark Pivot Points | [pivotdem.pine](../lib/reversals/pivotdem/pivotdem.pine) |
| PIVOTEXT | Extended Traditional Pivots | [pivotext.pine](../lib/reversals/pivotext/pivotext.pine) |
| PIVOTFIB | Fibonacci Pivot Points | [pivotfib.pine](../lib/reversals/pivotfib/pivotfib.pine) |
| PIVOTWOOD | Woodie's Pivot Points | [pivotwood.pine](../lib/reversals/pivotwood/pivotwood.pine) |
| SAR | Parabolic Stop And Reverse | [sar.pine](../lib/reversals/sar/sar.pine) |
| SAREXT | Parabolic SAR Extended | [sarext.pine](../lib/reversals/sarext/sarext.pine) |
| SWINGS | Swing High/Low Detection | [swings.pine](../lib/reversals/swings/swings.pine) |
| TTM_SCALPER | TTM Scalper Alert | [ttmscalper.pine](../lib/reversals/ttm_scalper/ttmscalper.pine) |

---

#### Forecasts

Predictions. Use with appropriate skepticism.

| Indicator | What It Does | Pine Script |
| :--- | :--- | :--- |
| AFIRMA | Adaptive FIR MA | [afirma.pine](../lib/forecasts/afirma/afirma.pine) |

---

#### Statistics

Quantitative measures of price behavior. Correlation, regression, distribution analysis.

| Indicator | What It Does | Pine Script |
| :--- | :--- | :--- |
| ACF | Autocorrelation Function | [acf.pine](../lib/statistics/acf/acf.pine) |
| BETA | Beta Coefficient | [beta.pine](../lib/statistics/beta/beta.pine) |
| CMA | Cumulative MA | [cma.pine](../lib/statistics/cma/cma.pine) |
| COINTEGRATION | Cointegration | [cointegration.pine](../lib/statistics/cointegration/cointegration.pine) |
| CORREL | Correlation | [correl.pine](../lib/statistics/correl/correl.pine) |
| COVARIANCE | Covariance | [covariance.pine](../lib/statistics/covariance/covariance.pine) |
| ENTROPY | Shannon Entropy | [entropy.pine](../lib/statistics/entropy/entropy.pine) |
| GEOMEAN | Geometric Mean | [geomean.pine](../lib/statistics/geomean/geomean.pine) |
| GRANGER | Granger Causality | [granger.pine](../lib/statistics/granger/granger.pine) |
| HARMEAN | Harmonic Mean | [harmean.pine](../lib/statistics/harmean/harmean.pine) |
| HURST | Hurst Exponent | [hurst.pine](../lib/statistics/hurst/hurst.pine) |
| IQR | Interquartile Range | [iqr.pine](../lib/statistics/iqr/iqr.pine) |
| JB | Jarque-Bera Test | [jb.pine](../lib/statistics/jb/jb.pine) |
| KENDALL | Kendall Rank Correlation | [kendall.pine](../lib/statistics/kendall/kendall.pine) |
| KURTOSIS | Kurtosis | [kurtosis.pine](../lib/statistics/kurtosis/kurtosis.pine) |
| LINREG | Linear Regression Curve | [linreg.pine](../lib/statistics/linreg/linreg.pine) |
| MEANDEV | Mean Absolute Deviation | [meandev.pine](../lib/statistics/meandev/meandev.pine) |
| MEDIAN | Rolling Median | [median.pine](../lib/statistics/median/median.pine) |
| MODE | Mode | [mode.pine](../lib/statistics/mode/mode.pine) |
| PACF | Partial Autocorrelation Function | [pacf.pine](../lib/statistics/pacf/pacf.pine) |
| PERCENTILE | Percentile | [percentile.pine](../lib/statistics/percentile/percentile.pine) |
| POLYFIT | Polynomial Fitting | [polyfit.pine](../lib/statistics/polyfit/polyfit.pine) |
| QUANTILE | Quantile | [quantile.pine](../lib/statistics/quantile/quantile.pine) |
| SKEW | Skewness | [skew.pine](../lib/statistics/skew/skew.pine) |
| SPEARMAN | Spearman Rank Correlation | [spearman.pine](../lib/statistics/spearman/spearman.pine) |
| STDDEV | Standard Deviation | [stddev.pine](../lib/statistics/stddev/stddev.pine) |
| STDERR | Standard Error of Regression | [stderr.pine](../lib/statistics/stderr/stderr.pine) |
| SUM | Rolling Sum | [sum.pine](../lib/statistics/sum/sum.pine) |
| THEIL | Theil Index | [theil.pine](../lib/statistics/theil/theil.pine) |
| TRIM | Trimmed Mean MA | [trim.pine](../lib/statistics/trim/trim.pine) |
| VARIANCE | Population and Sample Variance | [variance.pine](../lib/statistics/variance/variance.pine) |
| WAVG | Weighted Average | [wavg.pine](../lib/statistics/wavg/wavg.pine) |
| WINS | Winsorized Mean MA | [wins.pine](../lib/statistics/wins/wins.pine) |
| ZSCORE | Z-score | [zscore.pine](../lib/statistics/zscore/zscore.pine) |
| ZTEST | Z-Test | [ztest.pine](../lib/statistics/ztest/ztest.pine) |

---

#### Numerics (transforms and distributions)

Mathematical transforms, derivatives, and probability distributions. The kind of tools a quant reaches for at 3 AM.

| Indicator | What It Does | Pine Script |
| :--- | :--- | :--- |
| ACCEL | Acceleration | [accel.pine](../lib/numerics/accel/accel.pine) |
| BETADIST | Beta Distribution | [betadist.pine](../lib/numerics/betadist/betadist.pine) |
| BINOMDIST | Binomial Distribution | [binomdist.pine](../lib/numerics/binomdist/binomdist.pine) |
| CHANGE | Percentage Change | [change.pine](../lib/numerics/change/change.pine) |
| CWT | Continuous Wavelet Transform | [cwt.pine](../lib/numerics/cwt/cwt.pine) |
| DECAY | Linear Decay | [decay.pine](../lib/numerics/decay/decay.pine) |
| DWT | Discrete Wavelet Transform | [dwt.pine](../lib/numerics/dwt/dwt.pine) |
| EDECAY | Exponential Decay | [edecay.pine](../lib/numerics/edecay/edecay.pine) |
| EXPDIST | Exponential Distribution | [expdist.pine](../lib/numerics/expdist/expdist.pine) |
| EXPTRANS | Exponential Transform | [exptrans.pine](../lib/numerics/exptrans/exptrans.pine) |
| FDIST | F-Distribution | [fdist.pine](../lib/numerics/fdist/fdist.pine) |
| FFT | Fast Fourier Transform | [fft.pine](../lib/numerics/fft/fft.pine) |
| GAMMADIST | Gamma Distribution | [gammadist.pine](../lib/numerics/gammadist/gammadist.pine) |
| HIGHEST | Rolling Maximum | [highest.pine](../lib/numerics/highest/highest.pine) |
| IFFT | Inverse Fast Fourier Transform | [ifft.pine](../lib/numerics/ifft/ifft.pine) |
| JERK | Jerk | [jerk.pine](../lib/numerics/jerk/jerk.pine) |
| LINEARTRANS | Linear Transform | [lineartrans.pine](../lib/numerics/lineartrans/lineartrans.pine) |
| LOGNORMDIST | Log-Normal Distribution | [lognormdist.pine](../lib/numerics/lognormdist/lognormdist.pine) |
| LOGTRANS | Logarithmic Transform | [logtrans.pine](../lib/numerics/logtrans/logtrans.pine) |
| LOWEST | Rolling Minimum | [lowest.pine](../lib/numerics/lowest/lowest.pine) |
| NORMALIZE | Min-Max Normalization | [normalize.pine](../lib/numerics/normalize/normalize.pine) |
| NORMDIST | Normal Distribution | [normdist.pine](../lib/numerics/normdist/normdist.pine) |
| POISSONDIST | Poisson Distribution | [poissondist.pine](../lib/numerics/poissondist/poissondist.pine) |
| RELU | Rectified Linear Unit | [relu.pine](../lib/numerics/relu/relu.pine) |
| SIGMOID | Logistic Function | [sigmoid.pine](../lib/numerics/sigmoid/sigmoid.pine) |
| SLOPE | First Derivative | [slope.pine](../lib/numerics/slope/slope.pine) |
| SQRTTRANS | Square Root Transform | [sqrttrans.pine](../lib/numerics/sqrttrans/sqrttrans.pine) |
| TDIST | Student's t-Distribution | [tdist.pine](../lib/numerics/tdist/tdist.pine) |
| WEIBULLDIST | Weibull Distribution | [weibulldist.pine](../lib/numerics/weibulldist/weibulldist.pine) |

---

#### Errors (forecast accuracy metrics)

How wrong was your model? These metrics quantify the answer. Useful for comparing indicator accuracy or building adaptive systems.

| Indicator | What It Does | Pine Script |
| :--- | :--- | :--- |
| HUBER | Huber Loss | [huber.pine](../lib/errors/huber/huber.pine) |
| LOGCOSH | Log-Cosh Loss | [logcosh.pine](../lib/errors/logcosh/logcosh.pine) |
| MAAPE | Mean Arctangent Absolute Percentage Error | [maape.pine](../lib/errors/maape/maape.pine) |
| MAE | Mean Absolute Error | [mae.pine](../lib/errors/mae/mae.pine) |
| MAPD | Mean Absolute Percentage Deviation | [mapd.pine](../lib/errors/mapd/mapd.pine) |
| MAPE | Mean Absolute Percentage Error | [mape.pine](../lib/errors/mape/mape.pine) |
| MASE | Mean Absolute Scaled Error | [mase.pine](../lib/errors/mase/mase.pine) |
| MDAE | Median Absolute Error | [mdae.pine](../lib/errors/mdae/mdae.pine) |
| MDAPE | Median Absolute Percentage Error | [mdape.pine](../lib/errors/mdape/mdape.pine) |
| ME | Mean Error | [me.pine](../lib/errors/me/me.pine) |
| MPE | Mean Percentage Error | [mpe.pine](../lib/errors/mpe/mpe.pine) |
| MRAE | Mean Relative Absolute Error | [mrae.pine](../lib/errors/mrae/mrae.pine) |
| MSE | Mean Squared Error | [mse.pine](../lib/errors/mse/mse.pine) |
| MSLE | Mean Squared Logarithmic Error | [msle.pine](../lib/errors/msle/msle.pine) |
| PSEUDOHUBER | Pseudo-Huber Loss | [pseudohuber.pine](../lib/errors/pseudohuber/pseudohuber.pine) |
| QUANTILELOSS | Quantile Loss | [quantileloss.pine](../lib/errors/quantileloss/quantileloss.pine) |
| RAE | Relative Absolute Error | [rae.pine](../lib/errors/rae/rae.pine) |
| RMSE | Root Mean Squared Error | [rmse.pine](../lib/errors/rmse/rmse.pine) |
| RMSLE | Root Mean Squared Logarithmic Error | [rmsle.pine](../lib/errors/rmsle/rmsle.pine) |
| RSE | Relative Squared Error | [rse.pine](../lib/errors/rse/rse.pine) |
| RSQUARED | Coefficient of Determination | [rsquared.pine](../lib/errors/rsquared/rsquared.pine) |
| SMAPE | Symmetric Mean Absolute Percentage Error | [smape.pine](../lib/errors/smape/smape.pine) |
| THEILU | Theil's U Statistic | [theilu.pine](../lib/errors/theilu/theilu.pine) |
| TUKEY | Tukey Biweight Loss | [tukeybiweight.pine](../lib/errors/tukeybiweight/tukeybiweight.pine) |
| WMAPE | Weighted Mean Absolute Percentage Error | [wmape.pine](../lib/errors/wmape/wmape.pine) |
| WRMSE | Weighted RMSE | [wrmse.pine](../lib/errors/wrmse/wrmse.pine) |

---

## Anatomy of a QuanTAlib Pine Script

Every script follows the same structure. Understanding this structure is optional but occasionally useful when things do not look right on your chart.

```pine
// Licensed under the Apache License, Version 2.0
// © mihakralj
//@version=6
indicator("Exponential Moving Average (EMA)", "EMA", overlay=true)

// ---- The function definition ----
// This is where the math lives. You do not need to touch this.
ema(series float source, simple int period=0, simple float alpha=0) =>
    // ... math happens here ...
    result

// ---- Inputs ----
// These create the settings panel on TradingView
i_period = input.int(10, "Period", minval=1)
i_source = input.source(close, "Source")

// ---- Calculation ----
ema_value = ema(i_source, period=i_period)

// ---- Plot ----
plot(ema_value, "EMA", color=color.yellow, linewidth=2)
```

### The parts that matter to you

- **Inputs section**: Change `10` to whatever period you want as the default. Or just use the TradingView settings panel after adding the indicator.
- **Plot section**: Change `color.yellow` to whatever color you prefer. Options include `color.red`, `color.green`, `color.blue`, `color.white`, `color.orange`, `color.purple`.
- **The function**: Do not touch this unless you know what you are doing. It was validated against the C# reference implementation. Your modifications will not be validated against anything.

## Common Questions

### "The indicator shows values from bar 1 — shouldn't there be a warmup gap?"

That is correct behavior. QuanTAlib generates output from the very first bar. A 14-period RSI with only 5 bars uses the best estimate possible given available data. The values become fully converged once enough bars have accumulated, but you never see NaN or blank bars. Other libraries leave gaps. QuanTAlib fills them with mathematically defensible approximations that improve as data accumulates.

### "Can I use this in a strategy?"

Yes. Replace `indicator(...)` with `strategy(...)` and add your entry/exit logic. The indicator function itself does not change.

### "The values differ from TradingView's built-in version"

Possible reasons, in order of likelihood:

1. **Different default parameters.** Check the period, source, and any multipliers.
2. **Different warmup handling.** QuanTAlib uses exponential compensation from bar 1. TradingView's built-in indicators sometimes use different warmup methods.
3. **You are looking at the wrong indicator.** DEMA is not "double EMA period." It is a specific algorithm by Patrick Mulloy.

### "I modified the math and now it is broken"

Put the original code back. The math was correct before you edited it.

### "Which moving average should I use?"

If you are asking this question, use EMA. It is the Honda Civic of moving averages: reliable, understood by everyone, and good enough for most situations. Come back and explore JMA, KAMA, or T3 when you have a specific problem that EMA does not solve.

### "Can I combine multiple indicators?"

Yes. Add multiple scripts to your chart or combine functions within a single script:

```pine
//@version=6
indicator("EMA + RSI Combo", overlay=false)

// Calculate both
ema_val = ta.ema(close, 20)     // TradingView built-in
rsi_val = ta.rsi(close, 14)     // TradingView built-in

// Or use QuanTAlib versions by pasting the function definitions
// from their respective .pine files and calling them

plot(rsi_val, "RSI", color=color.purple)
hline(70, "Overbought", color=color.red)
hline(30, "Oversold", color=color.green)
```

## Going Deeper (If You Dare)

Each indicator has a `.md` documentation file next to its `.pine` file. These contain:

- Mathematical formulas (yes, with actual math notation)
- Historical context (who invented it and why)
- Performance characteristics
- Validation against other libraries
- Common pitfalls

For example: [EMA documentation](../lib/trends_IIR/ema/Ema.md) explains the exponential warmup compensator, why it matters, and why most other implementations get the first few values wrong.

You do not have to read any of this. But if you ever wonder why your backtest results differ from someone else's, the answer is probably in there.

## The Full Catalog

**[All 393 indicators with descriptions →](../lib/_index.md)**

Every indicator in that list has a `.pine` file. Every `.pine` file works on TradingView. Every implementation matches the C# reference engine.

Copy responsibly.
