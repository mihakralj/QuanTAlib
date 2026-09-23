# HWC: Holt-Winters Channel

| Property | Value |
| --- | --- |
| **Category** | Channel |
| **Inputs** | Close series |
| **Parameters** | `period` (default 20), `multiplier` (default 1.0) |
| **Outputs** | Upper, middle, and lower bands |
| **Warmup** | `period` bars |
| **PineScript** | [hwc.pine](hwc.pine) |

HWC combines Holt-Winters level, trend, and acceleration smoothing into an adaptive channel. The middle band is the Holt-Winters forecast; the outer bands use an EMA-smoothed squared forecast error, so they widen when price repeatedly misses the forecast and contract when the model tracks price closely.

$$F_t = \alpha x_t + (1-\alpha)(F_{t-1}+V_{t-1}+0.5A_{t-1})$$

$$Upper_t = F_t + m\sqrt{filt_t}, \qquad Lower_t = F_t - m\sqrt{filt_t}$$

The period constructor derives $\alpha = 2/(n+1)$, $\beta = 1/n$, and $\gamma = 1/n$. Use the explicit smoothing-factor constructor when the level, trend, and acceleration response must be tuned independently.
