# ISFALLING: Falling Predicate

> *The mirror image of IsRising, and just as central to breakdown detection.*

| Property         | Value                                     |
| ---------------- | ------------------------------------------ |
| **Category**     | Signals (Predicates)                       |
| **Inputs**       | Source (single series)                      |
| **Parameters**   | `n` (lookback, must be `>= 1`)              |
| **Outputs**      | Single crisp series (IsFalling)             |
| **Output range** | `{0.0, 1.0}` while hot; `NaN` during warmup  |
| **Warmup**       | `n + 1` bars                                |
| **PineScript**   | `ta.falling(source, n)`                     |

- ISFALLING tests whether the current value is strictly less than the minimum of the n bars preceding it.
- **Similar:** [AtLowest](../atlowest/AtLowest.md), [IsRising](../isrising/IsRising.md) | **Trading note:** The breakdown-below-recent-range test.
- Part of the strategy-primitives signal algebra.

IsFalling is the exact structural mirror of [IsRising](../isrising/IsRising.md): same confirm-then-push window design (a min-tracking `MonotonicDeque` over confirmed prior bars only, never the currently forming one), same reason no rebuild-on-correction is needed, same warmup and cold-state contract. See IsRising.md for the full implementation rationale, which applies here unchanged with the inequality and deque direction reversed.

## Mathematical Foundation

$$
\text{IsFalling}_t(n) = \begin{cases} 1.0 & \text{if } x_t < \min(x_{t-1}, \ldots, x_{t-n}) \\ 0.0 & \text{otherwise} \end{cases}
$$

The window is the previous n bars; a tie is `0.0` (strict comparison).

## Validation

| Test | Status |
| :--- | :---: |
| Warmup returns NaN for the first n bars | ✅ |
| n=1: strictly less than the immediately preceding bar | ✅ |
| Tie returns false (strict comparison) | ✅ |
| Current bar is excluded from its own comparison | ✅ |
| Correction to the forming bar does not disturb the confirmed window | ✅ |
| Batch/streaming parity | ✅ |

## Common Pitfalls

Same as [IsRising](../isrising/IsRising.md): the current bar is excluded from its own comparison, this is distinct from `AtLowest`'s inclusive-window question, and warmup is `n + 1`, not `n`.

## Usage Examples

```csharp
// "Lower low than the last 5 confirmed bars"
var falling5 = new IsFalling(5);
```

## References

- TradingView Pine Script Reference: `ta.falling`.
