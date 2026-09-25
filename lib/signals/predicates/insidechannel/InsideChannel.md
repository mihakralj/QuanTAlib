# INSIDECHANNEL: Channel Membership Predicate

> *Bollinger Bands, Keltner Channels, a fixed oscillator zone — every channel indicator eventually gets asked the same question: is the price inside it?*

| Property         | Value                                     |
| ---------------- | ------------------------------------------ |
| **Category**     | Signals (Predicates)                       |
| **Inputs**       | Value, Lower, Upper (triple series, or Value and fixed scalar bounds) |
| **Parameters**   | None                                        |
| **Outputs**      | Single crisp series (InsideChannel)         |
| **Output range** | `{0.0, 1.0}` while hot; `NaN` while cold     |
| **Warmup**       | `0` bars (hot after the first tick)         |
| **PineScript**   | `lower <= value and value <= upper`         |

- INSIDECHANNEL tests whether a value is inside (inclusive) a pair of channel bounds.
- **Similar:** [OutsideChannel](../outsidechannel/OutsideChannel.md), [ChannelPosition](../channelposition/ChannelPosition.md) | **Trading note:** Works with the Upper/Lower outputs of any existing channel indicator (Bollinger, Keltner, Donchian, STARC, ...), or a fixed oscillator zone.
- Part of the strategy-primitives signal algebra (`lib/signals/StrategyPrimitives.Spec.md`, Section 9.1).

## Mathematical Foundation

$$
\text{InsideChannel}_t = \begin{cases} 1.0 & \text{if } \text{lower}_t \leq \text{value}_t \leq \text{upper}_t \\ 0.0 & \text{otherwise} \end{cases}
$$

Both bounds are inclusive: a value sitting exactly on either bound is inside. This is the exact complement of [OutsideChannel](../outsidechannel/OutsideChannel.md), which uses strict inequalities — the two predicates never both fire on the same bar.

### Edge Cases

- **NaN/Infinity in any input**: substituted with that input's last valid value.
- **`lower > upper`** (an inverted channel): every value is reported outside; this is not validated as an error, since a host may legitimately pass momentarily inverted bounds from an upstream indicator during its own warmup.

## Implementation Details

Built on `MultiInputBase` with 3 inputs (value, lower, upper) — the first concrete consumer of that base, added specifically for this predicate family. `MultiInputBase` generalizes `BiInputIndicatorBase`'s time-join and NaN-substitution pattern to a fixed input count (2 to 8), so a result is only computed once every one of the three publishers has reported for the same bar `Time`.

The fixed-scalar-bounds constructor (`new InsideChannel(value, 30.0, 70.0)`) does not route through the 3-publisher time-join at all: it subscribes only to `value.Pub` and pairs each tick with freshly constructed `TValue`s wrapping the two constants, calling the same 3-input `Update` overload directly. This covers the common case of a fixed oscillator zone without wiring two extra constant streams.

## Performance Profile

| Operation | Count | Notes |
| :--- | :---: | :--- |
| Comparisons | 2 | `value >= lower`, `value <= upper` |
| Sanitize (NaN check) | 3 | One per input |
| **Total** | **~5** | O(1) constant time |

## Validation

| Test | Status |
| :--- | :---: |
| Inside, outside, and both boundary values (inclusive) | ✅ |
| NaN substitution | ✅ |
| `isNew = false` rollback | ✅ |
| Time-join across three independent publishers | ✅ |
| Fixed scalar bounds | ✅ |
| Cold → `NaN`, hot after first full tick, `Reset()` returns to cold | ✅ |

## Common Pitfalls

1. **Inclusive bounds.** A value exactly on the lower or upper bound is inside, matching most channel-indicator conventions but worth confirming against the specific host platform being replicated.
2. **Three-way time-join.** All three streams (value, lower, upper) must publish for the same bar before a result appears; a channel indicator with its own separate warmup can delay this predicate's own first output.
3. **Not the same as `ChannelPosition` thresholded at [0, 1].** `InsideChannel` is a direct crisp test; `ChannelPosition` returns the graded normalized position and requires an extra comparison to become crisp.

## Usage Examples

```csharp
// Inside Bollinger Bands
var bb = new Bbands(price, 20, 2.0);
var insideBands = new InsideChannel(price, bb.Lower, bb.Upper);

// Fixed oscillator zone: RSI between 30 and 70
var rsi = new Rsi(14);
var neutral = new InsideChannel(rsi, 30.0, 70.0);
```

## References

- Bollinger, J. (2001). *Bollinger on Bollinger Bands*. McGraw-Hill.
