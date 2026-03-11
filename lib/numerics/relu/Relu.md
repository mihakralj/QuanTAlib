# RELU: Rectified Linear Unit

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Numeric                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | None                      |
| **Outputs**      | Single series (RELU)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `0` bars                          |
| **PineScript**   | [relu.pine](relu.pine)                       |

- The Rectified Linear Unit (ReLU) activation function applies `max(0, x)` to each value, passing positive inputs unchanged while zeroing negative ones.
- No configurable parameters; computation is stateless per bar.
- Output range: Varies (see docs).
- Requires `0` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "The simplest non-linearity that works—ReLU's computational efficiency and gradient-friendly properties made deep learning practical."

The Rectified Linear Unit (ReLU) activation function applies `max(0, x)` to each value, passing positive inputs unchanged while zeroing negative ones. Its simplicity belies its importance: ReLU enabled the training of deep neural networks by mitigating vanishing gradients, and its computational efficiency makes it the default activation for most architectures.

## Mathematical Foundation

### Core Formula

$$
\text{ReLU}(x) = \max(0, x) = \begin{cases} x & \text{if } x > 0 \\ 0 & \text{if } x \leq 0 \end{cases}
$$

### Key Properties

| Property | Formula | Description |
|:---------|:--------|:------------|
| **Non-negativity** | $\text{ReLU}(x) \geq 0$ | Output always ≥ 0 |
| **Identity for Positives** | $\text{ReLU}(x) = x$ for $x > 0$ | Passthrough for positive values |
| **Sparsity Inducing** | $\text{ReLU}(x) = 0$ for $x \leq 0$ | Creates sparse activations |
| **Derivative** | $\frac{d}{dx}\text{ReLU}(x) = \mathbf{1}_{x>0}$ | 1 for positive, 0 for negative |
| **Scale Equivariance** | $\text{ReLU}(\alpha x) = \alpha \cdot \text{ReLU}(x)$ for $\alpha > 0$ | Positive scaling preserved |

### Domain and Range

| | Value |
|:--|:--|
| **Domain** | $(-\infty, +\infty)$ |
| **Range** | $[0, +\infty)$ |

## Financial Applications

### Threshold-Based Signals

Zero out values below a threshold (e.g., only consider positive returns):

$$
\text{PositiveReturns}_t = \text{ReLU}(r_t)
$$

### Asymmetric Risk Metrics

Compute downside deviation using ReLU on negated returns:

$$
\text{Downside}_t = \text{ReLU}(-r_t)
$$

### Clamping Negative Values

Ensure non-negative inputs to subsequent calculations:

$$
\text{Volume}_{\text{clamped}} = \text{ReLU}(\text{Volume} - \text{Threshold})
$$

### Neural Network Features

Pre-processing layer for ML-based trading models where ReLU activation is standard.

## Implementation Details

### SIMD Optimization

The implementation uses AVX2 vectorization when available:
- Processes 4 doubles per instruction using `Avx.Max`
- Falls back to scalar `Math.Max` for remaining elements
- Achieves ~4× throughput improvement on compatible hardware

### NaN Handling

Non-finite inputs (NaN, ±Infinity) are replaced with the last valid output value, maintaining series continuity.

### Streaming Characteristics

| Metric | Value |
|:-------|:------|
| **Warmup Period** | 0 |
| **Memory** | O(1) |
| **Complexity** | O(1) per update |

## Performance Profile

### Operation Count (Scalar)

| Operation | Count | Notes |
|:----------|:-----:|:------|
| CMP | 1 | Comparison with zero |
| MOV | 1 | Conditional move |
| **Total** | ~2-3 cycles | Branch-free with CMOV |

### SIMD Performance (AVX2)

| Mode | Throughput | Notes |
|:-----|:-----------|:------|
| Scalar | 1 value/cycle | Single comparison |
| AVX2 | 4 values/cycle | `vpmaxpd` instruction |
| **Speedup** | ~4× | For aligned batch operations |

### Quality Metrics

| Metric | Score | Notes |
|:-------|:-----:|:------|
| **Accuracy** | 10/10 | Exact computation |
| **Timeliness** | 10/10 | Zero lag |
| **Smoothness** | 7/10 | Discontinuous derivative at origin |

## Usage Examples

### Basic Usage

```csharp
var relu = new Relu();
var input = new TValue(DateTime.UtcNow, -5.0);
var result = relu.Update(input);  // Returns 0.0

input = new TValue(DateTime.UtcNow, 3.5);
result = relu.Update(input);  // Returns 3.5
```

### Filtering Negative Returns

```csharp
var returns = new TSeries();
// ... populate with return values

var relu = new Relu();
var positiveReturns = relu.Update(returns);
// All negative returns become 0
```

### Batch Processing with SIMD

```csharp
double[] source = { -2.0, -1.0, 0.0, 1.0, 2.0, 3.0 };
double[] output = new double[source.Length];

Relu.Calculate(source.AsSpan(), output.AsSpan());
// output: { 0, 0, 0, 1, 2, 3 }
```

## Common Pitfalls

1. **Dead Neurons**: In neural network contexts, neurons with ReLU can "die" if they receive consistently negative inputs during training—they output zero and have zero gradient.

2. **Unbounded Output**: Unlike sigmoid, ReLU has no upper bound. Large positive inputs pass through unchanged, potentially causing numerical issues downstream.

3. **Non-differentiable at Origin**: The derivative is technically undefined at x=0. In practice, implementations choose either 0 or 1; this rarely matters for gradient descent.

4. **Loss of Negative Information**: ReLU discards all information from negative values. If negative values carry meaningful signals, consider alternatives like LeakyReLU or using the raw values.

5. **Not Zero-Centered**: ReLU outputs are always non-negative, which can slow convergence in some optimization scenarios.

## Validation

| Test | Status |
|:-----|:------:|
| **Math.Max(0, x) Parity** | ✅ |
| **Zero Passthrough** | ✅ |
| **Negative → Zero** | ✅ |
| **Positive Passthrough** | ✅ |
| **SIMD/Scalar Consistency** | ✅ |
| **NaN Handling** | ✅ |

## References

- Nair, V. & Hinton, G. (2010). "Rectified Linear Units Improve Restricted Boltzmann Machines." *ICML*.
- Glorot, X., Bordes, A., & Bengio, Y. (2011). "Deep Sparse Rectifier Neural Networks." *AISTATS*.
- Goodfellow, I., Bengio, Y., & Courville, A. (2016). *Deep Learning*. MIT Press.
