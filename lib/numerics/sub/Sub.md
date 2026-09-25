# SUB: Element-wise Subtraction

> *A fast line minus a slow line is a spread; a spread crossing zero is a signal.*

| Property         | Value                                     |
| ---------------- | ------------------------------------------ |
| **Category**     | Numeric                                    |
| **Inputs**       | A, B (dual series)                          |
| **Parameters**   | None                                        |
| **Outputs**      | Single series (Sub)                         |
| **Output range** | $(-\infty, +\infty)$                        |
| **Warmup**       | `1` bar                                     |
| **PineScript**   | `a - b`                                     |

- SUB computes the element-wise difference of two time-aligned streams: result = a - b.
- **Similar:** [Add](../add/Add.md), [Mul](../mul/Mul.md), [Div](../div/Div.md) | **Trading note:** The textbook building block for spreads and MA gaps, e.g. `fast - slow` feeding a zero-line crossover.
- Validated against TA-Lib's `Sub` function.

SUB is the difference counterpart to [Add](../add/Add.md). Its most common use is building a spread series (`fast - slow`) that a `CrossOver(spread, 0)`-style trigger can consume directly, or reconstructing momentum by composing with [Lag](../lag/Lag.md): `Sub(x, Lag(x, n))` is algebraically identical to [Mom](../../momentum/mom/Mom.md)`(x, n)` outside the warmup region.

## Mathematical Foundation

$$
\text{Sub}_t = a_t - b_t
$$

### Edge Cases

- **NaN/Infinity in either input**: substituted with that input's last valid value before the difference is computed.
- **Both inputs cold**: not hot until the first bar has been processed.

## Implementation Details

Built on `BiInputIndicatorBase` with `period = 1`, the same reuse pattern as [Add](../add/Add.md): the windowed Kahan-compensated mean degenerates to the current bar's difference, so rollback and NaN-substitution come from the shared base with no extra state. Event-chained construction (`new Sub(a, b)`) applies the same time-join rule as `Add`: a result is only published once both sources have reported for the same bar `Time`.

## Performance Profile

### Operation Count (Per Bar)

| Operation | Count | Notes |
| :--- | :---: | :--- |
| Subtraction | 1 | a − b |
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
| **TA-Lib** | ✅ | `Sub` function, batch and streaming parity |
| **Manual** | ✅ | `Sub(x, Lag(x, n))` cross-checked against `Mom(x, n)` |

## Common Pitfalls

1. **Operand order matters**: `Sub(a, b)` is `a - b`, not `b - a`. Swapping arguments flips the sign of every downstream crossover.
2. **Not immediately hot**: `IsHot` is false until the first bar (see Add's pitfalls for the underlying reason).
3. **Time-join silence**: event-chained construction emits nothing until both sources report for the current bar.

## Usage Examples

```csharp
// Spread between two moving averages, feeding a crossover
var fast = new Ema(12);
var slow = new Ema(26);
var spread = new Sub(fast, slow);

// Momentum via composition (equivalent to Mom(x, 10))
var lagged = Lag.Batch(series, 10);
var momentum = Sub.Batch(series, lagged);
```

## References

- Murphy, J. (1999). *Technical Analysis of the Financial Markets*. New York Institute of Finance.
