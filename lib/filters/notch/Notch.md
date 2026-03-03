# Notch Filter

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Filter                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`, `q` (default 1.0)                      |
| **Outputs**      | Single series (Notch)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |
| **Signature**    | [notch_signature](notch_signature.md) |

### TL;DR

- The Notch Filter is a band-stop filter with a narrow bandwidth.
- Parameterized by `period`, `q` (default 1.0).
- Output range: Tracks input.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> Sometimes the best way to improved signal clarity isn't amplification, but rather the surgical removal of a specific annoyance.

The Notch Filter is a band-stop filter with a narrow bandwidth. It passes all frequencies except those in a narrow band centered on the specified frequency. In trading, this is useful for removing specific cyclic noise or seasonality without phase-distorting the rest of the signal significantly.

## Architecture & Physics

This implementation uses a standard **2-pole IIR (Infinite Impulse Response) Biquad** filter efficiently implemented with a Direct Form I topology (or Direct Form II Transposed for numerical stability if we were picky, but here we use a normalized difference equation).

The filter is recursive; its current output depends on previous inputs and previous outputs. This provides a sharp cutoff (high Q) with very few calculations, but creates distinct phase delay features near the notch frequency.

### The Q Factor

The $Q$ factor controls the selectivity.

* **High Q**: Very narrow notch. Surgical removal. Minimal impact on other frequencies. Slower transient decay.
* **Low Q**: Wide notch. Broader rejection. Faster settling.

## Mathematical Foundation

We normalize the RBJ Audio EQ Cookbook formulas for financial time series where sample rate $F_s = 1$.

### 1. Frequency & Alpha

$$ \omega_0 = \frac{2\pi}{\text{Period}} $$

$$ \alpha = \frac{\sin(\omega_0)}{2Q} $$

### 2. Coefficients

We calculate the normalized coefficients:

$$ a_0 = 1 + \alpha $$
$$ b_0 = \frac{1}{a_0}, \quad b_1 = \frac{-2\cos(\omega_0)}{a_0}, \quad b_2 = \frac{1}{a_0} $$
$$ a_1 = \frac{-2\cos(\omega_0)}{a_0}, \quad a_2 = \frac{1 - \alpha}{a_0} $$

### 3. Difference Equation

The filter is applied using the recurrence:

$$ y[n] = b_0 x[n] + b_1 x[n-1] + b_2 x[n-2] - a_1 y[n-1] - a_2 y[n-2] $$

## Performance Profile

### Operation Count (Streaming Mode)

Notch filter: 2nd-order IIR that attenuates a narrow frequency band. Standard biquad structure with passband at all frequencies except the notch center.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Input state shift | 2 | ~1 cy | ~2 cy |
| Feedforward FMA x3 | 3 | ~4 cy | ~12 cy |
| Feedback FMA x2 | 2 | ~4 cy | ~8 cy |
| State update | 2 | ~1 cy | ~2 cy |
| **Total** | **9** | — | **~24 cycles** |

O(1) per bar. Notch coefficient set computed at construction from center frequency and Q-factor. ~24 cycles/bar.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| IIR biquad recursion | No | Sequential dependency |

Batch throughput: ~24 cy/bar.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | High | 5 multiplies, 4 adds per bar. O(1). |
| **Allocations** | 0 | Stack-based state processing. |
| **Complexity** | O(1) | Recursive IIR Biquad. |
| **Accuracy** | High | Matches theoretical frequency response. |
| **Timeliness** | High | Minimal delay for frequencies far from notch. |
| **Smoothness** | Varied | Depends on signal content and Notch usage. |

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **Manual Calc** | ✅ | Exact match for Period=4, Q=0.5 theoretical values. |
| **Standard** | ✅ | Matches RBJ Audio EQ Cookbook topology. |

### C# Usage

```csharp
// Attenuate a 10-bar cycle with Q=1
var notch = new Notch(period: 10, q: 1.0);
TValue result = notch.Update(new TValue(DateTime.UtcNow, price));

// Static calculation for a full series
TSeries filtered = Notch.Calculate(series, period: 10, q: 1.0);
