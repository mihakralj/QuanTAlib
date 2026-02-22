# NLMA: Non-Lag Moving Average

> "Igorad at TrendLaboratory borrowed a trick from digital filter design: use a damped cosine kernel with negative weights in the mid-section to cancel the lag that positive-only kernels always produce. The result looks like an FIR filter but behaves like nothing else."

NLMA uses a damped cosine (fading sinusoid) kernel where the weight at position $i$ is $w(i) = \cos(2\pi i/N) \times (1 - i/N)$. The cosine oscillation creates negative weights in the mid-section of the kernel, which subtract lagged price components and reduce the filter's group delay. The linear decay envelope $(1 - i/N)$ ensures the kernel tapers to zero at the window edge. Normalization by the signed weight sum preserves DC gain of 1.0. The result is a moving average with substantially less lag than an SMA of the same period.

## Historical Context

NLMA was developed by Igorad (username on trading forums) at TrendLaboratory, inspired by the FATL/SATL digital filter coefficient sets published by Finware. The FATL (Fast Adaptive Trend Line) and SATL (Slow Adaptive Trend Line) filters use fixed FIR coefficients derived from optimal filter design, with negative weights that provide lag cancellation. Igorad's contribution was to replace the fixed coefficients with a parametric damped-cosine formula, allowing the filter to be configured for any period.

The damped cosine kernel has a natural interpretation in signal processing: it is the impulse response of a damped resonator at frequency $f = 1/N$. The resonance frequency matches the filter period, meaning the negative portion of the cosine systematically cancels the frequency component that causes the most lag. This is analogous to how DEMA uses $2\text{EMA} - \text{EMA}(\text{EMA})$ to cancel lag, but NLMA achieves it through the kernel shape itself rather than algebraic subtraction.

The presence of negative weights means NLMA is not a convex combination of input prices. The output can exceed the input range (overshoot), similar to DEMA and HMA.

## Architecture & Physics

### 1. Damped Cosine Weight Function

For each lag position $j = 0, 1, \ldots, N-1$ (where $j=0$ is newest):

$$
w(j) = \cos\!\left(\frac{2\pi j}{N}\right) \times \left(1 - \frac{j}{N}\right)
$$

### 2. Signed-Sum Normalization

The weight sum includes both positive and negative weights:

$$
\text{NLMA}_t = \frac{\sum_{j=0}^{N-1} w(j) \cdot x_{t-j}}{\sum_{j=0}^{N-1} w(j)}
$$

Because some weights are negative, $\sum w < \sum |w|$, which amplifies the effective contribution of recent (positive-weighted) bars.

### 3. Adaptive Warmup

During the warmup period ($\text{count} < N$), the kernel is recomputed with the effective period $p = \min(\text{bar\_count}, N)$, providing valid output from bar 1.

## Mathematical Foundation

The NLMA kernel function:

$$
w[j] = \cos\!\left(\frac{2\pi j}{N}\right) \cdot \left(1 - \frac{j}{N}\right), \quad j = 0, 1, \ldots, N-1
$$

**Weight structure analysis:**

| Region | Range of $j$ | $\cos$ sign | Weight sign | Effect |
| :--- | :---: | :---: | :---: | :--- |
| Recent | $0 \leq j < N/4$ | + | + | Track price |
| Mid-lag | $N/4 \leq j < 3N/4$ | - | - | Cancel lag |
| Old | $3N/4 \leq j < N$ | + | + | Mild stabilization |

The negative mid-section weights are the lag-cancellation mechanism. They subtract the delayed price component that would otherwise pull the output backward.

**Frequency response:** The damped cosine kernel creates a bandpass notch near $f = 1/N$, suppressing the frequency most responsible for lag while passing lower frequencies (trend) and attenuating higher frequencies (noise).

**DC gain normalization:**

$$
H(0) = \frac{\sum w[j]}{\sum w[j]} = 1
$$

**Default parameters:** `period = 14`, `minPeriod = 1`.

**Pseudo-code (streaming):**

```
p = min(bar_count, period)

// Compute weights (recompute during warmup)
for j = 0 to p-1:
    angle = 2π * j / p
    decay = 1 - j/p
    w[j] = cos(angle) * decay

// Weighted sum with signed normalization
sum_wv = 0; sum_w = 0
for i = 0 to p-1:
    if not NaN(source[i]):
        sum_wv += source[i] * w[i]
        sum_w  += w[i]

return sum_w != 0 ? sum_wv / sum_w : source
```

## Resources

- Igorad / TrendLaboratory. "NonLagMA" indicator documentation. Available on TradingView and various trading forums.
- Finware Ltd. "FATL/SATL Digital Filters." Technical documentation for FinWare trading software.
- Ehlers, J.F. (2001). *Rocket Science for Traders*. Wiley. Chapter 4: FIR Filters with Negative Weights.
