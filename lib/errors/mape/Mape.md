# MAPE: Mean Absolute Percentage Error

> "The metric that lets you compare apples to oranges, as long as you don't have any zeros."

Mean Absolute Percentage Error (MAPE) measures the average absolute percentage difference between actual and predicted values. It expresses accuracy as a percentage, making it scale-independent and easy to interpret.

## Historical Context

MAPE has been widely used in forecasting and operations research since the mid-20th century. Its intuitive percentage-based interpretation makes it a favorite in business contexts where stakeholders need to understand prediction accuracy without domain expertise.

## Architecture & Physics

MAPE divides each absolute error by the actual value, converting errors to percentages. This makes it independent of the scale of the data but introduces asymmetry and problems with zero values.

### Properties

* **Scale-independent**: Expressed as percentage
* **Asymmetric**: Penalizes over-prediction more than under-prediction
* **Undefined at zero**: Cannot compute when actual value is zero
* **Non-negative**: MAPE ≥ 0, with 0 indicating perfect prediction
* **No upper bound**: Can exceed 100% for large errors

## Mathematical Foundation

### 1. Percentage Error

For each observation, calculate the absolute percentage error:

$$APE_i = 100 \times \left| \frac{y_i - \hat{y}_i}{y_i} \right|$$

Where:

* $y_i$ = actual value
* $\hat{y}_i$ = predicted value

### 2. Mean Calculation

Average the absolute percentage errors over the period:

$$MAPE = \frac{100}{n} \sum_{i=1}^{n} \left| \frac{y_i - \hat{y}_i}{y_i} \right|$$

### 3. Running Update (O(1))

QuanTAlib uses a ring buffer with running sum for O(1) updates:

$$S_{new} = S_{old} - APE_{oldest} + APE_{newest}$$

$$MAPE = \frac{S_{new}}{n}$$

## Implementation Details

### Usage Patterns

```csharp
// Streaming mode - update with each new observation
var mape = new Mape(period: 20);
var result = mape.Update(actualValue, predictedValue);

// Batch mode - calculate for entire series
var results = Mape.Calculate(actualSeries, predictedSeries, period: 20);

// Span mode - zero-allocation for high performance
Mape.Batch(actualSpan, predictedSpan, outputSpan, period: 20);
```

### Parameters

| Parameter | Type | Description |
| :--- | :--- | :--- |
| **period** | int | Lookback window for averaging (must be > 0) |

### Properties

| Property | Type | Description |
| :--- | :--- | :--- |
| **Last** | TValue | Most recent MAPE value (as percentage) |
| **IsHot** | bool | True when buffer is full |
| **Name** | string | Indicator name (e.g., "Mape(20)") |
| **WarmupPeriod** | int | Number of periods before valid output |

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
| **Throughput** | ~12 ns/bar | O(1) update complexity |
| **Allocations** | 0 | Uses pre-allocated ring buffer |
| **Complexity** | O(1) | Constant time per update |
| **Accuracy** | 10/10 | Exact calculation |
| **Timeliness** | 9/10 | No lag beyond the period |
| **Smoothness** | 7/10 | Moderate smoothing |

## Interpretation

| MAPE Range | Interpretation |
| :--- | :--- |
| **< 10%** | Highly accurate |
| **10-20%** | Good accuracy |
| **20-50%** | Reasonable accuracy |
| **> 50%** | Poor accuracy |

## The Asymmetry Problem

MAPE is asymmetric because it divides by the actual value:

```csharp
var mape1 = new Mape(1);
var mape2 = new Mape(1);

// Under-prediction: actual=100, predicted=50
// |100-50|/100 = 50%
mape1.Update(100, 50);  // Returns 50%

// Over-prediction: actual=50, predicted=100  
// |50-100|/50 = 100%
mape2.Update(50, 100);  // Returns 100%
```

Same absolute error (50), but over-prediction shows higher MAPE.

## Comparison with Other Metrics

| Metric | Scale | Handles Zero | Symmetric |
| :--- | :--- | :--- | :--- |
| **MAPE** | Percentage | No | No |
| **MAPD** | Percentage | No | No |
| **SMAPE** | Percentage | Partially | Yes |
| **MAE** | Original units | Yes | Yes |
| **MPE** | Percentage | No | Yes (signed) |

## Common Use Cases

1. **Demand Forecasting**: Inventory and supply chain planning
2. **Sales Prediction**: Revenue forecasting accuracy
3. **Financial Modeling**: Investment return predictions
4. **Operations**: Capacity planning and scheduling

## Limitations

1. **Zero Values**: Undefined when actual = 0 (QuanTAlib uses epsilon fallback)
2. **Asymmetry**: Biases toward under-prediction
3. **Scale Sensitivity**: Low values inflate MAPE disproportionately
4. **Outlier Impact**: Single large percentage error can dominate

## Edge Cases

* **Identical Values**: Returns 0% when actual equals predicted
* **Zero Actual**: Uses epsilon (1e-10) to avoid division by zero
* **NaN Handling**: Uses last valid value substitution
* **Single Input**: Not supported (requires two series)
* **Period = 1**: Returns current percentage error

## Related Indicators

* [MAPD](../mapd/Mapd.md) - Mean Absolute Percentage Deviation (divides by predicted)
* [SMAPE](../smape/Smape.md) - Symmetric Mean Absolute Percentage Error
* [MPE](../mpe/Mpe.md) - Mean Percentage Error (signed)
* [MAE](../mae/Mae.md) - Mean Absolute Error (same units)
