# MRAE: Mean Relative Absolute Error

> "When you need to understand your error in the context of what you're predicting."

Mean Relative Absolute Error (MRAE) measures the average magnitude of errors relative to the actual values. This normalization makes the metric scale-independent and easier to interpret across different datasets.

## Historical Context

MRAE emerged as an alternative to MAPE for situations where relative error measurement is important but where the issues with percentage-based metrics (like undefined values when actuals are zero) need to be handled differently. It provides a bounded, interpretable measure of prediction accuracy.

## Architecture & Physics

MRAE divides each absolute error by the actual value, providing context for the error magnitude. The error of 5 means something different when predicting 10 versus predicting 1000, and MRAE captures this distinction.

### Properties

* **Scale-independent**: Comparable across different data magnitudes
* **Non-negative**: MRAE ≥ 0, with 0 indicating perfect prediction
* **Interpretable**: A value of 0.1 means 10% average relative error
* **Denominator sensitivity**: Undefined when actual values are zero (handled via substitution)

## Mathematical Foundation

### 1. Relative Absolute Error

For each observation, calculate the relative error:

$$e_i = \frac{|y_i - \hat{y}_i|}{|y_i|}$$

Where:
* $y_i$ = actual value
* $\hat{y}_i$ = predicted value

### 2. Mean Calculation

Average the relative errors over the period:

$$MRAE = \frac{1}{n} \sum_{i=1}^{n} \frac{|y_i - \hat{y}_i|}{|y_i|}$$

### 3. Running Update (O(1))

QuanTAlib uses a ring buffer with running sum for O(1) updates:

$$S_{new} = S_{old} - e_{oldest} + e_{newest}$$

$$MRAE = \frac{S_{new}}{n}$$

## Implementation Details

### Usage Patterns

```csharp
// Streaming mode - update with each new observation
var mrae = new Mrae(period: 20);
var result = mrae.Update(actualValue, predictedValue);

// Batch mode - calculate for entire series
var results = Mrae.Calculate(actualSeries, predictedSeries, period: 20);

// Span mode - zero-allocation for high performance
Mrae.Batch(actualSpan, predictedSpan, outputSpan, period: 20);
```

### Parameters

| Parameter | Type | Description |
| :--- | :--- | :--- |
| **period** | int | Lookback window for averaging (must be > 0) |

### Properties

| Property | Type | Description |
| :--- | :--- | :--- |
| **Last** | TValue | Most recent MRAE value |
| **IsHot** | bool | True when buffer is full |
| **Name** | string | Indicator name (e.g., "Mrae(20)") |
| **WarmupPeriod** | int | Number of periods before valid output |

## Performance Profile

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | ~15 ns/bar | O(1) update complexity |
| **Allocations** | 0 | Uses pre-allocated ring buffer |
| **Complexity** | O(1) | Constant time per update |
| **Accuracy** | 10/10 | Exact calculation |
| **Timeliness** | 9/10 | No lag beyond the period |
| **Smoothness** | 7/10 | Moderate smoothing |

## Interpretation

| MRAE Range | Interpretation |
| :--- | :--- |
| **0** | Perfect prediction |
| **0 - 0.1** | Excellent (< 10% average relative error) |
| **0.1 - 0.3** | Good (10-30% average relative error) |
| **> 0.3** | Poor (> 30% average relative error) |

## Comparison with Other Metrics

| Metric | Scale-Independent | Zero-Safe | Symmetry |
| :--- | :--- | :--- | :--- |
| **MRAE** | Yes | No (uses substitution) | No |
| **MAPE** | Yes | No | No |
| **MAE** | No | Yes | Yes |
| **SMAPE** | Yes | Partially | Yes |

## Common Use Cases

1. **Financial Forecasting**: Compare prediction accuracy across different asset prices
2. **Demand Forecasting**: Normalize errors across products with varying sales volumes
3. **Model Comparison**: Compare models on datasets with different scales
4. **Time Series Analysis**: Track relative prediction quality over time

## Edge Cases

* **Zero Actual Values**: Substitutes with small epsilon (1e-10) to avoid division by zero
* **NaN Handling**: Uses last valid value substitution
* **Single Input**: Not supported (requires two series)
* **Period = 1**: Returns current relative absolute error

## Related Indicators

* [MAE](../mae/Mae.md) - Mean Absolute Error (non-relative)
* [MAPE](../mape/Mape.md) - Mean Absolute Percentage Error
* [SMAPE](../smape/Smape.md) - Symmetric Mean Absolute Percentage Error
