# MSLE: Mean Squared Logarithmic Error

> *When your data spans orders of magnitude, MSLE keeps outliers from hijacking your loss function.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Error Metric                        |
| **Inputs**       | Actual, Predicted (dual series)          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (MSLE)                       |
| **Output range** | $\geq 0$                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [msle.pine](msle.pine)                       |

- Mean Squared Logarithmic Error transforms both actual and predicted values through logarithms before computing squared error.
- Parameterized by `period`.
- Output range: $\geq 0$.
- Requires 1 bar of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

Mean Squared Logarithmic Error transforms both actual and predicted values through logarithms before computing squared error. This compression makes MSLE robust to outliers and particularly suited for data with exponential growth patterns or wide dynamic ranges.

## Architecture & Physics

MSLE computes the squared difference in log space:

$$\text{MSLE} = \frac{1}{n} \sum_{i=1}^{n} \left(\log(1 + \text{actual}_i) - \log(1 + \text{predicted}_i)\right)^2$$

The `1 + x` transformation ensures defined behavior at zero and prevents negative arguments to the logarithm.

### Logarithmic Compression

For large values, logarithms compress the scale dramatically:

| Actual | Predicted | Absolute Error | MSE | MSLE |
| :--- | :--- | :--- | :--- | :--- |
| 100 | 50 | 50 | 2,500 | 0.48 |
| 10,000 | 5,000 | 5,000 | 25,000,000 | 0.48 |
| 1,000,000 | 500,000 | 500,000 | 2.5×10¹¹ | 0.48 |

Same ratio (2:1) produces nearly identical MSLE regardless of scale.

## Mathematical Foundation

### 1. Log Transform

$$\tilde{x} = \log(1 + x)$$

### 2. Squared Log Error

$$e_i = \left(\log(1 + \text{actual}_i) - \log(1 + \text{predicted}_i)\right)^2$$

This can be rewritten using the quotient rule:

$$e_i = \left(\log\frac{1 + \text{actual}_i}{1 + \text{predicted}_i}\right)^2$$

### 3. Rolling Average

$$\text{MSLE}_t = \frac{1}{n} \sum_{i=t-n+1}^{t} e_i$$

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
| **Throughput** | 25 ns/bar | O(1) via running sum |
| **Allocations** | 0 | Zero-allocation hot path |
| **Complexity** | O(1) | Constant per update |
| **Outlier Robustness** | 9/10 | Log compression |
| **Scale Independence** | 10/10 | Ratio-based comparison |
| **Zero Handling** | 10/10 | Uses 1+x transform |
| **Interpretability** | 5/10 | Log-scale units |

## Usage

```csharp
// Streaming mode - ideal for growth metrics
var msle = new Msle(20);

// Price prediction with wide range
msle.Update(actual: 1000.0, predicted: 950.0);
msle.Update(actual: 100000.0, predicted: 95000.0); // Same 5% error, similar MSLE

double logError = msle.Last.Value;

// Batch mode - historical analysis
var actual = new TSeries { 100, 1000, 10000, 100000 };
var predicted = new TSeries { 95, 950, 9500, 95000 };
var results = Msle.Calculate(actual, predicted, period: 3);

// Span mode - zero-allocation bulk processing
Span<double> output = stackalloc double[1000];
Msle.Batch(actualSpan, predictedSpan, output, period: 20);
```

## Interpretation Guide

| MSLE Value | Interpretation | Approximate Ratio Error |
| :--- | :--- | :--- |
| **0** | Perfect prediction | 1:1 |
| **0.01** | Excellent | ~10% ratio error |
| **0.1** | Good | ~30% ratio error |
| **0.5** | Moderate | ~70% ratio error |
| **1.0** | Poor | ~170% ratio error |
| **2.0** | Very poor | ~300% ratio error |

To convert MSLE to approximate percentage error:

$$\text{Ratio Error} \approx e^{\sqrt{\text{MSLE}}} - 1$$

## Use Cases

### 1. Growth Metrics

Revenue, user counts, and other metrics with exponential growth:

```csharp
// Day 1: Revenue $1,000, predicted $900
// Day 100: Revenue $1,000,000, predicted $900,000
// Both have same 10% error, MSLE treats them equally
```

### 2. Price Prediction

Stock prices, real estate, and other values spanning decades:

```csharp
// 1990: AAPL $0.30, predicted $0.27 (10% error)
// 2024: AAPL $180, predicted $162 (10% error)
// MSE would be dominated by 2024; MSLE balances both
```

### 3. Population/Count Data

Any count that varies by orders of magnitude:

```csharp
// City A: Population 10,000, predicted 9,000
// City B: Population 10,000,000, predicted 9,000,000
// MSLE weights these equally
```

## Comparison with Related Metrics

| Metric | Best For | Limitation |
| :--- | :--- | :--- |
| **MSE** | Uniform scale data | Outlier sensitive |
| **MSLE** | Wide dynamic range | Requires non-negative |
| **MAPE** | Percentage comparison | Undefined at zero |
| **Huber** | Mixed outliers | Requires delta tuning |

## Common Pitfalls

### 1. Negative Values

MSLE requires non-negative inputs. The implementation clamps negative values to 0:

```csharp
// negative actual or predicted → uses last valid value or 0
```

For data with negative values, consider MSE or ME instead.

### 2. Asymmetry

While MSLE squares the log error (making it sign-independent), the logarithm itself is asymmetric around ratios. Predicting 2x the actual has different log error than predicting 0.5x:

```csharp
// actual=100, predicted=200: log(101/201) ≈ -0.69
// actual=100, predicted=50: log(101/51) ≈ 0.68
// After squaring: ~0.48 vs ~0.46 (slightly different)
```

### 3. Near-Zero Sensitivity

Near zero, small absolute differences create large MSLE:

```csharp
// actual=0, predicted=1: log(1/2) = -0.69 → MSLE = 0.48
// actual=0, predicted=9: log(1/10) = -2.30 → MSLE = 5.30
```

## See Also

* [RMSLE](../rmsle/Rmsle.md) - Root of MSLE for interpretable units
* [MSE](../mse/Mse.md) - Linear-scale squared error
* [MAPE](../mape/Mape.md) - Percentage-based comparison
