# KAISER: Kaiser Window Moving Average

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (FIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period` (default 14), `beta` (default 3.0)                      |
| **Outputs**      | Single series (Kaiser)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |

### TL;DR

- KAISER applies the Kaiser-Bessel window function as FIR filter weights, providing a single parameter ($\beta$) that continuously controls the trade...
- Parameterized by `period` (default 14), `beta` (default 3.0).
- Output range: Tracks input.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "James Kaiser gave signal processing a knob. Turn beta up, sidelobes go down, transition band widens. Turn it down, you get an SMA. One parameter to rule them all."

KAISER applies the Kaiser-Bessel window function as FIR filter weights, providing a single parameter ($\beta$) that continuously controls the trade-off between main lobe width (transition band sharpness) and sidelobe attenuation (stopband rejection). At $\beta = 0$ it degenerates to a rectangular window (SMA); at $\beta \approx 5.65$ it approximates the Blackman window; at $\beta \approx 8.6$ it matches the Hamming window's sidelobe profile. This makes KAISER the most flexible single-parameter window-based moving average, allowing traders to tune frequency selectivity without changing the window length.

## Historical Context

James F. Kaiser and Ronald W. Schafer published the Kaiser window in 1980, building on Kaiser's earlier work at Bell Labs in the 1960s. The window was motivated by a practical problem: given a desired sidelobe attenuation level, what is the shortest FIR filter that achieves it? Kaiser showed that the modified Bessel function of the first kind, $I_0$, produces near-optimal windows that closely approximate the prolate spheroidal wave functions (the theoretically optimal windows derived by Slepian in 1964) while being far simpler to compute.

The Kaiser window became the default design tool in DSP textbooks (Oppenheim & Schafer, Parks & Burrus) because of its parametric flexibility. In financial applications, this flexibility maps directly to a smoothness-responsiveness knob: low $\beta$ preserves fast price movements (less smoothing, sharper transitions), while high $\beta$ produces smoother output with greater lag (more attenuation of high-frequency price noise).

The $I_0$ Bessel function is computed via power series: $I_0(x) = \sum_{m=0}^{M} \left[\frac{(x/2)^m}{m!}\right]^2$. Twenty-five terms provide double-precision convergence for $\beta \leq 20$.

## Architecture & Physics

### 1. Bessel Function Approximation

The zeroth-order modified Bessel function $I_0(x)$ is evaluated via its power series with 25 terms. The series converges rapidly because the terms are squared factorials, guaranteeing monotonic decrease after the peak term.

### 2. Weight Computation (One-Time)

For each position $k \in [0, N-1]$, the normalized coordinate $t = 2k/(N-1) - 1$ maps to $[-1, 1]$. The Kaiser window value is:

$$
w(k) = \frac{I_0\left(\beta \sqrt{1 - t^2}\right)}{I_0(\beta)}
$$

Weights are normalized to sum to 1.0. The $\sqrt{1-t^2}$ argument is clamped to non-negative to handle floating-point edge cases.

### 3. FIR Convolution

Standard weighted sum over the circular buffer using precomputed weights. O(N) per bar.

## Mathematical Foundation

The Kaiser window function for a filter of length $N$:

$$
w[k] = \frac{I_0\left(\beta\sqrt{1 - \left(\frac{2k}{N-1} - 1\right)^2}\right)}{I_0(\beta)}, \quad k = 0, 1, \ldots, N-1
$$

where $I_0(x)$ is the zeroth-order modified Bessel function of the first kind:

$$
I_0(x) = \sum_{m=0}^{\infty} \left[\frac{(x/2)^m}{m!}\right]^2
$$

**Key $\beta$ values and their equivalences:**

| $\beta$ | Equivalent Window | Sidelobe (dB) | Transition BW |
| :---: | :--- | :---: | :---: |
| 0 | Rectangular (SMA) | $-13$ | $0.92/N$ |
| 3.0 | General-purpose | $-33$ | $2.4/N$ |
| 5.65 | Blackman-like | $-57$ | $3.6/N$ |
| 8.6 | Hamming-like | $-90$ | $5.0/N$ |

**Kaiser's empirical formulas** (for filter design):

$$
\beta = \begin{cases} 0.1102(A - 8.7) & A > 50 \\ 0.5842(A-21)^{0.4} + 0.07886(A-21) & 21 \leq A \leq 50 \\ 0 & A < 21 \end{cases}
$$

where $A = -20\log_{10}(\delta)$ is the desired stopband attenuation in dB.

**Default parameters:** `period = 14`, `beta = 3.0`, `minPeriod = 2`.

**Pseudo-code (streaming):**

```
// One-time: compute I0 and weights
bessel_i0(x):
    sum = 1.0; term = 1.0; hx = x/2
    for m = 1 to 25: term *= hx/m; sum += term²
    return sum

i0_beta = bessel_i0(beta)
for k = 0 to period-1:
    t = 2k/(N-1) - 1
    arg = sqrt(max(0, 1 - t²))
    w[k] = bessel_i0(beta * arg) / i0_beta
normalize(w)

// Per-bar convolution
buffer.push(price)
if count < period: return price
return Σ buffer[j] * w[j]
```

## Resources

- Kaiser, J.F. & Schafer, R.W. (1980). "On the Use of the I0-Sinh Window for Spectrum Analysis." *IEEE Trans. Acoust., Speech, Signal Process.*, ASSP-28(1), 105-107.
- Oppenheim, A.V. & Schafer, R.W. (2009). *Discrete-Time Signal Processing*, 3rd ed. Prentice Hall. Section 7.4.
- Slepian, D. (1964). "Prolate Spheroidal Wave Functions, Fourier Analysis and Uncertainty." *Bell System Technical Journal*, 43(6), 3009-3057.

## Performance Profile

### Operation Count (Streaming Mode)

KAISER(N, β) is a direct FIR convolution using precomputed Kaiser-Bessel window weights (computed once in the constructor via a 25-term modified Bessel function series). Each `Update()` call is a pure length-N dot product — identical in structure to any other windowed FIR.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Ring buffer push | 1 | 3 | ~3 |
| FIR dot product: N FMA (weight × value + acc) | N | 4 | ~4N |
| **Total** | **N + 1** | — | **~(4N + 3) cycles** |

O(N) per bar. For default N = 14: ~59 cycles. Weight computation at construction: O(N × 25) for I₀ series — acceptable one-time cost. WarmupPeriod = N.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| FIR convolution | Yes | AVX2 `VFMADD231PD`; weight array loaded once into registers |
| Weight array | Yes | Precomputed; no runtime transcendental cost |
| Symmetric weight exploitation | Yes | Kaiser weights are symmetric: w[i] = w[N-1-i]; SIMD can fuse pairs |
| Cross-bar independence | Yes | Each bar fully independent; outer-loop SIMD viable |

Due to symmetric weights (w[i] = w[N-1-i]), the FIR can be folded: each pair (oldest + newest) shares the same weight, halving the multiply count to N/2 FMA. AVX2 batch throughput: approximately N/8 cycles per bar — for N = 14, ~1.75 cycles/bar at peak.
