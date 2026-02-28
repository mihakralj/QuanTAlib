# FISHER04: Ehlers Fisher Transform (2004 Cybernetic Analysis)

> "The Fisher Transform provides clear, unambiguous turning points that make it possible to identify trend reversals." — John Ehlers, *Cybernetic Analysis for Stocks and Futures* (2004)

## Introduction

The Fisher04 indicator implements the revised Fisher Transform from Chapter 1 of Ehlers' 2004 book *Cybernetic Analysis for Stocks and Futures*. It converts price data into a Gaussian normal distribution using the inverse hyperbolic tangent (arctanh), producing sharp turning-point signals. This 2004 revision uses wider normalization bandwidth, gentler IIR smoothing, and a reduced arctanh multiplier compared to the original 2002 TASC article, resulting in a smoother oscillator with less noise.

## Historical Context

Ehlers first published the Fisher Transform in a November 2002 *Stocks & Commodities* article titled "Using The Fisher Transform." That version used a 0.66 normalization coefficient and 0.67 IIR feedback. Two years later, in *Cybernetic Analysis for Stocks and Futures* (Wiley, 2004), Ehlers revised the coefficients. The 2004 version normalizes with a full 1.0 coefficient and 0.5 IIR feedback, tightens the clamp to 0.9999, and halves the arctanh multiplier from 0.5 to 0.25. No major external library (Skender, TA-Lib, Tulip, Ooples) implements this specific 2004 variant; they all use the 2002 formulation.

## Architecture

### 1. Min/Max Normalization

The lookback window tracks the highest high and lowest low over `period` bars using a `RingBuffer`. The raw price is mapped to [-0.5, 0.5]:

$$\text{norm} = \frac{\text{price} - \text{lowest}}{\text{highest} - \text{lowest}} - 0.5$$

When range is zero (flat price), `Value1` resets to 0.

### 2. IIR Smoothing (Value1)

The normalized value is smoothed with a single-pole IIR filter:

$$\text{Value1}_t = 1.0 \times \text{norm}_t + 0.5 \times \text{Value1}_{t-1}$$

Compare with Fisher (2002): $\text{Value1}_t = 0.66 \times \text{norm}_t + 0.67 \times \text{Value1}_{t-1}$

### 3. Clamping

Value1 is clamped to $(-0.9999, 0.9999)$ to prevent arctanh singularity:

$$\text{Value1} = \text{clamp}(\text{Value1}, -0.9999, 0.9999)$$

The clamped value is stored back for next iteration's IIR feedback.

### 4. Fisher Transform

The Fisher Transform applies arctanh with IIR feedback:

$$\text{Fish}_t = 0.25 \times \ln\!\left(\frac{1 + \text{Value1}}{1 - \text{Value1}}\right) + 0.5 \times \text{Fish}_{t-1}$$

The 0.25 multiplier (vs 0.5 in 2002) produces approximately half the amplitude, reducing false signals.

### 5. Signal Line

The signal line is the previous bar's Fisher value: $\text{Signal}_t = \text{Fish}_{t-1}$

## Coefficient Comparison

| Parameter | Fisher (2002) | Fisher04 (2004) |
|-----------|---------------|-----------------|
| Normalization | 0.66 | 1.0 |
| IIR feedback (Value1) | 0.67 | 0.5 |
| Clamp threshold | 0.99 → 0.999 | 0.9999 |
| Arctanh multiplier | 0.5 | 0.25 |
| Fisher IIR | 0.5 | 0.5 |

## Performance Profile

### Key Optimizations

- **FMA in IIR updates**: Both Value1 IIR and Fisher IIR use `Math.FusedMultiplyAdd` for the `feedback * prev + coeff * input` pattern.
- **Precomputed constants**: Normalization coefficient (1.0), IIR feedback (0.5), clamp threshold (0.9999), arctanh multiplier (0.25) are all `const` fields, avoiding repeated literal encoding.
- **RingBuffer for O(1) update**: `Add` and `UpdateNewest` are constant-time; only the min/max scan is O(period).
- **State copy pattern**: `_state`/`_p_state` record struct enables bar correction without allocation.
- **Zero allocation**: No heap allocation in the `Update` hot path; all state is stack-promoted via local copy.

### Operation Count (Streaming Mode)

| Operation | Count per bar |
|-----------|--------------|
| Comparisons | 2 x period (min/max scan) |
| Multiplications | 2 (normalize + arctanh multiplier) |
| Additions | 3 (normalize offset + 2x IIR) |
| FMA calls | 2 (Value1 IIR, Fisher IIR) |
| Log | 1 (arctanh via `Math.Log`) |
| Clamp | 1 |
| Division | 1 (normalization) |

### SIMD Analysis (Batch Mode)

| Aspect | Status |
|--------|--------|
| Min/max scan | Scalar (RingBuffer-based, O(period) per bar) |
| Normalization | Scalar (data-dependent division) |
| Value1 IIR smoothing | Scalar (sequential IIR dependency) |
| arctanh | Scalar (`Math.Log`, not vectorizable) |
| Fisher IIR | Scalar (sequential dependency on previous Fisher) |
| Vectorization potential | Low: dual IIR chain + logarithm prevents SIMD |

## Validation

No external library implements the 2004 Ehlers variant. Validation is performed against:

- Manual step-by-step computation matching the published algorithm
- Batch vs streaming consistency (tolerance: 1e-12)
- Span vs streaming consistency (tolerance: 1e-12)
- Coefficient difference verification against Fisher (2002)
- Amplitude reduction verification (Fisher04 < Fisher in avg absolute value)

## Common Pitfalls

1. **Confusing 2002 and 2004 versions.** The coefficient differences are subtle but produce measurably different outputs. Using 2002 coefficients with 2004 labels (or vice versa) produces incorrect results.
2. **Not storing clamped Value1 back.** The IIR feedback must use the clamped value, not the pre-clamp value. Failing to store back causes drift.
3. **Expecting identical results to Fisher.** Fisher04 uses 0.25x arctanh multiplier vs 0.5x; the amplitude is roughly halved.
4. **Using Fisher04 for high-frequency scalping.** The gentler coefficients make it slower to react than Fisher (2002). Better suited for swing trading.
5. **Ignoring the signal line crossover.** The primary trading signal is Fisher crossing above/below its one-bar-lagged signal line.

## References

1. Ehlers, J. F. (2004). *Cybernetic Analysis for Stocks and Futures*. Wiley. Chapter 1.
2. Ehlers, J. F. (2002). "Using The Fisher Transform." *Technical Analysis of Stocks & Commodities*, November 2002.
3. MESA Software. "The Inverse Fisher Transform." [mesasoftware.com](http://www.mesasoftware.com)
