# IFFT: Inverse Fast Fourier Transform (Spectral Filter)

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Numeric                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `windowSize` (default 64), `numHarmonics` (default 5)                      |
| **Outputs**      | Single series (Ifft)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | 1 bar                          |

### TL;DR

- The Inverse FFT indicator reconstructs a smoothed version of the price series by performing a forward DFT, retaining only the lowest-frequency harm...
- Parameterized by `windowsize` (default 64), `numharmonics` (default 5).
- Output range: Varies (see docs).
- Requires 1 bar of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The Inverse FFT indicator reconstructs a smoothed version of the price series by performing a forward DFT, retaining only the lowest-frequency harmonics, and synthesizing the output via inverse transform. The result is a spectral low-pass filter that preserves the dominant cyclical components while discarding high-frequency noise. By controlling the number of retained harmonics $H$, the user adjusts the smoothness/responsiveness trade-off: $H = 1$ yields a near-sinusoidal trend, while $H = N/2$ reproduces the original (windowed) signal. The indicator overlays on price and provides a frequency-domain alternative to conventional moving averages.

## Historical Context

Spectral filtering via Fourier decomposition dates to Joseph Fourier's 1822 work on heat conduction, where he showed that any periodic function can be represented as a sum of sinusoids. The idea of reconstructing a signal from a subset of its Fourier coefficients is foundational to signal compression (JPEG, MP3) and has been applied to financial time series since the 1970s.

John Ehlers brought spectral methods to mainstream technical analysis through his books on cycle analytics. His approach typically uses the DFT to identify the dominant cycle, then constructs adaptive filters tuned to that cycle. The IFFT indicator takes the complementary approach: rather than extracting a single cycle period, it reconstructs the signal from the $H$ lowest-frequency components, producing a multi-harmonic trend estimate.

The Hanning window applied before the forward DFT reduces spectral leakage, ensuring that the retained harmonics accurately represent the true low-frequency content rather than artifacts of the window boundary. The inverse step only uses the real part of the synthesis (cosine terms), since the output must be a real-valued price estimate. The factor of 2 in the inverse accounts for the conjugate symmetry of real-valued DFT coefficients.

## Architecture and Physics

The computation has three stages executed per bar:

**Stage 1: DC component** computes the windowed mean of the source over the window. This is the zero-frequency (average level) component:

$$\text{DC} = \frac{1}{N}\sum_{n=0}^{N-1} x[n] \cdot w[n]$$

**Stage 2: Forward DFT for harmonics $k = 1$ to $H$** computes the real and imaginary Fourier coefficients for each retained harmonic. The Hanning window $w[n] = 0.5 - 0.5\cos(2\pi n/N)$ is applied to every sample.

**Stage 3: Inverse synthesis** reconstructs the current bar's value by summing the DC component plus twice the real part of each harmonic evaluated at $n = 0$ (the current bar):

$$\hat{x}[0] = \frac{\text{DC}_{\text{Re}}}{N} + \sum_{k=1}^{H} \frac{2 \cdot \text{Re}(X[k])}{N}$$

The factor $2/N$ accounts for: (1) the $1/N$ normalization of the inverse DFT, and (2) the factor of 2 from collapsing the conjugate-symmetric negative frequencies.

**Complexity**: The forward DFT for $H$ harmonics costs $O(N \cdot H)$ multiply-adds per bar. With $N = 64$ and $H = 5$ (defaults), this is ~320 multiply-adds per bar. The inverse synthesis at $n = 0$ reduces to just summing the real components, costing $O(H)$.

**Smoothness control**: Fewer harmonics produce smoother output but introduce more lag and lose detail. The relationship between harmonics and equivalent moving average length is roughly: $H$ harmonics approximate the smoothness of an $N/(2H)$-period moving average, but with better frequency selectivity (sharper cutoff).

## Mathematical Foundation

The **forward DFT** with Hanning window:

$$X[k] = \sum_{n=0}^{N-1} x[n] \cdot w[n] \cdot e^{-j 2\pi k n / N}$$

where $w[n] = 0.5 - 0.5\cos(2\pi n / N)$.

The **inverse DFT** evaluated at the current bar ($n = 0$):

$$\hat{x}[0] = \frac{1}{N}\sum_{k=0}^{N-1} X[k] \cdot e^{j 2\pi k \cdot 0 / N} = \frac{1}{N}\sum_{k=0}^{N-1} X[k]$$

Since $e^{j \cdot 0} = 1$, the inverse at $n = 0$ is simply the sum of all retained coefficients divided by $N$.

For a real-valued signal, $X[N-k] = X[k]^*$, so:

$$\hat{x}[0] = \frac{X[0]}{N} + \frac{2}{N}\sum_{k=1}^{H} \text{Re}(X[k])$$

**Parseval's theorem** relates the energy retained:

$$\frac{\sum_{k=0}^{H} |X[k]|^2}{\sum_{k=0}^{N/2} |X[k]|^2} = \text{fraction of signal energy preserved}$$

**Parameter constraints**: `windowSize` $\in \{32, 64, 128\}$, `numHarmonics` $\ge 1$ (clamped to $N/2$).

```
IFFT(source, windowSize, numHarmonics):
    N = windowSize
    H = min(numHarmonics, N/2)
    twoPiOverN = 2 * pi / N

    // DC component (k=0)
    dcRe = 0
    for n = 0 to N-1:
        w = 0.5 - 0.5 * cos(twoPiOverN * n)
        dcRe += source[n] * w
    result = dcRe / N

    // Harmonics k=1..H
    for k = 1 to H:
        re = 0;  im = 0
        for n = 0 to N-1:
            w = 0.5 - 0.5 * cos(twoPiOverN * n)
            xw = source[n] * w
            angle = twoPiOverN * k * n
            re += xw * cos(angle)
            im -= xw * sin(angle)
        result += 2 * re / N    // inverse at n=0

    return result
```


## Performance Profile

### Operation Count (Streaming Mode)

IFFT (Inverse DFT reconstruction) sums B frequency components back into the time domain — O(N*B) per bar.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Complex multiply-accumulate (N * B) | N*B | 4 cy | ~4*N*B cy |
| cos/sin table lookup (precomputed) | 2*N*B | 0 cy | ~0 cy |
| Division by N for normalization | N | 1 cy | ~N cy |
| NaN guard + state update | 1 | 2 cy | ~2 cy |
| **Total (N=64, B=10)** | **O(N*B)** | — | **~2626 cy** |

Same complexity as forward FFT. Precomputed trig tables allow the inner loop to reduce to 4 FMAs per bin. Paired with FFT for frequency-domain filtering.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| Complex MAC (re*cos - im*sin) | Yes | FMA with precomputed table |
| Normalization | Yes | Vector divide by N |
| Output time-domain signal | Yes | Full SIMD reconstruction |

Same SIMD profile as FFT forward pass. 3-4× batch speedup expected over scalar using Vector<double> FMA.

## Resources

- Fourier, J.B.J. "Theorie Analytique de la Chaleur." Firmin Didot, 1822.
- Ehlers, J.F. "Cycle Analytics for Traders." Wiley, 2013.
- Oppenheim, A.V. & Schafer, R.W. "Discrete-Time Signal Processing." 3rd edition, Pearson, 2010.
- Bloomfield, P. "Fourier Analysis of Time Series: An Introduction." 2nd edition, Wiley, 2000.
- Priestley, M.B. "Spectral Analysis and Time Series." Academic Press, 1981.
