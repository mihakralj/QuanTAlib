# QuanTAlib - quantitative technical indicators for Quantower and other C#-based trading platorms

[![Lines of Code](https://sonarcloud.io/api/project_badges/measure?project=mihakralj_QuanTAlib&metric=ncloc)](https://sonarcloud.io/summary/overall?id=mihakralj_QuanTAlib)
[![Codacy grade](https://img.shields.io/codacy/grade/b1f9109222234c87bce45f1fd4c63aee?style=flat-square)](https://app.codacy.com/gh/mihakralj/QuanTAlib/dashboard)
[![codecov](https://codecov.io/gh/mihakralj/QuanTAlib/branch/main/graph/badge.svg?style=flat-square&token=YNMJRGKMTJ?style=flat-square)](https://codecov.io/gh/mihakralj/QuanTAlib)
[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=mihakralj_QuanTAlib&metric=security_rating)](https://sonarcloud.io/summary/new_code?id=mihakralj_QuanTAlib)
[![CodeFactor](https://www.codefactor.io/repository/github/mihakralj/quantalib/badge/main)](https://www.codefactor.io/repository/github/mihakralj/quantalib/overview/main)

[![Nuget](https://img.shields.io/nuget/v/QuanTAlib?style=flat-square)](https://www.nuget.org/packages/QuanTAlib/)
![GitHub last commit](https://img.shields.io/github/last-commit/mihakralj/QuanTAlib)
[![Nuget](https://img.shields.io/nuget/dt/QuanTAlib?style=flat-square)](https://www.nuget.org/packages/QuanTAlib/)
[![GitHub watchers](https://img.shields.io/github/watchers/mihakralj/QuanTAlib?style=flat-square)](https://github.com/mihakralj/QuanTAlib/watchers)
[![.NET7.0](https://img.shields.io/badge/.NET-7.0%20%7C%206.0%20%7C%204.8-blue?style=flat-square)](https://dotnet.microsoft.com/en-us/download/dotnet/7.0)

Quantitative TA Library (**QuanTAlib**) is an easy-to-use C# library for quantitative technical analysis with base algorithms, charts, signals and strategies useful for trading securities with [Quantower](https://www.quantower.com/) and other C#-based trading platforms.

**QuanTAlib** is written with some specific design criteria in mind - some reasons why there is '_yet another C# TA library_':

- Written in native C# - no code conversion from TA-LIB or other imported/converted TA libraries
- No usage of Decimal datatypes, LINQ, interface abstractions, or static classes with tons of methods (all for performance reasons)
- Supports both **historical data analysis** (working on bulk of historical arrays) and **real-time analysis** (adding one data item at the time without the need to re-calculate the whole history)
- Calculate early data right - no hiding of incomplete calculations with NaN values (unless explicitly requested with useNan: true), data is as valid as mathematically possible from the first value
- Usage of events - each data series is an event publisher, each indicator is a subscriber - this allows seamless data flow between indicators)
- Seamlessly integrates with **Polyglot notebooks** (.NET Interactive) and used in Jupyter notebooks - see the examples and documentation.

QuanTAlib does not focus on sources of OHLCV quotes. There are some basic data feeds available to use in learning and strategy exploration: `RND_Feed` and `GBM_Feed` for random data feed, `Yahoo_Feed` and `Alphavantage_Feed` for quick grab of basic daily data of US stock market.

See [Getting Started](https://github.com/mihakralj/QuanTAlib/blob/main/Docs/getting_started.ipynb) .NET interactive notebook to get a feel how library works. Developers can use QuanTAlib in .NET interactive or in console apps, but the best usage of the library is withing C#-enabled trading platforms - see **QuanTower_Charts** folder for Quantower examples.

## Coverage

⭐= Calculation is validated against other TA libraries

✔️= Calculation exists but has no cross-validation tests

⛔= Not implemented (yet)

| **BASIC TRANSFORMS** | **QuanTAlib** | **TA-LIB** | **Skender** |
|--|:--:|:--:|:--:|
| ✔️ OC2 - (Open+Close)/2 |️ `.OC2` || ️`GetBaseQuote` |
| ⭐ HL2 - Median Price | `.HL2` | `MEDPRICE` | ️`GetBaseQuote` |
| ⭐ HLC3 - Typical Price | `.HLC3` | `TYPPRICE` ||
| ✔️ OHL3 - (Open+High+Low)/3 | `.OHL3` |||
| ⭐ OHLC4 - Average Price | `.OHLC4` | `AVGPRICE` |️ `GetBaseQuote` |
| ⭐ HLCC4 - Weighted Price |  `.HLCC4` | `WCLPRICE` ||
| ⭐ MAX - Max value | `MAX_Series` | `MAX` ||
| ⭐ MIN - Min value | `MIN_Series` | `MIN` ||
| ⛔ MID - Midpoint value || `MIDPOINT` ||
| ⛔ MIDP - Midpoint price || `MIDPRICE` ||
| ⛔ SUM - Summation || `SUM` ||
| ⭐ ADD - Addition | `ADD_Series` | `ADD` ||
| ⭐ SUB - Subtraction | `SUB_Series` | `SUB` ||
| ⭐ MUL - Multiplication | `MUL_Series` | `MUL` ||
| ⭐ DIV - Division | `DIV_Series` | `DIV` ||
|||||
| **STATISTICS & NUMERICAL ANALYSIS** | **QuanTAlib** | **TA-LIB** | **Skender** |
| ✔️ BIAS - Bias | BIAS_Series |||
| ⛔ CORREL - Pearson's Correlation Coefficient || CORREL | GetCorrelation |
| ⛔ COVAR - Covariance ||| GetCorrelation |
| ✔️ ENTP - Entropy | ENTP_Series |||
| ✔️ KURT - Kurtosis | KURT_Series |||
| ⭐ LINREG - Linear Regression | LINREG_Series || GetSlope |
| ⭐ MAD - Mean Absolute Deviation | MAD_Series || GetSma |
| ⭐ MAPE - Mean Absolute Percent Error | MAPE_Series || GetSma |
| ✔️ MED - Median value | MED_Series |||
| ✔️ MSE - Mean Squared Error | MSE_Series || GetSma |
| ⛔ SKEW - Skewness ||||
| ⭐ SDEV - Standard Deviation (Volatility) | SDEV_Series | STDDEV ||
| ✔️ SSDEV - Sample Standard Deviation | SSDEV_Series |||
| ✔️ SMAPE - Symmetric Mean Absolute Percent Error | SMAPE_Series |||
| ✔️ VAR - Population Variance | VAR_Series | VAR ||
| ✔️ SVAR - Sample Variance | SVAR_Series |||
| ⛔ QUANT - Quantile ||||
| ✔️ WMAPE - Weighted Mean Absolute Percent Error | WMAPE_Series |||
| ⛔ ZSCORE - Number of standard deviations from mean ||||
|||||
| **TREND INDICATORS & AVERAGES** | **QuanTAlib** | **TA-LIB** | **Skender** |
| ⛔ AFIRMA - Autoregressive Finite Impulse Response Moving Average ||||
| ⭐ ALMA - Arnaud Legoux Moving Average | ALMA_Series || GetAlma |
| ⛔ ARIMA - Autoregressive Integrated Moving Average ||||
| ⭐ DEMA - Double EMA Average | DEMA_Series | DEMA | GetDema |
| ⭐ EMA - Exponential Moving Average | EMA_Series || GetEma |
| ⛔ EPMA - Endpoint Moving Average ||| GetEpma |
| ⛔ FRAMA - Fractal Adaptive Moving Average ||||
| ⛔ FWMA - Fibonacci's Weighted Moving Average ||||
| ⛔ HILO - Gann High-Low Activator ||||
| ✔️ HEMA - Hull/EMA Average | HEMA_Series |||
| ⛔ Hilbert Transform Instantaneous Trendline || HT_TRENDLINE | GetHtTrendline |
| ⭐ HMA - Hull  Moving Average | HMA_Series || GetHma |
| ⛔ HWMA - Holt-Winter Moving Average ||||
| ✔️ JMA - Jurik Moving Average | JMA_Series |||
| ⭐ KAMA - Kaufman's Adaptive Moving Average | KAMA_Series | KAMA | GetKama |
| ⛔ KDJ - KDJ Indicator (trend reversal) ||||
| ⛔ LSMA - Least Squares Moving Average ||||
| ⭐ MACD - Moving Average Convergence/Divergence | MACD_Series | MACD | GetMacd |
| ⛔ MAMA - MESA Adaptive Moving Average || MAMA | GetMama |
| ⛔ MCGD - McGinley Dynamic ||||
| ⛔ MMA - Modified Moving Average ||||
| ⛔ PPMA - Pivot Point Moving Average ||||
| ⛔ PWMA - Pascal's Weighted Moving Average ||||
| ✔️ RMA - WildeR's Moving Average | RMA__Series |||
| ⛔ SINWMA - Sine Weighted Moving Average ||||
| ⭐ SMA - Simple Moving Average | SMA_Series |||
| ⭐ SMMA - Smoothed Moving Average | SMMA_Series |||
| ⛔ SSF - Ehler's Super Smoother Filter ||||
| ⛔ SUP - Supertrend ||||
| ⛔ SWMA - Symmetric Weighted Moving Average ||||
| ⛔ T3 - Tillson T3 Moving Average || T3 | GetT3 |
| ⭐ TEMA - Triple EMA Average | TEMA_Series | TEMA | GetTema |
| ⛔ TRIMA - Triangular Moving Average || TRIMA ||
| ⛔ TSF - Time Series Forecast || TSF ||
| ⛔ VIDYA - Variable Index Dynamic Average ||||
| ⛔ VOR - Vortex Indicator ||||
| ⭐ WMA - Weighted Moving Average | WMA_Series | WMA | GetWma |
| ✔️ ZLEMA - Zero Lag EMA Average | ZLEMA_Series |||
|||||
| **VOLATILITY INDICATORS** | **QuanTAlib** | **TA-LIB** | **Skender** |
| ⭐ ADL - Chaikin Accumulation Distribution Line | ADL_Series | AD | GetAdl |
| ⭐ ADOSC - Chaikin Accumulation Distribution Oscillator | ADOSC_Series | ADOSC| GetAdl |
| ⭐ ATR - Average True Range | ATR_Series | ATR | GetAtr |
| ⭐ ATRP - Average True Range Percent | ATRP_Series || GetAtr |
| ⛔ BETA - Beta coefficient || BETA | GetBeta |
| ⭐ BBANDS - Bollinger Bands® | BBANDS_Series | BBANDS | GetBollingerBands |
| ⛔ CHAND - Chandelier Exit ||| GetChandelier |
| ⛔ CRSI - Connor RSI ||| GetConnorsRsi |
| ⛔ DON - Donchian Channels ||| GetDonchian |
| ⛔ FCB - Fractal Chaos Bands ||| GetFcb |
| ⛔ HV - Historical Volatility ||||
| ⛔ ICH - Ichimoku ||| GetIchimoku |
| ⛔ KEL - Keltner Channels ||| GetKeltner |
| ⛔ NATR - Normalized Average True Range || NATR | GetAtr |
| ⛔ CHN - Price Channel Indicator ||||
| ⭐ RSI - Relative Strength Index | RSI_Series | RSI | GetRsi |
| ⛔ SAR - Parabolic Stop and Reverse || SAR | GetParabolicSar |
| ⛔ SRSI - Stochastic RSI || STOCHRSI | GetStochRsi |
| ⛔ STARC - Starc Bands ||||
| ⭐ TR - True Range | TR_Series | TRANGE | GetTr |
| ⛔ UI - Ulcer Index ||||
| ⛔ VSTOP - Volatility Stop ||||
|||||
| **MOMENTUM INDICATORS & OSCILLATORS** | **QuanTAlib** | **TA-LIB** | **Skender** |
| ⛔ AC - Acceleration Oscillator ||||
| ⛔ ADX - Average Directional Movement Index || ADX | GetAdx |
| ⛔ ADXR - Average Directional Movement Index Rating || ADXR | GetAdx |
| ⛔ AO - Awesome Oscillator ||| GetAwesome |
| ⛔ APO - Absolute Price Oscillator || APO ||
| ⛔ AROON - Aroon oscillator || AROON | GetAroon |
| ⛔ BOP - Balance of Power || BOP | GetBop |
| ⭐ CCI - Commodity Channel Index | CCI_Series | CCI | GetCci |
| ⛔ CFO - Chande Forcast Oscillator ||||
| ⛔ CMO - Chande Momentum Oscillator || CMO | GetCmo |
| ⛔ COG - Center of Gravity ||||
| ⛔ COPPOCK - Coppock Curve ||||
| ⛔ CTI - Ehler's Correlation Trend Indicator ||||
| ⛔ DPO - Detrended Price Oscillator ||| GetDpo |
| ⛔ DMI - Directional Movement Index || DX | GetAdx |
| ⛔ EFI - Elder Ray's Force Index ||| GetElderRay |
| ⛔ GAT - Alligator oscillator ||| GetGator |
| ⛔ HURST - Hurst Exponent ||| GetHurst |
| ⛔ KRI - Kairi Relative Index ||||
| ⛔ KVO - Klinger Volume Oscillator ||||
| ⛔ MFI - Money Flow Index || MFI | GetMfi |
| ⛔ MOM - Momentum || MOM ||
| ⛔ NVI - Negative Volume Index ||||
| ⛔ PO - Price Oscillator ||||
| ⛔ PPO - Percentage Price Oscillator || PPO ||
| ⛔ PMO - Price Momentum Oscillator ||||
| ⛔ PVI - Positive Volume Index ||||
| ⛔ ROC - Rate of Change || MOM | GetRoc |
| ⛔ RVGI - Relative Vigor Index ||||
| ⛔ SMI - Stochastic Momentum Index ||||
| ⛔ STC - Schaff Trend Cycle ||||
| ⛔ STOCH - Stochastic Oscillator || STOCH | GetStoch |
| ⛔ TRIX - 1-day ROC of TEMA || TRIX | GetTrix |
| ⛔ TSI - True Strength Index ||||
| ⛔ UO - Ultimate Oscillator || ULTOSC | GetUltimate |
| ⛔ WILLR - Larry Williams' %R || WILLR | GetWilliamsR |
| ⛔ WGAT - Williams Alligator ||||
|||||
| **VOLUME INDICATORS** | **QuanTAlib** | **TA-LIB** | **Skender** |
| ⛔ AOBV - Archer On-Balance Volume ||||
| ⛔ CMF - Chaikin Money Flow ||||
| ⛔ EOM - Ease of Movement ||||
| ⛔ OBV - On-Balance Volume || OBV | GetObv |
| ⛔ PRS - Price Relative Strength |||
| ⛔ PVOL - Price-Volume ||||
| ⛔ PVO - Percentage Volume Oscillator ||||
| ⛔ PVR - Price Volume Rank ||||
| ⛔ PVT - Price Volume Trend ||||
| ⛔ VP - Volume Profile ||||
| ⛔ VWAP - Volume Weighted Average Price ||||
| ⛔ VWMA - Volume Weighted Moving Average ||||
