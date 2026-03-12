# LOGTRANS: Natural Logarithm Transformer

> *The logarithm is one of the most useful mathematical functions, turning multiplicative relationships into additive ones—a property that makes many financial calculations tractable.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Numeric                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | None                      |
| **Outputs**      | Single series (LOGTRANS)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `0` bars                          |
| **PineScript**   | [logtrans.pine](logtrans.pine)                       |

- The LOG transformer applies the natural logarithm function $\ln(x)$ to input values.
- No configurable parameters; computation is stateless per bar.
- Output range: Varies (see docs).
- Requires `0` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The LOG transformer applies the natural logarithm function $\ln(x)$ to input values. This point-wise transformation compresses large values and expands small ones, making it essential for analyzing multiplicative processes like compounded returns.

## Mathematical Foundation

The natural logarithm is defined as the inverse of the exponential function:

$$
y = \ln(x) \quad \text{where} \quad e^y = x
$$

Key identities:

- $\ln(1) = 0$
- $\ln(e) = 1$
- $\ln(e^n) = n$

### Logarithm Rules

**Product Rule:**
$$
\ln(a \cdot b) = \ln(a) + \ln(b)
$$

**Quotient Rule:**
$$
\ln\left(\frac{a}{b}\right) = \ln(a) - \ln(b)
$$

**Power Rule:**
$$
\ln(a^n) = n \cdot \ln(a)
$$

## Financial Applications

### Log Returns

Log returns (continuously compounded returns) are computed as:

$$
r_t = \ln\left(\frac{P_t}{P_{t-1}}\right) = \ln(P_t) - \ln(P_{t-1})
$$

Log returns have desirable properties:
- **Additive over time**: Multi-period return is the sum of single-period returns
- **Symmetric**: A +10% log return followed by -10% returns to original price
- **Approximately equal** to simple returns for small changes

### Volatility Analysis

Log-transformed prices are often used in volatility modeling because:
- Standard deviation of log returns estimates volatility
- Log prices follow geometric Brownian motion (GBM) under common models

## Domain Restrictions

The natural logarithm is only defined for positive real numbers:

$$
\text{Domain}: x > 0
$$

Invalid inputs (zero, negative, NaN, Infinity) return the last valid output value—a common pattern in financial indicators to prevent propagation of invalid data.

## Performance Profile

### Operation Count

| Operation | Count | Notes |
| :--- | :---: | :--- |
| Math.Log | 1 | Single transcendental function call |
| Comparison | 2 | Finite check, positive check |

**Cycles per value:** ~15-25 (dominated by log computation)

### SIMD Considerations

The Calculate span method includes AVX2 detection but falls back to scalar processing for proper last-valid-value handling. Pure SIMD vectorization of log is possible but requires handling domain violations differently.

## API Usage

### Streaming Mode

```csharp
var log = new Logtrans();
var result = log.Update(new TValue(time, price));
```

### Batch Mode

```csharp
var logPrices = Logtrans.Calculate(priceSeries);
```

### Span Mode

```csharp
Logtrans.Calculate(sourceSpan, outputSpan);
```

### Chaining

```csharp
var logTransform = new Logtrans(priceSource);
// logTransform.Last updates automatically when priceSource publishes
```

## Common Pitfalls

1. **Zero/Negative Inputs**: Log of zero or negative numbers is undefined. The implementation substitutes last valid value.

2. **Numerical Precision**: For values very close to 1, use `Math.Log1p(x-1)` for better precision (not implemented here).

3. **Overflow Potential**: $\exp(\ln(x)) = x$ only within floating-point precision limits.

4. **Inverse Relationship**: Remember that LOG compresses large values—a 10x price increase only doubles the log value.

## References

- Wilmott, P. (2006). "Paul Wilmott on Quantitative Finance." Wiley.
- Hull, J. (2018). "Options, Futures, and Other Derivatives." Pearson.
