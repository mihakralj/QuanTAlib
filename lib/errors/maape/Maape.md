# MAAPE: Mean Arctangent Absolute Percentage Error

> "When percentage errors need boundaries, arctangent provides the walls."

Mean Arctangent Absolute Percentage Error (MAAPE) transforms percentage errors through the arctangent function, naturally bounding the metric between 0 and π/2. This eliminates the unbounded nature of MAPE while preserving its scale-independence.

## Historical Context

MAAPE was introduced by Kim and Kim (2016) as a solution to MAPE's instability when actual values approach zero. By applying arctangent to percentage errors, extreme values are compressed while small errors remain approximately linear. This makes MAAPE particularly useful in domains where occasional extreme percentage errors occur.

## Architecture & Physics

MAAPE applies `arctan(|error/actual|)` to each error before averaging. The arctangent function compresses large values toward π/2 while preserving linearity for small inputs. This creates a bounded, well-behaved metric even when traditional MAPE would explode.

### Properties

- **Bounded**: Always between 0 and π/2 (≈ 1.571)
- **Scale-independent**: Percentage-based like MAPE
- **Smooth compression**: Large errors are dampened, not truncated
- **Zero-safe**: Handles near-zero actuals gracefully

## Mathematical Foundation

### 1. Arctangent Percentage Error

For each observation, compute:

$$e_i = \arctan\left(\frac{|y_i - \hat{y}_i|}{|y_i|}\right)$$

Where:
- $y_i$ = actual value
- $\hat{y}_i$ = predicted value

### 2. Mean Calculation

Average the arctangent errors:

$$MAAPE = \frac{1}{n} \sum_{i=1}^{n} \arctan\left(\frac{|y_i - \hat{y}_i|}{|y_i|}\right)$$

### 3. Bounds

The function is bounded:

$$0 \leq MAAPE \leq \frac{\pi}{2}$$

- When error = 0: arctan(0) = 0
- When error → ∞: arctan(∞) → π/2

### 4. Running Update (O(1))

QuanTAlib uses a ring buffer with running sum for O(1) updates:

$$S_{new} = S_{old} - e_{oldest} + e_{newest}$$

$$MAAPE = \frac{S_{new}}{n}$$

## Implementation Details

### Usage Patterns

```csharp
// Streaming mode - update with each new observation
var maape = new Maape(period: 20);
var result = maape.Update(actualValue, predictedValue);

// Batch mode - calculate for entire series
var results = Maape.Calculate(actualSeries, predictedSeries, period: 20);

// Span mode - zero-allocation for high performance
Maape.Batch(actualSpan, predictedSpan, outputSpan, period: 20);
```

### Parameters

| Parameter | Type | Description |
| :--- | :--- | :--- |
| **period** | int | Lookback window for averaging (must be > 0) |

### Properties

| Property | Type | Description |
| :--- | :--- | :--- |
| **Last** | TValue | Most recent MAAPE value (in radians) |
| **IsHot** | bool | True when buffer is full |
| **Name** | string | Indicator name (e.g., "Maape(20)") |
| **WarmupPeriod** | int | Number of periods before valid output |

## Performance Profile

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | ~20 ns/bar | O(1) update, arctan computation |
| **Allocations** | 0 | Uses pre-allocated ring buffer |
| **Complexity** | O(1) | Constant time per update |
| **Accuracy** | 10/10 | Exact calculation |
| **Timeliness** | 9/10 | No lag beyond the period |
| **Boundedness** | 10/10 | Always in [0, π/2] |

## Interpretation

| MAAPE Range | Interpretation | Approx. % Error |
| :--- | :--- | :--- |
| **0** | Perfect prediction | 0% |
| **0 - 0.1** | Excellent | < 10% |
| **0.1 - 0.3** | Good | 10-30% |
| **0.3 - 0.5** | Moderate | 30-50% |
| **0.5 - 0.8** | High error | 50-100% |
| **0.8 - π/2** | Very high error | > 100% |

## Comparison with MAPE

| Scenario | MAPE | MAAPE |
| :--- | :--- | :--- |
| **10% error** | 10% | 0.0997 rad |
| **100% error** | 100% | 0.785 rad (π/4) |
| **1000% error** | 1000% | 1.471 rad |
| **Near-zero actual** | → ∞ | → π/2 |
| **Outlier sensitivity** | High | Low |

### Key Insight

The arctangent compression means that the difference between 100% and 1000% error is much smaller in MAAPE than in MAPE, making MAAPE more robust to extreme outliers.

## Common Use Cases

1. **Demand Forecasting**: When some products have near-zero demand
2. **Financial Predictions**: Handling occasional extreme moves
3. **Model Comparison**: Stable metric across different scales
4. **Robust Evaluation**: When MAPE would be dominated by outliers

## Edge Cases

- **Zero Actual Values**: Uses arctan(∞) = π/2 (maximum bounded error)
- **NaN Handling**: Uses last valid value substitution
- **Single Input**: Not supported (requires two series)
- **Period = 1**: Returns current arctangent percentage error
- **Perfect Predictions**: Returns exactly 0

## Related Indicators

- [MAPE](../mape/Mape.md) - Mean Absolute Percentage Error (unbounded)
- [SMAPE](../smape/Smape.md) - Symmetric MAPE (different bounding approach)
- [LogCosh](../logcosh/LogCosh.md) - Log-Cosh Loss (similar compression philosophy)
