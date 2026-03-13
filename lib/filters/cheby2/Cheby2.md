# CHEBY2 (Chebyshev Type II / Inverse Chebyshev)

> *Chebyshev Type II pushes the ripple into the stopband, keeping the passband flat while still achieving a steep transition.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Filter                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`, `attenuation` (default 5.0)                      |
| **Outputs**      | Single series (Cheby2)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [cheby2.pine](cheby2.pine)                       |
| **Signature**    | [cheby2_signature](cheby2_signature.md) |

- A Chebyshev Type II filter (also known as Inverse Chebyshev) with O(1) complexity.
- **Similar:** [Cheby1](../cheby1/Cheby1.md), [Elliptic](../elliptic/Elliptic.md) | **Complementary:** Trend strength indicators | **Trading note:** Chebyshev Type II; flat passband with stopband ripple. No overshoot in passband.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

A Chebyshev Type II filter (also known as Inverse Chebyshev) with O(1) complexity. Unlike the Type I filter, Type II is maximally flat in the passband (like Butterworth) but has equiripple in the stopband.

## Algorithm

The filter calculates 2nd order IIR coefficients based on partial fraction expansion of the Chebyshev Type II transfer function.

### Parameters

- `Period`: The cutoff period (related to cutoff frequency).
- `Attenuation`: The minimum attenuation in the stopband in decibels (dB), default 5.0.

### Formula

The coefficients are derived from the poles and zeros of the Chebyshev Type II polynomial:

1. Calculate filter parameters from period and attenuation.
2. Determine poles (`sigma_p +/- j*omega_p`) and zeros (`+/- j*omega_z`).
3. Construct the IIR filter coefficients (`a0, a1, a2, b0, b1, b2`).
4. Apply difference equation:
   $$ y[n] = b_0 x[n] + b_1 x[n-1] + b_2 x[n-2] - a_1 y[n-1] - a_2 y[n-2] $$


## Performance Profile

### Operation Count (Streaming Mode)

CHEBY2 implements a 2nd-order IIR biquad identical in structure to CHEBY1 but with different coefficient derivation (stopband optimized). Five multiply-add operations per bar.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Input load + state shift | 3 | ~1 cy | ~3 cy |
| Feedforward FMA (b0*x + b1*x1 + b2*x2) | 3 | ~4 cy | ~12 cy |
| Feedback FMA (a1*y1 + a2*y2) | 2 | ~4 cy | ~8 cy |
| Output store + state update | 2 | ~1 cy | ~2 cy |
| **Total** | **10** | — | **~25 cycles** |

O(1) per bar. Cost profile identical to CHEBY1; differs only in coefficient calculation (stopband equiripple vs passband equiripple). ~25 cycles/bar.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| IIR biquad recursion | No | Sequential dependency: y[n] depends on y[n-1] |
| Coefficient computation | N/A | One-time at construction |
| Scalar batch loop | Partial | Loop overhead vectorizable; AR recursion is not |

Same SIMD constraints as CHEBY1. The recursive feedback path blocks vectorization. Batch throughput: ~25 cy/bar.

## Usage

```csharp
using QuanTAlib;

// Create a Cheby2 filter with period 10 and 5dB stopband attenuation
var filter = new Cheby2(period: 10, attenuation: 5.0);

// Update with a new value
var result = filter.Update(new TValue(DateTime.UtcNow, price));

// Access result
Console.WriteLine($"Filter value: {result.Value}");
```

## complexity

- **Time**: O(1) per update.
- **Space**: O(1) constant storage.