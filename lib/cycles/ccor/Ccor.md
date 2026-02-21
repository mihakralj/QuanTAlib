# CCOR: Ehlers Correlation Cycle

CCOR extracts cycle phase by computing Pearson correlation of a price window against cosine (Real) and negative-sine (Imaginary) reference waves of a presumed fixed period, converting the resulting phasor to an angle with a monotonic constraint, and classifying the market state as trending or cycling based on the angle rate of change. Unlike Hilbert Transform approaches that rely on analytic signal construction, CCOR uses the statistical machinery of correlation to measure how well price "fits" each quadrature component, yielding bounded $[-1, +1]$ outputs that double as confidence measures. The method was introduced to address the instability of Hilbert-based phasors during trend-dominated regimes.

## Historical Context

John F. Ehlers published "Correlation As A Cycle Indicator" in *Technical Analysis of Stocks & Commodities* (June 2020), presenting CCOR as a more robust alternative to his earlier Hilbert Transform phasor (circa 2001). The Hilbert approach suffers from amplitude sensitivity and poor convergence during strong trends because it treats all price action as containing a dominant cycle. CCOR sidesteps this by measuring correlation strength rather than instantaneous frequency; when price is trending, correlation with both cosine and sine references drops, naturally suppressing false cycle signals.

The key insight is that Pearson correlation normalizes for both mean and variance, making the Real and Imaginary outputs invariant to price level and volatility. This is a meaningful improvement over raw quadrature demodulation, where amplitude scaling can distort phase angle estimates. The addition of a monotonic angle constraint and a state classifier (trending vs. cycling) was Ehlers' acknowledgment that no cycle indicator should pretend to find cycles where none exist.

## Architecture & Physics

### 1. Dual Pearson Correlation (Quadrature Demodulation)

Two independent Pearson correlations are computed over a sliding window of length $N$ (the presumed period):

**Real component** correlates price with $\cos(2\pi k / N)$:

$$r_{\text{real}} = \frac{N \sum x_k \cos_k - \sum x_k \sum \cos_k}{\sqrt{(N \sum x_k^2 - (\sum x_k)^2)(N \sum \cos_k^2 - (\sum \cos_k)^2)}}$$

**Imaginary component** correlates price with $-\sin(2\pi k / N)$:

$$r_{\text{imag}} = \frac{N \sum x_k (-\sin_k) - \sum x_k \sum (-\sin_k)}{\sqrt{(N \sum x_k^2 - (\sum x_k)^2)(N \sum \sin_k^2 - (\sum \sin_k)^2)}}$$

Both $r_{\text{real}}, r_{\text{imag}} \in [-1, +1]$ by construction.

### 2. Phasor Angle with Quadrant Resolution

The raw angle (degrees) is computed from the arctangent of the Real/Imaginary ratio with quadrant correction:

$$\theta = \begin{cases} 90° + \arctan\!\left(\frac{r_{\text{real}}}{r_{\text{imag}}}\right) & \text{if } r_{\text{imag}} \neq 0 \\ 0° & \text{if } r_{\text{imag}} = 0 \end{cases}$$

If $r_{\text{imag}} > 0$, subtract $180°$ to resolve the correct quadrant.

### 3. Monotonic Constraint

The angle is never allowed to decrease:

$$\theta_t = \max(\theta_t, \theta_{t-1})$$

This prevents the phasor from "spinning backward" during noise, which would generate spurious state transitions.

### 4. Market State Detection

The angular velocity $|\Delta\theta| = |\theta_t - \theta_{t-1}|$ classifies regime:

$$\text{state} = \begin{cases} +1 & \text{if } |\Delta\theta| < \text{threshold} \text{ and } \theta \geq 0° \\ -1 & \text{if } |\Delta\theta| < \text{threshold} \text{ and } \theta \leq 0° \\ 0 & \text{otherwise (cycling)} \end{cases}$$

Small angle changes indicate the phasor is "stuck" in one region, implying a trend. Large angle changes indicate active cycling.

### 5. Complexity

Each bar requires two full Pearson correlation loops over $N$ samples: $O(N)$ per bar. The five accumulators ($S_x, S_y, S_{xx}, S_{xy}, S_{yy}$) per correlation can be maintained incrementally for $O(1)$ streaming, but the reference implementation uses explicit loops.

## Mathematical Foundation

### Parameters

| Parameter | Description | Default | Constraint |
|-----------|-------------|---------|------------|
| `period` | Presumed dominant cycle wavelength | 20 | $> 0$ |
| `threshold` | Angle rate threshold (degrees) for state detection | 9.0 | $> 0$ |
| `source` | Input price series | close | |

### Pearson Correlation (Detailed)

For window index $k = 0, 1, \ldots, N-1$:

$$S_x = \sum_{k=0}^{N-1} x_{t-k}, \quad S_y = \sum_{k=0}^{N-1} y_k$$

$$S_{xx} = \sum_{k=0}^{N-1} x_{t-k}^2, \quad S_{yy} = \sum_{k=0}^{N-1} y_k^2, \quad S_{xy} = \sum_{k=0}^{N-1} x_{t-k} \cdot y_k$$

$$D = (N \cdot S_{xx} - S_x^2)(N \cdot S_{yy} - S_y^2)$$

$$r = \begin{cases} \frac{N \cdot S_{xy} - S_x \cdot S_y}{\sqrt{D}} & \text{if } D > 0 \\ 0 & \text{otherwise} \end{cases}$$

Where:
- Real: $y_k = \cos(2\pi k / N)$
- Imaginary: $y_k = -\sin(2\pi k / N)$

### Pseudo-code

```
function CCOR(source, period, threshold):
    // Real correlation
    Sx_r = Sy_r = Sxx_r = Sxy_r = Syy_r = 0
    for k = 0 to period-1:
        x = source[k]
        y = cos(2π * k / period)
        Sx_r += x;  Sy_r += y
        Sxx_r += x*x;  Sxy_r += x*y;  Syy_r += y*y
    denom_r = (N*Sxx_r - Sx_r²) * (N*Syy_r - Sy_r²)
    real = denom_r > 0 ? (N*Sxy_r - Sx_r*Sy_r) / √denom_r : 0

    // Imaginary correlation (same accumulators for x, different y)
    Sx_i = Sy_i = Sxx_i = Sxy_i = Syy_i = 0
    for k = 0 to period-1:
        x = source[k]
        y = -sin(2π * k / period)
        Sx_i += x;  Sy_i += y
        Sxx_i += x*x;  Sxy_i += x*y;  Syy_i += y*y
    denom_i = (N*Sxx_i - Sx_i²) * (N*Syy_i - Sy_i²)
    imag = denom_i > 0 ? (N*Sxy_i - Sx_i*Sy_i) / √denom_i : 0

    // Phasor angle
    angle = 0
    if imag ≠ 0: angle = 90 + atan(real/imag) * (180/π)
    if imag > 0: angle -= 180

    // Monotonic constraint
    angle = max(angle, prev_angle)
    prev_angle = angle

    // State detection
    Δθ = |angle - saved_prev_angle|
    state = 0
    if Δθ < threshold and angle ≥ 0: state = +1
    if Δθ < threshold and angle ≤ 0: state = -1

    return [real, imag, angle, state]
```

### Output Interpretation

| Output | Range | Meaning |
|--------|-------|---------|
| `real` | $[-1, +1]$ | Correlation with cosine reference (in-phase strength) |
| `imag` | $[-1, +1]$ | Correlation with negative-sine reference (quadrature strength) |
| `angle` | monotonically increasing degrees | Phasor angle of detected cycle |
| `state` | $\{-1, 0, +1\}$ | $-1$ = downtrend, $0$ = cycling, $+1$ = uptrend |

## Resources

- **Ehlers, J.F.** "Correlation As A Cycle Indicator." *Technical Analysis of Stocks & Commodities*, June 2020.
- **Ehlers, J.F.** *Rocket Science for Traders*. Wiley, 2001. (Hilbert Transform phasor predecessor)
- **Ehlers, J.F.** *Cybernetic Analysis for Stocks and Futures*. Wiley, 2004. (Broader cycle analysis framework)
- **Pearson, K.** "Notes on Regression and Inheritance in the Case of Two Parents." *Proceedings of the Royal Society of London*, 58, 1895. (Original Pearson correlation)
