# EQUAL: Approximate Equality Predicate

> *Floating-point numbers are never exactly equal by accident; Equal asks whether they're close enough on purpose.*

| Property         | Value                                     |
| ---------------- | ------------------------------------------ |
| **Category**     | Signals (Predicates)                       |
| **Inputs**       | A, B (dual series, or A and a fixed scalar) |
| **Parameters**   | `epsilon` (default `SignalConstants.DefaultEpsilon` = 1e-10) |
| **Outputs**      | Single crisp series (Equal)                 |
| **Output range** | `{0.0, 1.0}` while hot; `NaN` while cold     |
| **Warmup**       | `0` bars (hot after the first tick)         |
| **PineScript**   | `a == b` (with tolerance)                   |

- EQUAL tests whether two values (or a value and a fixed scalar) are equal within an epsilon.
- **Similar:** [Above](../above/Above.md), [Below](../below/Below.md) | **Trading note:** A value predicate for detecting a retest of a prior level or a price sitting exactly on a moving average, not C# object identity or structural equality.
- Part of the strategy-primitives signal algebra (`lib/signals/StrategyPrimitives.Spec.md`, Section 9.1).

EQUAL is `ComparePredicateBase<EqualOp>`, the third canonical comparison predicate alongside [Above](../above/Above.md) and [Below](../below/Below.md), sharing the same generic comparison core, scalar-overload, time-join, and cold-NaN behavior. See Above.md for the shared implementation rationale.

## Mathematical Foundation

$$
\text{Equal}_t = \begin{cases} 1.0 & \text{if } |a_t - b_t| \leq \varepsilon \\ 0.0 & \text{otherwise} \end{cases}
$$

The default `ε = 10^{-10}` matches the `1e-10` convention already used by per-class epsilon constants throughout the library (see `momentum/Rs`, `statistics/Correl`). It is a named library constant, deliberately not `double.Epsilon` (a machine-level spacing value unsuitable as a financial comparison tolerance).

### Edge Cases

- **Negative or non-finite epsilon**: rejected at construction (`ArgumentException`).
- **`epsilon = 0`**: degenerates to exact bitwise equality of the sanitized inputs.

## Validation

| Test | Status |
| :--- | :---: |
| Exact equality | ✅ |
| Within default epsilon | ✅ |
| Beyond default epsilon | ✅ |
| Custom (wider) epsilon | ✅ |
| Invalid epsilon rejected | ✅ |
| Time-join across two independent publishers | ✅ |
| Scalar overload against a fixed value | ✅ |

## Common Pitfalls

1. **This is a tolerance test, not C# `==`.** Two values that differ by less than epsilon are "equal" for this predicate's purposes even though they are distinct `double` bit patterns.
2. **Epsilon is fixed per instance.** There is no per-call override; construct a second `Equal` node if a different tolerance is needed elsewhere in the same graph.
3. **Choosing epsilon.** The default (1e-10) suits normalized or index-like values. For raw prices with several significant digits, a coarser epsilon (e.g. a fraction of a tick size) is usually more appropriate.

## Usage Examples

```csharp
// Default epsilon
var eq = new Equal();
var result = eq.Update(5.0, 5.0); // 1.0

// Custom epsilon: within 0.5 of a round number
var nearRoundNumber = new Equal(price, 100.0, epsilon: 0.5);

// Two streams
var priceEqualsVwap = new Equal(price, vwap);
```

## References

- Goldberg, D. (1991). "What Every Computer Scientist Should Know About Floating-Point Arithmetic." *ACM Computing Surveys*.
