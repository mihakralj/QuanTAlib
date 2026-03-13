# RMSLE: Root Mean Squared Logarithmic Error

> *RMSLE: because sometimes your errors need to be measured in decades, not dollars.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Error Metric                        |
| **Inputs**       | Actual vs Predicted (dual input)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (RMSLE)                       |
| **Output range** | $\geq 0$                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [rmsle.pine](rmsle.pine)                       |

- Root Mean Squared Logarithmic Error is the square root of MSLE, providing an error metric in log-scale units.
- **Similar:** [MSLE](../msle/Msle.md), [RMSE](../rmse/Rmse.md) | **Trading note:** Root Mean Squared Log Error; measures relative error magnitude. Useful for price ratios.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

Root Mean Squared Logarithmic Error is the square root of MSLE, providing an error metric in log-scale units. This makes RMSLE more interpretable than MSLE while retaining all its benefits for data spanning multiple orders of magnitude.

## Architecture & Physics

RMSLE computes the root mean of squared log differences:

$$\text{RMSLE} = \sqrt{\frac{1}{n} \sum_{i=1}^{n} \left(\log(1 + \text{actual}_i) - \log(1 + \text{predicted}_i)\right)^2}$$

The relationship to MSLE is straightforward:

$$\text{RMSLE} = \sqrt{\text{MSLE}}$$

### Interpretability

RMSLE values correspond directly to log-scale error:

* RMSLE = 0.1 → approximately 10% ratio error
* RMSLE = 0.69 → approximately 100% ratio error (2:1 or 1:2 ratio)
* RMSLE = 1.0 → approximately 170% ratio error (~2.7:1 ratio)

## Mathematical Foundation

### 1. Log Transform

$$\tilde{x} = \log(1 + x)$$

### 2. Root Mean Square in Log Space

$$\text{RMSLE} = \sqrt{\frac{1}{n} \sum_{i=t-n+1}^{t} \left(\tilde{\text{actual}}_i - \tilde{\text{predicted}}_i\right)^2}$$

### 3. Approximation for Small Errors

For small relative errors ($\epsilon$):

$$\text{RMSLE} \approx |\log(1 + \epsilon)| \approx |\epsilon|$$

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
| **Throughput** | 28 ns/bar | O(1) with sqrt overhead |
| **Allocations** | 0 | Zero-allocation hot path |
| **Complexity** | O(1) | Constant per update |
| **Outlier Robustness** | 9/10 | Log compression |
| **Interpretability** | 7/10 | Better than MSLE |
| **Scale Independence** | 10/10 | Ratio-based |
| **Zero Handling** | 10/10 | Uses 1+x transform |

## Usage

```csharp
// Streaming mode - track prediction quality
var rmsle = new Rmsle(20);

// Revenue predictions across different scales
rmsle.Update(actual: 1000.0, predicted: 950.0);     // Small business
rmsle.Update(actual: 1000000.0, predicted: 950000.0); // Enterprise

double logError = rmsle.Last.Value;
Console.WriteLine($"RMSLE: {logError:F3}"); // Consistent ~0.05 for 5% error

// Batch mode - backtest analysis
var actual = new TSeries { 100, 1000, 10000, 100000 };
var predicted = new TSeries { 95, 950, 9500, 95000 };
var results = Rmsle.Calculate(actual, predicted, period: 3);

// Span mode - zero-allocation bulk processing
Span<double> output = stackalloc double[1000];
Rmsle.Batch(actualSpan, predictedSpan, output, period: 20);
```

## Interpretation Guide

| RMSLE Value | Interpretation | Typical Application |
| :--- | :--- | :--- |
| **< 0.1** | Excellent | High-precision forecasting |
| **0.1 - 0.3** | Good | Business forecasting |
| **0.3 - 0.5** | Moderate | General ML models |
| **0.5 - 1.0** | Poor | Needs improvement |
| **> 1.0** | Very poor | Model redesign needed |

### Converting RMSLE to Ratio Error

$$\text{Typical Ratio} \approx e^{\text{RMSLE}}$$

| RMSLE | Ratio Factor | Meaning |
| :--- | :--- | :--- |
| 0.1 | 1.105 | Predictions typically within ±10.5% |
| 0.2 | 1.221 | Predictions typically within ±22% |
| 0.5 | 1.649 | Predictions typically within ±65% |
| 0.693 | 2.0 | Predictions off by factor of 2 |
| 1.0 | 2.718 | Predictions off by factor of e |

## Comparison: RMSE vs RMSLE

```csharp
var rmse = new Rmse(1);
var rmsle = new Rmsle(1);

// Small scale
rmse.Update(100.0, 50.0);     // RMSE = 50
rmsle.Update(100.0, 50.0);    // RMSLE ≈ 0.69

// Large scale (same ratio)
rmse.Update(1000000.0, 500000.0);   // RMSE = 500,000
rmsle.Update(1000000.0, 500000.0);  // RMSLE ≈ 0.69

// RMSE varies wildly; RMSLE is consistent for same ratio
```

## Use Cases

### 1. E-Commerce Sales Forecasting

Product sales vary from single units to thousands:

```csharp
// Product A: sells 5 units, predicted 4
// Product B: sells 5000 units, predicted 4000
// Same 20% under-prediction, similar RMSLE
```

### 2. Financial Modeling

Stock prices, market caps, and volumes span many magnitudes:

```csharp
// Penny stock: $0.10 → $0.12 (20% move)
// Blue chip: $100 → $120 (20% move)
// RMSLE treats these equivalently
```

### 3. Scientific Measurements

Population counts, concentrations, or any log-normal data:

```csharp
// Bacteria count: 1,000 → 1,200
// Bacteria count: 1,000,000,000 → 1,200,000,000
// Same relative accuracy
```

## Common Pitfalls

### 1. Non-Negative Requirement

RMSLE requires both actual and predicted values to be non-negative:

```csharp
// Invalid inputs are replaced with last valid value or 0
rmsle.Update(-100.0, 50.0);  // Uses last valid actual
```

### 2. Unit Interpretation

RMSLE is in "log units," not the original units:

```csharp
// RMSLE = 0.5 does NOT mean $0.50 error
// It means predictions are typically off by ~65% ratio
```

### 3. Near-Zero Sensitivity

Small absolute values near zero can produce large RMSLE:

```csharp
// actual=1, predicted=10: RMSLE = |log(2) - log(11)| ≈ 1.7
// actual=1000, predicted=10000: RMSLE = |log(1001) - log(10001)| ≈ 2.3
// Not exactly proportional due to 1+x offset
```

## Relationship to Other Metrics

| Metric | Relationship |
| :--- | :--- |
| **MSLE** | RMSLE = √MSLE |
| **RMSE** | Different scale sensitivity |
| **MAPE** | Both percentage-like, but RMSLE handles zeros |
| **MAE** | RMSLE is log-transformed, squared, then rooted |

## See Also

* [MSLE](../msle/Msle.md) - Squared version without root
* [RMSE](../rmse/Rmse.md) - Linear-scale root mean squared error
* [MAPE](../mape/Mape.md) - Percentage error without log transform