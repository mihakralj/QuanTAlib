# IQR: Interquartile Range

> "The median is the most important statistic, and the interquartile range is the second most important." — John Tukey

## Introduction

The Interquartile Range measures the spread of the middle 50% of a sorted dataset within a rolling window. By subtracting the 25th percentile (Q1) from the 75th percentile (Q3), IQR provides a robust dispersion metric that ignores outliers in both tails. Unlike standard deviation, which squares deviations and amplifies extremes, IQR tells you how wide the "typical" price band actually is.

## Historical Context

John Tukey formalized the IQR in the 1970s as part of his Exploratory Data Analysis (EDA) framework. The concept traces back to Francis Galton's work on percentiles in the 1880s. In finance, IQR serves as a building block for outlier detection (the "1.5 IQR rule" for Tukey fences), robust volatility estimation, and distribution shape analysis. Most implementations of rolling IQR require sorting, making it O(N log N) per bar with a naive approach; this implementation uses incremental sorted buffer maintenance at O(N) per update.

## Architecture

### 1. Sorted Buffer Maintenance

The indicator maintains a sorted array alongside a ring buffer. On each new bar:

1. Remove the oldest value from the sorted array via binary search + shift.
2. Insert the new value at the correct sorted position via binary search + shift.
3. Compute Q1 and Q3 from the sorted array via linear interpolation.

This avoids a full sort on every update, reducing complexity from O(N log N) to O(N) per bar.

### 2. Percentile Interpolation

Percentile is computed using the PERCENTILE.INC method (matching Excel and PineScript):

$$\text{rank} = \frac{p}{100} \times (n - 1)$$

$$\text{percentile} = x_{\lfloor r \rfloor} + (r - \lfloor r \rfloor) \times (x_{\lceil r \rceil} - x_{\lfloor r \rfloor})$$

where $p$ is the desired percentile (25 or 75), $n$ is the window size, and $x_i$ is the $i$-th value in the sorted window.

### 3. Bar Correction

The sorted buffer state is backed up before each new bar. On `isNew=false`, the backup is restored, the old newest value is removed, and the corrected value is inserted. This supports Quantower's bar-correction protocol.

## Mathematical Foundation

### IQR Definition

$$\text{IQR} = Q_3 - Q_1$$

where:

- $Q_1 = P_{25}$ (25th percentile)
- $Q_3 = P_{75}$ (75th percentile)

### Properties

| Property | Value |
|----------|-------|
| Range | $[0, \infty)$ |
| Constant series | 0 |
| Symmetric distribution | $Q_3 - Q_1$ equals twice the median absolute deviation from median |
| Outlier resistance | Breakdown point = 25% (ignores up to 25% contamination per tail) |

### Relationship to Other Measures

- **Standard Deviation**: For normal data, $\text{IQR} \approx 1.35 \times \sigma$
- **Median Absolute Deviation**: $\text{MAD} \approx \text{IQR} / 1.349$ for normal data
- **Tukey Fences**: Outlier bounds at $Q_1 - 1.5 \times \text{IQR}$ and $Q_3 + 1.5 \times \text{IQR}$

## Usage

```csharp
// Streaming
var iqr = new Iqr(period: 20);
TValue result = iqr.Update(new TValue(time, price));

// Batch
TSeries results = Iqr.Batch(source, period: 20);

// Span (zero-allocation batch path)
Iqr.Batch(sourceSpan, outputSpan, period: 20);

// Event-driven chaining
var iqr = new Iqr(sourceIndicator, period: 20);

// Calculate bridge (returns results + indicator for continued streaming)
var (results, indicator) = Iqr.Calculate(source, period: 20);
```

## Interpretation

| IQR Behavior | Market Signal |
|-------------|---------------|
| Rising IQR | Increasing price dispersion; volatility expansion |
| Falling IQR | Decreasing price dispersion; volatility contraction |
| IQR near zero | Price clustering; potential breakout setup |
| IQR spike | Sudden distribution widening; possible regime change |

### Outlier Detection

Values outside $[Q_1 - 1.5 \times \text{IQR},\ Q_3 + 1.5 \times \text{IQR}]$ are statistical outliers. This can flag unusual price moves without sensitivity to extreme values that distort standard deviation.

## Performance Profile

### Operation Count (Streaming Mode)

IQR collects the window, partially sorts to find Q1 and Q3, then returns Q3 - Q1.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Ring buffer collect | N | 1 cy | ~N cy |
| Partial sort for Q1, Q3 | N log N | 2 cy | ~2N log N cy |
| Subtract Q3 - Q1 | 1 | 1 cy | ~1 cy |
| NaN guard + state update | 1 | 2 cy | ~2 cy |
| **Total (N=20)** | **O(N log N)** | — | **~183 cy** |

O(N log N) per update due to sort. No O(1) sliding-window IQR algorithm exists without maintaining a sorted structure (e.g., two heaps), which adds complexity.

| Metric | Value |
|--------|-------|
| Update complexity | O(N) per bar (binary search + array shift) |
| Memory | O(N) — sorted buffer + ring buffer + backup |
| SIMD potential | None (sorting is inherently sequential) |
| Warmup period | Equal to period |

### Quality Metrics

| Criterion | Score (1-10) |
|-----------|:---:|
| Outlier resistance | 9 |
| Computational efficiency | 6 |
| Interpretability | 9 |
| Parameter sensitivity | 3 |
| Lag | 5 |

## Validation

No external library implements a streaming rolling IQR with linear interpolation percentiles. Validation is based on:

- **Known-value tests**: Hand-computed Q1, Q3 for small windows
- **Constant series**: IQR = 0
- **Linear sequence**: Exact IQR computable analytically
- **Batch vs streaming consistency**: All four API modes produce identical results
- **Non-negativity**: IQR >= 0 for all inputs
- **IQR <= range**: Always bounded by full window range

## Common Pitfalls

1. **Period too small** (< 5): Percentile interpolation becomes unstable with very few points. Minimum period is 2, but practical use requires >= 10.
2. **Confusing IQR with standard deviation**: IQR measures range of middle 50%; it is not a drop-in replacement for standard deviation in formulas expecting variance-based measures.
3. **Ignoring the window effect**: As the window slides, old outliers dropping out can cause sudden IQR changes that look like false signals.
4. **Not accounting for NaN**: This implementation substitutes last-valid values for NaN/Infinity in streaming mode, but the static span batch does not (NaN propagates as-is in sorted position).
5. **Over-relying on the 1.5 IQR rule**: The Tukey fence is calibrated for roughly normal distributions. Heavily skewed financial data may need adjusted multipliers.

## References

- Tukey, J.W. (1977). *Exploratory Data Analysis*. Addison-Wesley.
- Galton, F. (1885). "Statistics by Intercomparison." *Philosophical Magazine*.
- Frigge, M., Hoaglin, D.C., Iglewicz, B. (1989). "Some Implementations of the Boxplot." *The American Statistician*, 43(1), 50-54.
