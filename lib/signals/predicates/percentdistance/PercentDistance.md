# PERCENTDISTANCE: Relative Distance

> *"More than 2% above its moving average" is a two-step question — compute the distance, then compare it — and PercentDistance is the first step.*

| Property         | Value                                     |
| ---------------- | ------------------------------------------ |
| **Category**     | Signals (Predicates)                       |
| **Inputs**       | A, B (dual series)                          |
| **Parameters**   | None                                        |
| **Outputs**      | Single series (PercentDistance)             |
| **Output range** | `(-\infty, +\infty)`; `NaN` while cold        |
| **Warmup**       | `0` bars (hot after the first tick)         |
| **PineScript**   | `(a - b) / b`                               |

- PERCENTDISTANCE computes the signed relative distance of a from b: `(a - b) / b`.
- **Similar:** [Above](../above/Above.md), [Below](../below/Below.md) | **Trading note:** Feeds a follow-up `Above`/`Below` call rather than being read directly, e.g. `Above(new PercentDistance(price, sma), 0.02)` for "more than 2% above its moving average."
- Part of the strategy-primitives signal algebra. A documented exception to the `[0, 1]` / crisp value contract (`lib/signals/StrategyPrimitives.Spec.md`, Section 9.1): "raw ratio ... documented exception to the range rule; feed into Above/Below."

## Mathematical Foundation

$$
\text{PercentDistance}_t = \frac{a_t - b_t}{b_t}
$$

### Why This Predicate Is Unbounded, Unlike Its Neighbors

Every other node in `signals/predicates/` outputs a crisp `{0, 1}` or graded `[0, 1]` value, per the Section 8.1 value contract. `PercentDistance` deliberately does not: distance-from-a-reference is naturally an unbounded signed quantity (a price can be arbitrarily far above or below its moving average), and forcing it into `[0, 1]` would lose the sign and the magnitude information the very next node (`Above`, `Below`) needs to compare against a threshold like `0.02` (2%). It lives in `signals/predicates/` rather than `numerics/` specifically because its purpose is to feed a predicate comparison, not general-purpose arithmetic — see `numerics/Sub`'s docs for the boundary between the two categories.

### Zero-Denominator Policy

Matches `numerics/Div` exactly: publishes `0.0` (not `NaN` or `±Infinity`) when `|b| <= Div.Epsilon` (1e-12). The reason is the same one documented on `Div`: this class is built on `BiInputIndicatorBase` at `period = 1`, and a non-finite `ComputeError` result would permanently poison the base's incrementally maintained running sum, even though the underlying single-slot buffer self-corrects on the very next bar. See `numerics/Div.md` for the full explanation of this hazard.

## Implementation Details

Built on `BiInputIndicatorBase` with a single-slot window (`period = 1`), the same reuse pattern as `numerics/Add`/`Sub`/`Mul`/`Div` and the `Above`/`Below`/`Equal` comparison predicates. Overrides `Reset()` to restore the `NaN` cold-state sentinel (the inherited `BiInputIndicatorBase.Reset()` clears `Last` to its type default, `0.0`, which would otherwise silently violate the cold-state contract).

## Performance Profile

| Operation | Count | Notes |
| :--- | :---: | :--- |
| Subtraction | 1 | `a - b` |
| Division | 1 | Guarded by the zero-denominator check |
| Sanitize (NaN check) | 2 | One per input |
| **Total** | **~4** | O(1) constant time |

## Validation

| Test | Status |
| :--- | :---: |
| Positive and negative distance | ✅ |
| Zero-denominator publishes 0.0, not NaN | ✅ |
| Zero-denominator bar does not poison the next bar's result | ✅ |
| Composition with `Above` for a threshold-distance condition | ✅ |
| `isNew = false` rollback | ✅ |
| Cold → `NaN`, hot after first tick, `Reset()` returns to cold | ✅ |

## Common Pitfalls

1. **This is not a crisp predicate.** Reading `PercentDistance`'s output directly as a truth value is a mistake; it must be composed with `Above`/`Below`/`Equal` to become one.
2. **Zero denominator returns 0.0, not NaN.** Same departure from the more familiar NaN-propagation convention as `numerics/Div`, and for the identical architectural reason — see `numerics/Div.md`.
3. **Sign convention:** positive means `a` is above `b`; do not swap the argument order expecting the opposite sign without checking downstream comparisons.

## Usage Examples

```csharp
// "More than 2% above its 20-period SMA"
var price = new Sma(1);
var sma = new Sma(20);
var distance = new PercentDistance(price, sma);
var farAboveMa = new Above(distance, 0.02);
```

## References

- Murphy, J. (1999). *Technical Analysis of the Financial Markets*. New York Institute of Finance.
