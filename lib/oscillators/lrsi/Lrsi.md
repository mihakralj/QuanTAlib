# LRSI: Laguerre RSI

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Oscillator                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `gamma` (default 0.5)                      |
| **Outputs**      | Single series (Lrsi)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `4` bars                          |

### TL;DR

- Laguerre RSI is an adaptive oscillator invented by John Ehlers that replaces standard RSI's Wilder-smoothed gain/loss averages with a 4-stage casca...
- Parameterized by `gamma` (default 0.5).
- Output range: Varies (see docs).
- Requires `4` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "The Laguerre transform lets you trade off between lag and smoothness using a single parameter." — John Ehlers

Laguerre RSI is an adaptive oscillator invented by John Ehlers that replaces standard RSI's Wilder-smoothed gain/loss averages with a 4-stage cascaded Laguerre filter. A single γ (gamma) parameter controls the entire responsiveness-smoothness trade-off. Output is dimensionless, always in [0, 1]. No period selection required.

## Historical Context

John Ehlers introduced Laguerre RSI in *Cybernetic Analysis for Stocks and Futures* (2004, Wiley), drawing on the earlier Laguerre polynomial filter described in the same book. The core insight was that classical RSI's Wilder smoothing is a fixed-lag IIR filter that cannot be tuned without changing the period; Laguerre's four cascaded all-pass stages deliver a free parameter γ that continuously trades lag against noise rejection.

Standard RSI uses only two price-derived time series (upward and downward RMAs of one-bar differences). Laguerre RSI produces four correlated time series (the filter stages L0–L3) and derives its RSI-like signal from the cumulative up and down differences across consecutive stages. This gives it substantially more information per bar while remaining entirely O(1).

No external C# library (Skender, TA-Lib, Tulip, OoplesFinance) implements LRSI. Validation is therefore self-consistency only: all four computation modes (streaming, batch TSeries, span, eventing) must produce bit-identical results, and output must remain strictly in [0, 1] under all conditions.

## Architecture & Physics

### 1. Laguerre Filter Stages

Each stage is a first-order all-pass IIR element parameterised by γ:

$$L_0[n] = (1-\gamma)\cdot p[n] + \gamma \cdot L_0[n-1]$$

$$L_k[n] = -\gamma \cdot L_{k-1}[n] + L_{k-1}[n-1] + \gamma \cdot L_k[n-1], \quad k = 1,2,3$$

The stages implement an orthonormal basis: each successive output is a delayed, damped projection of the input with the previous stage's component subtracted. The coefficient γ ∈ [0, 1) acts as a reflection coefficient in the all-pass lattice.

### 2. RSI Computation on Stage Differences

$$\text{cu} = \sum_{k=0}^{2} \max(L_k - L_{k+1},\ 0)$$

$$\text{cd} = \sum_{k=0}^{2} \max(L_{k+1} - L_k,\ 0)$$

$$\text{LRSI} = \begin{cases} \dfrac{\text{cu}}{\text{cu} + \text{cd}} & \text{if } \text{cu} + \text{cd} \ne 0 \\ 0.5 & \text{otherwise} \end{cases}$$

The 0.5 default covers the degenerate flat-market case where all stages are identical (no movement in any direction).

### 3. Gamma Semantics

| γ | Behaviour |
|---|-----------|
| 0.0 | No memory: L0 = price, L1 = L0⁻¹, L2 = L1⁻¹, L3 = L2⁻¹ — essentially a 4-tap FIR |
| 0.5 | Default: balanced responsiveness and smoothing |
| → 1.0 | Extreme smoothing; stages converge toward price mean; LRSI approaches 0.5 everywhere |

### 4. State Representation

```
record struct State { L0, L1, L2, L3, LastValid }
```

Five doubles only. No circular buffers. Bar correction (`isNew=false`) reduces to a single struct copy — the simplest possible rollback in the library.

### 5. FMA Usage

The all-pass recurrence `−γ·L_k + L_{k-1}[n-1] + γ·L_k[n-1]` maps directly to two FMA calls per stage:

```csharp
s.L0 = Math.FusedMultiplyAdd(g, prevL0, omg * value);            // g*prevL0 + omg*value
s.L1 = Math.FusedMultiplyAdd(g, prevL1, FMA(-g, s.L0, prevL0)); // g*prevL1 + (prevL0 - g*L0)
```

This avoids two intermediate rounding steps per stage, reducing accumulation error over long series.

## Mathematical Foundation

The transfer function of a single Laguerre all-pass element is:

$$H_1(z) = \frac{-\gamma + z^{-1}}{1 - \gamma z^{-1}}$$

For stage 0, the transfer function is a simple lowpass:

$$H_0(z) = \frac{1-\gamma}{1 - \gamma z^{-1}}$$

Cascading four stages shifts the phase progressively while retaining the same magnitude response, spreading spectral energy across the orthogonal basis. The RSI formula then reads out the directional momentum component of this spread.

## Performance Profile


### Operation Count (Streaming Mode)

Laguerre RSI uses a 4-pole Laguerre filter to compute a fast RSI-like oscillator.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| FMA × 4 (4-pole Laguerre filter L0–L3) | 4 | 4 | 16 |
| CMP × 4 (up/down classification per pole) | 4 | 1 | 4 |
| ADD × 2 (CU, CD sums) | 2 | 1 | 2 |
| DIV (CU / (CU+CD)) | 1 | 15 | 15 |
| CMP (div-by-zero guard) | 1 | 1 | 1 |
| **Total** | **12** | — | **~38 cycles** |

Four recursive Laguerre poles + RSI ratio. ~38 cycles per bar.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| Laguerre poles × 4 | **No** | Recursive IIR — each pole depends on prior value |
| CU/CD classification | Yes | VCMPPD + masked accumulate |
| RSI ratio | Yes | VDIVPD after poles computed |

| Operation | Count per bar |
|-----------|--------------|
| FMA calls | 6 (2 per stage for L1–L3) + 1 for L0 |
| Mul/Add total | ~14 floating-point ops |
| Memory access | 5 doubles read + 5 writes (struct promotion → registers) |
| Allocations | 0 (Update path is fully allocation-free) |

| Metric | Score (1–10) |
|--------|-------------|
| Lag | 9 (lower than standard RSI at same apparent smoothness) |
| Noise rejection | 8 |
| Parameterisation | 10 (single γ, mathematically principled) |
| Computational cost | 10 (O(1), no history) |
| Interpretability | 7 (same overbought/oversold logic as RSI but [0,1] not [0,100]) |

SIMD analysis: the four stage computations are sequentially dependent (each stage requires the result of the previous). SIMD across a single bar is not applicable. Across bars: the stage recurrence has a feedback term that prevents loop vectorisation. A pure batch SIMD path is therefore infeasible; the scalar loop with FMA is the correct implementation.

## Validation

No external C# library implements Laguerre RSI. Validation protocol:

| Test | Method | Tolerance |
|------|--------|-----------|
| Streaming == Batch (TSeries) | GBM 300 bars, γ=0.1/0.5/0.9 | 1e-10 |
| Span == TSeries | GBM 300 bars | 1e-10 |
| Eventing == Streaming | GBM 200 bars | 1e-10 |
| Output ∈ [0,1] | High/low volatility GBM | exact |
| Constant price → 0.5 | 200 identical bars | 1e-6 |
| Rising price → >0.8 | 100 bars +1/bar, γ=0.3 | exact |
| Falling price → <0.2 | 100 bars −1/bar, γ=0.3 | exact |
| Higher γ ⇒ lower variance | GBM 500 bars, γ=0.1 vs 0.9 | exact ordering |
| Determinism | Same GBM seed → identical | 1e-10 |

## Common Pitfalls

1. **Confusing [0,1] with [0,100]**: LRSI outputs in unit range; overbought/oversold levels are near 0.8/0.2, not 80/20. Plotting alongside standard RSI without rescaling produces vertical misalignment.

2. **γ = 1.0 produces constant 0.5**: All stages converge to a weighted mean; cu = cd = 0 for any non-spike input. The implementation returns 0.5 by convention; this is mathematically correct but operationally useless. Warn users who set γ ≥ 0.95.

3. **Expecting WarmupPeriod to gate output**: LRSI emits valid output from bar 1 (stages begin updating immediately). `WarmupPeriod = 4` is informational — it marks when all four stages have received at least one distinct value. Unlike period-based indicators, there is no discontinuity at the warmup boundary.

4. **Bar correction rollback is trivially cheap**: Because state is five scalars, `isNew=false` is just `_s = _ps` — no Array.Copy required. Any performance concerns from frequent bar corrections are unfounded for LRSI.

5. **Recursive filter cannot be vectorised**: Do not attempt a SIMD batch path. The stage-to-stage dependency chain is a strict serial recurrence. The only valid performance improvement is FMA (already applied) and ensuring the JIT promotes the state struct to registers (enabled by the local copy pattern).

6. **NaN substitution uses last valid close, not 0.5**: Substituting 0 or 0.5 on a NaN bar would distort the filter state. The last seen finite price is the correct substitution — it keeps the filter state continuous.

7. **γ behaviour is not monotone in lag for all signals**: Lower γ produces a faster filter, but also a noisier RSI signal. The optimum γ for a given instrument depends on frequency content of the underlying price series — there is no universally correct value.

## References

- Ehlers, J.F. (2004). *Cybernetic Analysis for Stocks and Futures*. Wiley. Chapter 14.
- Ehlers, J.F. (2001). *Rocket Science for Traders*. Wiley. Chapter 9 (Laguerre filter foundations).
- Vaidyanathan, P.P. (1993). *Multirate Systems and Filter Banks*. Prentice Hall. (All-pass lattice structures.)
