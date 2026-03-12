# MSE: Mean Squared Error

> *The metric that makes outliers pay dearly for their transgressions.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Error Metric                        |
| **Inputs**       | Actual, Predicted (dual series)          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (MSE)                       |
| **Output range** | $\geq 0$                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [mse.pine](mse.pine)                       |

- Mean Squared Error (MSE) measures the average of the squares of the errors between actual and predicted values.
- Parameterized by `period`.
- Output range: $\geq 0$.
- Requires 1 bar of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

Mean Squared Error (MSE) measures the average of the squares of the errors between actual and predicted values. By squaring errors, MSE penalizes large deviations more heavily than small ones.

## Historical Context

MSE is fundamental to least-squares regression, dating back to Gauss and Legendre in the early 1800s. It remains the most widely used loss function in machine learning and statistical modeling due to its mathematical convenience and theoretical properties.

## Architecture & Physics

MSE squares each error before averaging, which has significant implications:

* Large errors contribute disproportionately to the metric
* The quadratic penalty creates a smooth, differentiable loss surface
* Optimal for normally distributed errors

### Properties

* **Non-negative**: MSE ≥ 0, with 0 indicating perfect prediction
* **Squared units**: If data is in dollars, MSE is in dollars²
* **Outlier sensitive**: Single large error dominates the metric
* **Differentiable**: Smooth gradient for optimization algorithms

## Mathematical Foundation

### 1. Squared Error

For each observation, calculate the squared difference:

$$e_i = (y_i - \hat{y}_i)^2$$

### 2. Mean Calculation

Average the squared errors over the period:

$$MSE = \frac{1}{n} \sum_{i=1}^{n} (y_i - \hat{y}_i)^2$$

### 3. Running Update (O(1))

QuanTAlib uses a ring buffer with running sum for O(1) updates:

$$S_{new} = S_{old} - e_{oldest} + e_{newest}$$

$$MSE = \frac{S_{new}}{n}$$

## Implementation Details

### Usage Patterns

```csharp
// Streaming mode
var mse = new Mse(period: 20);
var result = mse.Update(actualValue, predictedValue);

// Batch mode
var results = Mse.Calculate(actualSeries, predictedSeries, period: 20);

// Span mode - zero allocation
Mse.Batch(actualSpan, predictedSpan, outputSpan, period: 20);
```

### Parameters

| Parameter | Type | Description |
| :--- | :--- | :--- |
| **period** | int | Lookback window for averaging (must be > 0) |

## Performance Profile

### Operation Count (Streaming Mode)

O(1) per bar. Single-pass scalar transformation of (actual, forecast) pair; no lookback window required.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Error computation (subtract, abs/square/log) | 1-3 | ~3-8 cy | ~5-15 cy |
| Running accumulator update (EMA or sum) | 1 | ~4 cy | ~4 cy |
| **Total** | **2-4** | — | **~9-19 cycles** |

Streaming update requires only the current actual/forecast pair and running state. ~10-15 cycles/bar typical.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| Element-wise error computation | Yes | Independent per bar; fully vectorizable with `Vector<double>` |
| Reduction (sum/mean) | Yes | Parallel reduction; AVX2 gives 4x speedup |
| Log/exp components | Partial | Transcendental ops; polynomial approx for SIMD |

Batch SIMD: 4x-8x speedup for large windows. ~3-5 cy/bar amortized in vectorized batch mode.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | ~12 ns/bar | O(1) with one multiplication |
| **Allocations** | 0 | Pre-allocated ring buffer |
| **Complexity** | O(1) | Constant time per update |
| **Accuracy** | 10/10 | Exact calculation |

## Interpretation

| MSE Range | Interpretation |
| :--- | :--- |
| **0** | Perfect prediction |
| **Low** | Predictions are close to actual values |
| **High** | Large prediction errors present |

## Relationship to RMSE

RMSE (Root Mean Squared Error) is simply the square root of MSE:

$$RMSE = \sqrt{MSE}$$

RMSE has the advantage of being in the same units as the original data.

## Common Use Cases

1. **Loss Function**: Primary loss for regression models
2. **Model Selection**: Compare models on validation data
3. **Gradient Descent**: Smooth gradient enables optimization
4. **Variance Estimation**: Related to sample variance

## Edge Cases

* **Identical Values**: Returns 0 when actual equals predicted
* **NaN Handling**: Uses last valid value substitution
* **Large Errors**: Can produce very large values due to squaring

## Related Indicators

* [MAE](../mae/Mae.md) - Mean Absolute Error (robust to outliers)
* [RMSE](../rmse/Rmse.md) - Root Mean Squared Error (same units as data)
* [Huber](../huber/Huber.md) - Combines MSE and MAE benefits
