# Numerics

> "Price is raw signal. Transform exposes hidden structure. Derivative reveals momentum. Normalization enables comparison. Mathematics is lens, not oracle."

Basic mathematical transforms and utility functions for time series. These building blocks convert raw price data into forms suitable for analysis, comparison, and downstream indicator consumption.

## Implementation Status

| Indicator | Full Name | Status | Description |
| :--- | :--- | :---: | :--- |
| [ACCEL](accel/Accel.md) | Acceleration | ✓ | Momentum change; second derivative of price. |
| [CHANGE](change/Change.md) | Percentage Change | ✓ | Relative price movement over lookback period. |
| [EXPTRANS](exptrans/Exptrans.md) | Exponential Transform | ✓ | e^x transform for log-space conversion reversal. |
| [HIGHEST](highest/Highest.md) | Rolling Maximum | ✓ | Maximum value over lookback window. |
| [JERK](jerk/Jerk.md) | Jerk | ✓ | Rate of acceleration; third derivative of price. |
| [LINEARTRANS](lineartrans/Lineartrans.md) | Linear Transform | ✓ | y = ax + b scaling transformation. |
| [LOGTRANS](logtrans/Logtrans.md) | Logarithmic Transform | ✓ | Natural log for percentage-based analysis. |
| [LOWEST](lowest/Lowest.md) | Rolling Minimum | ✓ | Minimum value over lookback window. |
| [MIDPOINT](midpoint/Midpoint.md) | Midrange | ✓ | (Highest + Lowest) / 2 over lookback window. |
| [NORMALIZE](normalize/Normalize.md) | Min-Max Normalization | ✓ | Scale to [0,1] range using rolling min/max. |
| [RELU](relu/Relu.md) | Rectified Linear Unit | ✓ | max(0, x); neural network activation function. |
| [SIGMOID](sigmoid/Sigmoid.md) | Logistic Function | ✓ | 1/(1+e^-x); bounded [0,1] transform. |
| [SLOPE](slope/Slope.md) | Rate of Change | ✓ | First derivative; velocity of price movement. |
| SMOOTHNESS | Smoothness Score | ≡ | Curvature continuity via second derivative analysis; 0-1 normalized. |
| [SQRTTRANS](sqrttrans/Sqrttrans.md) | Square Root Transform | ✓ | Variance-stabilizing transformation. |
| STANDARDIZE | Z-Score | ≡ | (x - μ) / σ; standard deviation units. |

## Selection Guide

**For price averaging:** HL2, HLC3, HLCC4, OC2, OHL3, OHLC4 provide different weighting schemes. HLC3 (typical price) is most common. HLCC4 emphasizes close price.

**For momentum analysis:** SLOPE provides first derivative (velocity). ACCEL provides second derivative (acceleration). JERK provides third derivative (rate of acceleration change).

**For range finding:** HIGHEST/LOWEST find extremes. MIDPOINT averages extremes. NORMALIZE scales between detected bounds.

**For neural network inputs:** SIGMOID, RELU provide bounded activation functions. STANDARDIZE centers data with unit variance.

**For log-space analysis:** LOGTRANS converts multiplicative relationships to additive. EXPTRANS reverses log transformation. SQRTTRANS stabilizes variance.

## Normalization Methods

| Method | Formula | Range | Properties |
| :--- | :--- | :---: | :--- |
| Min-Max | (x - min) / (max - min) | [0, 1] | Preserves relative distances |
| Z-Score | (x - μ) / σ | Unbounded | Centers at 0, unit variance |
| Sigmoid | 1 / (1 + e^-x) | (0, 1) | Smooth, asymptotic bounds |
| Tanh | (e^x - e^-x) / (e^x + e^-x) | (-1, 1) | Centered, symmetric |
| ReLU | max(0, x) | [0, ∞) | Sparse activation |

## Transform Applications

| Transform | Input Domain | Output Range | Primary Use |
| :--- | :--- | :--- | :--- |
| LOGTRANS | x > 0 | (-∞, ∞) | Percentage analysis, volatility |
| EXPTRANS | (-∞, ∞) | (0, ∞) | Reverse log transform |
| SQRTTRANS | x ≥ 0 | [0, ∞) | Variance stabilization |
| LINEARTRANS | (-∞, ∞) | (-∞, ∞) | Scaling, offset adjustment |
| ATAN2 | (y, x) | (-π, π] | Angle with quadrant |