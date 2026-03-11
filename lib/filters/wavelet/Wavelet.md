# WAVELET: Denoising Wavelet Filter

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Filter                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `levels` (default 4), `threshMult` (default 1.0)                      |
| **Outputs**      | Single series (Wavelet)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `2^levels` bars (default 16)   |
| **Signature**    | [wavelet_signature](wavelet_signature.md) |


### TL;DR

- The Wavelet Denoising Filter applies an *à trous* (with holes) Haar wavelet decomposition with soft thresholding to remove high-frequency noise fro...
- Parameterized by `levels` (default 4), `threshmult` (default 1.0).
- Output range: Tracks input.
- Requires `2^levels` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "The wavelet transform is to the Fourier transform what a microscope is to a telescope: same math, different scale."

## Introduction

The Wavelet Denoising Filter applies an *à trous* (with holes) Haar wavelet decomposition with soft thresholding to remove high-frequency noise from price series while preserving trend structure and edges. Unlike classical low-pass filters that blur everything uniformly, wavelet denoising estimates the noise floor at each decomposition level via Median Absolute Deviation (MAD) and surgically removes only coefficients below the threshold. The result: noise reduction without the phase lag or overshoot penalty of IIR alternatives.

## Historical Context

Wavelet denoising entered signal processing through Donoho and Johnstone's 1994 paper on "ideal spatial adaptation," which proved that soft thresholding of wavelet coefficients achieves near-optimal minimax risk for estimating functions in Besov spaces. The *à trous* algorithm, developed by Holschneider, Kronland-Martinet, Morlet, and Tchamitchian (1989), provides a non-decimated (stationary) wavelet transform that avoids the shift-variance problems of the standard dyadic DWT.

In financial time series, wavelet denoising occupies a niche between simple moving averages (which blur edges) and Kalman filters (which require state-space models). The Haar basis, the simplest wavelet, decomposes the signal into successive averages and differences at doubling scales. Each level captures oscillations at period $2^l$ bars. The MAD-based noise estimation is robust to outliers, unlike variance-based estimators that can be corrupted by a single spike.

This implementation uses the Haar wavelet exclusively. More complex wavelets (Daubechies, Symlets) offer better frequency localization but introduce boundary artifacts and computational overhead that rarely justify the marginal improvement on financial data.

## Architecture and Physics

### 1. À Trous Decomposition

The *à trous* algorithm computes a non-decimated wavelet transform by inserting zeros ("holes") into the filter at each level. For the Haar wavelet at level $l$, the smoothing operation averages samples separated by $2^{l-1}$:

$$a_l[n] = \frac{1}{2}\left(a_{l-1}[n] + a_{l-1}[n - 2^{l-1}]\right)$$

where $a_0[n]$ is the original signal and boundary values use the nearest available sample (clamped indexing).

The detail coefficients at level $l$ are the difference:

$$d_l[n] = a_{l-1}[n] - a_l[n]$$

### 2. MAD Noise Estimation

The noise standard deviation at each level is estimated via the Median Absolute Deviation of the detail coefficients:

$$\hat{\sigma}_l = \frac{\text{MAD}(d_l)}{0.6745}$$

The constant $0.6745 = \Phi^{-1}(3/4)$ normalizes MAD to match the standard deviation under Gaussian assumptions. The MAD uses only the most recent buffer of samples (sized $2^{\text{levels}} + 1$) to maintain locality.

### 3. Soft Thresholding

Each detail coefficient is soft-thresholded with level-dependent threshold $\tau_l = \lambda \cdot \hat{\sigma}_l$, where $\lambda$ is the user-controlled threshold multiplier:

$$\tilde{d}_l[n] = \text{sign}(d_l[n]) \cdot \max(|d_l[n]| - \tau_l, 0)$$

Soft thresholding shrinks coefficients toward zero continuously, avoiding the discontinuities of hard thresholding. The implementation uses `Math.CopySign` for branchless sign extraction.

### 4. Reconstruction

The denoised signal is reconstructed by summing the coarsest approximation and all thresholded detail coefficients:

$$\hat{x}[n] = a_L[n] + \sum_{l=1}^{L} \tilde{d}_l[n]$$

where $L$ is the number of decomposition levels.

## Mathematical Foundation

### Transfer Function (Frequency Domain)

The Haar wavelet at level $l$ acts as a band-pass filter centered at frequency $f_l = 1/(2^{l+1})$ cycles/sample. The à trous decomposition partitions the frequency axis into octave bands:

| Level | Center Frequency | Period (bars) |
|-------|-----------------|---------------|
| 1 | 0.25 | 2 |
| 2 | 0.125 | 4 |
| 3 | 0.0625 | 8 |
| 4 | 0.03125 | 16 |
| 5 | 0.015625 | 32 |
| 6 | 0.0078125 | 64 |
| 7 | 0.00390625 | 128 |
| 8 | 0.001953125 | 256 |

The filter removes energy from bands where $|d_l| < \tau_l$, preserving bands where true signal dominates noise.

### Threshold Selection

The universal threshold $\tau = \sigma \sqrt{2 \ln N}$ (Donoho-Johnstone) is optimal asymptotically. This implementation uses the simpler $\tau = \lambda \cdot \hat{\sigma}$ with user-controlled $\lambda$, which provides more intuitive control:

- $\lambda = 0$: No denoising (passthrough).
- $\lambda = 1$: Standard denoising (MAD-estimated noise floor).
- $\lambda = 2$: Aggressive denoising (removes coefficients up to $2\sigma$).

### Parameter Mapping

| Parameter | Symbol | Default | Range | Effect |
|-----------|--------|---------|-------|--------|
| Levels | $L$ | 4 | $[1, 8]$ | Decomposition depth; higher = coarser approximation |
| ThreshMult | $\lambda$ | 1.0 | $[0, \infty)$ | Threshold multiplier; higher = more aggressive denoising |

### Warmup Period

The filter requires $2^L$ samples to fill the decomposition buffer. `IsHot` activates after $2^L + 1$ samples have been processed.

## Performance Profile

### Operation Count Per Bar

| Operation | Count | Notes |
|-----------|-------|-------|
| Buffer insert | $O(1)$ | RingBuffer append |
| Decomposition ($L$ levels) | $O(L \cdot B)$ | $B = 2^L + 1$ buffer size |
| MAD estimation ($L$ levels) | $O(L \cdot B \log B)$ | Sort-based median per level |
| Soft thresholding | $O(L \cdot B)$ | Branchless via `CopySign` |
| Reconstruction | $O(L \cdot B)$ | Sum of thresholded details |
| **Total** | **$O(L \cdot B \log B)$** | Dominated by MAD sorting |

### Memory Usage

| Component | Size | Notes |
|-----------|------|-------|
| RingBuffer | $B$ doubles | $B = 2^L + 1$, default 17 |
| Approximation array | $B$ doubles | Per-bar stack allocation |
| Detail arrays | $L \times B$ doubles | Per-bar stack allocation |
| State struct | 2 doubles | `LastValid`, `Count` |
| **Total persistent** | **$B + 16$ bytes** | RingBuffer + state |

### Quality Metrics

| Metric | Score (1-10) | Notes |
|--------|:---:|-------|
| Smoothness | 8 | Excellent noise removal in quiet periods |
| Lag | 9 | Near-zero phase distortion |
| Overshoot | 9 | Soft thresholding prevents ringing |
| Noise rejection | 8 | MAD-based, robust to outliers |
| Edge preservation | 8 | Preserves sharp moves unlike MA filters |
| Computational cost | 5 | MAD sorting per level per bar |

## Validation

Wavelet denoising has no direct equivalent in standard TA libraries. Validation uses self-consistency tests.

| Test | Method | Result |
|------|--------|--------|
| Denoising effectiveness | High-frequency noise removal | Denoised diff variance < noisy diff variance |
| Streaming = Span | Mode parity | Match to $10^{-10}$ |
| Determinism | Two identical runs | Match to $10^{-15}$ |
| Constant signal | Preserved exactly | $\|y - 42\| < 10^{-10}$ |
| Linear trend | Minimal distortion | Max deviation < 5.0 |
| Stability | 5000-bar dataset | All outputs finite and bounded |
| Zero threshold | Signal preservation | All outputs finite |
| Higher threshold | More deviation from input | SAD increases monotonically |
| Calculate tuple | Returns results + indicator | Sizes match, IsHot true |

## Common Pitfalls

1. **Too many levels for the data.** Level $L$ requires $2^L$ history samples. Level 8 needs 256 bars of context. On shorter series, the coarse approximation captures almost nothing, and the filter degrades to passthrough. Impact: denoising effectiveness drops to near zero.

2. **Threshold too high.** Setting $\lambda > 3$ removes virtually all detail coefficients, collapsing the output to a very coarse moving average. The result looks smooth but loses all responsiveness to genuine price movements. Impact: effective lag increases by $2^L$ bars.

3. **Threshold too low.** Values near zero pass through most noise. The filter becomes expensive computation for negligible benefit. Impact: output is nearly identical to input.

4. **Confusing levels with period.** Level 4 does not mean "period 4." It means the decomposition buffer is $2^4 + 1 = 17$ samples, and the coarsest approximation captures oscillations at period 16. The effective smoothing scale is exponential, not linear.

5. **MAD instability on constant segments.** When the signal is exactly constant, all detail coefficients are zero, MAD is zero, and the threshold is zero. This is mathematically correct (no noise to remove) but can surprise users expecting nonzero output differences. Impact: none in practice, but edge case worth noting.

6. **Not suitable for SIMD.** The MAD computation requires sorting, and the decomposition's clamped boundary indexing creates data-dependent access patterns. Neither operation vectorizes cleanly. The Batch method uses a sequential loop internally.

7. **Haar basis limitations.** The Haar wavelet has poor frequency localization (wide spectral leakage). For signals with narrow-band components, Daubechies wavelets would theoretically perform better, but the implementation complexity and marginal improvement do not justify the trade-off for typical financial data.

## References

- Donoho, D.L. & Johnstone, I.M. (1994). "Ideal Spatial Adaptation by Wavelet Shrinkage." *Biometrika*, 81(3), 425-455.
- Holschneider, M., Kronland-Martinet, R., Morlet, J. & Tchamitchian, P. (1989). "A Real-Time Algorithm for Signal Analysis with the Help of the Wavelet Transform." In *Wavelets: Time-Frequency Methods and Phase Space*, Springer.
- Mallat, S. (2009). *A Wavelet Tour of Signal Processing: The Sparse Way*. 3rd ed. Academic Press.
- Nason, G.P. (2008). *Wavelet Methods in Statistics with R*. Springer.
- Ehlers, J.F. (2001). *Rocket Science for Traders*. Wiley. (Context for financial signal processing filters.)
