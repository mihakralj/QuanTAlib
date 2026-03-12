# MAPD: Mean Absolute Percentage Deviation

> *Like MAPE, but divides by what you predicted instead of what actually happened.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Error Metric                        |
| **Inputs**       | Actual, Predicted (dual series)          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (MAPD)                       |
| **Output range** | $\geq 0$                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [mapd.pine](mapd.pine)                       |

- Mean Absolute Percentage Deviation (MAPD) measures the average absolute percentage difference between actual and predicted values, using the predic...
- Parameterized by `period`.
- Output range: $\geq 0$.
- Requires 1 bar of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

Mean Absolute Percentage Deviation (MAPD) measures the average absolute percentage difference between actual and predicted values, using the predicted value as the denominator. This is the key difference from MAPE, which uses the actual value.

## Historical Context

MAPD emerged as an alternative to MAPE when analysts needed a metric that was more stable when actual values had high variance or approached zero. By using the predicted value as the denominator, MAPD provides different asymmetry characteristics than MAPE.

## Architecture & Physics

MAPD divides each absolute error by the predicted value instead of the actual value. This choice affects the asymmetry of the metric: MAPD penalizes under-prediction more heavily than over-prediction (opposite of MAPE).

### Properties

* **Scale-independent**: Expressed as percentage
* **Asymmetric**: Penalizes under-prediction more than over-prediction
* **Undefined at zero**: Cannot compute when predicted value is zero
* **Non-negative**: MAPD ≥ 0, with 0 indicating perfect prediction
* **Opposite bias to MAPE**: Favors over-prediction

## Mathematical Foundation

### 1. Percentage Deviation

For each observation, calculate the absolute percentage deviation:

$$APD_i = 100 \times \left| \frac{y_i - \hat{y}_i}{\hat{y}_i} \right|$$

Where:

* $y_i$ = actual value
* $\hat{y}_i$ = predicted value

### 2. Mean Calculation

Average the absolute percentage deviations over the period:

$$MAPD = \frac{100}{n} \sum_{i=1}^{n} \left| \frac{y_i - \hat{y}_i}{\hat{y}_i} \right|$$

### 3. Running Update (O(1))

QuanTAlib uses a ring buffer with running sum for O(1) updates:

$$S_{new} = S_{old} - APD_{oldest} + APD_{newest}$$

$$MAPD = \frac{S_{new}}{n}$$

## Implementation Details

### Usage Patterns

```csharp
// Streaming mode - update with each new observation
var mapd = new Mapd(period: 20);
var result = mapd.Update(actualValue, predictedValue);

// Batch mode - calculate for entire series
var results = Mapd.Calculate(actualSeries, predictedSeries, period: 20);

// Span mode - zero-allocation for high performance
Mapd.Batch(actualSpan, predictedSpan, outputSpan, period: 20);
```

### Parameters

| Parameter | Type | Description |
| :--- | :--- | :--- |
| **period** | int | Lookback window for averaging (must be > 0) |

### Properties

| Property | Type | Description |
| :--- | :--- | :--- |
| **Last** | TValue | Most recent MAPD value (as percentage) |
| **IsHot** | bool | True when buffer is full |
| **Name** | string | Indicator name (e.g., "Mapd(20)") |
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

## MAPE vs MAPD Comparison

```csharp
var mape = new Mape(1);
var mapd = new Mapd(1);

// actual=100, predicted=200
mape.Update(100, 200);  // |100-200|/100 = 100%
mapd.Update(100, 200);  // |100-200|/200 = 50%

// actual=200, predicted=100
mape.Update(200, 100);  // |200-100|/200 = 50%
mapd.Update(200, 100);  // |200-100|/100 = 100%
```

| Scenario | MAPE | MAPD |
| :--- | :--- | :--- |
| **Over-prediction** | Lower | Higher |
| **Under-prediction** | Higher | Lower |

## Comparison with Other Metrics

| Metric | Denominator | Bias |
| :--- | :--- | :--- |
| **MAPE** | Actual | Favors under-prediction |
| **MAPD** | Predicted | Favors over-prediction |
| **SMAPE** | (Actual + Predicted)/2 | Symmetric |
| **MAE** | None | No percentage conversion |

## Common Use Cases

1. **Forecast Validation**: When predicted values are more reliable than actuals
2. **Model Comparison**: Alternative perspective to MAPE
3. **Budgeting**: When comparing actuals to budget (predicted)
4. **Quality Control**: When predictions are the reference standard

## Edge Cases

* **Identical Values**: Returns 0% when actual equals predicted
* **Zero Predicted**: Uses epsilon (1e-10) to avoid division by zero
* **NaN Handling**: Uses last valid value substitution
* **Single Input**: Not supported (requires two series)
* **Period = 1**: Returns current percentage deviation

## Related Indicators

* [MAPE](../mape/Mape.md) - Mean Absolute Percentage Error (divides by actual)
* [SMAPE](../smape/Smape.md) - Symmetric Mean Absolute Percentage Error
* [MPE](../mpe/Mpe.md) - Mean Percentage Error (signed)
* [MAE](../mae/Mae.md) - Mean Absolute Error (same units)
