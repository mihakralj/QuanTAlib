# LTMA: Linear Trend Moving Average

> "Estimate the level. Estimate the slope. Project forward. It is the same trick radar operators use to track aircraft, applied to price data. LTMA extrapolates the EMA's implicit linear trend by a full period into the future."

LTMA uses dual cascaded EMAs to estimate both the level and the instantaneous slope of the price series, then extrapolates the linear trend forward by the full period length. Unlike DEMA (which cancels first-order lag algebraically), LTMA explicitly estimates the slope from the EMA difference and projects it: $\text{LTMA} = \text{EMA}_1 + \text{slope} \times N$, where $\text{slope} = \text{EMA}_1 - \text{EMA}_2$. This produces a predictive moving average with zero steady-state error on linear trends, at the cost of significant overshoot on reversals.

## Historical Context

Linear trend extrapolation from dual exponential smoothing is a core technique in time-series forecasting, originating with Brown's linear exponential smoothing (1963) and Holt's two-parameter method (1957). The application to technical analysis leverages the same principle: if you can estimate both where price is and how fast it is moving, you can project where it will be.

LTMA differs from Holt's method in that both EMAs share the same smoothing constant $\alpha = 2/(N+1)$, simplifying the parameter space to a single period. The slope estimate $\text{EMA}_1 - \text{EMA}_2$ approximates the first derivative of the exponentially smoothed series, and projecting by $N$ bars creates an aggressive lead that compensates for the EMA's inherent lag.

The projection distance of $N$ bars (the full period) makes LTMA more aggressive than DEMA or TEMA. While DEMA effectively projects by approximately $N/2$ bars via its $2 \cdot \text{EMA}_1 - \text{EMA}_2$ formula, LTMA's full-period projection creates a stronger lead that can anticipate trend continuation but overshoots badly on sharp reversals.

## Architecture & Physics

### 1. Dual Cascaded EMAs

Two EMAs with shared $\alpha = 2/(N+1)$:

- **EMA1:** Standard EMA of source.
- **EMA2:** EMA of EMA1.

### 2. Slope Estimation

$$
\text{slope} = \text{EMA}_1 - \text{EMA}_2
$$

The difference between single and double-smoothed EMAs approximates the first derivative scaled by a factor related to $\alpha$.

### 3. Linear Extrapolation

$$
\text{LTMA} = \text{EMA}_1 + \text{slope} \times N
$$

### 4. Warmup Compensation

Both EMAs use the exponential warmup compensator for valid output from bar 1.

## Mathematical Foundation

With $\alpha = 2/(N+1)$, $\beta = 1 - \alpha$:

$$
\text{EMA}_1[t] = \alpha \cdot x_t + \beta \cdot \text{EMA}_1[t-1]
$$

$$
\text{EMA}_2[t] = \alpha \cdot \text{EMA}_1[t] + \beta \cdot \text{EMA}_2[t-1]
$$

**Slope and output:**

$$
\text{slope}_t = \text{EMA}_1[t] - \text{EMA}_2[t]
$$

$$
\text{LTMA}[t] = \text{EMA}_1[t] + N \cdot \text{slope}_t
$$

$$
= (1+N) \cdot \text{EMA}_1[t] - N \cdot \text{EMA}_2[t]
$$

**Comparison with DEMA/GDEMA:** LTMA is equivalent to GDEMA with $v = N$:

| Method | Formula | Projection |
| :--- | :--- | :---: |
| EMA | $\text{EMA}_1$ | 0 bars |
| DEMA | $2\text{EMA}_1 - \text{EMA}_2$ | ~$N/2$ bars |
| LTMA | $(1+N)\text{EMA}_1 - N\text{EMA}_2$ | $N$ bars |

**Steady-state error on linear trend:** Zero. If $x_t = a + bt$, then $\text{LTMA}_t = a + bt$ exactly (after transient).

**Default parameters:** `period = 14`, `minPeriod = 1`.

**Pseudo-code (streaming):**

```
alpha = 2/(period+1); beta = 1-alpha

ema1 = alpha*(src - ema1) + ema1
ema2 = alpha*(ema1 - ema2) + ema2

if warmup:
    e *= beta; c = 1/(1-e)
    comp1 = c*ema1; comp2 = c*ema2
    slope = comp1 - comp2
    result = comp1 + slope * period
else:
    slope = ema1 - ema2
    result = ema1 + slope * period
```

## Resources

- Holt, C.C. (1957/2004). "Forecasting Seasonals and Trends by Exponentially Weighted Moving Averages." *International Journal of Forecasting*, 20(1), 5-10.
- Brown, R.G. (1963). *Smoothing, Forecasting and Prediction of Discrete Time Series*. Prentice-Hall. Chapter 5: Linear Exponential Smoothing.
- Gardner, E.S. (1985). "Exponential Smoothing: The State of the Art." *Journal of Forecasting*, 4(1), 1-28.
