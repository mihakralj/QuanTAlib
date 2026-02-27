# FDIST: F-Distribution CDF

The F-Distribution CDF transforms a min-max normalized price into the cumulative distribution function of the F-distribution (Fisher-Snedecor distribution), producing an output in $[0, 1]$. The F-distribution arises as the ratio of two chi-squared random variables divided by their respective degrees of freedom, making it the natural distribution for variance ratio tests. By mapping normalized price through the regularized incomplete beta function with parameters tied to degrees of freedom $d_1$ and $d_2$, FDIST provides a probabilistic ranking that is asymmetric: the CDF shape changes qualitatively depending on whether $d_1 < d_2$, $d_1 = d_2$, or $d_1 > d_2$, giving traders control over the nonlinear response curve.

## Historical Context

The F-distribution was developed independently by George Snedecor (1934) and Ronald Fisher (1924), though Fisher's earlier work on variance ratios laid the theoretical foundation. The distribution is named in Fisher's honor by Snedecor. Its primary statistical application is the F-test for comparing variances of two populations, and it forms the backbone of ANOVA (Analysis of Variance), one of the most widely used statistical procedures.

In financial applications, the F-distribution appears in variance ratio tests (Lo and MacKinlay, 1988) used to test the random walk hypothesis. The CDF form used here repurposes the distribution's shape as a nonlinear mapping: with equal degrees of freedom ($d_1 = d_2$), the CDF is approximately symmetric around 0.5; with $d_1 \gg d_2$, the curve shifts left (more probability mass near zero); with $d_1 \ll d_2$, it shifts right. This parameter-controlled asymmetry distinguishes FDIST from simpler sigmoid-like transformations.

The implementation uses the same Lanczos log-gamma and Lentz continued fraction machinery as BETADIST, since the F-distribution CDF reduces to a regularized incomplete beta function through a variable substitution.

## Architecture and Physics

The computation follows a three-phase pipeline:

**Phase 1: Min-max normalization** scans `period` bars to find extrema, then maps the current source to $x \in [0, 1]$. Zero-range defaults to 0.5.

**Phase 2: Variable transformation** converts the normalized value $x$ to the beta function argument:

$$t = \frac{d_1 \cdot x}{d_1 \cdot x + d_2}$$

This maps $x \in [0, \infty)$ to $t \in [0, 1)$, which is the domain of the regularized incomplete beta function. Since input $x$ is already in $[0, 1]$, the effective range of $t$ is $[0, d_1/(d_1 + d_2)]$.

**Phase 3: Regularized incomplete beta** evaluates $I_t(d_1/2, d_2/2)$ using the Lentz continued fraction algorithm. The implementation includes a reflection step when $x > (a+1)/(a+b+2)$ to ensure the continued fraction converges from the faster side. Convergence typically requires 10-20 iterations to reach $\epsilon = 10^{-10}$.

**Shared infrastructure**: The `lnGamma()` function uses the Lanczos approximation with $g = 7$ and 9 coefficients, identical to the implementation in BETADIST and other distribution indicators. The `betaReg()` continued fraction is likewise shared.

## Mathematical Foundation

The F-distribution with $d_1$ numerator and $d_2$ denominator degrees of freedom has PDF:

$$f(x; d_1, d_2) = \frac{1}{B(d_1/2, d_2/2)} \cdot \left(\frac{d_1}{d_2}\right)^{d_1/2} \cdot \frac{x^{d_1/2 - 1}}{(1 + d_1 x / d_2)^{(d_1+d_2)/2}}$$

The CDF is expressed via the regularized incomplete beta function:

$$F(x; d_1, d_2) = I_t\!\left(\frac{d_1}{2}, \frac{d_2}{2}\right), \quad t = \frac{d_1 x}{d_1 x + d_2}$$

where the **regularized incomplete beta function** is:

$$I_x(a, b) = \frac{B(x; a, b)}{B(a, b)} = \frac{1}{B(a, b)} \int_0^x t^{a-1}(1-t)^{b-1}\,dt$$

**Lentz continued fraction** for $I_x(a, b)$:

$$I_x(a,b) = \frac{x^a (1-x)^b}{a \cdot B(a,b)} \cdot \cfrac{1}{1 + \cfrac{d_1}{1 + \cfrac{d_2}{1 + \cdots}}}$$

with convergents $d_m$ defined by the even/odd recurrence involving $a$, $b$, and $x$.

**Parameter constraints**: `period` $> 0$, $d_1 > 0$, $d_2 > 0$. Output is bounded $[0, 1]$.

```
FDIST(source, period, d1, d2):
    // Phase 1: min-max normalization
    min_val = min(source[0..period-1])
    max_val = max(source[0..period-1])
    range = max_val - min_val
    x = range > 0 ? (source - min_val) / range : 0.5

    // Phase 2: variable transformation
    safe_x = max(0, x)
    t = d1 * safe_x / (d1 * safe_x + d2)

    // Phase 3: regularized incomplete beta via Lentz CF
    return betaReg(t, d1/2, d2/2)
```


## Performance Profile

### Operation Count (Streaming Mode)

F-distribution CDF uses regularized incomplete beta function — same cost structure as BetaDist.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Input validation (d1, d2 > 0; x >= 0) | 3 | 2 cy | ~6 cy |
| Transform x to beta variable | 1 | 3 cy | ~3 cy |
| Regularized incomplete beta (Lentz CF, ~20 iter) | ~20 | 15 cy | ~300 cy |
| NaN guard + state update | 1 | 2 cy | ~2 cy |
| **Total** | **O(1)** | — | **~311 cy** |

O(1) per evaluation. Dominated by the continued fraction solver, same as Beta/T distributions. Degrees-of-freedom parameters affect convergence speed slightly.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| x transformation | Yes | Vector arithmetic |
| Continued fraction | No | Sequential convergence |
| Output assignment | Yes | Trivial |

No SIMD benefit for the core evaluation. Outer loop across observations parallelizable with PLINQ for bulk p-value computation.

## Resources

- Fisher, R.A. "On a Distribution Yielding the Error Functions of Several Well Known Statistics." Proc. International Mathematical Congress, Toronto, 1924.
- Snedecor, G.W. "Calculation and Interpretation of Analysis of Variance and Covariance." Collegiate Press, 1934.
- Press, W.H. et al. "Numerical Recipes: The Art of Scientific Computing." 3rd edition, Cambridge University Press, 2007. Chapter 6.4 (Incomplete Beta Function).
- Lo, A. & MacKinlay, A.C. "Stock Market Prices Do Not Follow Random Walks: Evidence from a Simple Specification Test." Review of Financial Studies, 1988.
- Lentz, W.J. "Generating Bessel Functions in Mie Scattering Calculations Using Continued Fractions." Applied Optics, 1976.
