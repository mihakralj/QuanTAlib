# ROCP: Rate of Change (Fractional)

> *The decimal-fraction form of momentum: matches TA-Lib's ROCP exactly. Multiply by 100 to get percentage (QuanTAlib's ROC).*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Momentum                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period` (default 9)                      |
| **Outputs**      | Single series (Rocp)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period + 1` bars                          |
| **PineScript**   | [rocp.pine](rocp.pine)                       |

- ROCP (Rate of Change, Fractional) calculates the decimal fractional change between the current value and the value N periods ago.
- **Similar:** [MOM](../mom/Mom.md), [ROC](../roc/Roc.md), [ROCR](../rocr/Rocr.md) | **Complementary:** Volume ROC | **Trading note:** Rate of Change, decimal form (0.05 = 5%); matches TA-Lib's ROCP.
- Validated against TA-Lib reference implementation.

ROCP calculates the fractional (decimal) change between the current value and the value N periods ago (e.g., 0.05 = 5% increase, -0.03 = 3% decrease). It is identical to QuanTAlib's [ROC](../roc/Roc.md) divided by 100.

## Historical Context

TA-Lib exposes three related rate-of-change functions that are frequently confused:

- **`MOM`**: absolute change, `Price - Price[N]` (price units)
- **`ROC`**: percentage change, `100 × (Price - Price[N]) / Price[N]` (5.0 = 5%)
- **`ROCP`**: fractional change, `(Price - Price[N]) / Price[N]` (0.05 = 5%)
- **`ROCR`**: ratio, `Price / Price[N]` (1.05 = 5% increase)

QuanTAlib mirrors this exactly: `Mom`, `Roc`, `Rocp`, `Rocr` map one-to-one to TA-Lib's `MOM`, `ROC`, `ROCP`, `ROCR`.

## Core Formula

$$
\text{ROCP}_t = \frac{P_t - P_{t-n}}{P_{t-n}}
$$

### Conversions

$$
\text{ROCP} = \frac{\text{ROC}}{100}
$$

## Behavior

* **ROCP > 0**: Price increased (e.g., 0.05 = 5% increase)
* **ROCP < 0**: Price decreased (e.g., -0.03 = 3% decrease)
* **ROCP = 1.0**: Price doubled
* **ROCP = -0.5**: Price halved

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | ✅ | Matches TA-Lib's `ROCP` directly (both return the decimal fraction) |

## Common Pitfalls

1. **Scale**: ROCP returns a decimal fraction. A return of 0.05 means 5%, not 5.0.

2. **Don't confuse with ROC**: QuanTAlib's `Roc` (a separate indicator) returns percentage (5.0 for 5%). This `Rocp` indicator returns the decimal fraction (0.05 for 5%).

3. **Division by zero**: If the historical price is zero, ROCP returns 0.0 as a safe default.

4. **Warmup period**: The first `period` values return 0.0.

## References

- TA-Lib Documentation: ROCP function
