# Numerics

> "Price is raw signal. Transform exposes hidden structure. Derivative reveals momentum. Normalization enables comparison. Mathematics is lens, not oracle."

Basic mathematical transforms and utility functions for time series. These building blocks convert raw price data into forms suitable for analysis, comparison, and downstream indicator consumption.

## Implementation Status

| Indicator | Full Name | Status | Description |
| :--- | :--- | :---: | :--- |
| [ACCEL](accel/Accel.md) | Acceleration | ✓ | Momentum change; second derivative of price. |
| BETADIST | Beta Distribution | ≡ | Continuous probability distribution defined on interval [0, 1] |
| BINOMDIST | Binomial Distribution | ≡ | Discrete probability distribution of successes in n independent trials |
| [CHANGE](change/Change.md) | Percentage Change | ✓ | Relative price movement over lookback period. |
| CWT | Continuous Wavelet Transform | ≡ | Analyzes time series data across different frequency scales continuously |
| DIFF | Difference | ≡ | Calculates the simple difference between current and previous values |
| DWT | Discrete Wavelet Transform | ≡ | Analyzes time series data across different frequency scales at discrete intervals |
| EXPDIST | Exponential Distribution | ≡ | Continuous probability distribution describing time between events |
| [EXPTRANS](exptrans/Exptrans.md) | Exponential Transform | ✓ | e^x transform for log-space conversion reversal. |
| FDIST | F-Distribution | ≡ | Continuous probability distribution ratio of two chi-squared distributions |
| FFT | Fast Fourier Transform | ≡ | Efficient algorithm for computing the discrete Fourier transform and its inverse |
| GAMMADIST | Gamma Distribution | ≡ | Continuous probability distribution generalizing exponential and chi-squared |
| [HIGHEST](highest/Highest.md) | Rolling Maximum | ✓ | Maximum value over lookback window. |
| IFFT | Inverse Fast Fourier Transform | ≡ | Efficient algorithm for computing the inverse discrete Fourier transform |
| [JERK](jerk/Jerk.md) | Jerk | ✓ | Rate of acceleration; third derivative of price. |
| [LINEARTRANS](lineartrans/Lineartrans.md) | Linear Transform | ✓ | y = ax + b scaling transformation. |
| LOGNORMDIST | Log-normal Distribution | ≡ | Continuous probability distribution of a variable whose log is normally distributed |
| [LOGTRANS](logtrans/Logtrans.md) | Logarithmic Transform | ✓ | Natural log for percentage-based analysis. |
| [LOWEST](lowest/Lowest.md) | Rolling Minimum | ✓ | Minimum value over lookback window. |
| [MIDPOINT](midpoint/Midpoint.md) | Midrange | ✓ | (Highest + Lowest) / 2 over lookback window. |
| [NORMALIZE](normalize/Normalize.md) | Min-Max Normalization | ✓ | Scale to [0,1] range using rolling min/max. |
| NORMDIST | Normal Distribution | ≡ | Gaussian bell-shaped probability distribution |
| POISSONDIST | Poisson Distribution | ≡ | Discrete probability distribution expressing events in fixed time interval |
| [RELU](relu/Relu.md) | Rectified Linear Unit | ✓ | max(0, x); neural network activation function. |
| [SIGMOID](sigmoid/Sigmoid.md) | Logistic Function | ✓ | 1/(1+e^-x); bounded [0,1] transform. |
| [SLOPE](slope/Slope.md) | Rate of Change | ✓ | First derivative; velocity of price movement. |
| [SQRTTRANS](sqrttrans/Sqrttrans.md) | Square Root Transform | ✓ | Variance-stabilizing transformation. |
| TDIST | Student's t-Distribution | ≡ | Continuous probability distribution when estimating mean of normally distributed population |
| WEIBULLDIST | Weibull Distribution | ≡ | Continuous probability distribution useful in reliability and survival analysis |
