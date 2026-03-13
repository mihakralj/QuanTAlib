# CHEBY1: Chebyshev Type I Lowpass Filter

> *Chebyshev Type I trades passband ripple for a steeper rolloff — sharper frequency separation at the cost of amplitude wobble.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Filter                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`, `ripple` (default 1.0)                      |
| **Outputs**      | Single series (Cheby1)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [cheby1.pine](cheby1.pine)                       |
| **Signature**    | [cheby1_signature](cheby1_signature.md) |

- The Chebyshev Type I filter minimizes the error between the idealized and the actual filter characteristic over the range of the passband, but with...
- **Similar:** [Cheby2](../cheby2/Cheby2.md), [Elliptic](../elliptic/Elliptic.md) | **Complementary:** ATR for stop distance | **Trading note:** Chebyshev Type I; passband ripple for sharper transition. Steeper than Butterworth.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The Chebyshev Type I filter minimizes the error between the idealized and the actual filter characteristic over the range of the passband, but with ripples in the passband. This type of filter has a steeper rolloff and more passband ripple (type I) or stopband ripple (type II) than Butterworth filters.

## Parameters

| Parameter | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| **period** | `int` | `10` | The cutoff period. Related to cutoff frequency by $f_c = 1/period$. Must be $\ge 2$. |
| **ripple** | `double` | `1.0` | The maximum allowable ripple in the passband in decibels (dB). Must be $> 0$. |

## Formula

The filter is implemented as a 2nd-order IIR filter using the following difference equation:

$$
y[n] = b_0 x[n] + b_1 x[n-1] + b_2 x[n-2] - a_1 y[n-1] - a_2 y[n-2]
$$

### Filter Design

1. **Cutoff Frequency**: $\omega_c = \frac{2\pi}{N}$
2. **Pre-warped Frequency**: $W_c = \tan(\frac{\omega_c}{2})$
3. **Ripple Factor**: $\epsilon = \sqrt{10^{R/10} - 1}$
4. **Transformation**:
    $$
    \mu = \frac{1}{2} \sinh^{-1}(\frac{1}{\epsilon}) \\
    \sigma = -\sinh(\mu) W_c \\
    \omega_d = \cosh(\mu) W_c
    $$
5. **Intermediate Variables**:
    $$
    K = \sigma^2 + \omega_d^2
    $$
6. **S-Plane to Z-Plane Map (Bilinear Transform)**:
    $$
    b_0' = K \\
    b_1' = 2K \\
    b_2' = K \\
    a_0' = 1 - 2\sigma + K \\
    a_1' = 2K - 2 \\
    a_2' = 1 + 2\sigma + K
    $$
7. **Coefficients**:
    $$
    b_0 = \frac{b_0'}{a_0'}, \quad b_1 = \frac{b_1'}{a_0'}, \quad b_2 = \frac{b_2'}{a_0'} \\
    a_1 = \frac{a_1'}{a_0'}, \quad a_2 = \frac{a_2'}{a_0'}
    $$

## Example Usage

```csharp
using QuanTAlib;

// Create a Cheby1 filter with period 10 and 1dB ripple
var filter = new Cheby1(period: 10, ripple: 1.0);

// Update with a new value
var result = filter.Update(new TValue(DateTime.UtcNow, 100.0));

// result.Value contains the filtered value
```


## Performance Profile

### Operation Count (Streaming Mode)

CHEBY1 implements a 2nd-order IIR biquad: y[n] = b0*x[n] + b1*x[n-1] + b2*x[n-2] - a1*y[n-1] - a2*y[n-2]. Five multiply-add operations per bar; no recursion beyond depth 2.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Input load + state shift | 3 | ~1 cy | ~3 cy |
| Feedforward FMA (b0*x + b1*x1 + b2*x2) | 3 | ~4 cy | ~12 cy |
| Feedback FMA (a1*y1 + a2*y2) | 2 | ~4 cy | ~8 cy |
| Output store + state update | 2 | ~1 cy | ~2 cy |
| **Total** | **10** | — | **~25 cycles** |

O(1) per bar. Coefficients precomputed at construction from period and ripple parameters. ~25 cycles/bar.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| IIR biquad recursion | No | Sequential dependency: y[n] depends on y[n-1] |
| Coefficient computation | N/A | One-time at construction; not on hot path |
| Scalar batch loop | Partial | Loop overhead vectorizable; AR recursion is not |

IIR filters cannot be vectorized across the time axis due to their recursive structure. Throughput is bounded by the biquad latency chain (~25 cy/bar).

## References

- [Chebyshev filter - Wikipedia](https://en.wikipedia.org/wiki/Chebyshev_filter)