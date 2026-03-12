# TUKEY_W: Tukey (Tapered Cosine) Window Moving Average

> *John Tukey designed a window with a knob that goes from 'do nothing' to 'full Hann' in one parameter. Set alpha to 0.5 and you get the pragmatist's compromise: flat where it matters, tapered where it would otherwise ring.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (FIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period` (default 20), `alpha` (default 0.5)                      |
| **Outputs**      | Single series (Tukey_w)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [tukey_w.pine](tukey_w.pine)                       |
| **Signature**    | [tukey_w_signature](tukey_w_signature.md) |

- TUKEY_W applies the Tukey (tapered cosine) window as FIR filter weights, offering a single parameter $\alpha$ that controls the fraction of the win...
- Parameterized by `period` (default 20), `alpha` (default 0.5).
- Output range: Tracks input.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

TUKEY_W applies the Tukey (tapered cosine) window as FIR filter weights, offering a single parameter $\alpha$ that controls the fraction of the window that is cosine-tapered. At $\alpha = 0$, the window is rectangular (SMA). At $\alpha = 1$, it becomes the Hann window. The default $\alpha = 0.5$ tapers 25% at each edge while keeping the central 50% flat at unity, combining the passband efficiency of the rectangular window with the sidelobe suppression of cosine tapering. This makes Tukey the default "when in doubt" window in spectral analysis, and by extension, a sensible default for window-based moving averages.

## Historical Context

John Wilder Tukey (1915-2000) introduced the tapered cosine window as part of his extensive work on spectral analysis, culminating in the landmark *Power Spectral Analysis and Its Applications* textbook with Blackman (1958). Tukey recognized that the rectangular window's sharp edges cause spectral leakage (Gibbs phenomenon), while fully tapered windows like Hann sacrifice too much effective window length. The tapered cosine compromise preserves most of the rectangular window's frequency resolution (through the flat center) while controlling leakage through the cosine-tapered edges.

The Tukey window is also known as the "cosine-tapered window" or "split-cosine-bell window" in the spectral analysis literature. The parameter $\alpha$ is sometimes called the "taper ratio" or "rolloff fraction." In the acoustics and seismology communities, it is standard practice to start with $\alpha = 0.5$ and adjust based on the leakage characteristics of the specific data.

For financial applications, the Tukey window's parametric nature offers a practical advantage over fixed windows: a trader can adjust $\alpha$ to control how much edge attenuation is applied. Low $\alpha$ (near 0) prioritizes responsiveness (less lag), while high $\alpha$ (near 1) prioritizes smoothness (less noise).

## Architecture & Physics

### 1. Piecewise Weight Function

The Tukey window divides the $N$-point window into three regions:

- **Left taper** ($0 \leq n < \alpha(N-1)/2$): Raised-cosine ramp from 0 to 1.
- **Flat center** ($\alpha(N-1)/2 \leq n \leq (N-1)(1-\alpha/2)$): Constant weight of 1.
- **Right taper** ($(N-1)(1-\alpha/2) < n \leq N-1$): Raised-cosine ramp from 1 to 0.

### 2. Normalization

Weights are normalized by their sum, which depends on $\alpha$:

$$
\sum w = N - \alpha(N-1)/2 \cdot (1-2/\pi)
$$

(approximately, for large $N$).

### 3. FIR Convolution

Standard weighted convolution. O(N) per bar. The flat center section allows paired SIMD processing of constant-weight elements.

## Mathematical Foundation

For a window of length $N$, sample index $n \in [0, N-1]$, and taper fraction $\alpha \in [0, 1]$:

$$
w[n] = \begin{cases} \frac{1}{2}\left(1 - \cos\!\left(\frac{2\pi n}{\alpha(N-1)}\right)\right) & 0 \leq n < \frac{\alpha(N-1)}{2} \\ 1 & \frac{\alpha(N-1)}{2} \leq n \leq (N-1)\left(1 - \frac{\alpha}{2}\right) \\ \frac{1}{2}\left(1 - \cos\!\left(\frac{2\pi(N-1-n)}{\alpha(N-1)}\right)\right) & (N-1)\left(1 - \frac{\alpha}{2}\right) < n \leq N-1 \end{cases}
$$

**Special cases:**

| $\alpha$ | Window | Properties |
| :---: | :--- | :--- |
| 0 | Rectangular (SMA) | Max resolution, worst leakage |
| 0.5 | Half-tapered (default) | Good compromise |
| 1.0 | Hann | Best leakage suppression, widest main lobe |

**Frequency response properties (approximate for $N \gg 1$):**

| $\alpha$ | Main lobe width | First sidelobe (dB) |
| :---: | :---: | :---: |
| 0 | $2/N$ | $-13$ |
| 0.25 | $2.2/N$ | $-19$ |
| 0.5 | $2.5/N$ | $-26$ |
| 0.75 | $2.8/N$ | $-29$ |
| 1.0 | $3.2/N$ | $-32$ |

**Normalized output:**

$$
\text{TUKEY\_W}_t = \frac{\sum_{n=0}^{N-1} w[n] \cdot x_{t-n}}{\sum_{n=0}^{N-1} w[n]}
$$

**Default parameters:** `period = 20`, `alpha = 0.5`, `minPeriod = 2`.

**Pseudo-code (streaming):**

```
N = period - 1
aN = alpha * N
sumWV = 0; sumW = 0

for i = 0 to N:
    w = 1.0
    if aN > 0:
        if i < aN/2:
            w = 0.5 * (1 - cos(2π*i / aN))
        else if i > N - aN/2:
            w = 0.5 * (1 - cos(2π*(N-i) / aN))
    sumWV += src[i] * w
    sumW  += w
return sumWV / sumW
```

## Resources

- Tukey, J.W. (1967). "An Introduction to the Calculations of Numerical Spectrum Analysis." In *Spectral Analysis of Time Series*, ed. B. Harris. Wiley. pp. 25-46.
- Blackman, R.B. & Tukey, J.W. (1958). *The Measurement of Power Spectra from the Point of View of Communications Engineering*. Dover.
- Harris, F.J. (1978). "On the Use of Windows for Harmonic Analysis with the Discrete Fourier Transform." *Proceedings of the IEEE*, 66(1), 51-83.

## Performance Profile

### Operation Count (Streaming Mode)

TUKEY_W(N) is a direct FIR convolution using precomputed Tukey biweight window weights: w(k) = (1 − (2k/(N−1) − 1)²)² for |u| ≤ 1, 0 otherwise. The biweight is always non-negative, with a smooth quartic rolloff to zero at the edges.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Ring buffer push | 1 | 3 | ~3 |
| FIR dot product: N FMA | N | 4 | ~4N |
| **Total** | **N + 1** | — | **~(4N + 3) cycles** |

O(N) per bar. For default N = 14: ~59 cycles. Non-negative quartic weights; no special sign handling. WarmupPeriod = N.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| FIR convolution | Yes | `VFMADD231PD`; all weights non-negative |
| Tukey symmetric window | Yes | Symmetric: fold to ⌈N/2⌉ unique weights |
| Cross-bar independence | Yes | 4 output bars per AVX2 pass |

Tukey biweight shares the same symmetric FIR structure as Kaiser and Parzen. Symmetric folding halves FMA count to ⌈N/2⌉. AVX2 batch throughput: ~N/8 cycles per bar.
