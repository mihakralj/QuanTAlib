# DWT: Discrete Wavelet Transform

> *The discrete wavelet transform splits a signal into approximation and detail at each scale — multiresolution analysis in action.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Numeric                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `levels` (default 4), `output` (default 0)                      |
| **Outputs**      | Single series (Dwt)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `bufferSize` bars                          |
| **PineScript**   | [dwt.pine](dwt.pine)                       |

- The Discrete Wavelet Transform decomposes a price series into multi-resolution frequency components using the a trous (with holes) stationary Haar ...
- **Similar:** [CWT](../cwt/Cwt.md), [FFT](../fft/Fft.md) | **Trading note:** Discrete Wavelet Transform; decomposes signal into frequency bands at different scales.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The Discrete Wavelet Transform decomposes a price series into multi-resolution frequency components using the a trous (with holes) stationary Haar wavelet. Unlike decimated DWT, the stationary variant preserves time alignment at every scale, producing an approximation (trend) and detail coefficients (noise/cycles) at each decomposition level. Each level doubles the effective receptive field: level $L$ captures structure at $2^L$ bars. With 1-8 levels and $O(L)$ per-bar cost, DWT provides a complete multi-scale decomposition that cleanly separates trend from noise without the phase distortion inherent in moving-average cascades.

## Historical Context

Classical wavelet analysis traces to Jean Morlet's 1980s seismology work, with formal DWT construction by Stephane Mallat (1989) and Ingrid Daubechies (1988). The a trous algorithm (Holschneider et al., 1989) emerged as a shift-invariant alternative to Mallat's decimated pyramid, sacrificing orthogonality for translation invariance. For financial series where exact bar alignment matters more than basis orthogonality, the stationary variant dominates.

The Haar wavelet is the simplest possible mother wavelet: a step function that computes local averages and differences. While it lacks the smoothness of Daubechies-N wavelets, its simplicity means zero multiplications beyond the 0.5 scaling factor, and its compact support (2 taps) minimizes boundary artifacts. For price series where discontinuities (gaps, jumps) are common, the Haar basis actually outperforms smoother wavelets that assume continuous derivatives that do not exist in market data.

The multi-resolution analysis (MRA) framework guarantees perfect reconstruction: summing the approximation at any level with all detail coefficients from that level back to level 1 recovers the original signal exactly. This property is critical for attribution: the energy (variance) at each scale sums to the total variance, providing a complete variance decomposition across time scales.

## Architecture and Physics

The implementation uses an unrolled cascade of 8 levels, each conditionally executed based on the `levels` parameter. At level $j$, the approximation is the average of the current approximation and its value $2^{j-1}$ bars ago, with the detail coefficient being their difference.

**Pipeline structure:**

1. **Level 1**: Average source with 1-bar-ago source (2-bar window)
2. **Level 2**: Average level-1 approx with its 2-bar-ago value (4-bar effective window)
3. **Level $j$**: Average level-$(j-1)$ approx with its $2^{j-1}$-bar-ago value ($2^j$-bar effective window)

The `output` selector chooses which component to return: 0 for the deepest approximation (smooth trend), or 1-8 for the detail coefficient at that level. Detail level 1 captures the highest-frequency noise (2-bar oscillations); detail level $L$ captures oscillations at the $2^L$-bar scale.

**Boundary handling** uses `nz()` substitution: when historical data is unavailable at the required lag, the algorithm uses the current approximation value. This introduces a warm-up transient of $2^L$ bars for level $L$, after which the decomposition stabilizes.

**Stationarity property**: Because no downsampling occurs, every output sample aligns exactly with its input bar. This permits direct overlay of approximation on price and meaningful bar-by-bar analysis of detail coefficients.

## Mathematical Foundation

The a trous Haar wavelet decomposition at level $j$:

$$c_j[n] = \frac{1}{2}\bigl(c_{j-1}[n] + c_{j-1}[n - 2^{j-1}]\bigr)$$

$$d_j[n] = c_{j-1}[n] - c_j[n]$$

where $c_0[n] = x[n]$ is the input source.

**Perfect reconstruction** at any level $L$:

$$x[n] = c_L[n] + \sum_{j=1}^{L} d_j[n]$$

**Effective window** at level $j$ is $2^j$ bars. The Haar scaling function at level $j$ is:

$$\phi_j[n] = 2^{-j/2} \cdot \mathbf{1}_{[0,\, 2^j)}(n)$$

**Variance decomposition**: Since detail coefficients at different levels are uncorrelated:

$$\text{Var}(x) = \text{Var}(c_L) + \sum_{j=1}^{L} \text{Var}(d_j)$$

**Parameter ranges**: `levels` $\in [1, 8]$, `output` $\in [0, \text{levels}]$. Maximum lookback is $2^{\text{levels}}$ bars (256 bars at level 8).

```
DWT(source, levels, output):
    c[0] = source
    for j = 1 to levels:
        c[j] = 0.5 * (c[j-1] + c[j-1][2^(j-1)])
        d[j] = c[j-1] - c[j]
    if output == 0: return c[levels]    // approximation (trend)
    else:           return d[output]    // detail at selected level
```


## Performance Profile

### Operation Count (Streaming Mode)

DWT (Discrete Wavelet Transform) applies a 2-band filter bank recursively — O(N) per bar for a single decomposition level.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Ring buffer update | 1 | 3 cy | ~3 cy |
| Low-pass filter convolution (N/2 outputs) | N/2 * L | 2 cy | ~N*L cy |
| High-pass filter convolution (N/2 outputs) | N/2 * L | 2 cy | ~N*L cy |
| Downsampling (stride-2 access) | N | 0 cy | ~0 cy |
| **Total (N=32, L=4 Haar/D4)** | **O(N*L)** | — | **~256 cy** |

O(N*L) per bar where L = filter length. Haar wavelet (L=2) is cheapest; Daubechies D4 (L=4) doubles cost. Single decomposition level.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| LP/HP convolution | Yes | FMA inner loop; no feedback dependency |
| Downsampling | Yes | Gather with stride-2 mask |
| Multi-level recursion | Partial | Each level halves data size |

First decomposition level fully SIMD. Deeper levels become too small for effective vectorization. Expect 3× batch speedup for L1 decomposition.

## Resources

- Mallat, S. "A Theory for Multiresolution Signal Decomposition: The Wavelet Representation." IEEE Trans. PAMI, 1989.
- Daubechies, I. "Ten Lectures on Wavelets." SIAM, 1992.
- Holschneider, M. et al. "A Real-Time Algorithm for Signal Analysis with the Help of the Wavelet Transform." Wavelets: Time-Frequency Methods and Phase Space, 1989.
- Percival, D. & Walden, A. "Wavelet Methods for Time Series Analysis." Cambridge University Press, 2000.
- Gencay, R., Selcuk, F. & Whitcher, B. "An Introduction to Wavelets and Other Filtering Methods in Finance and Economics." Academic Press, 2002.