# DIV: Element-wise Division

> *Every ratio indicator - relative strength, a spread normalized by ATR - starts with one number divided by another, and the interesting engineering is what happens when the denominator is zero.*

| Property         | Value                                     |
| ---------------- | ------------------------------------------ |
| **Category**     | Numeric                                    |
| **Inputs**       | A (numerator), B (denominator)              |
| **Parameters**   | None                                        |
| **Outputs**      | Single series (Div)                         |
| **Output range** | $(-\infty, +\infty)$; `0.0` when \|B\| ≤ 1e-12 |
| **Warmup**       | `1` bar                                     |
| **PineScript**   | `a / b`                                     |

- DIV computes the element-wise quotient of two time-aligned streams: result = a / b.
- **Similar:** [Add](../add/Add.md), [Sub](../sub/Sub.md), [Mul](../mul/Mul.md) | **Trading note:** Ratios, relative strength, and volatility-normalized spreads all reduce to a division; DIV is the primitive they compose from.
- Validated against TA-Lib's `Div` function (with strictly positive GBM test data, so the zero-denominator policy is exercised only by this library's own unit tests, not by TA-Lib parity).

DIV is the quotient counterpart to [Mul](../mul/Mul.md). Its zero-denominator policy is the one place this family of operators departs from the "last valid value" convention used everywhere else in the library, and the reason is architectural, not a stylistic choice — see Implementation Details.

## Mathematical Foundation

$$
\text{Div}_t = \begin{cases} a_t / b_t & \text{if } |b_t| > \varepsilon \\ 0.0 & \text{otherwise} \end{cases}
\qquad \varepsilon = 10^{-12}
$$

### Edge Cases

- **NaN/Infinity in either input**: substituted with that input's last valid value before the quotient is computed.
- **Denominator within ε of zero**: publishes `0.0` (see below for why, not NaN or the last valid quotient).
- **Both inputs cold**: not hot until the first bar has been processed.

## Implementation Details

### Why zero, not NaN, on division by zero

DIV is built on `BiInputIndicatorBase` with `period = 1`, the same reuse pattern as [Add](../add/Add.md), [Sub](../sub/Sub.md), and [Mul](../mul/Mul.md). At `period = 1`, the base's windowed Kahan-compensated running sum degenerates to the current bar's `ComputeError(a, b)` result — but only as long as that result is always finite.

If `ComputeError` returned `NaN` or `±Infinity` on a zero denominator (as `momentum/Rs` does for its ratio, since it does *not* share this base), the base's incrementally maintained `Sum` field would be permanently poisoned: `Sum = Sum + delta`, and once `delta` is `NaN`, every subsequent `Sum` is `NaN` too, even though the underlying single-slot `RingBuffer` itself self-corrects on the very next `Add`. A "last valid quotient" policy — which would preserve the more familiar NaN-avoidance convention — would need extra rollback-aware state that `ComputeError`'s fixed two-argument signature has no hook to save and restore correctly across `isNew = false` bar corrections.

Given that constraint, DIV uses a **stateless, deterministic** zero-denominator policy: publish `0.0`. This is always correct under rollback (it is a pure function of the two finite sanitized inputs, with no memory of prior bars) and never poisons the shared running sum. Callers that need "last valid quotient" semantics instead should guard the denominator upstream — for example with a `MinStopDistance`-style floor — before feeding it to `Div`.

### Time-Join for Event-Chained Construction

`new Div(a, b)` applies the same time-join rule as `Add`: a result is only published once both sources have reported for the same bar `Time`.

## Performance Profile

### Operation Count (Per Bar)

| Operation | Count | Notes |
| :--- | :---: | :--- |
| Comparison (zero guard) | 1 | \|b\| > ε |
| Division | 1 | Conditional on the guard |
| Sanitize (NaN check) | 2 | One per input |
| **Total** | **~4** | O(1) constant time |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact arithmetic away from the zero guard |
| **Timeliness** | 10/10 | Zero lag |
| **Memory** | 10/10 | O(1), no extra state despite the zero-guard policy |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | ✅ | `Div` function, batch and streaming parity (positive test data only) |
| **Manual** | ✅ | Zero-denominator policy and non-poisoning of the running sum across the next bar |

## Common Pitfalls

1. **Zero denominator returns 0.0, not NaN**: this differs from `momentum/Rs`, which returns NaN for the same situation. The difference is deliberate — see Implementation Details — and callers relying on NaN propagation to detect a bad ratio must check the denominator themselves.
2. **Epsilon is fixed at `1e-12`**: exposed as `Div.Epsilon`. There is no constructor parameter to change it; guard the denominator upstream if a coarser threshold is needed.
3. **Not immediately hot**: `IsHot` is false until the first bar.
4. **Time-join silence**: event-chained construction emits nothing until both sources report for the current bar.
5. **Do not port the "return NaN" pattern from other indicators onto a `BiInputIndicatorBase` subclass**: any non-finite `ComputeError` result permanently poisons the shared running sum (see Implementation Details). This is a correctness rule for the base class, not specific to `Div`.

## Usage Examples

```csharp
// Direct pull-style update
var div = new Div();
var result = div.Update(10.0, 4.0); // 2.5
var guarded = div.Update(10.0, 0.0); // 0.0, not NaN

// Event-chained: a spread normalized by ATR
var spread = new Sub(fast, slow);
var atr = new Atr(14);
var normalized = new Div(spread, atr);
```

## References

- IEEE 754-2019. *IEEE Standard for Floating-Point Arithmetic*.
