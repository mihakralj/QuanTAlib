# ATHIGHEST: At-Maximum Predicate

> *Not "is this a new high compared to history" but the more immediate question a breakout trader asks every single bar: is right now the highest point of the last n bars, including right now?*

| Property         | Value                                     |
| ---------------- | ------------------------------------------ |
| **Category**     | Signals (Predicates)                       |
| **Inputs**       | Source (single series)                      |
| **Parameters**   | `n` (window size, current bar included, must be `>= 1`) |
| **Outputs**      | Single crisp series (AtHighest)             |
| **Output range** | `{0.0, 1.0}` while hot; `NaN` during warmup  |
| **Warmup**       | `n` bars                                    |
| **PineScript**   | `high == ta.highest(high, n)`               |

- ATHIGHEST tests whether the current value is the maximum of the trailing n-bar window that includes it.
- **Similar:** [IsRising](../isrising/IsRising.md), [AtLowest](../atlowest/AtLowest.md) | **Trading note:** Fires on the exact bar a new n-bar high is set; a natural entry trigger for pure breakout systems.
- Part of the strategy-primitives signal algebra (`lib/signals/StrategyPrimitives.Spec.md`, Section 9.1: "Reuses `MonotonicDeque`").

## Mathematical Foundation

$$
\text{AtHighest}_t(n) = \begin{cases} 1.0 & \text{if } x_t = \max(x_{t-n+1}, \ldots, x_t) \\ 0.0 & \text{otherwise} \end{cases}
$$

The window is **inclusive** of the current bar — the defining difference from [IsRising](../isrising/IsRising.md), whose window explicitly excludes it. Since the current bar is always trivially `<=` its own inclusive-window maximum, `AtHighest` is really asking "is the current bar the (most recent) one achieving that maximum," not a strict-greater-than test.

### Tie-Breaking

Implemented by exact index comparison — "is the deque's front the current bar's own logical index" — not floating-point equality. This sidesteps epsilon/tie ambiguity entirely. On an exact value tie between the current bar and an earlier one in the window, the **current** (most recent) bar wins: `MonotonicDeque.PushMax` evicts equal-or-smaller values from the back of the deque before inserting, so the newest occurrence of the maximum always ends up at the front.

## Implementation Details

### Inclusive Window Requires Rebuild-on-Correction

Because the window includes the currently forming bar, a correction (`isNew = false`) changes a value that is already inside the window — unlike [IsRising](../isrising/IsRising.md)'s exclusive window, which never contains the forming bar. `AtHighest` follows the same pattern as `channels/Dc` (Donchian Channels): the raw buffer slot is overwritten and `MonotonicDeque.RebuildMax` reconstructs the deque from scratch on every correction. This is O(n) per correction rather than the O(1) amortized cost of a normal push, which is an acceptable trade-off since corrections are expected to be rare relative to new-bar ticks.

## Performance Profile

| Operation | Count | Notes |
| :--- | :---: | :--- |
| Deque push (amortized, new bar) | 1 | O(1) amortized |
| Deque rebuild (correction) | O(n) | Only on `isNew = false` |
| Index comparison | 1 | `FrontIndex == current index` |

## Validation

| Test | Status |
| :--- | :---: |
| Current bar is the window max → true | ✅ |
| Current bar is not the window max → false | ✅ |
| Exact tie: current (most recent) bar wins | ✅ |
| Correction rebuilds the window correctly | ✅ |
| `n = 1` is trivially always true | ✅ |
| Warmup returns NaN for the first n-1 bars | ✅ |
| Batch/streaming parity | ✅ |

## Common Pitfalls

1. **`n = 1` is trivially always true.** With a window of one bar, the current bar is by definition its own (only) maximum every time; this is a degenerate but valid configuration, not a bug.
2. **Not the same as `IsRising`.** `AtHighest(n)` and `IsRising(n)` use different window inclusivity and different correction strategies; do not substitute one for the other even though both track "n" and a rolling maximum.
3. **Corrections are O(n), not O(1).** A host that corrects the same bar many times before confirming it will pay the rebuild cost each time; this is fine for typical live-tick usage but worth knowing for extreme replay scenarios.

## Usage Examples

```csharp
// Pure breakout entry: fires exactly on the bar setting a new 20-bar high
var newHigh = new AtHighest(20);

// Chained from another indicator
var oscillatorPeak = new AtHighest(new Rsi(14), 10);
```

## References

- Tarjan, R. E. (1985). "Amortized Computational Complexity." *SIAM Journal on Algebraic Discrete Methods*.
