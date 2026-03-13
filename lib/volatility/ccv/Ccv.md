# CCV: Close-to-Close Volatility

> *The simplest volatility measure is often the most robust—when all you have is closing prices, make the most of them.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Volatility                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`, `method` (default 1)                      |
| **Outputs**      | Single series (Ccv)                       |
| **Output range** | $\geq 0$                     |
| **Warmup**       | `period + 1` bars                          |
| **PineScript**   | [ccv.pine](ccv.pine)                       |

- Close-to-Close Volatility (CCV) calculates the annualized standard deviation of logarithmic returns using only closing prices.
- **Similar:** [HV](../hv/hv.md) | **Complementary:** Close-to-close analysis | **Trading note:** Close-to-Close volatility; simplest vol estimator.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

Close-to-Close Volatility (CCV) calculates the annualized standard deviation of logarithmic returns using only closing prices. This is the foundational volatility measure in quantitative finance, serving as a benchmark against which more sophisticated estimators are compared. The implementation supports three smoothing methods (SMA, EMA, WMA) and annualizes using the standard √252 factor for daily data.

## Historical Context

Close-to-close volatility has been the workhorse of volatility estimation since the earliest days of quantitative finance. Its simplicity—requiring only closing prices—made it practical for analysis when intraday data was unavailable or expensive. While modern volatility estimators like Parkinson (1980), Garman-Klass (1980), and Yang-Zhang (2000) leverage high/low/open data for improved efficiency, CCV remains the standard reference point.

The mathematical foundation rests on the assumption that log returns follow a normal distribution with constant volatility over the estimation window. When this assumption holds, CCV is the maximum likelihood estimator. When it doesn't (which is most of the time in real markets), CCV still provides a reasonable baseline that's easy to interpret and compare across assets.

## Architecture & Physics

### 1. Log Return Calculation

The first step converts prices to continuously compounded returns:

$$
r_t = \ln\left(\frac{C_t}{C_{t-1}}\right)
$$

where:

- $C_t$ = closing price at time $t$
- $r_t$ = log return at time $t$

Log returns are preferred over simple returns because they're additive over time and symmetric (a +10% gain followed by -10% loss returns to approximately the original value).

### 2. Population Standard Deviation

The volatility is calculated as the population standard deviation of returns:

$$
\sigma = \sqrt{\frac{1}{n}\sum_{i=1}^{n}(r_i - \bar{r})^2}
$$

where:

- $n$ = number of observations in the period
- $\bar{r}$ = mean of log returns over the period

Using population (n) rather than sample (n-1) variance provides consistency with the PineScript reference implementation.

### 3. Annualization

Daily volatility is scaled to annual terms:

$$
\sigma_{annual} = \sigma_{daily} \times \sqrt{252}
$$

The factor 252 represents the typical number of trading days in a year. This annualization assumes:

- Returns are independent and identically distributed
- Variance scales linearly with time

### 4. Smoothing Methods

Three smoothing options are available:

**Method 1 - SMA (Simple Moving Average):**
Reports the raw annualized standard deviation—no additional smoothing.

**Method 2 - EMA/RMA with Warmup Compensation:**

$$
\text{raw}_t = \beta \cdot \text{raw}_{t-1} + \alpha \cdot \sigma_t
$$

$$
e_t = e_{t-1} \cdot \beta
$$

$$
\text{CCV}_t = \begin{cases}
\frac{\text{raw}_t}{1 - e_t} & \text{if } e_t > \epsilon \\
\text{raw}_t & \text{otherwise}
\end{cases}
$$

where $\alpha = 1/\text{period}$, $\beta = 1 - \alpha$, and the compensation term $(1 - e_t)$ corrects for the bias during warmup.

**Method 3 - WMA (Weighted Moving Average):**
Applies triangular weights to the annualized volatility values, giving more influence to recent observations.

## Mathematical Foundation

### Log Return Properties

For a stock with price $S_t$ following geometric Brownian motion:

$$
dS_t = \mu S_t dt + \sigma S_t dW_t
$$

The log return over interval $\Delta t$ is:

$$
r = \ln(S_{t+\Delta t}/S_t) \sim N\left((\mu - \frac{\sigma^2}{2})\Delta t, \sigma^2 \Delta t\right)
$$

This means:

- Expected log return ≈ $\mu \Delta t$ (for small $\sigma$)
- Variance of log return = $\sigma^2 \Delta t$
- Standard deviation = $\sigma \sqrt{\Delta t}$

### Annualization Derivation

If daily volatility is $\sigma_d$ and we assume independence:

$$
\text{Var}[\text{annual return}] = 252 \times \text{Var}[\text{daily return}]
$$

Therefore:

$$
\sigma_a = \sqrt{252} \times \sigma_d \approx 15.875 \times \sigma_d
$$

### Efficiency Analysis

CCV uses only closing prices, discarding intraday information. Compared to range-based estimators:

| Estimator | Efficiency vs CCV | Data Required |
| :--- | :---: | :--- |
| Close-to-Close (CCV) | 1.0 | Close only |
| Parkinson | ~5.0× | High, Low |
| Garman-Klass | ~7.4× | OHLC |
| Yang-Zhang | ~8.0× | OHLC + overnight |

"Efficiency" measures how much faster the estimator converges to the true volatility. Higher is better, but requires more data.

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

Per-bar operations for SMA method:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| DIV | 1 | 15 | 15 |
| LOG | 1 | 50 | 50 |
| ADD/SUB | ~2n | 1 | ~2n |
| MUL | n | 3 | 3n |
| SQRT | 1 | 15 | 15 |
| **Total** | — | — | **~80 + 5n cycles** |

For period=20: approximately 180 cycles per bar.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 6/10 | Unbiased but inefficient vs range-based |
| **Timeliness** | 8/10 | Direct calculation, no smoothing delay |
| **Robustness** | 9/10 | Only needs closing prices |
| **Simplicity** | 10/10 | Foundational measure |
| **Comparability** | 10/10 | Universal standard |

## Validation

CCV is a standard volatility measure implemented consistently across platforms:

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | No direct equivalent |
| **Skender** | N/A | Uses different approach |
| **Manual** | ✅ | Validated against hand calculations |
| **PineScript** | ✅ | Matches reference implementation |

## Common Pitfalls

1. **Assuming normality**: Real returns have fat tails. CCV underestimates the frequency of extreme moves.

2. **Ignoring overnight gaps**: For assets with significant overnight risk (stocks), CCV captures this but range-based estimators may not.

3. **Period selection**: Too short = noisy; too long = slow to adapt. 20 days is a common default for daily data.

4. **Annualization factor**: Use 252 for daily equity data, but consider:
   - Crypto: 365 (trades every day)
   - Forex: ~252 (variable by pair)
   - Commodities: ~252 (check specific contract)

5. **Mean assumption**: The calculation assumes mean return ≈ 0 over short periods. For trending markets, this introduces slight bias.

6. **Smoothing trade-offs**:
   - Method 1 (SMA): Most responsive, noisiest
   - Method 2 (EMA): Smoothest, has lag
   - Method 3 (WMA): Compromise between responsiveness and smoothness

## References

- Black, F., & Scholes, M. (1973). "The Pricing of Options and Corporate Liabilities." *Journal of Political Economy*.
- Parkinson, M. (1980). "The Extreme Value Method for Estimating the Variance of the Rate of Return." *Journal of Business*.
- Garman, M., & Klass, M. (1980). "On the Estimation of Security Price Volatilities from Historical Data." *Journal of Business*.
- Yang, D., & Zhang, Q. (2000). "Drift-Independent Volatility Estimation Based on High, Low, Open, and Close Prices." *Journal of Business*.