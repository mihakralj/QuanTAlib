# ISRISING: Rising Predicate

> *Pine Script's `ta.rising` asks a deceptively simple question — has this value beaten everything it's seen in the last n bars — and the "excludes the current bar" detail is the whole implementation.*

| Property         | Value                                     |
| ---------------- | ------------------------------------------ |
| **Category**     | Signals (Predicates)                       |
| **Inputs**       | Source (single series)                      |
| **Parameters**   | `n` (lookback, must be `>= 1`)              |
| **Outputs**      | Single crisp series (IsRising)              |
| **Output range** | `{0.0, 1.0}` while hot; `NaN` during warmup  |
| **Warmup**       | `n + 1` bars                                |
| **PineScript**   | `ta.rising(source, n)`                      |

- ISRISING tests whether the current value is strictly greater than the maximum of the n bars preceding it.
- **Similar:** [AtHighest](../athighest/AtHighest.md), [IsFalling](../isfalling/IsFalling.md) | **Trading note:** The breakout-of-recent-range test; `BecameTrue(IsRising(n))` on a source is the rising edge equivalent of a Breakout trigger.
- Part of the strategy-primitives signal algebra. The exact definition here — `current > max(previous n)`, current bar excluded, comparison strict — was confirmed against the "most common understanding" during the design of this predicate, superseding an earlier draft that read Pine's `ta.rising` as adjacent-pair monotonicity.

## Mathematical Foundation

$$
\text{IsRising}_t(n) = \begin{cases} 1.0 & \text{if } x_t > \max(x_{t-1}, \ldots, x_{t-n}) \\ 0.0 & \text{otherwise} \end{cases}
$$

The window is the **previous** n bars — the current bar is never compared against itself. A tie (`x_t` equal to the prior maximum) is `0.0`: the comparison is strict. Contrast with [AtHighest](../athighest/AtHighest.md), whose window *includes* the current bar and asks a different question ("is the current bar the maximum"), which by definition is always at least a tie for the maximum, never a strict-greater-than test against itself.

### Warmup

`NaN` until `n` confirmed prior bars exist — `n + 1` total bars observed (the first bar can never have `n` predecessors).

## Implementation Details

### Confirm-Then-Push: Why No Rebuild Is Needed on Correction

The window (backed by a `MonotonicDeque`, same data structure as `numerics/Highest`) holds only **confirmed prior bars**, never the currently forming one. A tick with `isNew = true` first checks whether the *previous* forming bar's now-settled value should join the window (it does, if one exists), pushing it in with `MonotonicDeque.PushMax` — then only after that does the new tick become the new pending value. A tick with `isNew = false` (a correction) only ever overwrites the pending value; it never touches the window, since the window was never told about the currently-forming bar in the first place.

This is the same confirm-then-push pattern as [`ConfirmOnClose`](../../../core/ConfirmOnClose.md), applied to a rolling window instead of a single value, and it means `IsRising` needs no `RebuildMax`-on-correction logic at all — unlike [AtHighest](../athighest/AtHighest.md), whose inclusive window does need to rebuild, because a correction there changes a value that is already inside the window.

## Performance Profile

| Operation | Count | Notes |
| :--- | :---: | :--- |
| Deque push (amortized) | ≤1 | Only when a bar closes |
| Deque query | 1 | `GetExtremum` |
| Comparison | 1 | `current > extremum` |
| **Total** | **O(1) amortized** | Same complexity class as `numerics/Highest` |

## Validation

| Test | Status |
| :--- | :---: |
| Warmup returns NaN for the first n bars | ✅ |
| n=1: strictly greater than the immediately preceding bar | ✅ |
| Tie returns false (strict comparison) | ✅ |
| Current bar is excluded from its own comparison | ✅ |
| Correction to the forming bar does not disturb the confirmed window | ✅ |
| NaN/Infinity substitution | ✅ |
| Batch/streaming parity | ✅ |

## Common Pitfalls

1. **The current bar is never part of the comparison.** `IsRising(n)` compares against the *previous* n bars only; a value tying its own immediately preceding value is not "rising," and a run of n identical values followed by a higher one is rising on that last bar, not sooner.
2. **Do not confuse with `AtHighest`.** `IsRising(n)` and `AtHighest(n)` ask genuinely different questions (exclusive vs. inclusive window) and use different `MonotonicDeque` correction strategies internally; they are not interchangeable even though both use "n" and a rolling maximum.
3. **Warmup is `n + 1`, not `n`.** The first bar can never have n predecessors, so the node needs to observe n+1 bars total before its first non-NaN output.

## Usage Examples

```csharp
// "Higher high than the last 5 confirmed bars"
var rising5 = new IsRising(5);

// Chained from another indicator
var trendAccelerating = new IsRising(new Roc(price, 10), 3);
```

## References

- TradingView Pine Script Reference: `ta.rising`.
