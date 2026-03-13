# CV: Conditional Volatility (GARCH(1,1))

> *Volatility begets volatility—the GARCH model captures what traders have always known: calm markets stay calm, turbulent markets stay turbulent.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Volatility                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period` (default 20), `alpha` (default 0.2), `beta` (default 0.7)                      |
| **Outputs**      | Single series (Cv)                       |
| **Output range** | $\geq 0$                     |
| **Warmup**       | `period + 1` bars                          |
| **PineScript**   | [cv.pine](cv.pine)                       |

- Conditional Volatility (CV) implements the GARCH(1,1) model for volatility forecasting, the most widely used time-varying volatility model in finan...
- **Similar:** [HV](../hv/hv.md) | **Complementary:** Volatility analysis | **Trading note:** Coefficient of Variation; ratio of std dev to mean.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

Conditional Volatility (CV) implements the GARCH(1,1) model for volatility forecasting, the most widely used time-varying volatility model in financial econometrics. Unlike simple historical volatility measures, GARCH captures two key empirical features of financial returns: volatility clustering (large moves tend to follow large moves) and mean reversion (volatility eventually returns to a long-run average). The output is annualized volatility expressed as a percentage.

## Historical Context

Robert Engle introduced ARCH (Autoregressive Conditional Heteroskedasticity) in 1982, earning him the 2003 Nobel Prize in Economics. Tim Bollerslev generalized this to GARCH (Generalized ARCH) in 1986. The GARCH(1,1) specification—with one lag of squared returns and one lag of variance—became the workhorse model because it captures the essential dynamics while remaining parsimonious.

The key insight was that volatility is not constant over time but evolves predictably. A large price shock today increases tomorrow's expected volatility, which then decays gradually back to the long-run level. This "persistence" in volatility is captured by the β coefficient, while the immediate reaction to shocks is captured by α.

Traditional implementations require maximum likelihood estimation to fit parameters to historical data. This implementation takes a different approach: it uses the warmup period to estimate the long-run variance, then applies user-specified α and β coefficients. This makes the indicator immediately usable without optimization, while still capturing the essential GARCH dynamics.

## Architecture & Physics

### 1. Log Return Calculation

Returns are computed as continuously compounded (log) returns:

$$
r_t = \ln\left(\frac{C_t}{C_{t-1}}\right)
$$

where:

- $C_t$ = closing price at time $t$
- $r_t$ = log return at time $t$

Extreme returns are clamped to ±20% to prevent numerical instability from outliers.

### 2. Long-Run Variance Estimation (Warmup Phase)

During the initial `period` observations, the indicator estimates the unconditional (long-run) variance:

$$
\bar{\sigma}^2 = \frac{1}{n}\sum_{i=1}^{n} r_i^2
$$

This running mean of squared returns provides the anchor point toward which volatility mean-reverts.

### 3. Omega Calculation

The constant term ω is derived from the stationarity constraint:

$$
\omega = (1 - \alpha - \beta) \times \bar{\sigma}^2
$$

This ensures that the unconditional variance of the GARCH process equals the estimated long-run variance:

$$
E[\sigma^2] = \frac{\omega}{1 - \alpha - \beta} = \bar{\sigma}^2
$$

### 4. GARCH(1,1) Recursion

After warmup, variance evolves according to:

$$
\sigma^2_t = \omega + \alpha \cdot r^2_{t-1} + \beta \cdot \sigma^2_{t-1}
$$

where:

- $\omega$ = constant term (pulls variance toward long-run level)
- $\alpha$ = innovation coefficient (weight on previous squared return)
- $\beta$ = persistence coefficient (weight on previous variance)
- $\alpha + \beta$ = persistence (must be < 1 for stationarity)

### 5. Annualization

Daily variance is converted to annualized volatility percentage:

$$
CV_t = \sqrt{252 \times \sigma^2_t} \times 100
$$

## Mathematical Foundation

### GARCH(1,1) Properties

**Unconditional Variance:**

$$
E[\sigma^2] = \frac{\omega}{1 - \alpha - \beta}
$$

**Persistence:**
The sum $\alpha + \beta$ measures how quickly shocks decay:

- $\alpha + \beta$ close to 1: Very persistent (shocks decay slowly)
- $\alpha + \beta$ close to 0: Mean-reverting quickly

**Half-Life of Shocks:**

$$
\text{Half-life} = \frac{\ln(0.5)}{\ln(\alpha + \beta)}
$$

For default parameters ($\alpha = 0.2$, $\beta = 0.7$, persistence = 0.9):

$$
\text{Half-life} = \frac{-0.693}{-0.105} \approx 6.6 \text{ days}
$$

### Stationarity Constraint

For the variance process to be covariance-stationary:

$$
\alpha + \beta < 1
$$

When $\alpha + \beta \geq 1$, the process becomes IGARCH (Integrated GARCH) and shocks have permanent effects.

### Volatility Clustering

The GARCH model mathematically captures why "large changes tend to be followed by large changes":

$$
E[\sigma^2_{t+1} | \sigma^2_t, r_t] = \omega + (\alpha + \beta) \sigma^2_t + \alpha (r^2_t - \sigma^2_t)
$$

If today's squared return exceeds the current variance forecast, tomorrow's forecast increases.

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

Per-bar operations after warmup:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| DIV | 1 | 15 | 15 |
| LOG | 1 | 50 | 50 |
| MUL | 4 | 3 | 12 |
| ADD/SUB | 3 | 1 | 3 |
| FMA | 2 | 4 | 8 |
| SQRT | 1 | 15 | 15 |
| MAX | 1 | 1 | 1 |
| **Total** | — | — | **~104 cycles** |

### Batch Mode (512 values)

The GARCH recursion is inherently sequential due to the $\sigma^2_{t-1}$ dependency. However, the log return calculation can be vectorized:

| Operation | Scalar Ops | SIMD Ops (AVX2) | Speedup |
| :--- | :---: | :---: | :---: |
| Log returns | 512 | 64 | 8× |
| GARCH recursion | 512 | 512 | 1× (sequential) |

**Total batch savings: ~15-20%** (log return vectorization only)

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 8/10 | Captures clustering and mean reversion |
| **Timeliness** | 7/10 | Responds immediately to shocks |
| **Smoothness** | 8/10 | Smooth decay after shocks |
| **Interpretability** | 9/10 | Parameters have clear meanings |
| **Robustness** | 7/10 | Sensitive to parameter choice |

## Validation

CV/GARCH is proprietary with no direct open-source equivalents using the same approach:

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | No GARCH implementation |
| **Skender** | N/A | No GARCH implementation |
| **Tulip** | N/A | No GARCH implementation |
| **Manual** | ✅ | Validated against GARCH formula |
| **PineScript** | ✅ | Matches cv.pine reference |

## Common Pitfalls

1. **Stationarity violation**: Ensure $\alpha + \beta < 1$. The constructor enforces this constraint. Values near 1.0 produce extreme persistence.

2. **Parameter selection**: Default $\alpha = 0.2$, $\beta = 0.7$ are reasonable starting points. Higher α = more reactive to shocks; higher β = more persistent.

3. **Warmup period**: The `period` parameter determines how many observations are used to estimate long-run variance. Too short = noisy estimate; too long = slow to initialize. Default 20 is reasonable for daily data.

4. **Not a forecast**: The output is the *current* conditional variance, not a prediction. For forecasting, the expected variance $h$ days ahead is:

   $$
   E[\sigma^2_{t+h}] = \bar{\sigma}^2 + (\alpha + \beta)^h (\sigma^2_t - \bar{\sigma}^2)
   $$

5. **Memory footprint**: Minimal—only stores previous variance and previous close. No rolling buffers required.

6. **Annualization assumption**: Uses 252 trading days. For crypto (365 days) or other markets, the annualization factor may need adjustment in the calling code.

## References

- Engle, R. F. (1982). "Autoregressive Conditional Heteroscedasticity with Estimates of the Variance of United Kingdom Inflation." *Econometrica*, 50(4), 987-1007.
- Bollerslev, T. (1986). "Generalized Autoregressive Conditional Heteroskedasticity." *Journal of Econometrics*, 31(3), 307-327.
- Engle, R. F. (2001). "GARCH 101: The Use of ARCH/GARCH Models in Applied Econometrics." *Journal of Economic Perspectives*, 15(4), 157-168.
- Hansen, P. R., & Lunde, A. (2005). "A Forecast Comparison of Volatility Models: Does Anything Beat a GARCH(1,1)?" *Journal of Applied Econometrics*, 20(7), 873-889.