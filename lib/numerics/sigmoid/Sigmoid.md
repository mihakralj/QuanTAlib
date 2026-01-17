# SIGMOID: Logistic Function

> "The sigmoid function is the S-curve that turns messy reality into neat probabilities—a mathematical diplomat that insists every answer must be between 0 and 1."

The Sigmoid (Logistic) transformer maps any real-valued input to the bounded range (0, 1) using the standard logistic function. Its characteristic S-shaped curve makes it indispensable for probability estimation, neural network activations, and any scenario requiring bounded outputs from unbounded inputs.

## Mathematical Foundation

### Core Formula

$$
S(x) = \frac{1}{1 + e^{-k(x - x_0)}}
$$

where:
- $x$ is the input value
- $k$ is the steepness factor (default 1.0)
- $x_0$ is the midpoint where $S(x_0) = 0.5$ (default 0.0)
- $e \approx 2.71828...$ is Euler's number

### Key Properties

| Property | Formula | Description |
|:---------|:--------|:------------|
| **Midpoint** | $S(x_0) = 0.5$ | Centered at $x_0$ |
| **Symmetry** | $S(x_0 + d) + S(x_0 - d) = 1$ | Point symmetry about $(x_0, 0.5)$ |
| **Limits** | $\lim_{x \to -\infty} S(x) = 0$, $\lim_{x \to +\infty} S(x) = 1$ | Asymptotic bounds |
| **Derivative** | $S'(x) = k \cdot S(x) \cdot (1 - S(x))$ | Self-referential gradient |
| **Monotonicity** | $S'(x) > 0$ for all $x$ | Strictly increasing |
| **Steepness** | Higher $k$ → steeper transition | Controls sensitivity |

### Domain and Range

| | Value |
|:--|:--|
| **Domain** | $(-\infty, +\infty)$ |
| **Range** | $(0, 1)$ exclusive |

The sigmoid accepts any real number and always produces outputs strictly between 0 and 1 (never exactly 0 or 1).

## Financial Applications

### Probability-like Outputs

Convert any signal to a pseudo-probability:

$$
P_{signal} = S(z\text{-score})
$$

where large positive z-scores approach 1, negative approach 0.

### Bounded Confidence Indicators

Transform unbounded oscillators to fixed ranges:

$$
\text{BoundedRSI} = S(k \cdot (\text{RSI} - 50))
$$

### Regime Classification

Soft classification between bullish (1) and bearish (0) regimes:

$$
\text{Regime} = S(k \cdot \text{TrendStrength})
$$

### Position Sizing

Map conviction signals to allocation weights:

$$
\text{Weight} = S(\text{ConvictionScore})
$$

## Parameter Guide

### Steepness ($k$)

| $k$ Value | Behavior | Use Case |
|:----------|:---------|:---------|
| 0.1 | Very gradual | Smooth transitions, noise reduction |
| 0.5 | Gentle | Conservative probability mapping |
| 1.0 | Standard | General purpose (default) |
| 2.0 | Steep | Quick regime detection |
| 5.0+ | Very steep | Near binary classification |

### Midpoint ($x_0$)

| $x_0$ Value | Behavior |
|:------------|:---------|
| 0.0 | Standard (default), symmetric about origin |
| Mean | Centers output around data average |
| Threshold | Custom decision boundary |

## Implementation Details

### Overflow Handling

For extreme inputs, the exponential can overflow:
- When $-k(x - x_0) > 700$: return 0.0 (avoid exp overflow)
- When $-k(x - x_0) < -700$: return 1.0 (exp underflows to 0)

### Precision Considerations

| Input Range | Output Precision |
|:------------|:-----------------|
| $|k(x-x_0)| < 20$ | Full 15-16 digits |
| $|k(x-x_0)| > 36$ | Saturates to 0 or 1 within double precision |

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
| SUB | 1 | $x - x_0$ |
| MUL | 1 | $k \times (x - x_0)$ |
| NEG | 1 | Negate for exp |
| EXP | 1 | Hardware instruction |
| ADD | 1 | $1 + \exp(...)$ |
| DIV | 1 | Final division |
| **Total** | ~25-30 cycles | Dominated by EXP |

### Quality Metrics

| Metric | Score | Notes |
|:-------|:-----:|:------|
| **Accuracy** | 10/10 | IEEE 754 compliant |
| **Timeliness** | 10/10 | Zero lag |
| **Smoothness** | 10/10 | Infinitely differentiable |
| **Boundedness** | 10/10 | Guaranteed (0, 1) output |

## Usage Examples

### Basic Usage

```csharp
// Create Sigmoid with default parameters
var sigmoid = new Sigmoid();

// Transform z-score to probability-like value
var zscore = new TValue(DateTime.UtcNow, 2.0);
var probability = sigmoid.Update(zscore);  // ≈ 0.881
```

### Custom Steepness

```csharp
// Steep sigmoid for quick transitions
var steepSigmoid = new Sigmoid(k: 3.0);

var x = new TValue(DateTime.UtcNow, 1.0);
var result = steepSigmoid.Update(x);  // ≈ 0.953 (steeper than default 0.731)
```

### Custom Midpoint

```csharp
// Center sigmoid at RSI neutral level (50)
var rsiSigmoid = new Sigmoid(k: 0.1, x0: 50);

var rsiValue = new TValue(DateTime.UtcNow, 70);
var bullishProbability = rsiSigmoid.Update(rsiValue);  // ≈ 0.881
```

### Span API for Batch Processing

```csharp
double[] inputs = { -2, -1, 0, 1, 2 };
double[] outputs = new double[inputs.Length];

Sigmoid.Calculate(inputs, outputs, k: 1.0, x0: 0.0);
// outputs ≈ { 0.119, 0.269, 0.500, 0.731, 0.881 }
```

## Common Pitfalls

1. **Not Exactly 0 or 1**: Sigmoid asymptotically approaches but never reaches 0 or 1. If you need exact binary outputs, apply a threshold post-sigmoid.

2. **Vanishing Gradients**: For very large or small inputs, $S'(x) \approx 0$. This is a feature for boundedness but can cause issues if the sigmoid is part of a learning system.

3. **Scale Sensitivity**: The default $k=1$ assumes inputs are roughly in the range $[-5, 5]$. For inputs with different scales, adjust $k$ or normalize inputs first.

4. **Midpoint Confusion**: Remember $x_0$ shifts where 0.5 occurs, not where 0 occurs. Sigmoid never outputs exactly 0.

5. **Symmetry Assumption**: Sigmoid imposes symmetric transition behavior. For asymmetric responses, consider other activation functions.

## Validation

| Test | Status |
|:-----|:------:|
| **Midpoint S(x₀) = 0.5** | ✅ |
| **Symmetry Property** | ✅ |
| **Range (0, 1)** | ✅ |
| **Monotonicity** | ✅ |
| **Steepness Effect** | ✅ |
| **Limit Behavior** | ✅ |
| **Overflow Guards** | ✅ |

## References

- Verhulst, P.-F. (1838). "Notice sur la loi que la population suit dans son accroissement." *Correspondance Mathématique et Physique*.
- Rumelhart, D., Hinton, G., & Williams, R. (1986). "Learning representations by back-propagating errors." *Nature*.
- Bishop, C. (2006). *Pattern Recognition and Machine Learning*. Springer.
