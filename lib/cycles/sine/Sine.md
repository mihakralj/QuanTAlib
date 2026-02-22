# SINE: Ehlers Sine Wave

SINE extracts the dominant cycle from price data using cascaded signal processing: a high-pass filter removes the trend, a Super-Smoother filter removes noise, and a Hilbert Transform FIR decomposes the filtered signal into In-Phase and Quadrature components for power-normalized sine wave output. The result oscillates between $-1$ and $+1$, representing the normalized position within the current cycle. Unlike HT_SINE which derives phase from the full TA-Lib Hilbert cascade, this Ehlers implementation uses explicit detrending and bandpass stages for cleaner cycle isolation.

## Historical Context

John Ehlers introduced the Sine Wave indicator in *Cybernetic Analysis for Stocks and Futures* (2004) as a refined approach to cycle extraction. The design philosophy separates three signal processing concerns into distinct filter stages: (1) trend removal via high-pass filtering sets the long-wavelength cutoff, (2) aliasing prevention via Super-Smoother sets the short-wavelength cutoff, and (3) cycle extraction via Hilbert Transform generates the quadrature decomposition. This staged approach produces cleaner output than attempting all three simultaneously (as in the HT_SINE). The Sine Wave output at extremes ($\pm 1$) indicates the cyclical component is stretched and likely to revert, while zero crossings indicate phase transitions. The indicator is particularly valuable for mean-reversion strategies in ranging markets.

## Architecture & Physics

### 1. High-Pass Filter (Detrending)

A single-pole high-pass filter removes low-frequency trends below the cutoff:

$$\alpha_{HP} = \frac{1 - \sin(2\pi / P_{HP})}{\cos(2\pi / P_{HP})}$$

$$HP_t = \frac{1 + \alpha_{HP}}{2}(P_t - P_{t-1}) + \alpha_{HP} \cdot HP_{t-1}$$

### 2. Super-Smoother Filter (Noise Removal)

A 2-pole Butterworth low-pass removes high-frequency noise:

$$a = e^{-\sqrt{2}\pi / P_{SSF}}$$

$$b = 2a \cos(\sqrt{2}\pi / P_{SSF})$$

$$c_1 = 1 - b + a^2, \quad c_2 = b, \quad c_3 = -a^2$$

$$Filt_t = \frac{c_1}{2}(HP_t + HP_{t-1}) + c_2 \cdot Filt_{t-1} + c_3 \cdot Filt_{t-2}$$

### 3. Hilbert Transform FIR

Discrete Hilbert approximation extracts quadrature component:

$$Q_t = 0.0962 \cdot Filt_{t-3} + 0.5769 \cdot Filt_{t-1} - 0.5769 \cdot Filt_{t-5} - 0.0962 \cdot Filt_{t-7}$$

$$I_t = Filt_t$$

### 4. Power Normalization

$$Power_t = I_t^2 + Q_t^2$$

$$Sine_t = \frac{I_t}{\sqrt{Power_t}}$$

When $Power \approx 0$, output is zero.

### 5. Complexity

$O(1)$ per bar. Fixed filter stages with ring buffers of 2 (source) + 2 (HP) + 8 (filtered) = 12 elements. Warmup: $\max(P_{HP}, P_{SSF}) + 8$ bars.

## Mathematical Foundation

### Parameters

| Parameter | Description | Default | Constraint |
|-----------|-------------|---------|------------|
| `hpPeriod` | High-pass filter cutoff period | 40 | $\geq 1$ |
| `ssfPeriod` | Super-smoother filter period | 10 | $\geq 1$ |

### Tuning Relationship

Typically $P_{SSF} \approx P_{HP} / 4$ to $P_{HP} / 2$. The high-pass defines the trend/cycle boundary; the super-smoother defines the noise/cycle boundary. Together they create a bandpass that isolates the frequency range of interest.

### Pseudo-code

```
function SINE(source, hpPeriod, ssfPeriod):
    // Precompute HP coefficient
    α_hp ← (1 - sin(2π/hpPeriod)) / cos(2π/hpPeriod)

    // Precompute SSF coefficients
    a ← exp(-√2·π / ssfPeriod)
    b ← 2·a·cos(√2·π / ssfPeriod)
    c₁ ← (1 - b + a²) / 2

    hp_prev ← 0; p_prev ← 0
    filt_1 ← 0; filt_2 ← 0
    filtBuf ← CircularBuffer(8)

    for each price in source:
        // High-pass
        hp ← 0.5·(1 + α_hp)·(price - p_prev) + α_hp·hp_prev

        // Super-smoother
        filt ← c₁·(hp + hp_prev) + b·filt_1 - a²·filt_2

        // Hilbert FIR quadrature
        filtBuf.Add(filt)
        Q ← 0.0962·filtBuf[3] + 0.5769·filtBuf[1]
           - 0.5769·filtBuf[5] - 0.0962·filtBuf[7]
        I ← filt

        // Power normalization
        power ← I² + Q²
        sine ← (power > 0) ? I / √power : 0

        // Shift state
        hp_prev ← hp; p_prev ← price
        filt_2 ← filt_1; filt_1 ← filt

        emit sine
```

### SINE vs HT_SINE

| Aspect | SINE | HT_SINE |
|--------|------|---------|
| Detrending | Explicit high-pass filter | Implicit in Hilbert cascade |
| Noise removal | Explicit Super-Smoother | 4-bar WMA only |
| Period tuning | User-configurable (hpPeriod, ssfPeriod) | Fixed (TA-Lib spec) |
| Output | Single (Sine only) | Dual (Sine + LeadSine) |
| Phase source | I/Q power normalization | DFT phase accumulation |

### Output Interpretation

| Condition | Meaning |
|-----------|---------|
| $Sine \approx +1$ | Cycle peak (potential short / mean-reversion) |
| $Sine \approx -1$ | Cycle trough (potential long / mean-reversion) |
| Zero crossing up | Bullish phase transition |
| Zero crossing down | Bearish phase transition |
| Erratic output | Strong trend overwhelming cycle extraction |

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count per bar | Notes |
|-----------|--------------|-------|
| High-pass filter | ~4 | 1 SUB + 1 MUL + 1 FMA |
| Super-Smoother (2-pole IIR) | ~5 | 1 ADD + 2 FMA + 1 MUL |
| Hilbert FIR (quadrature) | ~7 | 4-tap FIR: 4 MUL + 3 ADD |
| I² + Q² (power) | ~3 | 2 MUL + 1 ADD |
| SQRT + normalization | ~4 | 1 SQRT + 1 DIV + 1 branch |
| Buffer management | ~3 | 1 circular buffer write + index update |
| State shift | ~4 | 4 register moves |
| **Total** | **~30** | **O(1) fixed; single SQRT is only transcendental** |

### Batch Mode (SIMD Analysis)

| Aspect | Assessment |
|--------|------------|
| SIMD vectorizable | No: HP and SSF are recursive IIR with sequential state dependencies |
| Bottleneck | `Math.Sqrt` in power normalization (~15 cycles); rest is pure arithmetic |
| Parallelism | None: each bar's HP/SSF output depends on previous bar |
| Memory | O(1): 8-element ring buffer + 4 scalar state variables (~96 bytes) |
| Throughput | Very fast; slightly faster than EBSW (no 3-bar averaging, no clamp) |

## Resources

- **Ehlers, J.F.** *Cybernetic Analysis for Stocks and Futures*. Wiley, 2004.
- **Ehlers, J.F.** *Cycle Analytics for Traders*. Wiley, 2013.
