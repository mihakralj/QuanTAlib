# LinReg: Linear Regression Curve

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Statistic                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`, `offset` (default 0)                      |
| **Outputs**      | Single series (LinReg)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period` bars                          |

### TL;DR

- The Linear Regression Curve plots the end point of the linear regression line for each bar.
- Parameterized by `period`, `offset` (default 0).
- Output range: Varies (see docs).
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "The trend is your friend, until it bends."

The Linear Regression Curve plots the end point of the linear regression line for each bar. It fits a straight line $y = mx + b$ to the data points using the least squares method, providing a smoothed representation of the price trend that is more responsive than a Simple Moving Average (SMA).

## Historical Context

Linear Regression is a fundamental statistical tool used to model the relationship between a dependent variable (price) and an independent variable (time). In technical analysis, it is used to identify the prevailing trend and potential reversal points. The Linear Regression Curve (often called LSMA or Least Squares Moving Average) connects the endpoints of regression lines calculated over a rolling window.

## Architecture & Physics

The `LinReg` indicator calculates the best-fit line for the last `Period` data points. It minimizes the sum of squared vertical distances between the observed data and the fitted line.

The calculation is optimized for streaming data using O(1) updates. Instead of recalculating the sums of $x$, $y$, $xy$, and $x^2$ from scratch for each new bar, the algorithm updates these sums incrementally as the window slides.

### Implementation Details

* **O(1) Update Formula**: The incremental update for $\sum xy$ is mathematically elegant. When removing the oldest value and shifting all x-coordinates by +1, the sum increases by the previous sum of y minus the contribution of the oldest value: `sum_xy_new = sum_xy_old + prev_sum_y - n * oldest`.
* **Floating-Point Drift Protection**: To combat the accumulation of rounding errors inherent in incremental algorithms, the indicator performs a full recalculation from scratch every 1000 updates (`ResyncInterval`).
* **R-Squared Stability**: Handles edge cases where variance is zero (all values identical) by setting $R^2$ to 1.0 (perfect fit to a horizontal line), avoiding division by zero.
* **Slope Sign Convention**: The internal coordinate system uses $x=0$ for the present and increases into the past. This results in a negative slope for rising prices in x-space. The public `Slope` property negates this value (`Slope = -m`) to provide a standard time-forward slope interpretation.

### Complexity

| Metric | Value | Notes |
| :--- | :--- | :--- |
| **Time Complexity** | O(1) | Constant time update per bar. |
| **Space Complexity** | O(N) | Requires a buffer of size `Period`. |
| **Stability** | High | Uses double precision floating point. |

## Mathematical Foundation

The linear regression line is defined by the equation:

$$ y = mx + b $$

Where:

* $m$ is the slope.
* $b$ is the y-intercept.
* $x$ is the time index (0 for the current bar, increasing into the past).

The coefficients are calculated as:

$$ m = \frac{n \sum xy - \sum x \sum y}{n \sum x^2 - (\sum x)^2} $$

$$ b = \frac{\sum y - m \sum x}{n} $$

The `LinReg` value at the current bar (offset 0) is simply the intercept $b$ (since $x=0$).

### Properties

* **Slope**: The rate of change of the regression line. Positive slope indicates an uptrend, negative slope indicates a downtrend.
* **Intercept**: The value of the regression line at the current bar.
* **RSquared**: The coefficient of determination ($r^2$), indicating how well the line fits the data (0 to 1).

## Performance Profile

### Operation Count (Streaming Mode)

LinReg uses online running sums (Sx, Sy, Sxx, Sxy) for exact O(1) linear regression update.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Ring buffer add/evict | 1 | 3 cy | ~3 cy |
| Update 4 running sums | 4 | 2 cy | ~8 cy |
| Solve slope + intercept (2x2 system) | 1 | 6 cy | ~6 cy |
| Project endpoint value via FMA | 1 | 1 cy | ~1 cy |
| NaN guard + state update | 1 | 2 cy | ~2 cy |
| **Total** | **O(1)** | — | **~20 cy** |

O(1) per update. The fastest OLS variant because time index is deterministic; Sx and Sxx have closed-form expressions in terms of N.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | High | O(1) updates ensure minimal latency. |
| **Allocations** | 0 | Zero allocations in the hot path. |
| **Accuracy** | High | Matches standard statistical definitions. |
| **Responsiveness** | High | More responsive than SMA for the same period. |

## Validation

Validated against Skender.Stock.Indicators.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **Skender** | ✅ | Slope and RSquared match. |
| **Ooples** | ⚠️ | Slope magnitude differs significantly (likely unit mismatch). |

## Usage

```csharp
using QuanTAlib;

// Create indicator with period 14
var linreg = new LinReg(14);

// Update with new value
linreg.Update(new TValue(DateTime.UtcNow, 100.0));

// Access result
double value = linreg.Last.Value;
double slope = linreg.Slope;
double r2 = linreg.RSquared;
