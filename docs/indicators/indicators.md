# Indicators in QuanTAlib

⭐= Validation against several TA libraries<br>
✔️= Validation tests passed<br>
❌= Issue

|**MOMENTUM INDICATORS**|**Class Name**|Skender.Stock|TALib.NETCore|
|--|:--:|:--:|:--:|
|DMI - Directional Movement Index|`?`|GetDmi||
|DMX - Jurik Directional Movement Index|`?`|||
|MOM - Momentum|`?`|||
|VEL - Jurik Signal Velocity|`?`|||
|ADX - Average Directional Movement Index|`?`|GetAdx|Adx|
|ADXR - Average Directional Movement Index|`?`|Rating|Adxr|
|APO - Absolute Price Oscillator|`?`|Apo||
|DPO - Detrended Price Oscillator|`?`|GetDpo||
|MACD - Moving Average Convergence/Divergence|`?`|||
|PO - Price Oscillator|`?`|||
|PPO - Percentage Price Oscillator|`?`|||
|PMO - Price Momentum Oscillator|`?`|GetPmo||
|PRS - Price Relative Strength|`?`|GetPrs||
|ROC - Rate of Change|`?`|GetRoc||
|TRIX - 1-day ROC of TEMA|`?`|GetTrix||
|VORTEX - Vortex Indicator|`?`|||
<br>
|**VOLATILITY INDICATORS**|**Class Name**|Skender.Stock|TALib.NETCore|
|ADR - Average Daily Range|`?`|||
|ANDREW - Andrew's Pitchfork|`?`|||
|ATR - Average True Range|`Atr`|GetAtr|Atr|
|ATRP - Average True Range Percent|`?`|||
|ATRSTOP - ATR Trailing Stop|`?`|GetAtrStop||
|BBANDS - Bollinger Bands®|`?`|BollingerBands||
|CHAND - Chandelier Exit|`?`|GetChandelier||
|CVI - Chaikins Volatility|`?`|||
|DON - Donchian Channels|`?`|GetDonchian||
|FCB - Fractal Chaos Bands|`?`|GetFcb||
|HV - Historical Volatility|`Hv`|||
|ICH - Ichimoku Cloud|`?`|GetIchimoku||
|KEL - Keltner Channels|`?`|GetKeltner||
|NATR - Normalized Average True Range|`?`|GetAtr||
|CHN - Price Channel Indicator|`?`|||
|SAR - Parabolic Stop and Reverse|`?`|GetParabolicSar||
|STARC - Starc Bands|`?`|GetStarcBands||
|TR - True Range|`?`|||
|UI - Ulcer Index|`?`|GetUlcerIndex||
|VSTOP - Volatility Stop|`?`|GetVolatilityStop||
<br>
|**OSCILLATORS**|**Class Name**|Skender.Stock|TALib.NETCore|
|RSI - Relative Strength Index|`Rsi`|GetRsi||
|RSX - Jurik Trend Strength Index|`Rsx`|||
|AC - Acceleration Oscillator|`?`|||
|AO - Awesome Oscillator|`?`|GetAwesome||
|AROON - Aroon oscillator|`?`|GetAroon|Aroon|
|BOP - Balance of Power|`?`|GetBop|Bop|
|CCI - Commodity Channel Index|`?`|GetCci|Cci|
|CFO - Chande Forcast Oscillator|`?`|||
|CMO - Chande Momentum Oscillator|`Cmo`|GetCmo|Cmo|
|CHOP - Choppiness Index|`?`|GetChop||
|COG - Ehler's Center of Gravity|`?`|||
|COPPOCK - Coppock Curve|`?`|||
|CRSI - Connor RSI|`?`|GetConnorsRsi||
|CTI - Ehler's Correlation Trend Indicator|`?`|||
|DOSC - Derivative Oscillator|`?`|||
|EFI - Elder Ray's Force Index|`?`|GetElderRay||
|FISHER - Fisher Transform|`?`|||
|FOSC - Forecast Oscillator|`?`|||
|GATOR - Williams Alliator Oscillator|`?`|GetGator||
|KDJ - KDJ Indicator (trend reversal)|`?`|||
|KRI - Kairi Relative Index|`?`|||
|RVGI - Relative Vigor Index|`?`|||
|SMI - Stochastic Momentum Index|`?`|GetSmi||
|SRSI - Stochastic RSI|`?`|GetStochRsi||
|STC - Schaff Trend Cycle|`?`|GetStc||
|STOCH - Stochastic Oscillator|`?`|GetStoch||
|TSI - True Strength Index|`?`|GetTsi||
|UO - Ultimate Oscillator|`?`|GetUltimate||
|WILLR - Larry Williams' %R|`?`|GetWilliamsR||
<br>
|**VOLUME INDICATORS**|**Class Name**|Skender.Stock|TALib.NETCore|
|ADL - Chaikin Accumulation Distribution Line|`?`|GetAdl|Ad|
|ADOSC - Chaikin Accumulation Distribution Oscillator|`?`|GetChaikinOsc|AdOsc|
|AOBV - Archer On-Balance Volume|`?`|||
|CMF - Chaikin Money Flow|`?`|GetCmf||
|EOM - Ease of Movement|`?`|||
|KVO - Klinger Volume Oscillator|`?`|GetKvo||
|MFI - Money Flow Index|`?`|GetMfi||
|NVI - Negative Volume Index|`?`|||
|OBV - On-Balance Volume|`?`|GetObv||
|PVI - Positive Volume Index|`?`|||
|PVOL - Price-Volume|`?`|||
|PVO - Percentage Volume Oscillator|`?`|GetPvo||
|PVR - Price Volume Rank|`?`|||
|PVT - Price Volume Trend|`?`|||
|TVI - Trade Volume Index|`?`|||
|VP - Volume Profile|`?`|||
|VWAP - Volume Weighted Average Price|`?`|GetVwap||
|VWMA - Volume Weighted Moving Average|`?`|GetVwma||
<br>
|**NUMERICAL ANALYSIS**|**Class Name**|Skender.Stock|TALib.NETCore|
|BETA - Beta coefficient|`?`|||
|CORR - Correlation Coefficient|`?`|||
|CURVATURE - Rate of Change in Direction or Slope|`Curvature`|||
|ENTROPY - Measure of Uncertainty or Disorder|`Entropy`|||
|KURTOSIS - Measure of Tails/Peakedness|`Kurtosis`|||
|HUBER - Huber Loss|`Huber`|||
|HURST - Hurst Exponent|`?`|GetHurst||
|MAX - Maximum with exponential decay|`Max`|||
|MEDIAN - Middle value|`Median`|||
|MIN - Minimum with exponential decay|`Min`|||
|MODE - Most Frequent Value|`Mode`|||
|PERCENTILE - Rank Order|`Percentile`|||
|RSQUARED - Coefficient of Determination R-Squared|`?`|||
|SKEW - Skewness, asymmetry of distribution|`Skew`|||
|SLOPE - Rate of Change, Linear Regression|`Slope`|||
|STDDEV - Standard Deviation, Measure of Spread|`Stddev`|||
|THEIL - Theil's U Statistics|`?`|||
|TSF - Time Series Forecast|`?`|✔️|✔️|
|VARIANCE - Average of Squared Deviations|`Variance`|||
|ZSCORE - Standardized Score|`Zscore`|||
<br>
|**ERRORS**|**Class Name**|Skender.Stock|TALib.NETCore|
|MAE - Mean Absolute Error|`Mae`|||
|MAPD - Mean Absolute Percentage Deviation|`Mapd`|||
|MAPE - Mean Absolute Percentage Error|`Mape`|||
|MASE - Mean Absolute Scaled Error|`Mase`|||
|MDA - Mean Directional Accuracy|`Mda`|||
|ME - Mean Error|`Me`|||
|MPE - Mean Percentage Error|`Mpe`|||
|MSE - Mean Squared Error|`Mse`|||
|MSLE - Mean Squared Logarithmic Error|`Msle`|||
|RAE - Relative Absolute Error|`Rae`|||
|RMSE - Root Mean Squared Error|`Rmse`|||
|RSE - Relative Squared Error|`Rse`|||
|RMSLE - Root Mean Squared Logarithmic Error|`Rmsle`|||
|SMAPE - Symmetric Mean Absolute Percentage Error|`Smape`|||
<br>
|**AVERAGES & TRENDS**|**Class Name**|Skender.Stock|TALib.NETCore|
|AFIRMA - Autoregressive Finite Impulse Response Moving Average|`Afirma`|||
|ALMA - Arnaud Legoux Moving Average|`Alma`|✔️||
|DEMA - Double EMA Average|`Dema`|✔️|✔️|
|DSMA - Deviation Scaled Moving Average|`Dsma`|||
|DWMA - Double WMA Average|`Dwma`|||
|EMA - Exponential Moving Average|`Ema`|⭐|⭐|
|EPMA - Endpoint Moving Average|`Epma`|✔️||
|FRAMA - Fractal Adaptive Moving Average|`Frama`|||
|FWMA - Fibonacci Weighted Moving Average|`Fwma`|||
|HILO - Gann High-Low Activator|`?`|||
|HTIT - Hilbert Transform Instantaneous Trendline|`Htit`|✔️|✔️|
|GMA - Gaussian-Weighted Moving Average|`Gma`|||
|HMA - Hull Moving Average|`Hma`|✔️|✔️|
|HWMA - Holt-Winter Moving Average|`Hwma`|||
|JMA - Jurik Moving Average|`Jma`|||
|JORDAN - Jordan Moving Average|`?`|||
|KAMA - Kaufman's Adaptive Moving Average|`Kama`|✔️|✔️|
|LTMA - Laguerre Transform Moving Average|`Ltma`|||
|MAAF - Median-Average Adaptive Filter|`Maaf`|||
|MAMA - MESA Adaptive Moving Average|`Mama`|✔️|✔️|
|MGDI - McGinley Dynamic Indicator|`Mgdi`|✔️||
|MLMA - Minimal Lag Moving Average|`?`|||
|MMA - Modified Moving Average|`Mma`|||
|PPMA - Pivot Point Moving Average|`?`|||
|PWMA - Pascal's Weighted Moving Average|`Pwma`|||
|QEMA - Quad Exponential Moving Average|`Qema`|||
|RMA - WildeR's Moving Average|`Rma`|||
|SINEMA - Sine Weighted Moving Average|`Sinema`|||
|SMA - Simple Moving Average|`Sma`|||
|SMMA - Smoothed Moving Average|`Smma`|✔️||
|SSF - Ehler's Super Smoother Filter|`?`|||
|SUPERTREND - Supertrend|`?`|✔️||
|T3 - Tillson T3 Moving Average|`T3`|✔️|✔️|
|TEMA - Triple EMA Average|`Tema`|✔️|✔️|
|TRIMA - Triangular Moving Average|`Trima`|✔️||
|VIDYA - Variable Index Dynamic Average|`Vidya`|||
|WMA - Weighted Moving Average|`Wma`|✔️||
|ZLEMA - Zero Lag EMA Average|`Zlema`|||
<br>
|**BASIC TRANSFORMS**|**Class Name**|Skender.Stock|TALib.NETCore|
|OC2 - Midpoint price|`.OC2`|CandlePart.OC2|MidPoint|
|HL2 - Median Price|`.HL2`|CandlePart.HL2|MedPrice|
|HLC3 - Typical Price|`.HLC3`|CandlePart.HLC3|TypPrice|
|OHL3 - Mean Price|`.OHL3`|CandlePart.OHL3||
|OHLC4 - Average Price|`.OHLC4`|CandlePart.OHLC4|AvgPrice|
|HLCC4 - Weighted Price|`.HLCC4`||WclPrice|
