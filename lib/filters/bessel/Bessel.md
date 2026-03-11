# BESSEL: Bessel Filter

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Filter                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `length`                      |
| **Outputs**      | Single series (Bessel)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `length` bars                    |
| **PineScript**   | [bessel.pine](bessel.pine)                       |
| **Signature**    | [bessel_signature](bessel_signature.md) |

- The Bessel Filter is a 2nd-order low-pass IIR filter designed to preserve the **shape** and **timing** of price moves.
- Parameterized by `length`.
- Output range: Tracks input.
- Requires `length` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> When you care more about *when* the market turns than how aggressively you can torture the noise, you reach for a Bessel.

The Bessel Filter is a 2nd-order low-pass IIR filter designed to preserve the **shape** and **timing** of price moves. Unlike sharper filters that chase steep roll-off at the expense of phase distortion, the Bessel family is engineered for a **maximally flat group delay**: signals are delayed, but not deformed.

This implementation follows John Ehlers–style adaptations for financial time series and is tuned for O(1) updates and zero heap allocations in QuanTAlib.

## The Standard

Originally derived from Friedrich Bessel’s work on Bessel polynomials and later adapted to signal processing, the Bessel filter became popular where **waveform integrity** matters more than raw attenuation: control systems, audio, and here, price series.

In trading terms:

* You keep the **relative timing** of swings.
* You avoid overshoot and ringing common in sharper filters.
* You accept a gentler roll-off as the price of cleaner turning points.

QuanTAlib implements the **2nd-order low-pass** variant used in Ehlers-style digital filters.

## Architecture & Physics

BESSEL is implemented as a **2nd-order IIR filter** with a fixed structure:

* State: last two filtered values plus last valid input
* Behavior:
  * Short warmup period (a few bars)
  * Stable, monotonic smoothing
  * Minimal overshoot on sharp transitions

Conceptually:

* High frequencies are attenuated gradually.
* Phase is nearly linear in the passband, so local structures (peaks, troughs, breakout steps) keep their relative timing.
* It runs as an **O(1)** streaming update:
  * One input in, one output out, constant work per bar.

### Specific Architectural Challenge

The main tension is:

* The design demands **IIR smoothness** and responsiveness.
* Recursive instability or phase warping in turning zones cannot be tolerated.

BESSEL solves this by:

* Fixing a 2nd-order topology with coefficients derived from the Bessel prototype.
* Using a **safe minimum length** (at least 2) to keep coefficients in a numerically stable region.
* Treating non-finite values via a last-valid-value cache so NaNs and infinities never poison the state.

## Mathematical Foundation

Let $L$ be the user-specified length (cutoff period). Internally it is clamped as

$$ L_{\text{safe}} = \max(L, 2) $$

The coefficients are:

$$ a = e^{-\pi / L_{\text{safe}}} $$
$$ b = 2 a \cos\!\left(1.738 \frac{\pi}{L_{\text{safe}}}\right) $$
$$ c_2 = b $$
$$ c_3 = -a^2 $$
$$ c_1 = 1 - c_2 - c_3 $$

The constant $1.738 \approx \sqrt{3}$ is chosen to match the 2nd-order Bessel group-delay characteristics.

For an input price series $s[n]$, the recursive filter is

$$ \text{BESSEL}[n] = c_1 s[n] + c_2\, \text{BESSEL}[n-1] + c_3\, \text{BESSEL}[n-2] $$

with initialization:

* For the first few bars, the filter output is seeded directly from the price (no recursion) to avoid transient garbage.

### NaN and Infinity Handling

For robustness:

* Maintain a `LastValidValue` cache $v_{\text{last}}$.
* For each input $x$:
  * If $x$ is finite, set $v_{\text{last}} = x$.
  * If $x$ is `NaN` or infinite, use $x \leftarrow v_{\text{last}}$.
* The recursive update always runs on a finite input.

## Performance Profile

### Operation Count (Streaming Mode)

Bessel implements a maximally flat group-delay 2nd-order IIR biquad. Five coefficients applied per bar via the standard difference equation.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Input state shift | 2 | ~1 cy | ~2 cy |
| Feedforward FMA (b0*x + b1*x1 + b2*x2) | 3 | ~4 cy | ~12 cy |
| Feedback FMA (a1*y1 + a2*y2) | 2 | ~4 cy | ~8 cy |
| State update | 2 | ~1 cy | ~2 cy |
| **Total** | **9** | — | **~24 cycles** |

O(1) per bar. Coefficients precomputed from the period parameter. ~24 cycles/bar.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| IIR biquad recursion | No | Sequential: y[n] = f(y[n-1], y[n-2]) |

Recursive IIR baseline: ~24 cy/bar scalar.

BESSEL is designed for **zero allocations** on the hot path and efficient batch processing for analysis and backtests.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | ★★★★★ | O(1) streaming update. |
| **Allocations** | ★★★★★ | 0 bytes; hot path is allocation-free. |
| **Complexity** | ★★★★★ | Constant time per update. |
| **Precision** | ★★★★★ | `double` precision critical for recursive stability. |

### Zero-Allocation Design

The filter maintains its state in a small set of scalar variables (`_prev1`, `_prev2`, `_lastValidValue`). No arrays or buffers are allocated during the `Update` cycle.

## Validation

Validation focuses on internal consistency between streaming, TSeries, and Span APIs.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Internal consistency verified (Span vs TSeries). |
| **TA-Lib** | ❌ | Not implemented. |
| **Skender** | ❌ | Not implemented. |
| **Tulip** | ❌ | Not implemented. |
| **Ooples** | ❌ | Not implemented. |

### Common Pitfalls

* **Expecting razor-sharp cutoff:** Bessel is **not** a Chebyshev or elliptic filter. Roll-off is gentler by design to preserve shape and timing. If you want violent attenuation of high-frequency noise, pick Butterworth, Chebyshev, or a band-pass.
* **Over-smoothing with large length:** Very large $L$ values will still preserve shape, but you will delay turning points more than necessary. Typical sweet spot for daily data is $L \in [10, 30]$.
* **Misinterpreting flat response as “weak” filter:** The goal is not to crush all noise. The goal is to keep enough structure that pattern recognition, divergence analysis, and multi-stream alignment still make sense.
* **Ignoring NaN propagation:** If your upstream feed throws `NaN` or infinities and you do not clean it, BESSEL will fall back to the last valid value. This is intentional. If you want gaps instead, preprocess the series and pass explicit masked values.

Used correctly, BESSEL gives you a **shape-faithful trend line** with clean timing and low overshoot, ideal for traders who care more about *when* than *how loudly* the filter shouts.
