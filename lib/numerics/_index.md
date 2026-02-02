# Numerics

> "Price is raw signal. Transform exposes hidden structure. Derivative reveals momentum. Normalization enables comparison."

Basic mathematical transforms and utility functions for time series. These building blocks convert raw price data into forms suitable for analysis, comparison, and downstream indicator consumption.

| Indicator | Full Name | Description |
| :--- | :--- | :--- |
| [ACCEL](/lib/numerics/accel/Accel.md) | Acceleration | Momentum change; second derivative of price. |
| [CHANGE](/lib/numerics/change/Change.md) | Percentage Change | Relative price movement over lookback period. |
| [EXPTRANS](/lib/numerics/exptrans/Exptrans.md) | Exponential Transform | e^x transform for log-space conversion reversal. |
| [HIGHEST](/lib/numerics/highest/Highest.md) | Rolling Maximum | Maximum value over lookback window. |
| [JERK](/lib/numerics/jerk/Jerk.md) | Jerk | Rate of acceleration; third derivative of price. |
| [LINEARTRANS](/lib/numerics/lineartrans/Lineartrans.md) | Linear Transform | y = ax + b scaling transformation. |
| [LOGTRANS](/lib/numerics/logtrans/Logtrans.md) | Logarithmic Transform | Natural log for percentage-based analysis. |
| [LOWEST](/lib/numerics/lowest/Lowest.md) | Rolling Minimum | Minimum value over lookback window. |
| [MIDPOINT](/lib/numerics/midpoint/Midpoint.md) | Midrange | (Highest + Lowest) / 2 over lookback window. |
| [NORMALIZE](/lib/numerics/normalize/Normalize.md) | Min-Max Normalization | Scale to [0,1] range using rolling min/max. |
| [RELU](/lib/numerics/relu/Relu.md) | Rectified Linear Unit | max(0, x); neural network activation function. |
| [SIGMOID](/lib/numerics/sigmoid/Sigmoid.md) | Logistic Function | 1/(1+e^-x); bounded [0,1] transform. |
| [SLOPE](/lib/numerics/slope/Slope.md) | Rate of Change | First derivative; velocity of price movement. |
| [SQRTTRANS](/lib/numerics/sqrttrans/Sqrttrans.md) | Square Root Transform | Variance-stabilizing transformation. |
| [STANDARDIZE](/lib/numerics/standardize/Standardize.md) | Z-Score Normalization | (x - mean) / stddev; zero-mean unit-variance transform. |
