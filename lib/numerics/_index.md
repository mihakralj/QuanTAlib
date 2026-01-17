# Numerics

> "Price is raw signal. Transform exposes hidden structure. Derivative reveals momentum. Normalization enables comparison. Mathematics is lens, not oracle."

Basic mathematical transforms and utility functions for time series. These building blocks convert raw price data into forms suitable for analysis, comparison, and downstream indicator consumption.

## Implementation Status

| Indicator | Full Name | Status | Description |
| :--- | :--- | :---: | :--- |
| ACCEL | Acceleration | =Ë | Momentum change; second derivative of price. |
| ATAN2 | Two-Argument Arctangent | =Ë | Angle calculation preserving quadrant information. |
| CHANGE | Percentage Change | =Ë | Relative price movement over lookback period. |
| EXP | Exponential Transform | =Ë | e^x transform for log-space conversion reversal. |
| HIGHEST | Rolling Maximum | =Ë | Maximum value over lookback window. |
| HL2 | Midpoint Price | =Ë | (High + Low) / 2; typical price without close. |
| HLC3 | Typical Price | =Ë | (High + Low + Close) / 3; standard typical price. |
| HLCC4 | Weighted Close | =Ë | (High + Low + Close + Close) / 4; close-weighted typical. |
| JOLT | Jolt | =Ë | Rate of acceleration; third derivative of price. |
| LINEAR | Linear Transform | =Ë | y = ax + b scaling transformation. |
| LOG | Logarithmic Transform | =Ë | Natural log for percentage-based analysis. |
| LOWEST | Rolling Minimum | =Ë | Minimum value over lookback window. |
| MIDPOINT | Midrange | =Ë | (Highest + Lowest) / 2 over lookback window. |
| NORMALIZE | Min-Max Normalization | =Ë | Scale to [0,1] range using rolling min/max. |
| OC2 | Open-Close Midpoint | =Ë | (Open + Close) / 2; body center price. |
| OHL3 | Three-Point Average | =Ë | (Open + High + Low) / 3; excludes close. |
| OHLC4 | Four-Point Average | =Ë | (Open + High + Low + Close) / 4; equal weights. |
| RELU | Rectified Linear Unit | =Ë | max(0, x); neural network activation function. |
| SIGMOID | Logistic Function | =Ë | 1/(1+e^-x); bounded [0,1] transform. |
| SLOPE | Rate of Change | =Ë | First derivative; velocity of price movement. |
| SQRT | Square Root Transform | =Ë | Variance-stabilizing transformation. |
| STANDARDIZE | Z-Score | =Ë | (x - ¼) / Ã; standard deviation units. |
| TANH | Hyperbolic Tangent | =Ë | Bounded [-1,1] transform; smoother than sigmoid. |

## Selection Guide

**For price averaging:** HL2, HLC3, HLCC4, OC2, OHL3, OHLC4 provide different weighting schemes. HLC3 (typical price) is most common. HLCC4 emphasizes close price.

**For momentum analysis:** SLOPE provides first derivative (velocity). ACCEL provides second derivative (acceleration). JOLT provides third derivative (rate of acceleration change).

**For range finding:** HIGHEST/LOWEST find extremes. MIDPOINT averages extremes. NORMALIZE scales between detected bounds.

**For neural network inputs:** SIGMOID, TANH, RELU provide bounded activation functions. STANDARDIZE centers data with unit variance.

**For log-space analysis:** LOG converts multiplicative relationships to additive. EXP reverses log transformation. SQRT stabilizes variance.

## Price Composite Types

| Function | Formula | Use Case | Close Weight |
| :--- | :--- | :--- | :---: |
| HL2 | (H + L) / 2 | Range midpoint | 0% |
| HLC3 | (H + L + C) / 3 | Typical price (standard) | 33% |
| HLCC4 | (H + L + 2C) / 4 | Weighted close | 50% |
| OC2 | (O + C) / 2 | Body center | 50% |
| OHL3 | (O + H + L) / 3 | Session bias | 0% |
| OHLC4 | (O + H + L + C) / 4 | Equal weight | 25% |

## Derivative Hierarchy

| Order | Function | Measures | Physical Analog |
| :---: | :--- | :--- | :--- |
| 0 | Price | Position | Distance |
| 1 | SLOPE | Velocity | Speed |
| 2 | ACCEL | Acceleration | Force |
| 3 | JOLT | Jolt | Rate of force change |

Higher derivatives amplify noise. Third derivative (JOLT) requires significant smoothing for practical use.

## Normalization Methods

| Method | Formula | Range | Properties |
| :--- | :--- | :---: | :--- |
| Min-Max | (x - min) / (max - min) | [0, 1] | Preserves relative distances |
| Z-Score | (x - ¼) / Ã | Unbounded | Centers at 0, unit variance |
| Sigmoid | 1 / (1 + e^-x) | (0, 1) | Smooth, asymptotic bounds |
| Tanh | (e^x - e^-x) / (e^x + e^-x) | (-1, 1) | Centered, symmetric |
| ReLU | max(0, x) | [0, ) | Sparse activation |

## Transform Applications

| Transform | Input Domain | Output Range | Primary Use |
| :--- | :--- | :--- | :--- |
| LOG | x > 0 | (-, ) | Percentage analysis, volatility |
| EXP | (-, ) | (0, ) | Reverse log transform |
| SQRT | x e 0 | [0, ) | Variance stabilization |
| LINEAR | (-, ) | (-, ) | Scaling, offset adjustment |
| ATAN2 | (y, x) | (-À, À] | Angle with quadrant |