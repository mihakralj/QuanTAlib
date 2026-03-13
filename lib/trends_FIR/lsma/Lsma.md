# LSMA: Least Squares Moving Average

> *If you want to know where the price is going, draw a line through where it's been. LSMA does this for every single bar, tirelessly fitting linear regressions while you sleep.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (FIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`, `offset` (default 0)                      |
| **Outputs**      | Single series (Lsma)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [lsma.pine](lsma.pine)                       |
| **Signature**    | [lsma_signature](lsma_signature.md) |

- LSMA (Least Squares Moving Average), also known as the Moving Linear Regression or Endpoint Moving Average, calculates the least squares regression...
- **Similar:** [EPMA](../epma/epma.md), [ALMA](../alma/alma.md) | **Complementary:** R-squared for regression quality | **Trading note:** Least Squares MA; linear regression value at current bar, minimizing squared deviations.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

LSMA (Least Squares Moving Average), also known as the Moving Linear Regression or Endpoint Moving Average, calculates the least squares regression line for the preceding time periods. In plain English: it finds the "best fit" line for the data window and tells you where that line ends.

## Historical Context

Linear regression is as old as Gauss (c. 1809). Applying it as a moving window to financial time series is a more recent development, popularized by traders who realized that a moving average is just a poor man's regression line (specifically, an SMA is a regression line with a slope of 0). LSMA captures both the level and the trend (slope) of the data.

## Architecture & Physics

LSMA is computationally heavier than an SMA because it minimizes the sum of squared errors for a line equation $y = mx + b$.

* **Slope ($m$)**: Represents the trend strength/direction.
* **Intercept ($b$)**: Represents the value at the start of the window.
* **Endpoint**: The value at the current bar ($y = m \times 0 + b$ in our coordinate system where current bar is 0).

## Mathematical Foundation

The regression line is $y = mx + b$.

$$ m = \frac{N \sum xy - \sum x \sum y}{N \sum x^2 - (\sum x)^2} $$

$$ b = \frac{\sum y - m \sum x}{N} $$

$$ \text{LSMA} = b - m \times \text{Offset} $$

(Note: In the QuanTAlib implementation, $x$ ranges from $N-1$ (oldest) to $0$ (newest) to simplify the math).

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

The O(1) algorithm maintains running sums instead of recomputing the regression on each bar:

**State variables maintained:**
- `sum_x`: Sum of x indices (precomputed constant for fixed period)
- `sum_y`: Running sum of y values
- `sum_xy`: Running sum of x×y products
- `sum_xx`: Sum of x² (precomputed constant)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ADD/SUB | 6 | 1 | 6 |
| MUL | 4 | 3 | 12 |
| DIV | 2 | 15 | 30 |
| **Total** | **12** | — | **~48 cycles** |

**Hot path breakdown:**
- Update running sums: `sum_y += new - old`, `sum_xy += (N-1)×new - sum_y_old` → 4 ADD/SUB
- Slope calculation: `m = (N×sum_xy - sum_x×sum_y) / denom` → 2 MUL + 1 DIV
- Intercept: `b = (sum_y - m×sum_x) / N` → 1 MUL + 1 SUB + 1 DIV
- Endpoint: `LSMA = b - m×offset` → 1 MUL + 1 SUB

**Comparison with naive O(N) regression:**

| Mode | Complexity | Cycles (Period=100) |
| :--- | :---: | :---: |
| Naive (recompute) | O(N) | ~600 cycles |
| QuanTAlib O(1) | O(1) | ~48 cycles |
| **Improvement** | **—** | **~12× faster** |

### Batch Mode (SIMD)

LSMA batch can vectorize the running sum updates:

| Operation | Scalar Ops (512 bars) | SIMD Ops (AVX2) | Speedup |
| :--- | :---: | :---: | :---: |
| Running sum updates | 512 | 64 | 8× |
| Slope calculations | 1024 | 128 | 8× |
| Endpoint projections | 512 | 64 | 8× |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Mathematically precise regression endpoint |
| **Timeliness** | 8/10 | Projects trend forward, reducing perceived lag |
| **Overshoot** | 2/10 | Significant overshoot on trend reversals (projects continuation) |
| **Smoothness** | 3/10 | Sensitive to outliers; least-squares fit follows noise |

## Validation

Validated against Skender.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **Skender** | ✅ | Matches `GetEpma` |
| **TA-Lib** | N/A | Not implemented |

| **Tulip** | N/A | Not implemented. |
| **Ooples** | N/A | Not implemented. |

### Common Pitfalls

1. **Overshoot**: Because it projects a trend, LSMA will overshoot significantly when the trend reverses. It assumes the trend continues.
2. **Offset**: You can use a positive offset to extrapolate into the future (forecasting), or a negative offset to center the average.
3. **Noise**: It is very sensitive to outliers because it tries to fit a line to them.