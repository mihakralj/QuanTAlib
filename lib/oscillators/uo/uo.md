# UO: Ehlers Universal Oscillator

> *Where EEO compresses with tanh and DSO stretches with arctanh, UO simply lets the AGC compressor do the talking — peak-tracked normalization delivers a clean bounded signal with zero math functions.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Oscillator                       |
| **Inputs**       | Source (close)                   |
| **Parameters**   | `bandEdge` (default 20)          |
| **Outputs**      | Single series (Uo)               |
| **Output range** | Bounded [-1, +1]                |
| **Warmup**       | `bandEdge` bars                  |
| **PineScript**   | [uo.pine](uo.pine)              |

- UO (Universal Oscillator) extracts white noise via 2-bar differencing, smooths it through a 2-pole Super Smoother filter, then normalizes the output using Automatic Gain Control (AGC) peak tracking to produce a bounded [-1, +1] oscillator.
- **Similar:** [EEO](../eeo/Eeo.md), [DSO](../dso/Dso.md) | **Complementary:** ADX for trend confirmation | **Trading note:** Output bounded [-1, +1]; ±0.5 levels indicate strong momentum. Simpler than EEO (no RMS/IFT) and DSO (no Fisher Transform).
- No external validation libraries implement UO. Validated through self-consistency and behavioral testing.

UO is Ehlers' 2015 "universal" approach to oscillator design. Unlike his later EEO (2022) which uses IFT/RMS normalization, or DSO (2018) which uses Fisher Transform/RMS, UO achieves bounded output through the simplest possible mechanism: AGC peak tracking with exponential decay. The white noise extraction stage removes trend bias, the Super Smoother removes high-frequency noise, and the AGC normalizer ensures the output stays within [-1, +1] by dividing the filtered signal by its decaying peak.

## Historical Context

John F. Ehlers published the Universal Oscillator in the January 2015 issue of *Technical Analysis of Stocks & Commodities* magazine under the title "Relax, It's Just Noise — Whiter Is Brighter." The article presents a systematic approach to creating a noise-free oscillator using signal processing principles: first whiten the input (remove spectral coloring), then smooth (remove noise), then normalize (bound the output). The AGC normalization is borrowed from radio receiver design where automatic gain control maintains constant output amplitude regardless of input signal strength.

## Architecture & Physics

### Stage 1: White Noise Extraction

$$\text{WhiteNoise} = \frac{\text{Close} - \text{Close}[2]}{2}$$

The 2-bar difference divided by 2 creates a band-limited derivative that removes DC (trend) and Nyquist components, producing a spectrally flat ("white") signal. This is the same "zeros" whitening used in EEO and DSO.

### Stage 2: Super Smoother Filter (2-Pole Butterworth)

$$a_1 = e^{-1.414\pi / \text{BandEdge}}$$

$$b_1 = 2 \cdot a_1 \cdot \cos\!\left(\frac{1.414 \cdot \pi}{\text{BandEdge}}\right)$$

$$c_2 = b_1, \quad c_3 = -a_1^2, \quad c_1 = 1 - c_2 - c_3$$

$$\text{Filt} = \frac{c_1}{2}(\text{WN} + \text{WN}[1]) + c_2 \cdot \text{Filt}[1] + c_3 \cdot \text{Filt}[2]$$

The Super Smoother removes high-frequency noise while preserving the phase relationship. The BandEdge parameter controls the cutoff frequency.

### Stage 3: AGC Peak Normalization

$$\text{Peak} = \max(0.991 \cdot \text{Peak}[1],\ |\text{Filt}|)$$

$$\text{Universal} = \frac{\text{Filt}}{\text{Peak}}$$

The AGC tracks the peak amplitude with a decay factor of 0.991 (approximately 111-bar half-life). When a new peak exceeds the decayed previous peak, it becomes the new reference. The output is simply the filtered value divided by the current peak, naturally bounded to [-1, +1].

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

| Operation               | Count | Notes                          |
|:----------------------- |:----- |:------------------------------ |
| Subtraction (WN)        | 1     | (Close - Close[2]) / 2        |
| FMA × 2 (SSF)           | 2     | 2-pole recursive filter        |
| Multiply (decay)        | 1     | 0.991 * Peak                   |
| Abs + compare (AGC)     | 1     | Peak tracking                  |
| Division (normalize)    | 1     | Filt / Peak                    |
| **Total per bar**       | **~6** | Constant O(1), no buffers     |

### Batch Mode (SIMD Analysis)

The algorithm's IIR Super Smoother stage prevents full vectorization. Batch mode processes sequentially but avoids per-bar allocation overhead. No RingBuffer needed — all state is scalar.

### Quality Metrics

| Metric              | Score  | Notes                                    |
|:------------------- |:------ |:---------------------------------------- |
| Lag                 | Low    | SSF has minimal phase distortion         |
| Noise rejection     | High   | SSF smoothing after whitening            |
| Sensitivity         | High   | 2-bar derivative is very responsive      |
| Bounded output      | Yes    | [-1, +1] from AGC normalization          |
| Parameter count     | 1      | Only BandEdge                            |
| Memory footprint    | Minimal | No buffers, pure scalar state           |

## Validation

UO is validated through self-consistency tests (streaming ≡ batch ≡ span ≡ eventing) and behavioral tests (constant input → 0, trending → non-zero, symmetry).

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

1. **AGC decay rate**: The 0.991 decay factor is hardcoded per Ehlers' specification. This gives an approximately 111-bar half-life for peak memory. Do not change it.

2. **BandEdge vs Period**: BandEdge is the Super Smoother cutoff, not a lookback period. Higher BandEdge = more smoothing but more lag.

3. **Startup behavior**: During the first few bars, the AGC peak may be very small, causing large output swings. The WarmupPeriod of `bandEdge` bars mitigates this.

4. **Comparison with EEO/DSO**: UO is the simplest of the three Ehlers oscillators. EEO adds RMS normalization + IFT (tanh), DSO adds RMS + Fisher Transform (arctanh). UO uses only AGC — fewer computations, similar bounded output.
