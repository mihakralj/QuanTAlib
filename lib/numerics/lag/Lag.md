# LAG: Value Delay

> *Every crossover, every momentum calculation, every "compared to N bars ago" condition needs one primitive underneath it: the value as it was, some bars in the past.*

| Property         | Value                                     |
| ---------------- | ------------------------------------------ |
| **Category**     | Numeric                                    |
| **Inputs**       | Source (close)                              |
| **Parameters**   | `n` (delay in bars, default 1)              |
| **Outputs**      | Single series (Lag)                         |
| **Output range** | Same domain as the input; `0.0` during warmup |
| **Warmup**       | `n + 1` bars                                |
| **PineScript**   | `x[n]`                                      |

- LAG republishes the input value observed n bars ago: result = x[t-n].
- **Similar:** [Mom](../../momentum/mom/Mom.md), [Change](../change/Change.md) | **Trading note:** The delay primitive behind "price above its value N bars ago" conditions; composes with [Sub](../sub/Sub.md) to reconstruct momentum.
- Validated against a direct reference implementation of Pine Script's `x[n]` history-referencing operator.

LAG fills the one gap in the series algebra that `numerics/` did not otherwise cover: every arithmetic node (`Add`, `Sub`, `Mul`, `Div`) combines two *current* values, but conditions like `close > close[5]` need a *delayed* value first. `Lag(0)` is the identity transform; `Sub(x, Lag(x, n))` is algebraically identical to [Mom](../../momentum/mom/Mom.md)`(x, n)` outside the warmup region — LAG is the more general primitive, and MOM is one specific composition of it.

## Mathematical Foundation

$$
\text{Lag}_t(n) = \begin{cases} x_{t-n} & \text{if at least } n+1 \text{ bars have been observed} \\ 0.0 & \text{otherwise (warmup)} \end{cases}
$$

### Edge Cases

- **NaN/Infinity input**: substituted with the last valid value *before* entering the delay window, so a single bad tick does not create a permanent hole n bars later.
- **`n = 0`**: identity — the node republishes the current value with no delay.
- **Warmup**: publishes `0.0` for the first `n` bars, consistent with `momentum/Mom`'s convention (never NaN).

## Implementation Details

### Ring Buffer of Depth n+1

LAG stores the sanitized input in a `RingBuffer(n + 1)`. After each `Add`, the oldest element in the buffer (`_buffer[0]`) is exactly the value from n bars before the current one, so retrieval is a single indexed read — no scanning, no recomputation.

### Bar Correction (`isNew = false`)

Because the ring buffer's `Add(value, isNew)` overload already implements the snapshot/restore semantics used throughout the library, correcting the current forming bar does not disturb the delayed value: only the newest slot is replaced, and the oldest slot (the one LAG reads from) is untouched until the bar is confirmed and a new one begins.

## Performance Profile

### Operation Count (Per Bar)

| Operation | Count | Notes |
| :--- | :---: | :--- |
| Sanitize (NaN check) | 1 | On the input |
| Buffer add | 1 | O(1) ring buffer write |
| Buffer read | 1 | O(1) indexed read of the oldest slot |
| **Total** | **~3** | O(1) constant time |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact delayed value |
| **Timeliness** | N/A | By definition n bars behind the source |
| **Memory** | 9/10 | O(n) for the ring buffer |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **Reference (`x[t-n]`)** | ✅ | Batch and streaming match a direct array-index reference |
| **Composition** | ✅ | `Sub(x, Lag(x, n))` matches `Mom(x, n)` outside warmup |

## Common Pitfalls

1. **Warmup returns 0.0, not the current value**: the first `n` bars publish `0.0`, matching `Mom`'s convention. Do not assume `Lag(n)` behaves like `Lag(0)` during warmup.
2. **`n` is fixed at construction**: there is no way to change the delay after construction; build a new `Lag` node if a different depth is needed.
3. **NaN substitution happens before the delay, not after**: a bad tick is replaced by the last valid value immediately, so it is that substituted value — not NaN — that eventually surfaces n bars later.
4. **Off-by-one with warmup length**: `WarmupPeriod` is `n + 1`, not `n`, because the node needs to have *seen* n+1 bars (the current one plus n prior ones) before its oldest slot holds a real delayed value.

## Usage Examples

```csharp
// "Price above its value 5 bars ago"
var lag5 = new Lag(5);
// combine with Above/CrossOver once available in signals/

// Reconstructing momentum by composition
var lagged = Lag.Batch(series, 10);
var momentum = Sub.Batch(series, lagged); // equals Mom.Batch(series, 10) outside warmup

// Identity pass-through
var identity = new Lag(0);
```

## References

- Murphy, J. (1999). *Technical Analysis of the Financial Markets*. New York Institute of Finance.
