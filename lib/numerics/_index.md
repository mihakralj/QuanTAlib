# Numerics

Basic mathematical transforms and utility functions for time series. These building blocks convert raw price data into forms suitable for analysis, comparison, and downstream indicator consumption.

| Indicator | Full Name | Description |
| :--- | :--- | :--- |
| [ACCEL](accel/Accel.md) | Acceleration | Momentum change; second derivative of price. |
| [BETADIST](betadist/Betadist.md) | Beta Distribution | Beta probability distribution transform. |
| [BINOMDIST](binomdist/Binomdist.md) | Binomial Distribution | Binomial probability distribution transform. |
| [CHANGE](change/Change.md) | Percentage Change | Relative price movement over lookback period. |
| [DECAY](decay/Decay.md) | Linear Decay | Peak envelope with linear degradation; max(input, prev − 1/period). |
| [CWT](cwt/Cwt.md) | Continuous Wavelet Transform | Time-frequency decomposition with continuous wavelets. |
| [DWT](dwt/Dwt.md) | Discrete Wavelet Transform | À trous Haar stationary DWT; multi-resolution approximation + detail decomposition. |
| [EDECAY](edecay/Edecay.md) | Exponential Decay | Peak envelope with exponential degradation; max(input, prev × (period−1)/period). |
| [EXPDIST](expdist/Expdist.md) | Exponential Distribution | Exponential probability distribution transform. |
| [EXPTRANS](exptrans/Exptrans.md) | Exponential Transform | e^x transform for log-space conversion reversal. |
| [FDIST](fdist/Fdist.md) | F-Distribution | Fisher-Snedecor probability distribution transform. |
| [FFT](fft/Fft.md) | Fast Fourier Transform | Frequency-domain decomposition via FFT algorithm. |
| [GAMMADIST](gammadist/Gammadist.md) | Gamma Distribution | Gamma probability distribution transform. |
| [HIGHEST](highest/Highest.md) | Rolling Maximum | Maximum value over lookback window. |
| [MAXINDEX](maxindex/Maxindex.md) | Rolling Maximum Index | Position of maximum value in rolling window. |
| [IFFT](ifft/Ifft.md) | Inverse Fast Fourier Transform | Frequency-to-time domain reconstruction. |
| [JERK](jerk/Jerk.md) | Jerk | Rate of acceleration; third derivative of price. |
| [LINEARTRANS](lineartrans/Lineartrans.md) | Linear Transform | y = ax + b scaling transformation. |
| [LOGNORMDIST](lognormdist/Lognormdist.md) | Log-normal Distribution | Log-normal probability distribution transform. |
| [LOGTRANS](logtrans/Logtrans.md) | Logarithmic Transform | Natural log for percentage-based analysis. |
| [LOWEST](lowest/Lowest.md) | Rolling Minimum | Minimum value over lookback window. |
| [MININDEX](minindex/Minindex.md) | Rolling Minimum Index | Position of minimum value in rolling window. |
| [NORMDIST](normdist/Normdist.md) | Normal Distribution | Gaussian probability distribution transform. |
| [NORMALIZE](normalize/Normalize.md) | Min-Max Normalization | Scale to [0,1] range using rolling min/max. |
| [POISSONDIST](poissondist/Poissondist.md) | Poisson Distribution | Poisson probability distribution transform. |
| [RELU](relu/Relu.md) | Rectified Linear Unit | max(0, x); neural network activation function. |
| [SIGMOID](sigmoid/Sigmoid.md) | Logistic Function | 1/(1+e^-x); bounded [0,1] transform. |
| [SLOPE](slope/Slope.md) | First Derivative | First derivative; velocity of price movement. |
| [SQRTTRANS](sqrttrans/Sqrttrans.md) | Square Root Transform | Variance-stabilizing transformation. |
| [TDIST](tdist/Tdist.md) | Student's t-Distribution | Student's t probability distribution transform. |
| [WEIBULLDIST](weibulldist/Weibulldist.md) | Weibull Distribution | Weibull probability distribution transform. |
