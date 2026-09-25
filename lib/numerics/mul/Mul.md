# MUL: Element-wise Multiplication

> *Price times volume, or a value times a scaling factor — multiplication combines two streams into a single weighted quantity.*

| Property         | Value                                     |
| ---------------- | ------------------------------------------ |
| **Category**     | Numeric                                    |
| **Inputs**       | A, B (dual series)                          |
| **Parameters**   | None                                        |
| **Outputs**      | Single series (Mul)                         |
| **Output range** | $(-\infty, +\infty)$                        |
| **Warmup**       | `1` bar                                     |
| **PineScript**   | `a * b`                                     |

- MUL computes the element-wise product of two time-aligned streams: result = a * b.
- **Similar:** [Add](../add/Add.md), [Sub](../sub/Sub.md), [Div](../div/Div.md) | **Trading note:** Weighting one series by another, e.g. price × volume proxies, or applying a dynamic multiplier such as a regime-dependent scalar.
- Validated against TA-Lib's `Mult` function.

MUL is the product counterpart to [Add](../add/Add.md) and [Sub](../sub/Sub.md). A scalar multiplier (`x · k`) does not need this node — use [Lineartrans](../lineartrans/Lineartrans.md)`(slope: k)` instead, which avoids constructing a constant child stream. MUL is for combining two genuinely independent live series.

## Mathematical Foundation

$$
\text{Mul}_t = a_t \cdot b_t
$$

### Edge Cases

- **NaN/Infinity in either input**: substituted with that input's last valid value before the product is computed.
- **Both inputs cold**: not hot until the first bar has been processed.

## Implementation Details

Built on `BiInputIndicatorBase` with `period = 1`, following the same reuse pattern as [Add](../add/Add.md): the windowed Kahan-compensated mean degenerates to the current bar's product, so rollback and NaN-substitution come from the shared base with no extra state. Event-chained construction (`new Mul(a, b)`) applies the same time-join rule as `Add`: a result is only published once both sources have reported for the same bar `Time`.

## Performance Profile

### Operation Count (Per Bar)

| Operation | Count | Notes |
| :--- | :---: | :--- |
| Multiplication | 1 | a × b |
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
| **TA-Lib** | ✅ | `Mult` function, batch and streaming parity |
| **Manual** | ✅ | Direct calculation verified |

## Common Pitfalls

1. **Scalar multiplication should use `Lineartrans`**: `Mul(x, constant)` works but needlessly wires a second stream; prefer `Lineartrans(slope: constant)`.
2. **Not immediately hot**: `IsHot` is false until the first bar.
3. **Time-join silence**: event-chained construction emits nothing until both sources report for the current bar.
4. **Overflow with large magnitudes**: as with any floating-point product, very large operands can overflow to infinity; downstream nodes treat that as a non-finite input and substitute the last valid value.

## Usage Examples

```csharp
// Direct pull-style update
var mul = new Mul();
var result = mul.Update(6.0, 7.0); // 42.0

// Event-chained: price × a dynamic volume weight
var priceProxy = new Sma(1);
var volumeWeight = new Sma(20); // any live weighting stream
var weighted = new Mul(priceProxy, volumeWeight);
```

## References

- IEEE 754-2019. *IEEE Standard for Floating-Point Arithmetic*.
