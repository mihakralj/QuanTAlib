# MASE: Mean Absolute Scaled Error

> "A good forecast is one that's better than guessing. MASE tells you exactly how much better."

Mean Absolute Scaled Error (MASE) normalizes forecast errors by the average error of a naive "random walk" forecast (using the previous value as the prediction). This makes MASE scale-independent and interpretable across different time series.

## Architecture & Physics

MASE computes a ratio: the mean absolute error of your predictions divided by the mean absolute error of a naive forecast. The naive forecast simply predicts that tomorrow's value equals today's value.

### Interpretation Guide

| MASE Value   | Interpretation |
|:-------------|:---------------|
| **MASE < 1** | Forecast is better than naive (good) |
| **MASE = 1** | Forecast equals naive performance |
| **MASE > 1** | Forecast is worse than naive (bad) |
| **MASE = 0** | Perfect forecast |

The naive baseline captures the inherent "forecastability" of the series. A highly volatile series has a larger naive error, making a given absolute error less significant.

## Mathematical Foundation

### 1. Absolute Error

$$e_t = |y_t - \hat{y}_t|$$

### 2. Naive Forecast Scale

$$\text{Scale} = \frac{1}{n-1} \sum_{i=2}^{n} |y_i - y_{i-1}|$$

The scale represents the average absolute change from one period to the next.

### 3. Mean Absolute Scaled Error

$$\text{MASE} = \frac{\frac{1}{n} \sum_{t=1}^{n} |y_t - \hat{y}_t|}{\frac{1}{n-1} \sum_{i=2}^{n} |y_i - y_{i-1}|}$$

Or more simply:

$$\text{MASE} = \frac{\text{MAE}}{\text{Scale}}$$

## Performance Profile

| Metric | Score | Notes |
|:-------|:------|:------|
| **Throughput** | ~35 ns/bar | Dual running sums for error and scale |
| **Allocations** | 0 | Zero-allocation implementation |
| **Complexity** | O(1) | Constant time per update |
| **Accuracy** | 9/10 | Handles edge cases well |
| **Timeliness** | 7/10 | Rolling window introduces lag |
| **Robustness** | 10/10 | Works with zero/negative values |

## Common Pitfalls

### Flat Series Problem

When the actual series is constant (no change between values), the scale becomes zero. The implementation handles this by returning the raw MAE when scale is near zero.

### Initial Warmup

The scale calculation requires at least two values (to compute differences). During warmup, MASE defaults to MAE / 1.0.

### Different from Other Scaled Metrics

Unlike MAPE which scales by actual values, MASE scales by the difficulty of the forecasting problem itself.

## Usage

```csharp
// Create MASE calculator with period 14
var mase = new Mase(14);

// Stream values
var result = mase.Update(actual, predicted);
Console.WriteLine($"MASE: {result.Value:F4}");
// MASE < 1 = better than naive, MASE > 1 = worse than naive

// Batch calculation
var maseSeries = Mase.Calculate(actualSeries, predictedSeries, 14);

// Zero-allocation span version
Mase.Batch(actualSpan, predictedSpan, outputSpan, 14);
```

## Comparison with Other Error Metrics

| Metric | Scale-Independent | Handles Zero | Symmetric | Interpretable |
|:-------|:------------------|:-------------|:----------|:--------------|
| **MASE** | ✅ | ✅ | ✅ | ✅ (vs naive) |
| **MAPE** | ✅ | ❌ | ❌ | ✅ (% error) |
| **SMAPE** | ✅ | ⚠️ | ✅ | ⚠️ (bounded %) |
| **MAE** | ❌ | ✅ | ✅ | ❌ (raw units) |
| **RMSE** | ❌ | ✅ | ✅ | ❌ (raw units) |

MASE is particularly valuable when:

- Comparing forecasts across different series
- Evaluating against a natural baseline (naive forecast)
- Working with data that includes zeros
- Needing symmetric treatment of over/under predictions
