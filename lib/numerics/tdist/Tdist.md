# TDIST: Student's t-Distribution CDF

The Student's t-Distribution CDF transforms a min-max normalized price into the cumulative distribution function of Student's t-distribution, producing an output in $[0, 1]$. The t-distribution is the normal distribution's heavier-tailed cousin: as degrees of freedom $\nu$ increase, it converges to the Gaussian; at low $\nu$ it accommodates extreme values that the normal distribution would assign negligible probability. The implementation normalizes price to $[0, 1]$, maps to a t-statistic via linear scaling to $[-3, +3]$, then evaluates the CDF through the regularized incomplete beta function. This makes TDIST a robust percentile ranking that is less sensitive to outliers than NORMDIST.

## Historical Context

The t-distribution was derived by William Sealy Gosset (1908), publishing under the pseudonym "Student" while employed at the Guinness brewery. Gosset needed to make statistical inferences from small sample sizes where the population variance was unknown. Ronald Fisher (1925) generalized the distribution and introduced the degrees-of-freedom parameter.

In finance, the t-distribution has become central to fat-tailed modeling. Empirical studies consistently show that asset returns have heavier tails than the normal distribution (Mandelbrot, 1963; Fama, 1965). The t-distribution with $\nu \approx 4\text{-}6$ provides a reasonable fit to daily equity returns, and it underpins GARCH-t models, Student-t copulas in credit risk, and robust regression in factor modeling.

The CDF is computed via the identity connecting it to the regularized incomplete beta function:

$$F(t; \nu) = \begin{cases} 1 - \frac{1}{2} I_x\!\left(\frac{\nu}{2}, \frac{1}{2}\right) & t \ge 0 \\ \frac{1}{2} I_x\!\left(\frac{\nu}{2}, \frac{1}{2}\right) & t < 0 \end{cases}$$

where $x = \nu/(\nu + t^2)$. This reuses the same Lanczos log-gamma and Lentz continued fraction infrastructure as BETADIST and FDIST.

## Architecture and Physics

The computation follows a four-phase pipeline:

**Phase 1: Min-max normalization** scans `period` bars for extrema, maps the current source to $x \in [0, 1]$.

**Phase 2: t-statistic mapping** transforms $x$ to a t-value via linear scaling:

$$t = (x - 0.5) \times 6.0$$

This maps $[0, 1]$ to $[-3, +3]$, covering approximately 99.7% of the standard normal range and the bulk of any t-distribution with $\nu \ge 3$.

**Phase 3: Beta function argument** converts the t-statistic to the incomplete beta argument:

$$\text{bx} = \frac{\nu}{\nu + t^2}$$

For $t = 0$, $\text{bx} = 1$ and the CDF returns 0.5 (symmetric around zero). As $|t|$ grows, $\text{bx}$ approaches 0.

**Phase 4: Regularized incomplete beta** evaluates $I_{\text{bx}}(\nu/2, 1/2)$ via the Lentz continued fraction with symmetry flip for numerical stability. The sign of $t$ determines whether the result is in the lower or upper tail.

**Degrees-of-freedom effects**: $\nu = 1$ gives the Cauchy distribution (extremely heavy tails, no finite mean). $\nu = 5$ gives moderately heavy tails. $\nu = 30$ is nearly indistinguishable from the normal. $\nu \to \infty$ converges to $N(0, 1)$.

## Mathematical Foundation

The Student's t-distribution with $\nu$ degrees of freedom has PDF:

$$f(t; \nu) = \frac{\Gamma\!\left(\frac{\nu+1}{2}\right)}{\sqrt{\nu\pi}\;\Gamma\!\left(\frac{\nu}{2}\right)} \left(1 + \frac{t^2}{\nu}\right)^{-(\nu+1)/2}$$

The CDF via regularized incomplete beta:

$$F(t; \nu) = \begin{cases} 1 - \frac{1}{2} I_x\!\left(\frac{\nu}{2}, \frac{1}{2}\right) & t \ge 0 \\[4pt] \frac{1}{2} I_x\!\left(\frac{\nu}{2}, \frac{1}{2}\right) & t < 0 \end{cases}$$

where $x = \frac{\nu}{\nu + t^2}$.

**Moments** (defined only when $\nu$ is sufficiently large):

$$E[T] = 0 \;(\nu > 1), \quad \text{Var}(T) = \frac{\nu}{\nu - 2} \;(\nu > 2), \quad \text{Kurt} = \frac{6}{\nu - 4} \;(\nu > 4)$$

**Convergence to normal**: As $\nu \to \infty$, $t_\nu \to N(0,1)$. For practical purposes, $\nu \ge 30$ produces CDF values within $10^{-3}$ of the normal CDF.

**Parameter constraints**: `period` $> 0$, $\nu > 0$. Output is bounded $[0, 1]$.

```
TDIST(source, period, df):
    // Phase 1: min-max normalization
    min_val = min(source[0..period-1])
    max_val = max(source[0..period-1])
    range = max_val - min_val
    x = range > 0 ? (source - min_val) / range : 0.5

    // Phase 2: t-statistic mapping
    t = (x - 0.5) * 6.0

    // Phase 3: beta argument
    bx = df / (df + t*t)

    // Phase 4: CDF via incomplete beta
    ibeta = betaReg(bx, df/2, 0.5)
    if t >= 0: return 1.0 - 0.5 * ibeta
    else:      return 0.5 * ibeta
```

## Resources

- Student (Gosset, W.S.). "The Probable Error of a Mean." Biometrika, 1908.
- Fisher, R.A. "Statistical Methods for Research Workers." Oliver and Boyd, 1925.
- Mandelbrot, B. "The Variation of Certain Speculative Prices." Journal of Business, 1963.
- Fama, E.F. "The Behavior of Stock-Market Prices." Journal of Business, 1965.
- Bollerslev, T. "Generalized Autoregressive Conditional Heteroskedasticity." Journal of Econometrics, 1986.
- Press, W.H. et al. "Numerical Recipes: The Art of Scientific Computing." 3rd edition, Cambridge University Press, 2007. Chapter 6.4.
