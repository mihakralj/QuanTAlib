# SWMA: Symmetric Weighted Moving Average

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (FIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period` (default 4)                      |
| **Outputs**      | Single series (Swma)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |

### TL;DR

- SWMA applies triangular (symmetric) weights that peak at the center of the window and taper linearly to the edges.
- Parameterized by `period` (default 4).
- Output range: Tracks input.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "Take the SMA of an SMA and you get a triangular filter. It is the simplest possible smoothing kernel that has zero phase distortion and no frequency-domain discontinuities. Sometimes simple is exactly what you need."

SWMA applies triangular (symmetric) weights that peak at the center of the window and taper linearly to the edges. For period $N$, the weight at position $i$ is $w(i) = (N/2 + 1) - |i - N/2|$, producing a tent-shaped kernel. This is mathematically equivalent to convolving two rectangular windows (SMA of SMA), giving SWMA a frequency response that is the square of the SMA's sinc-like response. The result is smoother than SMA with better sidelobe suppression, at the cost of slightly more lag.

## Historical Context

The symmetric (triangular) weighted average is one of the oldest smoothing methods in statistics, predating modern signal processing by centuries. Its equivalence to the double-application of the simple moving average was recognized by Macaulay (1931) in his NBER monograph on time-series smoothing. The TRIMA (Triangular Moving Average) implemented elsewhere in QuanTAlib is the same mathematical operation computed via double SMA composition.

In PineScript, `ta.swma` refers specifically to the 4-point variant with weights $[1, 2, 2, 1]/6$, which is a special case of the general symmetric weighted average. QuanTAlib's SWMA generalizes this to arbitrary periods.

The triangular kernel has a natural Bayesian interpretation: if you believe the "true" signal is equally likely to be any value in a window of width $N/2$, and your observation window is also $N/2$, the posterior belief about the signal value is triangular. This makes SWMA the optimal Bayesian filter under uniform prior and uniform observation noise assumptions.

## Architecture & Physics

### 1. Weight Computation

For a window of length $N$ with half-width $h = (N-1)/2$:

$$
w(i) = h + 1 - |i - h|, \quad i = 0, 1, \ldots, N-1
$$

Weights form a triangle peaking at the center. For even $N$, the peak is a plateau of two equal values.

### 2. Normalized Weighted Sum

$$
\text{SWMA} = \frac{\sum_{i=0}^{N-1} w(i) \cdot x_{t-i}}{\sum_{i=0}^{N-1} w(i)}
$$

The weight sum equals $(h+1)^2$ for odd $N$ and $h(h+2)+1$ for even $N$.

### 3. Equivalence to Double SMA

SWMA(N) produces the same output as SMA(M) applied to SMA(M) where $M = \lceil N/2 \rceil$. This means the streaming implementation can compose two SMA instances for O(1) updates, rather than O(N) convolution.

## Mathematical Foundation

The triangular window for length $N$, with $h = (N-1)/2$:

$$
w[i] = h + 1 - |i - h|, \quad i = 0, \ldots, N-1
$$

**Frequency response:**

$$
H_{\text{SWMA}}(f) = H_{\text{SMA}}^2(f) = \left[\frac{\sin(\pi f M)}{\pi f M}\right]^2
$$

where $M = \lceil N/2 \rceil$. The squared sinc provides:

| Property | SMA | SWMA |
| :--- | :---: | :---: |
| First zero | $1/N$ | $2/N$ |
| First sidelobe | $-13$ dB | $-26$ dB |
| Rolloff rate | $-6$ dB/octave | $-12$ dB/octave |
| Passband ripple | Moderate | Low |

**Weight sum (closed form):**

For odd $N = 2m+1$: $\sum w = (m+1)^2$

For even $N = 2m$: $\sum w = m(m+1)$

**PineScript special case:** `ta.swma` uses $N = 4$, $h = 1.5$, weights $= [1, 2, 2, 1]$, $\sum w = 6$.

**Default parameters:** `period = 4`, `minPeriod = 2`.

**Pseudo-code (streaming):**

```
half = (period - 1) / 2.0
sumWV = 0; sumW = 0
for i = 0 to period-1:
    w = half + 1 - |i - half|
    sumWV += src[i] * w
    sumW  += w
return sumWV / sumW
```

## Resources

- Macaulay, F.R. (1931). *The Smoothing of Time Series.* National Bureau of Economic Research. Chapter 3: Moving Averages and Their Properties.
- Oppenheim, A.V. & Schafer, R.W. (2009). *Discrete-Time Signal Processing*, 3rd ed. Prentice Hall. Section 5.6: The Bartlett (Triangular) Window.
- Murphy, J.J. (1999). *Technical Analysis of the Financial Markets*. New York Institute of Finance. Chapter 9: Moving Averages.

## Performance Profile

### Operation Count (Streaming Mode)

SWMA(N) is an O(N) FIR convolution using symmetric triangular weights (ascending then descending). Weights are precomputed at construction and normalized to sum = 1. The triangular shape gives the center bar the highest weight.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Ring buffer push | 1 | 3 | ~3 |
| FIR dot product: N FMA | N | 4 | ~4N |
| **Total** | **N + 1** | — | **~(4N + 3) cycles** |

O(N) per bar. For default N = 14: ~59 cycles. Triangular weights are strictly positive — numerically clean. WarmupPeriod = N.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| FIR convolution | Yes | `VFMADD231PD`; all-positive weights |
| Symmetric triangular window | Yes | Fold: only ⌈N/2⌉ unique weights; halves FMA count |
| Cross-bar independence | Yes | 4 output bars per AVX2 pass |

Symmetric folding reduces the effective FMA count to ⌈N/2⌉. For N = 14: 7 FMAs per bar. AVX2 batch throughput: ~N/8 cycles per bar. Among the windowed FIR filters, SWMA has the fewest effective operations due to its simple triangular shape.
