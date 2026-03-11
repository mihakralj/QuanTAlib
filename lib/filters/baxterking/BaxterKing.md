# BK: Baxter-King Band-Pass Filter

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Filter                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `pLow` (default 6), `pHigh` (default 32), `k` (default 12)                      |
| **Outputs**      | Single series (BaxterKing)                       |
| **Output range** | Oscillates around zero           |
| **Warmup**       | `2K+1` bars (default 25)         |
| **PineScript**   | [baxterking.pine](baxterking.pine)                       |

- The **Baxter-King Band-Pass Filter** is a symmetric finite impulse response (FIR) filter that approximates the ideal spectral band-pass by truncati...
- Parameterized by `plow` (default 6), `phigh` (default 32), `k` (default 12).
- Output range: Oscillates around zero (extracts cyclical component).
- Requires `2K+1` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "The business cycle is whatever remains after you strip away the trend and the noise. Baxter and King figured out the stripping."

The **Baxter-King Band-Pass Filter** is a symmetric finite impulse response (FIR) filter that approximates the ideal spectral band-pass by truncating the infinite sinc-like impulse response at lag $K$ and normalizing the weights to sum to zero. It extracts cyclical components with periodicities between $p_L$ (low) and $p_H$ (high) bars, rejecting both the DC trend and high-frequency noise. Output oscillates around zero with a fixed delay of $K$ bars.

## Historical Context

Baxter and King (1999) developed their band-pass filter to solve a specific problem in macroeconomics: isolating business cycle fluctuations from GDP and other economic time series. The NBER defines business cycles as fluctuations with periodicities between 6 and 32 quarters. Extracting exactly those frequencies from noisy economic data requires a band-pass filter.

The ideal band-pass filter has an infinite impulse response (the inverse Fourier transform of a rectangular window in frequency). In practice, you must truncate it. Baxter and King showed that truncating at lag $K$ and then normalizing the weights so they sum to zero (ensuring DC rejection) produces a filter with excellent frequency-domain properties for moderate $K$. The resulting filter is symmetric, which means zero phase distortion: peaks and troughs in the extracted cycle align exactly with the corresponding features in the original data (subject to the $K$-bar output delay).

Christiano and Fitzgerald (2003) later proposed an asymmetric alternative that avoids the endpoint data loss inherent in symmetric filters. However, the BK filter remains the standard reference in econometrics because its symmetry guarantees zero phase shift and its FIR structure guarantees stability.

In trading applications, BK is useful for extracting swing-trade-frequency cycles. Set $p_L = 6$ and $p_H = 32$ (the NBER defaults) for daily bars to isolate cycles between roughly 1 and 6 weeks. The $K$ parameter controls quality vs. data loss: larger $K$ yields sharper spectral cutoffs but loses $K$ bars on each end.

## Architecture and Physics

### 1. Ideal Band-Pass Weights

The ideal band-pass filter for angular frequencies between $a = 2\pi/p_H$ (low cutoff) and $b = 2\pi/p_L$ (high cutoff) has impulse response coefficients:

$$B_0 = \frac{b - a}{\pi}$$

$$B_j = \frac{\sin(jb) - \sin(ja)}{\pi j}, \quad j = 1, 2, \ldots$$

These are the coefficients of the inverse Fourier transform of the rectangular frequency window $[a, b]$.

### 2. Truncation at Lag K

The infinite sequence $\{B_j\}$ is truncated at $j = K$, retaining only $2K + 1$ weights. The truncation introduces Gibbs-phenomenon ripple in the frequency response. Larger $K$ reduces this ripple (sharper cutoffs) at the cost of losing $K$ observations from each end of the series.

### 3. BK Normalization (DC Rejection)

The truncated weights do not sum to zero. The BK normalization subtracts a constant $\theta$ from each weight:

$$\theta = \frac{B_0 + 2\sum_{j=1}^{K} B_j}{2K + 1}$$

$$w_j = B_j - \theta, \quad j = 0, 1, \ldots, K$$

The resulting weights $\{w_j\}$ sum exactly to zero, guaranteeing that a constant (DC) input maps to zero output. This is the defining property of the BK filter.

### 4. Symmetric FIR Convolution

The filter output at time $t$ is:

$$y_t = \sum_{j=-K}^{K} w_{|j|} \cdot x_{t-j}$$

Because $w_j = w_{-j}$ (symmetry), the filter has zero phase shift. The output at time $t$ depends on $K$ future and $K$ past values, so in real-time streaming the output is delayed by $K$ bars.

### Inertial Physics

- **Zero DC Gain**: $\sum w_j = 0$ by construction. Constant input yields zero output.
- **Zero Phase**: Symmetric weights $\Rightarrow$ linear phase $\Rightarrow$ zero group delay at all frequencies (after accounting for the $K$-bar shift).
- **FIR Stability**: No feedback (no poles). Always stable regardless of parameters.
- **Gibbs Ripple**: Truncation of the ideal response causes passband ripple proportional to $1/K$. The BK normalization mitigates the DC component of this ripple.

## Mathematical Foundation

### Weight Computation

Given parameters $p_L$ (low period), $p_H$ (high period), $K$ (half-length):

$$a = \frac{2\pi}{p_H}, \quad b = \frac{2\pi}{p_L}$$

$$B_0 = \frac{b - a}{\pi}$$

$$B_j = \frac{\sin(jb) - \sin(ja)}{\pi j}, \quad j = 1, \ldots, K$$

$$\theta = \frac{B_0 + 2\sum_{j=1}^{K} B_j}{2K + 1}$$

$$w[K \pm j] = B_j - \theta$$

### Transfer Function

The z-domain transfer function is a $(2K)$-th order FIR:

$$H(z) = \sum_{n=0}^{2K} w[n] \cdot z^{-n}$$

With $w[n] = w[2K - n]$ (Type I linear-phase FIR).

### Default Parameters

| Parameter | Default | Purpose |
| :--- | :--- | :--- |
| `pLow` | 6 | Minimum period of passband (bars). Cycles faster than this are rejected. |
| `pHigh` | 32 | Maximum period of passband (bars). Cycles slower than this are rejected. |
| `K` | 12 | Filter half-length (number of leads/lags). Controls sharpness vs. data loss. |

The NBER-standard defaults (6, 32) target business cycle frequencies for quarterly data. For daily trading bars, typical choices are `pLow=6..10`, `pHigh=20..40`, `K=8..16`.

## Performance Profile

### Operation Count (Streaming Mode)

Baxter-King is a symmetric FIR band-pass filter; the full symmetric window covers 2K+1 points (K leads + K lags + center). The streaming implementation stores history and updates with a dot product.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| RingBuffer update | 1 | ~3 cy | ~3 cy |
| Dot product over 2K+1 weights (FMA) | 2K+1 | ~5 cy | ~305 cy (K=30) |
| **Total (K=30)** | **62** | — | **~308 cycles** |

O(K) per bar. Precomputed symmetric weights; full-window convolution each bar. ~308 cycles for K=30.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| Dot-product convolution | Yes | `Vector<double>` 4x speedup; weights symmetric (reduce by 2x) |
| History window | Partial | Contiguous RingBuffer layout required for SIMD reads |

AVX2 dot product: ~80 cy for K=30 (4x better than scalar).

| Metric | Impact | Notes |
| :--- | :--- | :--- |
| **Throughput** | O(K)/bar | Single weighted sum over $2K+1$ values per bar. |
| **Allocations** | 0 | Precomputed weights, RingBuffer, zero heap allocation in hot path. |
| **SIMD** | Not applicable | $K$ is typically small (8-20); SIMD overhead exceeds benefit. |
| **Accuracy** | 8/10 | Excellent band-pass approximation for $K \geq 12$. |
| **Timeliness** | 5/10 | Fixed $K$-bar delay. Inherent to symmetric FIR design. |
| **Smoothness** | 9/10 | Symmetric FIR, zero phase, no ringing from poles. |
| **DC Rejection** | 10/10 | Perfect by construction (weights sum to zero). |

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **Pine Script** | Validated | Ported from BaxterKing PineScript v6 reference implementation. |
| **Synthetic** | Validated | Multi-frequency sine waves confirm in-band pass, out-of-band rejection. |
| **Self-Consistency** | Validated | Streaming, batch, span, and eventing modes produce identical results. |
| **DC Rejection** | Validated | Constant input produces exactly zero output after warmup. |
| **Symmetry** | Validated | Reversed input produces reversed output (symmetric FIR property). |
| **Weight Sum** | Validated | Weights sum to zero within machine epsilon. |
| **Linear Ramp** | Validated | Linear input produces zero output (weights sum to zero). |

## Common Pitfalls

1. **Expecting overlay behavior**: BK output oscillates around zero: it extracts the cyclical component, not a smoothed price. Plot in a separate window (`SeparateWindow = true`).

2. **Ignoring the K-bar delay**: The symmetric filter uses $K$ future and $K$ past values. In real-time streaming, the output at bar $t$ actually represents the cycle at bar $t - K$. This is an inherent cost of zero-phase filtering.

3. **K too small**: With $K < 6$, the truncated weights poorly approximate the ideal band-pass. Gibbs ripple becomes severe, and out-of-band energy leaks through. Use $K \geq 12$ for clean extraction.

4. **K too large**: Each bar of $K$ loses one observation from each end of the series. For a 250-bar daily series with $K = 50$, you lose 100 bars (40%). Balance sharpness against data availability.

5. **pLow too close to pHigh**: If $p_H - p_L < 4$, the passband is extremely narrow. The truncated filter cannot resolve such narrow bands cleanly. Widen the passband or use a higher-order filter (e.g., Butterworth BPF).

6. **Confusing with Christiano-Fitzgerald**: The CF filter is asymmetric and does not require discarding endpoint data, but it introduces phase distortion. BK and CF answer different engineering trade-offs.

7. **Using for tick data**: BK was designed for regularly-spaced time series (daily, weekly, quarterly). Irregularly-spaced tick data violates the uniform sampling assumption. Resample to fixed intervals before applying BK.

## References

1. Baxter, M. and R.G. King. "Measuring Business Cycles: Approximate Band-Pass Filters for Economic Time Series." Review of Economics and Statistics 81(4), 575-593, 1999.
2. Christiano, L.J. and T.J. Fitzgerald. "The Band Pass Filter." International Economic Review 44(2), 435-465, 2003.
3. Stock, J.H. and M.W. Watson. "Business Cycle Fluctuations in US Macroeconomic Time Series." Handbook of Macroeconomics, Vol. 1, 1999.
4. Burns, A.F. and W.C. Mitchell. "Measuring Business Cycles." NBER, 1946.

## Usage

```csharp
using QuanTAlib;

// Default: pLow=6, pHigh=32, K=12 (NBER business cycle parameters)
var bk = new BaxterKing(pLow: 6, pHigh: 32, k: 12);

// Streaming update
var result = bk.Update(new TValue(DateTime.UtcNow, price));
// result.Value = band-pass oscillator (around 0)
// bk.IsHot = true after 2K+1 bars

// Static batch (span-based)
double[] output = new double[prices.Length];
BaxterKing.Batch(prices, output, pLow: 6, pHigh: 32, k: 12);

// Calculate factory method
var (results, indicator) = BaxterKing.Calculate(series, pLow: 6, pHigh: 32, k: 12);

// Event-driven chaining
var source = new TSeries();
var bkChained = new BaxterKing(source, pLow: 6, pHigh: 32, k: 12);
source.Add(new TValue(DateTime.UtcNow, price)); // bkChained.Last auto-updates
```
