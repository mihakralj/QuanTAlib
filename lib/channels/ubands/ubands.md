# UBANDS: Ehlers Ultimate Bands

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Channel                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period` (default DefaultPeriod), `multiplier` (default DefaultMultiplier)                      |
| **Outputs**      | Multiple series (Upper, Middle, Lower, Width)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [ubands.pine](ubands.pine)                       |

- Ehlers Ultimate Bands replace the conventional SMA foundation of Bollinger Bands with the Ultrasmooth Filter (USF), a 2-pole IIR filter with zero o...
- Parameterized by `period` (default defaultperiod), `multiplier` (default defaultmultiplier).
- Output range: Tracks input.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

Ehlers Ultimate Bands replace the conventional SMA foundation of Bollinger Bands with the Ultrasmooth Filter (USF), a 2-pole IIR filter with zero overshoot and minimal lag. Band width is determined by the RMS (Root Mean Square) of residuals between price and the smoothed centerline, providing a mathematically rigorous deviation measure that makes no assumptions about the distribution of returns. The USF is a recursive filter requiring O(1) computation per bar, while the RMS calculation scans the lookback window at O(n) per bar.

## Historical Context

John F. Ehlers introduced Ultimate Bands in 2024 as part of his ongoing research into digital signal processing applied to financial markets. Ehlers' career spans decades of applying engineering concepts (particularly from electrical and mechanical engineering) to trading indicator design.

The key insight behind Ultimate Bands: traditional standard deviation measures assume stationarity and normality, assumptions that financial time series routinely violate. By measuring the RMS of actual residuals (the difference between price and the USF-smoothed value), the bands adapt to whatever distribution the market presents. RMS is the natural measure of dispersion around zero; since the residuals are already centered on the smooth, RMS is the mathematically correct choice.

The Ultrasmooth Filter itself is derived from Ehlers' work on maximally flat filters. Its 2-pole IIR design achieves zero overshoot (unlike many smoothing filters that ring on sharp price moves), minimal lag compared to SMA of equivalent smoothness, and excellent high-frequency noise rejection with 12 dB/octave rolloff.

## Architecture & Physics

### 1. Ultrasmooth Filter Coefficients

The USF coefficients are derived from the period parameter $n$:

$$
\text{arg} = \frac{\sqrt{2}\,\pi}{n}
$$

$$
c_2 = 2\,e^{-\text{arg}} \cos(\text{arg})
$$

$$
c_3 = -e^{-2\,\text{arg}}
$$

$$
c_1 = \frac{1 + c_2 - c_3}{4}
$$

### 2. USF Recursion (Middle Band)

The filter processes input prices $P_t$ through a 2-pole IIR structure:

$$
\text{USF}_t = (1 - c_1)\,P_t + (2c_1 - c_2)\,P_{t-1} - (c_1 + c_3)\,P_{t-2} + c_2\,\text{USF}_{t-1} + c_3\,\text{USF}_{t-2}
$$

During the first few bars (before sufficient history exists), the filter initializes directly to the input value.

### 3. Residuals and RMS

The residual captures the high-frequency component rejected by the filter:

$$
r_t = P_t - \text{USF}_t
$$

The RMS over the lookback window:

$$
\text{RMS}_t = \sqrt{\frac{1}{n} \sum_{i=0}^{n-1} r_{t-i}^2}
$$

### 4. Band Construction

$$
U_t = \text{USF}_t + k \cdot \text{RMS}_t
$$

$$
L_t = \text{USF}_t - k \cdot \text{RMS}_t
$$

where $k$ is the multiplier (default 1.0). Note the default is 1.0 (not 2.0 as in Bollinger Bands), because RMS of residuals from the USF is typically larger than population standard deviation from an SMA.

### 5. Complexity

The USF recursion is $O(1)$ per bar (four multiply-adds). The RMS calculation scans $n$ residuals per bar, yielding $O(n)$ total. Memory: two scalar states for USF history plus a buffer of $n$ squared residuals.

## Mathematical Foundation

### Parameters

| Symbol | Name | Default | Constraint | Description |
|--------|------|---------|------------|-------------|
| $n$ | period | 20 | $\geq 1$ | USF smoothing and RMS lookback period |
| $k$ | multiplier | 1.0 | $> 0$ | RMS multiplier for band width |

### USF Transfer Function

In the z-domain:

$$
H(z) = \frac{(1 - c_1) + (2c_1 - c_2)\,z^{-1} - (c_1 + c_3)\,z^{-2}}{1 - c_2\,z^{-1} - c_3\,z^{-2}}
$$

Cutoff frequency: approximately $f_c \approx 1/(2\pi n)$ cycles per bar. Rolloff: 12 dB/octave.

### Pseudo-code

```
function ubands(source[], period, multiplier):
    // precompute USF coefficients
    arg = sqrt(2) * pi / period
    c2 = 2 * exp(-arg) * cos(arg)
    c3 = -exp(-2 * arg)
    c1 = (1 + c2 - c3) / 4

    usf_prev1 = NaN, usf_prev2 = NaN

    for each bar t:
        s0 = source[t]
        s1 = source[t-1]  // or s0 if unavailable
        s2 = source[t-2]  // or s1 if unavailable

        if usf not initialized:
            usf = s0
        else:
            usf = (1 - c1)*s0 + (2*c1 - c2)*s1
                  - (c1 + c3)*s2 + c2*usf_prev1 + c3*usf_prev2

        usf_prev2 = usf_prev1
        usf_prev1 = usf

        // RMS of residuals over window
        sum_sq = 0, count = 0
        for i = 0 to period-1:
            r = source[t-i] - usf_at[t-i]  // residual at bar t-i
            if r is valid:
                sum_sq += r * r
                count += 1

        rms = count > 0 ? sqrt(sum_sq / count) : 0
        upper = usf + multiplier * rms
        lower = usf - multiplier * rms

        emit (upper, usf, lower)
```

### RMS vs Standard Deviation

Standard deviation measures dispersion around the mean: $\sigma = \sqrt{E[(X - \mu)^2]}$. RMS measures dispersion around zero: $\text{RMS} = \sqrt{E[X^2]}$. Since the residuals $r_t = P_t - \text{USF}_t$ are already deviations from the smooth centerline, RMS is the correct measure. When the mean of residuals is zero (as it approximately is for a well-fitted filter), RMS equals standard deviation.

### Output Interpretation

| Output | Interpretation |
|--------|---------------|
| USF slope positive | Underlying trend is up |
| Bands widening | Residual volatility increasing |
| Bands narrowing | Residual volatility compressing |
| Price at upper band | High-frequency component is large positive |
| Price at lower band | High-frequency component is large negative |

## Performance Profile

### Operation Count (Streaming Mode)

UBANDS combines an $O(1)$ USF IIR recursion (center line) with an $O(n)$ RMS scan (band width):

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| MUL + ADD (USF coefficients, 4 terms) | 4 | 4 | 16 |
| ADD (USF: 2 feedback + 3 feedforward) | 5 | 1 | 5 |
| SUB (residual = source - USF) | 1 | 1 | 1 |
| MUL (residual² for RMS buffer) | 1 | 3 | 3 |
| ADD (sum of squared residuals, $n$) | $n$ | 1 | $n$ |
| DIV (sumSq / count) | 1 | 15 | 15 |
| SQRT (RMS) | 1 | 20 | 20 |
| MUL (k × RMS) | 1 | 3 | 3 |
| ADD/SUB (USF ± width) | 2 | 1 | 2 |
| **Total** | **~$n + 16$** | — | **~$n + 65$ cycles** |

For period 20: ~85 cycles/bar. The USF recursion is fast ($\sim$21 cycles); the RMS window scan at $O(n)$ dominates.

### Batch Mode (SIMD Analysis)

The USF is recursive (IIR dependency). The RMS scan over squared residuals is vectorizable:

| Optimization | Benefit |
| :--- | :--- |
| USF 2-pole IIR | Sequential; 5 multiply-adds per bar |
| RMS accumulation (sum of r²) | Vectorizable with `Vector.Multiply` + horizontal sum |
| Band arithmetic | Vectorizable in a post-pass |

## Resources

- Ehlers, J. F. (2024). "Ultimate Bands." *Technical Analysis of Stocks & Commodities*.
- Ehlers, J. F. (2013). *Cycle Analytics for Traders*. Wiley.
- Ehlers, J. F. (2001). *Rocket Science for Traders*. Wiley.
