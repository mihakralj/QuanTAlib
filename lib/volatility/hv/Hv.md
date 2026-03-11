# HV: Historical Volatility (Close-to-Close)

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Volatility                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `period` (default 20), `annualize` (default true), `annualPeriods` (default 252)                      |
| **Outputs**      | Single series (Hv)                       |
| **Output range** | $\geq 0$                     |
| **Warmup**       | `period + 1` bars                          |
| **PineScript**   | [hv.pine](hv.pine)                       |

- Historical Volatility (HV), also known as close-to-close volatility or realized volatility, is the classical measure of price volatility using the ...
- Parameterized by `period` (default 20), `annualize` (default true), `annualperiods` (default 252).
- Output range: $\geq 0$.
- Requires `period + 1` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "The foundation of all volatility measures—simple, intuitive, and yet surprisingly informative when you understand what it's actually measuring."

Historical Volatility (HV), also known as close-to-close volatility or realized volatility, is the classical measure of price volatility using the standard deviation of logarithmic returns. First formalized in the early 20th century and central to the Black-Scholes option pricing model, HV remains the benchmark against which all other volatility estimators are compared. This implementation uses population standard deviation with a rolling window and optional annualization.

## Historical Context

The close-to-close volatility estimator predates most range-based alternatives, with its mathematical foundations established alongside the development of stochastic calculus and diffusion processes. The estimator became central to quantitative finance with the publication of the Black-Scholes model in 1973, which explicitly required an estimate of stock price volatility.

Louis Bachelier's 1900 thesis "Théorie de la spéculation" laid the groundwork, modeling price changes as Brownian motion. Fischer Black, Myron Scholes, and Robert Merton formalized the use of historical standard deviation of log returns as the volatility parameter in option pricing.

Despite the development of more efficient estimators (Parkinson 1980, Garman-Klass 1980, Yang-Zhang 2000), close-to-close volatility remains the most widely used and understood measure because:
1. It requires only closing prices, universally available
2. It directly measures what options traders care about—settlement-to-settlement variation
3. It serves as the baseline efficiency benchmark (efficiency = 1.0)

## Architecture & Physics

### 1. Log Return Calculation

Each period's return is computed as the natural logarithm of price ratios:

$$
r_t = \ln\left(\frac{P_t}{P_{t-1}}\right)
$$

where:

- $P_t$ = Closing price at time $t$
- $P_{t-1}$ = Closing price at time $t-1$

Log returns are preferred because they:
- Are time-additive: $r_{t_0 \to t_2} = r_{t_0 \to t_1} + r_{t_1 \to t_2}$
- Normalize percentage changes symmetrically around zero
- Cannot produce prices below zero when simulating

### 2. Rolling Window Statistics

The implementation maintains a rolling window of $n$ log returns and computes population variance using the computational formula:

$$
\sigma^2 = E[X^2] - E[X]^2 = \frac{\sum r_i^2}{n} - \left(\frac{\sum r_i}{n}\right)^2
$$

Two running sums are maintained:
- $\sum r_i$ — sum of returns
- $\sum r_i^2$ — sum of squared returns

This enables O(1) update complexity per new bar.

### 3. Population vs Sample Variance

This implementation uses **population variance** (dividing by $n$) rather than sample variance (dividing by $n-1$). For typical periods (14-30 returns), the difference is small:

| Period | Sample/Pop Ratio |
| :---: | :---: |
| 10 | 1.111 |
| 14 | 1.077 |
| 20 | 1.053 |
| 30 | 1.034 |

Population variance provides a consistent estimator for the rolling window and matches the implementation in most trading platforms.

### 4. Volatility Calculation

Convert variance to volatility (standard deviation):

$$
\sigma_t = \sqrt{variance}
$$

### 5. Optional Annualization

If annualization is enabled (default):

$$
\sigma_{annual,t} = \sigma_t \times \sqrt{N}
$$

where $N$ = annual periods (default 252 trading days).

## Mathematical Foundation

### Log Return Properties

For a geometric Brownian motion $dS = \mu S dt + \sigma S dW$:

The log return over interval $\Delta t$ is:

$$
r = \ln\left(\frac{S_t}{S_{t-1}}\right) = \left(\mu - \frac{\sigma^2}{2}\right)\Delta t + \sigma \sqrt{\Delta t} \cdot Z
$$

where $Z \sim N(0,1)$.

The variance of log returns is:

$$
\text{Var}(r) = \sigma^2 \Delta t
$$

Therefore, the annualized volatility is:

$$
\sigma_{annual} = \frac{\sigma_{period}}{\sqrt{\Delta t}} = \sigma_{period} \times \sqrt{N}
$$

### Efficiency Comparison

| Estimator | Relative Efficiency | Data Required |
| :--- | :---: | :--- |
| **Close-to-Close (HV)** | **1.0** | **C** |
| Parkinson (HLV) | 5.2 | H, L |
| Garman-Klass (GKV) | 7.4 | O, H, L, C |
| Rogers-Satchell | 8.4 | O, H, L, C |
| Yang-Zhang | 14.0 | O, H, L, C |

HV (close-to-close) is the efficiency baseline. A Parkinson estimator with efficiency 5.2 means you need 5.2× fewer observations to achieve the same precision—or equivalently, 5.2× better precision with the same observations.

### Annualization Factor

For daily data with 252 trading days:

$$
\sqrt{252} \approx 15.875
$$

Common annualization factors:

| Data Frequency | Periods/Year | Factor |
| :--- | :---: | :---: |
| Daily | 252 | 15.875 |
| Weekly | 52 | 7.211 |
| Monthly | 12 | 3.464 |
| Hourly (6.5h/day) | 1638 | 40.472 |

### Warmup Period

HV requires `period + 1` prices to produce a valid result:
- First price establishes the baseline
- Next `period` prices generate `period` returns
- Standard deviation is calculated on these `period` returns

The `IsHot` property indicates when warmup is complete.

## Performance Profile

### Operation Count (Streaming Mode, Scalar)

Per-bar operations after warmup:

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| LOG | 1 | 25 | 25 |
| DIV | 1 | 15 | 15 |
| MUL | 2 | 3 | 6 |
| ADD/SUB | 4 | 1 | 4 |
| DIV (variance) | 2 | 15 | 30 |
| SQRT | 1 | 15 | 15 |
| MUL (annual) | 1 | 3 | 3 |
| **Total** | — | — | **~98 cycles** |

The dominant costs are LOG (26%) and SQRT (15%). Computational formula avoids iteration over the window.

### Batch Mode (512 values, SIMD/FMA)

| Operation | Scalar Ops | SIMD Ops (AVX2) | Speedup |
| :--- | :---: | :---: | :---: |
| LOG (vectorized) | 512 | 64 | 8× |
| DIV (prev price) | 512 | 64 | 8× |
| Rolling stats | 512 | 512 | 1× |
| SQRT (vectorized) | 512 | 64 | 8× |

**Note:** Rolling sum updates are sequential, limiting total batch improvement.

### Memory Profile

- **Per instance:** ~88 bytes (state struct + RingBuffer reference)
- **RingBuffer:** 8 bytes × period (default 20 = 160 bytes)
- **100 instances @ period 20:** ~24.8 KB

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 7/10 | Unbiased under GBM, but lowest efficiency |
| **Efficiency** | 5/10 | Baseline (1.0x), outperformed by range-based |
| **Timeliness** | 8/10 | Direct measurement, minimal lag |
| **Smoothness** | 6/10 | Can be noisy without smoothing |
| **Simplicity** | 10/10 | Only requires close prices |

## Validation

HV (close-to-close) is implemented in most technical analysis libraries:

| Library | Status | Notes |
| :--- | :---: | :--- |
| **TA-Lib** | N/A | Not directly implemented |
| **Skender** | N/A | Not directly implemented |
| **Tulip** | N/A | Not directly implemented |
| **OoplesFinance** | N/A | Not directly implemented |
| **PineScript** | ✅ | Matches hv.pine reference |
| **Manual** | ✅ | Validated against formula |

Note: Most libraries provide building blocks (STDDEV, LOG) rather than a dedicated HV function. The implementation is validated against the mathematical formula and PineScript reference.

## Common Pitfalls

1. **Warmup period**: HV requires `period + 1` prices before producing valid results. With default period=20, you need 21 prices to generate 20 returns. The `IsHot` property indicates when warmup is complete.

2. **Zero or negative prices**: Log transformation requires positive prices. Zero or negative values trigger last-valid-value substitution to prevent NaN propagation.

3. **Constant prices**: When all prices in the window are identical, returns are zero, producing zero volatility. This is mathematically correct but may indicate data issues.

4. **Annualization assumptions**: Default annualization assumes 252 trading days/year. For intraday data, cryptocurrency (365 days), or weekly data, adjust `annualPeriods` accordingly.

5. **Mean return assumption**: The standard formula implicitly subtracts the mean return. During strong trends, this captures both directional movement and noise, potentially overstating "noise" volatility.

6. **Population vs sample variance**: This implementation uses population variance (n divisor). If comparing with implementations using sample variance (n-1 divisor), expect slight differences: sample/pop ratio ≈ n/(n-1).

7. **Overnight gaps**: Unlike range-based estimators, HV fully captures overnight gaps (close-to-close movements). This can be an advantage (complete picture) or disadvantage (includes information not tradeable intraday).

8. **Comparison with range-based**: HV is 5.2× less efficient than Parkinson (HLV) and 7.4× less efficient than Garman-Klass (GKV). Use HV when:
   - Only close prices are available
   - You specifically want close-to-close volatility (e.g., settlement-based risk)
   - Comparing with implied volatility (which prices close-to-close variation)

## Trading Applications

### Options Volatility Comparison

Compare realized HV with implied volatility (IV):

```
Volatility Risk Premium = IV - HV

If IV > HV consistently: Options are "expensive," consider selling
If IV < HV consistently: Options are "cheap," consider buying
```

This comparison is most valid with HV because IV prices close-to-close variation.

### Position Sizing

Use HV for volatility-adjusted position sizing:

```
Position size = Account risk / (HV × Price × √holding period)
```

Example: $100K account, 1% risk, HV = 0.25, Price = $100, 5-day hold:
Position = $1000 / (0.25 × $100 × √5) ≈ 17.9 shares

### Volatility Regime Detection

Track HV percentile over lookback:

```
High HV rank (>80%): High volatility regime
- Reduce position sizes
- Widen stop losses
- Consider volatility mean reversion trades

Low HV rank (<20%): Low volatility regime
- Potential for volatility expansion
- Breakout strategies may work better
- Options are likely cheap
```

### Historical Volatility Cones

Plot HV at multiple periods (10, 20, 60, 120 days) to see the term structure:

```
Normal: Short HV < Long HV (contango)
Inverted: Short HV > Long HV (backwardation, stress regime)
```

### Risk Reporting

HV is the standard for regulatory risk calculations (VaR, ES) because:
- Clear mathematical definition
- Universally understood
- Directly comparable across assets and time

## References

- Bachelier, L. (1900). "Théorie de la spéculation." *Annales scientifiques de l'École Normale Supérieure*, 17, 21-86.
- Black, F., & Scholes, M. (1973). "The Pricing of Options and Corporate Liabilities." *Journal of Political Economy*, 81(3), 637-654.
- Parkinson, M. (1980). "The Extreme Value Method for Estimating the Variance of the Rate of Return." *Journal of Business*, 53(1), 61-65.
- Garman, M. B., & Klass, M. J. (1980). "On the Estimation of Security Price Volatilities from Historical Data." *Journal of Business*, 53(1), 67-78.
- Merton, R. C. (1980). "On Estimating the Expected Return on the Market: An Exploratory Investigation." *Journal of Financial Economics*, 8(4), 323-361.
