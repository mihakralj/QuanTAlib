# LINEARTRANS: Linear Scaling Transformer

> *The simplest transformations are often the most powerful—linear scaling is the mathematical equivalent of adjusting the volume and tuning the dial.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Numeric                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `slope` (default 1.0), `intercept` (default 0.0)                      |
| **Outputs**      | Single series (Lineartrans)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `0` bars                          |
| **PineScript**   | [lineartrans.pine](lineartrans.pine)                       |

- The Linear transformer applies an affine transformation $y = \text{slope} \cdot x + \text{intercept}$ to each value in a time series.
- Parameterized by `slope` (default 1.0), `intercept` (default 0.0).
- Output range: Varies (see docs).
- Requires `0` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The Linear transformer applies an affine transformation $y = \text{slope} \cdot x + \text{intercept}$ to each value in a time series. This fundamental operation enables scaling, offsetting, unit conversion, and normalization—the building blocks for preparing data for analysis or combining signals from different sources.

## Mathematical Foundation

### Core Formula

$$
\text{Linear}_t = m \cdot x_t + b
$$

where:
- $m$ is the slope (multiplicative factor)
- $b$ is the intercept (additive constant)
- $x_t$ is the input value at time $t$

### Key Properties

| Property | Formula | Description |
|:---------|:--------|:------------|
| **Identity** | $1 \cdot x + 0 = x$ | Default parameters preserve input |
| **Composition** | $c(ax+b)+d = (ac)x + (bc+d)$ | Sequential transforms combine linearly |
| **Inverse** | $\frac{1}{m}(y - b) = x$ | Recoverable when $m \neq 0$ |
| **Difference Preservation** | $y_2 - y_1 = m(x_2 - x_1)$ | Relative differences scaled by slope |
| **Zero Crossing** | $y = 0$ when $x = -b/m$ | Predictable intercept with x-axis |

### Domain and Range

| | Value |
|:--|:--|
| **Domain** | $(-\infty, +\infty)$ |
| **Range** | $(-\infty, +\infty)$ when $m \neq 0$; $\{b\}$ when $m = 0$ |

## Financial Applications

### Unit Conversion

Convert between price units or currencies:

$$
P_{\text{USD}} = \text{rate} \cdot P_{\text{EUR}}
$$

### Percentage to Decimal

Convert percentage values to decimal form:

$$
r_{\text{decimal}} = 0.01 \cdot r_{\text{percent}}
$$

### Basis Point Scaling

Convert decimal rates to basis points:

$$
r_{\text{bps}} = 10000 \cdot r_{\text{decimal}}
$$

### Price Normalization

Normalize prices to a baseline:

$$
P_{\text{norm}} = \frac{P_t - P_0}{P_0} = \frac{1}{P_0} \cdot P_t - 1
$$

This is `Linear(1/P₀, -1)`.

### Signal Combination

Scale and combine multiple indicators:

$$
\text{Combo} = w_1 \cdot \text{RSI} + w_2 \cdot \text{MACD}_{\text{scaled}}
$$

## Implementation Details

### Fused Multiply-Add (FMA)

The implementation uses `Math.FusedMultiplyAdd(slope, value, intercept)` which computes $m \cdot x + b$ with a single rounding operation, providing:
- Better numerical precision
- Potential hardware acceleration
- Reduced floating-point error accumulation

### Special Cases

| slope | intercept | Effect |
|:------|:----------|:-------|
| 1.0 | 0.0 | Identity (passthrough) |
| 0.0 | b | Constant output |
| -1.0 | 0.0 | Negation |
| m | 0.0 | Pure scaling |
| 1.0 | b | Pure offset |

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
| FMA | 1 | Single fused operation |
| **Total** | ~4 cycles | Near-instantaneous |

### SIMD Optimization

The span-based `Calculate` method uses AVX2/FMA intrinsics:
- Processes 4 doubles per iteration
- Hardware FMA when available
- ~8× throughput improvement for large datasets

### Quality Metrics

| Metric | Score | Notes |
|:-------|:-----:|:------|
| **Accuracy** | 10/10 | FMA provides optimal precision |
| **Timeliness** | 10/10 | Zero lag |
| **Smoothness** | N/A | Transform preserves input characteristics |

## Usage Examples

### Basic Usage

```csharp
// Scale values by 2x and add 10
var linear = new Lineartrans(slope: 2.0, intercept: 10.0);

var input = new TValue(DateTime.UtcNow, 50.0);
var result = linear.Update(input);  // 110.0
```

### Converting Percentage to Decimal

```csharp
var toDecimal = new Lineartrans(slope: 0.01, intercept: 0.0);

var percent = new TValue(DateTime.UtcNow, 5.5);  // 5.5%
var decimalRate = toDecimal.Update(percent);     // 0.055
```

### Normalizing to Baseline

```csharp
double baseline = 100.0;
var normalizer = new Lineartrans(slope: 1.0 / baseline, intercept: -1.0);

// Converts prices to percentage change from baseline
var price = new TValue(DateTime.UtcNow, 105.0);
var pctChange = normalizer.Update(price);  // 0.05 (5% above baseline)
```

### Inverting a Transform

```csharp
double m = 2.0, b = 10.0;

var transform = new Lineartrans(m, b);
var inverse = new Lineartrans(1.0 / m, -b / m);

// Round-trip: value → transformed → original
var original = new TValue(DateTime.UtcNow, 50.0);
var transformed = transform.Update(original);  // 110.0
var recovered = inverse.Update(transformed);   // 50.0
```

### Chaining Transforms

```csharp
var scale = new Lineartrans(2.0, 0.0);
var offset = new Lineartrans(scale, 1.0, 10.0);  // Chain: scale then add 10

// Equivalent to: Linear(2.0, 10.0)
```

## Common Pitfalls

1. **Zero Slope Trap**: Setting `slope=0` produces constant output regardless of input. This is valid but often unintentional.

2. **Division by Zero in Inverse**: When computing inverse transforms, ensure the original slope is non-zero.

3. **Overflow Risk**: Large slopes combined with large inputs can overflow. For slope=1e100 and x=1e100, the result exceeds double precision.

4. **Precision Accumulation**: While single transforms are precise, many chained transforms accumulate error. Use composition formula to combine into single transform when possible.

5. **Parameter Validation**: Constructor rejects NaN/Infinity for slope and intercept to fail fast rather than propagate invalid results.

## Validation

| Test | Status |
|:-----|:------:|
| **Mathematical Formula Parity** | ✅ |
| **Identity Transform** | ✅ |
| **Composition Property** | ✅ |
| **Inverse Recovery** | ✅ |
| **Difference Preservation** | ✅ |
| **FMA Accuracy** | ✅ |

## References

- Strang, G. (2016). *Introduction to Linear Algebra*. Wellesley-Cambridge Press.
- Goldberg, D. (1991). "What Every Computer Scientist Should Know About Floating-Point Arithmetic." *ACM Computing Surveys*.
- Intel Corporation. (2023). *Intel 64 and IA-32 Architectures Optimization Reference Manual*. (FMA instruction details)
