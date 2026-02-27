# AGC: Ehlers Automatic Gain Control

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Filter                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `decay` (default 0.991)                      |
| **Outputs**      | Single series (AGC)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `1` bars                          |

### TL;DR

- The Automatic Gain Control normalizes any oscillating signal to the \[-1, +1\] range through exponential peak tracking.
- Parameterized by `decay` (default 0.991).
- Output range: Tracks input.
- Requires `1` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "The purpose of the AGC is to normalize the amplitude of any indicator to unity." — John F. Ehlers, TASC January 2015

## Introduction

The Automatic Gain Control normalizes any oscillating signal to the \[-1, +1\] range through exponential peak tracking. Unlike fixed-window normalization (min-max scaling), AGC adapts continuously: the peak decays exponentially each bar and ratchets up instantly when the signal exceeds the current peak. The result is amplitude-independent comparison of filter outputs across instruments and timeframes. Ehlers introduced AGC as the final stage of his "Universal Oscillator" — a signal-processing chain that converts any price series into a bounded, zero-mean indicator suitable for threshold-based trading signals.

## Historical Context

Ehlers published the AGC technique in *Technical Analysis of Stocks & Commodities* (January 2015) as part of the "Universal Oscillator" article. The concept borrows directly from radio engineering, where automatic gain control circuits maintain constant output amplitude despite varying input signal strength. In the RF domain, AGC dates to the 1920s vacuum tube era and remains fundamental in modern receivers.

The key insight for technical analysis: oscillating filter outputs (bandpass, roofing, super smoother) have amplitude that varies with volatility. Without normalization, a fixed overbought/oversold threshold (say ±0.8) triggers at different volatility regimes. AGC eliminates this dependency by rescaling every signal to unit amplitude.

## Architecture and Physics

### 1. Exponential Peak Decay

The peak envelope decays exponentially each bar:

$$\text{Peak}_i = \delta \cdot \text{Peak}_{i-1}$$

where $\delta$ is the decay factor (default 0.991). The half-life in bars:

$$t_{1/2} = \frac{\ln 2}{\ln(1/\delta)} = \frac{0.6931}{\ln(1/0.991)} \approx 77 \text{ bars}$$

### 2. Peak Ratchet

When the absolute signal exceeds the decayed peak, the peak snaps to the new value:

$$\text{Peak}_i = \max(\delta \cdot \text{Peak}_{i-1},\; |\text{Signal}_i|)$$

This creates an asymmetric envelope: instant response to amplitude increases, gradual decay for decreases.

### 3. Normalization

$$\text{AGC}_i = \frac{\text{Signal}_i}{\text{Peak}_i}$$

Output is bounded to \[-1, +1\] for well-behaved oscillating inputs.

## Mathematical Foundation

### Transfer Characteristics

AGC is a nonlinear, time-varying gain element. The effective gain at bar $i$:

$$G_i = \frac{1}{\text{Peak}_i}$$

For a stationary sine wave with amplitude $A$ and period $P$, after sufficient bars the peak converges to:

$$\text{Peak}_\infty = A$$

since peak ratchets to $A$ at each cycle peak and the decay $\delta^{P/4}$ (quarter-cycle between peaks) is less than the ratchet-up. The normalized output then equals $\sin(\omega t)$ exactly.

### Decay Parameter Mapping

| Decay ($\delta$) | Half-life (bars) | Character |
|---|---|---|
| 0.95 | ~14 | Aggressive — fast adaptation |
| 0.98 | ~34 | Moderate |
| 0.991 | ~77 | Default — smooth adaptation |
| 0.999 | ~693 | Conservative — slow adaptation |

### Initialization

Peak initializes to $10^{-10}$ (tiny positive) to avoid division by zero on the first bar. After one bar with a finite input, peak ratchets to $|\text{input}|$ and normal operation begins.

## Performance Profile

### Operation Count (Streaming Mode)

AGC (Adaptive Gain Control) applies a slow EMA to estimate signal level, then scales the signal by the inverse of that level. O(1) per bar.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Level EMA (FMA) | 1 | ~4 cy | ~4 cy |
| Gain = 1 / level (division) | 1 | ~10 cy | ~10 cy |
| Output multiply | 1 | ~3 cy | ~3 cy |
| **Total** | **3** | — | **~17 cycles** |

O(1) per bar. The division dominates; precomputing gain incrementally saves it but adds state. ~17 cycles/bar.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| Level EMA recursion | No | Sequential IIR dependency |
| Gain division | No | Depends on current EMA |
| Output multiply | N/A | Single scalar multiply |

Fully recursive. Batch throughput: ~17 cy/bar.

| Metric | Value |
|---|---|
| Operations per bar | 1 multiply + 1 compare + 1 divide |
| Memory | 3 doubles (peak, lastValid, count) |
| Complexity | O(1) |
| Warmup | 1 bar |
| SIMD potential | Low (data-dependent branching) |

### Quality Metrics

| Metric | Score (1-10) |
|---|---|
| Amplitude normalization | 10 |
| Latency | 10 (zero delay) |
| Adaptation speed | 8 (asymmetric — fast up, slow down) |
| Noise sensitivity | 7 (peak tracks noise spikes) |

## Validation

AGC is a proprietary Ehlers normalizer with no external library implementations. Validation relies on self-consistency:

| Test | Status |
|---|---|
| Sine wave → bounded \[-1, +1\] | ✅ |
| Growing amplitude → stays bounded | ✅ |
| Decaying amplitude → peak adapts | ✅ |
| Streaming matches span | ✅ |
| Deterministic | ✅ |
| NaN-safe | ✅ |
| All 4 modes consistent | ✅ |

## Common Pitfalls

1. **Feeding raw price** — AGC on close prices produces a flatline near 1.0 because the peak tracks the price. Always pre-filter with a bandpass/roofing filter first.

2. **Decay too aggressive** — Low decay values (< 0.95) cause the peak to shrink rapidly between cycles, producing output that overshoots ±1 when the next peak arrives.

3. **Decay too conservative** — High decay values (> 0.999) make the normalizer sluggish; amplitude changes take hundreds of bars to reflect.

4. **Noise spikes** — A single large noise spike ratchets the peak up, compressing subsequent output until the peak decays back. Pre-filtering mitigates this.

5. **Conflating AGC with rescaling** — AGC is NOT min-max normalization. It tracks a running peak envelope, not the full range.

6. **Expecting symmetry** — AGC responds instantly to amplitude increases but requires $t_{1/2}$ bars to adapt to decreases. This asymmetry is intentional.

## Usage

```csharp
// Standalone AGC on pre-filtered signal
var roofing = new Roofing(48, 10);
var agc = new Agc(0.991);
foreach (var bar in series)
{
    var filtered = roofing.Update(bar);
    var normalized = agc.Update(filtered);
    // normalized.Value is in [-1, +1]
}

// Span API
double[] prices = series.Values.ToArray();
double[] filtered = new double[prices.Length];
double[] output = new double[prices.Length];
Roofing.Batch(prices, filtered, 48, 10);
Agc.Batch(filtered, output, 0.991);

// Event chaining
var source = new TSeries();
var roofing = new Roofing(source, 48, 10);
var agc = new Agc(roofing, 0.991);
source.Add(new TValue(DateTime.UtcNow, close));
// agc.Last.Value is automatically updated
```

## References

- Ehlers, J. F. "The Universal Oscillator." *Technical Analysis of Stocks & Commodities*, January 2015.
- Ehlers, J. F. *Cycle Analytics for Traders*. Wiley, 2013.
