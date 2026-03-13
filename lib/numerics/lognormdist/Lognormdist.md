# LOGNORMDIST: Log-Normal Distribution CDF

> *The log-normal CDF models variables whose logarithm is normal — the natural distribution of prices and multiplicative processes.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Numeric                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `mu` (default 0.0), `sigma` (default 1.0), `period` (default 14)                      |
| **Outputs**      | Single series (Lognormdist)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [lognormdist.pine](lognormdist.pine)                       |

- The Log-Normal Distribution CDF transforms a min-max normalized price into the cumulative distribution function of the log-normal distribution, pro...
- **Trading note:** Log-normal distribution; models multiplicative processes like returns. Foundation of Black-Scholes.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The Log-Normal Distribution CDF transforms a min-max normalized price into the cumulative distribution function of the log-normal distribution, producing an output in $[0, 1]$. A random variable $X$ is log-normally distributed when $\ln(X)$ follows a normal distribution. This makes the log-normal CDF natural for financial data, where multiplicative returns (log-returns) are approximately normally distributed. The indicator min-max normalizes the source to $(0, 1]$, takes the natural logarithm, standardizes by parameters $\mu$ and $\sigma$, then evaluates the standard normal CDF. The result emphasizes values near the bottom of the recent range (where the logarithm diverges) and compresses values near the top.

## Historical Context

The log-normal distribution was first described by Francis Galton (1879) and formalized by Donald McAlister (1879) in a paper read to the Royal Society. It gained prominence in finance through Louis Bachelier's thesis (1900) on price speculation and was later adopted as the foundation of the Black-Scholes option pricing model (1973), where stock prices are assumed to follow geometric Brownian motion, making the price at any future time log-normally distributed.

The log-normal assumption remains the default model in quantitative finance despite well-documented violations (fat tails, volatility clustering). Its mathematical tractability and the economic argument that prices cannot go negative (the log-normal support is $(0, \infty)$) make it a reasonable first approximation. The CDF form used here provides a probability integral transform: if the normalized price truly followed a log-normal distribution with parameters $\mu$ and $\sigma$, the output would be uniformly distributed on $[0, 1]$.

The implementation reduces the log-normal CDF to the standard normal CDF through the substitution $z = (\ln x - \mu)/\sigma$, then uses the Abramowitz and Stegun rational approximation (formula 7.1.26) for $\Phi(z)$, achieving accuracy of approximately $1.5 \times 10^{-7}$.

## Architecture and Physics

The computation follows a three-phase pipeline:

**Phase 1: Min-max normalization** scans `period` bars for extrema, maps the current source to $x \in [0, 1]$. A floor of $10^{-10}$ is applied to prevent $\ln(0)$.

**Phase 2: Log-standardization** computes $z = (\ln x - \mu) / \sigma$. With default $\mu = 0, \sigma = 1$, this simplifies to $z = \ln(x)$. Since $x \in (0, 1]$, $z \in (-\infty, 0]$, so default parameters place most output in $[0, 0.5]$. Shifting $\mu$ negative or increasing $\sigma$ spreads the output across the full $[0, 1]$ range.

**Phase 3: Normal CDF** evaluates $\Phi(z)$ using the Abramowitz and Stegun approximation with 5 polynomial coefficients:

$$\Phi(z) = 1 - \phi(|z|) \cdot (b_1 t + b_2 t^2 + b_3 t^3 + b_4 t^4 + b_5 t^5)$$

where $t = 1/(1 + 0.2316419|z|)$ and $\phi(z) = e^{-z^2/2}/\sqrt{2\pi}$.

**Parameter effects**: $\mu$ shifts the inflection point of the S-curve along the logarithmic axis. $\sigma$ controls the steepness: small $\sigma$ produces a sharp transition, large $\sigma$ produces a gradual one. For financial applications, $\mu = -1, \sigma = 0.5$ centers the CDF near the geometric midpoint of the $[0, 1]$ range.

## Mathematical Foundation

If $X \sim \text{LogNormal}(\mu, \sigma^2)$, then $\ln(X) \sim N(\mu, \sigma^2)$, and the CDF is:

$$F(x; \mu, \sigma) = \Phi\!\left(\frac{\ln x - \mu}{\sigma}\right), \quad x > 0$$

where $\Phi$ is the standard normal CDF.

**Moments of the log-normal distribution:**

$$E[X] = e^{\mu + \sigma^2/2}$$

$$\text{Var}(X) = (e^{\sigma^2} - 1) \cdot e^{2\mu + \sigma^2}$$

$$\text{Skew} = (e^{\sigma^2} + 2)\sqrt{e^{\sigma^2} - 1}$$

**Standard normal CDF** (Abramowitz and Stegun 7.1.26):

$$\Phi(z) = 1 - \frac{e^{-z^2/2}}{\sqrt{2\pi}} \sum_{i=1}^{5} b_i t^i, \quad t = \frac{1}{1 + 0.2316419|z|}$$

with $b_1 = 0.319381530$, $b_2 = -0.356563782$, $b_3 = 1.781477937$, $b_4 = -1.821255978$, $b_5 = 1.330274429$.

**Parameter constraints**: `period` $> 0$, $\sigma > 0$, $\mu \in \mathbb{R}$. Output is bounded $[0, 1]$.

```
LOGNORMDIST(source, period, mu, sigma):
    // Phase 1: min-max normalization
    min_val = min(source[0..period-1])
    max_val = max(source[0..period-1])
    range = max_val - min_val
    x = range > 0 ? (source - min_val) / range : 0.5
    safe_x = max(1e-10, x)

    // Phase 2: log-standardization
    z = (ln(safe_x) - mu) / sigma

    // Phase 3: standard normal CDF
    return normalCdf(z)
```


## Performance Profile

### Operation Count (Streaming Mode)

Log-Normal CDF = Normal CDF of (ln(x) - mu) / sigma — one log() plus an erfc() evaluation.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Input validation (x > 0; sigma > 0) | 2 | 2 cy | ~4 cy |
| log(x) | 1 | 8 cy | ~8 cy |
| z = (log(x) - mu) / sigma | 1 | 4 cy | ~4 cy |
| Normal CDF via erfc (rational approximation) | 1 | 15 cy | ~15 cy |
| NaN guard + state update | 1 | 2 cy | ~2 cy |
| **Total** | **O(1)** | — | **~33 cy** |

O(1) — reduces to Normal CDF after log transform. erfc() rational approximation dominates; log() is secondary cost.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| log(x) | Partial | _mm256_log_pd with SVML |
| z normalization | Yes | Vector FMA |
| erfc() | No | Rational polynomial; scalar |

Limited vectorization — erfc blocks full SIMD. With SVML log: partial vectorization for the transform step.

## Resources

- Galton, F. "The Geometric Mean, in Vital and Social Statistics." Proc. Royal Society, 1879.
- Aitchison, J. & Brown, J.A.C. "The Lognormal Distribution." Cambridge University Press, 1957.
- Black, F. & Scholes, M. "The Pricing of Options and Corporate Liabilities." Journal of Political Economy, 1973.
- Abramowitz, M. & Stegun, I. "Handbook of Mathematical Functions." NBS Applied Mathematics Series 55, 1964. Formula 7.1.26.
- Limpert, E., Stahel, W. & Abbt, M. "Log-normal Distributions across the Sciences: Keys and Clues." BioScience, 2001.