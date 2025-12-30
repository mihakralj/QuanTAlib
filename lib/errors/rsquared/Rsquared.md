# R²: Coefficient of Determination

> "R² tells you how much of the variance in actual values is explained by your predictions. It's the statistician's favorite metric for good reason."

The Coefficient of Determination (R²) measures the proportion of variance in the actual values that is predictable from the predicted values. R² ranges from negative infinity to 1, where 1 indicates perfect predictions.

## Architecture & Physics

R² is computed as 1 minus the ratio of residual sum of squares (RSS) to total sum of squares (TSS). This is mathematically equivalent to R² = 1 - RSE, making R² the complement of Relative Squared Error.

### Interpretation Guide

| R² Value | Interpretation |
|:---------|:---------------|
| **R² = 1** | Perfect predictions (all variance explained) |
| **R² > 0.9** | Excellent model |
| **R² > 0.7** | Good model |
| **R² > 0.5** | Moderate model |
| **R² = 0** | Model is no better than predicting the mean |
| **R² < 0** | Model is worse than predicting the mean |

## Mathematical Foundation

### 1. Residual Sum of Squares (RSS)

$$\text{RSS} = \sum_{t=1}^{n} (y_t - \hat{y}_t)^2$$

### 2. Total Sum of Squares (TSS)

$$\text{TSS} = \sum_{t=1}^{n} (y_t - \bar{y})^2$$

where $\bar{y}$ is the rolling mean of actual values.

### 3. Coefficient of Determination

$$R^2 = 1 - \frac{\text{RSS}}{\text{TSS}} = 1 - \frac{\sum_{t=1}^{n} (y_t - \hat{y}_t)^2}{\sum_{t=1}^{n} (y_t - \bar{y})^2}$$

### 4. Relationship to RSE

$$R^2 = 1 - \text{RSE}$$

## Performance Profile

| Metric | Score | Notes |
|:-------|:------|:------|
| **Throughput** | ~40 ns/bar | Three running sums maintained |
| **Allocations** | 0 | Zero-allocation implementation |
| **Complexity** | O(1) | Constant time per update |
| **Accuracy** | 10/10 | Standard statistical measure |
| **Timeliness** | 7/10 | Rolling window introduces lag |
| **Sensitivity** | 8/10 | Sensitive to outliers (squared errors) |

## Common Pitfalls

### Flat Series Problem

When all actual values in the window are identical, TSS becomes zero (all values equal the mean). The implementation returns 0.0 in this case, indicating no variance to explain.

### Negative R² Values

R² can be negative when predictions are worse than simply predicting the mean. This indicates a fundamentally flawed model that should not be used.

### R² ≠ Correlation Squared (in general)

While R² equals the square of Pearson correlation for simple linear regression, this relationship does not hold for general predictions. R² can be negative; correlation squared cannot.

### High R² Doesn't Mean Good Predictions

R² measures relative fit, not absolute accuracy. A model with R² = 0.99 could still have large absolute errors if the data has high variance.

## Usage

```csharp
// Create R² calculator with period 14
var rsquared = new Rsquared(14);

// Stream values
var result = rsquared.Update(actual, predicted);
Console.WriteLine($"R²: {result.Value:F4}");
// R² > 0 = better than mean, R² = 1 = perfect

// Batch calculation
var r2Series = Rsquared.Calculate(actualSeries, predictedSeries, 14);

// Zero-allocation span version
Rsquared.Batch(actualSpan, predictedSpan, outputSpan, 14);
```

## R² Quick Reference

| R² Value | Quality | Description |
|:---------|:--------|:------------|
| 1.00 | Perfect | Model explains all variance |
| 0.95 | Excellent | Model explains 95% of variance |
| 0.80 | Good | Model explains 80% of variance |
| 0.50 | Moderate | Model explains 50% of variance |
| 0.00 | Poor | Model is no better than mean |
| -0.50 | Useless | Model is worse than mean |

## Comparison with RSE

| Property | R² | RSE |
|:---------|:---|:----|
| **Range** | (-∞, 1] | [0, +∞) |
| **Perfect score** | 1 | 0 |
| **Mean predictor** | 0 | 1 |
| **Interpretation** | Variance explained | Error ratio |
| **Relationship** | R² = 1 - RSE | RSE = 1 - R² |

## When to Use R²

- **Use R²** when you want an intuitive measure of model quality (0-1 scale for good models)
- **Use RSE** when you want to compare error magnitudes directly
- **Use both** to get complementary perspectives on model performance
