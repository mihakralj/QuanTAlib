# LANCZOS: Lanczos (Sinc) Window Moving Average

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (FIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period` (default 14)                      |
| **Outputs**      | Single series (Lanczos)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |
| **Signature**    | [lanczos_signature](lanczos_signature.md) |

### TL;DR

- LANCZOS applies the normalized sinc function $\text{sinc}(x) = \sin(\pi x)/(\pi x)$ as a symmetric FIR window, producing a moving average with near...
- Parameterized by `period` (default 14).
- Output range: Tracks input.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "Cornelius Lanczos used the sinc function to reconstruct band-limited signals from discrete samples. Apply it to price data and you get a moving average that respects the Nyquist limit while your competitors are still using SMAs."

LANCZOS applies the normalized sinc function $\text{sinc}(x) = \sin(\pi x)/(\pi x)$ as a symmetric FIR window, producing a moving average with near-ideal low-pass frequency characteristics. The sinc function is the impulse response of the perfect brick-wall low-pass filter; windowing it to finite length trades sharp cutoff for practical realizability. The result is a smoother with minimal Gibbs phenomenon ringing and excellent passband flatness, at the cost of small negative sidelobe weights that can cause minor overshooting on sharp price discontinuities.

## Historical Context

Cornelius Lanczos (1893-1974) was a Hungarian-American mathematician and physicist who made foundational contributions to applied mathematics, including the Lanczos algorithm for eigenvalue computation, the Lanczos tau method for differential equations, and the Lanczos sigma factor for reducing Gibbs phenomenon in Fourier series. His 1956 book *Applied Analysis* introduced the sinc-based window that bears his name.

The Lanczos window is the simplest sinc-kernel window: a single lobe of the sinc function, truncated to the filter length. Higher-order Lanczos kernels (Lanczos-2, Lanczos-3) multiply $\text{sinc}(x) \cdot \text{sinc}(x/a)$ for sharper cutoff and are widely used in image resampling (e.g., the default resizer in FFmpeg and ImageMagick). For financial time series, the first-order Lanczos window provides a good balance between frequency selectivity and computational simplicity.

The key property distinguishing Lanczos from other window-based MAs is the sinc function's direct relationship to the ideal low-pass filter. While Hann, Hamming, and Blackman windows are ad-hoc designs optimized for sidelobe suppression, the Lanczos window starts from the theoretically optimal impulse response and truncates it, preserving the passband flatness that other windows sacrifice for sidelobe control.

## Architecture & Physics

### 1. Weight Computation (One-Time)

For each position $k \in [0, N-1]$, the normalized coordinate $x = 2k/(N-1) - 1$ maps to $[-1, 1]$. The Lanczos window value is:

$$
w(k) = \text{sinc}(x) = \frac{\sin(\pi x)}{\pi x}, \quad w(0) = 1
$$

The sinc function produces negative values for $|x| > 1$ in the general case, but within the $[-1, 1]$ window, negative weights appear only near the edges where $|x|$ approaches 1. These negative weights are retained (not clamped) for frequency-domain fidelity.

### 2. Normalization

Weights are normalized to sum to 1.0, ensuring the filter preserves constant (DC) signals exactly.

### 3. FIR Convolution

Standard weighted convolution over the circular buffer. O(N) per bar. The symmetric weight structure enables potential paired-multiplication optimization (summing symmetric buffer pairs before multiplying by the shared weight).

## Mathematical Foundation

The Lanczos window for a filter of length $N$:

$$
w[k] = \text{sinc}\!\left(\frac{2k}{N-1} - 1\right), \quad k = 0, 1, \ldots, N-1
$$

where:

$$
\text{sinc}(x) = \begin{cases} 1 & x = 0 \\ \frac{\sin(\pi x)}{\pi x} & x \neq 0 \end{cases}
$$

**Frequency response:** The continuous sinc function has an ideal rectangular frequency response (brick-wall low-pass). Truncation introduces sidelobes at approximately $-13$ dB for the first sidelobe (comparable to the rectangular window), with subsequent sidelobes decaying as $1/f$. The passband flatness is superior to most other windows of the same length.

**Normalized output:**

$$
\text{LANCZOS}_t = \frac{\sum_{k=0}^{N-1} w[k] \cdot x_{t-k}}{\sum_{k=0}^{N-1} w[k]}
$$

**Default parameters:** `period = 14`, `minPeriod = 2`.

**Pseudo-code (streaming):**

```
// One-time weight computation
for k = 0 to period-1:
    x = 2k/(N-1) - 1
    if |x| < 1e-10:
        w[k] = 1.0
    else:
        w[k] = sin(π·x) / (π·x)
normalize(w)

// Per-bar convolution
buffer.push(price)
if count < period: return price
return Σ buffer[j] * w[j]
```

## Resources

- Lanczos, C. (1956). *Applied Analysis*. Prentice-Hall. Reprinted by Dover, 1988.
- Duchon, C.E. (1979). "Lanczos Filtering in One and Two Dimensions." *Journal of Applied Meteorology*, 18(8), 1016-1022.
- Oppenheim, A.V. & Schafer, R.W. (2009). *Discrete-Time Signal Processing*, 3rd ed. Prentice Hall. Section 7.2: Properties of Commonly Used Windows.
- Turkowski, K. (1990). "Filters for Common Resampling Tasks." In *Graphics Gems I*, Academic Press. pp. 147-165.

## Performance Profile

### Operation Count (Streaming Mode)

LANCZOS(N) is a direct FIR convolution using precomputed sinc weights. The sinc function produces both positive and positive-then-negative lobes; weights are sign-preserving and normalized. Each `Update()` is a pure N-tap dot product.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Ring buffer push | 1 | 3 | ~3 |
| FIR dot product: N FMA | N | 4 | ~4N |
| **Total** | **N + 1** | — | **~(4N + 3) cycles** |

O(N) per bar. For default N = 14: ~59 cycles. Sinc weights are computed once at construction (involves `Math.Sin`/division per weight — one-time O(N) cost). WarmupPeriod = N.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| FIR convolution | Yes | `VFMADD231PD`; negative-sidelobe weights handled naturally |
| Sinc symmetry | Yes | sinc(x) is symmetric; fold the dot product for N/2 FMAs |
| Cross-bar independence | Yes | Batch outer loop: process 4 output bars per AVX2 iteration |
| Negative weight handling | Yes | Signed FMA; no branch needed |

AVX2 batch throughput with symmetric folding: ~N/8 cycles per output bar. For N = 14 over 1000-bar batch: ~1750 cycles vs ~59000 cycles scalar (~34× speedup at peak, memory-limited at larger N).
