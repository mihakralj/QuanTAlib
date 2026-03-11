# POISSONDIST: Poisson Distribution CDF

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Numeric                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `lambda` (default 1.0), `period` (default 14), `threshold` (default 5)                      |
| **Outputs**      | Single series (Poissondist)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [poissondist.pine](poissondist.pine)                       |

- The Poisson Distribution CDF computes the probability $P(X \le k)$ for a Poisson random variable whose rate parameter $\lambda$ is derived from the...
- Parameterized by `lambda` (default 1.0), `period` (default 14), `threshold` (default 5).
- Output range: Varies (see docs).
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The Poisson Distribution CDF computes the probability $P(X \le k)$ for a Poisson random variable whose rate parameter $\lambda$ is derived from the min-max normalized price. The Poisson distribution models the number of events in a fixed interval given a constant average rate, making it natural for count-based financial metrics (trade arrivals, tick counts, order flow). The implementation maps normalized price to $\lambda$ via a scale factor, then evaluates the CDF using the identity $P(X \le k) = 1 - P(k+1, \lambda)$ where $P(a, x)$ is the regularized lower incomplete gamma function. This reuses the same Lanczos log-gamma and series/continued-fraction infrastructure as GAMMADIST.

## Historical Context

The Poisson distribution was derived by Simeon Denis Poisson (1837) as a limiting case of the binomial distribution when the number of trials is large and the success probability is small. Ladislaus Bortkiewicz (1898) famously demonstrated its applicability by modeling deaths from horse kicks in the Prussian army, establishing it as the canonical distribution for rare events.

In financial markets, Poisson processes model trade arrivals in market microstructure theory (O'Hara, 1995), jump events in Merton's jump-diffusion model (1976), and order book dynamics. The CDF form used here provides a probability-weighted indicator: for a given threshold $k$ and price-derived rate $\lambda$, the output answers "what is the probability that a Poisson process with rate proportional to the normalized price would produce at most $k$ events?"

When the normalized price is low (near 0), $\lambda$ is small and the CDF is close to 1 (almost certainly $\le k$ events). When normalized price approaches 1, $\lambda$ is large and the CDF drops (many events expected, exceeding $k$ becomes likely). The `lambda_scale` parameter controls the dynamic range: higher values cause broader CDF variation across the $[0, 1]$ normalized range.

## Architecture and Physics

The computation follows a three-phase pipeline:

**Phase 1: Min-max normalization** scans `period` bars for extrema, maps the current source to $x \in [0, 1]$, then derives the rate: $\lambda = \max(0, x \cdot \text{lambda\_scale})$.

**Phase 2: Degenerate case** handles $\lambda = 0$ by returning 1.0 (Poisson with rate 0 puts all mass at $X = 0$, so $P(X \le k) = 1$ for any $k \ge 0$).

**Phase 3: Gamma function identity** uses the well-known relationship between the Poisson CDF and the regularized incomplete gamma function:

$$P(X \le k) = 1 - P(k + 1, \lambda) = Q(k + 1, \lambda)$$

where $P(a, x)$ is the regularized lower incomplete gamma and $Q$ is its complement. The implementation delegates to `gammaP()`, which internally selects between series expansion (for $\lambda < k + 2$) and Lentz continued fraction (otherwise).

**Threshold parameter $k$**: Integer-valued, controls the step function shape. Small $k$ (0-2) creates a steep CDF that drops rapidly as $\lambda$ increases. Large $k$ (10+) creates a gentle curve that stays near 1.0 until $\lambda$ significantly exceeds $k$.

## Mathematical Foundation

The Poisson distribution with rate $\lambda > 0$ has PMF:

$$P(X = n) = \frac{\lambda^n e^{-\lambda}}{n!}, \quad n = 0, 1, 2, \ldots$$

The CDF is:

$$F(k; \lambda) = P(X \le k) = e^{-\lambda} \sum_{n=0}^{k} \frac{\lambda^n}{n!}$$

The **gamma function identity** connects this to the incomplete gamma:

$$P(X \le k) = 1 - P(k+1, \lambda) = \frac{\Gamma(k+1, \lambda)}{k!}$$

where $\Gamma(a, x) = \int_x^\infty t^{a-1} e^{-t}\,dt$ is the upper incomplete gamma function and $P(a, x) = \gamma(a, x)/\Gamma(a)$ is the regularized lower incomplete gamma.

**Moments**: $E[X] = \lambda$, $\text{Var}(X) = \lambda$, $\text{Skew} = 1/\sqrt{\lambda}$.

**Normal approximation**: For large $\lambda$, $\text{Poisson}(\lambda) \approx N(\lambda, \lambda)$.

**Parameter constraints**: `period` $> 0$, $k \ge 0$ (integer), `lambda_scale` $> 0$. Output is bounded $[0, 1]$.

```
POISSONDIST(source, period, k, lambda_scale):
    // Phase 1: min-max normalization + rate derivation
    min_val = min(source[0..period-1])
    max_val = max(source[0..period-1])
    range = max_val - min_val
    x = range > 0 ? (source - min_val) / range : 0.5
    lambda = max(0, x * lambda_scale)

    // Phase 2: degenerate case
    if lambda <= 0: return 1.0

    // Phase 3: CDF via incomplete gamma identity
    return 1.0 - gammaP(k + 1, lambda)
```


## Performance Profile

### Operation Count (Streaming Mode)

Poisson PMF = e^(-lambda) * lambda^k / k! computed via log-space to avoid overflow.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Input validation (lambda > 0; k >= 0) | 2 | 2 cy | ~4 cy |
| k * log(lambda) - lgamma(k+1) - lambda | 3 | 10 cy | ~30 cy |
| exp() of log-PMF | 1 | 20 cy | ~20 cy |
| CDF cumulative sum (k terms) | k | 50 cy | ~50k cy |
| **Total (PMF only)** | **O(1)** | — | **~54 cy** |

PMF is O(1) via log-space computation. CDF is O(k) — expensive for large k. For k > 30, use Normal approximation. lgamma() dominates for small k.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| log(lambda) | Partial | _mm256_log_pd with SVML |
| lgamma(k+1) | No | Transcendental; scalar |
| exp() | Partial | _mm256_exp_pd with SVML |
| CDF sum | No | Sequential dependency |

PMF batch: partial SIMD with SVML. CDF must be scalar. For large lambda, Normal approximation enables full vectorization.

## Resources

- Poisson, S.D. "Recherches sur la probabilite des jugements en matiere criminelle et en matiere civile." 1837.
- Bortkiewicz, L. "Das Gesetz der kleinen Zahlen." Teubner, 1898.
- Merton, R.C. "Option Pricing When Underlying Stock Returns Are Discontinuous." Journal of Financial Economics, 1976.
- O'Hara, M. "Market Microstructure Theory." Blackwell, 1995.
- Press, W.H. et al. "Numerical Recipes: The Art of Scientific Computing." 3rd edition, Cambridge University Press, 2007. Chapter 6.2.
