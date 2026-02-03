# Cointegration: Engle-Granger Two-Step Cointegration Test

> "Correlation tells you they move together. Cointegration tells you they're bound together. Two stocks can be uncorrelated yet cointegrated, or perfectly correlated yet destined to drift apart forever. The difference between 'similar direction' and 'shared destiny' is the difference between a tourist attraction and a gravitational orbit."

The Cointegration indicator measures the long-run equilibrium relationship between two price series using the Engle-Granger two-step method with an Augmented Dickey-Fuller (ADF) test. Unlike correlation, which measures short-term co-movement, cointegration tests whether two non-stationary series share a common stochastic trend—meaning they may diverge temporarily but are statistically bound to revert to their equilibrium relationship.

## Historical Context

Cointegration was developed by Nobel laureates Clive Granger and Robert Engle in the 1980s, fundamentally changing how economists and traders think about relationships between time series. Their work addressed a critical problem: traditional regression on non-stationary data (like stock prices) produces spurious results—apparent relationships that are statistically meaningless.

The Engle-Granger (1987) two-step method remains the most widely used approach:
1. Estimate the cointegrating regression
2. Test the residuals for stationarity using the ADF test

This implementation follows the PineScript reference implementation, adapting the algorithm for O(1) streaming updates using running sums and ring buffers.

## Architecture & Physics

### The Mean-Reversion Mechanism

Cointegrated series exhibit an error-correction mechanism: when they diverge from equilibrium, market forces conspire to pull them back. This differs fundamentally from correlation:

| Property | Correlation | Cointegration |
| :--- | :--- | :--- |
| **Measures** | Direction similarity | Long-run equilibrium |
| **Horizon** | Short-term | Long-term |
| **Stability** | Can vary over time | Structural relationship |
| **Trading implication** | Momentum | Mean-reversion |

### 1. Linear Regression Component

The first step estimates the equilibrium relationship:

$$A_t = \alpha + \beta \cdot B_t + \epsilon_t$$

Where:
- $\alpha$ = intercept (hedge ratio offset)
- $\beta$ = slope coefficient (hedge ratio)
- $\epsilon_t$ = residual (spread)

The regression coefficients are derived from correlation and standard deviations:

$$\beta = \rho_{AB} \cdot \frac{\sigma_A}{\sigma_B}$$

$$\alpha = \bar{A} - \beta \cdot \bar{B}$$

### 2. Residual Calculation

The spread (residual) represents the deviation from equilibrium:

$$\epsilon_t = A_t - (\alpha + \beta \cdot B_t)$$

For cointegrated series, this spread should be stationary (mean-reverting).

### 3. Augmented Dickey-Fuller Test

The ADF test checks if residuals are stationary by testing for a unit root:

$$\Delta\epsilon_t = \gamma \cdot \epsilon_{t-1} + u_t$$

Where:
- $\Delta\epsilon_t = \epsilon_t - \epsilon_{t-1}$ (first difference)
- $\gamma$ = coefficient indicating mean-reversion speed
- $u_t$ = regression error

The ADF statistic is:

$$\text{ADF} = \frac{\gamma}{\text{SE}(\gamma)}$$

Where $\text{SE}(\gamma) = \sqrt{\frac{\text{Var}(u)}{\text{Var}(\epsilon_{t-1})}}$

### 4. Interpretation

| ADF Statistic | Interpretation |
| :---: | :--- |
| < -3.43 | Strong cointegration (1% significance) |
| < -2.86 | Cointegration (5% significance) |
| < -2.57 | Weak cointegration (10% significance) |
| > -2.57 | No evidence of cointegration |

More negative values indicate stronger evidence that the series share a long-run equilibrium.

## Mathematical Foundation

### Running Statistics for O(1) Updates

This implementation maintains running sums for efficient streaming computation:

**Means:**
$$\bar{A} = \frac{\sum A_i}{n}, \quad \bar{B} = \frac{\sum B_i}{n}$$

**Variances:**
$$\sigma_A^2 = \frac{\sum A_i^2}{n} - \bar{A}^2, \quad \sigma_B^2 = \frac{\sum B_i^2}{n} - \bar{B}^2$$

**Covariance:**
$$\text{Cov}(A, B) = \frac{\sum A_i B_i}{n} - \bar{A} \cdot \bar{B}$$

**Correlation:**
$$\rho_{AB} = \frac{\text{Cov}(A, B)}{\sigma_A \cdot \sigma_B}$$

### ADF Regression Statistics

The gamma coefficient is computed using running sums over period-1 observations:

$$\gamma = \frac{\text{Cov}(\Delta\epsilon, \epsilon_{t-1})}{\text{Var}(\epsilon_{t-1})}$$

**Standard Error:**
$$\text{SE}(\gamma)^2 = \frac{\sum(u_t)^2 / n}{\text{Var}(\epsilon_{t-1})}$$

where $u_t = \Delta\epsilon_t - \gamma \cdot \epsilon_{t-1}$

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| ADD/SUB | 25 | 1 | 25 |
| MUL | 12 | 3 | 36 |
| DIV | 8 | 15 | 120 |
| SQRT | 3 | 15 | 45 |
| Buffer Access | 8 | 3 | 24 |
| FMA | 8 | 4 | 32 |
| **Total** | **64** | — | **~282 cycles** |

Division and square root operations dominate the cost profile.

### Memory Footprint

| Component | Size |
| :--- | :--- |
| Main buffers (2× period) | 16 × period bytes |
| ADF buffers (2× period-1) | 16 × (period-1) bytes |
| Running sums | 80 bytes |
| State variables | 64 bytes |
| **Total per instance** | **~32 × period + 144 bytes** |

For period=20: ~784 bytes per indicator instance.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | Matches Engle-Granger methodology |
| **Timeliness** | 6/10 | Requires full period for stable estimates |
| **Robustness** | 8/10 | Handles edge cases (NaN, zero variance) |
| **Interpretability** | 7/10 | Requires understanding critical values |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | No cointegration implementation |
| **Skender** | N/A | No cointegration implementation |
| **Tulip** | N/A | No cointegration implementation |
| **Ooples** | N/A | No cointegration implementation |
| **TradingView** | ✅ | Matches PineScript reference implementation |
| **Statistical** | ✅ | Validated against expected properties |

Note: Cointegration is typically found in econometrics packages (statsmodels, R's urca) rather than TA libraries. This implementation focuses on streaming computation suitable for real-time trading.

## Use Cases

### 1. Pairs Trading

Identify cointegrated pairs for mean-reversion strategies:
- **Entry**: When spread deviates significantly from mean
- **Exit**: When spread reverts to equilibrium
- **Stop**: When cointegration breaks down

### 2. Statistical Arbitrage

Build market-neutral portfolios using cointegrated baskets:
- Long undervalued leg, short overvalued leg
- Position sizing based on hedge ratio (β)

### 3. Risk Management

Monitor cointegration stability:
- Degrading ADF statistics signal relationship breakdown
- Adjust positions before pairs diverge permanently

### 4. Index Tracking

Construct synthetic indices from cointegrated components:
- Track expensive ETFs with cheaper alternatives
- Exploit tracking errors

## API Usage

### Streaming Mode (Bi-Input)

```csharp
var coint = new Cointegration(period: 20);
foreach (var (priceA, priceB) in pricePairs)
{
    var result = coint.Update(priceA, priceB);
    if (coint.IsHot && result.Value < -2.86)
    {
        Console.WriteLine($"Cointegrated at 5% level: ADF = {result.Value:F2}");
    }
}
```

### Batch Mode

```csharp
var seriesA = new TSeries();
var seriesB = new TSeries();
// ... populate series ...
var results = Cointegration.Calculate(seriesA, seriesB, period: 20);
```

### Span Mode (Zero Allocation)

```csharp
double[] pricesA = new double[1000];
double[] pricesB = new double[1000];
double[] output = new double[1000];
// ... populate inputs ...
Cointegration.Calculate(pricesA.AsSpan(), pricesB.AsSpan(), output.AsSpan(), period: 20);
```

### Bar Correction Support

```csharp
var coint = new Cointegration(20);

// New bar
coint.Update(100.0, 50.0, isNew: true);  // ADF = -2.5

// Same bar corrected (e.g., real-time tick update)
coint.Update(101.0, 51.0, isNew: false); // Recalculates without advancing state
```

## Common Pitfalls

1. **Confusing Correlation with Cointegration**: High correlation does not imply cointegration. Two trending stocks can be 99% correlated but not cointegrated (spurious regression). Conversely, mean-reverting pairs may have low correlation but strong cointegration.

2. **Warmup Period**: The indicator requires `period + 1` bars before producing valid results. During warmup, `IsHot` returns false and results may be NaN.

3. **Critical Values**: ADF critical values are approximate: -3.43 (1%), -2.86 (5%), -2.57 (10%). These differ from standard t-distribution values due to the unit root null hypothesis.

4. **Zero-Variance Edge Cases**: Perfectly linear relationships (A = β×B + α with no noise) produce zero-variance residuals, resulting in NaN. This is mathematically correct—perfect cointegration has no estimation uncertainty.

5. **Non-Stationarity Requirement**: Both input series should be integrated of order 1 (I(1))—non-stationary but with stationary first differences. Applying cointegration to already-stationary series is meaningless.

6. **Period Selection**: Short periods (10-20) respond faster but may produce unstable estimates. Longer periods (50-100) are more stable but slower to adapt. Consider the expected holding period for your trading strategy.

7. **Structural Breaks**: Cointegration can break down due to fundamental changes (mergers, regulatory shifts, market regime changes). Monitor ADF statistics over time and be prepared to exit when the relationship deteriorates.

8. **Memory per Instance**: Each indicator instance allocates ~32×period bytes for buffers. For scanning many pairs, consider batch processing or pooling.

## When to Use Cointegration

**Use it when:**
- Building pairs trading or statistical arbitrage strategies
- Identifying mean-reversion opportunities across related instruments
- Validating hedge ratios for portfolio construction
- Monitoring relationship stability over time

**Skip it when:**
- Series are already stationary (use correlation instead)
- Looking for momentum/trend signals
- Short-term (intraday) trading where co-movement matters more than equilibrium
- One-off analysis where econometrics packages (statsmodels) are more appropriate

## References

- Engle, R.F. and Granger, C.W.J. (1987). "Co-integration and Error Correction: Representation, Estimation, and Testing." *Econometrica*, 55(2), 251-276.
- Dickey, D.A. and Fuller, W.A. (1979). "Distribution of the Estimators for Autoregressive Time Series with a Unit Root." *Journal of the American Statistical Association*, 74(366), 427-431.
- TradingView. "Cointegration Indicator (PineScript)." *TradingView Community Scripts*.
- Vidyamurthy, G. (2004). "Pairs Trading: Quantitative Methods and Analysis." *Wiley Finance*.