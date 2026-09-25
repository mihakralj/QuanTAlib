# OUTSIDECHANNEL: Channel Exclusion Predicate

> *A breakout, almost by definition, is a value stepping outside a channel it had been living inside.*

| Property         | Value                                     |
| ---------------- | ------------------------------------------ |
| **Category**     | Signals (Predicates)                       |
| **Inputs**       | Value, Lower, Upper (triple series, or Value and fixed scalar bounds) |
| **Parameters**   | None                                        |
| **Outputs**      | Single crisp series (OutsideChannel)        |
| **Output range** | `{0.0, 1.0}` while hot; `NaN` while cold     |
| **Warmup**       | `0` bars (hot after the first tick)         |
| **PineScript**   | `value > upper or value < lower`            |

- OUTSIDECHANNEL tests whether a value is above the upper bound or below the lower bound of a channel.
- **Similar:** [InsideChannel](../insidechannel/InsideChannel.md), [ChannelPosition](../channelposition/ChannelPosition.md) | **Trading note:** The direct building block for breakout detection against any channel indicator.
- Part of the strategy-primitives signal algebra.

## Mathematical Foundation

$$
\text{OutsideChannel}_t = \begin{cases} 1.0 & \text{if } \text{value}_t > \text{upper}_t \text{ or } \text{value}_t < \text{lower}_t \\ 0.0 & \text{otherwise} \end{cases}
$$

The exact complement of [InsideChannel](../insidechannel/InsideChannel.md): both use strict inequalities here and inclusive bounds there, so a value sitting exactly on either bound is always inside, never outside, and the two predicates partition every possible input with no overlap and no gap.

## Implementation Details

Identical construction to [InsideChannel](../insidechannel/InsideChannel.md) — built on `MultiInputBase` with 3 inputs (value, lower, upper), same time-join rule, same fixed-scalar-bounds convenience overload for a fixed breakout zone. See InsideChannel.md for the shared implementation rationale.

## Performance Profile

| Operation | Count | Notes |
| :--- | :---: | :--- |
| Comparisons | 2 | `value > upper`, `value < lower` |
| Sanitize (NaN check) | 3 | One per input |
| **Total** | **~5** | O(1) constant time |

## Validation

| Test | Status |
| :--- | :---: |
| Above upper, below lower, and inside bounds | ✅ |
| Boundary values report inside (not outside) | ✅ |
| Exact complement of `InsideChannel` across a range of samples | ✅ |
| Time-join across three independent publishers | ✅ |
| Fixed scalar bounds | ✅ |

## Common Pitfalls

1. **Boundary values are inside, not outside.** `OutsideChannel` never fires exactly on a bound; a strategy expecting "touch the band = breakout" should confirm whether it wants this strict definition or `Not(InsideChannel(...))` with a slightly different epsilon-adjusted bound.
2. **Fires on gaps and spikes, not just sustained breaks.** A single-bar wick beyond the channel triggers this predicate the same as a sustained close beyond it; combine with a confirmation trigger (once `signals/triggers/` exists) to require persistence.

## Usage Examples

```csharp
// Breakout beyond Donchian Channels
var dc = new Dc(20);
var breakout = new OutsideChannel(price, dc.Lower, dc.Upper);

// Fixed breakout zone
var rsi = new Rsi(14);
var extreme = new OutsideChannel(rsi, 20.0, 80.0);
```

## References

- Murphy, J. (1999). *Technical Analysis of the Financial Markets*. New York Institute of Finance.
