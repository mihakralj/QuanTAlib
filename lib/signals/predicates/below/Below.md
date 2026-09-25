# BELOW: Less-Than Predicate

> *The mirror image of Above — and just as fundamental to a strategy's vocabulary.*

| Property         | Value                                     |
| ---------------- | ------------------------------------------ |
| **Category**     | Signals (Predicates)                       |
| **Inputs**       | A, B (dual series, or A and a fixed scalar) |
| **Parameters**   | None                                        |
| **Outputs**      | Single crisp series (Below)                 |
| **Output range** | `{0.0, 1.0}` while hot; `NaN` while cold     |
| **Warmup**       | `0` bars (hot after the first tick)         |
| **PineScript**   | `a < b`                                     |

- BELOW tests whether the left value is strictly less than the right value or a fixed scalar.
- **Similar:** [Above](../above/Above.md), [Equal](../equal/Equal.md) | **Trading note:** Feeds "RSI below 30" oversold conditions and `CrossUnder`-style exits.
- Part of the strategy-primitives signal algebra (`lib/signals/StrategyPrimitives.Spec.md`, Section 9.1).

BELOW is `ComparePredicateBase<BelowOp>`, the exact structural mirror of [Above](../above/Above.md): same generic comparison core over `BiInputIndicatorBase` at `period = 1`, same scalar-overload and time-join behavior, same cold-NaN contract. See Above.md for the full implementation rationale, which applies here unchanged with `a < b` in place of `a > b`.

## Mathematical Foundation

$$
\text{Below}_t = \begin{cases} 1.0 & \text{if } a_t < b_t \\ 0.0 & \text{otherwise} \end{cases}
$$

A tie (`a == b`) is `0.0`: the comparison is strict.

## Validation

| Test | Status |
| :--- | :---: |
| Strict inequality at the boundary (tie → false) | ✅ |
| NaN/Infinity substitution | ✅ |
| Time-join across two independent publishers | ✅ |
| Scalar overload against a fixed threshold | ✅ |
| Cold → `NaN`, hot after first tick, `Reset()` returns to cold | ✅ |

## Common Pitfalls

Same as [Above](../above/Above.md): a tie is false, boundary flicker needs [Hysteresis](../hysteresis/Hysteresis.md), and cold reads as `NaN`, not `false`.

## Usage Examples

```csharp
// Fixed threshold: RSI below 30
var rsi = new Rsi(14);
var oversold = new Below(rsi, 30.0);

// Two streams: fast EMA below slow EMA
var fast = new Ema(12);
var slow = new Ema(26);
var bearish = new Below(fast, slow);
```

## References

- Murphy, J. (1999). *Technical Analysis of the Financial Markets*. New York Institute of Finance.
