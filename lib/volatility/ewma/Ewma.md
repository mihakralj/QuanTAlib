# EWMA: Exponentially Weighted Moving Average Volatility

> "The past doesn't repeat itself, but it does rhyme—and EWMA captures the rhythm of volatility with exponential memory."

EWMA Volatility calculates market volatility using an exponentially weighted moving average of squared log returns with bias correction. Unlike simple historical volatility that weights all observations equally, EWMA gives more weight to recent observations while still considering historical data, making it more responsive to current market conditions.

## Historical Context

Exponentially Weighted Moving Average volatility emerged from J.P. Morgan's RiskMetrics methodology in the 1990s. The approach addressed a key limitation of simple historical volatility: equal weighting of all past observations regardless of age. Financial practitioners recognized that recent market movements often provide more relevant information about current risk than distant historical data.

The original RiskMetrics Technical Document (1996) proposed a "decay factor" (λ) of 0.94 for daily data, meaning approximately 6% weight goes to the most recent observation. This implementation uses an equivalent RMA (Running Moving Average) formulation with period-based smoothing plus bias correction to address the initialization problem that affects early estimates.

## Architecture & Physics

### 1. Log Return Calculation

The foundation uses continuously compounded returns:

$$
r_t = \ln\left(\frac{P_t}{P_{t-1}}\right)
$$

Log returns are preferred over simple returns because:
- They are additive across time periods
- They are symmetric (±10% moves have similar magnitude)
- They approximate percentage changes for small movements

### 2. RMA Smoothing of Squared Returns

The squared returns are smoothed using RMA (Running Moving Average):

$$
\text{RMA}_t = \frac{\text{RMA}_{t-1} \times (period - 1) + r_t^2}{period}
$$

This is equivalent to an EMA with smoothing factor $\alpha = 1/period$:

$$
\text{RMA}_t = (1 - \alpha) \times \text{RMA}_{t-1} + \alpha \times r_t^2
$$

### 3. Bias Correction

The bias correction factor addresses the initialization problem where early estimates are biased toward zero:

$$
e_t = (1 - \alpha)^t
$$

$$
\text{CorrectedVariance}_t = \frac{\text{RMA}_t}{1 - e_t}
$$

As $t \to \infty$, the correction factor approaches 1, having negligible effect on mature estimates.

### 4. Volatility Output

The volatility is the square root of the corrected variance:

$$
\sigma_t = \sqrt{\text{CorrectedVariance}_t}
$$

With optional annualization:

$$
\sigma_{annual} = \sigma_t \times \sqrt{T}
$$

where $T$ is the number of periods per year (252 for daily, 52 for weekly, 12 for monthly).

## Mathematical Foundation

### Decay Factor Relationship

The period parameter maps to the traditional RiskMetrics decay factor:

$$
\lambda = \frac{period - 1}{period} = 1 - \frac{1}{period}
$$

For period = 20: $\lambda = 0.95$ (5% weight on new observation)
For period = 10: $\lambda = 0.90$ (10% weight on new observation)

### Effective Window

The effective window (where ~95% of weight is concentrated) is approximately:

$$
\text{EffectiveWindow} \approx \frac{2}{\alpha} = 2 \times period
$$

### Bias Correction Derivation

The uncorrected RMA is a biased estimator because:

$$
E[\text{RMA}_t] = E[r^2] \times (1 - (1-\alpha)^t)
$$

Dividing by $(1 - (1-\alpha)^t)$ produces an unbiased estimator.

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| LOG | 1 | 50 | 50 |
| DIV | 2 | 15 | 30 |
| MUL | 3 | 3 | 9 |
| FMA | 1 | 4 | 4 |
| SQRT | 1 | 15 | 15 |
| ADD/SUB | 2 | 1 | 2 |
| CMP | 3 | 1 | 3 |
| **Total** | **13** | — | **~113 cycles** |

The LOG operation dominates the cost. For batch processing, the logarithm is unavoidable due to the sequential dependency on price ratios.

### Batch Mode (SIMD Applicability)

EWMA has limited SIMD potential due to:
1. **Sequential dependency**: Each RMA value depends on the previous
2. **Log operation**: Sequential price ratio requirement

The batch implementation maintains the same algorithm as streaming for consistency.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | Bias correction ensures accurate early estimates |
| **Timeliness** | 8/10 | Exponential weighting responds quickly to shocks |
| **Smoothness** | 7/10 | Smoother than simple historical volatility |
| **Simplicity** | 9/10 | Straightforward implementation |
| **Robustness** | 8/10 | Handles edge cases well |

## Validation

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | No direct EWMA volatility function |
| **Skender** | N/A | No EWMA volatility indicator |
| **Tulip** | N/A | No EWMA volatility indicator |
| **Ooples** | N/A | No EWMA volatility indicator |
| **PineScript** | ✅ | Reference implementation matches |

Note: This implementation is based on the PineScript reference at `ewma.pine`. The mathematical properties and mode consistency are validated through comprehensive unit tests.

## Common Pitfalls

1. **Annualization Confusion**: The `annualPeriods` parameter should match your data frequency. Use 252 for daily bars, 52 for weekly, 12 for monthly. Using incorrect values produces misleading annualized volatility.

2. **Period Selection**: Shorter periods (e.g., 10) respond faster to volatility changes but are noisier. Longer periods (e.g., 50) are smoother but slower to react. The classic RiskMetrics λ=0.94 corresponds to period≈17.

3. **First Value Interpretation**: The first output is always 0 (no return calculated yet). The second output may show high volatility if there's a large price change from the first bar.

4. **Log Return Assumptions**: EWMA assumes returns are approximately normally distributed. During extreme market events (fat tails), volatility may be underestimated.

5. **Bar Correction (isNew=false)**: When correcting the current bar's price, the indicator properly rolls back state. Multiple corrections within the same bar are handled correctly.

6. **Invalid Inputs**: NaN, Infinity, zero, and negative prices are replaced with the last valid price to maintain calculation continuity.

## References

- J.P. Morgan/Reuters. (1996). "RiskMetrics Technical Document." Fourth Edition.
- Bollerslev, T. (1986). "Generalized Autoregressive Conditional Heteroskedasticity." Journal of Econometrics.
- Hull, J. (2018). "Options, Futures, and Other Derivatives." Chapter on Volatility Estimation.