# ROCR: Rate of Change Ratio

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Momentum                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period` (default 9)                      |
| **Outputs**      | Single series (Rocr)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period + 1` bars                          |

### TL;DR

- ROCR (Rate of Change Ratio) calculates the ratio between the current value and the value N periods ago.
- Parameterized by `period` (default 9).
- Output range: Varies (see docs).
- Requires `period + 1` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "The ratio form of momentum: how many times larger is the current price compared to the past? A multiplier view of market movement."

ROCR (Rate of Change Ratio) calculates the ratio between the current value and the value N periods ago. Values hover around 1.0, with values above 1.0 indicating price increase and values below 1.0 indicating price decrease. Unlike ROC (absolute) or ROCP (percentage), ROCR provides a dimensionless multiplier that directly shows the price ratio.

## Historical Context

ROCR belongs to the family of momentum indicators that measure price change over time. The ratio form is particularly useful when comparing relative movements across instruments with different price scales. The terminology varies by platform and library:

- **TA-Lib**: Uses `ROCR` for ratio (price / past_price)
- **Tulip**: Uses `ROCR` for ratio
- **TradingView/PineScript**: Uses `source / source[n]` pattern
- **QuanTAlib**: Uses `ROCR` for ratio, `ROC` for absolute, `ROCP` for percentage

## Architecture & Physics

### 1. Ring Buffer Storage

The indicator maintains a sliding window of `period + 1` values:

$$
\text{buffer} = [v_{t-n}, v_{t-n+1}, ..., v_{t-1}, v_t]
$$

where $n$ is the lookback period. Only the oldest and newest values are needed for calculation.

### 2. Ratio Calculation

$$
\text{ROCR}_t = \frac{v_t}{v_{t-n}}
$$

where:
- $v_t$ = current value
- $v_{t-n}$ = value from $n$ periods ago
- Result is dimensionless (ratio around 1.0)

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
\text{ROCR}_t = \frac{P_t}{P_{t-n}}
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
\text{ROCR} = \text{CHANGE} + 1 = \frac{P_t}{P_{t-n}}
$$

$$
\text{ROCP} = (\text{ROCR} - 1) \times 100
$$

$$
\text{CHANGE} = \text{ROCR} - 1
$$

## Performance Profile

### Operation Count (Streaming Mode)

| Operation | Count | Notes |
| :--- | :---: | :--- |
| DIV | 1 | current / past |
| Buffer add | 1 | O(1) ring buffer |
| State copy | 1 | rollback support |
| Zero check | 1 | division safety |
| **Total** | **~4 ops** | Very lightweight |

### Batch Mode (Span-based)

The span-based calculation is a simple loop with no dependencies between iterations.

| Operation | Complexity | Notes |
| :--- | :---: | :--- |
| Per-element | O(1) | Single division |
| Total | O(n) | Linear scan |
| Memory | O(1) | No additional allocation |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact arithmetic, no approximation |
| **Timeliness** | 10/10 | Zero lag by definition |
| **Smoothness** | 3/10 | No smoothing, reflects raw volatility |
| **Simplicity** | 10/10 | Single division |

## Interpretation

* **ROCR = 1.0**: No change from N periods ago
* **ROCR > 1.0**: Price increased (e.g., 1.05 = 5% increase)
* **ROCR < 1.0**: Price decreased (e.g., 0.95 = 5% decrease)
* **ROCR = 2.0**: Price doubled
* **ROCR = 0.5**: Price halved

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | ✅ | ROCR matches exactly |
| **Tulip** | ✅ | Matches ratio calculation |
| **TradingView** | ✅ | Matches PineScript division |

## Common Pitfalls

1. **Value interpretation**: ROCR returns values around 1.0, not percentages. A ROCR of 1.05 means 5% increase, not 105% increase.

2. **Division by zero**: If the historical price is zero, ROCR returns 1.0 as a safe default.

3. **Warmup period**: The first `period` values return 1.0 as there's no historical reference point.

4. **Scale invariance**: ROCR is comparable across instruments since it's a ratio.

5. **Compounding**: ROCR values can be multiplied across periods: total_change = ROCR_1 × ROCR_2 × ...

## References

- Pring, M. J. (2014). "Technical Analysis Explained." McGraw-Hill.
- Murphy, J. J. (1999). "Technical Analysis of the Financial Markets." New York Institute of Finance.
- TA-Lib Documentation: ROCR function
