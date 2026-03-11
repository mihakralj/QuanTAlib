# Stderr: Standard Error of Regression

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Statistic                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Stderr)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [stderr.pine](stderr.pine)                       |

- `Stderr` computes the standard error of an OLS regression fit over a rolling window.
- Parameterized by `period`.
- Output range: non-negative real values (or 0 during insufficient/degenerate windows).
- Requires `period` bars of warmup before first stable output (`IsHot = true`).
- Validated against an internal brute-force OLS reference implementation.

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

`Stderr` keeps regression sums in O(1), then performs an O(N) residual pass to compute SSR.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Running-sum updates | O(1) | — | small |
| Residual SSR scan | O(N) | dominant | dominant |
| Final sqrt/divide | O(1) | — | small |
| **Total** | **O(N)** | — | period-dependent |

Per-update complexity is O(N) because residuals must be re-evaluated for the current window.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | Moderate | O(N) per update due to residual calculation. |
| **Allocations** | 0 | Zero-allocation hot path with ring buffer. |
| **Complexity** | O(N) | Must iterate window for residual sum of squares. |
| **Accuracy** | High | Matches standard statistical definitions. |

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **TA-Lib** | ⚠️ | Formula differs (`stderr` in Tulip/other libs often means standard error of mean). |
| **TradingView** | ✅ | Matches Pine-style OLS residual standard error behavior for this implementation. |
| **Reference OLS** | ✅ | Cross-validated against brute-force OLS residual calculation. |

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
- **StdDev** — Standard Deviation (dispersion from the mean, not from a regression line).
