# VOSS: Ehlers Voss Predictive Filter

> *The best filter is one that tells you what is about to happen, not what already did.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Filter                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period` (default 20), `predict` (default 3), `bandwidth` (default 0.25)                      |
| **Outputs**      | Single series (VOSS)                       |
| **Output range** | Oscillates around zero           |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [voss.pine](voss.pine)                       |

- The Voss Predictive Filter is a two-stage signal processing pipeline that extracts a dominant cycle from noisy price data and then predicts its fut...
- Parameterized by `period` (default 20), `predict` (default 3), `bandwidth` (default 0.25).
- Output range: Tracks input.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

## Introduction

The Voss Predictive Filter is a two-stage signal processing pipeline that extracts a dominant cycle from noisy price data and then predicts its future trajectory using negative group delay. Stage 1 is a two-pole bandpass filter (BPF) that isolates cycles near a specified period. Stage 2 is the Voss predictor, which applies a weighted feedback summation over past output values to shift the filter response forward in time. The result is a leading oscillator that anticipates bandpass zero crossings by a configurable number of bars. Crossings between the Filt (bandpass) and Voss (predictor) lines generate early trade signals with reduced lag.

## Historical Context

John Ehlers introduced the Voss Predictive Filter in his August 2019 TASC article "A Peek Into the Future." The algorithm builds on theoretical work by Henning U. Voss, who demonstrated that universal negative delay filters can be constructed using weighted feedback over a finite history of output samples. Ehlers adapted this concept to financial time series by pairing it with his standard two-pole bandpass filter for cycle extraction. The predictor stage computes `Order = 3 * Predict` weighted lookback terms, where each weight increases linearly from `1/Order` to `1.0`. This linear ramp produces a negative group delay proportional to the `Predict` parameter, effectively looking `Predict` bars into the future of the bandpass signal.

Unlike zero-lag moving averages that reduce delay by overshooting, the Voss predictor achieves genuine anticipation by exploiting the mathematical structure of narrowband signals. When the input contains a dominant cycle near the tuned period, the predictor's weighted sum reconstructs future values with high fidelity. For broadband or aperiodic inputs, prediction accuracy degrades gracefully; the output reverts toward the bandpass filter itself.

## Architecture and Physics

### 1. Two-Pole Bandpass Filter (Stage 1)

The BPF isolates cycles near the specified `Period` using a two-pole recursive structure:

$$F_1 = \cos\!\left(\frac{2\pi}{\text{Period}}\right)$$

$$G_1 = \cos\!\left(\frac{\text{Bandwidth} \cdot 2\pi}{\text{Period}}\right)$$

$$S_1 = \frac{1}{G_1} - \sqrt{\frac{1}{G_1^2} - 1}$$

$$\text{Filt}[n] = \frac{1 - S_1}{2}\bigl(\text{src}[n] - \text{src}[n{-}2]\bigr) + F_1(1 + S_1)\,\text{Filt}[n{-}1] - S_1\,\text{Filt}[n{-}2]$$

The differencing term `(src[n] - src[n-2])` removes DC content. The feedback coefficients `F1` and `S1` create a resonant peak at the target frequency. The `Bandwidth` parameter controls the Q factor (selectivity) of the resonance.

For the first 6 bars (`n <= 5`), Filt is clamped to zero to prevent startup transients from propagating into the predictor.

### 2. Voss Predictor (Stage 2)

The predictor computes a weighted sum of its own past values:

$$\text{Order} = 3 \times \text{Predict}$$

$$\text{SumC} = \sum_{k=0}^{\text{Order}-1} \frac{k+1}{\text{Order}} \cdot \text{Voss}[n - (\text{Order} - k)]$$

$$\text{Voss}[n] = \frac{3 + \text{Order}}{2} \cdot \text{Filt}[n] - \text{SumC}$$

The gain factor `(3 + Order) / 2` scales the current bandpass value, while `SumC` subtracts a weighted average of past predictor values. The linear weight ramp `(k+1)/Order` gives more influence to recent values, creating the negative group delay effect.

### 3. Ring Buffer Implementation

The predictor requires `Order` past Voss values. The implementation uses a ring buffer of size `Order + 1` with modular indexing, making the per-bar cost O(Order) with zero heap allocation in streaming mode.

## Mathematical Foundation

### Transfer Function Analysis

The BPF stage has a z-domain transfer function with conjugate poles at angular frequency $\omega_0 = 2\pi/\text{Period}$:

$$H_{\text{BPF}}(z) = \frac{(1-S_1)/2 \cdot (1 - z^{-2})}{1 - F_1(1+S_1)z^{-1} + S_1 z^{-2}}$$

The Voss predictor stage is an IIR filter with `Order` feedback taps, each weighted linearly. Its transfer function creates constructive interference at the tuned frequency, producing a net negative group delay of approximately `Predict` bars near $\omega_0$.

### Parameter Mapping

| Parameter | Default | Range | Effect |
|-----------|---------|-------|--------|
| Period | 20 | >= 2 | Center frequency of the bandpass |
| Predict | 3 | >= 1 | Bars of anticipation (negative delay) |
| Bandwidth | 0.25 | (0, 1) | Selectivity; lower = narrower passband |

## Performance Profile

### Operation Count (Streaming Mode)

Ehlers Voss Predictive Filter: two-stage pipeline — 2-pole bandpass filter (Stage 1) + weighted feedback predictor (Stage 2). O(Order) per bar where Order = 3 × Predict.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| BPF: diff + 3-term FMA | 3 | ~4 cy | ~12 cy |
| Voss: weighted sum loop | Order | ~5 cy | ~45 cy (Order=9) |
| Voss: gain × filt − sumC | 2 | ~4 cy | ~8 cy |
| State update (shifts) | 4 | ~1 cy | ~4 cy |
| **Total (Order=9)** | **Order+9** | — | **~69 cycles** |

O(Order) per bar. Order = 3 × Predict; at defaults (Predict=3), Order=9. ~69 cycles/bar.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| BPF recursion | No | y[n] depends on y[n-1] and y[n-2] |
| Voss weighted sum | Partial | Inner loop vectorizable but short (Order=9) |

Batch throughput: ~69 cy/bar at defaults.

| Metric | Value |
|--------|-------|
| Time Complexity | O(Order) per bar streaming; O(N * Order) batch |
| Space | Ring buffer of Order+1 doubles + 6-field state struct |
| Allocations per Update | 0 (streaming) |
| SIMD | Not applicable (recursive filter) |
| FMA | Used in BPF stage for coefficient multiplication |

### Quality Metrics (1-10)

| Metric | Score | Notes |
|--------|-------|-------|
| Lag Reduction | 9 | Genuine negative group delay at tuned frequency |
| Noise Rejection | 7 | BPF passband limits noise but Voss amplifies in-band noise |
| Stability | 8 | Linear weights prevent runaway feedback |
| Parameter Sensitivity | 6 | Requires approximate knowledge of dominant cycle period |
| Computational Cost | 7 | O(Order) per bar; Order = 9 at defaults |

## Validation

Since the Voss Predictive Filter is a proprietary Ehlers indicator with no reference implementations in standard TA libraries, validation relies on self-consistency checks:

| Test | Method | Criterion |
|------|--------|-----------|
| Bandpass behavior | Synthetic sinusoids at various periods | In-band signal passes; out-of-band attenuated |
| Predictor lead | Zero-crossing analysis | Voss crosses zero before Filt for in-band signals |
| DC rejection | Constant input | Output converges to zero |
| Mode consistency | Span vs streaming vs batch vs eventing | All four modes match within 1e-9 |
| Determinism | Same input twice | Bitwise identical output |
| Stability | 10,000 bar GBM dataset | All outputs finite |
| NaN safety | Periodic NaN injection | All outputs finite; last-valid substitution |

## Common Pitfalls

1. **Wrong period estimate.** The predictor only works well when the `Period` parameter approximates the actual dominant cycle in the data. Mistuning by more than 30% degrades prediction accuracy significantly.

2. **Over-prediction.** Setting `Predict` too high (e.g., > 5) increases `Order` to 15+, which amplifies noise in the weighted feedback loop. Practical values are 2-5.

3. **Narrow bandwidth on noisy data.** Setting `Bandwidth` below 0.1 creates a very selective filter that rings excessively and responds slowly to cycle changes.

4. **Interpreting Voss as a price level.** Both `Filt` and `Voss` oscillate around zero. They are not price predictions; they are cycle-phase predictions. Use crossings, not levels.

5. **Ignoring warmup.** The BPF needs approximately `Period` bars to stabilize. The first 6 bars are explicitly clamped to zero. Signals during the warmup phase are unreliable.

6. **Using on trending data without detrending.** The BPF removes DC, but strong trends can create aliasing artifacts. Consider pre-processing with a highpass filter for strongly trending instruments.

7. **Bar correction overhead.** The ring buffer of size `Order + 1` is copied on each `isNew` state snapshot. For large `Predict` values, this copy cost increases linearly.

## Usage

```csharp
// Streaming
var voss = new Voss(period: 20, predict: 3, bandwidth: 0.25);
foreach (var bar in data)
{
    var result = voss.Update(bar);
    double vossValue = result.Value;   // Predictor output
    double filtValue = voss.LastFilt;  // Bandpass output
    // Signal: Voss crosses above Filt → bullish
    // Signal: Voss crosses below Filt → bearish
}

// Batch (span)
double[] input = prices.ToArray();
double[] output = new double[input.Length];
Voss.Batch(input, output, period: 20, predict: 3, bandwidth: 0.25);

// Event-driven chaining
var source = new TSeries();
var voss = new Voss(source, period: 20, predict: 3, bandwidth: 0.25);
source.Add(new TValue(DateTime.UtcNow, 100.0));
```

## References

1. Ehlers, J. F. "A Peek Into the Future." *Technical Analysis of Stocks and Commodities*, August 2019.
2. Voss, H. U. "Anticipating chaotic synchronization." *Physical Review E*, 61(5), 2000.
3. Ehlers, J. F. *Cycle Analytics for Traders*. Wiley, 2013.
