# GEOMEAN: Geometric Mean

> *The geometric mean is never greater than the arithmetic mean.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Statistic                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Geomean)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [geomean.pine](geomean.pine)                       |

- The Geometric Mean computes the nth root of the product of n positive values over a sliding window.
- Parameterized by `period`.
- Output range: Varies (see docs).
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The Geometric Mean computes the nth root of the product of n positive values over a sliding window. Unlike the arithmetic mean, it captures multiplicative relationships and is the correct average for growth rates, ratios, and log-normally distributed data. For financial time series, this means it properly accounts for compounding.

## Historical Context

The geometric mean dates to Euclid's Elements (ca. 300 BCE), where it appeared as the "mean proportional" between two lengths. The concept was well-understood in ancient Greek geometry but found its modern statistical footing in the 19th century. In finance, the geometric mean return became the standard for reporting compounded investment performance after the realization that arithmetic means systematically overstate expected returns for volatile assets. A portfolio returning +50% then -50% has an arithmetic mean of 0% but a geometric mean of approximately -13.4%, which is the actual result. The arithmetic mean lied; the geometric mean told the truth.

## Architecture & Physics

`Geomean` extends `AbstractBase` for single-value input streaming. Instead of computing the nth root of a product directly (which overflows or underflows for even modest windows), it maintains a running sum of logarithms using Kahan-Babuska compensated summation.

### Design Decisions

1. **Log-sum approach**: Converts the product $\prod x_i$ into $\sum \ln(x_i)$, then exponentiates. This avoids catastrophic overflow/underflow that plagues direct multiplication for windows larger than approximately 20 values.

2. **O(1) streaming updates**: Uses a `RingBuffer` to track which log-values are in the window. When a new value enters, its log is added; when an old value exits, its log is subtracted. The Kahan-Babuska compensation preserves numerical accuracy across millions of updates.

3. **Periodic resync**: Every 1000 ticks, the running sum is recomputed from scratch to bound floating-point drift. Without this, sequential add/subtract cycles accumulate error proportional to the number of updates.

4. **Non-positive value handling**: Values <= 0 have undefined logarithms. The indicator substitutes the last valid positive value, matching the PineScript reference behavior. This is conservative but safe.

5. **No SIMD in Update**: The streaming path is inherently sequential (running compensated sum with state). SIMD is used in the static `Batch(Span)` method where applicable.

## Mathematical Foundation

For $n$ positive values $x_1, x_2, \ldots, x_n$, the geometric mean is:

$$ G = \left(\prod_{i=1}^{n} x_i\right)^{1/n} $$

Equivalently, using logarithms:

$$ G = \exp\!\left(\frac{1}{n} \sum_{i=1}^{n} \ln(x_i)\right) $$

The key identity exploited by the implementation:

$$ \ln(G) = \frac{1}{n} \sum_{i=1}^{n} \ln(x_i) $$

### AM-GM Inequality

For positive real numbers, the geometric mean is always less than or equal to the arithmetic mean:

$$ G \leq A = \frac{1}{n} \sum_{i=1}^{n} x_i $$

Equality holds if and only if all values are identical. This property is validated in the test suite.

### Kahan-Babuska Compensation

The running log-sum uses second-order compensation:

$$
\begin{aligned}
y &= \ln(x_{\text{new}}) - c \\
t &= S + y \\
c &= (t - S) - y \\
S &= t
\end{aligned}
$$

This bounds the accumulated error to $O(\varepsilon)$ rather than $O(n\varepsilon)$ for naive summation, where $\varepsilon$ is machine epsilon.

## Performance Profile

### Operation Count (Streaming Mode)

Geometric Mean uses a running log-sum over the sliding window for O(1) update.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Ring buffer add/evict | 1 | 3 cy | ~3 cy |
| log(new) - log(evict) on running sum | 2 | 8 cy | ~16 cy |
| exp(sum / N) for geometric mean | 1 | 20 cy | ~20 cy |
| NaN guard (non-positive values) | 1 | 2 cy | ~2 cy |
| **Total** | **O(1)** | — | **~41 cy** |

O(1) per update via log-sum trick. exp() is the dominant cost (~20 cy). Guard against log(0) by substituting last-valid when input <= 0.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | ~5ns/bar | O(1) log-add/subtract per update. |
| **Allocations** | 0 | RingBuffer pre-allocated; no heap allocation in Update. |
| **Complexity** | O(1) streaming | Amortized O(1) with periodic O(period) resync every 1000 ticks. |
| **Accuracy** | 9/10 | Kahan-Babuska compensation + periodic resync. |

## Validation

Self-validated against mathematical properties and known analytical values. MathNet.Numerics `Statistics.GeometricMean` provides external cross-validation.

| Property | Status | Notes |
| :--- | :--- | :--- |
| **Known values** | ✅ | geomean({2, 8}) = 4.0; geomean({1, 2, 4, 8}) = 2√2 ≈ 2.8284. |
| **Constant series** | ✅ | Returns the constant value exactly. |
| **AM-GM inequality** | ✅ | G ≤ A for all test inputs. |
| **Positive output** | ✅ | Always positive for positive inputs. |
| **Batch = Streaming** | ✅ | Exact match across all modes. |
| **MathNet cross-validation** | ✅ | Matches `Statistics.GeometricMean` within 1e-9. |

## Common Pitfalls

1. **Zero or negative values**: The geometric mean is undefined for non-positive values. The indicator substitutes the last valid value, but this is a lossy approximation. Filter your data first if zeros are meaningful.

2. **Overflow with direct multiplication**: Never compute $\prod x_i$ directly for large windows. Even double-precision overflows around $n \approx 20$ for values > 100. The log-sum approach eliminates this entirely.

3. **Confusing with arithmetic mean**: The geometric mean is always smaller (or equal) for positive values. Using the arithmetic mean for compounding returns overstates expected performance.

4. **Small windows**: With period=2, the geometric mean reduces to $\sqrt{x_1 \cdot x_2}$. Mathematically correct but noisy.

5. **Log-normal assumption**: The geometric mean is the natural center for log-normally distributed data (returns). For normally distributed data, the arithmetic mean is more appropriate.

## Usage

```csharp
using QuanTAlib;

// Create a 14-period Geometric Mean
var geomean = new Geomean(14);

// Update with a new value
var result = geomean.Update(new TValue(DateTime.UtcNow, 100.0));

// Get the last computed geometric mean
double value = geomean.Last.Value;

// Batch mode
var series = Geomean.Batch(source, period: 14);

// Span mode
Geomean.Batch(inputSpan, outputSpan, period: 14);
```

## References

- Euclid, *Elements*, Book VI, Proposition 13 (ca. 300 BCE).
- Cauchy, A.-L. "Cours d'analyse de l'Ecole royale polytechnique" (1821). First rigorous proof of AM-GM.
- Kahan, W. "Pracniques: Further Remarks on Reducing Truncation Errors" (1965).
- PineScript `ta.geomean()` reference implementation.
