# DSO: Ehlers Deviation-Scaled Oscillator

> *When price deviates from its smoothed norm, DSO amplifies the signal through Fisher transformation—producing sharp, decisive oscillator readings that compress during noise and expand during trends.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Oscillator                       |
| **Inputs**       | Source (close)                   |
| **Parameters**   | `period` (default 40)            |
| **Outputs**      | Single series (Dso)              |
| **Output range** | Unbounded (typically ±3)         |
| **Warmup**       | `period` bars                    |
| **PineScript**   | [dso.pine](dso.pine)             |

- DSO (Deviation-Scaled Oscillator) is a Fisher-transformed, RMS-normalized Super Smoother oscillator that measures price deviation from its filtered trend, amplified through a nonlinear Fisher Transform.
- **Similar:** [REFLEX](../reflex/Reflex.md), [TRENDFLEX](../trendflex/Trendflex.md) | **Complementary:** ADX for trend confirmation | **Trading note:** Values beyond ±2 indicate extreme deviation; zero crossings signal direction changes.
- No external validation libraries implement DSO. Validated through self-consistency and behavioral testing.

DSO applies three stages of signal processing: (1) input whitening to remove DC bias and Nyquist aliasing, (2) a 2-pole Super Smoother filter for trend extraction, and (3) RMS normalization followed by a Fisher Transform that amplifies readings near the center and compresses extremes, producing sharp turning-point signals.

## Historical Context

The Deviation-Scaled Oscillator was published by John F. Ehlers in the October 2018 issue of *Technical Analysis of Stocks & Commodities* magazine. Ehlers described it as a "Fisherized" version of his deviation-scaled approach, combining the Super Smoother filter (his signature contribution to technical analysis) with RMS normalization and the Fisher Transform (inverse hyperbolic tangent) to produce an oscillator with Gaussian-distributed output—ideal for statistical threshold-based trading.

## Architecture & Physics

DSO operates in four stages:

### Stage 1: Input Whitening

The raw price is whitened by computing a 2-bar difference:

$$ \text{Zeros}_t = \text{Close}_t - \text{Close}_{t-2} $$

This removes the DC (constant) component and rejects Nyquist frequency aliasing, ensuring only meaningful mid-frequency cycles pass through to the filter.

### Stage 2: Super Smoother Filter (2-pole Butterworth)

The whitened input is smoothed using Ehlers' 2-pole Super Smoother at half-period cutoff:

$$ \text{Filt}_t = \frac{c_1}{2}(\text{Zeros}_t + \text{Zeros}_{t-1}) + c_2 \cdot \text{Filt}_{t-1} + c_3 \cdot \text{Filt}_{t-2} $$

Coefficients are precomputed from the period:

$$ a_1 = e^{-\sqrt{2} \cdot \pi / (\text{period}/2)} $$

$$ c_2 = 2 a_1 \cos\left(\sqrt{2} \cdot \pi / (\text{period}/2)\right), \quad c_3 = -a_1^2, \quad c_1 = 1 - c_2 - c_3 $$

### Stage 3: RMS Normalization

Root Mean Square over the period window normalizes the filtered signal by its recent volatility:

$$ \text{RMS}_t = \sqrt{\frac{1}{N} \sum_{i=0}^{N-1} \text{Filt}_{t-i}^2} $$

$$ \text{ScaledFilt}_t = \frac{\text{Filt}_t}{\text{RMS}_t} $$

Implemented using a `RingBuffer` for O(1) running sum updates. RMS is floored at `1e-10` to prevent division by zero.

### Stage 4: Fisher Transform

The scaled filter output is clamped to ±0.99 and passed through the Fisher (inverse hyperbolic tangent) Transform:

$$ \text{DSO}_t = \frac{1}{2} \ln\left(\frac{1 + \text{clamp}(\text{ScaledFilt}_t)}{1 - \text{clamp}(\text{ScaledFilt}_t)}\right) $$

The Fisher Transform converts the bounded [-1, 1] input into an unbounded Gaussian-like output, amplifying readings near zero (where reversals often originate) and compressing extreme values.

Implemented with FMA for the SSF filter:

```csharp
filt = Math.FusedMultiplyAdd(_c1Half, zeros + _s.Zeros1,
    Math.FusedMultiplyAdd(_c2, _s.Filt, _c3 * _s.Filt1));
```

## Performance Profile

DSO combines a 2-pole IIR filter, O(1) RMS via ring buffer, and the Fisher Transform logarithm.

### Operation Count (Streaming Mode, Scalar)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| **Stage 1: Input Whitening** | | | |
| SUB (Close - Close[2]) | 1 | 1 | 1 |
| **Stage 2: Super Smoother (2-pole Butterworth)** | | | |
| ADD (zeros + zeros1) | 1 | 1 | 1 |
| FMA (c1Half × sum + c2×filt + c3×filt1) | 2 | 4 | 8 |
| MUL (c3 × filt1) | 1 | 3 | 3 |
| **Stage 3: RMS Buffer Update** | | | |
| MUL (filt × filt) | 1 | 3 | 3 |
| ADD/SUB (sumSquared update) | 2 | 1 | 2 |
| FMA (running sum) | 1 | 4 | 4 |
| MUL (sumSquared × periodRecip) | 1 | 3 | 3 |
| SQRT | 1 | 15 | 15 |
| **Stage 4: Fisher Transform** | | | |
| DIV (filt / rms) | 1 | 15 | 15 |
| CLAMP (max/min) | 2 | 1 | 2 |
| ADD/SUB (1±clamped) | 2 | 1 | 2 |
| DIV (ratio) | 1 | 15 | 15 |
| LOG | 1 | 20 | 20 |
| MUL (0.5 × log) | 1 | 3 | 3 |
| **Total** | | | **~97 cycles** |

**Dominant costs:**
- LOG (20 cycles, 21%) — Fisher Transform
- SQRT (15 cycles, 15%) — RMS calculation
- DIV (2×15 cycles, 31%) — RMS normalization + Fisher ratio

### Batch Mode (SIMD Analysis)

DSO is **not SIMD-parallelizable** across bars due to:
1. Super Smoother is a 2-pole IIR filter with recursive state
2. RMS depends on running sum of squared values
3. Fisher Transform LOG is inherently scalar

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 8/10 | Fisher Transform amplifies clean signals near zero |
| **Timeliness** | 8/10 | Input whitening + SSF = low lag for oscillator class |
| **Overshoot** | 7/10 | Clamping at ±0.99 prevents infinity, but Fisher amplifies |
| **Smoothness** | 7/10 | Super Smoother provides good noise rejection |

## Validation

DSO is not implemented in mainstream libraries. Validation relies on behavioral testing.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **TA-Lib** | N/A | Not implemented |
| **Skender** | N/A | Not implemented |
| **Tulip** | N/A | Not implemented |
| **Ooples** | N/A | Not implemented |
| **Behavioral** | ✅ | Validated: constant→zero, symmetry, mode consistency |

### Behavioral Test Summary

- **Constant Input → Zero**: Constant close → zeros=0 → filt=0 → scaledFilt=0 → Fisher(0)=0
- **Fisher Symmetry**: DSO(-x) = -DSO(x) — output is antisymmetric
- **Trending Input**: Strong trend produces non-zero DSO values
- **Mode Consistency**: Streaming, batch, span, and event-driven modes produce identical results
- **Bar Correction**: Snapshot/Restore via RingBuffer produces exact rollback

## Common Pitfalls

1. **Warmup Period**: DSO requires `Period` bars to fill the RMS buffer. Before warmup, output will be unstable. Use `IsHot` to detect readiness.

2. **Close[2] Dependency**: The whitening step `Close - Close[2]` requires tracking two-bar-ago close. State stores both `Src1` and `Src2` for this purpose. On the first two bars, the filter output is zero.

3. **Fisher Transform Singularity**: The Fisher Transform has a singularity at ±1 (ln(0)). Clamping at ±0.99 prevents this. The maximum possible DSO value is ±2.646 (`0.5 * ln(199) ≈ 2.646`).

4. **RMS Floor**: During perfectly flat markets (zero volatility), RMS approaches zero. The `MinRms = 1e-10` floor prevents division by zero but may produce large scaled values. The ±0.99 Fisher clamp provides a second safety net.

5. **Not a Bounded Oscillator**: Unlike RSI or Stochastics, DSO is unbounded. Values beyond ±2 indicate extreme deviation—roughly equivalent to a 2-sigma event in the Fisher-transformed space.

6. **Period Selection**: Ehlers recommends period=40 (approximately one market month of bars on daily charts). Shorter periods increase sensitivity but also noise; longer periods add lag.

7. **Bar Correction**: Like all QuanTAlib indicators, DSO supports bar correction via the `isNew` parameter. The RingBuffer `Snapshot()`/`Restore()` mechanism handles this atomically.
