# EEO: Ehlers Elegant Oscillator

A bounded zero-crossing oscillator that applies the Inverse Fisher Transform to RMS-normalized 2-bar momentum, then smooths the result with a 2-pole Super Smoother filter. Output is approximately bounded to [-1, +1].

| Property       | Value                                         |
|:-------------- |:--------------------------------------------- |
| **Category**   | Oscillators                                   |
| **Author**     | John F. Ehlers                                |
| **Source**      | TASC, February 2022                          |
| **Article**    | "An Elegant Oscillator: Inverse Fisher Transform Redux" |
| **Input**      | Single series (Close)                         |
| **Parameters** | BandEdge (default 20)                         |
| **Output**     | Bounded ≈ [-1, +1]                           |
| **Hot after**  | 50 + BandEdge bars                           |

## Historical Context

Ehlers' 2022 "Elegant Oscillator" is a refinement of his earlier DSO (2018). Where DSO applies the Fisher Transform (arctanh) to expand a normalized signal, EEO applies the **Inverse Fisher Transform** (tanh) to compress it. The IFT naturally bounds the output to [-1, +1] without needing the ±0.99 clamping that DSO requires. A Super Smoother post-filter then removes residual noise.

## Architecture & Physics

### Stage 1: 2-Bar Momentum (Derivative)

$$\text{Deriv} = \text{Close} - \text{Close}[2]$$

This is the same "zeros" whitening used in DSO — it removes DC and Nyquist components, creating a band-limited derivative.

### Stage 2: RMS Normalization (Fixed 50-Bar Window)

$$\text{RMS} = \sqrt{\frac{1}{50}\sum_{k=0}^{49}\text{Deriv}[k]^2}$$

$$\text{NDeriv} = \frac{\text{Deriv}}{\text{RMS}}$$

The fixed 50-bar window (not parameterized) provides a stable normalization base. The RMS measures the "typical" derivative magnitude, so NDeriv represents "how many standard deviations" the current derivative is from zero.

### Stage 3: Inverse Fisher Transform (tanh)

$$\text{IFish} = \tanh(\text{NDeriv}) = \frac{e^{2 \cdot \text{NDeriv}} - 1}{e^{2 \cdot \text{NDeriv}} + 1}$$

The IFT compresses the normalized derivative into [-1, +1]. Values near ±1 indicate extreme momentum relative to recent history.

### Stage 4: Super Smoother Filter (2-Pole Butterworth)

$$a_1 = e^{-1.414\pi / \text{BandEdge}}$$

$$b_1 = 2 \cdot a_1 \cdot \cos\!\left(\frac{1.414 \cdot 180°}{\text{BandEdge}}\right)$$

$$c_2 = b_1, \quad c_3 = -a_1^2, \quad c_1 = 1 - c_2 - c_3$$

$$\text{SS} = \frac{c_1}{2}(\text{IFish} + \text{IFish}[1]) + c_2 \cdot \text{SS}[1] + c_3 \cdot \text{SS}[2]$$

The Super Smoother removes high-frequency chatter from the IFT output while preserving the phase relationship.

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

| Operation               | Count | Notes                          |
|:----------------------- |:----- |:------------------------------ |
| Subtraction (Deriv)     | 1     | Close - Close[2]               |
| Multiply (deriv²)       | 1     | For RMS buffer                 |
| RingBuffer add/remove   | 1     | O(1) circular buffer           |
| FMA (running sum)       | 1     | SumSq update                   |
| Sqrt (RMS)              | 1     | √(sum/50)                      |
| Division (normalize)    | 1     | Deriv / RMS                    |
| Exp + tanh (IFT)        | 1     | Math.Tanh()                    |
| FMA × 2 (SSF)           | 2     | 2-pole recursive filter        |
| **Total per bar**       | **~9** | Constant O(1)                 |

### Batch Mode (SIMD Analysis)

The algorithm's IIR Super Smoother stage prevents full vectorization. Batch mode processes sequentially but avoids per-bar allocation overhead.

### Quality Metrics

| Metric              | Score | Notes                                    |
|:------------------- |:----- |:---------------------------------------- |
| Lag                 | Low   | SSF has minimal phase distortion         |
| Noise rejection     | High  | IFT compression + SSF smoothing          |
| Sensitivity         | High  | 2-bar derivative is very responsive      |
| Bounded output      | Yes   | ≈ [-1, +1] from tanh                    |
| Parameter count     | 1     | Only BandEdge                            |

## Validation

EEO is validated through self-consistency tests (streaming ≡ batch ≡ span ≡ eventing) and behavioral tests (constant input → 0, trending → non-zero, symmetry).

### Behavioral Test Summary

| Test                    | Expected Result           |
|:----------------------- |:------------------------- |
| Constant input          | Output → 0               |
| Strong uptrend          | Output > 0               |
| Strong downtrend        | Output < 0               |
| Ascending vs descending | Opposite signs            |
| NaN/Inf input           | Finite output (fallback)  |
| Bar correction (isNew)  | State restored correctly  |

## Common Pitfalls

1. **Fixed RMS window**: The 50-bar window is hardcoded per Ehlers' specification. Do not parameterize it — it provides a stable normalization base independent of BandEdge.

2. **BandEdge vs Period**: BandEdge is the Super Smoother cutoff, not an RMS lookback. Higher BandEdge = more smoothing but more lag.

3. **Bounded output**: Unlike DSO (which uses Fisher Transform producing unbounded output), EEO output is bounded to ≈ [-1, +1]. Signal levels of ±0.5 are typical thresholds, not ±2 as with DSO.

4. **Warmup**: Requires 50 + BandEdge bars. The first 50 bars fill the RMS window; then BandEdge more bars are needed for SSF convergence.
