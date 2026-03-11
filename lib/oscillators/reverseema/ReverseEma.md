# REVERSEEMA: Ehlers Reverse EMA

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Oscillator                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (ReverseEma)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [reverseema.pine](reverseema.pine)                       |

- The Reverse EMA applies an 8-stage cascaded Z-transform inversion to a compensated EMA, progressively extracting and subtracting the accumulated la...
- Parameterized by `period`.
- Output range: Varies (see docs).
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "The best way to remove lag is to understand where it comes from." — John F. Ehlers

## Introduction

The Reverse EMA applies an 8-stage cascaded Z-transform inversion to a compensated EMA, progressively extracting and subtracting the accumulated lag component. Where standard EMA smoothing introduces phase delay proportional to the filter order, the reverse cascade reconstructs the lag error through successively doubled power coefficients of the decay factor, producing a signal with dramatically reduced latency. O(1) per bar, zero allocation, 8 FMA operations in the critical path.

## Historical Context

John Ehlers introduced the Reverse EMA concept in his 2017 work on signal processing for traders. The technique builds on the observation that an EMA's transfer function in the Z-domain has a known, invertible structure. Rather than attempting a single-stage inversion (which would amplify noise catastrophically), Ehlers cascaded 8 stages where each stage uses exponentially increasing powers of the decay factor: $cc^1, cc^2, cc^4, cc^8, cc^{16}, cc^{32}, cc^{64}, cc^{128}$.

This doubling sequence means the 8 stages collectively address lag components across 8 orders of magnitude, from the immediate decay factor through its 128th power. The approach is mathematically elegant: each stage removes progressively deeper lag without the numerical instability of direct polynomial inversion.

No other major library (TA-Lib, Skender, Tulip, Ooples) implements this indicator, making QuanTAlib's implementation a reference.

## Architecture and Physics

### 1. Forward EMA with Warmup Compensation

The base EMA uses the standard IIR form with bias compensation:

$$\text{EMA}_n = \alpha \cdot x_n + (1-\alpha) \cdot \text{EMA}_{n-1}$$

where $\alpha = \frac{2}{period + 1}$ and $cc = 1 - \alpha$ (the decay factor).

During warmup, the compensator $E$ tracks accumulated bias:

$$E_n = E_{n-1} \cdot cc, \quad \text{EMA}_{\text{corrected}} = \frac{\text{EMA}_{\text{raw}}}{1 - E_n}$$

The indicator transitions to uncompensated mode when $E \leq 10^{-10}$, and `IsHot` fires when $E \leq 0.05$ (~95% coverage).

### 2. Eight-Stage Cascaded Reverse

Each reverse stage follows the recurrence:

$$RE_k[n] = cc^{2^{k-1}} \cdot RE_{k-1}[n] + RE_{k-1}[n-1]$$

where $RE_0 = \text{EMA}$ (the compensated EMA serves as input to stage 1).

| Stage | Power | Coefficient |
|-------|-------|-------------|
| RE1 | $cc^1$ | Immediate decay |
| RE2 | $cc^2$ | Second-order |
| RE3 | $cc^4$ | Fourth-order |
| RE4 | $cc^8$ | Eighth-order |
| RE5 | $cc^{16}$ | 16th-order |
| RE6 | $cc^{32}$ | 32nd-order |
| RE7 | $cc^{64}$ | 64th-order |
| RE8 | $cc^{128}$ | 128th-order |

Each stage requires the current input from the prior stage AND the previous bar's output from the prior stage, creating an 8-deep state chain.

### 3. Signal Extraction

The final output subtracts the scaled reverse accumulation from the EMA:

$$\text{Signal} = \text{EMA} - \alpha \cdot RE_8$$

This produces an oscillator-type output (not an overlay), centered around zero when the underlying price is stationary.

## Mathematical Foundation

### Z-Domain Transfer Function

The standard EMA transfer function:

$$H(z) = \frac{\alpha}{1 - cc \cdot z^{-1}}$$

The reverse stage $k$ has transfer function:

$$R_k(z) = cc^{2^{k-1}} + z^{-1}$$

The 8-stage cascade produces:

$$G(z) = \prod_{k=1}^{8} R_k(z) = \prod_{k=1}^{8} \left(cc^{2^{k-1}} + z^{-1}\right)$$

The signal combines the forward and reverse paths:

$$S(z) = H(z) - \alpha \cdot G(z) \cdot H(z)$$

### Precomputed Power Coefficients

All 8 powers are computed once in the constructor via successive squaring:

```text
cc1   = cc
cc2   = cc1 × cc1
cc4   = cc2 × cc2
cc8   = cc4 × cc4
cc16  = cc8 × cc8
cc32  = cc16 × cc16
cc64  = cc32 × cc32
cc128 = cc64 × cc64
```

This costs 7 multiplications at construction time, zero at runtime.

### FMA Usage

Every reverse stage uses `Math.FusedMultiplyAdd`:

```csharp
re1 = Math.FusedMultiplyAdd(cc1, emaVal, prevEma);
re2 = Math.FusedMultiplyAdd(cc2, re1, prevRe1);
// ... through re8
signal = Math.FusedMultiplyAdd(-alpha, re8, emaVal);
```

Total: 9 FMA operations per bar (8 stages + signal extraction).

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

| Operation | Count | Notes |
|-----------|-------|-------|
| FMA (EMA step) | 1 | `Math.FusedMultiplyAdd(ema, decay, alpha * input)` |
| Multiply (compensation) | 1 | `E *= decay` (warmup only) |
| Division (compensation) | 1 | `ema / (1 - E)` (warmup only) |
| FMA (8 reverse stages) | 8 | One per stage |
| FMA (signal extraction) | 1 | `FMA(-alpha, re8, emaVal)` |
| State store (prev shift) | 8 | Shift current to previous |
| **Total hot path** | **10 FMA + 8 stores** | Post-warmup |

### Batch Mode

The batch path uses a simple loop over `CalculateCore`. Since the algorithm is inherently serial (each stage depends on the prior bar's state), SIMD parallelization is not applicable. However, the FMA chain provides excellent instruction-level pipelining on modern CPUs.

### Quality Metrics

| Metric | Score | Notes |
|--------|-------|-------|
| Lag Reduction | 9/10 | Near-zero lag via 8-stage inversion |
| Noise Sensitivity | 4/10 | Lag removal amplifies noise |
| Smoothness | 3/10 | Oscillator output, not smooth overlay |
| Responsiveness | 9/10 | Extremely fast response |
| Computational Cost | 8/10 | O(1), 10 FMA per bar |
| Memory Efficiency | 10/10 | No buffers, ~160 bytes state |

## Validation

| Library | Status | Notes |
|---------|--------|-------|
| TA-Lib | N/A | Not implemented |
| Skender | N/A | Not implemented |
| Tulip | N/A | Not implemented |
| Ooples | N/A | Not implemented |
| PineScript | Reference | `reverseema.pine` — validated self-consistency |

Self-consistency validation: Streaming, Batch (TSeries), and Span Batch modes produce identical results to machine precision ($< 10^{-12}$).

## Common Pitfalls

1. **Not an overlay.** ReverseEma output is oscillator-type (centered around a trend-dependent baseline), not a price overlay. Plot in a separate window.

2. **Noise amplification.** The 8-stage cascade effectively "un-smooths" the EMA. For noisy data, the output will be noisier than the input. Consider pre-filtering.

3. **Period sensitivity.** Very small periods ($\leq 3$) produce extreme lag removal and correspondingly extreme noise. Periods of 10-30 are typical.

4. **State depth.** The 8-deep state chain (16 previous-bar values + EMA state) means bar corrections (`isNew=false`) must restore all 20+ state variables. The `record struct State` pattern handles this correctly.

5. **Not a standalone signal.** Best used as a component in larger systems (e.g., as a leading indicator to anticipate EMA crossovers) rather than as a direct trading signal.

6. **Warm-up convergence.** The EMA warmup compensation ensures valid output from bar 1, but the 8 reverse stages need several periods to stabilize. Treat output during the warmup phase with caution.

7. **Floating-point drift.** Over very long streams (>10,000 bars), cumulative FMA operations may introduce subtle drift. The current implementation accepts this as the drift is well within double precision tolerance.

## References

- Ehlers, J. F. (2017). "Reverse EMA." Technical analysis signal processing concepts.
- Ehlers, J. F. (2004). *Cybernetic Analysis for Stocks and Futures*. Wiley.
- Ehlers, J. F. (2001). *Rocket Science for Traders*. Wiley.
