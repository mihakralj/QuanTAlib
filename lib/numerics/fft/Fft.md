# FFT: Fast Fourier Transform (Dominant Cycle Detector)

The FFT indicator computes the dominant cycle period in a price series using a Discrete Fourier Transform with a Hanning window. Rather than outputting frequency-domain magnitudes, it returns the estimated dominant cycle period in bars, making it directly usable as an adaptive period input for other indicators. The implementation uses a brute-force DFT over a constrained frequency band (not a radix-2 FFT), with parabolic interpolation on the magnitude spectrum to achieve sub-bin frequency resolution. With window sizes of 32, 64, or 128 and $O(N \cdot N/2)$ complexity per bar, the indicator trades computational cost for precise cycle detection within user-specified period bounds.

## Historical Context

The Fourier transform, formalized by Joseph Fourier (1822), decomposes any periodic signal into sinusoidal components. The Fast Fourier Transform algorithm (Cooley and Tukey, 1965) reduced the DFT from $O(N^2)$ to $O(N \log N)$, enabling real-time spectral analysis. However, for the small window sizes used in financial cycle detection (32-128 samples), the asymptotic advantage of FFT over DFT is minimal, and the DFT avoids the power-of-two length constraint.

John Ehlers pioneered the application of spectral analysis to financial markets in the 1990s and 2000s, using DFT-based cycle measurement to create adaptive indicators. His work demonstrated that financial time series contain quasi-periodic cycles with time-varying periods, typically in the 6-40 bar range. The dominant cycle period, extracted via spectral peak detection, can drive adaptive moving averages (MAMA, FAMA), adaptive RSI, and other indicators that benefit from knowing the current market rhythm.

The Hanning window (also called Hann window, after Julius von Hann) is applied to reduce spectral leakage. Without windowing, the sharp truncation of a finite data segment creates artificial high-frequency components that contaminate the spectrum. The Hanning window tapers the data to zero at both ends, suppressing sidelobes at the cost of slightly wider main lobes (reduced frequency resolution).

## Architecture and Physics

The computation pipeline has four stages:

**Stage 1: Windowed DFT** computes the real and imaginary components of the Fourier coefficients for frequency bins $k$ ranging from `minBin` to `maxBin`:

$$X[k] = \sum_{n=0}^{N-1} x[n] \cdot w[n] \cdot e^{-j 2\pi k n / N}$$

where $w[n] = 0.5 - 0.5\cos(2\pi n/N)$ is the Hanning window. Only bins corresponding to periods in `[minPeriod, maxPeriod]` are evaluated, reducing computation.

**Stage 2: Power spectrum peak** finds the bin $k^*$ with maximum squared magnitude $|X[k]|^2 = \text{Re}^2 + \text{Im}^2$. During the search, the magnitudes of the bins adjacent to the peak (one before, one after) are captured for interpolation.

**Stage 3: Parabolic interpolation** refines the peak location using a three-point parabola fit on the magnitudes at bins $k^*-1$, $k^*$, $k^*+1$:

$$\delta = \frac{M_{k^*-1} - M_{k^*+1}}{M_{k^*-1} + 2 M_{k^*} + M_{k^*+1}}$$

The refined dominant period is $N / (k^* + \delta)$.

**Stage 4: Clamping** ensures the output stays within `[minPeriod, maxPeriod]`.

**Window size trade-offs**: $N = 32$ gives coarse resolution (period bins spaced ~1 bar apart) but fast response; $N = 128$ gives fine resolution (~0.25 bar spacing) but sluggish adaptation. The default $N = 64$ balances resolution and responsiveness.

## Mathematical Foundation

The **Discrete Fourier Transform** for $N$ samples:

$$X[k] = \sum_{n=0}^{N-1} x[n] \cdot e^{-j 2\pi k n / N}, \quad k = 0, 1, \ldots, N-1$$

**Hanning window**:

$$w[n] = 0.5 - 0.5\cos\!\left(\frac{2\pi n}{N}\right)$$

**Frequency-to-period** mapping: bin $k$ corresponds to period $T = N/k$ bars.

**Bin range** from period bounds:

$$k_{\min} = \max\!\left(1,\; \left\lfloor\frac{N}{T_{\max}}\right\rfloor\right), \quad k_{\max} = \min\!\left(\frac{N}{2},\; \left\lfloor\frac{N}{T_{\min}}\right\rfloor\right)$$

**Power spectrum**: $P[k] = \text{Re}(X[k])^2 + \text{Im}(X[k])^2$

**Parabolic interpolation** for sub-bin precision:

$$\hat{k} = k^* + \frac{P[k^*-1] - P[k^*+1]}{P[k^*-1] + 2P[k^*] + P[k^*+1]}$$

$$T_{\text{dominant}} = \frac{N}{\hat{k}}$$

**Parameter constraints**: `windowSize` $\in \{32, 64, 128\}$, `minPeriod` $\ge 2$, `maxPeriod` $\le N/2$.

```
FFT(source, windowSize, minPeriod, maxPeriod):
    N = windowSize
    twoPiOverN = 2 * pi / N
    minBin = max(1, N / maxPeriod)
    maxBin = min(N/2, N / minPeriod)

    maxMag = 0;  peakBin = 0
    for k = minBin to maxBin:
        re = 0;  im = 0
        for n = 0 to N-1:
            w = 0.5 - 0.5 * cos(twoPiOverN * n)   // Hanning
            xw = source[n] * w
            angle = twoPiOverN * k * n
            re += xw * cos(angle)
            im -= xw * sin(angle)
        mag = re*re + im*im
        if mag > maxMag:
            track neighbor magnitudes
            maxMag = mag;  peakBin = k

    // Parabolic interpolation
    shift = (magBefore - magAfter) / (magBefore + 2*maxMag + magAfter)
    dominantPeriod = N / (peakBin + shift)
    return clamp(dominantPeriod, minPeriod, maxPeriod)
```


## Performance Profile

### Operation Count (Streaming Mode)

FFT (DFT dominant cycle detector) evaluates B frequency bins, each requiring N multiply-accumulates — O(N*B) per bar.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Hanning window multiply | N | 2 cy | ~2N cy |
| DFT inner loop (B bins * N samples) | B*N | 4 cy | ~4*N*B cy |
| cos/sin evaluation (precomputed table) | 2*B*N | 0 cy | ~0 cy |
| Magnitude comparison + peak track | B | 2 cy | ~2B cy |
| Parabolic interpolation (3 points) | 1 | 5 cy | ~5 cy |
| **Total (N=64, B=10)** | **O(N*B)** | — | **~2617 cy** |

O(N*B) per bar where B = active frequency bins. Precomputed sin/cos tables eliminate transcendental cost. Suitable for 1-minute+ timeframes; not tick-data hot paths.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| Hanning window application | Yes | Vector multiply with precomputed weights |
| DFT inner dot product | Yes | FMA with sin/cos table lookup |
| Magnitude squared | Yes | Vector FMA (re^2 + im^2) |
| Peak search | Partial | Max reduction; SIMD-friendly |

Strong batch SIMD: inner dot products are FMA-vectorizable. AVX2 processes 4 complex outputs per 2 cycles. Expected 3-4× speedup for N=64.

## Resources

- Cooley, J.W. & Tukey, J.W. "An Algorithm for the Machine Calculation of Complex Fourier Series." Mathematics of Computation, 1965.
- Ehlers, J.F. "Cycle Analytics for Traders." Wiley, 2013.
- Ehlers, J.F. "Rocket Science for Traders." Wiley, 2001.
- Harris, F.J. "On the Use of Windows for Harmonic Analysis with the Discrete Fourier Transform." Proc. IEEE, 1978.
- Oppenheim, A.V. & Schafer, R.W. "Discrete-Time Signal Processing." 3rd edition, Pearson, 2010.
