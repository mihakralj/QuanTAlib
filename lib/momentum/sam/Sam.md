# SAM: Smoothed Adaptive Momentum

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Momentum                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `alpha` (default 0.07), `cutoff` (default 8)                      |
| **Outputs**      | Single series (Sam)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `MaxCyclePeriod * 2` bars                          |

### TL;DR

- The Smoothed Adaptive Momentum oscillator measures price momentum over an adaptively determined lookback period equal to the dominant cycle length,...
- Parameterized by `alpha` (default 0.07), `cutoff` (default 8).
- Output range: Varies (see docs).
- Requires `MaxCyclePeriod * 2` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The Smoothed Adaptive Momentum oscillator measures price momentum over an adaptively determined lookback period equal to the dominant cycle length, then smooths the result with a 2-pole Super Smoother filter. Unlike fixed-period momentum indicators (ROC, TRIX) that use an arbitrary lookback, SAM measures the dominant cycle via Ehlers' Homodyne Discriminator and uses that cycle length as the momentum window, ensuring that the momentum measurement always spans exactly one full cycle. This eliminates the half-cycle phase distortion that plagues fixed-period momentum, producing a zero-lag momentum oscillator that naturally adapts to changing market rhythm.

## Historical Context

SAM was developed by John F. Ehlers and presented in Chapter 12 ("Adapting to the Trend") of "Cybernetic Analysis for Stocks and Futures" (2004). It represents the synthesis of two Ehlers innovations: the Homodyne Discriminator for cycle measurement and the Super Smoother for noise reduction.

The key insight is that momentum measured over exactly one dominant cycle period produces a nearly zero-mean oscillator with minimal spectral leakage. If the dominant cycle is 20 bars, then `close - close[20]` captures one full oscillation, with the difference being zero at the start and end of the cycle (when phase completes 360°). A fixed 14-bar momentum, by contrast, may measure a fractional cycle, producing a biased oscillator with a non-zero mean that varies as the cycle length drifts.

The Homodyne Discriminator (named after the homodyne detection technique from radio engineering) measures the instantaneous frequency by correlating the analytic signal with a one-bar-delayed version of itself. The phase change between bars gives the instantaneous frequency, which is smoothed and clamped to produce a stable period estimate in the 6-50 bar range.

The Super Smoother is Ehlers' preferred final-stage filter: a 2-pole IIR low-pass with better amplitude response than a Butterworth of the same order, specifically designed to minimize lag while suppressing high-frequency noise.

## Architecture and Physics

The pipeline has five stages:

**Stage 1: 4-bar FIR smoother** applies a [1, 2, 2, 1]/6 weighted average to eliminate 2-bar and 3-bar cycle noise. This is a standard Ehlers preprocessing step that removes aliasing artifacts without introducing significant lag.

**Stage 2: Hilbert Transform** extracts the analytic signal from the smoothed price using Ehlers' modified Hilbert Transform. The detrender and quadrature components $I_1$ and $Q_1$ are derived via 7-tap FIR filters with empirically chosen coefficients that approximate the Hilbert Transform over financial cycle frequencies.

**Stage 3: Phase advance** applies the Hilbert Transform to $I_1$ and $Q_1$ themselves, producing $JI$ and $JQ$ (90° phase-advanced versions). The phasor addition $I_2 = I_1 - JQ$ and $Q_2 = Q_1 + JI$ creates the forward-rotated analytic signal needed for homodyne detection.

**Stage 4: Homodyne Discriminator** correlates the current phasor $(I_2, Q_2)$ with the previous bar's phasor to extract the instantaneous frequency:

$$\text{Re} = I_2 \cdot I_2[1] + Q_2 \cdot Q_2[1], \quad \text{Im} = I_2 \cdot Q_2[1] - Q_2 \cdot I_2[1]$$

The period is $2\pi / \arctan(\text{Im}/\text{Re})$, clamped to $[6, 50]$ and smoothed via two cascaded EMA stages to produce the dominant cycle period.

**Stage 5: Adaptive momentum + Super Smoother** computes `source - source[dcPeriod]` where `dcPeriod` is the rounded dominant cycle, then applies a 2-pole Super Smoother with the user-specified cutoff period.

## Mathematical Foundation

**4-bar FIR smoother**:

$$s[n] = \frac{x[n] + 2x[n-1] + 2x[n-2] + x[n-3]}{6}$$

**Ehlers Hilbert Transform** (7-tap approximation):

$$H[n] = 0.0962\,s[n] + 0.5769\,s[n-2] - 0.5769\,s[n-4] - 0.0962\,s[n-6]$$

scaled by the adaptive gain factor $(0.075\,P[n-1] + 0.54)$.

**Homodyne Discriminator**:

$$\text{Re}[n] = I_2[n] \cdot I_2[n-1] + Q_2[n] \cdot Q_2[n-1]$$

$$\text{Im}[n] = I_2[n] \cdot Q_2[n-1] - Q_2[n] \cdot I_2[n-1]$$

$$P = \frac{2\pi}{\arctan(\text{Im}/\text{Re})}, \quad P \in [6, 50]$$

**Dominant Cycle Period** (double-smoothed):

$$P_{\text{inst}} = 0.33 \cdot P + 0.67 \cdot P_{\text{inst}}[1]$$

$$P_{\text{DC}} = 0.15 \cdot P_{\text{inst}} + 0.85 \cdot P_{\text{DC}}[1]$$

**Adaptive momentum**: $M[n] = x[n] - x[n - \lfloor P_{\text{DC}} \rfloor]$

**2-pole Super Smoother** with cutoff $C$:

$$a_1 = e^{-\sqrt{2}\pi/C}, \quad b_1 = 2a_1\cos(\sqrt{2}\pi/C)$$

$$\text{filt}[n] = \frac{1 - b_1 + a_1^2}{2}(M[n] + M[n-1]) + b_1\,\text{filt}[n-1] - a_1^2\,\text{filt}[n-2]$$

**Parameter constraints**: $\alpha \in (0, 1)$, `cutoff` $\ge 2$.

```
SAM(source, alpha, cutoff):
    smooth = (src + 2*src[1] + 2*src[2] + src[3]) / 6

    // Hilbert Transform -> I1, Q1
    // Phase advance -> JI, JQ
    // Phasor addition -> I2, Q2
    I2 = I1 - JQ;  Q2 = Q1 + JI
    I2 = alpha*I2 + (1-alpha)*I2[1]     // smooth
    Q2 = alpha*Q2 + (1-alpha)*Q2[1]

    // Homodyne Discriminator
    Re = I2*I2[1] + Q2*Q2[1]
    Im = I2*Q2[1] - Q2*I2[1]
    period = 2*pi / atan(Im/Re), clamped [6,50]
    dcPeriod = double_smooth(period)

    // Adaptive momentum + Super Smoother
    momentum = source - source[dcPeriod]
    return superSmoother(momentum, cutoff)
```

## Resources

- Ehlers, J.F. "Cybernetic Analysis for Stocks and Futures." Wiley, 2004. Chapter 12, p.166.
- Ehlers, J.F. "Rocket Science for Traders." Wiley, 2001. Chapters on Hilbert Transform and cycle measurement.
- Ehlers, J.F. "MESA and Trading Market Cycles." 2nd edition, Wiley, 2002.
- Oppenheim, A.V. & Schafer, R.W. "Discrete-Time Signal Processing." 3rd edition, Pearson, 2010. Chapter on Hilbert Transform.

## Performance Profile

### Operation Count (Streaming Mode)

SAM runs a 5-stage pipeline fully O(1) per bar. All state is scalar; the `RingBuffer` provides O(1) indexed read for the adaptive momentum lookback. The dominant cost is the Hilbert Transform stage (7-tap FIR ×4 channels) and the homodyne phasor multiplications.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| 4-bar FIR smoother (3 FMA) | 3 | 4 | ~12 |
| Hilbert detrender (7-tap FIR ×2) | 14 | 4 | ~56 |
| Phase advance (7-tap FIR ×2 on I1/Q1) | 14 | 4 | ~56 |
| Phasor products I2/Q2 (4 MUL + 2 ADD/SUB) | 6 | 3 | ~18 |
| EMA smooth I2/Q2 (2 FMA) | 2 | 4 | ~8 |
| Homodyne products Re/Im (4 FMA) | 4 | 4 | ~16 |
| EMA smooth Re/Im (2 FMA) | 2 | 4 | ~8 |
| Period: ATAN2 + divide + clamp | 3 | 25 | ~75 |
| InstPeriod + DcPeriod EMA (2 FMA) | 2 | 4 | ~8 |
| Adaptive momentum: ring buffer read + SUB | 2 | 3 | ~6 |
| Super Smoother (2 FMA) | 2 | 4 | ~8 |
| **Total** | **56** | — | **~271 cycles** |

O(1) per bar. The `ATAN2` call dominates (~25 cycles on modern x86). WarmupPeriod ≈ 40 bars for stable cycle detection. All 56 operations are scalar — the `record struct State` with 36 fields is promoted to registers by the JIT using the local-copy pattern.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| 4-bar FIR smoother | Partial | Coefficients are fixed; overlapping windows need careful striding |
| Hilbert FIR (7-tap ×4 channels) | No | State history per channel creates sequential dependency |
| Phasor products | No | Current bar depends on previous I2/Q2 via EMA smoothing |
| Homodyne Re/Im | No | Recursive EMA on running products |
| ATAN2 / period estimation | No | Transcendental function; no AVX2 intrinsic; libm `vatan2` via SVML possible |
| Super Smoother | No | 2-pole IIR; z-transform has poles inside unit circle, inherently serial |
| Adaptive indexing (ring buffer) | No | Index depends on computed dcPeriod |

SAM cannot be meaningfully vectorized — every stage except the FIR smoother has a data dependency that threads through the recursive EMA states. The dominant SIMD opportunity is the batch computation of candidate FIR outputs using strided AVX2 loads, but the downstream homodyne feedback loop negates it. Batch mode runs the same scalar kernel as streaming.
