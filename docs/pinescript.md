# PineScript Guide: QuanTAlib Indicators for TradingView

> "You do not need to understand the math. But the math does not care whether you understand it."

## Welcome, Brave Copy-Paster

You are here because you want an indicator on your TradingView chart. Maybe someone on Crypto Twitter posted a screenshot with colored lines and you thought "I need that." Maybe you googled "best RSI Pine Script" at 2 AM. Maybe you clicked a link by accident. All valid paths to enlightenment.

Here is the good news: QuanTAlib provides **Pine Script v6 source code** for every one of its 393 indicators. Each script is self-contained, tested against the C# reference implementation, and ready to paste into TradingView's Pine Editor. No dependencies. No imports. No subscription to someone's Discord.

Here is the less-good news: the scripts contain actual mathematics. You do not have to read it. But it is there, silently judging.

## How to Use a Pine Script (The Short Version)

1. Find the indicator you want (see table below)
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

#### Core (price transforms)

These turn OHLC data into derived price series. If you do not know what "typical price" means, you probably want `close` instead.

| Indicator | What It Does | Pine Script |
| :--- | :--- | :--- |
| AVGPRICE | Average of OHLC | [avgprice.pine](../lib/core/avgprice/avgprice.pine) |
| HA | Heikin-Ashi candles | [ha.pine](../lib/core/ha/ha.pine) |
| MEDPRICE | Median of high and low | [medprice.pine](../lib/core/medprice/medprice.pine) |
| MIDPOINT | Midpoint of highest and lowest | [midpoint.pine](../lib/core/midpoint/midpoint.pine) |
| MIDPRICE | Midpoint of high and low | [midprice.pine](../lib/core/midprice/midprice.pine) |
| TYPPRICE | Typical price: (H+L+C)/3 | [typprice.pine](../lib/core/typprice/typprice.pine) |
| WCLPRICE | Weighted close: (H+L+2C)/4 | [wclprice.pine](../lib/core/wclprice/wclprice.pine) |

#### Moving Averages: FIR (the "simple" ones)

FIR stands for Finite Impulse Response. It means the average uses a fixed window of bars. SMA is an FIR filter. You have been using FIR filters this whole time.

| Indicator | What It Does | Pine Script |
| :--- | :--- | :--- |
| SMA | Simple Moving Average (the one everyone knows) | [sma.pine](../lib/trends_FIR/sma/sma.pine) |
| WMA | Weighted Moving Average | [wma.pine](../lib/trends_FIR/wma/wma.pine) |
| HMA | Hull Moving Average (fast, smooth) | [hma.pine](../lib/trends_FIR/hma/hma.pine) |
| ALMA | Arnaud Legoux MA (offset + sigma tuning) | [alma.pine](../lib/trends_FIR/alma/alma.pine) |
| TRIMA | Triangular Moving Average | [trima.pine](../lib/trends_FIR/trima/trima.pine) |
| LSMA | Least Squares (linear regression line) | [lsma.pine](../lib/trends_FIR/lsma/lsma.pine) |
| FWMA | Fibonacci Weighted MA | [fwma.pine](../lib/trends_FIR/fwma/fwma.pine) |
| GWMA | Gaussian Weighted MA | [gwma.pine](../lib/trends_FIR/gwma/gwma.pine) |
| SWMA | Symmetric Weighted MA | [swma.pine](../lib/trends_FIR/swma/swma.pine) |
| DWMA | Double Weighted MA | [dwma.pine](../lib/trends_FIR/dwma/dwma.pine) |
| SINEMA | Sine-Weighted MA | [sinema.pine](../lib/trends_FIR/sinema/sinema.pine) |
| HANMA | Hann-Weighted MA | [hanma.pine](../lib/trends_FIR/hanma/hanma.pine) |
| PARZEN | Parzen-Weighted MA | [parzen.pine](../lib/trends_FIR/parzen/parzen.pine) |
| SGMA | Savitzky-Golay MA | [sgma.pine](../lib/trends_FIR/sgma/sgma.pine) |
| TSF | Time Series Forecast | [tsf.pine](../lib/trends_FIR/tsf/tsf.pine) |
| BLMA | Blackman MA | [blma.pine](../lib/trends_FIR/blma/blma.pine) |
| PWMA | Pascal Weighted MA | [pwma.pine](../lib/trends_FIR/pwma/pwma.pine) |
| NLMA | Non-Linear MA | [nlma.pine](../lib/trends_FIR/nlma/nlma.pine) |
| ILRS | Integral of Linear Regression Slope | [ilrs.pine](../lib/trends_FIR/ilrs/ilrs.pine) |
| HAMMA | Hamming MA | [hamma.pine](../lib/trends_FIR/hamma/hamma.pine) |
| KAISER | Kaiser-Windowed MA | [kaiser.pine](../lib/trends_FIR/kaiser/kaiser.pine) |
| LANCZOS | Lanczos MA | [lanczos.pine](../lib/trends_FIR/lanczos/lanczos.pine) |
| PMA | Polynomial MA | [pma.pine](../lib/trends_FIR/pma/pma.pine) |
| NYQMA | Nyquist MA | [nyqma.pine](../lib/trends_FIR/nyqma/nyqma.pine) |
| QRMA | QR Decomposition MA | [qrma.pine](../lib/trends_FIR/qrma/qrma.pine) |
| RWMA | Range Weighted MA | [rwma.pine](../lib/trends_FIR/rwma/rwma.pine) |
| HEND | Henderson MA | [hend.pine](../lib/trends_FIR/hend/hend.pine) |
| CONV | Convolution (custom kernel) | [conv.pine](../lib/trends_FIR/conv/conv.pine) |
| BWMA | Butterworth-Weighted MA | [bwma.pine](../lib/trends_FIR/bwma/bwma.pine) |
| CRMA | Crowley MA | [crma.pine](../lib/trends_FIR/crma/crma.pine) |
| SP15 | SP-15 MA | [sp15.pine](../lib/trends_FIR/sp15/sp15.pine) |
| TUKEY_W | Tukey-Windowed MA | [tukey_w.pine](../lib/trends_FIR/tukey_w/tukey_w.pine) |
| RAIN | RAIN MA | [rain.pine](../lib/trends_FIR/rain/rain.pine) |

#### Moving Averages: IIR (the "smart" ones)

IIR stands for Infinite Impulse Response. These use recursive feedback: the previous output affects the current output. Generally smoother, lower lag, and harder to understand. The indicator descriptions link to documentation if curiosity ever strikes.

| Indicator | What It Does | Pine Script |
| :--- | :--- | :--- |
| EMA | Exponential MA (the popular one) | [ema.pine](../lib/trends_IIR/ema/ema.pine) |
| DEMA | Double EMA (less lag) | [dema.pine](../lib/trends_IIR/dema/dema.pine) |
| TEMA | Triple EMA (even less lag) | [tema.pine](../lib/trends_IIR/tema/tema.pine) |
| T3 | Tillson T3 (six-stage cascade) | [t3.pine](../lib/trends_IIR/t3/t3.pine) |
| JMA | Jurik MA (adaptive, low noise) | [jma.pine](../lib/trends_IIR/jma/jma.pine) |
| KAMA | Kaufman Adaptive MA | [kama.pine](../lib/trends_IIR/kama/kama.pine) |
| VIDYA | Variable Index Dynamic Average | [vidya.pine](../lib/trends_IIR/vidya/vidya.pine) |
| FRAMA | Fractal Adaptive MA | [frama.pine](../lib/trends_IIR/frama/frama.pine) |
| MAMA | MESA Adaptive MA (Ehlers) | [mama.pine](../lib/trends_IIR/mama/mama.pine) |
| HOLT | Holt Exponential Smoothing | [holt.pine](../lib/trends_IIR/holt/holt.pine) |
| HWMA | Holt-Winters MA | [hwma.pine](../lib/trends_IIR/hwma/hwma.pine) |
| RMA | Wilder's MA (used inside RSI) | [rma.pine](../lib/trends_IIR/rma/rma.pine) |
| ZLEMA | Zero-Lag EMA | [zlema.pine](../lib/trends_IIR/zlema/zlema.pine) |
| ZLDEMA | Zero-Lag Double EMA | [zldema.pine](../lib/trends_IIR/zldema/zldema.pine) |
| ZLTEMA | Zero-Lag Triple EMA | [zltema.pine](../lib/trends_IIR/zltema/zltema.pine) |
| MGDI | McGinley Dynamic | [mgdi.pine](../lib/trends_IIR/mgdi/mgdi.pine) |
| LEMA | Leader EMA | [lema.pine](../lib/trends_IIR/lema/lema.pine) |
| HEMA | Hull EMA | [hema.pine](../lib/trends_IIR/hema/hema.pine) |
| GDEMA | Generalized Double EMA | [gdema.pine](../lib/trends_IIR/gdema/gdema.pine) |
| DSMA | Deviation-Scaled MA | [dsma.pine](../lib/trends_IIR/dsma/dsma.pine) |
| CORAL | Coral Trend Filter | [coral.pine](../lib/trends_IIR/coral/coral.pine) |
| AHRENS | Ahrens MA | [ahrens.pine](../lib/trends_IIR/ahrens/ahrens.pine) |
| DECYCLER | Ehlers Decycler | [decycler.pine](../lib/trends_IIR/decycler/decycler.pine) |
| MCNMA | McNicholl EMA | [mcnma.pine](../lib/trends_IIR/mcnma/mcnma.pine) |
| MMA | Modified MA | [mma.pine](../lib/trends_IIR/mma/mma.pine) |
| NMA | Natural MA | [nma.pine](../lib/trends_IIR/nma/nma.pine) |
| QEMA | Quad EMA | [qema.pine](../lib/trends_IIR/qema/qema.pine) |
| REMA | Regularized EMA | [rema.pine](../lib/trends_IIR/rema/rema.pine) |
| RGMA | Recursive Gaussian MA | [rgma.pine](../lib/trends_IIR/rgma/rgma.pine) |
| TRAMA | Trend Regularity Adaptive MA | [trama.pine](../lib/trends_IIR/trama/trama.pine) |
| LTMA | Linear Trend MA | [ltma.pine](../lib/trends_IIR/ltma/ltma.pine) |
| HTIT | Hilbert Transform Instantaneous Trend | [htit.pine](../lib/trends_IIR/htit/htit.pine) |
| ADXVMA | ADX Variable MA | [adxvma.pine](../lib/trends_IIR/adxvma/adxvma.pine) |
| VAMA | Volatility Adjusted MA | [vama.pine](../lib/trends_IIR/vama/vama.pine) |
| YZVAMA | Yang-Zhang Volatility Adjusted MA | [yzvama.pine](../lib/trends_IIR/yzvama/yzvama.pine) |
| MAVP | Moving Average Variable Period | [mavp.pine](../lib/trends_IIR/mavp/mavp.pine) |

#### Oscillators

Numbers that bounce between limits. Overbought, oversold, divergence. You know the drill.

| Indicator | What It Does | Pine Script |
| :--- | :--- | :--- |
| RSI | Relative Strength Index (the king) | [rsi.pine](../lib/momentum/rsi/rsi.pine) |
| STOCH | Stochastic Oscillator | [stoch.pine](../lib/oscillators/stoch/stoch.pine) |
| STOCHF | Fast Stochastic | [stochf.pine](../lib/oscillators/stochf/stochf.pine) |
| STOCHRSI | Stochastic RSI | [stochrsi.pine](../lib/oscillators/stochrsi/stochrsi.pine) |
| CCI | Commodity Channel Index | [cci.pine](../lib/momentum/cci/cci.pine) |
| WILLR | Williams %R | [willr.pine](../lib/oscillators/willr/willr.pine) |
| FISHER | Fisher Transform (Ehlers) | [fisher.pine](../lib/oscillators/fisher/Fisher.pine) |
| QQE | Qualitative Quantitative Estimation | [qqe.pine](../lib/oscillators/qqe/qqe.pine) |
| TRIX | Triple EMA Rate of Change | [trix.pine](../lib/oscillators/trix/trix.pine) |
| KDJ | KDJ Indicator | [See oscillators](../lib/oscillators/) |
| CTI | Correlation Trend Indicator | [cti.pine](../lib/oscillators/cti/cti.pine) |
| LRSI | Laguerre RSI | [lrsi.pine](../lib/oscillators/lrsi/lrsi.pine) |
| REFLEX | Ehlers Reflex | [reflex.pine](../lib/oscillators/reflex/reflex.pine) |
| SMI | Stochastic Momentum Index | [smi.pine](../lib/oscillators/smi/smi.pine) |
| STC | Schaff Trend Cycle | [stc.pine](../lib/oscillators/stc/stc.pine) |

#### Momentum

How fast price is moving. Direction matters here.

| Indicator | What It Does | Pine Script |
| :--- | :--- | :--- |
| MACD | Moving Average Convergence Divergence | [macd.pine](../lib/momentum/macd/macd.pine) |
| ROC | Rate of Change | [roc.pine](../lib/momentum/roc/roc.pine) |
| MOM | Momentum (price change over N bars) | [mom.pine](../lib/momentum/mom/mom.pine) |
| TSI | True Strength Index | [tsi.pine](../lib/momentum/tsi/tsi.pine) |
| CMO | Chande Momentum Oscillator | [cmo.pine](../lib/momentum/cmo/cmo.pine) |
| PPO | Percentage Price Oscillator | [ppo.pine](../lib/momentum/ppo/ppo.pine) |
| VEL | Velocity | [vel.pine](../lib/momentum/vel/vel.pine) |
| BOP | Balance of Power | [bop.pine](../lib/momentum/bop/bop.pine) |
| CFB | Composite Force Balance | [cfb.pine](../lib/momentum/cfb/cfb.pine) |

#### Dynamics (trend strength)

Is there a trend? How strong? These indicators answer that.

| Indicator | What It Does | Pine Script |
| :--- | :--- | :--- |
| ADX | Average Directional Index | [adx.pine](../lib/dynamics/adx/adx.pine) |
| AROON | Aroon Up/Down | [aroon.pine](../lib/dynamics/aroon/aroon.pine) |
| SUPERTREND | SuperTrend (ATR-based stops) | [super.pine](../lib/dynamics/super/super.pine) |
| ICHIMOKU | Ichimoku Cloud | [ichimoku.pine](../lib/dynamics/ichimoku/ichimoku.pine) |
| VORTEX | Vortex Indicator | [vortex.pine](../lib/dynamics/vortex/vortex.pine) |
| CHOP | Choppiness Index | [chop.pine](../lib/dynamics/chop/chop.pine) |
| ALLIGATOR | Williams Alligator | [alligator.pine](../lib/dynamics/alligator/alligator.pine) |
| PSAR | Parabolic SAR | [psar.pine](../lib/reversals/psar/psar.pine) |

#### Volatility

How much price moves. Not direction. Just magnitude.

| Indicator | What It Does | Pine Script |
| :--- | :--- | :--- |
| ATR | Average True Range | [atr.pine](../lib/volatility/atr/atr.pine) |
| TR | True Range | [tr.pine](../lib/volatility/tr/tr.pine) |
| BBW | Bollinger Band Width | [bbw.pine](../lib/volatility/bbw/bbw.pine) |
| HV | Historical Volatility | [hv.pine](../lib/volatility/hv/hv.pine) |
| NATR | Normalized ATR | [natr.pine](../lib/volatility/natr/natr.pine) |

#### Channels (bands around price)

Upper band, lower band, sometimes a middle. Price bounces between them. In theory.

| Indicator | What It Does | Pine Script |
| :--- | :--- | :--- |
| BBANDS | Bollinger Bands | [bbands.pine](../lib/channels/bbands/bbands.pine) |
| KCHANNEL | Keltner Channels | [kchannel.pine](../lib/channels/kchannel/kchannel.pine) |
| DCHANNEL | Donchian Channels | [dchannel.pine](../lib/channels/dchannel/dchannel.pine) |
| PCHANNEL | Price Channels | [pchannel.pine](../lib/channels/pchannel/pchannel.pine) |
| ACCBANDS | Acceleration Bands | [accbands.pine](../lib/channels/accbands/accbands.pine) |

#### Volume

What the crowd is doing with their money.

| Indicator | What It Does | Pine Script |
| :--- | :--- | :--- |
| OBV | On-Balance Volume | [obv.pine](../lib/volume/obv/obv.pine) |
| VWAP | Volume Weighted Average Price | [vwap.pine](../lib/volume/vwap/vwap.pine) |
| MFI | Money Flow Index (volume RSI) | [mfi.pine](../lib/volume/mfi/mfi.pine) |
| CMF | Chaikin Money Flow | [cmf.pine](../lib/volume/cmf/cmf.pine) |
| ADL | Accumulation/Distribution Line | [adl.pine](../lib/volume/adl/adl.pine) |
| VWMA | Volume Weighted MA | [vwma.pine](../lib/volume/vwma/vwma.pine) |
| KVO | Klinger Volume Oscillator | [kvo.pine](../lib/volume/kvo/kvo.pine) |

#### Filters (signal processing)

These are the heavy artillery. Kalman filters, Butterworth filters, wavelets. If you do not know what a transfer function is, start with the moving averages above and come back when you are ready. No judgment. (Some judgment.)

| Indicator | What It Does | Pine Script |
| :--- | :--- | :--- |
| KALMAN | Kalman Filter | [kalman.pine](../lib/filters/kalman/kalman.pine) |
| SGF | Savitzky-Golay Filter | [sgf.pine](../lib/filters/sgf/sgf.pine) |
| SSF2 | Ehlers Super Smoother (2-pole) | [ssf2.pine](../lib/filters/ssf2/ssf2.pine) |
| SSF3 | Ehlers Super Smoother (3-pole) | [ssf3.pine](../lib/filters/ssf3/ssf3.pine) |
| GAUSS | Gaussian Filter | [gauss.pine](../lib/filters/gauss/gauss.pine) |
| BUTTER2 | Butterworth (2nd order) | [butter2.pine](../lib/filters/butter2/butter2.pine) |
| BUTTER3 | Butterworth (3rd order) | [butter3.pine](../lib/filters/butter3/butter3.pine) |
| HPF | High-Pass Filter | [hpf.pine](../lib/filters/hpf/hpf.pine) |
| LAGUERRE | Laguerre Filter | [laguerre.pine](../lib/filters/laguerre/laguerre.pine) |
| ROOFING | Ehlers Roofing Filter | [roofing.pine](../lib/filters/roofing/roofing.pine) |
| VOSS | Voss Predictor | [voss.pine](../lib/filters/voss/voss.pine) |

**Not every indicator is listed here.** Browse the [full catalog of 393 indicators](../lib/_index.md) for the complete collection, including cycles, statistics, error metrics, reversals, numerics, and forecasts.

## Anatomy of a QuanTAlib Pine Script

Every script follows the same structure. Understanding this structure is optional but occasionally useful when things do not look right on your chart.

```pine
// The MIT License (MIT)
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
