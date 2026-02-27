# PARZEN: Parzen (de la Vallée-Poussin) Window Moving Average

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (FIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period` (default 14)                      |
| **Outputs**      | Single series (Parzen)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |

### TL;DR

- PARZEN applies the Parzen (de la Vallée-Poussin) window function as FIR filter weights, producing a moving average with exceptional sidelobe suppre...
- Parameterized by `period` (default 14).
- Output range: Tracks input.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "Emanuel Parzen convolved two triangular windows and got a piecewise cubic with zero sidelobe discontinuity. When your window function is its own proof of smoothness, the spectral leakage has nowhere to hide."

PARZEN applies the Parzen (de la Vallée-Poussin) window function as FIR filter weights, producing a moving average with exceptional sidelobe suppression ($-24$ dB/octave rolloff) and a smooth bell-shaped kernel. The Parzen window is the self-convolution of two triangular (Bartlett) windows at half-length, which guarantees continuous first and second derivatives at all points. This makes it one of the few windows whose frequency response has no discontinuities in its first three derivatives, yielding the fastest sidelobe decay rate among common windows without requiring the computational cost of Bessel functions (Kaiser) or specialized polynomials (Henderson).

## Historical Context

Emanuel Parzen (1929-2016) introduced the window in a 1961 paper on spectral estimation in *Technometrics*, though the underlying function was studied earlier by de la Vallée-Poussin in the context of Fourier series summability. Parzen's contribution was to recognize the window's optimality properties for spectral density estimation: among all non-negative windows with continuous derivatives up to order 2, the Parzen window minimizes the integrated squared bias of the spectral estimate.

The Parzen window's construction as a convolution of two Bartlett windows gives it a natural interpretation: it is equivalent to computing the SMA of an SMA of half the period, twice. This "double triangular smoothing" produces the piecewise cubic shape without explicit polynomial computation. In the spectral domain, the convolution translates to multiplication: the Parzen frequency response is the square of the Bartlett frequency response, which explains the doubled sidelobe rolloff rate ($-24$ dB/octave vs. $-12$ dB/octave for Bartlett).

Compared to competing windows, Parzen trades main-lobe width for sidelobe suppression. Its main lobe is wider than Hann or Hamming (meaning more lag in the time domain), but its sidelobes decay faster than any other polynomial-based window. For financial applications where smooth trend extraction matters more than sharp frequency cutoff, this trade-off favors Parzen.

## Architecture & Physics

### 1. Piecewise Cubic Weight Function

The Parzen window is defined in two regions based on the normalized coordinate $|u| = |k - (N-1)/2| / ((N-1)/2)$:

- **Inner region** ($|u| \leq 0.5$): Cubic spline with positive curvature tapering from the peak.
- **Outer region** ($0.5 < |u| \leq 1.0$): Cubic taper to zero at the window edge.

The two pieces join with continuous first and second derivatives at $|u| = 0.5$, ensuring no spectral artifacts from weight discontinuities.

### 2. Weight Normalization

Weights are normalized to sum to 1.0. Because all Parzen weights are non-negative, the filter output is always a convex combination of input prices (no overshoot possible from negative weights).

### 3. FIR Convolution

Standard weighted convolution over the circular buffer. O(N) per bar. The symmetric structure allows paired-element optimization for SIMD.

## Mathematical Foundation

For a window of length $N$, with normalized coordinate $u = (k - (N-1)/2) / ((N-1)/2)$, $k = 0, \ldots, N-1$:

$$
w(k) = \begin{cases} 1 - 6u^2 + 6|u|^3 & |u| \leq 0.5 \\ 2(1 - |u|)^3 & 0.5 < |u| \leq 1.0 \\ 0 & |u| > 1.0 \end{cases}
$$

**Frequency response properties:**

| Property | Value |
| :--- | :--- |
| Main lobe width ($-3$ dB) | $\approx 2.0/N$ |
| First sidelobe | $-53$ dB |
| Sidelobe rolloff | $-24$ dB/octave |
| All weights non-negative | Yes |

**Equivalence to double convolution:**

$$
w_{\text{Parzen}}[n] = w_{\text{Bartlett}}[n] * w_{\text{Bartlett}}[n]
$$

where $*$ denotes discrete convolution and the Bartlett windows are of length $N/2$.

**Normalized output:**

$$
\text{PARZEN}_t = \frac{\sum_{k=0}^{N-1} w[k] \cdot x_{t-k}}{\sum_{k=0}^{N-1} w[k]}
$$

**Default parameters:** `period = 14`, `minPeriod = 2`.

**Pseudo-code (streaming):**

```
// One-time weight computation
half_N = (period - 1) / 2
for k = 0 to period-1:
    u = (k - half_N) / half_N
    abs_u = |u|
    if abs_u <= 0.5:
        w[k] = 1 - 6*abs_u² + 6*abs_u³
    else if abs_u <= 1.0:
        w[k] = 2*(1 - abs_u)³
    else:
        w[k] = 0
normalize(w)

// Per-bar convolution
buffer.push(price)
if count < period: return price
return Σ buffer[j] * w[j]
```

## Resources

- Parzen, E. (1961). "Mathematical Considerations in the Estimation of Spectra." *Technometrics*, 3(2), 167-190.
- Harris, F.J. (1978). "On the Use of Windows for Harmonic Analysis with the Discrete Fourier Transform." *Proceedings of the IEEE*, 66(1), 51-83.
- Nuttall, A.H. (1981). "Some Windows with Very Good Sidelobe Behavior." *IEEE Trans. Acoust., Speech, Signal Process.*, 29(1), 84-91.

## Performance Profile

### Operation Count (Streaming Mode)

PARZEN(N) is a direct FIR convolution using precomputed Parzen (de la Vallée Poussin) window weights. The Parzen window is piecewise cubic — always non-negative, infinite differentiability at endpoints — with zero negative sidelobes. Each `Update()` is a pure N-tap FMA dot product.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Ring buffer push | 1 | 3 | ~3 |
| FIR dot product: N FMA | N | 4 | ~4N |
| **Total** | **N + 1** | — | **~(4N + 3) cycles** |

O(N) per bar. For default N = 14: ~59 cycles. No negative weights — normalization is a simple sum. WarmupPeriod = N.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| FIR convolution | Yes | AVX2 `VFMADD231PD`; all weights non-negative |
| Parzen symmetry | Yes | Symmetric window: w[i] = w[N-1-i]; fold for N/2 FMAs |
| Cross-bar independence | Yes | Full outer-loop SIMD viable |

Symmetric folding halves the multiply count. AVX2 batch throughput: ~N/8 cycles per output bar. Non-negative weights avoid any masking overhead, giving slightly cleaner codegen than sinc-based filters.
