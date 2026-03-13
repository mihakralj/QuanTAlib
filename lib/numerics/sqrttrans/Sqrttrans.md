# SQRTTRANS: Square Root Transform

> *The square root is nature's variance-stabilizing trick—halving the exponent space while preserving monotonicity. When price volatility scales with level, sqrt compresses the noise.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Numeric                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | None                      |
| **Outputs**      | Single series (SQRTTRANS)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `0` bars                          |
| **PineScript**   | [sqrttrans.pine](sqrttrans.pine)                       |

- The Square Root (SQRT) transformer applies $\sqrt{x}$ to each value in a time series.
- No configurable parameters; computation is stateless per bar.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The Square Root (SQRT) transformer applies $\sqrt{x}$ to each value in a time series. This variance-stabilizing transformation compresses ranges where volatility scales with magnitude, making it useful for heteroscedastic data where standard deviation increases with price level.

## Mathematical Foundation

### Core Formula

$$
\text{SQRT}_t = \sqrt{x_t}
$$

where:
- $x_t$ is the input value at time $t$
- $x_t \geq 0$ (domain restriction)

### Key Properties

| Property | Formula | Description |
|:---------|:--------|:------------|
| **Domain** | $x \geq 0$ | Only non-negative inputs valid |
| **Range** | $y \geq 0$ | Output always non-negative |
| **Product Rule** | $\sqrt{ab} = \sqrt{a} \cdot \sqrt{b}$ | Factors separate under sqrt |
| **Quotient Rule** | $\sqrt{a/b} = \sqrt{a} / \sqrt{b}$ | Division becomes ratio of roots |
| **Power Relation** | $\sqrt{x} = x^{0.5}$ | Half-power equivalence |
| **Inverse** | $(\sqrt{x})^2 = x$ | Squaring reverses sqrt |
| **Identity** | $\sqrt{0} = 0$, $\sqrt{1} = 1$ | Fixed points |

### Derivative

$$
\frac{d}{dx}\sqrt{x} = \frac{1}{2\sqrt{x}}
$$

The derivative approaches infinity as $x \to 0^+$, meaning small changes near zero produce large output changes.

## Financial Applications

### Variance Stabilization

For data where standard deviation scales with the mean (Poisson-like behavior), sqrt transformation normalizes variance:

$$
\text{Var}(\sqrt{X}) \approx \text{constant}
$$

This enables statistical techniques that assume homoscedasticity.

### Volatility Scaling

When volatility is proportional to price level:

$$
\sigma_{price} \propto P \implies \sigma_{\sqrt{P}} \approx \text{constant}
$$

The sqrt transformation can normalize volatility for cross-asset comparison.

### Distance Metrics

Euclidean distance in feature space:

$$
d = \sqrt{\sum_i (x_i - y_i)^2}
$$

### Risk Metrics

Volatility from variance:

$$
\sigma = \sqrt{\text{Var}(R)}
$$

## Implementation Details

### Negative Input Handling

Mathematical $\sqrt{x}$ is undefined for $x < 0$. This implementation:
- Returns last valid value for negative inputs
- Returns last valid value for NaN/Infinity
- Starts with lastValid = 0.0 (since sqrt(0) = 0)

### Precision Characteristics

| Input Range | Relative Precision |
|:------------|:-------------------|
| $x > 0$ | Full 15-16 digits |
| $x = 0$ | Exact (returns 0) |
| $x < 0$ | Substituted with last valid |

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
| SQRT | 1 | Hardware instruction (FSQRT) |
| CMP | 1 | Domain check |
| **Total** | ~15-20 cycles | Platform dependent |

### Quality Metrics

| Metric | Score | Notes |
|:-------|:-----:|:------|
| **Accuracy** | 10/10 | IEEE 754 compliant |
| **Timeliness** | 10/10 | Zero lag |
| **Smoothness** | N/A | Transform preserves input characteristics |

## Usage Examples

### Basic Usage

```csharp
// Create SQRT transformer
var sqrt = new Sqrttrans();

// Transform a value
var price = new TValue(DateTime.UtcNow, 100.0);
var result = sqrt.Update(price);  // 10.0
```

### Variance Stabilization

```csharp
var prices = new TSeries();
// ... populate with price data

// Apply sqrt transform for variance stabilization
var sqrtPrices = Sqrttrans.Calculate(prices);

// Now compute statistics on transformed data
var stdDev = StdDev.Calculate(sqrtPrices, 20);
```

### Batch Processing

```csharp
var source = new double[] { 1, 4, 9, 16, 25 };
var output = new double[source.Length];

Sqrttrans.Calculate(source, output);
// output: { 1, 2, 3, 4, 5 }
```

### Chained with Square

```csharp
// Round-trip: sqrt(x^2) = |x|
var values = bars.Close;
var squared = values.Select(v => new TValue(v.Time, v.Value * v.Value)).ToTSeries();
var recovered = Sqrttrans.Calculate(squared);
// recovered ≈ abs(original)
```

## Common Pitfalls

1. **Negative Input**: Prices are always positive, but derived values (returns, differences) can be negative. Sqrt is undefined for negatives—this implementation returns last valid value.

2. **Zero Amplification**: Near zero, small changes in input cause large changes in sqrt output. $\sqrt{0.01} = 0.1$ but $\sqrt{0.0001} = 0.01$—a 100x input change yields only 10x output change.

3. **Reversal Requires Squaring**: To undo sqrt, square the result. Unlike log/exp which are inverses, sqrt/square are only one-way inverses for non-negative values.

4. **Variance Stabilization Assumption**: Sqrt is optimal when variance scales linearly with mean. For other heteroscedasticity patterns, log or Box-Cox may be more appropriate.

5. **Magnitude Compression**: Sqrt compresses large values more than small ones. $\sqrt{10000} = 100$ but $\sqrt{100} = 10$. This can distort technical analysis patterns that depend on absolute price levels.

## Validation

| Test | Status |
|:-----|:------:|
| **Math.Sqrt Parity** | ✅ |
| **Perfect Squares (0,1,4,9,16,25,100)** | ✅ |
| **Irrational Results (√2, √3, √5)** | ✅ |
| **Inverse of Square** | ✅ |
| **Product Rule** | ✅ |
| **Quotient Rule** | ✅ |
| **Power Relationship (x^0.5)** | ✅ |
| **Small Values (1e-10 to 1e-2)** | ✅ |
| **Large Values (1e10 to 1e100)** | ✅ |

## References

- Box, G.E.P., & Cox, D.R. (1964). "An Analysis of Transformations." *Journal of the Royal Statistical Society, Series B*, 26(2), 211-252.
- Tukey, J.W. (1977). *Exploratory Data Analysis*. Addison-Wesley. (Variance-stabilizing transformations)
- IEEE 754-2019. *Standard for Floating-Point Arithmetic*. (sqrt specification)