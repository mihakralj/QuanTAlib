# Tukey's Biweight: Robust Loss Function

> "When outliers need to be silenced, not just quieted."

Tukey's Biweight (also called Bisquare) is a redescending M-estimator that completely ignores errors beyond a threshold. Unlike Huber loss which still penalizes large errors linearly, Tukey's biweight treats extreme outliers as if they don't exist.

## Historical Context

Developed by John Tukey as part of his work on robust statistics in the 1970s, the biweight function was designed for situations where outliers are not just unusual but fundamentally different from the rest of the data. In such cases, including outliers at all (even with reduced influence) can corrupt the estimate.

## Architecture & Physics

The biweight function is a smooth, bell-shaped curve that rises from 0, peaks at some finite error, and then descends back toward 0 for very large errors. Errors beyond the threshold c contribute nothing to the loss. This "redescending" property makes it extremely robust to gross outliers.

### Properties

- **Redescending**: Large errors contribute zero loss (complete outlier rejection)
- **Smooth**: Continuously differentiable everywhere
- **Bounded**: Maximum loss is c²/6, regardless of error magnitude
- **Tunable**: Parameter c controls the outlier threshold

## Mathematical Foundation

### 1. Tukey's Biweight Function

For each error, compute:

$$\rho(e) = \begin{cases}
\frac{c^2}{6}\left[1 - \left(1 - \left(\frac{e}{c}\right)^2\right)^3\right] & \text{if } |e| \leq c \\
\frac{c^2}{6} & \text{if } |e| > c
\end{cases}$$

Where:
- $e = y - \hat{y}$ = prediction error
- $c$ = tuning constant (threshold)

### 2. Alternative Form

For $|e| \leq c$:

$$\rho(e) = \frac{c^2}{6}\left(1 - \left(1 - u^2\right)^3\right)$$

where $u = e/c$

### 3. Key Values

- At $e = 0$: $\rho(0) = 0$
- At $e = c$: $\rho(c) = c^2/6$ (maximum)
- For $|e| > c$: $\rho(e) = c^2/6$ (constant, flat)

### 4. Running Update (O(1))

QuanTAlib uses a ring buffer with running sum for O(1) updates:

$$S_{new} = S_{old} - \rho_{oldest} + \rho_{newest}$$

$$TukeyBiweight = \frac{S_{new}}{n}$$

## Implementation Details

### Usage Patterns

```csharp
// Streaming mode - with custom threshold
var tukey = new TukeyBiweight(period: 20, c: 4.685);
var result = tukey.Update(actualValue, predictedValue);

// Batch mode - calculate for entire series
var results = TukeyBiweight.Calculate(actualSeries, predictedSeries, period: 20, c: 4.685);

// Span mode - zero-allocation for high performance
TukeyBiweight.Batch(actualSpan, predictedSpan, outputSpan, period: 20, c: 4.685);
```

### Parameters

| Parameter | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| **period** | int | - | Lookback window for averaging (must be > 0) |
| **c** | double | 4.685 | Tuning constant (must be > 0) |

### Properties

| Property | Type | Description |
| :--- | :--- | :--- |
| **Last** | TValue | Most recent Tukey Biweight value |
| **IsHot** | bool | True when buffer is full |
| **C** | double | Current threshold parameter |
| **Name** | string | Indicator name (e.g., "TukeyBiweight(20,4.685)") |
| **WarmupPeriod** | int | Number of periods before valid output |

## Performance Profile

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | ~15 ns/bar | O(1) update complexity |
| **Allocations** | 0 | Uses pre-allocated ring buffer |
| **Complexity** | O(1) | Constant time per update |
| **Accuracy** | 10/10 | Exact calculation |
| **Timeliness** | 9/10 | No lag beyond the period |
| **Robustness** | 10/10 | Complete outlier rejection |

## Choosing c

| c Value | Efficiency | Robustness | Use Case |
| :--- | :--- | :--- | :--- |
| **4.685** | 95% at Gaussian | Moderate | Standard choice |
| **6.0** | 98% at Gaussian | Lower | More outlier-tolerant |
| **3.0** | 85% at Gaussian | Higher | More aggressive rejection |
| **1.5** | ~70% at Gaussian | Very high | Extreme outlier rejection |

The default c=4.685 achieves 95% efficiency for Gaussian data while providing good robustness.

## Comparison with Other Robust Losses

| Error Size | L2 (MSE) | Huber | Tukey |
| :--- | :--- | :--- | :--- |
| **Small (< δ)** | e² | e²/2 | Growing |
| **Medium (δ to c)** | e² | δ\|e\| - δ²/2 | Growing |
| **Large (> c)** | e² (huge) | δ\|e\| - δ²/2 (linear) | c²/6 (flat) |
| **Very large** | Explodes | Still grows | Constant |

### Key Insight

Tukey's biweight is the only loss function that completely stops penalizing errors beyond a threshold. A prediction error of 10 contributes the same as an error of 1000 if both exceed c.

## Common Use Cases

1. **Sensor Data**: Reject faulty readings entirely
2. **Financial Data**: Ignore flash crashes or data errors
3. **Image Processing**: Robust edge detection
4. **Scientific Measurement**: Exclude instrument failures

## Edge Cases

- **Perfect Predictions**: Returns exactly 0
- **All Outliers**: Returns c²/6 (maximum bounded loss)
- **NaN Handling**: Uses last valid value substitution
- **Single Input**: Not supported (requires two series)
- **c = 0**: Invalid (division issues)
- **Errors exactly at c**: Smooth transition (differentiable)

## Related Indicators

- [Huber](../huber/Huber.md) - Huber Loss (linear, not redescending)
- [MdAE](../mdae/Mdae.md) - Median Absolute Error (robust via median)
- [LogCosh](../logcosh/LogCosh.md) - Log-Cosh Loss (smooth L1/L2 hybrid)
