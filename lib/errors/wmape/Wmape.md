# WMAPE: Weighted Mean Absolute Percentage Error

> *When not all errors are created equal, weight them by what matters.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Error Metric                        |
| **Inputs**       | Actual vs Predicted (dual input)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Wmape)                       |
| **Output range** | $\geq 0$                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [wmape.pine](wmape.pine)                       |

- Weighted Mean Absolute Percentage Error (WMAPE) adjusts MAPE by weighting each error by the magnitude of the actual value.
- Parameterized by `period`.
- Output range: $\geq 0$.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

Weighted Mean Absolute Percentage Error (WMAPE) adjusts MAPE by weighting each error by the magnitude of the actual value. This produces a single, interpretable percentage that represents overall accuracy weighted by importance.

## Historical Context

WMAPE emerged from retail and supply chain forecasting where aggregate accuracy matters more than individual item accuracy. A 10% error on a high-volume product impacts business more than the same percentage error on a low-volume item. WMAPE naturally captures this by summing absolute errors before dividing by summed actuals.

## Architecture & Physics

WMAPE accumulates both absolute errors and actual values, then computes their ratio. This approach means larger actual values contribute proportionally more to the final metric, providing a volume-weighted view of accuracy.

### Characteristics

* **Volume-weighted**: High-value items contribute more to the metric
* **Scale-independent**: Result is always a percentage
* **Non-negative**: WMAPE ≥ 0, with 0 indicating perfect prediction
* **Aggregate interpretation**: Represents total error as percentage of total actual

## Mathematical Foundation

### 1. Weighted Error Accumulation

Sum absolute errors and actual values separately:

$$\text{Total Error} = \sum_{i=1}^{n} |y_i - \hat{y}_i|$$

$$\text{Total Actual} = \sum_{i=1}^{n} |y_i|$$

### 2. WMAPE Calculation

Divide total error by total actual:

$$WMAPE = \frac{\sum_{i=1}^{n} |y_i - \hat{y}_i|}{\sum_{i=1}^{n} |y_i|} \times 100$$

### 3. Running Update (O(1))

QuanTAlib maintains two running sums for O(1) updates:

$$S_{err,new} = S_{err,old} - e_{oldest} + e_{newest}$$

$$S_{act,new} = S_{act,old} - a_{oldest} + a_{newest}$$

$$WMAPE = \frac{S_{err,new}}{S_{act,new}} \times 100$$

## Implementation Details

### Usage Patterns

```csharp
// Streaming mode - update with each new observation
var wmape = new Wmape(period: 20);
var result = wmape.Update(actualValue, predictedValue);

// Batch mode - calculate for entire series
var results = Wmape.Calculate(actualSeries, predictedSeries, period: 20);

// Span mode - zero-allocation for high performance
Wmape.Batch(actualSpan, predictedSpan, outputSpan, period: 20);
```

### Parameters

| Parameter | Type | Description |
| :--- | :--- | :--- |
| **period** | int | Lookback window for calculation (must be > 0) |

### Properties

| Property | Type | Description |
| :--- | :--- | :--- |
| **Last** | TValue | Most recent WMAPE value (in percentage) |
| **IsHot** | bool | True when buffer is full |
| **Name** | string | Indicator name (e.g., "Wmape(20)") |
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
| **Allocations** | 0 | Uses pre-allocated ring buffers |
| **Complexity** | O(1) | Constant time per update |
| **Accuracy** | 10/10 | Exact calculation |
| **Timeliness** | 9/10 | No lag beyond the period |
| **Interpretability** | 10/10 | Clear business meaning |

## Interpretation

| WMAPE Range | Interpretation |
| :--- | :--- |
| **0%** | Perfect prediction |
| **0-5%** | Excellent (total error < 5% of total actual) |
| **5-15%** | Good aggregate accuracy |
| **15-30%** | Moderate accuracy |
| **> 30%** | Poor aggregate accuracy |

## Comparison with MAPE

| Aspect | MAPE | WMAPE |
| :--- | :--- | :--- |
| **Weighting** | Equal weights | Weighted by actual value |
| **High-value items** | Same as low-value | More influential |
| **Business interpretation** | Average % error | Total % of total |
| **Aggregation** | Mean of percentages | Ratio of totals |

### Numerical Example

| Actual | Predicted | MAPE Term | WMAPE Contribution |
| :--- | :--- | :--- | :--- |
| 100 | 90 | 10% | Error: 10, Actual: 100 |
| 10 | 5 | 50% | Error: 5, Actual: 10 |
| **MAPE** | **30%** | (10+50)/2 | |
| **WMAPE** | **13.6%** | | 15/110 |

WMAPE gives less weight to the small-volume item with high percentage error.

## Common Use Cases

1. **Retail Demand Planning**: Aggregate accuracy across product portfolio
2. **Revenue Forecasting**: Error weighted by revenue impact
3. **Supply Chain**: Inventory planning where volume matters
4. **Resource Allocation**: Budget forecasting

## Edge Cases

* **Zero Actual Sum**: Returns 0 when total actual is zero (handled via substitution)
* **NaN Handling**: Uses last valid value substitution
* **Single Input**: Not supported (requires two series)
* **Period = 1**: Returns current weighted percentage error
* **All Zero Actuals**: Uses epsilon substitution

## Related Indicators

* [MAPE](../mape/Mape.md) - Mean Absolute Percentage Error (unweighted)
* [MAE](../mae/Mae.md) - Mean Absolute Error (non-percentage)
* [SMAPE](../smape/Smape.md) - Symmetric MAPE
