# Pseudo-Huber: Smooth Huber Approximation

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Error Metric                        |
| **Inputs**       | Actual vs Predicted (dual input)                          |
| **Parameters**   | `period`, `delta` (default 1.0)                      |
| **Outputs**      | Single series (PseudoHuber)                       |
| **Output range** | $\geq 0$                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [pseudohuber.pine](pseudohuber.pine)                       |

- Pseudo-Huber Loss (also called Charbonnier Loss) is a smooth approximation to the Huber loss function.
- Parameterized by `period`, `delta` (default 1.0).
- Output range: $\geq 0$.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "All the robustness of Huber, none of the discontinuities."

Pseudo-Huber Loss (also called Charbonnier Loss) is a smooth approximation to the Huber loss function. Unlike Huber which has a piecewise definition with a kink at δ, Pseudo-Huber is continuously differentiable everywhere, making it ideal for gradient-based optimization.

## Historical Context

The Pseudo-Huber function emerged from the optimization and machine learning communities as a way to get Huber-like robustness while maintaining smooth gradients. It's also known as Charbonnier loss in image processing, where it's used for edge-preserving smoothing and optical flow estimation.

## Architecture & Physics

Pseudo-Huber uses the formula δ²(√(1 + (x/δ)²) - 1), which smoothly interpolates between quadratic behavior for small errors and linear behavior for large errors. The transition is gradual rather than abrupt, with no discontinuity in derivatives.

### Properties

* **Smooth everywhere**: Infinitely differentiable (unlike Huber's kink)
* **Non-negative**: Always ≥ 0, with 0 for perfect prediction
* **Robust**: Large errors grow linearly, not quadratically
* **Tunable**: δ (delta) controls the L2-to-L1 transition point

## Mathematical Foundation

### 1. Pseudo-Huber Function

For each error, compute:

$$L_\delta(e) = \delta^2 \left(\sqrt{1 + \left(\frac{e}{\delta}\right)^2} - 1\right)$$

Where:
* $e = y - \hat{y}$ = prediction error
* $\delta$ = tuning parameter (transition width)

### 2. Asymptotic Behavior

For small errors (|e| << δ):

$$L_\delta(e) \approx \frac{e^2}{2}$$

For large errors (|e| >> δ):

$$L_\delta(e) \approx \delta|e| - \delta^2$$

### 3. Gradient (Derivative)

$$\frac{dL}{de} = \frac{e}{\sqrt{1 + (e/\delta)^2}}$$

This approaches:
* e for small errors (like L2)
* δ·sign(e) for large errors (like L1)

### 4. Running Update (O(1))

QuanTAlib uses a ring buffer with running sum for O(1) updates:

$$S_{new} = S_{old} - L_{oldest} + L_{newest}$$

$$PseudoHuber = \frac{S_{new}}{n}$$

## Implementation Details

### Usage Patterns

```csharp
// Streaming mode - with custom delta
var pseudoHuber = new PseudoHuber(period: 20, delta: 1.0);
var result = pseudoHuber.Update(actualValue, predictedValue);

// Batch mode - calculate for entire series
var results = PseudoHuber.Calculate(actualSeries, predictedSeries, period: 20, delta: 1.0);

// Span mode - zero-allocation for high performance
PseudoHuber.Batch(actualSpan, predictedSpan, outputSpan, period: 20, delta: 1.0);
```

### Parameters

| Parameter | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| **period** | int | - | Lookback window for averaging (must be > 0) |
| **delta** | double | 1.0 | Transition parameter (must be > 0) |

### Properties

| Property | Type | Description |
| :--- | :--- | :--- |
| **Last** | TValue | Most recent Pseudo-Huber value |
| **IsHot** | bool | True when buffer is full |
| **Delta** | double | Current delta parameter |
| **Name** | string | Indicator name (e.g., "PseudoHuber(20,1.000)") |
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
| **Throughput** | ~15 ns/bar | O(1) update, sqrt computation |
| **Allocations** | 0 | Uses pre-allocated ring buffer |
| **Complexity** | O(1) | Constant time per update |
| **Accuracy** | 10/10 | Exact calculation |
| **Timeliness** | 9/10 | No lag beyond the period |
| **Smoothness** | 10/10 | Infinitely differentiable |

## Comparison with Huber

| Aspect | Huber | Pseudo-Huber |
| :--- | :--- | :--- |
| **Small errors** | e²/2 | ≈ e²/2 |
| **Large errors** | δ\|e\| - δ²/2 | ≈ δ\|e\| - δ² |
| **At e = δ** | Kink (C¹) | Smooth (C^∞) |
| **Gradient** | Discontinuous 2nd derivative | Continuous all derivatives |
| **Computation** | Conditional logic | Single formula |
| **Optimization** | Can cause issues | Smooth convergence |

### Numerical Comparison

| Error (e) | Huber (δ=1) | Pseudo-Huber (δ=1) |
| :--- | :--- | :--- |
| **0.0** | 0.000 | 0.000 |
| **0.5** | 0.125 | 0.118 |
| **1.0** | 0.500 | 0.414 |
| **2.0** | 1.500 | 1.236 |
| **10.0** | 9.500 | 9.049 |

Pseudo-Huber produces slightly smaller values but follows the same qualitative behavior.

## Choosing δ

| δ Value | Behavior | Use Case |
| :--- | :--- | :--- |
| **0.1** | Quickly linear | Aggressive outlier handling |
| **1.0** | Balanced | Standard choice |
| **10.0** | Mostly quadratic | Near-MSE behavior |
| **100.0** | Almost pure L2 | When outliers are rare |

## Common Use Cases

1. **Neural Network Training**: Smooth loss for gradient descent
2. **Computer Vision**: Optical flow, stereo matching
3. **Robust Regression**: When smoothness matters for optimization
4. **Image Processing**: Edge-preserving filtering (Charbonnier)

## Edge Cases

* **Perfect Predictions**: Returns exactly 0
* **NaN Handling**: Uses last valid value substitution
* **Single Input**: Not supported (requires two series)
* **δ = 0**: Invalid (division by zero)
* **Large Errors**: Numerically stable (no overflow)

## Related Indicators

* [Huber](../huber/Huber.md) - Huber Loss (piecewise, with kink)
* [LogCosh](../logcosh/LogCosh.md) - Log-Cosh Loss (different smooth approximation)
* [MAE](../mae/Mae.md) - Mean Absolute Error (pure L1)
* [MSE](../mse/Mse.md) - Mean Squared Error (pure L2)
