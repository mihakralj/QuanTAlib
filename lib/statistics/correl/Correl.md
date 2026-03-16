# CORR: Pearson Correlation Coefficient

> *Correlation is not causation, but it sure is a hint. The market doesn't care why two instruments move together—only that they do, and whether that relationship will persist long enough for you to profit from it.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Statistic                        |
| **Inputs**       | Two series (X, Y)                       |
| **Parameters**   | `period` (default 20)                      |
| **Outputs**      | Single series (Pearson r)               |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [correl.pine](correl.pine)                       |

- The Pearson Correlation Coefficient measures the linear relationship between two variables, returning a value from -1 (perfect negative correlation...
- **Similar:** [Spearman](../spearman/Spearman.md), [Kendall](../kendall/Kendall.md) | **Trading note:** Pearson correlation; measures linear relationship strength. Used for portfolio diversification and pairs trading.
- Validated against TradingView reference behavior and mathematical invariants.

The Pearson Correlation Coefficient measures the linear relationship between two variables, returning a value from -1 (perfect negative correlation) to +1 (perfect positive correlation). Zero indicates no linear relationship. This implementation uses running sums for O(1) streaming updates, making it suitable for real-time analysis of price relationships.

## Historical Context

Karl Pearson formalized the correlation coefficient in the 1890s, building on earlier work by Francis Galton. The formula has remained unchanged for over a century because it elegantly captures what traders intuitively understand: when two instruments move together, there's an exploitable relationship.

Unlike cointegration (which tests for long-run equilibrium), correlation measures instantaneous co-movement. Two stocks can be highly correlated yet drift apart permanently—correlation tells you about direction, not destination. This distinction matters enormously for pairs trading: correlation helps with hedging and timing, but cointegration determines whether mean-reversion is statistically justified.

This implementation follows the PineScript reference, using circular buffers and running sums to achieve constant-time updates regardless of lookback period.

## Architecture & Physics

### 1. Running Sums Framework

The indicator maintains five running sums updated incrementally:

| Sum | Description | Formula |
| :--- | :--- | :--- |
| $S_X$ | Sum of X values | $\sum_{i=1}^{n} X_i$ |
| $S_Y$ | Sum of Y values | $\sum_{i=1}^{n} Y_i$ |
| $S_{X^2}$ | Sum of X squared | $\sum_{i=1}^{n} X_i^2$ |
| $S_{Y^2}$ | Sum of Y squared | $\sum_{i=1}^{n} Y_i^2$ |
| $S_{XY}$ | Sum of X×Y products | $\sum_{i=1}^{n} X_i Y_i$ |

### 2. Circular Buffer

A `RingBuffer` of capacity `period` stores paired values. When full, the oldest pair is subtracted from running sums before adding the new pair—maintaining O(1) complexity regardless of period length.

### 3. Correlation Formula

The Pearson coefficient is computed as:

$$r = \frac{\text{Cov}(X, Y)}{\sigma_X \cdot \sigma_Y}$$

Expanded using running sums:

$$r = \frac{n \cdot S_{XY} - S_X \cdot S_Y}{\sqrt{(n \cdot S_{X^2} - S_X^2)(n \cdot S_{Y^2} - S_Y^2)}}$$

Where $n$ is the number of observations (capped at `period`).

### 4. Edge Case Handling

| Condition | Result | Rationale |
| :--- | :--- | :--- |
| Zero variance in X or Y | NaN | Division by zero—undefined correlation |
| Insufficient data | NaN | Need at least 2 points |
| NaN/Infinity input | Last valid value | Substitution preserves series continuity |

## Mathematical Foundation

### Derivation from Covariance

Starting with the population covariance:

$$\text{Cov}(X, Y) = \frac{\sum(X_i - \bar{X})(Y_i - \bar{Y})}{n}$$

Expanding:

$$\text{Cov}(X, Y) = \frac{\sum X_i Y_i}{n} - \bar{X} \cdot \bar{Y}$$

$$= \frac{S_{XY}}{n} - \frac{S_X}{n} \cdot \frac{S_Y}{n}$$

$$= \frac{n \cdot S_{XY} - S_X \cdot S_Y}{n^2}$$

Similarly for standard deviations:

$$\sigma_X = \sqrt{\frac{S_{X^2}}{n} - \left(\frac{S_X}{n}\right)^2} = \frac{\sqrt{n \cdot S_{X^2} - S_X^2}}{n}$$

Combining:

$$r = \frac{\text{Cov}(X, Y)}{\sigma_X \cdot \sigma_Y} = \frac{n \cdot S_{XY} - S_X \cdot S_Y}{\sqrt{(n \cdot S_{X^2} - S_X^2)(n \cdot S_{Y^2} - S_Y^2)}}$$

### Update Mechanics

When a new pair $(x_{new}, y_{new})$ arrives and an old pair $(x_{old}, y_{old})$ exits the window:

$$S_X \leftarrow S_X - x_{old} + x_{new}$$
$$S_Y \leftarrow S_Y - y_{old} + y_{new}$$
$$S_{X^2} \leftarrow S_{X^2} - x_{old}^2 + x_{new}^2$$
$$S_{Y^2} \leftarrow S_{Y^2} - y_{old}^2 + y_{new}^2$$
$$S_{XY} \leftarrow S_{XY} - x_{old} \cdot y_{old} + x_{new} \cdot y_{new}$$

This achieves O(1) per-bar complexity.

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ADD/SUB | 12 | 1 | 12 |
| MUL | 8 | 3 | 24 |
| DIV | 1 | 15 | 15 |
| SQRT | 1 | 15 | 15 |
| Buffer Access | 2 | 3 | 6 |
| **Total** | **24** | — | **~72 cycles** |

Correlation is significantly cheaper than cointegration (~72 vs ~282 cycles) because it doesn't require the ADF regression step.

### Memory Footprint

| Component | Size |
| :--- | :--- |
| Ring buffer (period × 2 doubles) | 16 × period bytes |
| Running sums (5 doubles) | 40 bytes |
| State variables | 32 bytes |
| **Total per instance** | **~16 × period + 72 bytes** |

For period=20: ~392 bytes per indicator instance.

### Batch Mode (SIMD Potential)

The correlation formula is not directly SIMD-friendly due to the final division and square root. However, the running sum accumulation phase can benefit from vectorization when processing batches:

| Phase | SIMD Benefit |
| :--- | :--- |
| Sum accumulation | 4-8× (AVX2/AVX-512) |
| Final formula | 1× (scalar) |
| **Overall improvement** | ~2-3× for batch processing |

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 10/10 | Exact Pearson formula |
| **Timeliness** | 8/10 | Responsive to recent changes |
| **Robustness** | 9/10 | Handles edge cases gracefully |
| **Interpretability** | 10/10 | Universal [-1, +1] scale |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | No correlation implementation |
| **Skender** | N/A | No direct correlation (has Beta) |
| **Tulip** | N/A | No correlation implementation |
| **Ooples** | N/A | No correlation implementation |
| **TradingView** | ✅ | Matches PineScript `ta.correlation()` |
| **Mathematical** | ✅ | Validated against known properties |

Note: Correlation is typically found in statistical packages rather than TA libraries. This implementation validates against mathematical properties (symmetry, boundedness, scale invariance) and the PineScript reference.

## Use Cases

### 1. Hedging

Find correlated instruments to offset risk:
- **r > 0.7**: Strong positive correlation, use for portfolio diversification analysis
- **r < -0.7**: Strong negative correlation, natural hedges

### 2. Pairs Trading (Short-Term)

Identify co-moving pairs for short-term mean reversion:
- High correlation indicates pairs move together
- Combine with cointegration for statistical justification

### 3. Sector Analysis

Measure how closely a stock tracks its sector or index:
- Rolling correlation reveals changing relationships
- Divergence from sector may signal alpha opportunities

### 4. Risk Management

Monitor correlation stability:
- Correlations tend toward 1 during market stress
- "Correlation breakdown" can devastate hedged portfolios

## API Usage

### Streaming Mode (Bi-Input)

```csharp
var corr = new Correl(period: 20);
foreach (var (priceA, priceB) in pricePairs)
{
    var result = corr.Update(priceA, priceB);
    if (corr.IsHot)
    {
        Console.WriteLine($"Correlation: {result.Value:F4}");
    }
}
```

### Batch Mode

```csharp
var seriesA = new TSeries();
var seriesB = new TSeries();
// ... populate series ...
var results = Correl.Calculate(seriesA, seriesB, period: 20);
```

### Span Mode (Zero Allocation)

```csharp
double[] pricesA = new double[1000];
double[] pricesB = new double[1000];
double[] output = new double[1000];
// ... populate inputs ...
Correl.Batch(pricesA.AsSpan(), pricesB.AsSpan(), output.AsSpan(), period: 20);
```

### Bar Correction Support

```csharp
var corr = new Correl(20);

// New bar
corr.Update(100.0, 50.0, isNew: true);  // r = 0.85

// Same bar corrected (e.g., real-time tick update)
corr.Update(101.0, 51.0, isNew: false); // Recalculates without advancing state
```

## Interpreting Results

| Correlation | Interpretation |
| :---: | :--- |
| **+0.7 to +1.0** | Strong positive: move in same direction |
| **+0.3 to +0.7** | Moderate positive |
| **-0.3 to +0.3** | Weak or no linear relationship |
| **-0.7 to -0.3** | Moderate negative |
| **-1.0 to -0.7** | Strong negative: move in opposite directions |

**Warning**: Correlation only measures *linear* relationships. Two variables with a perfect quadratic relationship (Y = X²) may show r ≈ 0.

## Common Pitfalls

1. **Confusing Correlation with Causation**: High correlation does not imply one variable causes changes in the other. Both may be driven by a third factor (confounding).

2. **Assuming Stability**: Correlations change over time. A 0.9 correlation over the past year doesn't guarantee 0.9 tomorrow. Rolling correlation reveals regime changes.

3. **Ignoring Non-Linear Relationships**: Pearson correlation misses curvilinear dependencies. If you suspect non-linear relationships, consider Spearman rank correlation instead.

4. **Crisis Correlation Spike**: During market stress, correlations tend toward 1.0 (or -1.0 for inverse ETFs). Diversification benefits evaporate precisely when you need them most.

5. **Lookback Period Selection**: Short periods (5-10) are noisy but responsive. Long periods (50-100) are stable but slow to adapt. Match the period to your trading horizon.

6. **Zero-Variance Edge Case**: If either series is constant within the window, variance is zero and correlation is undefined (NaN). This is mathematically correct.

7. **Warmup Period**: The indicator requires `period` bars before producing valid results. During warmup, `IsHot` returns false.

8. **Outlier Sensitivity**: Pearson correlation is sensitive to outliers. A single extreme observation can dramatically shift the coefficient. Consider winsorizing data or using Spearman for robustness.

## Correlation vs Cointegration

| Aspect | Correlation | Cointegration |
| :--- | :--- | :--- |
| **Measures** | Linear co-movement | Long-run equilibrium |
| **Range** | [-1, +1] | ADF statistic (unbounded) |
| **Time horizon** | Short-term | Long-term |
| **Use case** | Hedging, risk | Pairs trading |
| **Computational cost** | ~72 cycles | ~282 cycles |
| **Stationarity required** | No | Yes (I(1) series) |

**Rule of thumb**: Use correlation for hedging and short-term analysis. Use cointegration for pairs trading and mean-reversion strategies.

## References

- Pearson, K. (1895). "Notes on regression and inheritance in the case of two parents." *Proceedings of the Royal Society of London*, 58, 240-242.
- TradingView. "ta.correlation() function." *Pine Script Language Reference Manual*.
- Vidyamurthy, G. (2004). "Pairs Trading: Quantitative Methods and Analysis." *Wiley Finance*. Chapter on correlation analysis.
- Embrechts, P., McNeil, A., & Straumann, D. (2002). "Correlation and dependence in risk management: properties and pitfalls." *Risk Management: Value at Risk and Beyond*, Cambridge University Press.