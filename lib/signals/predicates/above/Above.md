# ABOVE: Greater-Than Predicate

> *The simplest question a strategy asks, over and over: is this number bigger than that one?*

| Property         | Value                                     |
| ---------------- | ------------------------------------------ |
| **Category**     | Signals (Predicates)                       |
| **Inputs**       | A, B (dual series, or A and a fixed scalar) |
| **Parameters**   | None                                        |
| **Outputs**      | Single crisp series (Above)                 |
| **Output range** | `{0.0, 1.0}` while hot; `NaN` while cold     |
| **Warmup**       | `0` bars (hot after the first tick)         |
| **PineScript**   | `a > b`                                     |

- ABOVE tests whether the left value is strictly greater than the right value or a fixed scalar.
- **Similar:** [Below](../below/Below.md), [Equal](../equal/Equal.md) | **Trading note:** The building block behind every "fast line above slow line" or "RSI above 70" condition; feeds [CrossOver](../../triggers/) triggers and [AllOf](../../combinators/) combinators once those layers exist.
- Part of the strategy-primitives signal algebra (`lib/signals/StrategyPrimitives.Spec.md`, Section 9.1).

ABOVE is the first of the three canonical comparison predicates (`Above`, `Below`, `Equal`) that the specification's Section 8.8 describes as "thin sealed classes over one implementation." All three share `ComparePredicateBase<TOp>`, a generic class built on `BiInputIndicatorBase` at `period = 1`.

## Mathematical Foundation

$$
\text{Above}_t = \begin{cases} 1.0 & \text{if } a_t > b_t \\ 0.0 & \text{otherwise} \end{cases}
$$

A tie (`a == b`) is `0.0`: the comparison is strict.

### Edge Cases

- **NaN/Infinity in either input**: substituted with that input's last valid value before the comparison.
- **Cold (before the first tick)**: publishes `NaN`, not `0.0` or `1.0` — see Implementation Details for why this matters to combinators.

## Implementation Details

### Reuse of `BiInputIndicatorBase` via the Generic Comparison Core

`Above` is `ComparePredicateBase<AboveOp>`, where `AboveOp` is a zero-field readonly struct implementing `static bool Eval(double a, double b, double eps) => a > b`. The JIT specializes `ComparePredicateBase<AboveOp>` with no virtual dispatch. `ComputeError` (the hook `BiInputIndicatorBase` calls each bar) always returns exactly `0.0` or `1.0` — a finite value — which is required: a non-finite `ComputeError` result would permanently poison the base's running sum (documented on `BiInputIndicatorBase` and `numerics/Div`).

### Why Cold Publishes NaN, Not 0.0

Unlike `numerics/Add`, which is a plain arithmetic transform, `Above` is a *signal* predicate, and the strategy-primitives specification's value contract (Section 8.1, rule 3) requires cold nodes to publish `NaN`: "It never publishes a truth value... `NaN` (rather than `0.0`) is required so that `Not` of a cold input stays cold instead of becoming true." `Above`, `Below`, and `Equal` each override `Reset()` to restore this `NaN` sentinel, since the inherited `BiInputIndicatorBase.Reset()` clears `Last` to its type default (`0.0`), which would silently violate this rule.

### Scalar Overload

`new Above(source, 30.0)` never constructs a constant child publisher. It subscribes directly to `source.Pub` and pairs each tick with a fresh `TValue` wrapping the fixed scalar, calling the same two-input `Update` the two-stream constructor uses. This matches Section 8.8's requirement that "Scalar right operands... use a dedicated single-input path and never create a constant child node."

### Time-Join for Two-Stream Construction

`new Above(a, b)` applies `BiInputIndicatorBase.Subscribe`'s time-join rule: a result is only published once both `a` and `b` have reported for the same bar `Time`.

## Performance Profile

| Operation | Count | Notes |
| :--- | :---: | :--- |
| Comparison | 1 | `a > b` |
| Sanitize (NaN check) | 2 | One per input |
| **Total** | **~3** | O(1) constant time |

## Validation

| Test | Status |
| :--- | :---: |
| Strict inequality at the boundary (tie → false) | ✅ |
| NaN/Infinity substitution | ✅ |
| `isNew = false` rollback | ✅ |
| Time-join across two independent publishers | ✅ |
| Scalar overload against a fixed threshold | ✅ |
| Cold → `NaN`, hot after first tick, `Reset()` returns to cold | ✅ |

## Common Pitfalls

1. **A tie is false, not true.** `Above(5.0, 5.0)` is `0.0`. Use `Above` combined with `Equal` (or `>=` semantics built from `Not(Below(...))`) if a tie should count.
2. **Flicker at the boundary.** A noisy value oscillating around a threshold produces repeated true/false flips. Use [Hysteresis](../hysteresis/Hysteresis.md) for a dead-band version of this same test.
3. **Cold reads as `NaN`, not `false`.** Do not treat a `NaN` result as `0.0`; propagating that assumption into `Not` would turn a cold input into a false positive `true`.

## Usage Examples

```csharp
// Fixed threshold: RSI above 70
var rsi = new Rsi(14);
var overbought = new Above(rsi, 70.0);

// Two streams: fast EMA above slow EMA
var fast = new Ema(12);
var slow = new Ema(26);
var bullish = new Above(fast, slow);

// Direct pull-style update
var above = new Above();
var result = above.Update(10.0, 5.0); // 1.0
```

## References

- Murphy, J. (1999). *Technical Analysis of the Financial Markets*. New York Institute of Finance.
