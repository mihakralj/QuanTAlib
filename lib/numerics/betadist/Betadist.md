# BETADIST: Beta Distribution CDF

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Numeric                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period` (default 50), `alpha` (default 2.0), `beta` (default 2.0)                      |
| **Outputs**      | Single series (Betadist)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period` bars                          |

### TL;DR

- BETADIST computes the cumulative distribution function of the Beta distribution applied to a min-max normalized price series.
- Parameterized by `period` (default 50), `alpha` (default 2.0), `beta` (default 2.0).
- Output range: Varies (see docs).
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

BETADIST computes the cumulative distribution function of the Beta distribution applied to a min-max normalized price series. The source price is first normalized to $[0, 1]$ over a lookback window, then passed through the regularized incomplete beta function $I_x(\alpha, \beta)$ to produce a probability-mapped oscillator. The two shape parameters $\alpha$ and $\beta$ control the nonlinear mapping: symmetric parameters ($\alpha = \beta$) produce a sigmoid-like transformation centered at 0.5, while asymmetric parameters skew the mapping to emphasize extremes in either direction.

## Historical Context

The Beta distribution is one of the fundamental distributions in Bayesian statistics, serving as the conjugate prior for Bernoulli and binomial processes. Its application to financial time series normalization leverages the distribution's unique property of being defined on the bounded interval $[0, 1]$, making it a natural fit for min-max normalized price data. The CDF transformation converts a uniformly-distributed normalized price into a probability-weighted oscillator where the shape parameters control sensitivity to price levels within the range. When $\alpha = \beta = 1$, the Beta distribution reduces to the uniform distribution (no transformation); when $\alpha = \beta = 2$, it produces a smooth S-curve that compresses extremes and expands the midrange. The regularized incomplete beta function required for the CDF has no elementary closed form and requires numerical methods — this implementation uses Lentz's continued fraction algorithm, the standard approach in numerical libraries (NAG, CEPHES, Numerical Recipes).

## Architecture & Physics

### Three-Stage Pipeline

1. **Min-Max Normalization:** Scans the lookback window to find minimum and maximum values, then maps the current source to $x \in [0, 1]$. If the range is zero (flat price), defaults to 0.5.

2. **Lanczos Log-Gamma:** The Lanczos approximation with $g = 7$ and 9 coefficients computes $\ln\Gamma(z)$ for any positive $z$. This is used internally by the continued fraction to compute the prefactor of the incomplete beta function.

3. **Lentz Continued Fraction:** The regularized incomplete beta function $I_x(a, b)$ is evaluated via the modified Lentz algorithm. A symmetry flip is applied when $x > (a+1)/(a+b+2)$ to ensure convergence of the continued fraction from the correct side. Convergence typically requires 10-20 iterations for standard parameter ranges.

## Mathematical Foundation

**Min-max normalization:**

$$x_t = \frac{S_t - \min_{i \in [t-n, t]} S_i}{\max_{i \in [t-n, t]} S_i - \min_{i \in [t-n, t]} S_i}$$

**Beta CDF (regularized incomplete beta function):**

$$I_x(\alpha, \beta) = \frac{B(x; \alpha, \beta)}{B(\alpha, \beta)} = \frac{\int_0^x t^{\alpha-1}(1-t)^{\beta-1}\,dt}{B(\alpha, \beta)}$$

**Lentz continued fraction** for $I_x(a, b)$:

$$I_x(a,b) = \frac{x^a (1-x)^b}{a \cdot B(a,b)} \cdot \cfrac{1}{1+\cfrac{d_1}{1+\cfrac{d_2}{1+\cdots}}}$$

where $d_{2m} = \frac{m(b-m)x}{(a+2m-1)(a+2m)}$ and $d_{2m+1} = \frac{-(a+m)(a+b+m)x}{(a+2m)(a+2m+1)}$

**Symmetry flip:** If $x > \frac{a+1}{a+b+2}$, compute $I_x(a,b) = 1 - I_{1-x}(b,a)$

**Lanczos log-gamma** ($g = 7$, 9 coefficients):

$$\ln\Gamma(z) = \frac{1}{2}\ln(2\pi) + (z - \tfrac{1}{2})\ln(t) - t + \ln\left(\sum_{k=0}^{8} \frac{c_k}{z+k}\right)$$

where $t = z + g - \frac{1}{2}$

**Default parameters:** period = 50, alpha = 2.0, beta = 2.0.


## Performance Profile

### Operation Count (Streaming Mode)

Beta distribution CDF uses a regularized incomplete beta function evaluated via continued fraction expansion.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Input validation (alpha, beta > 0; x in [0,1]) | 3 | 2 cy | ~6 cy |
| Regularized incomplete beta (Lentz CF) | ~20 iter | 15 cy | ~300 cy |
| Log-beta normalization constant | 1 | 25 cy | ~25 cy |
| NaN guard + state update | 1 | 2 cy | ~2 cy |
| **Total** | **O(1)** | — | **~333 cy** |

O(1) per bar — cost is fixed by the continued fraction convergence threshold regardless of input. log-Gamma dominates setup; each Lentz iteration is ~15 cy.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| Input validation | Yes | Vector range check |
| Continued fraction iteration | No | Sequential convergence |
| Log-Gamma computation | No | Transcendental function; scalar |
| Output assignment | Yes | Trivial |

Transcendental math blocks SIMD. Batch is a simple scalar loop. For large datasets use parallel outer loop (PLINQ) for throughput.

## Resources

- Abramowitz, M. & Stegun, I. (1964). *Handbook of Mathematical Functions*, Chapter 26
- Press, W. et al. (2007). *Numerical Recipes*, 3rd ed. Cambridge, §6.4 (Incomplete Beta Function)
- PineScript reference: [`betadist.pine`](betadist.pine)
