# HT_SINE: Ehlers Hilbert Transform SineWave (also known as SINE)

> *The Hilbert sine wave renders cycle timing visible — crossovers of sine and lead-sine mark turning points.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Cycle                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | None                      |
| **Outputs**      | Single series (HT_SINE)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `LOOKBACK` bars                          |
| **PineScript**   | [ht_sine.pine](ht_sine.pine)                       |

- HT_SINE extracts the dominant market cycle phase and outputs both Sine and LeadSine (45° phase advance) for cycle timing.
- No configurable parameters; computation is stateless per bar.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

HT_SINE extracts the dominant market cycle phase and outputs both Sine and LeadSine (45° phase advance) for cycle timing. The crossover of these two waves identifies turning points in ranging markets up to one-eighth of a cycle early. Compatible with TA-Lib's `HT_SINE` function, the indicator builds on the full Hilbert Transform cascade (phasor extraction, homodyne period estimation, DFT phase accumulation) to produce dual bounded $[-1, +1]$ oscillators that track cycle position rather than price amplitude.

## Historical Context

John Ehlers introduced the Hilbert Transform SineWave in *Rocket Science for Traders* (2001) as part of his signal processing framework for financial markets. Traditional oscillators (RSI, Stochastic) respond to price amplitude, inherently lagging reversals. HT_SINE measures cycle phase directly, theoretically providing zero-lag detection of cycle turning points. The LeadSine output advances the phase by 45°, creating a built-in early warning system: when LeadSine diverges from Sine, a reversal is approaching. The dual-line design provides both confirmation (crossover) and anticipation (LeadSine leading). The indicator is most effective in ranging markets with well-defined cycles; in strong trends, the two lines travel in parallel ("snake pattern"), correctly indicating that no cyclical reversal is imminent.

## Architecture & Physics

### 1. Hilbert Transform Cascade

The full TA-Lib Hilbert pipeline: 4-bar WMA smoothing, Hilbert FIR with coefficients $A = 0.0962$, $B = 0.5769$, phasor extraction ($I_2$, $Q_2$), EMA smoothing ($\alpha = 0.2$).

### 2. Homodyne Period Estimation

$$Re_t = 0.2(I_{2,t} \cdot I_{2,t-1} + Q_{2,t} \cdot Q_{2,t-1}) + 0.8 \cdot Re_{t-1}$$

$$Im_t = 0.2(I_{2,t} \cdot Q_{2,t-1} - Q_{2,t} \cdot I_{2,t-1}) + 0.8 \cdot Im_{t-1}$$

$$Period = \frac{2\pi}{\arctan(Im / Re)}$$

Clamped to $[6, 50]$, then smoothed ($\alpha = 0.33$).

### 3. DC Phase via DFT Accumulation

Over the smoothed period $P$:

$$RealPart = \sum_{i=0}^{P-1} \sin\!\left(\frac{2\pi i}{P}\right) \cdot SmoothPrice_{t-i}$$

$$ImagPart = \sum_{i=0}^{P-1} \cos\!\left(\frac{2\pi i}{P}\right) \cdot SmoothPrice_{t-i}$$

$$\phi_t = \arctan\!\left(\frac{RealPart}{ImagPart}\right)$$

With quadrant correction and phase unwrapping.

### 4. Output Generation

$$Sine_t = \sin(\phi_t)$$

$$LeadSine_t = \sin(\phi_t + 45°)$$

### 5. Complexity

$O(P)$ per bar where $P$ is the smoothed period (typically 6-50), due to the DFT accumulation loop. Fixed-size circular buffers (50 + 44 + 64 elements) give $O(1)$ space. Warmup: 63 bars (31 + 32 for TA-Lib compatibility).

## Mathematical Foundation

### Parameters

| Parameter | Description | Default | Constraint |
|-----------|-------------|---------|------------|
| (none) | No user-configurable parameters | | |

All constants are fixed by the TA-Lib specification.

### Crossover Signals

| Pattern | Signal |
|---------|--------|
| Sine crosses above LeadSine | Bullish: cycle turning up from trough |
| Sine crosses below LeadSine | Bearish: cycle turning down from peak |
| Lines parallel, both rising | Uptrend in progress (not cycling) |
| Lines parallel, both falling | Downtrend in progress (not cycling) |
| LeadSine diverges first | Early warning of approaching reversal |

### Output Interpretation

| Output | Range | Meaning |
|--------|-------|---------|
| `Sine` | $[-1, +1]$ | Current cycle phase position |
| `LeadSine` | $[-1, +1]$ | 45° advanced cycle phase (early warning) |

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count per bar | Notes |
|-----------|--------------|-------|
| Hilbert cascade (WMA + 4×FIR + phasor + homodyne) | ~84 | Same pipeline as HT_DCPERIOD |
| DFT sin/cos accumulation | ~4P | P sin + P cos evaluations + 2P FMA |
| Phase ATAN extraction | ~15 | `Math.Atan` transcendental |
| Phase adjustment + unwrapping | ~5 | Quadrant correction + wrapping |
| Final SIN (sine) | ~15 | `Math.Sin` transcendental |
| Final SIN (leadSine) | ~15 | `Math.Sin(φ + π/4)` transcendental |
| **Total (P=20 typical)** | **~214** | **O(P) dominated by DFT + 3 transcendentals** |
| **Total (P=50 worst case)** | **~454** | **Heaviest of the HT family** |

### Batch Mode (SIMD Analysis)

| Aspect | Assessment |
|--------|------------|
| SIMD vectorizable | Partially: DFT inner loop vectorizable; final sin calls are scalar |
| Bottleneck | DFT loop (P sin/cos calls) + 3 final transcendentals per bar |
| Parallelism | DFT accumulation independent; dual sin output trivially parallel |
| Memory | O(P): ~50-element smooth price buffer + ~44-element det buffer + Hilbert state (~1.3 KB) |
| Throughput | Slowest HT variant; ~2.5× HT_DCPHASE due to extra sin evaluations |

## Resources

- **Ehlers, J.F.** *Rocket Science for Traders*. Wiley, 2001.
- **TA-Lib** `TA_HT_SINE()` reference implementation.
- **Ehlers, J.F.** *Cybernetic Analysis for Stocks and Futures*. Wiley, 2004.
- **Hilbert, D.** *Grundzüge einer allgemeinen Theorie der linearen Integralgleichungen*. Teubner, 1912.