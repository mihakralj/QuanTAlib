# MAE: Mean Absolute Error

> "When you need to know how wrong you are on average, without the drama of squared errors."

Mean Absolute Error (MAE) measures the average magnitude of errors in a set of predictions, without considering their direction. It represents the average of the absolute differences between actual and predicted values.

## Historical Context

MAE is one of the oldest and most intuitive error metrics in statistics. Its simplicity and interpretability have made it a staple in regression analysis, forecasting, and model evaluation since the early days of statistical analysis.

## Architecture & Physics

MAE treats all errors equally, making it more robust to outliers compared to squared-error metrics like MSE. The absolute value operation removes directionality, focusing purely on error magnitude.

### Properties

- **Non-negative**: MAE ≥ 0, with 0 indicating perfect prediction
- **Same units**: Unlike MSE, MAE is in the same units as the original data
- **Linear sensitivity**: Each unit of error contributes equally to the final metric
- **Robust**: Less sensitive to outliers than squared-error metrics

## Mathematical Foundation

### 1. Absolute Error

For each observation, calculate the absolute difference between actual and predicted values:

$$e_i = |y_i - \hat{y}_i|$$

Where:
- $y_i$ = actual value
- $\hat{y}_i$ = predicted value

### 2. Mean Calculation

Average the absolute errors over the period:

$$MAE = \frac{1}{n} \sum_{i=1}^{n} |y_i - \hat{y}_i|$$

### 3. Running Update (O(1))

QuanTAlib uses a ring buffer with running sum for O(1) updates:

$$S_{new} = S_{old} - e_{oldest} + e_{newest}$$

$$MAE = \frac{S_{new}}{n}$$

## Implementation Details

### Usage Patterns

```csharp
// Streaming mode - update with each new observation
var mae = new Mae(period: 20);
var result = mae.Update(actualValue, predictedValue);

// Batch mode - calculate for entire series
var results = Mae.Calculate(actualSeries, predictedSeries, period: 20);

// Span mode - zero-allocation for high performance
Mae.Batch(actualSpan, predictedSpan, outputSpan, period: 20);
```

### Parameters

| Parameter | Type | Description |
| :--- | :--- | :--- |
| **period** | int | Lookback window for averaging (must be > 0) |

### Properties

| Property | Type | Description |
| :--- | :--- | :--- |
| **Last** | TValue | Most recent MAE value |
| **IsHot** | bool | True when buffer is full |
| **Name** | string | Indicator name (e.g., "Mae(20)") |
| **WarmupPeriod** | int | Number of periods before valid output |

## Performance Profile

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | ~10 ns/bar | O(1) update complexity |
| **Allocations** | 0 | Uses pre-allocated ring buffer |
| **Complexity** | O(1) | Constant time per update |
| **Accuracy** | 10/10 | Exact calculation |
| **Timeliness** | 9/10 | No lag beyond the period |
| **Smoothness** | 7/10 | Moderate smoothing |

## Interpretation

| MAE Range | Interpretation |
| :--- | :--- |
| **0** | Perfect prediction |
| **Low** | Predictions are close to actual values |
| **High** | Large average prediction error |

## Comparison with Other Metrics

| Metric | Outlier Sensitivity | Units | Interpretation |
| :--- | :--- | :--- | :--- |
| **MAE** | Low | Same as data | Average absolute error |
| **MSE** | High | Squared units | Penalizes large errors more |
| **RMSE** | High | Same as data | MSE in original units |
| **MAPE** | Varies | Percentage | Relative error |

## Common Use Cases

1. **Forecast Evaluation**: Measure prediction accuracy over time
2. **Model Comparison**: Compare different prediction models
3. **Trading Strategy**: Track signal accuracy
4. **Risk Assessment**: Monitor prediction reliability

## Edge Cases

- **Identical Values**: Returns 0 when actual equals predicted
- **NaN Handling**: Uses last valid value substitution
- **Single Input**: Not supported (requires two series)
- **Period = 1**: Returns current absolute error

## Related Indicators

- [MSE](../mse/Mse.md) - Mean Squared Error
- [RMSE](../rmse/Rmse.md) - Root Mean Squared Error
- [MAPE](../mape/Mape.md) - Mean Absolute Percentage Error
