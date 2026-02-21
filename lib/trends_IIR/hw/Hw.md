# HW: Holt-Winters Triple Exponential Smoothing

> "Charles Holt tracked level and slope. Peter Winters added seasonality. This implementation drops seasonality and adds acceleration, the second derivative that tells you when the trend is speeding up or slowing down. Three state variables, three smoothing constants, one second-order Taylor expansion."

HW implements Holt-Winters triple exponential smoothing with level (F), velocity (V), and acceleration (A) components. Instead of the seasonal component from classical Holt-Winters, this variant tracks the second derivative of the time series, enabling it to anticipate curvature in price trends. The output is a second-order Taylor expansion forecast: $F + V + \frac{1}{2}A$, providing smooth trend tracking that naturally leads price during acceleration phases and dampens during deceleration.

## Historical Context

Charles C. Holt developed double exponential smoothing in 1957 (published in 2004 after a 47-year delay), adding a slope component to simple exponential smoothing. Peter R. Winters extended this to triple smoothing in 1960, adding a seasonal component for periodic data.

The acceleration variant used here replaces the seasonal component with a second-order derivative tracker. This approach is common in control theory and tracking filters (e.g., the alpha-beta-gamma filter used in radar tracking), where the goal is to follow a target whose acceleration changes over time. In financial applications, acceleration corresponds to the rate of change of momentum, a signal that often leads price reversals.

The three smoothing constants ($\alpha$, $\beta$, $\gamma$) control the responsiveness of level, velocity, and acceleration respectively. When set to auto-derive from the period ($\alpha = 2/(N+1)$, $\beta = \gamma = 1/N$), the filter provides balanced tracking. Manual overrides allow fine-tuning for specific market regimes.

## Architecture & Physics

### 1. Three-State IIR System

The filter maintains three state variables updated sequentially:

- **F (Level):** Exponentially smoothed estimate of the current value.
- **V (Velocity):** Exponentially smoothed estimate of the first derivative.
- **A (Acceleration):** Exponentially smoothed estimate of the second derivative.

### 2. Update Equations

Each state depends on the previous values of all three states, creating a coupled IIR system:

$$
F_t = \alpha \cdot x_t + (1-\alpha)(F_{t-1} + V_{t-1} + \tfrac{1}{2}A_{t-1})
$$

$$
V_t = \beta(F_t - F_{t-1}) + (1-\beta)(V_{t-1} + A_{t-1})
$$

$$
A_t = \gamma(V_t - V_{t-1}) + (1-\gamma)A_{t-1}
$$

### 3. Taylor Forecast Output

$$
\text{HW}_t = F_t + V_t + \tfrac{1}{2}A_t
$$

## Mathematical Foundation

**State-space formulation:**

$$
\mathbf{s}_t = \begin{bmatrix} F_t \\ V_t \\ A_t \end{bmatrix}
$$

The update equations form the state transition:

$$
F_t = \alpha \cdot x_t + (1-\alpha)\left(F_{t-1} + V_{t-1} + \tfrac{1}{2}A_{t-1}\right)
$$

$$
V_t = \beta\left(F_t - F_{t-1}\right) + (1-\beta)\left(V_{t-1} + A_{t-1}\right)
$$

$$
A_t = \gamma\left(V_t - V_{t-1}\right) + (1-\gamma) A_{t-1}
$$

**Output (second-order Taylor expansion):**

$$
\hat{x}_{t+1} = F_t + V_t + \tfrac{1}{2}A_t
$$

**Smoothing constant relationships:**

| Parameter | Auto-value | Controls |
| :---: | :--- | :--- |
| $\alpha$ | $2/(N+1)$ | Level responsiveness |
| $\beta$ | $1/N$ | Velocity responsiveness |
| $\gamma$ | $1/N$ | Acceleration responsiveness |

**Stability conditions:** All three smoothing constants must be in $(0, 1]$. The system is stable when the eigenvalues of the state transition matrix lie within the unit circle, which is guaranteed for standard parameter ranges.

**Default parameters:** `period = 10`, `alpha = 0` (auto), `beta = 0` (auto), `gamma = 0` (auto), `minPeriod = 1`.

**Pseudo-code (streaming):**

```
alpha = (na > 0) ? na : 2/(period+1)
beta  = (nb > 0) ? nb : 1/period
gamma = (ng > 0) ? ng : 1/period

if first_bar:
    F = source; V = 0; A = 0
    return source

forecast = F + V + 0.5*A
F_new = alpha * source + (1-alpha) * forecast
V_new = beta * (F_new - F) + (1-beta) * (V + A)
A_new = gamma * (V_new - V) + (1-gamma) * A

F = F_new; V = V_new; A = A_new
return F + V + 0.5*A
```

## Resources

- Holt, C.C. (1957/2004). "Forecasting Seasonals and Trends by Exponentially Weighted Moving Averages." *International Journal of Forecasting*, 20(1), 5-10.
- Winters, P.R. (1960). "Forecasting Sales by Exponentially Weighted Moving Averages." *Management Science*, 6(3), 324-342.
- Brown, R.G. (1963). *Smoothing, Forecasting and Prediction of Discrete Time Series*. Prentice-Hall. Chapter 10: Higher-Order Smoothing.
- Benedict, T.R. & Bordner, G.W. (1962). "Synthesis of an Optimal Set of Radar Track-While-Scan Smoothing Equations." *IRE Trans. Automatic Control*, 7(4), 27-32.
