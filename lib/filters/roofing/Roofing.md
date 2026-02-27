# ROOFING: Ehlers Roofing Filter

> "The trend is your friend until it overwhelms the signal. The noise is your enemy until you mistake it for alpha."

The **Roofing Filter** is John Ehlers' bandpass architecture designed specifically for oscillator construction. It cascades a 2nd-order Butterworth Highpass (to strip trend) with a Super Smoother Lowpass (to strip noise), passing only the cyclic energy within a user-defined frequency band. The output oscillates around zero, with zero crossings serving as directional signals.

## Historical Context

Ehlers introduced the Roofing Filter in "Cycle Analytics for Traders" (2013) and formalized it in TASC's January 2014 article "Predictive Indicators for Effective Trading Strategies." The core insight: most indicators suffer from two problems simultaneously. Trend contamination causes indicator drift (the indicator "follows" price instead of measuring momentum). Noise contamination causes whipsaw (the indicator triggers on random ticks rather than meaningful cycles).

The Roofing Filter solves both by creating a double-bounded passband. The "roof" (highpass cutoff) caps the maximum cycle period admitted, eliminating trend drift. The "floor" (super smoother cutoff) sets the minimum cycle period, eliminating noise jitter. What remains is the cyclic energy between these bounds, and that energy is what generates tradeable signals.

Prior art includes simple highpass filters (which solve drift but amplify noise) and lowpass filters (which solve noise but introduce lag and trend contamination). The Roofing Filter's contribution is the cascaded architecture: apply both in sequence, using matched Butterworth coefficients for predictable phase behavior.

This implementation uses the same Butterworth coefficient derivation for both stages, consistent with the library's BPF pattern.

## Architecture and Physics

The filter operates as a two-stage cascade. Each stage is a 2nd-order IIR filter with its own set of precomputed coefficients.

### 1. Stage 1: Highpass (Detrending)

The signal enters a 2nd-order Butterworth Highpass filter parameterized by `hpLength`. This stage removes cycles longer than `hpLength` bars. A 48-bar default means the filter strips any component with a period exceeding 48 bars, effectively removing the "trend" from the perspective of a swing trader.

The highpass applies the second-difference operator $(x[t] - 2x[t-1] + x[t-2])$ scaled by a gain factor, then feeds back through two poles:

$$HP[t] = G_{hp}(x[t] - 2x[t-1] + x[t-2]) + C_{2,hp} \cdot HP[t-1] + C_{3,hp} \cdot HP[t-2]$$

### 2. Stage 2: Super Smoother (Denoising)

The highpass output feeds into a 2nd-order Butterworth Lowpass (Super Smoother) parameterized by `ssLength`. This stage removes cycles shorter than `ssLength` bars. A 10-bar default means any component with a period below 10 bars is treated as noise and suppressed.

$$ROOF[t] = G_{ss} \cdot HP[t] + C_{2,ss} \cdot ROOF[t-1] + C_{3,ss} \cdot ROOF[t-2]$$

### Inertial Physics

- **Recursive Stability**: Both stages place poles inside the unit circle, guaranteeing exponential decay of transients.
- **Zero DC Gain**: The highpass stage has a zero at $z = 1$, ensuring constant input maps to zero output. The Roofing Filter is a zero-mean oscillator by construction.
- **Warmup**: Dominated by `hpLength` (the slower stage). Until the HP coefficients have decayed initial transients, output is "cold."

## Mathematical Foundation

### Coefficient Derivation

Both stages use identical Butterworth coefficient computation. For a given cutoff period $P$:

$$\lambda = \frac{\pi\sqrt{2}}{P}$$
$$\alpha = e^{-\lambda}$$
$$C_2 = 2\alpha\cos(\lambda)$$
$$C_3 = -\alpha^2$$

### Highpass Gain

$$G_{hp} = \frac{1 + C_{2,hp} - C_{3,hp}}{4}$$

### Lowpass Gain

$$G_{ss} = 1 - C_{2,ss} - C_{3,ss}$$

### Default Parameters

| Parameter | Default | Purpose |
| :--- | :--- | :--- |
| `hpLength` | 48 | Highpass cutoff. Removes cycles longer than 48 bars (trend). |
| `ssLength` | 10 | Super Smoother cutoff. Removes cycles shorter than 10 bars (noise). |

## Performance Profile

### Operation Count (Streaming Mode)

Roofing filter: Ehlers 2-stage cascade — first a high-pass filter removes low-frequency drift, then a super-smooth filter removes high-frequency noise. Two O(1) IIR stages in series.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| HP stage: IIR high-pass (3 FMA) | 3 | ~4 cy | ~12 cy |
| HP state update | 1 | ~1 cy | ~1 cy |
| SuperSmooth stage: 2-pole IIR (3 FMA) | 3 | ~4 cy | ~12 cy |
| SS state update | 2 | ~1 cy | ~2 cy |
| **Total** | **9** | — | **~27 cycles** |

O(1) per bar. Two cascaded IIR stages with precomputed coefficients. ~27 cycles/bar.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| HP stage recursion | No | Sequential IIR |
| SuperSmooth recursion | No | Depends on HP output |

Batch throughput: ~27 cy/bar.

| Metric | Impact | Notes |
| :--- | :--- | :--- |
| **Throughput** | ~4 ns/bar | O(1) per update. 7 multiplications, 6 additions. |
| **Allocations** | 0 | Zero-allocation in hot path. FMA-optimized. |
| **Complexity** | O(1) | Constant time per streaming update. |
| **Accuracy** | High | -12 dB/octave rolloff outside passband (2nd order). |
| **Timeliness** | 8/10 | Minimal phase lag within passband. |
| **Smoothness** | 9/10 | Butterworth maximally flat response in passband. |

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **Pine Script** | Validated | Ported from validated Ehlers/TradingView implementation. |
| **Synthetic** | Validated | Multi-frequency sine waves confirm bandpass behavior. |
| **Self-Consistency** | Validated | Streaming, batch, span, and eventing modes produce identical results. |
| **BPF Cross-Check** | Validated | Same Butterworth coefficient architecture as BPF. |

## Common Pitfalls

1. **Expecting overlay behavior**: The Roofing Filter oscillates around zero. It is NOT a price overlay. Plot it in a separate window (`SeparateWindow = true`).

2. **Confusing parameter semantics with BPF**: In BPF, `lowerPeriod` is the HP cutoff and `upperPeriod` is the LP cutoff. In Roofing, `hpLength` is the HP cutoff and `ssLength` is the SS (LP) cutoff. Same math, different naming.

3. **Choosing ssLength > hpLength**: While not invalid, setting the smoother period larger than the highpass period creates an unusual passband. Typical usage keeps `ssLength` well below `hpLength` (e.g., 10 vs 48).

4. **Ignoring warmup**: The first `hpLength` bars are "cold." Trade signals taken before warmup completion are unreliable. Check `IsHot` before acting on zero crossings.

5. **Overfitting cutoff periods**: The default 48/10 works for daily charts on most liquid instruments. Changing these for specific instruments risks curve-fitting to historical noise.

6. **Assuming stationarity**: The Roofing Filter assumes a fixed passband. If the dominant market cycle shifts outside the passband, the filter will attenuate real signal.

## References

1. John F. Ehlers. "Cycle Analytics for Traders." Wiley, 2013.
2. John F. Ehlers. "Predictive Indicators for Effective Trading Strategies." Technical Analysis of Stocks and Commodities, January 2014.
3. thinkorswim. "EhlersRoofingFilter" study documentation.

## Usage

```csharp
using QuanTAlib;

// Default: HP=48 (remove trend > 48 bars), SS=10 (remove noise < 10 bars)
var roofing = new Roofing(hpLength: 48, ssLength: 10);

// Streaming update
var result = roofing.Update(new TValue(DateTime.UtcNow, price));
// result.Value oscillates around 0. Positive = bullish cycle, negative = bearish.

// Static batch (zero allocation)
double[] output = new double[prices.Length];
Roofing.Batch(prices, output, hpLength: 48, ssLength: 10);

// Event-driven chaining
var source = new TSeries();
var roofingChained = new Roofing(source, hpLength: 48, ssLength: 10);
source.Add(new TValue(DateTime.UtcNow, price)); // roofingChained.Last auto-updates
```
