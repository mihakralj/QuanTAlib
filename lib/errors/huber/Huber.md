# Huber: Huber Loss

> "The Goldilocks of loss functions: not too sensitive, not too robust, just right."

Huber Loss is a hybrid loss function that combines the best properties of Mean Squared Error (MSE) and Mean Absolute Error (MAE). For small errors, it behaves quadratically like MSE; for large errors, it behaves linearly like MAE.

## Historical Context

Introduced by Peter J. Huber in 1964 as part of robust statistics, Huber Loss was designed to be less sensitive to outliers than squared error while maintaining the nice mathematical properties of quadratic loss for small errors. The default delta value of 1.345 provides 95% asymptotic efficiency for normally distributed data.

## Architecture & Physics

Huber Loss uses a threshold parameter (delta) to switch between quadratic and linear behavior:

* **Small errors (|e| ≤ δ)**: Quadratic penalty, like MSE
* **Large errors (|e| > δ)**: Linear penalty, like MAE

This makes it differentiable everywhere (unlike MAE) while being robust to outliers (unlike MSE).

### Properties

* **Non-negative**: Huber ≥ 0, with 0 indicating perfect prediction
* **Differentiable**: Smooth at the transition point (unlike MAE)
* **Robust**: Less sensitive to outliers than MSE
* **Configurable**: Delta controls the transition between quadratic and linear

## Mathematical Foundation

### 1. Huber Loss Function

For each error $e = y - \hat{y}$:

$$L_{\delta}(e) = \begin{cases} \frac{1}{2}e^2 & \text{if } |e| \leq \delta \\ \delta|e| - \frac{1}{2}\delta^2 & \text{if } |e| > \delta \end{cases}$$

Where:

* $y$ = actual value
* $\hat{y}$ = predicted value
* $\delta$ = threshold parameter (default: 1.345)

### 2. Mean Huber Loss

Average the individual losses over the period:

$$\text{Huber} = \frac{1}{n} \sum_{i=1}^{n} L_{\delta}(e_i)$$

### 3. Running Update (O(1))

QuanTAlib uses a ring buffer with running sum for O(1) updates:

$$S_{new} = S_{old} - L_{oldest} + L_{newest}$$

$$\text{Huber} = \frac{S_{new}}{n}$$

## Implementation Details

### Usage Patterns

```csharp
// Streaming mode - update with each new observation
var huber = new Huber(period: 20, delta: 1.345);
var result = huber.Update(actualValue, predictedValue);

// Batch mode - calculate for entire series
var results = Huber.Calculate(actualSeries, predictedSeries, period: 20, delta: 1.345);

// Span mode - zero-allocation for high performance
Huber.Batch(actualSpan, predictedSpan, outputSpan, period: 20, delta: 1.345);
```

### Parameters

| Parameter | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| **period** | int | - | Lookback window for averaging (must be > 0) |
| **delta** | double | 1.345 | Threshold for quadratic/linear transition |

### Properties

| Property | Type | Description |
| :--- | :--- | :--- |
| **Last** | TValue | Most recent Huber Loss value |
| **IsHot** | bool | True when buffer is full |
| **Name** | string | Indicator name (e.g., "Huber(20,1.345)") |
| **WarmupPeriod** | int | Number of periods before valid output |

## Performance Profile

### Operation Count (Streaming Mode)

Huber loss: L = 0.5*e^2 if |e|<=delta, else delta*(|e| - 0.5*delta). Conditional on residual vs threshold; two paths.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Residual e = actual - forecast | 1 | ~2 cy | ~2 cy |
| Absolute value + comparison vs delta | 1 | ~3 cy | ~3 cy |
| Quadratic path: 0.5*e^2 | 1 | ~4 cy | ~4 cy |
| Linear path: delta*(|e| - 0.5*delta) | 2 | ~4 cy | ~8 cy |
| Running accumulator update | 1 | ~4 cy | ~4 cy |
| **Total** | **~5** | — | **~15 cycles** |

O(1) per bar. Branch prediction favors the quadratic path for small errors. ~15 cycles/bar.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| Residual computation | Yes | Element-wise subtract |
| Conditional Huber selection | Yes | Branchless via SIMD blend/mask |
| Accumulation | Yes | Parallel reduction |

Branchless SIMD implementation eliminates branch mispredictions. ~4 cy/bar in batch mode.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | ~12 ns/bar | O(1) update complexity |
| **Allocations** | 0 | Uses pre-allocated ring buffer |
| **Complexity** | O(1) | Constant time per update |
| **Accuracy** | 10/10 | Exact calculation |
| **Timeliness** | 9/10 | No lag beyond the period |
| **Smoothness** | 8/10 | Good smoothing properties |

## Delta Selection Guide

| Delta Value | Behavior | Use Case |
| :--- | :--- | :--- |
| **Small (< 1)** | More like MAE | Heavy outlier presence |
| **1.345** | 95% efficiency | General purpose (default) |
| **Large (> 5)** | More like MSE | Few outliers expected |

## Comparison with Other Metrics

| Metric | Outlier Sensitivity | Differentiable | Behavior |
| :--- | :--- | :--- | :--- |
| **Huber** | Medium | Yes | Hybrid quadratic/linear |
| **MAE** | Low | No | Always linear |
| **MSE** | High | Yes | Always quadratic |
| **RMSE** | High | Yes | Quadratic (same units) |

## Common Use Cases

1. **Robust Regression**: Training models with some outliers
2. **Financial Forecasting**: When extreme values occur occasionally
3. **Signal Processing**: Noise reduction with outlier tolerance
4. **Machine Learning**: Loss function for neural networks

## Behavior Examples

```csharp
// Small error (quadratic region)
// Error = 0.5, delta = 1.345
// Huber = 0.5 * 0.5² = 0.125
var huber = new Huber(1, 1.345);
huber.Update(100, 99.5); // Returns 0.125

// Large error (linear region)
// Error = 10, delta = 1.345
// Huber = 1.345 * 10 - 0.5 * 1.345² = 13.45 - 0.904 = 12.546
huber.Reset();
huber.Update(110, 100); // Returns ~12.546
```

## Edge Cases

* **Identical Values**: Returns 0 when actual equals predicted
* **NaN Handling**: Uses last valid value substitution
* **Single Input**: Not supported (requires two series)
* **Period = 1**: Returns current Huber loss
* **Error at delta**: Uses quadratic formula (continuous transition)

## Related Indicators

* [MAE](../mae/Mae.md) - Mean Absolute Error (linear everywhere)
* [MSE](../mse/Mse.md) - Mean Squared Error (quadratic everywhere)
* [RMSE](../rmse/Rmse.md) - Root Mean Squared Error
