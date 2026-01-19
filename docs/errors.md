# Error Metrics: A Practitioner's Guide

> "All models are wrong, but some are useful." — George Box
>
> "All error metrics are flawed, but some are less misleading." — Every quantitative analyst at 3 AM

Error metrics quantify the gap between prediction and reality. Sounds simple. Is not. The choice of error metric shapes what a model optimizes, what failures it hides, and what surprises await in production. QuanTAlib provides over 20 error metrics, each with distinct mathematical properties, failure modes, and use cases.

## The Physics of Error Measurement

An error metric transforms a vector of residuals $(y_i - \hat{y}_i)$ into a scalar. That scalar must capture "wrongness" across thousands of predictions in a single number. This compression is lossy by definition.

Different metrics compress differently:

- **Magnitude metrics** (MAE, RMSE) measure raw distance from truth
- **Relative metrics** (MAPE, SMAPE) normalize by scale
- **Robust metrics** (Huber, MdAE) resist outlier corruption
- **Comparative metrics** (MASE, R²) benchmark against naive alternatives
- **Bias metrics** (ME, MPE) reveal systematic over/underprediction
- **Weighted metrics** (WMAPE, Quantile) handle asymmetric costs

No metric dominates. Each illuminates one facet of model failure while obscuring others.

## Architectural Foundation

QuanTAlib implements error metrics with streaming efficiency. Most achieve O(1) updates via running sums and ring buffers. Two median-based metrics (MdAE, MdAPE) require O(n log n) sorting per update because median computation has no incremental shortcut. Mathematics sometimes refuses to cooperate.

### Performance Characteristics

| Complexity | Metrics | Notes |
| :--------- | :------ | :---- |
| **O(1) streaming** | 23 metrics | Running sum with periodic resync |
| **O(n log n) streaming** | MdAE, MdAPE | Sorting required for median |
| **SIMD batch** | 15 metrics | AVX2 via ErrorHelpers |
| **Zero allocation** | All | stackalloc for buffers ≤256 |

The O(1) indicators maintain running sums with periodic recalculation (every 1000 ticks) to prevent floating-point drift accumulation. Trust but verify, even with arithmetic.

## Metric Categories

### 1. Absolute Error Metrics

These measure error in original units. Directly interpretable. No percentage gymnastics.

#### MAE (Mean Absolute Error)

$$\text{MAE} = \frac{1}{n} \sum_{i=1}^{n} |y_i - \hat{y}_i|$$

The workhorse. Robust to outliers (linear penalty). Reports error in same units as data.

**When to use**: Default choice when interpretability matters.

**Weakness**: Scale-dependent. Cannot compare models across different series.

#### RMSE (Root Mean Squared Error)

$$\text{RMSE} = \sqrt{\frac{1}{n} \sum_{i=1}^{n} (y_i - \hat{y}_i)^2}$$

Quadratic penalty amplifies large errors. Standard in optimization because squared loss has smooth gradients.

**When to use**: When large errors are disproportionately costly.

**Weakness**: Single outlier can dominate. RMSE of 100 might mean "consistently off by 100" or "perfect except one error of 1000." The metric does not distinguish these scenarios; sleep-deprived analysts must.

#### MSE (Mean Squared Error)

$$\text{MSE} = \frac{1}{n} \sum_{i=1}^{n} (y_i - \hat{y}_i)^2$$

RMSE squared. Units are squared, making interpretation awkward, but mathematically convenient for optimization.

#### MdAE (Median Absolute Error)

$$\text{MdAE} = \text{median}(|y_i - \hat{y}_i|)$$

The median ignores up to 50% outliers. Maximum breakdown point achievable.

**When to use**: Contaminated data where outliers are measurement errors, not signal.

**Weakness**: O(n log n) per update. Ignores error magnitude beyond the median.

### 2. Percentage Error Metrics

Normalize by actual values for scale-independence. Percentage interpretation aids communication with stakeholders who fled mathematics long ago.

#### MAPE (Mean Absolute Percentage Error)

$$\text{MAPE} = \frac{100}{n} \sum_{i=1}^{n} \left| \frac{y_i - \hat{y}_i}{y_i} \right|$$

The classic percentage error. "Model is off by X% on average."

**When to use**: Comparing accuracy across series with different scales.

**Fatal flaw**: Undefined when $y_i = 0$. Asymmetric in ways that surprise: underestimation bounded at 100%, overestimation unbounded. A prediction of 200 for actual 100 gives 100% error. A prediction of 0 for actual 100 also gives 100% error. A prediction of 100 for actual 200 gives 50% error. This asymmetry biases models toward underprediction without telling anyone.

#### SMAPE (Symmetric Mean Absolute Percentage Error)

$$\text{SMAPE} = \frac{100}{n} \sum_{i=1}^{n} \frac{|y_i - \hat{y}_i|}{(|y_i| + |\hat{y}_i|) / 2}$$

Bounded 0-200%. Symmetric treatment of over/underprediction.

**When to use**: When MAPE's asymmetry is problematic.

**Weakness**: Still sensitive to near-zero actuals. The "symmetric" claim is debated in literature with more vigor than most academic disputes.

#### MAPD (Mean Absolute Percentage Deviation)

$$\text{MAPD} = \frac{100}{n} \sum_{i=1}^{n} \frac{2|y_i - \hat{y}_i|}{|y_i| + |\hat{y}_i|}$$

Alternative symmetric formulation. Denominator uses sum rather than average.

#### MdAPE (Median Absolute Percentage Error)

$$\text{MdAPE} = \text{median}\left( \left| \frac{y_i - \hat{y}_i}{y_i} \right| \times 100 \right)$$

Median variant of MAPE. Robust to outlier percentage errors.

#### MAAPE (Mean Arctangent Absolute Percentage Error)

$$\text{MAAPE} = \frac{1}{n} \sum_{i=1}^{n} \arctan\left( \left| \frac{y_i - \hat{y}_i}{y_i} \right| \right)$$

Arctangent bounds the penalty for large percentage errors. Range: $[0, \pi/2)$.

**When to use**: When percentage errors can be legitimately huge (early-stage forecasts, sparse data).

### 3. Bias Detection Metrics

Signed errors reveal systematic over/underprediction.

#### ME (Mean Error)

$$\text{ME} = \frac{1}{n} \sum_{i=1}^{n} (y_i - \hat{y}_i)$$

Positive ME: model underpredicts. Negative ME: model overpredicts.

**When to use**: Detecting systematic bias.

**Weakness**: Errors can cancel. ME near zero does not imply accuracy, just balanced wrongness.

#### MPE (Mean Percentage Error)

$$\text{MPE} = \frac{100}{n} \sum_{i=1}^{n} \frac{y_i - \hat{y}_i}{y_i}$$

Signed percentage bias.

### 4. Relative/Scaled Metrics

Compare model performance against baseline predictors.

#### MASE (Mean Absolute Scaled Error)

$$\text{MASE} = \frac{\text{MAE}}{\frac{1}{n-1} \sum_{i=2}^{n} |y_i - y_{i-1}|}$$

Scales by naive forecast error (random walk). MASE < 1 beats naive.

**When to use**: Scale-free comparison. The recommended metric for M-competitions.

**Key insight**: Denominator is the MAE of predicting "tomorrow equals today." If the model cannot beat this, reconsider the approach. Seriously.

#### RAE (Relative Absolute Error)

$$\text{RAE} = \frac{\sum |y_i - \hat{y}_i|}{\sum |y_i - \bar{y}|}$$

Normalizes by mean predictor error. RAE < 1 beats predicting the mean.

#### RSE (Relative Squared Error)

$$\text{RSE} = \frac{\sum (y_i - \hat{y}_i)^2}{\sum (y_i - \bar{y})^2}$$

Squared variant of RAE. Related to R² by: $R^2 = 1 - \text{RSE}$.

#### R² (Coefficient of Determination)

$$R^2 = 1 - \frac{\sum (y_i - \hat{y}_i)^2}{\sum (y_i - \bar{y})^2}$$

Proportion of variance explained. R² = 1 is perfect. R² = 0 equals mean predictor. R² < 0 is worse than predicting the mean (a humbling outcome).

**When to use**: Model quality assessment. Widely understood.

**Weakness**: Can be misleading for nonlinear relationships. High R² does not guarantee good predictions.

**Implementation note**: QuanTAlib's R² uses a streaming-optimized incremental formula where TSS (Total Sum of Squares) is accumulated using the running mean at each point in time. This differs from the textbook formula where TSS uses the final window mean for all values. The implementation is validated for internal consistency (streaming vs batch modes produce identical results) and mathematical properties (perfect prediction returns 1.0, bounded at 1.0 from above).

#### Theil's U

$$U = \frac{\sqrt{\sum (y_i - \hat{y}_i)^2}}{\sqrt{\sum y_i^2 + \sum \hat{y}_i^2}}$$

Bounded [0, 1] for reasonable forecasts. U > 1 indicates worse than naive.

### 5. Logarithmic Metrics

For multiplicative errors or data spanning orders of magnitude.

#### MSLE (Mean Squared Logarithmic Error)

$$\text{MSLE} = \frac{1}{n} \sum_{i=1}^{n} (\log(1 + y_i) - \log(1 + \hat{y}_i))^2$$

Penalizes underestimation more than overestimation in relative terms. Useful when target spans orders of magnitude.

**When to use**: Population counts, revenue forecasts, anything with exponential growth.

#### RMSLE (Root Mean Squared Logarithmic Error)

$$\text{RMSLE} = \sqrt{\text{MSLE}}$$

Interpretable version of MSLE.

#### MRAE (Mean Relative Absolute Error)

$$\text{MRAE} = \frac{1}{n} \sum_{i=1}^{n} \left| \frac{y_i - \hat{y}_i}{y_i - y_{i-1}} \right|$$

Relative error scaled by naive forecast change.

### 6. Robust Loss Functions

Control outlier sensitivity through parameterization.

#### Huber Loss

$$L_\delta(a) = \begin{cases} \frac{1}{2}a^2 & \text{if } |a| \le \delta \\ \delta(|a| - \frac{1}{2}\delta) & \text{otherwise} \end{cases}$$

Quadratic for small errors, linear for large. Parameter δ controls transition.

**When to use**: Want MSE's smoothness but MAE's robustness. Default δ = 1.35 approximates 95% efficiency at Gaussian errors.

#### Pseudo-Huber Loss

$$L_\delta(a) = \delta^2 \left( \sqrt{1 + (a/\delta)^2} - 1 \right)$$

Smooth approximation to Huber. Differentiable everywhere, unlike Huber which has a kink at δ.

**When to use**: Gradient-based optimization where Huber's non-differentiability at the transition point causes issues. Provides same robustness as Huber with better numerical properties.

**Advantage over Huber**: Continuous second derivative enables faster convergence in Newton-type optimizers.

#### Tukey Biweight

$$\rho(u) = \begin{cases} \frac{c^2}{6}\left[1 - \left(1 - \left(\frac{u}{c}\right)^2\right)^3\right] & \text{if } |u| \le c \\ \frac{c^2}{6} & \text{otherwise} \end{cases}$$

Completely ignores errors beyond threshold c. Maximum robustness (breakdown point approaches 50%).

**When to use**: When outliers should have zero influence, not just reduced influence.

#### Log-Cosh Loss

$$L(a) = \log(\cosh(a))$$

Approximately quadratic for small a, approximately linear for large a. Smooth everywhere.

#### Quantile Loss

$$L_\tau(a) = \begin{cases} \tau \cdot a & \text{if } a \ge 0 \\ (\tau - 1) \cdot a & \text{otherwise} \end{cases}$$

Asymmetric loss for quantile regression. τ = 0.5 gives MAE. τ = 0.9 penalizes underprediction 9× more than overprediction.

**When to use**: When cost of over/underprediction differs.

### 7. Weighted Metrics

#### WMAPE (Weighted Mean Absolute Percentage Error)

$$\text{WMAPE} = \frac{\sum |y_i - \hat{y}_i|}{\sum |y_i|} \times 100$$

Weights errors by actual values. High-value items contribute more.

**When to use**: Demand forecasting where total volume matters more than per-item accuracy.

## Comparison Matrix

| Metric | Scale | Outlier Robust | Bias Detect | Near-Zero Safe | Complexity |
| :----- | :---- | :------------- | :---------- | :------------- | :--------- |
| MAE | Original | ✓ | ✗ | ✓ | O(1) |
| RMSE | Original | ✗ | ✗ | ✓ | O(1) |
| MSE | Squared | ✗ | ✗ | ✓ | O(1) |
| MdAE | Original | ✓✓ | ✗ | ✓ | O(n log n) |
| MAPE | Percentage | ○ | ✗ | ✗ | O(1) |
| SMAPE | 0-200% | ○ | ✗ | ○ | O(1) |
| MdAPE | Percentage | ✓✓ | ✗ | ✗ | O(n log n) |
| ME | Original | ✗ | ✓ | ✓ | O(1) |
| MPE | Percentage | ✗ | ✓ | ✗ | O(1) |
| MASE | Scaled | ✓ | ✗ | ✓ | O(1) |
| R² | 0-1 | ✗ | ✗ | ✓ | O(1) |
| Huber | Original | ✓ | ✗ | ✓ | O(1) |
| Pseudo-Huber | Original | ✓ | ✗ | ✓ | O(1) |
| Quantile | Original | ○ | Asymmetric | ✓ | O(1) |
| Log-Cosh | Original | ✓ | ✗ | ✓ | O(1) |
| Tukey | Original | ✓✓ | ✗ | ✓ | O(1) |

Legend: ✓ Yes, ✗ No, ○ Partial, ✓✓ Very

## Selection Guidelines

### Decision Tree

```text
Is interpretability critical?
├─ Yes → MAE (everyone understands "off by X units")
└─ No → Continue

Are large errors catastrophic?
├─ Yes → RMSE or MSE
└─ No → Continue

Is data contaminated with outliers?
├─ Yes → Pseudo-Huber (smooth), Huber (tunable), or MdAE (maximum robustness)
└─ No → Continue

Need scale-free comparison?
├─ Yes, time series → MASE
├─ Yes, general → R² or RAE
└─ No → Continue

Is near-zero actual possible?
├─ Yes → Avoid MAPE. Use SMAPE, MAE, or MASE
└─ No → MAPE acceptable

Different cost for over/under?
├─ Yes → Quantile Loss
└─ No → Symmetric metrics
```

### By Domain

| Domain | Primary | Secondary | Notes |
| :----- | :------ | :-------- | :---- |
| Financial trading | MAE, RMSE | Pseudo-Huber, Huber | Prices cross zero; avoid MAPE |
| Demand forecasting | WMAPE, MASE | SMAPE | Total volume matters |
| Model comparison | MASE, R² | RAE | Scale-free required |
| Academic papers | RMSE, R² | MAE | Field conventions vary |
| Production monitoring | MAE, ME | MAPE | Simplicity wins at 3 AM |
| ML model training | Pseudo-Huber, Log-Cosh | Huber | Outlier resistance + smooth gradients |

## Implementation Details

### Usage Pattern

All error metrics follow consistent dual-input API:

```csharp
// Streaming (O(1) per update for most metrics)
var mae = new Mae(period: 20);
foreach (var (actual, predicted) in data)
{
    var error = mae.Update(actual, predicted);
    Console.WriteLine($"Rolling MAE: {error.Value:F4}");
}

// Batch (SIMD-accelerated where applicable)
var maeSeries = Mae.Calculate(actualSeries, predictedSeries, period: 20);

// Zero-allocation span
Span<double> output = stackalloc double[data.Length];
Mae.Batch(actualSpan, predictedSpan, output, period: 20);
```

### Bar Correction Support

All metrics support intra-bar updates via `isNew` parameter:

```csharp
// New bar
mae.Update(actual, predicted, isNew: true);

// Same bar, updated value
mae.Update(actual, revisedPredicted, isNew: false);
```

### NaN Handling

Invalid inputs (NaN, Infinity) trigger last-valid-value substitution:

```csharp
mae.Update(100.0, 95.0);      // Normal
mae.Update(double.NaN, 96.0); // Uses last valid actual (100.0)
mae.Update(101.0, 97.0);      // Normal, updates last valid
```

## Common Pitfalls

### MAPE Division Chaos

```csharp
// Actual = 0.001, Predicted = 0.002
// MAPE = |0.001 - 0.002| / |0.001| * 100 = 100%
// Actual = 100, Predicted = 101
// MAPE = |100 - 101| / |100| * 100 = 1%
// Same absolute error (1 unit), vastly different MAPE
```

### RMSE Outlier Amplification

```csharp
// 99 predictions: error = 1 each → contribution = 99
// 1 prediction: error = 100 → contribution = 10,000
// RMSE dominated by single outlier
```

### R² Misinterpretation

```csharp
// R² = 0.95 sounds great
// But if predicting stock returns, 0.95 might be overfitting
// For some problems, R² = 0.3 is excellent
// R² < 0 is possible (worse than mean prediction)
```

### Metric Selection Regret

Choosing MAPE for a dataset that occasionally touches zero. Choosing RMSE for data with known outliers. Choosing R² and celebrating 0.99 without checking for overfitting. These mistakes compound in production. Selection happens once; consequences repeat daily.

## Performance Benchmarks

Measured on 10,000-element series, period = 20:

| Test Environment | Specification |
| :--------------- | :------------ |
| CPU | Intel i9-13900K |
| Series Length | 10,000 elements |
| Period | 20 |
| Framework | .NET 8.0 |

| Metric | Streaming (ns/update) | Batch (ns/element) | Speedup |
| :----- | --------------------: | -----------------: | ------: |
| MAE | 12 | 3.2 | 3.75× |
| RMSE | 14 | 3.8 | 3.68× |
| MAPE | 15 | 4.1 | 3.66× |
| Huber | 18 | 5.2 | 3.46× |
| R² | 45 | 12 | 3.75× |
| MASE | 28 | 8.5 | 3.29× |
| MdAE | 850 | 420 | 2.02× |

Batch mode achieves 3-4× throughput via SIMD (AVX2) for applicable metrics. MdAE's modest 2× improvement reflects sorting overhead that vectorization cannot eliminate.

## References

1. Hyndman, R.J., & Koehler, A.B. (2006). Another look at measures of forecast accuracy. *International Journal of Forecasting*, 22(4), 679-688.
2. Makridakis, S. (1993). Accuracy measures: theoretical and practical concerns. *International Journal of Forecasting*, 9(4), 527-529.
3. Armstrong, J.S., & Collopy, F. (1992). Error measures for generalizing about forecasting methods: Empirical comparisons. *International Journal of Forecasting*, 8(1), 69-80.
4. Chai, T., & Draxler, R.R. (2014). Root mean square error (RMSE) or mean absolute error (MAE)? *Geoscientific Model Development*, 7(3), 1247-1250.