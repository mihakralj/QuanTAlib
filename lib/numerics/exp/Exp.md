# EXP: Exponential Function

> "The exponential function is the only function that is its own derivative—a mathematical curiosity that makes it indispensable for modeling growth, decay, and everything compounding."

The Exponential (EXP) transformer applies the natural exponential function $e^x$ to each value in a time series. As the inverse of the natural logarithm, it converts additive relationships back to multiplicative ones, making it essential for reconstructing price levels from log-returns and implementing models that assume log-normal distributions.

## Mathematical Foundation

### Core Formula

$$
\text{EXP}_t = e^{x_t}
$$

where:
- $x_t$ is the input value at time $t$
- $e \approx 2.71828...$ is Euler's number

### Key Properties

| Property | Formula | Description |
|:---------|:--------|:------------|
| **Inverse of Log** | $e^{\ln(x)} = x$ | Undoes natural logarithm |
| **Product Rule** | $e^{a+b} = e^a \cdot e^b$ | Additive inputs → multiplicative outputs |
| **Quotient Rule** | $e^{a-b} = e^a / e^b$ | Differences → ratios |
| **Power Rule** | $e^{n \cdot x} = (e^x)^n$ | Scaling in exponent → power |
| **Identity** | $e^0 = 1$ | Zero maps to unity |
| **Base Value** | $e^1 = e \approx 2.71828$ | Unit exponent gives $e$ |

### Domain and Range

| | Value |
|:--|:--|
| **Domain** | $(-\infty, +\infty)$ |
| **Range** | $(0, +\infty)$ |

The exponential function accepts any real number but always produces strictly positive outputs.

## Financial Applications

### Log-Return to Price Reconstruction

Given cumulative log-returns, reconstruct price levels:

$$
P_t = P_0 \cdot e^{\sum_{i=1}^{t} r_i}
$$

where $r_i$ are log-returns.

### Volatility Scaling

Convert log-volatility to multiplicative factors:

$$
\text{VolFactor} = e^{\sigma \sqrt{T}}
$$

### Compound Growth

Model continuous compounding:

$$
A = P \cdot e^{rt}
$$

where $r$ is the continuous rate and $t$ is time.

### Option Pricing

The exponential appears throughout Black-Scholes:

$$
C = S \cdot N(d_1) - K \cdot e^{-rT} \cdot N(d_2)
$$

## Implementation Details

### Overflow Handling

For large positive inputs, $e^x$ can overflow to infinity:
- $e^{709}$ ≈ $8.2 \times 10^{307}$ (near double max)
- $e^{710}$ → overflow

The implementation substitutes the last valid value when overflow occurs.

### Precision Considerations

| Input Range | Relative Precision |
|:------------|:-------------------|
| $|x| < 1$ | Full 15-16 digits |
| $|x| < 20$ | Full precision |
| $|x| > 700$ | Overflow risk |

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
| EXP | 1 | Hardware instruction |
| **Total** | ~20 cycles | Platform dependent |

### Quality Metrics

| Metric | Score | Notes |
|:-------|:-----:|:------|
| **Accuracy** | 10/10 | IEEE 754 compliant |
| **Timeliness** | 10/10 | Zero lag |
| **Smoothness** | N/A | Transform preserves input characteristics |

## Usage Examples

### Basic Usage

```csharp
// Create EXP transformer
var exp = new Exp();

// Transform log-returns back to growth factors
var logReturn = new TValue(DateTime.UtcNow, 0.05);
var growthFactor = exp.Update(logReturn);  // ≈ 1.0513
```

### Reconstructing Prices from Log-Returns

```csharp
var logReturns = new TSeries();
// ... populate with cumulative log-returns

var cumulativeExp = new Exp();
var priceRatios = cumulativeExp.Update(logReturns);

// Multiply by initial price to get price levels
var initialPrice = 100.0;
var prices = priceRatios.Select(v => v * initialPrice);
```

### Undoing Log Transform

```csharp
var log = new Log();
var exp = new Exp();

// Round-trip: price → log → exp → price
var price = new TValue(DateTime.UtcNow, 150.0);
var logPrice = log.Update(price);     // ≈ 5.0106
var recovered = exp.Update(logPrice); // ≈ 150.0
```

## Common Pitfalls

1. **Overflow Risk**: Input values above ~709 cause overflow. Monitor input ranges when working with cumulative sums.

2. **Magnitude Explosion**: Small additive changes in the exponent create large multiplicative changes in output. A change of 1.0 in the exponent multiplies the output by $e$ ≈ 2.72.

3. **Inverse Relationship**: EXP undoes LOG, but only if the original values were positive. Negative prices cannot be recovered through log-exp round-trip.

4. **Scale Sensitivity**: Unlike LOG which compresses ranges, EXP expands them dramatically. Ensure downstream consumers can handle the output magnitudes.

## Validation

| Test | Status |
|:-----|:------:|
| **Math.Exp Parity** | ✅ |
| **Known Values (e⁰=1, e¹=e)** | ✅ |
| **Inverse of Log** | ✅ |
| **Product Rule** | ✅ |
| **Quotient Rule** | ✅ |
| **Power Rule** | ✅ |

## References

- Euler, L. (1748). *Introductio in analysin infinitorum*.
- Maor, E. (1994). *e: The Story of a Number*. Princeton University Press.
- Hull, J. (2018). *Options, Futures, and Other Derivatives*. Pearson. (Black-Scholes applications)
