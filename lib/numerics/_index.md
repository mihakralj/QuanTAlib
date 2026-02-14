# Numerics

> "Price is raw signal. Transform exposes hidden structure. Derivative reveals momentum. Normalization enables comparison."

Basic mathematical transforms and utility functions for time series. These building blocks convert raw price data into forms suitable for analysis, comparison, and downstream indicator consumption.

| Indicator | Full Name | Description |
| :--- | :--- | :--- |
| [ACCEL](accel/Accel.md) | Acceleration | Momentum change; second derivative of price. |
| [CHANGE](change/Change.md) | Percentage Change | Relative price movement over lookback period. |
| [EXPTRANS](exptrans/Exptrans.md) | Exponential Transform | e^x transform for log-space conversion reversal. |
| [HIGHEST](highest/Highest.md) | Rolling Maximum | Maximum value over lookback window. |
| [JERK](jerk/Jerk.md) | Jerk | Rate of acceleration; third derivative of price. |
| [LINEARTRANS](lineartrans/Lineartrans.md) | Linear Transform | y = ax + b scaling transformation. |
| [LOGTRANS](logtrans/Logtrans.md) | Logarithmic Transform | Natural log for percentage-based analysis. |
| [LOWEST](lowest/Lowest.md) | Rolling Minimum | Minimum value over lookback window. |
| [MIDPOINT](midpoint/Midpoint.md) | Midrange | (Highest + Lowest) / 2 over lookback window. |
| [NORMALIZE](normalize/Normalize.md) | Min-Max Normalization | Scale to [0,1] range using rolling min/max. |
| [RELU](relu/Relu.md) | Rectified Linear Unit | max(0, x); neural network activation function. |
| [SIGMOID](sigmoid/Sigmoid.md) | Logistic Function | 1/(1+e^-x); bounded [0,1] transform. |
| [SLOPE](slope/Slope.md) | Rate of Change | First derivative; velocity of price movement. |
| [SQRTTRANS](sqrttrans/Sqrttrans.md) | Square Root Transform | Variance-stabilizing transformation. |
| [STANDARDIZE](standardize/Standardize.md) | Z-Score Normalization | (x - mean) / stddev; zero-mean unit-variance transform. |
