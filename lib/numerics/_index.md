# Numerics

> "Price is raw signal. Transform exposes hidden structure. Derivative reveals momentum. Normalization enables comparison."

Basic mathematical transforms and utility functions for time series. These building blocks convert raw price data into forms suitable for analysis, comparison, and downstream indicator consumption.

| Indicator | Full Name | Description |
| :--- | :--- | :--- |
| [ACCEL](accel/Accel.md) | Acceleration | Momentum change; second derivative of price. |
| BETADIST | Beta Distribution | Beta probability distribution transform. |
| BINOMDIST | Binomial Distribution | Binomial probability distribution transform. |
| [CHANGE](change/Change.md) | Percentage Change | Relative price movement over lookback period. |
| CWT | Continuous Wavelet Transform | Time-frequency decomposition with continuous wavelets. |
| DWT | Discrete Wavelet Transform | Multi-resolution signal decomposition. |
| EXPDIST | Exponential Distribution | Exponential probability distribution transform. |
| [EXPTRANS](exptrans/Exptrans.md) | Exponential Transform | e^x transform for log-space conversion reversal. |
| FDIST | F-Distribution | Fisher-Snedecor probability distribution transform. |
| FFT | Fast Fourier Transform | Frequency-domain decomposition via FFT algorithm. |
| GAMMADIST | Gamma Distribution | Gamma probability distribution transform. |
| [HIGHEST](highest/Highest.md) | Rolling Maximum | Maximum value over lookback window. |
| IFFT | Inverse Fast Fourier Transform | Frequency-to-time domain reconstruction. |
| [JERK](jerk/Jerk.md) | Jerk | Rate of acceleration; third derivative of price. |
| [LINEARTRANS](lineartrans/Lineartrans.md) | Linear Transform | y = ax + b scaling transformation. |
| LOGNORMDIST | Log-normal Distribution | Log-normal probability distribution transform. |
| [LOGTRANS](logtrans/Logtrans.md) | Logarithmic Transform | Natural log for percentage-based analysis. |
| [LOWEST](lowest/Lowest.md) | Rolling Minimum | Minimum value over lookback window. |
| NORMDIST | Normal Distribution | Gaussian probability distribution transform. |
| [NORMALIZE](normalize/Normalize.md) | Min-Max Normalization | Scale to [0,1] range using rolling min/max. |
| POISSONDIST | Poisson Distribution | Poisson probability distribution transform. |
| [RELU](relu/Relu.md) | Rectified Linear Unit | max(0, x); neural network activation function. |
| [SIGMOID](sigmoid/Sigmoid.md) | Logistic Function | 1/(1+e^-x); bounded [0,1] transform. |
| [SLOPE](slope/Slope.md) | First Derivative | First derivative; velocity of price movement. |
| [SQRTTRANS](sqrttrans/Sqrttrans.md) | Square Root Transform | Variance-stabilizing transformation. |
| TDIST | Student's t-Distribution | Student's t probability distribution transform. |
| WEIBULLDIST | Weibull Distribution | Weibull probability distribution transform. |
