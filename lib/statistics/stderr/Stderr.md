# Stderr: Standard Error of Regression

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Statistic                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Stderr)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period` bars                          |

### TL;DR

- ````markdown
- Parameterized by `period`.
- Output range: Varies (see docs).
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "How confident are you in your line of best fit?"

Standard Error of Regression (also called the Standard Error of the Estimate) measures the average distance that the observed values fall from the regression line. It quantifies the typical size of the residuals, providing a direct measure of how well a linear regression model fits the data.

## Historical Context

The Standard Error of Regression has its roots in the work of Carl Friedrich Gauss and the method of least squares (1809). It became a cornerstone of inferential statistics, widely used in econometrics, quality control, and technical analysis. In finance, it serves as a volatility envelope around linear regression channels, helping traders identify statistically significant deviations from trend.

## Architecture & Physics

`Stderr` is implemented as a companion to the `LinReg` indicator. It uses the same least squares regression framework to fit a line to the data, then calculates the root mean square of the vertical distances (residuals) between each data point and the fitted line.

### Key Design Principles

* **O(N) per update**: Each update recalculates the residuals across the window to compute the standard error. The regression coefficients are derived from incrementally maintained sums.
* **Circular Buffer**: Uses a ring buffer of size `Period` for efficient sliding window management.
* **Numerical Stability**: Residual sum of squares is computed from the fitted line parameters, avoiding catastrophic cancellation.

## Mathematical Foundation

Given a linear regression line $\hat{y} = mx + b$ fitted to $N$ data points, the Standard Error of Regression is:

$$ SE = \sqrt{\frac{\sum_{i=1}^{N} (y_i - \hat{y}_i)^2}{N - 2}} $$

Where:

* $y_i$ is the observed value at time $i$.
* $\hat{y}_i = mx_i + b$ is the predicted value from the regression line.
* $N$ is the number of data points (period).
* $N - 2$ accounts for the two degrees of freedom consumed by estimating the slope and intercept.

The regression coefficients are:

$$ m = \frac{N \sum xy - \sum x \sum y}{N \sum x^2 - (\sum x)^2} $$

$$ b = \frac{\sum y - m \sum x}{N} $$

## Performance Profile

### Operation Count (Streaming Mode)

Standard Error = StdDev / sqrt(N), computed atop the O(1) StdDev computation.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| O(1) StdDev computation | 1 | 28 cy | ~28 cy |
| Divide by sqrt(N) (precomputed) | 1 | 4 cy | ~4 cy |
| NaN guard + state update | 1 | 2 cy | ~2 cy |
| **Total** | **O(1)** | — | **~34 cy** |

O(1) per update. sqrt(N) is precomputed in the constructor. Negligible additional cost over StdDev.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | Moderate | O(N) per update due to residual calculation. |
| **Allocations** | 0 | Zero-allocation hot path with ring buffer. |
| **Complexity** | O(N) | Must iterate window for residual sum of squares. |
| **Accuracy** | High | Matches standard statistical definitions. |

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **TA-Lib** | ✅ | Matches `STDERR` output. |
| **TradingView** | ✅ | Matches Pine Script `ta.stdev` of residuals. |

## Usage

```csharp
using QuanTAlib;

// Create a 14-period Standard Error of Regression
var stderr = new Stderr(14);

// Update with a new value
var result = stderr.Update(new TValue(DateTime.UtcNow, 100.0));

// Get the last value
double value = stderr.Last.Value;
```

## See Also

* **LinReg** — Linear Regression Curve (the trend line itself).
* **StdDev** — Standard Deviation (dispersion from the mean, not from a regression line).
````
