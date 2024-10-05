# Indicators in QuanTAlib

⭐= Validation against several TA libraries<br>
✔️= Validation tests passed<br>
❌= Issue

|**BASIC TRANSFORMS**|**QuanTALib**|Skender.Stock|TALib.NETCore|Tulip.NETCore|Trady|
|--|:--:|:--:|:--:|:--:|:--:|
|OC2 - Midpoint price|️`.OC2`|CandlePart.OC2|MidPoint||
|HL2 - Median Price|️`.HL2`|CandlePart.HL2|MedPrice||
|HLC3 - Typical Price|️`.HLC3`|CandlePart.HLC3|TypPrice||
|OHL3 - Mean Price|`️.OHL3`|CandlePart.OHL3`|||
|OHLC4 - Average Price|`️.OHLC4`|CandlePart.OHLC4|AvgPrice||
|HLCC4 - Weighted Price|`️.HLCC4`||WclPrice||
|<br>||||
|**STATISTICS AND NUMERICAL ANALYSIS**|**QuanTALib**|Skender.Stock|TALib.NETCore|Tulip.NETCore|Trady|
|BETA - Beta coefficient|||||
|CORR - Correlation Coefficient|||||
|CURVATURE - Rate of Change in Direction or Slope|`Curvature`||||
|ENTROPY - Measure of Uncertainty or Disorder|`Entropy`||||
|KURTOSIS - Measure of Tails/Peakedness|`Kurtosis`||||
|HUBER - Huber Loss|||||
|MAX - Maximum with exponential decay|`Max`||||
|MAE - Mean Absolute Error|||||
|MAPD - Mean Absolute Percentage Deviation|||||
|MAPE - Mean Absolute Percentage Error|||||
|MASE - Mean Absolute Scaled Error|||||
|MDA - Mean Directional Accuracy|||||
|ME - Mean Error|||||
|MEDIAN - Middle value|`Median`||||
|MIN - Minimum with exponential decay|`Min`||||
|MODE - Most Frequent Value|`Mode`||||
|MPE - Pean Percentage Error|||||
|MSE - Mean Squared Error|||||
|MSLE - Mean Squared Logarithmic Error|||||
|PERCENTILE - Rank Order|`Percentile`||||
|RSQUARED - Coefficient of Determination R-Squared|||||
|RAE - Relative Absolute Error|||||
|RMSE - Root Mean Squared Error|||||
|RSE - Relateive Squared Error|||||
|RMSLE - Root Mean Squared Logarithmic Error|||||
|SKEW - Skewness, asymmetry of distribution|`Skew`||||
|SLOPE - Rate of Change, Linear Regression|`Slope`||||
|SMAPE - Symmetric Mean Absolute Percentage Error|||||
|STDDEV - Standard Deviation, Measure of Spread|||||
|THEIL - Theil's U Statistics|||||
|VARIANCE - Average of Squared Deviations|`Variance`||||
|ZSCORE - Standardized Score|`Zscore`||||
|<br>|||||
|**AVERAGES & TRENDS**|**QuanTALib**|Skender.Stock|TALib.NETCore|Tulip.NETCore|Trady|
|AFIRMA - Autoregressive Finite Impulse Response Moving Average|`Afirma`||||
|ALMA - Arnaud Legoux Moving Average|`Alma`|`✔️`|||
|⭐DEMA - Double EMA Average|`Dema`|`⭐`|`⭐`|`⭐`||
|DSMA - Deviation Scaled Moving Average|`Dsma`||||
|DWMA - Double WMA Average|`Dwma`||||
|⭐EMA - Exponential Moving Average|`Ema`|`⭐`|`⭐`|`⭐`|`⭐`|
|EPMA - Endpoint Moving Average|`Epma`|`✔️`|||
|FRAMA - Fractal Adaptive Moving Average|`Frama`||||
|FWMA - Fibonacci Weighted Moving Average|`Fwma`||||
|HILO - Gann High-Low Activator|||||
|HTIT - Hilbert Transform Instantaneous Trendline|`Htit`|`✔️`|`✔️`||
|GMA - Gaussian-Weighted Moving Average|`Gma`||||
|HMA - Hull  Moving Average|`Hma`|`✔️`||`✔️`|
|HWMA - Holt-Winter Moving Average|`Hwma`||||
|JMA - Jurik Moving Average|`Jma`||||
|KAMA - Kaufman's Adaptive Moving Average|`Kama`|`✔️`|`✔️`|`✔️`|
|KDJ - KDJ Indicator (trend reversal)|||||
|LTMA - Laguerre Transform Moving Average|`Ltma`||||
|MAAF - Median-Average Adaptive Filter|`Maaf`||||
|MACD - Movign Average Convergence/Divergence||`✔️`|`✔️`||
|MAMA - MESA Adaptive Moving Average|`Mama`|`✔️`|`✔️`||
|MGDI - McGinley Dynamic Indicator|`Mgdi`|`✔️`|||
|MMA - Modified Moving Average|`Mma`||||
|PPMA - Pivot Point Moving Average|||||
|PWMA - Pascal's Weighted Moving Average|||||
|QEMA - Quad Exponential Moving Average|`Qema`||||
|RMA - WildeR's Moving Average|`Rma`||||
|SINEMA - Sine Weighted Moving Average|`Sinema`||||
|⭐SMA - Simple Moving Average|`Sma`|`⭐`|`⭐`|`⭐`|`⭐`|
|SMMA - Smoothed Moving Average|`Smma`|`✔️`|||
|SSF - Ehler's Super Smoother Filter|||||
|SUPERTREND - Supertrend||`✔️`|||
|SWMA - Symmetric Weighted Moving Average|||||
|T3 - Tillson T3 Moving Average|`T3`|`✔️`|`✔️`||
|TEMA - Triple EMA Average|`Tema`|`✔️`|`✔️`|`✔️`|
|TRIMA - Triangular Moving Average|`Trima`|`✔️`||`✔️`|
|TSF - Time Series Forecast|||`✔️`|`✔️`|
|VIDYA - Variable Index Dynamic Average|`Vidya`|||`✔️`|
|VORTEX - Vortex Indicator||`✔️`|||
|WMA - Weighted Moving Average|`Wma`|`✔️`|`✔️`|`✔️`|
|ZLEMA - Zero Lag EMA Average|`Zlema`|||`✔️`|
|<br>||||
|**VOLATILITY INDICATORS**|**QuanTALib**|Skender.Stock|TALib.NETCore|Tulip.NETCore|Trady|
|ADL - Chaikin Accumulation Distribution Line||GetAdl|Ad||
|ADOSC - Chaikin Accumulation Distribution Oscillator||GetChaikinOsc|AdOsc||
|ATR - Average True Range||GetAtr|Atr||
|ATRP - Average True Range Percent|||||
|ATRSTOP - ATR Trailing Stop ||GetAtrStop|||
|BBANDS - Bollinger Bands®||BollingerBands|||
|CHAND - Chandelier Exit||GetChandelier|||
|CRSI - Connor RSI||GetConnorsRsi|||
|CVI - Chaikins Volatility|||||
|DON - Donchian Channels||GetDonchian|||
|FCB - Fractal Chaos Bands||GetFcb|||
|FISHER - Fisher Transform|||||
|HV - Historical Volatility|||||
|ICH - Ichimoku Cloud||GetIchimoku|||
|KEL - Keltner Channels||GetKeltner|||
|NATR - Normalized Average True Range||GetAtr|||
|CHN - Price Channel Indicator|||||
|RSI - Relative Strength Index||GetRsi|||
|SAR - Parabolic Stop and Reverse||GetParabolicSar|||
|SRSI - Stochastic RSI||GetStochRsi|||
|STARC - Starc Bands||GetStarcBands|||
|TR - True Range|||||
|UI - Ulcer Index||GetUlcerIndex|||
|VSTOP - Volatility Stop||GetVolatilityStop|||
|<br>||||
|**MOMENTUM INDICATORS & OSCILLATORS**|**QuanTALib**|Skender.Stock|TALib.NETCore|Tulip.NETCore|Trady|
|AC - Acceleration Oscillator|||||
|ADX - Average Directional Movement Index||GetAdx|Adx||
|ADXR - Average Directional Movement Index||Rating|Adxr||
|AO - Awesome Oscillator||GetAwesome|||
|APO - Absolute Price Oscillator||Apo|||
|AROON - Aroon oscillator||GetAroon|Aroon||
|BOP - Balance of Power||GetBop|Bop||
|CCI - Commodity Channel Index||GetCci|Cci||
|CFO - Chande Forcast Oscillator|||||
|CMO - Chande Momentum Oscillator||GetCmo|Cmo||
|CHOP - Choppiness Index||GetChop|||
|COG - Center of Gravity|||||
|COPPOCK - Coppock Curve|||||
|CTI - Ehler's Correlation Trend Indicator|||||
|DPO - Detrended Price Oscillator||GetDpo|||
|DMI - Directional Movement Index||GetDmi|||
|EFI - Elder Ray's Force Index||GetElderRay|||
|FOSC - Forecast oscillator||||||
|GATOR - Gator oscillator||GetGator|||
|HURST - Hurst Exponent||GetHurst|||
|KRI - Kairi Relative Index|||||
|KVO - Klinger Volume Oscillator||GetKvo||||
|MFI - Money Flow Index||GetMfi|||
|MOM - Momentum|||||
|NVI - Negative Volume Index|||||
|PO - Price Oscillator|||||
|PPO - Percentage Price Oscillator|||||
|PMO - Price Momentum Oscillator||GetPmo|||
|PVI - Positive Volume Index|||||
|ROC - Rate of Change||GetRoc|||
|RVGI - Relative Vigor Index|||||
|SMI - Stochastic Momentum Index||GetSmi|||
|STC - Schaff Trend Cycle||GetStc|||
|STOCH - Stochastic Oscillator||`GetStoch|||
|TRIX - 1-day ROC of TEMA||GetTrix||trix.Run|
|TSI - True Strength Index||GetTsi|||
|UO - Ultimate Oscillator||GetUltimate|||
|WILLR - Larry Williams' %R||GetWilliamsR|||
|WGAT - Williams Alligator||GetAlligator|||
|<br>||||
|**VOLUME INDICATORS**|**QuanTALib**|Skender.Stock|TALib.NETCore|Tulip.NETCore|Trady|
|AOBV - Archer On-Balance Volume|||||
|CMF - Chaikin Money Flow||GetCmf|||
|EOM - Ease of Movement|||||
|KVO - Klinger Volume Oscilaltor|||||
|OBV - On-Balance Volume||GetObv|||
|PRS - Price Relative Strength||`GetPrs|||
|PVOL - Price-Volume|||||
|PVO - Percentage Volume Oscillator||GetPvo|||
|PVR - Price Volume Rank|||||
|PVT - Price Volume Trend|||||
|VP - Volume Profile|||||
|VWAP - Volume Weighted Average Price||GetVwap|||
|VWMA - Volume Weighted Moving Average||GetVwma||||
