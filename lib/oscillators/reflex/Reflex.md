# REFLEX: Ehlers Reflex Indicator

> *John Ehlers measured how much a filtered price deviates from its own linear extrapolation. The result is a zero-lag oscillator that catches reversals before they happen, because the deviation is largest precisely when the trend is bending.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Oscillator                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Reflex)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [reflex.pine](reflex.pine)                       |

- REFLEX is a zero-lag oscillator that measures the reversal tendency of price by comparing a Super-Smoother-filtered price against a linear extrapol...
- Parameterized by `period`.
- Output range: Varies (see docs).
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

REFLEX is a zero-lag oscillator that measures the reversal tendency of price by comparing a Super-Smoother-filtered price against a linear extrapolation from $N$ bars ago. The filter computes the slope of the filtered series over the lookback window, projects a straight line, and sums the deviations of the actual filtered values from this projected line. The sum is normalized by an exponential RMS estimate to produce values in roughly $\pm \sigma$ scale. Values above 0 indicate uptrend, below 0 indicate downtrend; crossovers signal potential reversals.

## Historical Context

John F. Ehlers published REFLEX in "Reflex: A New Zero-Lag Indicator" (*Technical Analysis of Stocks & Commodities*, February 2020). Ehlers' motivation was to create a cycle-based oscillator that responds to trend reversals with zero lag, unlike traditional oscillators (RSI, stochastic) that inherently lag price due to their smoothing components.

The core idea is that linear extrapolation of a smoothed series will overshoot (undershoot) when the trend is decelerating (accelerating). By measuring the sum of these overshoots, REFLEX detects curvature changes — exactly the inflection points where trends reverse. This is mathematically similar to measuring the second derivative (acceleration), but the linear-extrapolation approach is more numerically stable and naturally adapts to the trend's own slope.

The 2-pole Super Smoother pre-filter (at half the specified period) removes high-frequency noise before the reflex computation, preventing false signals from bar-to-bar price noise. The exponential RMS normalization ensures the output has consistent scale regardless of the instrument's volatility.

## Architecture & Physics

### 1. Super Smoother Pre-Filter

A 2-pole IIR low-pass filter with cutoff at half the specified period:

$$
\text{Filt} = c_1 \cdot \frac{x_t + x_{t-1}}{2} + c_2 \cdot \text{Filt}_{t-1} + c_3 \cdot \text{Filt}_{t-2}
$$

where $a_1 = e^{-\sqrt{2}\pi / (N/2)}$, $c_2 = 2a_1\cos(\sqrt{2}\pi/(N/2))$, $c_3 = -a_1^2$, $c_1 = 1-c_2-c_3$.

### 2. Linear Extrapolation Slope

$$
\text{slope} = \frac{\text{Filt}_{t-N} - \text{Filt}_t}{N}
$$

### 3. Deviation Summation

$$
\text{Sum} = \frac{1}{N}\sum_{i=1}^{N}\left[(\text{Filt}_t + i \cdot \text{slope}) - \text{Filt}_{t-i}\right]
$$

### 4. Exponential RMS Normalization

$$
\text{MS} = 0.04 \cdot \text{Sum}^2 + 0.96 \cdot \text{MS}_{t-1}
$$

$$
\text{REFLEX} = \frac{\text{Sum}}{\sqrt{\text{MS}}}
$$

## Mathematical Foundation

**Super Smoother coefficients (half-period cutoff):**

$$
a_1 = e^{-\sqrt{2}\pi / (N/2)}, \quad c_2 = 2a_1\cos\!\left(\frac{\sqrt{2}\pi}{N/2}\right), \quad c_3 = -a_1^2, \quad c_1 = 1-c_2-c_3
$$

**Deviation from linear trend:**

$$
D_i = (\text{Filt}_t + i \cdot \text{slope}) - \text{Filt}_{t-i}, \quad i = 1, \ldots, N
$$

**Mean deviation:**

$$
\text{Sum} = \frac{1}{N}\sum_{i=1}^{N} D_i
$$

**Interpretation:**

- $\text{Sum} > 0$: filtered price is above its linear extrapolation (upward curvature, potential uptrend)
- $\text{Sum} < 0$: filtered price is below its linear extrapolation (downward curvature, potential downtrend)
- Zero crossings signal inflection points (trend reversals)

**Default parameters:** `period = 20`, `minPeriod = 2`. Output is an oscillator (not overlay).

**Pseudo-code (streaming):**

```
// Super Smoother (2-pole IIR)
filt = c1*(price + price[1])/2 + c2*filt[1] + c3*filt[2]

// Store in circular buffer
buf[head] = filt

// Slope from N-bar-ago to current
slope = (filt_lag_N - filt) / N

// Sum deviations from linear extrapolation
sum = 0
for i = 1 to N:
    sum += (filt + i*slope) - filt[i]
sum /= N

// Normalize by exponential RMS
ms = 0.04 * sum² + 0.96 * ms[1]
return ms > 0 ? sum / sqrt(ms) : 0
```

## Performance Profile

### Operation Count (Streaming Mode)

Reflex (Ehlers) uses a Super Smoother and a slope sum to detect cycles.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| SSF update × 2 (FMA coefficients) | 2 | 4 | 8 |
| Running slope sum (add new + subtract oldest) | 2 | 1 | 2 |
| RMS normalization (variance accumulation) | 4 | 3 | 12 |
| SQRT (RMS divisor) | 1 | 20 | 20 |
| DIV (normalize) | 1 | 15 | 15 |
| **Total** | **10** | — | **~57 cycles** |

SQRT dominates. ~57 cycles per bar.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| SSF IIR passes × 2 | **No** | Recursive 2-pole IIR — sequential |
| Slope sum | Partial | Prefix-sum assist after SSF computed |
| RMS computation | Yes | VFMADD for variance; VSQRTPD |

IIR dependencies block bar-parallel SIMD; RMS computation in batch is vectorizable.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | RMS normalization keeps scale consistent |
| **Timeliness** | 6/10 | SSF half-period lag + slope window |
| **Smoothness** | 9/10 | Super Smoother base + normalized output |
| **Noise Rejection** | 9/10 | SSF rejects frequencies above cutoff; RMS stabilizes amplitude |

## Resources

- Ehlers, J.F. (2020). "Reflex: A New Zero-Lag Indicator." *Technical Analysis of Stocks & Commodities*, February 2020.
- Ehlers, J.F. (2013). *Cycle Analytics for Traders*. Wiley. Chapter 3: Super Smoothers.
