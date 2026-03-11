# FFT: Fast Fourier Transform (Dominant Cycle Detector)

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Numeric                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `windowSize` (default 64), `minPeriod` (default 4), `maxPeriod` (default 32)                      |
| **Outputs**      | Single series (Fft)                       |
| **Output range** | [minPeriod, maxPeriod]                     |
| **Warmup**       | windowSize bars                          |

### TL;DR

- The FFT indicator computes the dominant cycle period in a price series using a radix-2 Cooley-Tukey Fast Fourier Transform with a Hanning window.
- Parameterized by `windowSize` (default 64), `minPeriod` (default 4), `maxPeriod` (default 32).
- Output range: [minPeriod, maxPeriod] bars.
- Requires windowSize bars of warmup before first valid output (IsHot = true).
- True $O(N \log N)$ radix-2 FFT with bit-reversal permutation and Cooley-Tukey butterflies.

The FFT indicator computes the dominant cycle period in a price series using a true radix-2 Cooley-Tukey Fast Fourier Transform with a Hanning window. Rather than outputting frequency-domain magnitudes, it returns the estimated dominant cycle period in bars, making it directly usable as an adaptive period input for other indicators. The implementation uses an in-place iterative radix-2 FFT with bit-reversal permutation and Cooley-Tukey butterfly operations, achieving $O(N \log N)$ complexity. Parabolic interpolation on the magnitude spectrum provides sub-bin frequency resolution. With window sizes restricted to powers of two (32, 64, or 128), the indicator achieves precise cycle detection within user-specified period bounds with pre-allocated work arrays for zero-allocation streaming.

## Historical Context

The Fourier transform, formalized by Joseph Fourier (1822), decomposes any periodic signal into sinusoidal components. The Fast Fourier Transform algorithm, published by James Cooley and John Tukey in 1965, reduced the DFT from $O(N^2)$ to $O(N \log N)$ by recursively decomposing the DFT into smaller sub-problems using the "butterfly" operation pattern. The radix-2 variant requires power-of-two input lengths and uses bit-reversal permutation followed by iterative butterfly stages.

John Ehlers pioneered the application of spectral analysis to financial markets in the 1990s and 2000s, using FFT-based cycle measurement to create adaptive indicators. His work demonstrated that financial time series contain quasi-periodic cycles with time-varying periods, typically in the 6-40 bar range. The dominant cycle period, extracted via spectral peak detection, can drive adaptive moving averages (MAMA, FAMA), adaptive RSI, and other indicators that benefit from knowing the current market rhythm.

The Hanning window (also called Hann window, after Julius von Hann) is applied to reduce spectral leakage. Without windowing, the sharp truncation of a finite data segment creates artificial high-frequency components that contaminate the spectrum. The Hanning window tapers the data to zero at both ends, suppressing sidelobes at the cost of slightly wider main lobes (reduced frequency resolution).

## Architecture and Physics

The computation pipeline has five stages:

**Stage 1: Windowing** applies the Hanning window to the rolling price buffer:

$$x_w[n] = x[n] \cdot w[n], \quad w[n] = 0.5 - 0.5\cos\!\left(\frac{2\pi n}{N}\right)$$

**Stage 2: Bit-reversal permutation** reorders the windowed data according to the bit-reversed indices, preparing for in-place butterfly computation. The permutation table is pre-computed in the constructor.

**Stage 3: Cooley-Tukey butterflies** perform $\log_2(N)$ stages of butterfly operations. Each stage $s$ processes pairs of elements separated by $2^{s-1}$ positions, combining them with twiddle factors:

$$\begin{aligned}
X[i_0] &\leftarrow X[i_0] + W_N^k \cdot X[i_1] \\
X[i_1] &\leftarrow X[i_0] - W_N^k \cdot X[i_1]
\end{aligned}$$

where $W_N^k = e^{-j2\pi k/N}$ is the twiddle factor.

**Stage 4: Peak detection with parabolic interpolation** finds the bin $k^*$ with maximum squared magnitude in `[minBin, maxBin]`, then refines using a three-point parabolic fit:

$$\delta = \frac{0.5 \cdot (P[k^*-1] - P[k^*+1])}{P[k^*-1] - 2P[k^*] + P[k^*+1]}$$

**Stage 5: Period extraction and clamping** converts the refined bin index to period: $T = N / (k^* + \delta)$, clamped to `[minPeriod, maxPeriod]`.

**Window size trade-offs**: $N = 32$ gives coarse resolution (period bins spaced ~1 bar apart) but fast response; $N = 128$ gives fine resolution (~0.25 bar spacing) but sluggish adaptation. The default $N = 64$ balances resolution and responsiveness.

## Mathematical Foundation

The **Discrete Fourier Transform** for $N$ samples:

$$X[k] = \sum_{n=0}^{N-1} x[n] \cdot e^{-j 2\pi k n / N}, \quad k = 0, 1, \ldots, N-1$$

**Radix-2 Cooley-Tukey decomposition** splits the DFT into even and odd indexed sub-problems:

$$X[k] = \sum_{r=0}^{N/2-1} x[2r] \cdot W_{N/2}^{kr} + W_N^k \sum_{r=0}^{N/2-1} x[2r+1] \cdot W_{N/2}^{kr}$$

This recursion, applied iteratively with bit-reversal permutation, achieves $O(N \log N)$ complexity.

**Hanning window**:

$$w[n] = 0.5 - 0.5\cos\!\left(\frac{2\pi n}{N}\right)$$

**Frequency-to-period** mapping: bin $k$ corresponds to period $T = N/k$ bars.

**Bin range** from period bounds:

$$k_{\min} = \max\!\left(1,\; \left\lfloor\frac{N}{T_{\max}}\right\rfloor\right), \quad k_{\max} = \min\!\left(\frac{N}{2},\; \left\lfloor\frac{N}{T_{\min}}\right\rfloor\right)$$

**Power spectrum**: $P[k] = \text{Re}(X[k])^2 + \text{Im}(X[k])^2$

**Parabolic interpolation** for sub-bin precision:

$$\hat{k} = k^* + \frac{0.5 \cdot (P[k^*-1] - P[k^*+1])}{P[k^*-1] - 2P[k^*] + P[k^*+1]}$$

$$T_{\text{dominant}} = \frac{N}{\hat{k}}$$

**Parameter constraints**: `windowSize` $\in \{32, 64, 128\}$, `minPeriod` $\ge 2$, `maxPeriod` $\le N/2$.

```
FFT(source, windowSize, minPeriod, maxPeriod):
    N = windowSize
    // Stage 1: Apply Hanning window
    for n = 0 to N-1:
        workRe[n] = source[n] * hanning[n]
        workIm[n] = 0

    // Stage 2: Bit-reversal permutation
    for i = 0 to N-1:
        j = bitReverse(i)
        if j > i: swap(workRe[i], workRe[j])

    // Stage 3: Cooley-Tukey butterflies
    len = 2
    while len <= N:
        half = len / 2
        angStep = -2π / len
        for start = 0 to N-1 step len:
            for k = 0 to half-1:
                w = exp(j * angStep * k)
                butterfly(workRe, workIm, start+k, start+k+half, w)
        len *= 2

    // Stage 4: Peak detection + interpolation
    peakBin = argmax |X[k]|² for k in [minBin..maxBin]
    shift = 0.5*(P[k-1] - P[k+1]) / (P[k-1] - 2*P[k] + P[k+1])

    // Stage 5: Period extraction
    return clamp(N / (peakBin + shift), minPeriod, maxPeriod)
```


## Performance Profile

### Operation Count (Streaming Mode)

FFT (radix-2 Cooley-Tukey) performs N/2 butterflies per stage across log₂(N) stages — O(N log N) per bar.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Hanning window multiply | N | 2 cy | ~2N cy |
| Bit-reversal permutation | N | 1 cy | ~N cy |
| Butterfly operations (log₂N stages × N/2) | N/2 × log₂N | 8 cy | ~4N·log₂N cy |
| cos/sin per butterfly | N/2 × log₂N | 14 cy | ~7N·log₂N cy |
| Magnitude search (B bins) | B | 4 cy | ~4B cy |
| Parabolic interpolation | 1 | 10 cy | ~10 cy |
| **Total (N=64, B=10)** | **O(N log N)** | — | **~4362 cy** |

O(N log N) per bar. Pre-allocated work arrays ensure zero allocation in the hot path. Twiddle factor computation dominates; pre-computing sin/cos tables would reduce to ~2500 cy.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| Hanning window application | Yes | Vector multiply with precomputed weights |
| Bit-reversal permutation | No | Random access pattern; scalar only |
| Butterfly multiply-add | Yes | Complex FMA operations on paired elements |
| Magnitude squared | Yes | Vector FMA (re² + im²) |
| Peak search | Partial | Max reduction; SIMD-friendly |

Moderate SIMD potential: butterfly FMA operations are vectorizable within each stage. Bit-reversal permutation is inherently scalar. Expected 2× speedup over scalar for N=64.

## Resources

- Cooley, J.W. & Tukey, J.W. "An Algorithm for the Machine Calculation of Complex Fourier Series." *Mathematics of Computation*, 1965.
- Ehlers, J.F. "Cycle Analytics for Traders." Wiley, 2013.
- Ehlers, J.F. "Rocket Science for Traders." Wiley, 2001.
- Harris, F.J. "On the Use of Windows for Harmonic Analysis with the Discrete Fourier Transform." *Proc. IEEE*, 1978.
- Oppenheim, A.V. & Schafer, R.W. "Discrete-Time Signal Processing." 3rd edition, Pearson, 2010.
- PineScript reference: [`fft.pine`](fft.pine)
