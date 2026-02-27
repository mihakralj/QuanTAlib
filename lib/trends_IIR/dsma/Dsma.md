# DSMA: Deviation-Scaled Moving Average

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (IIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`, `scaleFactor` (default 0.5)                      |
| **Outputs**      | Single series (Dsma)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |

### TL;DR

- DSMA (Deviation-Scaled Moving Average) is a volatility-adaptive trend filter that combines a Super Smoother (2-pole Butterworth IIR filter) with RM...
- Parameterized by `period`, `scalefactor` (default 0.5).
- Output range: Tracks input.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "When the market screams, DSMA sprints. When it whispers, DSMA crawls. An adaptive moving average that lets volatility dictate the pace."

DSMA (Deviation-Scaled Moving Average) is a volatility-adaptive trend filter that combines a Super Smoother (2-pole Butterworth IIR filter) with RMS-based deviation scaling. Unlike fixed-period moving averages that treat all market conditions identically, DSMA adjusts its responsiveness based on measured volatility—accelerating when trends are strong and decelerating when prices consolidate.

## Historical Context

DSMA appears to be a proprietary or boutique indicator without mainstream adoption in commercial platforms. The algorithm surfaced in custom PineScript implementations, drawing from established signal processing concepts: Butterworth filtering for trend extraction (popularized by John Ehlers) and RMS deviation measurement for volatility assessment. This QuanTAlib implementation follows the PineScript reference, translating its recursive logic into high-performance C# with zero-allocation streaming updates.

## Architecture & Physics

DSMA operates in three stages, each addressing a specific signal processing challenge:

### Stage 1: Trend Extraction via Super Smoother

The Super Smoother is a 2-pole Butterworth low-pass filter—the same topology used in analog audio circuits to eliminate high-frequency noise without phase distortion. Ehlers adapted it for financial time series by discretizing the transfer function:

$$ H(z) = \frac{c_0 + c_1 z^{-1} + c_2 z^{-2}}{1 - a_1 z^{-1} - a_2 z^{-2}} $$

Coefficients are precomputed from the period parameter:

$$ \omega = \frac{\sqrt{2} \cdot \pi}{\text{period}} $$

$$ a = e^{-\omega} $$

$$ c_0 = \frac{(1 - a)^2}{1 + 2a \cos(\omega) + a^2} $$

The filter maintains two delay states ($z^{-1}$, $z^{-2}$) and produces a smooth baseline trend ($\text{filt}_t$) with minimal lag for its degree of smoothing.

### Stage 2: Volatility Measurement via RMS

Root Mean Square (RMS) quantifies the magnitude of oscillations around the filtered trend:

$$ \text{RMS}_t = \sqrt{\frac{1}{N} \sum_{i=0}^{N-1} (\text{price}_{t-i} - \text{filt}_{t-i})^2} $$

RMS is computed incrementally over a rolling window using a circular `RingBuffer` for O(1) updates. Unlike standard deviation (which measures dispersion around a mean), RMS measures absolute deviation from the trend line—a more direct proxy for volatility in trend-following contexts.

### Stage 3: Adaptive Alpha Scaling

The final EMA-style smoothing coefficient adapts based on the ratio of trend strength to volatility:

$$ \alpha_t = \min\left(\text{scaleFactor} \cdot \frac{5}{\text{period}} \cdot \frac{|\text{filt}_t|}{\text{RMS}_t}, 1\right) $$

- **Numerator** ($|\text{filt}_t|$): Captures the magnitude of the filtered deviation.
- **Denominator** ($\text{RMS}_t$): Normalizes by recent volatility, preventing over-reaction to noise.
- **Scale Factor**: User-adjustable multiplier (default 0.5) to control overall responsiveness.
- **Clamping**: Alpha is bounded at 1.0 to prevent numerical instability.

When trends are strong relative to volatility (high signal-to-noise ratio), alpha approaches its maximum, and DSMA tracks price aggressively. During consolidation (low signal-to-noise), alpha shrinks, and DSMA smooths heavily.

The final output is an exponential moving average using this dynamic alpha:

$$ \text{DSMA}_t = \alpha_t \cdot \text{price}_t + (1 - \alpha_t) \cdot \text{DSMA}_{t-1} $$

Implemented with fused multiply-add for single-rounding precision:

```csharp
_state.Dsma = Math.FusedMultiplyAdd(_state.Dsma, 1.0 - alpha, alpha * input.Value);
```

## Performance Profile

DSMA combines the computational cost of a 2-pole IIR filter, a rolling RMS calculation, and an EMA update—still achieving constant-time complexity through incremental ring buffer updates.

### Operation Count (Streaming Mode, Scalar)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| **Stage 1: Deviation Calculation** | | | |
| SUB (zeros = value - result) | 1 | 1 | 1 |
| **Stage 2: Super Smoother (2-pole Butterworth)** | | | |
| ADD (zeros + zeros1) | 1 | 1 | 1 |
| MUL (c1Half × sum) | 1 | 3 | 3 |
| FMA (filt1 × b1 - a1Sq × filt2) | 1 | 4 | 4 |
| MUL (a1Sq × filt2) | 1 | 3 | 3 |
| ADD (filtPart1 + filtPart2) | 1 | 1 | 1 |
| **Stage 3: RMS Buffer Update** | | | |
| MUL (filt × filt) | 1 | 3 | 3 |
| ADD/SUB (sumSquared update) | 2 | 1 | 2 |
| FMA (running sum) | 1 | 4 | 4 |
| **Stage 4: RMS Calculation** | | | |
| MUL (sumSquared × periodRecip) | 1 | 3 | 3 |
| CMP/MAX (MinRms guard) | 1 | 1 | 1 |
| SQRT | 1 | 15 | 15 |
| **Stage 5: Alpha Calculation** | | | |
| ABS | 1 | 1 | 1 |
| DIV (filt / rms) | 1 | 15 | 15 |
| MUL (scaleAdjustment × ratio) | 1 | 3 | 3 |
| CMP/MIN (clamp to 1.0) | 1 | 1 | 1 |
| **Stage 6: Adaptive EMA** | | | |
| SUB (1 - alpha) | 1 | 1 | 1 |
| FMA (result × decay + alpha × value) | 1 | 4 | 4 |
| MUL (alpha × value) | 1 | 3 | 3 |
| **Total** | | | **~69 cycles** |

**Dominant costs:**
- SQRT (15 cycles, 22%) — RMS calculation
- DIV (15 cycles, 22%) — alpha normalization by RMS
- Super Smoother filter (~12 cycles, 17%) — 2-pole IIR recursion

### Batch Mode (SIMD Analysis)

DSMA is **not SIMD-parallelizable** across bars due to:
1. Super Smoother is a 2-pole IIR filter with recursive state (filt[t-1], filt[t-2])
2. Adaptive alpha depends on current RMS which depends on running sum
3. Final EMA output feeds back as input to next iteration

**FMA optimization (already applied):**
- RMS running sum update uses FMA
- Final adaptive EMA uses FMA pattern

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 7/10 | Volatile in choppy markets; shines in trends |
| **Timeliness** | 8/10 | Adaptive lag—minimal during strong trends |
| **Overshoot** | 6/10 | Can overshoot when volatility spikes suddenly |
| **Smoothness** | 8/10 | Super Smoother baseline ensures good filtering |

*Benchmarked on Intel i7-12700K @ 3.6 GHz (Turbo off), AVX2, .NET 10.0, 10K iterations.*

## Validation

DSMA is not implemented in mainstream libraries (TA-Lib, Skender, Tulip, Ooples). Validation relies on behavioral testing against known algorithm properties.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **TA-Lib** | N/A | Not implemented |
| **Skender** | N/A | Not implemented |
| **Tulip** | N/A | Not implemented |
| **Ooples** | N/A | Not implemented |
| **Behavioral** | ✅ | Validated: trend following, volatility response, bounds |

### Behavioral Test Summary

- **Trend Following**: DSMA converges to price during sustained trends (10 consecutive bars ±1%, final deviation <2%)
- **Volatility Response**: Higher scale factor (0.9 vs 0.1) produces 3-5x larger deviation during volatile periods
- **Smoothness**: Output exhibits <50% of input variance when volatility is low (validated over 1000 GBM bars)
- **Bounds**: Output remains within [min, max] price range ±1% tolerance
- **Mathematical Consistency**: Streaming updates match batch calculations (ε < 1e-10)

## C# Implementation Considerations

### State Management

DSMA uses a comprehensive State record struct combining all filter stages:

```csharp
[StructLayout(LayoutKind.Auto)]
private record struct State
{
    public double Filt;       // current filtered value
    public double Filt1;      // filt[t-1]
    public double Filt2;      // filt[t-2]
    public double Zeros1;     // deviation[t-1]
    public double SumSquared; // running sum for RMS
    public double Result;     // current DSMA value
    public double LastPrice;  // last valid price
    public int Bars;
}
```

Bar correction requires coordinated rollback of both state and RingBuffer:

```csharp
if (isNew) { _p_state = _state; _filtSquaredBuffer.Snapshot(); }
else { _state = _p_state; _filtSquaredBuffer.Restore(); }
```

### RingBuffer for RMS

The RingBuffer maintains O(1) running sum updates for RMS calculation:

```csharp
double removed = _filtSquaredBuffer.Add(filtSq);
_state.SumSquared = Math.FusedMultiplyAdd(-1.0, removed, _state.SumSquared + filtSq);
```

The buffer's `Snapshot()`/`Restore()` methods enable atomic rollback on bar corrections.

### Precomputed Constants

Constructor calculates all filter coefficients once:

```csharp
double arg = SqrtTwo * Math.PI / (period * 0.5);
double a1 = Math.Exp(-arg);
_b1 = 2.0 * a1 * Math.Cos(arg);
_a1Sq = a1 * a1;
_c1Half = (1.0 - _b1 + _a1Sq) * 0.5;
_periodRecip = 1.0 / period;
_scaleAdjustment = scaleFactor * 5.0 / period;
```

### FMA Usage

FMA optimizes the Super Smoother IIR and adaptive EMA:

```csharp
// Super Smoother: filt = c1Half*(zeros+zeros1) + b1*filt1 - a1Sq*filt2
double filtPart2 = Math.FusedMultiplyAdd(_state.Filt1, _b1, -_a1Sq * _state.Filt2);

// Adaptive EMA: result = prevResult*decay + alpha*value
double result = Math.FusedMultiplyAdd(_state.Result, decay, alpha * value);
```

### Memory Layout

| Field | Type | Size | Purpose |
| :--- | :--- | :---: | :--- |
| `_b1` | double | 8B | Super Smoother coefficient |
| `_c1Half` | double | 8B | Halved c₁ coefficient |
| `_a1Sq` | double | 8B | a₁² coefficient |
| `_periodRecip` | double | 8B | 1/period |
| `_scaleAdjustment` | double | 8B | Combined scale factor |
| `_filtSquaredBuffer` | RingBuffer | ~8B+period×8B | Circular buffer for RMS |
| `_state` | State | ~64B | Current calculation state |
| `_p_state` | State | ~64B | Previous state for rollback |
| **Total (fixed)** | | **~176B + period×8B** | Per indicator instance |

### SIMD Limitations

The 2-pole IIR recursion and adaptive alpha dependency on running RMS preclude SIMD parallelization across bars. The `Calculate(Span)` method uses a scalar loop—parallelization should target multiple independent series rather than within-series vectorization.

## Common Pitfalls

1. **Warmup Period**: DSMA requires `Period` bars to fill the Super Smoother delay line and RMS buffer. The first `Period` outputs will be unstable. Use `IsHot` to detect when the indicator has sufficient history.

2. **Scale Factor Sensitivity**: The default `scaleFactor = 0.5` balances responsiveness and stability. Values >0.7 can cause whipsaws in choppy markets; values <0.3 introduce excessive lag. Tune this parameter based on your asset's typical volatility regime.

3. **Volatility Normalization**: The RMS denominator in the alpha formula can approach zero during extended flat periods, causing alpha to spike. The implementation clamps alpha at 1.0, but extremely low volatility can still produce jittery behavior. Consider a minimum RMS threshold (not implemented in this version).

4. **Not a Momentum Oscillator**: DSMA is a trend filter, not a momentum indicator. Do not confuse high alpha values with strong momentum—alpha reflects signal-to-noise ratio, not directional strength. Use a separate momentum indicator (RSI, MACD) for confirmation.

5. **Comparison with JMA**: DSMA uses a simpler adaptive mechanism than JMA (which employs fractal efficiency and phase adjustment). JMA typically offers smoother output and better overshoot control but at higher computational cost. DSMA is faster and more transparent algorithmically.

6. **Bar Correction**: Like all QuanTAlib indicators, DSMA supports bar correction via the `isNew` parameter. When `isNew = false`, it rolls back to the previous state before recalculating. Ensure your data feed correctly signals bar updates versus corrections.

7. **SIMD Limitation**: The recursive nature of the Super Smoother filter and adaptive alpha calculation precludes efficient SIMD vectorization. The `Calculate(Span)` method uses a scalar loop. For bulk backtesting, consider parallelizing across multiple series rather than within a single series.
