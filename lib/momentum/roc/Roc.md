# ROC: Rate of Change Percentage

> *The percentage form of momentum: by what percent has price changed? The most intuitive momentum measure.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Momentum                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period` (default 9)                      |
| **Outputs**      | Single series (Roc)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period + 1` bars                          |
| **PineScript**   | [roc.pine](roc.pine)                       |

- ROC (Rate of Change Percentage) calculates the percentage change between the current value and the value N periods ago.
- **Similar:** [MOM](../mom/Mom.md), [ROCP](../rocp/Rocp.md), [ROCR](../rocr/Rocr.md) | **Complementary:** Volume ROC | **Trading note:** Rate of Change, percentage form (5.0 = 5%); matches TA-Lib's ROC.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

ROC (Rate of Change Percentage) calculates the percentage change between the current value and the value N periods ago. This is the most commonly used form of rate of change, expressing change in percentage terms that are directly interpretable (e.g., 5.0 = 5% increase).

## Historical Context

ROC is the standard way of expressing price momentum in percentage terms. It's widely used in technical analysis because percentage changes are comparable across different instruments regardless of their price levels.

The terminology varies by platform:
- **TA-Lib**: `ROC` returns percentage (5.0 = 5%); `ROCP` returns the decimal fraction (0.05 = 5%)
- **TradingView/PineScript**: Often uses `change` for the absolute (non-percentage) form
- **QuanTAlib**: `Roc` returns percentage (5.0 = 5%, matches TA-Lib `ROC`); `Rocp` returns the decimal fraction (0.05 = 5%, matches TA-Lib `ROCP`); `Mom` returns the absolute change

## Architecture & Physics

### 1. Ring Buffer Storage

The indicator maintains a sliding window of `period + 1` values:

$$
\text{buffer} = [v_{t-n}, v_{t-n+1}, ..., v_{t-1}, v_t]
$$

where $n$ is the lookback period.

### 2. Percentage Calculation

$$
\text{ROC}_t = 100 \times \frac{v_t - v_{t-n}}{v_{t-n}}
$$

where:
- $v_t$ = current value
- $v_{t-n}$ = value from $n$ periods ago
- Result is in percentage units (5.0 = 5%)

## Mathematical Foundation

### Core Formula

$$
\text{ROC}_t = 100 \times \frac{P_t - P_{t-n}}{P_{t-n}}
$$

### Relationship to Other Rate of Change Variants

| Indicator | Formula | Output |
|-----------|---------|--------|
| **MOM** | $P_t - P_{t-n}$ | Absolute (price units) |
| **ROC** | $\frac{P_t - P_{t-n}}{P_{t-n}} \times 100$ | Percentage (%) |
| **ROCR** | $\frac{P_t}{P_{t-n}}$ | Ratio (dimensionless) |
| **ROCP** | $\frac{P_t - P_{t-n}}{P_{t-n}}$ | Decimal (0.10 = 10%) |

### Conversions

$$
\text{ROC} = \text{ROCP} \times 100
$$

$$
\text{ROC} = (\text{ROCR} - 1) \times 100
$$

$$
\text{CHANGE} = \text{ROC} / 100
$$

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Notes |
| :--- | :---: | :--- |
| SUB | 1 | current - past |
| DIV | 1 | change / past |
| MUL | 1 | × 100 |
| Buffer add | 1 | O(1) ring buffer |
| **Total** | **~4 ops** | Very lightweight |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact arithmetic |
| **Timeliness** | 10/10 | Zero lag |
| **Smoothness** | 3/10 | Reflects raw volatility |
| **Simplicity** | 10/10 | Basic arithmetic |

## Interpretation

* **ROC = 0.0**: No change from N periods ago
* **ROC > 0**: Price increased (e.g., 5.0 = 5% increase)
* **ROC < 0**: Price decreased (e.g., -3.0 = 3% decrease)
* **ROC = 100**: Price doubled
* **ROC = -50**: Price halved

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | ✅ | Matches TA-Lib's `ROC` directly (both return percentage, e.g. 5.0 = 5%) |
| **TradingView** | ✅ | Matches PineScript calculation |

## Common Pitfalls

1. **Scale**: ROC returns percentage values directly. A return of 5.0 means 5%, not 0.05.

2. **Division by zero**: If the historical price is zero, ROC returns 0.0 as a safe default.

3. **Don't confuse with ROCP**: QuanTAlib's `Rocp` (a separate indicator) returns the decimal fraction (0.05 for 5%), matching TA-Lib's `ROCP`. This `Roc` indicator matches TA-Lib's `ROC` (percentage).

4. **Compounding**: Unlike ROCR, ROC values cannot be directly multiplied for multi-period changes.

5. **Warmup period**: The first `period` values return 0.0.

## References

- Pring, M. J. (2014). "Technical Analysis Explained." McGraw-Hill.
- Murphy, J. J. (1999). "Technical Analysis of the Financial Markets."
- TA-Lib Documentation: ROC function