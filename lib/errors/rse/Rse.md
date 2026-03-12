# RSE: Relative Squared Error

> *The squared error version of RAE. RSE and R² are two sides of the same coin: R² = 1 - RSE.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Error Metric                        |
| **Inputs**       | Actual vs Predicted (dual input)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Rse)                       |
| **Output range** | $\geq 0$                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [rse.pine](rse.pine)                       |

- Relative Squared Error (RSE) measures the total squared error of predictions relative to the total squared error of a simple baseline predictor tha...
- Parameterized by `period`.
- Output range: $\geq 0$.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

Relative Squared Error (RSE) measures the total squared error of predictions relative to the total squared error of a simple baseline predictor that always predicts the mean. RSE is directly related to the coefficient of determination (R²).

## Architecture & Physics

RSE computes a ratio of summed squared errors. The numerator is the residual sum of squares (RSS). The denominator is the total sum of squares (TSS). The relationship R² = 1 - RSE provides a direct conversion between the two metrics.

### Interpretation Guide

| RSE Value | R² Value | Interpretation |
| :-------- | :------- | :------------- |
| **RSE = 0** | **R² = 1** | Perfect predictions |
| **RSE < 1** | **R² > 0** | Better than mean predictor |
| **RSE = 1** | **R² = 0** | Same as mean predictor |
| **RSE > 1** | **R² < 0** | Worse than mean predictor |

Squared errors penalize large errors more heavily than small ones, making RSE more sensitive to outliers than RAE.

## Mathematical Foundation

### 1. Squared Error (RSS)

$$e_t^2 = (y_t - \hat{y}_t)^2$$

### 2. Squared Baseline Error (TSS)

$$b_t^2 = (y_t - \bar{y})^2$$

where $\bar{y}$ is the rolling mean of actual values.

### 3. Relative Squared Error

$$\text{RSE} = \frac{\sum_{t=1}^{n} (y_t - \hat{y}_t)^2}{\sum_{t=1}^{n} (y_t - \bar{y})^2} = \frac{\text{RSS}}{\text{TSS}}$$

### 4. Relationship to R²

$$R^2 = 1 - \text{RSE}$$

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
| :----- | :---- | :---- |
| **Throughput** | ~40 ns/bar | Three running sums maintained |
| **Allocations** | 0 | Zero-allocation implementation |
| **Complexity** | O(1) | Constant time per update |
| **Accuracy** | 9/10 | Standard statistical measure |
| **Timeliness** | 7/10 | Rolling window introduces lag |
| **Sensitivity** | 8/10 | Sensitive to outliers (squared errors) |

## Common Pitfalls

### Flat Series Problem

When all actual values in the window are identical, TSS becomes zero (all values equal the mean). The implementation returns 1.0 in this case.

### Outlier Sensitivity

Because errors are squared, a single large error can dominate the RSE calculation. For outlier-robust alternatives, consider RAE (which uses absolute errors).

### Negative R² is Possible

When RSE > 1, the implied R² is negative. This indicates predictions are worse than simply predicting the mean: a sign of a fundamentally flawed model.

## Usage

```csharp
// Create RSE calculator with period 14
var rse = new Rse(14);

// Stream values
var result = rse.Update(actual, predicted);
Console.WriteLine($"RSE: {result.Value:F4}");
Console.WriteLine($"Implied R²: {1 - result.Value:F4}");
// RSE < 1 = better than mean, R² > 0

// Batch calculation
var rseSeries = Rse.Calculate(actualSeries, predictedSeries, 14);

// Zero-allocation span version
Rse.Batch(actualSpan, predictedSpan, outputSpan, 14);
```

## RSE vs R² Quick Reference

| Scenario | RSE | R² | Quality |
| :------- | :-- | :- | :------ |
| Perfect model | 0.00 | 1.00 | Excellent |
| Very good model | 0.05 | 0.95 | Very good |
| Good model | 0.20 | 0.80 | Good |
| Moderate model | 0.50 | 0.50 | Moderate |
| Poor model (= mean) | 1.00 | 0.00 | Poor |
| Useless model | 2.00 | -1.00 | Useless |

## Comparison with RAE

| Property | RSE | RAE |
| :------- | :-- | :-- |
| **Error type** | Squared (L2) | Absolute (L1) |
| **Outlier sensitivity** | High | Low |
| **Related to** | R² | — |
| **Baseline** | Mean predictor | Mean predictor |
| **Interpretation** | 1 - R² | Better/worse than mean |
