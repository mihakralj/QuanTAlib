# CHANNELPOSITION: Normalized Channel Position

> *Not just "is the price inside the band" but "where inside the band" — the %B indicator's underlying idea, generalized to any channel.*

| Property         | Value                                     |
| ---------------- | ------------------------------------------ |
| **Category**     | Signals (Predicates)                       |
| **Inputs**       | Value, Lower, Upper (triple series, or Value and fixed scalar bounds) |
| **Parameters**   | `policy` (`OutOfRangePolicy.Cold` default, or `Saturate`) |
| **Outputs**      | Single graded series (ChannelPosition)      |
| **Output range** | `[0.0, 1.0]` while hot; `NaN` while cold or invalid |
| **Warmup**       | `0` bars (hot after the first valid tick)   |
| **PineScript**   | `(value - lower) / (upper - lower)` (Bollinger %B, generalized) |

- CHANNELPOSITION returns the value's normalized position within a pair of channel bounds: 0.0 at the lower bound, 1.0 at the upper bound.
- **Similar:** [InsideChannel](../insidechannel/InsideChannel.md), [OutsideChannel](../outsidechannel/OutsideChannel.md) | **Trading note:** The general form of Bollinger %B, usable with any channel indicator (Keltner, Donchian, STARC, ...).
- Part of the strategy-primitives signal algebra (`lib/signals/StrategyPrimitives.Spec.md`, Section 9.1).

## Mathematical Foundation

$$
\text{ChannelPosition}_t = \frac{\text{value}_t - \text{lower}_t}{\text{upper}_t - \text{lower}_t}
$$

### Two Distinct Failure Modes

This predicate has two ways to fail to produce a valid `[0, 1]` result, handled differently on purpose:

1. **Zero-width channel** (`|upper - lower| <= DefaultEpsilon`): a division-by-zero guard, not a policy choice. Always publishes `NaN` and reports not hot for that bar, regardless of the constructor's `policy` argument.
2. **Value outside the channel**: the raw ratio falls outside `[0, 1]`. This is governed by the `OutOfRangePolicy` constructor parameter:
   - **`Cold`** (default): publishes `NaN` and reports not hot for that bar.
   - **`Saturate`**: clamps the ratio into `[0, 1]` — an explicit, documented choice the caller opts into.

### `IsHot` Is Not a One-Way Latch Here

Unlike almost every other node in this library, `ChannelPosition.IsHot` can become `false` again on a specific bar without calling `Reset()`. It combines "has a full set of three inputs ever arrived" (inherited from `MultiInputBase`) with "was the *most recent* computation valid" (this class's own `_lastValid` flag, recomputed fresh on every call). A bar that hits either failure mode above reports `IsHot = false`; the very next valid bar reports `IsHot = true` again.

## Implementation Details

Built on `MultiInputBase` with 3 inputs (value, lower, upper), the same base as [InsideChannel](../insidechannel/InsideChannel.md) and [OutsideChannel](../outsidechannel/OutsideChannel.md). The `_lastValid` flag needs no explicit rollback bookkeeping (no `_p_lastValid` snapshot): it is a pure function of the current call's sanitized inputs, recomputed unconditionally inside `Compute` on every `Update` call, whether `isNew` is `true` or `false`.

## Performance Profile

| Operation | Count | Notes |
| :--- | :---: | :--- |
| Subtraction | 2 | `value - lower`, `upper - lower` |
| Division | 1 | Guarded by the zero-width check |
| Comparison | up to 2 | Range check against `[0, 1]` |
| Sanitize (NaN check) | 3 | One per input |
| **Total** | **~8** | O(1) constant time |

## Validation

| Test | Status |
| :--- | :---: |
| At lower bound → 0.0, at upper bound → 1.0, midpoint → 0.5 | ✅ |
| Zero-width channel → NaN, regardless of policy | ✅ |
| Out-of-range value, Cold policy → NaN | ✅ |
| Out-of-range value, Saturate policy → clamped | ✅ |
| `IsHot` recovers on the next valid bar after a Cold-policy failure | ✅ |
| Fixed scalar bounds | ✅ |

## Common Pitfalls

1. **Zero-width is always cold, never saturated.** `Saturate` only affects out-of-range *values*; it does not give a division-by-zero a defined result, since there is no sensible position to report when the channel itself has collapsed.
2. **`IsHot` is per-bar here, not a permanent latch.** Code that assumes "once hot, always hot until `Reset()`" (true for almost every other indicator in this library) will be surprised by `ChannelPosition` under the default `Cold` policy.
3. **Default policy silently drops out-of-range bars.** If a strategy needs every bar to report *some* number even when the price is outside its bands, construct with `OutOfRangePolicy.Saturate` explicitly rather than relying on the default.

## Usage Examples

```csharp
// Bollinger %B, generalized
var bb = new Bbands(price, 20, 2.0);
var percentB = new ChannelPosition(price, bb.Lower, bb.Upper);

// Saturating variant: always produces a [0, 1] number
var clamped = new ChannelPosition(price, bb.Lower, bb.Upper, OutOfRangePolicy.Saturate);

// Fixed scalar bounds
var rsiPosition = new ChannelPosition(rsi, 0.0, 100.0);
```

## References

- Bollinger, J. (2001). *Bollinger on Bollinger Bands*. McGraw-Hill.
