# RAE: Relative Absolute Error

> "How much better than just guessing the mean? RAE gives you the ratio."

Relative Absolute Error (RAE) measures the total absolute error of predictions relative to the total absolute error of a simple baseline predictor that always predicts the mean of actual values. This provides a normalized performance metric.

## Architecture & Physics

RAE computes a ratio of summed absolute errors. The numerator is the sum of absolute errors between actual and predicted values. The denominator is the sum of absolute errors between actual values and their mean (the naive mean-predictor baseline).

### Interpretation Guide

| RAE Value | Interpretation |
| ------ | ------ |
| **RAE < 1** | Predictions are better than mean predictor |
| **RAE = 1** | Predictions equal mean predictor performance |
| **RAE > 1** | Predictions are worse than mean predictor |
| **RAE = 0** | Perfect predictions |

The baseline captures how variable the data is. For highly variable data, a larger absolute error is expected from any predictor.

## Mathematical Foundation

### 1. Absolute Error

$$e_t = |y_t - \hat{y}_t|$$

### 2. Baseline Error (vs Mean)

$$b_t = |y_t - \bar{y}|$$

where $\bar{y}$ is the rolling mean of actual values.

### 3. Relative Absolute Error

$$\text{RAE} = \frac{\sum_{t=1}^{n} |y_t - \hat{y}_t|}{\sum_{t=1}^{n} |y_t - \bar{y}|}$$

## Performance Profile

| Metric | Score | Notes |
| ------ | ------ | ------ |
| **Throughput** | ~40 ns/bar | Three running sums maintained |
| **Allocations** | 0 | Zero-allocation implementation |
| **Complexity** | O(1) | Constant time per update |
| **Accuracy** | 9/10 | Clear baseline comparison |
| **Timeliness** | 7/10 | Rolling window introduces lag |
| **Robustness** | 9/10 | Handles edge cases well |

## Common Pitfalls

### Flat Series Problem

When all actual values in the window are identical, the mean equals every value, making the baseline error zero. The implementation returns 1.0 in this case (equivalent to mean predictor performance).

### Rolling Mean Updates

The baseline error is calculated against the rolling mean, which updates each tick. This means historical baseline errors aren't static: they would change if recalculated with the new mean. The implementation stores instantaneous baseline errors for O(1) performance.

### Different from R²

RAE and R² (coefficient of determination) are related but distinct:
<<<<<<< HEAD
=======

>>>>>>> d493bfd42fe5d6238736660aaaa808279cb3a27a
* RAE uses absolute errors (L1 norm)
* R² uses squared errors (L2 norm)
* Both use mean-predictor as baseline

## Usage

```csharp
// Create RAE calculator with period 14
var rae = new Rae(14);

// Stream values
var result = rae.Update(actual, predicted);
Console.WriteLine($"RAE: {result.Value:F4}");
// RAE < 1 = better than mean, RAE > 1 = worse than mean

// Batch calculation
var raeSeries = Rae.Calculate(actualSeries, predictedSeries, 14);

// Zero-allocation span version
Rae.Batch(actualSpan, predictedSpan, outputSpan, 14);
```

## Comparison with Related Metrics

| Metric | Error Type | Baseline | Range | Units |
| ------ | ------ | ------ | ------ | ------ |
| **RAE** | Absolute | Mean predictor | [0, ∞) | Ratio |
| **RSE** | Squared | Mean predictor | [0, ∞) | Ratio |
| **R²** | Squared | Mean predictor | (-∞, 1] | Coefficient |
| **MASE** | Absolute | Naive forecast | [0, ∞) | Ratio |

RAE is preferable when:

* You want robustness to outliers (absolute vs squared errors)
* You need a ratio interpretation (< 1 is good, > 1 is bad)
* The mean predictor is a relevant baseline for your domain
