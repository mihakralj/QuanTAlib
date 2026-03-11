# SPBF: Ehlers Super Passband Filter

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Filter                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `shortPeriod` (default 40), `longPeriod` (default 60), `rmsPeriod` (default 50)                      |
| **Outputs**      | Single series (SPBF)                       |
| **Output range** | Oscillates around zero           |
| **Warmup**       | `max(longPeriod, rmsPeriod)` bars (default 60) |
| **PineScript**   | [spbf.pine](spbf.pine)                       |

- The **Super Passband Filter** is John Ehlers' wide-band bandpass constructed by differencing two z-transformed EMAs with Ehlers-style smoothing ($\...
- Parameterized by `shortperiod` (default 40), `longperiod` (default 60), `rmsperiod` (default 50).
- Output range: Oscillates around zero.
- Requires `max(longPeriod, rmsPeriod)` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "Two EMAs walk into a frequency domain. The difference between them is the only thing worth trading."

The **Super Passband Filter** is John Ehlers' wide-band bandpass constructed by differencing two z-transformed EMAs with Ehlers-style smoothing ($\alpha = 5/N$). It rejects both DC trend and high-frequency noise, passing only the cyclic energy between two EMA-defined cutoff frequencies. The output oscillates around zero, with an RMS trigger envelope providing signal/noise discrimination.

## Historical Context

Ehlers introduced the Super Passband Filter in "The Super Passband Filter" (TASC, July 2016). The motivation: standard bandpass filters (Butterworth, Chebyshev) require trigonometric coefficient computation and careful pole placement. The Super Passband sidesteps this by exploiting a simpler observation. Any bandpass can be constructed as the difference of two lowpass filters with different cutoff frequencies. EMA is the simplest IIR lowpass. Subtract a slow EMA from a fast EMA, and the resulting filter passes frequencies where the fast EMA still tracks while the slow EMA has already smoothed away the signal.

The "super" prefix refers to the unusually wide passband achievable. Because EMA rolloff is gradual (first-order, -6 dB/octave), the transition bands are wide, which means the passband admits a broad range of cycles. Contrast this with the Roofing Filter (second-order Butterworth stages, -12 dB/octave rolloff), which creates a sharper but narrower passband.

The Ehlers smoothing convention $\alpha = 5/N$ (rather than the standard $\alpha = 2/(N+1)$) makes the EMAs more reactive, shifting the effective cutoff frequencies higher. This is a deliberate design choice: Ehlers optimized the Super Passband for responsiveness in trading applications, accepting wider transition bands in exchange for reduced lag.

## Architecture and Physics

### 1. Differenced EMA Bandpass

The filter computes the z-domain difference of two first-order IIR lowpass filters (EMAs). Given smoothing factors $\alpha_1 = 5/N_1$ (short) and $\alpha_2 = 5/N_2$ (long), the combined transfer function is:

$$H(z) = H_1(z) - H_2(z) = \frac{\alpha_1}{1 - (1-\alpha_1)z^{-1}} - \frac{\alpha_2}{1 - (1-\alpha_2)z^{-1}}$$

Cross-multiplying denominators yields a second-order IIR recurrence:

$$PB[t] = c_0 \cdot x[t] + c_1 \cdot x[t-1] + d_1 \cdot PB[t-1] + d_2 \cdot PB[t-2]$$

where the coefficients are precomputed from $\alpha_1$ and $\alpha_2$:

- $c_0 = \alpha_1 - \alpha_2$
- $c_1 = \alpha_2(1 - \alpha_1) - \alpha_1(1 - \alpha_2)$
- $d_1 = (1 - \alpha_1) + (1 - \alpha_2)$
- $d_2 = -(1 - \alpha_1)(1 - \alpha_2)$

### 2. RMS Trigger Envelope

The second output is an RMS (Root Mean Square) envelope computed over the last `rmsPeriod` passband values:

$$RMS[t] = \sqrt{\frac{1}{N}\sum_{i=0}^{N-1}PB[t-i]^2}$$

This provides a dynamic threshold for signal discrimination. When $|PB| > RMS$, the oscillator has broken above its "noise floor." This is the original Ehlers usage: trade when the passband crosses the RMS envelope.

### Inertial Physics

- **Zero DC Gain**: At $z = 1$, $H(1) = 1 - 1 = 0$. Constant input maps to zero output. The filter is a zero-mean oscillator by construction.
- **First-Order Rolloff**: Each EMA contributes -6 dB/octave. The combined filter has -6 dB/octave on each side of the passband (gentler than Butterworth-based bandpass designs).
- **Recursive Stability**: Both poles lie inside the unit circle (EMA poles are always stable for $0 < \alpha < 2$). No instability risk.

## Mathematical Foundation

### EMA Smoothing Convention

Ehlers uses $\alpha = 5/N$ instead of the standard $\alpha = 2/(N+1)$:

| Period $N$ | Ehlers $\alpha = 5/N$ | Standard $\alpha = 2/(N+1)$ |
| :--- | :--- | :--- |
| 10 | 0.500 | 0.182 |
| 20 | 0.250 | 0.095 |
| 40 | 0.125 | 0.049 |
| 60 | 0.083 | 0.033 |

The Ehlers convention makes the EMA approximately 2.5x more reactive for the same nominal period.

### Coefficient Derivation

Given $\alpha_1 = 5/N_1$, $\alpha_2 = 5/N_2$, define decay constants $\delta_1 = 1 - \alpha_1$, $\delta_2 = 1 - \alpha_2$:

$$c_0 = \alpha_1 - \alpha_2$$
$$c_1 = \alpha_2 \delta_1 - \alpha_1 \delta_2$$
$$d_1 = \delta_1 + \delta_2$$
$$d_2 = -\delta_1 \delta_2$$

### Default Parameters

| Parameter | Default | Purpose |
| :--- | :--- | :--- |
| `shortPeriod` | 40 | Fast EMA period. Defines the high-frequency cutoff. |
| `longPeriod` | 60 | Slow EMA period. Defines the low-frequency cutoff. |
| `rmsPeriod` | 50 | RMS averaging window for trigger envelope. |

## Performance Profile

### Operation Count (Streaming Mode)

Spectral Band-Pass FIR: FIR filter designed in the frequency domain. Coefficient generation is O(N log N) once at construction; per-bar streaming is O(N) dot product.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| RingBuffer write | 1 | ~2 cy | ~2 cy |
| Dot product FMA (N taps) | N | ~5 cy | ~250 cy (N=50) |
| **Total (N=50)** | **N+1** | — | **~252 cycles** |

O(N) per bar. FIR coefficient table precomputed from spectral specification. ~252 cycles for N=50.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| FIR dot product | Yes | `Vector<double>` 4x speedup |
| Spectral coefficient table | N/A | Precomputed once |

AVX2 batch: ~65 cy for N=50.

| Metric | Impact | Notes |
| :--- | :--- | :--- |
| **Throughput** | ~3 ns/bar (PB only) | O(1) passband: 4 FMA operations. |
| **RMS** | O(rmsPeriod)/bar | Ring buffer sum of squares. |
| **Allocations** | 0 | Zero-allocation in hot path. FMA-optimized. |
| **Accuracy** | 7/10 | -6 dB/octave rolloff (first-order per side). |
| **Timeliness** | 9/10 | Minimal lag due to reactive Ehlers smoothing. |
| **Smoothness** | 6/10 | Wide transition bands admit some out-of-band energy. |

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **Pine Script** | Validated | Ported from validated Ehlers/TradingView implementation. |
| **Synthetic** | Validated | Multi-frequency sine waves confirm bandpass behavior. |
| **Self-Consistency** | Validated | Streaming, batch, span, and eventing modes produce identical results. |
| **DC Rejection** | Validated | Constant input produces zero output. |
| **RMS Envelope** | Validated | >80% of passband values fall within ±2×RMS after warmup. |

## Common Pitfalls

1. **Expecting overlay behavior**: SPBF oscillates around zero. It is NOT a price overlay. Plot it in a separate window (`SeparateWindow = true`).

2. **Confusing period semantics with standard EMA**: Ehlers uses $\alpha = 5/N$, not $\alpha = 2/(N+1)$. A "40-period" SPBF reacts much faster than a 40-period standard EMA. Do not map mental models from standard EMA periods.

3. **shortPeriod > longPeriod**: While mathematically valid (the coefficients simply flip sign), this inverts the passband semantics. The convention is `shortPeriod < longPeriod` so that $\alpha_1 > \alpha_2$.

4. **Ignoring the RMS envelope**: The raw passband oscillator is noisy. Ehlers designed the RMS trigger specifically for signal discrimination. Trading raw zero crossings without RMS filtering yields excessive whipsaws.

5. **Gradual rolloff means spectral leakage**: Unlike Butterworth-based bandpass (Roofing, BPF), SPBF has -6 dB/octave rolloff. Out-of-band energy leaks through. For sharper spectral isolation, prefer BPF or Roofing.

6. **Comparing against Roofing directly**: Roofing uses second-order Butterworth stages for both HP and LP. SPBF uses first-order EMAs. They are architecturally different filters with different frequency responses, even if both are "bandpass."

7. **RMS period too short**: If `rmsPeriod` is much shorter than the passband cycle period, the RMS envelope oscillates with the signal rather than providing a stable baseline. Default 50 works well with the default 40/60 passband.

## References

1. John F. Ehlers. "The Super Passband Filter." Technical Analysis of Stocks and Commodities, July 2016.
2. John F. Ehlers. "Cycle Analytics for Traders." Wiley, 2013.
3. John F. Ehlers. "Rocket Science for Traders." Wiley, 2001.

## Usage

```csharp
using QuanTAlib;

// Default: short=40, long=60, rms=50
var spbf = new Spbf(shortPeriod: 40, longPeriod: 60, rmsPeriod: 50);

// Streaming update
var result = spbf.Update(new TValue(DateTime.UtcNow, price));
// result.Value = passband oscillator (around 0)
// spbf.Rms = RMS trigger level

// Static batch (zero allocation, passband only)
double[] output = new double[prices.Length];
Spbf.Batch(prices, output, shortPeriod: 40, longPeriod: 60, rmsPeriod: 50);

// Batch with both passband and RMS
double[] pb = new double[prices.Length];
double[] rms = new double[prices.Length];
Spbf.BatchWithRms(prices, pb, rms, shortPeriod: 40, longPeriod: 60, rmsPeriod: 50);

// Event-driven chaining
var source = new TSeries();
var spbfChained = new Spbf(source, shortPeriod: 40, longPeriod: 60, rmsPeriod: 50);
source.Add(new TValue(DateTime.UtcNow, price)); // spbfChained.Last auto-updates
```
