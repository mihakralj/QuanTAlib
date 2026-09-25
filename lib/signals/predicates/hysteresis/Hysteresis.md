# HYSTERESIS: Schmitt-Trigger Predicate

> *A thermostat doesn't turn the furnace on and off every time the temperature crosses 70 degrees by a tenth — it waits for a real change. Hysteresis brings that same dead band to a trading condition.*

| Property         | Value                                     |
| ---------------- | ------------------------------------------ |
| **Category**     | Signals (Predicates)                       |
| **Inputs**       | Source (single series)                      |
| **Parameters**   | `enter`, `exit` (must satisfy `exit &lt;= enter`) |
| **Outputs**      | Single crisp series (Hysteresis)            |
| **Output range** | `{0.0, 1.0}` while hot; `NaN` before the first value |
| **Warmup**       | `0` bars (hot after the first value)        |
| **PineScript**   | No direct equivalent; a manual `var bool` latch |

- HYSTERESIS latches true once the input reaches `enter`, and stays true until the input falls to `exit` or below, removing the boundary flicker a plain [Above](../above/Above.md)`(threshold)` produces when a noisy value hovers near one level.
- **Similar:** [Above](../above/Above.md), [Below](../below/Below.md) | **Trading note:** The dead-band wrapper referenced by the strategy-primitives specification's note on `Above`/`Below` (Section 9.1: "Optional `Hysteresis(enter, exit)` band removes boundary flicker").
- Part of the strategy-primitives signal algebra.

## Mathematical Foundation

$$
\text{latched}_t = \begin{cases}
\text{true} & \text{if } x_t \geq \text{enter} \\
\text{false} & \text{if } x_t \leq \text{exit} \\
\text{latched}_{t-1} & \text{otherwise (dead band)}
\end{cases}
\qquad \text{Hysteresis}_t = \begin{cases} 1.0 & \text{latched}_t \\ 0.0 & \text{otherwise} \end{cases}
$$

`enter == exit` degenerates to a plain threshold test with no dead band, equivalent to `Above(source, enter)` at the boundary (though still latched, so it will not re-fire on repeated ticks exactly at the threshold the way `Above` would on a tie either side of it).

### Polarity

This implementation supports one polarity — the "upward latch," where `enter` is the higher of the two thresholds (`exit <= enter` is enforced at construction). This covers the overwhelmingly common case: a dead band around a single rising threshold (e.g. latch overbought at RSI ≥ 70, release at RSI ≤ 65). A "downward latch" (latch on a falling threshold, release on a rising one) is not built as a separate polarity; compose it by negating the source or wrapping with `Below` semantics once combinators exist.

### Edge Cases

- **NaN/Infinity input**: holds the last latched state rather than flipping it.
- **Before the first value**: publishes `NaN` (cold), not `false`.
- **`enter < exit`**: rejected at construction.

## Implementation Details

Built directly on `AbstractBase` (not `BiInputIndicatorBase`, since there is one input, not two) with a two-field state record (`Latched`, `HasValue`) and the standard `_state`/`_p_state` rollback pattern. Because the latch decision only ever depends on the current sanitized value and the previous latched state — never on a window — no ring buffer or deque is needed.

## Performance Profile

| Operation | Count | Notes |
| :--- | :---: | :--- |
| Comparison | up to 2 | Against `enter`, then `exit` |
| Finite check | 1 | NaN/Infinity guard |
| **Total** | **~3** | O(1) constant time |

## Validation

| Test | Status |
| :--- | :---: |
| Latches true at `enter` | ✅ |
| Holds state inside the dead band | ✅ |
| Latches false at `exit` | ✅ |
| Degenerates correctly when `enter == exit` | ✅ |
| NaN input holds last latched state | ✅ |
| `isNew = false` rollback | ✅ |
| Cold → `NaN` before first value | ✅ |

## Common Pitfalls

1. **`exit` must not exceed `enter`.** The constructor throws otherwise; this implementation only supports the upward-latch polarity.
2. **A dead band delays confirmation, by design.** A value that reaches `enter` and immediately retreats into the dead band stays latched true — that persistence is the point, not a bug.
3. **Not the same as `Above` with two thresholds ORed together.** `Above(x, exit)` alone would flip back to false the instant `x` dips below `exit` even briefly above `enter` moments earlier without ever reaching `enter` again; `Hysteresis` requires reaching `enter` first.

## Usage Examples

```csharp
// Overbought with a dead band: latch at 70, release at 65
var rsi = new Rsi(14);
var overbought = new Hysteresis(rsi, enter: 70.0, exit: 65.0);

// Plain threshold (no dead band)
var plain = new Hysteresis(rsi, enter: 50.0, exit: 50.0);
```

## References

- Schmitt, O. H. (1938). "A Thermionic Trigger." *Journal of Scientific Instruments*, 15(1), 24–26.
