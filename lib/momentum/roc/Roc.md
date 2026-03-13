# ROC: Rate of Change (Absolute)

> *The simplest momentum measure: how far has price moved? Not percentage, not ratio - just the raw difference.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Momentum                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period` (default 9)                      |
| **Outputs**      | Single series (Roc)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period + 1` bars                          |
| **PineScript**   | [roc.pine](roc.pine)                       |

- ROC (Rate of Change) calculates the absolute price difference between the current value and the value N periods ago.
- **Similar:** [ROCP](../rocp/Rocp.md), [MOM](../mom/Mom.md) | **Complementary:** Moving average for smoothing | **Trading note:** Rate of Change; percentage price change over n periods. Unbounded oscillator.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

ROC (Rate of Change) calculates the absolute price difference between the current value and the value N periods ago. This is the most basic form of momentum measurement, returning the raw price change in the same units as the input data. Unlike ROCP (percentage) or ROCR (ratio), ROC preserves the original scale, making it directly interpretable in dollar/point terms.

## Historical Context

ROC belongs to the earliest class of technical indicators, predating computerized analysis. Traders have always measured "how much has price moved" as a fundamental question. The absolute form (current - past) appears in technical analysis literature under various names: momentum, price change, and rate of change. The terminology varies by platform and library:

- **TA-Lib**: Uses `MOM` (Momentum) for absolute change
- **Tulip**: Uses `MOM` for absolute change
- **TradingView/PineScript**: Uses `ROC` for absolute change in this codebase
- **QuanTAlib**: Uses `ROC` for absolute change, `CHANGE` for percentage

This implementation follows the PineScript convention where ROC represents the absolute difference.

## Architecture & Physics

### 1. Ring Buffer Storage

The indicator maintains a sliding window of `period + 1` values:

$$
\text{buffer} = [v_{t-n}, v_{t-n+1}, ..., v_{t-1}, v_t]
$$

where $n$ is the lookback period. Only the oldest and newest values are needed for calculation.

### 2. Absolute Change Calculation

$$
\text{ROC}_t = v_t - v_{t-n}
$$

where:
- $v_t$ = current value
- $v_{t-n}$ = value from $n$ periods ago
- Result is in the same units as input (dollars, points, etc.)

### 3. State Management

The indicator uses state rollback for bar correction:

```
if isNew:
    save current state as previous
else:
    restore previous state
```

This enables real-time bar updates without corrupting historical calculations.

## Mathematical Foundation

### Core Formula

$$
\text{ROC}_t = P_t - P_{t-n}
$$

### Relationship to Other Rate of Change Variants

| Indicator | Formula | Output |
|-----------|---------|--------|
| **ROC** | $P_t - P_{t-n}$ | Absolute (price units) |
| **ROCP** | $\frac{P_t - P_{t-n}}{P_{t-n}} \times 100$ | Percentage (%) |
| **ROCR** | $\frac{P_t}{P_{t-n}}$ | Ratio (dimensionless) |
| **CHANGE** | $\frac{P_t - P_{t-n}}{P_{t-n}}$ | Decimal (0.10 = 10%) |

### Conversions

$$
\text{ROCP} = \text{CHANGE} \times 100
$$

$$
\text{ROCR} = \text{CHANGE} + 1 = \frac{P_t}{P_{t-n}}
$$

$$
\text{ROC} = \text{CHANGE} \times P_{t-n}
$$

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Notes |
| :--- | :---: | :--- |
| SUB | 1 | current - past |
| Buffer add | 1 | O(1) ring buffer |
| State copy | 1 | rollback support |
| **Total** | **~3 ops** | Extremely lightweight |

### Batch Mode (Span-based)

The span-based calculation is a simple loop with no dependencies between iterations, making it trivially parallelizable and cache-friendly.

| Operation | Complexity | Notes |
| :--- | :---: | :--- |
| Per-element | O(1) | Single subtraction |
| Total | O(n) | Linear scan |
| Memory | O(1) | No additional allocation |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact arithmetic, no approximation |
| **Timeliness** | 10/10 | Zero lag by definition |
| **Smoothness** | 3/10 | No smoothing, reflects raw volatility |
| **Simplicity** | 10/10 | Single subtraction |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | Uses MOM for this calculation |
| **Tulip** | ✅ | MOM matches exactly |
| **TradingView** | ✅ | Matches PineScript roc.pine |

## Common Pitfalls

1. **Unit confusion**: ROC returns absolute values in price units, not percentages. A ROC of 5 means the price moved 5 dollars/points, not 5%.

2. **Scale dependency**: ROC values are not comparable across instruments with different price levels. Use ROCP or CHANGE for normalized comparisons.

3. **Warmup period**: The first `period` values return 0 as there's no historical reference point.

4. **Zero handling**: Unlike percentage-based variants, ROC has no division-by-zero risk.

5. **Sign interpretation**: Positive ROC indicates price increase, negative indicates decrease.

6. **API confusion**: QuanTAlib's `CHANGE` indicator returns percentage (decimal), while `ROC` returns absolute change. This differs from some platforms where ROC means percentage.

## References

- Pring, M. J. (2014). "Technical Analysis Explained." McGraw-Hill.
- Murphy, J. J. (1999). "Technical Analysis of the Financial Markets." New York Institute of Finance.
- TradingView PineScript Reference: ta.roc, ta.mom