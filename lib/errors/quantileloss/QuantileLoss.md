# Quantile Loss: Pinball Loss Function

> "When over-prediction and under-prediction carry different costs, quantiles find the balance."

Quantile Loss (also called Pinball Loss) measures prediction accuracy with asymmetric penalties for over-prediction versus under-prediction. It's essential for probabilistic forecasting where different quantiles of the distribution matter.

## Historical Context

Quantile Loss emerged from quantile regression, developed by Koenker and Bassett in 1978. Unlike ordinary regression which targets the mean, quantile regression targets specific percentiles of the distribution. The quantile loss function enables this by penalizing errors differently based on their sign and the target quantile.

## Architecture & Physics

The loss function applies a multiplier of τ (tau) to under-predictions and (1-τ) to over-predictions, where τ is the target quantile. For τ=0.5 (median), the loss is symmetric and equals half the absolute error. For τ=0.9, under-predictions are penalized 9x more than over-predictions.

### Properties

* **Asymmetric**: Different penalties for under vs. over prediction
* **Non-negative**: Always ≥ 0, with 0 for perfect prediction
* **Interpretable**: τ directly controls the penalty asymmetry
* **Distribution-free**: No assumptions about error distribution

## Mathematical Foundation

### 1. Quantile Loss Function

For each observation, compute:

$$L_\tau(y, \hat{y}) = \begin{cases}
\tau \cdot (y - \hat{y}) & \text{if } y \geq \hat{y} \text{ (under-prediction)} \\
(1-\tau) \cdot (\hat{y} - y) & \text{if } y < \hat{y} \text{ (over-prediction)}
\end{cases}$$

Or equivalently:

$$L_\tau(y, \hat{y}) = \max(\tau(y - \hat{y}), (\tau - 1)(y - \hat{y}))$$

Where:
* $y$ = actual value
* $\hat{y}$ = predicted value
* $\tau$ = target quantile (0 < τ < 1)

### 2. Mean Quantile Loss

Average the losses over the period:

$$QL = \frac{1}{n} \sum_{i=1}^{n} L_\tau(y_i, \hat{y}_i)$$

### 3. Special Cases

* **τ = 0.5**: Symmetric loss = 0.5 × MAE (equivalent to median regression)
* **τ = 0.9**: 9:1 penalty ratio for under:over prediction
* **τ = 0.1**: 1:9 penalty ratio for under:over prediction

### 4. Running Update (O(1))

QuanTAlib uses a ring buffer with running sum for O(1) updates:

$$S_{new} = S_{old} - L_{oldest} + L_{newest}$$

$$QL = \frac{S_{new}}{n}$$

## Implementation Details

### Usage Patterns

```csharp
// Streaming mode - 90th percentile forecast
var quantileLoss = new QuantileLoss(period: 20, tau: 0.9);
var result = quantileLoss.Update(actualValue, predictedValue);

// Batch mode - calculate for entire series
var results = QuantileLoss.Calculate(actualSeries, predictedSeries, period: 20, tau: 0.9);

// Span mode - zero-allocation for high performance
QuantileLoss.Batch(actualSpan, predictedSpan, outputSpan, period: 20, tau: 0.9);
```

### Parameters

| Parameter | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| **period** | int | - | Lookback window for averaging (must be > 0) |
| **tau** | double | 0.5 | Target quantile (must be in (0, 1)) |

### Properties

| Property | Type | Description |
| :--- | :--- | :--- |
| **Last** | TValue | Most recent Quantile Loss value |
| **IsHot** | bool | True when buffer is full |
| **Tau** | double | Current quantile parameter |
| **Name** | string | Indicator name (e.g., "QuantileLoss(20,0.900)") |
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
| **Flexibility** | 10/10 | Any quantile τ ∈ (0, 1) |

## Interpretation

| Quantile (τ) | Under-Prediction Penalty | Over-Prediction Penalty | Use Case |
| :--- | :--- | :--- | :--- |
| **0.1** | 10% of error | 90% of error | Conservative (avoid over-forecast) |
| **0.5** | 50% of error | 50% of error | Symmetric (median) |
| **0.9** | 90% of error | 10% of error | Safety stock (avoid under-forecast) |
| **0.99** | 99% of error | 1% of error | Extreme upper bound |

## Common Use Cases

1. **Inventory Management**: τ=0.95 for safety stock (stockouts costly)
2. **Energy Forecasting**: Different quantiles for trading vs. reliability
3. **Risk Management**: VaR-style predictions at specific confidence levels
4. **Probabilistic Forecasting**: Evaluate quantile forecast calibration

## Numerical Example

| Actual | Predicted | Error | τ=0.9 Loss | τ=0.1 Loss |
| :--- | :--- | :--- | :--- | :--- |
| 100 | 90 | +10 (under) | 0.9 × 10 = 9.0 | 0.1 × 10 = 1.0 |
| 100 | 110 | -10 (over) | 0.1 × 10 = 1.0 | 0.9 × 10 = 9.0 |

With τ=0.9, under-predictions are penalized 9x more than over-predictions.

## Edge Cases

* **Perfect Predictions**: Returns exactly 0
* **τ = 0 or 1**: Invalid (returns division issues)
* **NaN Handling**: Uses last valid value substitution
* **Single Input**: Not supported (requires two series)
* **Period = 1**: Returns current quantile loss

## Related Indicators

* [MAE](../mae/Mae.md) - Mean Absolute Error (equivalent to τ=0.5 × 2)
* [Huber](../huber/Huber.md) - Huber Loss (robust symmetric)
* [MAPE](../mape/Mape.md) - Mean Absolute Percentage Error
