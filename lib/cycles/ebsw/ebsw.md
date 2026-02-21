# EBSW: Ehlers Even Better Sinewave

EBSW is a refined cycle oscillator that combines a high-pass filter (trend removal), a Super-Smoother filter (noise removal), and Automatic Gain Control to produce a normalized $[-1, +1]$ output representing the current position within the dominant market cycle. Developed by John Ehlers as an improvement over the original Hilbert Transform SineWave, it provides cleaner turning point detection without requiring complex phase extraction mathematics.

## Historical Context

Ehlers' original SineWave indicator relied on the Hilbert Transform to extract phase, but direct Hilbert Transforms proved unstable on real market data due to amplitude sensitivity and convergence issues during strong trends. The "Even Better" SineWave, published in *Cycle Analytics for Traders* (2013), simplifies the approach: instead of complex phase math, it uses a tuned bandpass filter (high-pass cascaded with a 2-pole low-pass) to isolate the dominant cycle, then normalizes the result via RMS-based AGC. The 3-bar averaging in both the wave and power calculations acts as a simple anti-aliasing stage. The result is a more robust tool for identifying turning points in both trending and ranging markets.

## Architecture & Physics

### 1. High-Pass Filter (Trend Removal)

A single-pole high-pass filter removes frequencies below the cutoff:

$$\alpha_1 = \frac{1 - \sin(2\pi / P_{HP})}{\cos(2\pi / P_{HP})}$$

$$HP_t = \frac{1 + \alpha_1}{2}(P_t - P_{t-1}) + \alpha_1 \cdot HP_{t-1}$$

### 2. Super-Smoother Filter (Noise Removal)

A 2-pole Butterworth low-pass attenuates high-frequency aliasing noise:

$$a = e^{-\sqrt{2}\pi / P_{SSF}}$$

$$b = 2a \cos(\sqrt{2}\pi / P_{SSF})$$

$$Filt_t = \frac{(1 - b + a^2)}{2}(HP_t + HP_{t-1}) + b \cdot Filt_{t-1} - a^2 \cdot Filt_{t-2}$$

### 3. Wave and Power Calculation

Three-bar averaging for both signal and energy:

$$Wave_t = \frac{Filt_t + Filt_{t-1} + Filt_{t-2}}{3}$$

$$Power_t = \frac{Filt_t^2 + Filt_{t-1}^2 + Filt_{t-2}^2}{3}$$

### 4. Normalization (AGC)

$$EBSW_t = \frac{Wave_t}{\sqrt{Power_t}}$$

Result is clamped to $[-1, +1]$. When $Power \approx 0$, output is zero.

### 5. Complexity

$O(1)$ per bar. Fixed cascaded IIR filters with $O(1)$ memory (only filter state variables and 3-bar history for wave/power).

## Mathematical Foundation

### Parameters

| Parameter | Description | Default | Constraint |
|-----------|-------------|---------|------------|
| `hpLength` | High-pass filter period (detrending cutoff) | 40 | $\geq 1$, $\neq 4$ |
| `ssfLength` | Super-smoother filter period (noise cutoff) | 10 | $\geq 1$ |

### Precomputed Coefficients

```
α₁ = (1 - sin(2π/hpLength)) / cos(2π/hpLength)
a  = exp(-√2·π / ssfLength)
b  = 2·a·cos(√2·π / ssfLength)
c₁ = (1 - b + a²) / 2
```

### Pseudo-code

```
function EBSW(source, hpLength, ssfLength):
    // Precompute HP coefficient
    α₁ ← (1 - sin(2π/hpLength)) / cos(2π/hpLength)

    // Precompute SSF coefficients
    a ← exp(-√2·π / ssfLength)
    b ← 2·a·cos(√2·π / ssfLength)
    c₁ ← (1 - b + a²) / 2

    hp_prev ← 0; p_prev ← 0
    filt_1 ← 0; filt_2 ← 0

    for each price in source:
        // High-pass filter
        hp ← 0.5·(1 + α₁)·(price - p_prev) + α₁·hp_prev

        // Super-smoother
        filt ← c₁·(hp + hp_prev) + b·filt_1 - a²·filt_2

        // Wave (3-bar average of filtered signal)
        wave ← (filt + filt_1 + filt_2) / 3

        // Power (3-bar RMS²)
        power ← (filt² + filt_1² + filt_2²) / 3

        // AGC normalization
        ebsw ← (power > 0) ? wave / √power : 0
        ebsw ← clamp(ebsw, -1, +1)

        // Shift state
        hp_prev ← hp; p_prev ← price
        filt_2 ← filt_1; filt_1 ← filt

        emit ebsw
```

### Output Interpretation

| Condition | Meaning |
|-----------|---------|
| $EBSW \approx +1$ | Cycle peak (potential short entry) |
| $EBSW \approx -1$ | Cycle trough (potential long entry) |
| Zero crossing up | Bullish phase transition |
| Zero crossing down | Bearish phase transition |
| Railing at $\pm 1$ | Strong directional move overwhelming cycle |

## Resources

- **Ehlers, J.F.** *Cycle Analytics for Traders*. Wiley, 2013.
- **Ehlers, J.F.** *Cybernetic Analysis for Stocks and Futures*. Wiley, 2004.
