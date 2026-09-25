# ATLOWEST: At-Minimum Predicate

> *The mirror image of AtHighest, and just as central to breakdown-entry systems.*

| Property         | Value                                     |
| ---------------- | ------------------------------------------ |
| **Category**     | Signals (Predicates)                       |
| **Inputs**       | Source (single series)                      |
| **Parameters**   | `n` (window size, current bar included, must be `>= 1`) |
| **Outputs**      | Single crisp series (AtLowest)              |
| **Output range** | `{0.0, 1.0}` while hot; `NaN` during warmup  |
| **Warmup**       | `n` bars                                    |
| **PineScript**   | `low == ta.lowest(low, n)`                  |

- ATLOWEST tests whether the current value is the minimum of the trailing n-bar window that includes it.
- **Similar:** [AtHighest](../athighest/AtHighest.md), [IsFalling](../isfalling/IsFalling.md) | **Trading note:** Fires on the exact bar a new n-bar low is set.
- Part of the strategy-primitives signal algebra.

AtLowest is the exact structural mirror of [AtHighest](../athighest/AtHighest.md): inclusive window, exact index-comparison tie-breaking (current bar wins ties), and the same rebuild-on-correction requirement using a min-tracking `MonotonicDeque`. See AtHighest.md for the full implementation rationale, which applies here unchanged.

## Mathematical Foundation

$$
\text{AtLowest}_t(n) = \begin{cases} 1.0 & \text{if } x_t = \min(x_{t-n+1}, \ldots, x_t) \\ 0.0 & \text{otherwise} \end{cases}
$$

## Validation

| Test | Status |
| :--- | :---: |
| Current bar is the window min → true | ✅ |
| Current bar is not the window min → false | ✅ |
| Exact tie: current (most recent) bar wins | ✅ |
| Correction rebuilds the window correctly | ✅ |
| Batch/streaming parity | ✅ |

## Common Pitfalls

Same as [AtHighest](../athighest/AtHighest.md): `n = 1` is trivially always true, this is distinct from `IsFalling`'s exclusive-window question, and corrections cost O(n).

## Usage Examples

```csharp
// Pure breakdown entry: fires exactly on the bar setting a new 20-bar low
var newLow = new AtLowest(20);
```

## References

- Tarjan, R. E. (1985). "Amortized Computational Complexity." *SIAM Journal on Algebraic Discrete Methods*.
