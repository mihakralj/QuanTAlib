# IFFT: Inverse Fast Fourier Transform (Spectral Low-Pass Filter)

> *Inverse FFT reconstructs a time series from selected frequency components — a spectral scalpel for noise removal.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Numeric                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `windowSize` (default 64), `numHarmonics` (default 5)                      |
| **Outputs**      | Single series (Ifft)                       |
| **Output range** | Varies (overlays on price)                     |
| **Warmup**       | windowSize bars                          |
| **PineScript**   | [ifft.pine](ifft.pine)                       |

- The IFFT indicator reconstructs a smoothed version of the price series using a true forward FFT → spectral truncation → inverse FFT pipeline.
- True $O(N \log N)$ radix-2 FFT/IFFT with bit-reversal permutation and Cooley-Tukey butterflies.
- **Similar:** [FFT](../fft/Fft.md) | **Trading note:** Inverse FFT; reconstructs filtered time-domain signal from frequency domain. Used with FFT for spectral filtering.

The IFFT indicator reconstructs a smoothed version of the price series by performing a true radix-2 forward FFT, zeroing frequency bins above the specified number of harmonics (spectral truncation), then applying a true inverse FFT to reconstruct the filtered time-domain signal. The result is a spectral low-pass filter that preserves the dominant cyclical components while discarding high-frequency noise. By controlling the number of retained harmonics $H$, the user adjusts the smoothness/responsiveness trade-off: $H = 1$ yields a near-sinusoidal trend, while $H = N/2$ reproduces the original (windowed) signal. The indicator overlays on price and provides a frequency-domain alternative to conventional moving averages.

## Historical Context

Spectral filtering via Fourier decomposition dates to Joseph Fourier's 1822 work on heat conduction, where he showed that any periodic function can be represented as a sum of sinusoids. The Cooley-Tukey FFT algorithm (1965) made real-time spectral analysis practical by reducing complexity from $O(N^2)$ to $O(N \log N)$.

John Ehlers brought spectral methods to mainstream technical analysis through his books on cycle analytics. The IFFT indicator implements the classic spectral filtering paradigm: forward FFT to decompose into frequency components, selective retention of low-frequency bins, and inverse FFT to reconstruct the filtered signal.

The Hanning window applied before the forward FFT reduces spectral leakage, ensuring that the retained harmonics accurately represent the true low-frequency content rather than artifacts of the window boundary. Conjugate symmetry is preserved during spectral truncation to guarantee real-valued reconstruction.

## Architecture and Physics

The computation has four stages executed per bar:

**Stage 1: Forward FFT** applies a Hanning window to the rolling price buffer, then performs an in-place radix-2 Cooley-Tukey FFT with bit-reversal permutation:

$$X[k] = \text{FFT}\!\left(x[n] \cdot w[n]\right)$$

where $w[n] = 0.5 - 0.5\cos(2\pi n/N)$.

**Stage 2: Spectral truncation** zeroes frequency bins outside the preserved range, keeping bins $k = 0, 1, \ldots, H$ and their conjugate mirrors $k = N-H, \ldots, N-1$:

$$\tilde{X}[k] = \begin{cases} X[k] & \text{if } k \le H \text{ or } k \ge N-H \\ 0 & \text{otherwise} \end{cases}$$

This preserves conjugate symmetry ($\tilde{X}[N-k] = \tilde{X}[k]^*$), ensuring the inverse FFT produces real-valued output.

**Stage 3: Inverse FFT** reconstructs the filtered time-domain signal using the conjugate method:

$$\hat{x}[n] = \frac{1}{N} \cdot \overline{\text{FFT}\!\left(\overline{\tilde{X}[k]}\right)}$$

This reuses the forward FFT algorithm by conjugating inputs, applying FFT, conjugating outputs, and scaling by $1/N$.

**Stage 4: Sample extraction** returns the value at position $N-1$ (the newest bar in the window).

**Complexity**: Two FFT passes of $O(N \log N)$ each, plus $O(N)$ for windowing and spectral truncation. Total: $O(N \log N)$ per bar.

**Smoothness control**: Fewer harmonics produce smoother output but introduce more lag. The relationship between harmonics and equivalent moving average length is roughly: $H$ harmonics approximate the smoothness of an $N/(2H)$-period moving average, with better frequency selectivity (sharper cutoff).

## Mathematical Foundation

The **forward FFT** with Hanning window (radix-2 Cooley-Tukey):

$$X[k] = \text{FFT}_N\!\left(x[n] \cdot w[n]\right), \quad k = 0, 1, \ldots, N-1$$

**Spectral truncation** (ideal low-pass in frequency domain):

$$\tilde{X}[k] = X[k] \cdot H_{\text{LP}}[k], \quad H_{\text{LP}}[k] = \begin{cases} 1 & k \le H \text{ or } k \ge N-H \\ 0 & \text{otherwise} \end{cases}$$

**Inverse FFT** via conjugation:

$$\hat{x}[n] = \frac{1}{N} \cdot \overline{\text{FFT}_N\!\left(\overline{\tilde{X}[k]}\right)}$$

**Parseval's theorem** relates the energy retained:

$$\frac{\sum_{k=0}^{H} |X[k]|^2}{\sum_{k=0}^{N/2} |X[k]|^2} = \text{fraction of signal energy preserved}$$

**Parameter constraints**: `windowSize` $\in \{32, 64, 128\}$, `numHarmonics` $\ge 1$ (clamped to $N/2$).

```
IFFT(source, windowSize, numHarmonics):
    N = windowSize
    H = min(numHarmonics, N/2)

    // Stage 1: Window + Forward FFT
    for n = 0 to N-1:
        workRe[n] = source[n] * hanning[n]
        workIm[n] = 0
    FFT_InPlace(workRe, workIm, N)

    // Stage 2: Spectral truncation
    for k = H+1 to N-H-1:
        workRe[k] = 0
        workIm[k] = 0

    // Stage 3: Inverse FFT (via conjugation)
    for i = 0 to N-1: workIm[i] = -workIm[i]
    FFT_InPlace(workRe, workIm, N)
    for i = 0 to N-1:
        workRe[i] /= N
        workIm[i] = -workIm[i] / N

    // Stage 4: Extract newest sample
    return workRe[N-1]
```


## Performance Profile

### Operation Count (Streaming Mode)

IFFT performs two radix-2 FFT passes (forward + inverse) plus spectral truncation — O(N log N) per bar.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Hanning window multiply | N | 2 cy | ~2N cy |
| Forward FFT (N/2 × log₂N butterflies) | N/2 × log₂N | 8 cy | ~4N·log₂N cy |
| Spectral truncation | N-2H | 1 cy | ~(N-2H) cy |
| Conjugation (2×) | 2N | 1 cy | ~2N cy |
| Inverse FFT (N/2 × log₂N butterflies) | N/2 × log₂N | 8 cy | ~4N·log₂N cy |
| Scale by 1/N | N | 1 cy | ~N cy |
| **Total (N=64, H=5)** | **O(N log N)** | — | **~3254 cy** |

Two FFT passes dominate cost. Pre-allocated work arrays ensure zero allocation in the hot path.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| Hanning window application | Yes | Vector multiply with precomputed weights |
| FFT butterfly operations | Yes | Complex FMA on paired elements |
| Spectral truncation (zeroing) | Yes | Vector zero-fill |
| IFFT butterfly operations | Yes | Same as forward FFT |
| Scale by 1/N | Yes | Vector multiply by constant |

Good SIMD potential: both FFT passes are vectorizable. Expected 2× speedup over scalar for N=64.

## Resources

- Cooley, J.W. & Tukey, J.W. "An Algorithm for the Machine Calculation of Complex Fourier Series." *Mathematics of Computation*, 1965.
- Fourier, J.B.J. "Theorie Analytique de la Chaleur." Firmin Didot, 1822.
- Ehlers, J.F. "Cycle Analytics for Traders." Wiley, 2013.
- Oppenheim, A.V. & Schafer, R.W. "Discrete-Time Signal Processing." 3rd edition, Pearson, 2010.
- Bloomfield, P. "Fourier Analysis of Time Series: An Introduction." 2nd edition, Wiley, 2000.
- PineScript reference: [`ifft.pine`](ifft.pine)