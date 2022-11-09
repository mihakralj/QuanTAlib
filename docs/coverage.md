# Coverage of indicators

✔️= Calculation exists in QuanTAlib

⭐= Calculation is validated against other TA libraries

| **BASIC TRANSFORMS** | **QuanTAlib** | **TA-LIB** | **Skender** |
|--|:--:|:--:|:--:|
| ✔️ OC2 - (Open+Close)/2 |️ .OC2 || ️GetBaseQuote |
| ⭐ HL2 - Median Price | .HL2 | MEDPRICE | ️GetBaseQuote |
| ⭐ HLC3 - Typical Price | .HLC3 | TYPPRICE ||
| ✔️ OHL3 - (Open+High+Low)/3 | .OHL3 |||
| ⭐ OHLC4 - Average Price | .OHLC4 | AVGPRICE |️ GetBaseQuote |
| ⭐ HLCC4 - Weighted Price |  .HLCC4 | WCLPRICE ||
| ✔️ ZL - De-lagged price (Zero-Lag) | ZL_Series |||
| ⭐ ADD - Addition | ADD_Series | ADD ||
| ⭐ SUB - Subtraction | SUB_Series | SUB ||
| ⭐ MUL - Multiplication | MUL_Series | MUL ||
| ⭐ DIV - Division | DIV_Series | DIV ||
|||||
| **STATISTICS & NUMERICAL ANALYSIS** | **QuanTAlib** | **TA-LIB** | **Skender** |
| ✔️ BIAS - Bias | BIAS_Series |||
| ✔️ ENTP - Entropy | ENTP_Series |||
| ✔️ KURT - Kurtosis | KURT_Series |||
| ⭐ LINREG - Linear Regression | LINREG_Series |||
| ⭐ MAD - Mean Absolute Deviation | MAD_Series |||
| ⭐ MAPE - Mean Absolute Percent Error | MAPE_Series |||
| ✔️ MED - Median value | MED_Series |||
| ⭐ MAX - Max value | MAX_Series |||
| ⭐ MIN - Min value | MIN_Series |||
| ✔️ MSE - Mean Squared Error | MSE_Series |||
| ⛔ SKEW - Skewness ||||
| ⭐ SDEV - Standard Deviation (Volatility) | SDEV_Series |||
| ✔️ SSDEV - Sample Standard Deviation | SSDEV_Series |||
| ✔️ SMAPE - Symmetric Mean Absolute Percent Error | SMAPE_Series |||
| ✔️ VAR - Population Variance | VAR_Series |||
| ✔️ SVAR - Sample Variance | SVAR_Series |||
| ⛔ QUANT - Quantile ||||
| ✔️ WMAPE - Weighted Mean Absolute Percent Error | WMAPE_Series |||
| ⛔ ZSCORE - Number of standard deviations from mean ||||
|||||
| **TREND INDICATORS & AVERAGES** | **QuanTAlib** | **TA-LIB** | **Skender** |
| ⛔ AFIRMA - Autoregressive Finite Impulse Response Moving Average ||||
| ⭐ ALMA - Arnaud Legoux Moving Average | ALMA_Series || GetAlma |
| ⛔ ARIMA - Autoregressive Integrated Moving Average ||||
| ⭐ DEMA - Double EMA Average | DEMA_Series |||
| ⭐ EMA - Exponential Moving Average | EMA_Series |||
| ⛔ EPMA - Endpoint Moving Average ||||
| ⛔ FWMA - Fibonacci's Weighted Moving Average ||||
| ✔️ HEMA - Hull/EMA Average | HEMA_Series |||
| ⭐ HMA - Hull  Moving Average | HMA_Series |||
| ⛔ HWMA - Holt-Winter Moving Average ||||
| ✔️ JMA - Jurik Moving Average | JMA_Series |||
| ⭐ KAMA - Kaufman's Adaptive Moving Average | KAMA_Series |||
| ⛔ LSMA - Least Squares Moving Average ||||
| ⭐ MACD - Moving Average Convergence/Divergence | MACD_Series |||
| ⛔ MAMA - MESA Adaptive Moving Average ||||
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
| ⛔ T3 - Tillson T3 Moving Average ||||
| ⭐ TEMA - Triple EMA Average | TEMA_Series |||
| ⛔ TRIMA - Triangular Moving Average ||||
| ⛔ VIDYA - Variable Index Dynamic Average ||||
| ⭐ WMA - Weighted Moving Average | WMA_Series |||
| ✔️ ZLEMA - Zero Lag EMA Average | ZLEMA_Series |||
|||||
| **VOLATILITY INDICATORS** | **QuanTAlib** | **TA-LIB** | **Skender** |
| ⭐ ADL - Chaikin Accumulation Distribution Line | ADL_Series |||
| ⭐ ADOSC - Chaikin Accumulation Distribution Oscillator | ADOSC_Series |||
| ⭐ ATR - Average True Range | ATR_Series | ATR | GetAtr |
| ⭐ ATRP - Average True Range Percent | ATRP_Series |||
| ✔️ BETA - Beta coefficient ||||
| ⭐ BBANDS - Bollinger Bands® | BBANDS_Series | BBANDS | GetBollingerBands |
| ⛔ CRSI - Connor RSI ||||
| ⛔ DON - Donchian Channels ||||
| ⛔ FCB - Fractal Chaos Bands ||||
| ⛔ HV - Historical Volatility ||||
| ⛔ ICH - Ichimoku ||||
| ⛔ KEL - Keltner Channels ||||
| ⛔ NATR - Normalized Average True Range ||||
| ⭐ RSI - Relative Strength Index | RSI_Series ||
| ⛔ SRSI - Stochastic RSI ||||
| ⛔ STARC - Starc Bands ||||
| ⭐ TR - True Range | TR_Series |||
| ⛔ UI - Ulcer Index ||||
| ⛔ VSTOP - Volatility Stop ||||
|||||
| **MOMENTUM INDICATORS & OSCILLATORS** | **QuanTAlib** | **TA-LIB** | **Skender** |
| ⛔ AC - Acceleration Oscillator ||||
| ⛔ ADX - Average Directional Movement Index ||||
| ⛔ ADXR - Average Directional Movement Index Rating ||||
| ⛔ AO - Awesome Oscillator ||||
| ⛔ APO - Absolute Price Oscillator ||||
| ⛔ AROON - Aroon oscillator ||||
| ⛔ BOP - Balance of Power ||||
| ⭐ CCI - Commodity Channel Index | CCI_Series |||
| ⛔ CFO - Chande Forcast Oscillator ||||
| ⛔ CMF - Chaikin Money Flow ||||
| ⛔ CMO - Chande Momentum Oscillator ||||
| ⛔ COG - Center of Gravity ||||
| ⛔ CTI - Ehler's Correlation Trend Indicator ||||
| ⛔ DPO - De-trended Price Oscillator ||||
| ⛔ DMI - Directional Movement Index ||||
| ⛔ EFI - Elder Ray's Force Index ||||
| ⛔ GAT - Alligator oscillator ||||
| ⛔ KRI - Kairi Relative Index ||||
| ⛔ KVO - Klinger Volume Oscillator ||||
| ⛔ MFI - Money Flow Index ||||
| ⛔ MOM - Momentum ||||
| ⛔ NVI - Negative Volume Index ||||
| ⛔ PO - Price Oscillator ||||
| ⛔ PPO - Percentage Price Oscillator ||||
| ⛔ PMO - Price Momentum Oscillator ||||
| ⛔ PVI - Positive Volume Index ||||
| ⛔ RVGI - Relative Vigor Index ||||
| ⛔ SMI - Stochastic Momentum Index ||||
| ⛔ STOCH - Stochastic Oscillator ||||
| ⛔ TRIX - 1-day ROC of TEMA ||||
| ⛔ TSI - True Strength Index ||||
| ⛔ UO - Ultimate Oscillator ||||
| ⛔ WGAT - Williams Alligator ||||
|||||
| **VOLUME INDICATORS** | **QuanTAlib** | **TA-LIB** | **Skender** |
| ⛔ AOBV - Archer On-Balance Volume ||||
| ⛔ OBV - On-Balance Volume ||||
| ⛔ PRS - Price Relative Strength |||
| ⛔ PVOL - Price-Volume ||||
| ⛔ PVO - Percentage Volume Oscillator ||||
| ⛔ PVR - Price Volume Rank ||||
| ⛔ PVT - Price Volume Trend ||||
| ⛔ VP - Volume Profile ||||
| ⛔ VWAP - Volume Weighted Average Price ||||
| ⛔ VWMA - Volume Weighted Moving Average ||||
|||||
|**Unsorted** | **QuanTAlib** | **TA-LIB** | **Skender** |
| ⛔ CHN - Price Channel ||||
| ⛔ COPPOCK - Coppock Curve ||||
| ⛔ CORREL - Pearson's Correlation Coefficient ||||
| ⛔ EOM - Ease of Movement ||||
| ⛔ HILO - Gann High-Low Activator ||||
| ⛔ HT - HT Trendline ||||
| ⛔ MCGD - McGinley Dynamic ||||
| ⛔ ROC - Rate of Change ||||
| ⛔ SAR - Parabolic Stop and Reverse ||||
| ⛔ STC - Schaff Trend Cycle ||||
| ⛔ WILLR - Larry Williams' %R ||||
| ⛔ HURST - Hurst Exponent ||||
| ⛔ VOR - Vortex Indicator ||||
| ⛔ PVT - Pivot Points ||||
| ⛔ KDJ - KDJ Index ||||
| ⛔ CHAND - Chandelier Exit ||||
| ⛔ Hilbert Transform Instantaneous Trendline ||||
