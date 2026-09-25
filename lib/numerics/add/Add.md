# ADD: Element-wise Addition

> *Two streams, one sum — the simplest way to combine a fast line and a slow line into a spread.*

| Property         | Value                                     |
| ---------------- | ------------------------------------------ |
| **Category**     | Numeric                                    |
| **Inputs**       | A, B (dual series)                          |
| **Parameters**   | None                                        |
| **Outputs**      | Single series (Add)                         |
| **Output range** | $(-\infty, +\infty)$                        |
| **Warmup**       | `1` bar                                     |
| **PineScript**   | `a + b`                                     |

- ADD computes the element-wise sum of two time-aligned streams: result = a + b.
- **Similar:** [Sub](../sub/Sub.md), [Mul](../mul/Mul.md), [Div](../div/Div.md) | **Trading note:** Combines two indicators or price series into a single sum, e.g. summing two volume proxies.
- Validated against TA-Lib's `Add` function.

ADD is the two-operand half of the series algebra that the single-operand [Lineartrans](../lineartrans/Lineartrans.md) already covers for `a·x + b`. It exists so predicate and trigger nodes (see the signal-primitives specification in `lib/signals/`) can express conditions such as spreads or combined proxies without host-side code.

## Mathematical Foundation

$$
\text{Add}_t = a_t + b_t
$$

### Edge Cases

- **NaN/Infinity in either input**: substituted with that input's last valid value before the sum is computed (never propagated).
- **Both inputs cold**: the node is not hot until the first bar has been processed (see Implementation Details).

## Implementation Details

### Reuse of `BiInputIndicatorBase`

ADD is built on `BiInputIndicatorBase` with a single-slot window (`period = 1`). At period 1 the base's windowed Kahan-compensated mean degenerates algebraically to the current bar's `ComputeError(a, b)` result, so the existing rollback (`isNew` correction) and NaN-substitution machinery apply with no additional state. This is why `IsHot` becomes true only after the first bar, rather than immediately as with a stateless single-input transform like `Lineartrans`.

### Time-Join for Event-Chained Construction

`new Add(sourceA, sourceB)` subscribes to two independent publishers. A result is only published once **both** sources have reported for the same bar `Time`; an input reporting a newer `Time` before the other side has caught up starts a new pending pair and drops the unmatched half, rather than mixing bar *t* of one input with bar *t-1* of the other.

## Performance Profile

### Operation Count (Per Bar)

| Operation | Count | Notes |
| :--- | :---: | :--- |
| Addition | 1 | a + b |
| Sanitize (NaN check) | 2 | One per input |
| **Total** | **~3** | O(1) constant time |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact arithmetic |
| **Timeliness** | 10/10 | Zero lag |
| **Memory** | 10/10 | O(1) |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | ✅ | `Add` function, batch and streaming parity |
| **Manual** | ✅ | Direct calculation verified |

## Common Pitfalls

1. **Not immediately hot**: unlike stateless transforms (e.g. `Lineartrans`), `IsHot` is false until the first bar is processed, because the underlying base class tracks a one-element window.
2. **Time-join silence**: when event-chained to two publishers, no value is emitted until both sources have reported for the current bar. A single-source update alone produces no output.
3. **Sentinel timestamp for raw doubles**: `Update(double a, double b, bool isNew)` stamps `DateTime.MinValue`; use the `TValue` overload for time-sensitive chaining.

## Usage Examples

```csharp
// Direct pull-style update
var add = new Add();
var result = add.Update(3.0, 4.0); // 7.0

// Event-chained: sum of two EMAs
var fast = new Ema(12);
var slow = new Ema(26);
var sum = new Add(fast, slow);
```

## References

- IEEE 754-2019. *IEEE Standard for Floating-Point Arithmetic*.
