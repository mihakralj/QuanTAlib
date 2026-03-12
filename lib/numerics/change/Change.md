# CHANGE: Relative Price Change

> *The simplest measure of movement is often the most powerful.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Numeric                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period` (default 1)                      |
| **Outputs**      | Single series (Change)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period + 1` bars                          |
| **PineScript**   | [change.pine](change.pine)                       |

- CHANGE calculates the percentage change between the current value and a value N periods ago.
- Parameterized by `period` (default 1).
- Output range: Varies (see docs).
- Requires `period + 1` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

CHANGE calculates the percentage change between the current value and a value N periods ago. This fundamental indicator forms the basis for momentum analysis, rate of change calculations, and relative performance comparisons.

## Mathematical Foundation

The change calculation is straightforward:

$$
\text{Change}_t = \frac{P_t - P_{t-n}}{P_{t-n}}
$$

where:
- $P_t$ = current price
- $P_{t-n}$ = price N periods ago
- Result is expressed as a decimal (multiply by 100 for percentage)

### Edge Cases

- **Division by zero**: When $P_{t-n} = 0$, returns 0
- **NaN/Infinity inputs**: Uses last valid value substitution

## Performance Profile

### Operation Count (Per Bar)

| Operation | Count | Notes |
| :--- | :---: | :--- |
| Subtraction | 1 | Current - Past |
| Division | 1 | Conditional on past ≠ 0 |
| Buffer access | 1 | Ring buffer lookup |
| **Total** | **~3** | O(1) constant time |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact mathematical calculation |
| **Timeliness** | 10/10 | No lag beyond lookback period |
| **Smoothness** | 3/10 | Raw returns are noisy |
| **Memory** | 9/10 | Only stores period+1 values |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | ✅ | ROC function (divide by 100) |
| **Skender** | ✅ | Roc indicator |
| **Manual** | ✅ | Direct calculation verified |

## Common Pitfalls

1. **Percentage vs Decimal**: QuanTAlib returns decimal (0.1 = 10%), while TA-Lib ROC returns percentage (10.0 = 10%). Multiply by 100 when comparing.

2. **Warmup Period**: Requires `period + 1` bars before producing meaningful results. First `period` values return 0.

3. **Zero Division**: When the past value is zero, returns 0 rather than NaN/Infinity.

4. **Compounding**: For multi-period returns, geometric compounding may be more appropriate than simple arithmetic change.

## Usage Examples

```csharp
// Period-1 change (simple return)
var change = new Change(1);

// 10-period momentum
var momentum = new Change(10);

// Chained from another indicator
var smaChange = new Change(new Sma(20), 5);
```

## References

- Murphy, J. (1999). "Technical Analysis of the Financial Markets." New York Institute of Finance.
- Pring, M. (2002). "Technical Analysis Explained." McGraw-Hill.
