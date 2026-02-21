# GAMMADIST: Gamma Distribution CDF

The Gamma Distribution CDF transforms a min-max normalized price into the cumulative distribution function of the gamma distribution, producing an output in $[0, 1]$. The gamma distribution generalizes the exponential distribution by adding a shape parameter $\alpha$ that controls whether the PDF is monotonically decreasing ($\alpha < 1$), exponential ($\alpha = 1$), or bell-shaped with a right skew ($\alpha > 1$). Combined with a rate parameter $\beta$ that scales the normalized input, GAMMADIST provides a flexible nonlinear mapping with controllable asymmetry. The CDF is computed via the regularized lower incomplete gamma function using series expansion or Lentz continued fraction, selecting the faster-converging method based on the argument relative to the shape parameter.

## Historical Context

The gamma distribution was first studied by Leonard Euler (1729) through his generalization of the factorial function to the gamma function $\Gamma(z)$. The distribution itself was formalized by Karl Pearson (1893) as part of his system of frequency curves, where it appears as a Type III distribution. The incomplete gamma function, central to computing the CDF, was tabulated extensively by Pearson (1922) before computational methods made tables obsolete.

In finance, the gamma distribution models positively-skewed quantities: waiting times between events (generalizing the exponential), aggregate claim sizes in insurance (actuarial science), and the distribution of realized volatility (which is approximately gamma-distributed under certain stochastic volatility models). The chi-squared distribution is a special case with $\alpha = k/2$ and $\beta = 2$ (where $k$ is degrees of freedom), connecting GAMMADIST to variance-based statistical tests.

The implementation uses two complementary algorithms for the regularized incomplete gamma function: a series expansion that converges rapidly for $x < \alpha + 1$, and a Lentz continued fraction for $x \ge \alpha + 1$. This split ensures convergence in approximately 10-30 iterations across the entire parameter space.

## Architecture and Physics

The computation follows a three-phase pipeline:

**Phase 1: Min-max normalization** scans `period` bars for extrema, maps the current source to $x \in [0, 1]$, then scales by the rate parameter: $\text{scaled} = \max(0, x \cdot \beta)$.

**Phase 2: Algorithm selection** chooses between series and continued fraction based on the relationship between the scaled input and the shape parameter:
- If $\text{scaled} < \alpha + 1$: use the series expansion (converges from below)
- If $\text{scaled} \ge \alpha + 1$: use $1 - Q(\alpha, \text{scaled})$ via continued fraction (converges from above)

**Phase 3: CDF evaluation** computes the regularized lower incomplete gamma function $P(\alpha, \text{scaled})$.

The **series expansion** accumulates terms $\delta_n = x^n / (\alpha(\alpha+1)\cdots(\alpha+n))$ until the relative change drops below $10^{-10}$, then multiplies by the normalization factor $x^\alpha e^{-x} / \Gamma(\alpha)$.

The **continued fraction** (Lentz algorithm) evaluates the complementary function $Q(\alpha, x) = 1 - P(\alpha, x)$ using the recurrence with convergents $a_i = -i(i - \alpha)$ and $b_i = x + 2i + 1 - \alpha$.

**Shape parameter effects**: $\alpha = 1$ reduces to exponential distribution. $\alpha = 2, \beta = 3$ (default) gives a moderate right-skewed S-curve. Large $\alpha$ approaches a normal CDF shape.

## Mathematical Foundation

The gamma distribution with shape $\alpha > 0$ and rate $\beta > 0$ has PDF:

$$f(x; \alpha, \beta) = \frac{\beta^\alpha}{\Gamma(\alpha)} x^{\alpha-1} e^{-\beta x}, \quad x > 0$$

The CDF is the **regularized lower incomplete gamma function**:

$$F(x; \alpha, \beta) = P(\alpha, \beta x) = \frac{\gamma(\alpha, \beta x)}{\Gamma(\alpha)} = \frac{1}{\Gamma(\alpha)} \int_0^{\beta x} t^{\alpha-1} e^{-t}\,dt$$

**Series expansion** for $P(a, x)$ when $x < a + 1$:

$$P(a, x) = e^{-x} x^a \sum_{n=0}^{\infty} \frac{x^n}{a(a+1)\cdots(a+n)}$$

**Continued fraction** for $Q(a, x) = 1 - P(a, x)$ when $x \ge a + 1$:

$$Q(a, x) = e^{-x} x^a \cdot \cfrac{1}{x + 1 - a + \cfrac{1 \cdot (1-a)}{x + 3 - a + \cfrac{2 \cdot (2-a)}{x + 5 - a + \cdots}}}$$

**Log-gamma** via Lanczos approximation ($g = 7$, 9 coefficients):

$$\ln\Gamma(z) = \frac{1}{2}\ln(2\pi) + \left(z - \frac{1}{2}\right)\ln(z + g - \frac{1}{2}) - (z + g - \frac{1}{2}) + \ln\!\left(\sum_{k=0}^{8} \frac{c_k}{z + k}\right)$$

**Parameter constraints**: `period` $> 0$, $\alpha > 0$, $\beta > 0$. Output is bounded $[0, 1]$.

```
GAMMADIST(source, period, shape, rate):
    // Phase 1: min-max normalization + scaling
    min_val = min(source[0..period-1])
    max_val = max(source[0..period-1])
    range = max_val - min_val
    x = range > 0 ? (source - min_val) / range : 0.5
    scaled = max(0, x * rate)

    // Phase 2-3: regularized lower incomplete gamma
    if scaled <= 0: return 0.0
    if scaled < shape + 1:
        return gammaSeries(shape, scaled)     // series expansion
    else:
        return 1.0 - gammaCF(shape, scaled)   // continued fraction
```

## Resources

- Pearson, K. "Contributions to the Mathematical Theory of Evolution." Phil. Trans. Royal Society, 1893.
- Lanczos, C. "A Precision Approximation of the Gamma Function." SIAM J. Numerical Analysis B, 1964.
- Press, W.H. et al. "Numerical Recipes: The Art of Scientific Computing." 3rd edition, Cambridge University Press, 2007. Chapter 6.2 (Incomplete Gamma Function).
- Lentz, W.J. "Generating Bessel Functions in Mie Scattering Calculations Using Continued Fractions." Applied Optics, 1976.
- Johnson, N.L., Kotz, S. & Balakrishnan, N. "Continuous Univariate Distributions, Vol. 1." Wiley, 1994.
