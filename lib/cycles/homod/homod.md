# HOMOD: Ehlers Homodyne Discriminator

HOMOD estimates the dominant cycle period of a market using homodyne mixing, a technique from radio engineering where a signal is multiplied by a delayed copy of itself to expose the angular phase change between samples. The output is a continuously varying period measurement (in bars) that tracks the market's instantaneous cycle length, enabling adaptive indicator tuning. Developed by John Ehlers, it offers better noise rejection and stability than the raw Hilbert Transform period estimator.

## Historical Context

John Ehlers introduced the Homodyne Discriminator in *Rocket Science for Traders* (2001) and refined it in *Cybernetic Analysis for Stocks and Futures* (2004). In RF engineering, homodyne detection multiplies a signal with a local oscillator at the same frequency to extract phase information. Ehlers adapted this by multiplying the complex analytic signal $z_t = I_t + jQ_t$ by its own conjugate delayed by one bar, yielding the phase rotation per sample. The angular velocity directly encodes the instantaneous frequency (and hence period). Compared to the raw Hilbert Transform discriminator which estimates phase absolutely, homodyne detection measures phase *differences*, making it less sensitive to amplitude variations and more stable during noisy market conditions.

## Architecture & Physics

### 1. Pre-Processing (4-Bar WMA)

$$Smooth_t = \frac{4P_t + 3P_{t-1} + 2P_{t-2} + P_{t-3}}{10}$$

### 2. Analytic Signal Generation

The Hilbert Transform FIR generates quadrature components using Ehlers' 4-tap approximation with coefficients $A = 0.0962$ and $B = 0.5769$:

$$Detrender_t = A \cdot Smooth_t + B \cdot Smooth_{t-2} - B \cdot Smooth_{t-4} - A \cdot Smooth_{t-6}$$

In-Phase ($I_1$) is the detrender delayed by 3 bars. Quadrature ($Q_1$) is the Hilbert transform of the detrender. Both are further refined:

$$I_2 = I_1 - jQ, \qquad Q_2 = Q_1 + jI$$

Smoothed with EMA ($\alpha = 0.2$).

### 3. Homodyne Mixing

Multiplying the complex signal by its one-bar-delayed conjugate:

$$Re_t = I_{2,t} \cdot I_{2,t-1} + Q_{2,t} \cdot Q_{2,t-1}$$

$$Im_t = I_{2,t} \cdot Q_{2,t-1} - Q_{2,t} \cdot I_{2,t-1}$$

Both smoothed with EMA ($\alpha = 0.2$).

### 4. Period Extraction

$$\theta = \operatorname{atan2}(Im_t, Re_t)$$

$$Period_{raw} = \frac{2\pi}{\theta}$$

Clamped to $[MinPeriod, MaxPeriod]$ and smoothed with EMA ($\alpha = 0.33$).

### 5. Complexity

$O(1)$ per bar with $O(1)$ memory. The pipeline consists entirely of fixed-depth IIR filters and short delay lines.

## Mathematical Foundation

### Parameters

| Parameter | Description | Default | Constraint |
|-----------|-------------|---------|------------|
| `minPeriod` | Minimum detectable period | 6.0 | $> 0$ |
| `maxPeriod` | Maximum detectable period | 50.0 | $> minPeriod$ |

### Pseudo-code

```
function HOMOD(source, minPeriod, maxPeriod):
    A ← 0.0962; B ← 0.5769
    smoothBuf ← CircularBuffer(7)
    detBuf ← CircularBuffer(7)

    I2_prev ← 0; Q2_prev ← 0
    Re_prev ← 0; Im_prev ← 0
    period_prev ← (minPeriod + maxPeriod) / 2

    for each price in source:
        // 4-bar WMA
        smooth ← (4·price + 3·p[1] + 2·p[2] + p[3]) / 10
        smoothBuf.Add(smooth)

        // Detrender (Hilbert FIR)
        det ← A·smooth[0] + B·smooth[2] - B·smooth[4] - A·smooth[6]
        detBuf.Add(det)

        // I1 = det[3], Q1 = Hilbert(det)
        I1 ← det[3]
        Q1 ← A·det[0] + B·det[2] - B·det[4] - A·det[6]

        // Hilbert of I1 and Q1
        jI ← HilbertFIR(I1_history)
        jQ ← HilbertFIR(Q1_history)

        // Phasor components (smoothed)
        I2 ← 0.2·(I1 - jQ) + 0.8·I2_prev
        Q2 ← 0.2·(Q1 + jI) + 0.8·Q2_prev

        // Homodyne mixing
        re ← 0.2·(I2·I2_prev + Q2·Q2_prev) + 0.8·Re_prev
        im ← 0.2·(I2·Q2_prev - Q2·I2_prev) + 0.8·Im_prev

        // Period extraction
        if im ≠ 0 and re ≠ 0:
            period ← 2π / atan2(im, re)
        else:
            period ← period_prev

        period ← clamp(period, minPeriod, maxPeriod)
        period ← 0.33·period + 0.67·period_prev

        // Update state
        I2_prev ← I2; Q2_prev ← Q2
        Re_prev ← re; Im_prev ← im
        period_prev ← period

        emit period
```

### Output Interpretation

| Output | Meaning |
|--------|---------|
| `period` | Dominant cycle length in bars |
| Stable period | Market exhibiting regular cyclical behavior |
| Period drifting to maxPeriod | Trending market; cycle measurement unreliable |
| Rapidly fluctuating period | Noisy or transitioning market regime |

## Resources

- **Ehlers, J.F.** *Rocket Science for Traders*. Wiley, 2001.
- **Ehlers, J.F.** *Cybernetic Analysis for Stocks and Futures*. Wiley, 2004.
- **Haykin, S.** *Communication Systems*. 4th ed., Wiley, 2001. (Homodyne detection theory)
