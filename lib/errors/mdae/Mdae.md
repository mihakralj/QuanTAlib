# MdAE: Median Absolute Error

> "When outliers scream but you need to hear the whisper of typical performance."

Median Absolute Error (MdAE) measures the middle value of all absolute errors. Unlike MAE which averages errors, MdAE finds the median, providing exceptional robustness against outliers and extreme values.

## Historical Context

MdAE emerged from robust statistics, where the median has long been preferred over the mean for its resistance to outliers. In forecasting and machine learning, MdAE provides a more stable measure of typical prediction accuracy when data contains anomalies or heavy-tailed distributions.

## Architecture & Physics

MdAE maintains a sorted view of errors through a specialized ring buffer. When new errors arrive, they replace the oldest while maintaining sort order, enabling O(1) median retrieval. This makes MdAE both robust and efficient.

### Properties

- **Outlier-robust**: Unaffected by extreme values
- **Non-negative**: MdAE ≥ 0, with 0 indicating perfect prediction
- **Same units**: Results are in the same units as the original data
- **Stable**: Small changes in data produce small changes in output

## Mathematical Foundation

### 1. Absolute Error

For each observation, calculate the absolute difference:

$$e_i = |y_i - \hat{y}_i|$$

Where:
- $y_i$ = actual value
- $\hat{y}_i$ = predicted value

### 2. Median Calculation

Find the middle value of the sorted errors:

$$MdAE = \text{median}(e_1, e_2, ..., e_n)$$

For odd n: middle element
For even n: average of two middle elements

### 3. Running Update (O(1))

QuanTAlib uses a sorted ring buffer for efficient median retrieval:

$$MdAE = \begin{cases}
e_{(n+1)/2} & \text{if } n \text{ is odd} \\
\frac{e_{n/2} + e_{n/2+1}}{2} & \text{if } n \text{ is even}
\end{cases}$$

## Implementation Details

### Usage Patterns

```csharp
// Streaming mode - update with each new observation
var mdae = new Mdae(period: 20);
var result = mdae.Update(actualValue, predictedValue);

// Batch mode - calculate for entire series
var results = Mdae.Calculate(actualSeries, predictedSeries, period: 20);

// Span mode - zero-allocation for high performance
Mdae.Batch(actualSpan, predictedSpan, outputSpan, period: 20);
```

### Parameters

| Parameter | Type | Description |
| :--- | :--- | :--- |
| **period** | int | Lookback window for median calculation (must be > 0) |

### Properties

| Property | Type | Description |
| :--- | :--- | :--- |
| **Last** | TValue | Most recent MdAE value |
| **IsHot** | bool | True when buffer is full |
| **Name** | string | Indicator name (e.g., "Mdae(20)") |
| **WarmupPeriod** | int | Number of periods before valid output |

## Performance Profile

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | ~20 ns/bar | O(1) with sorted buffer |
| **Allocations** | 0 | Uses pre-allocated buffers |
| **Complexity** | O(1) | Constant time per update |
| **Accuracy** | 10/10 | Exact calculation |
| **Timeliness** | 9/10 | No lag beyond the period |
| **Robustness** | 10/10 | Immune to outliers |

## Interpretation

| MdAE Range | Interpretation |
| :--- | :--- |
| **0** | Perfect prediction |
| **Low** | Typical predictions are close to actual values |
| **High** | Typical prediction error is large |
| **MdAE < MAE** | Outliers are inflating the mean |
| **MdAE ≈ MAE** | Errors are symmetrically distributed |

## Comparison with MAE

| Scenario | MAE | MdAE |
| :--- | :--- | :--- |
| **No outliers** | Similar values | Similar values |
| **Single large outlier** | Significantly affected | Unchanged |
| **Heavy-tailed errors** | Inflated | Stable |
| **Symmetric errors** | Equal | Equal |

## Common Use Cases

1. **Anomaly Detection**: When some predictions may be wildly off
2. **Financial Markets**: Price forecasting with occasional extreme moves
3. **Robust Evaluation**: Model comparison ignoring outlier performance
4. **Quality Control**: Track typical accuracy without noise

## Edge Cases

- **Identical Values**: Returns 0 when actual equals predicted
- **NaN Handling**: Uses last valid value substitution
- **Single Input**: Not supported (requires two series)
- **Period = 1**: Returns current absolute error
- **All Same Errors**: Returns that error value

## Related Indicators

- [MAE](../mae/Mae.md) - Mean Absolute Error (uses mean)
- [MdAPE](../mdape/Mdape.md) - Median Absolute Percentage Error
- [Huber](../huber/Huber.md) - Huber Loss (robust but differentiable)
