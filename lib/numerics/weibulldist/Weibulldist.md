# WEIBULLDIST: Weibull Distribution CDF

> *The Weibull distribution models failure rates that change over time — a flexible tool for reliability and survival analysis.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Numeric                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `k` (default 1.5), `lambda` (default 1.0), `period` (default 14)                      |
| **Outputs**      | Single series (Weibulldist)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [weibulldist.pine](weibulldist.pine)                       |

- The Weibull Distribution CDF transforms a min-max normalized price into the cumulative distribution function of the Weibull distribution, producing...
- Parameterized by `k` (default 1.5), `lambda` (default 1.0), `period` (default 14).
- Output range: Varies (see docs).
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The Weibull Distribution CDF transforms a min-max normalized price into the cumulative distribution function of the Weibull distribution, producing an output in $[0, 1]$. The Weibull distribution is a flexible two-parameter family that subsumes the exponential distribution ($k = 1$) and approximates the normal distribution ($k \approx 3.6$) as special cases. Its closed-form CDF requires only `pow` and `exp`, making it the computationally cheapest distribution indicator after EXPDIST. The shape parameter $k$ controls the CDF curvature: $k < 1$ produces a concave curve (rapid initial rise), $k = 1$ gives the exponential, $k = 2$ produces the Rayleigh distribution, and $k > 3$ creates an S-shaped curve approaching Gaussian behavior.

## Historical Context

The Weibull distribution was formalized by Waloddi Weibull (1951) for modeling material fatigue and breaking strength, though the mathematical form appeared earlier in work by Rosin and Rammler (1933) on particle size distributions and Frechet (1927) on extreme value theory. It is one of three extreme value distributions (alongside Gumbel and Frechet), making it theoretically grounded for modeling maxima and minima of samples.

In engineering, the Weibull distribution dominates reliability analysis: the shape parameter $k$ (also called the Weibull modulus) characterizes the failure rate. $k < 1$ means decreasing failure rate (infant mortality), $k = 1$ means constant failure rate (random failures), and $k > 1$ means increasing failure rate (wear-out). This maps to financial interpretation: $k < 1$ emphasizes breakouts from the bottom of the range (rapid CDF rise for small normalized values), while $k > 1$ emphasizes breakouts near the top (CDF stays low until normalized value approaches the scale parameter).

The scale parameter $\lambda$ controls the characteristic life: the value at which the CDF equals $1 - e^{-1} \approx 0.632$. With default $\lambda = 0.5$, the CDF reaches 63.2% when the normalized price is at the midpoint of the recent range.

## Architecture and Physics

The computation follows a two-phase pipeline:

**Phase 1: Min-max normalization** scans `period` bars for extrema, maps the current source to $x \in [0, 1]$. Zero-range defaults to 0.5.

**Phase 2: Closed-form CDF** evaluates:

$$F(x) = 1 - \exp\!\left(-\left(\frac{x}{\lambda}\right)^k\right)$$

with a floor of $x = 0$ (negative values impossible after normalization). The computation requires one division, one `pow`, one negation, and one `exp`. No special functions, no iterations, no convergence checks.

**Shape parameter effects on the CDF curve:**

| $k$ | Character | Financial Interpretation |
|-----|-----------|------------------------|
| 0.5 | Steep concave | Highly sensitive to any move off the low |
| 1.0 | Exponential | Memoryless; equivalent to EXPDIST with $\lambda = 1/\text{scale}$ |
| 2.0 | Rayleigh | Linear failure rate; moderate S-curve |
| 3.6 | Near-Gaussian | Approximate normal CDF shape |
| 5.0+ | Steep sigmoid | Insensitive until price nears the scale point, then jumps |

**Scale parameter effects**: $\lambda = 0.25$ compresses the transition zone toward low normalized values (CDF saturates quickly). $\lambda = 1.0$ spreads the transition across the entire $[0, 1]$ range (CDF is gentler). Default $\lambda = 0.5$ centers the characteristic value at the midpoint.

## Mathematical Foundation

The Weibull distribution with shape $k > 0$ and scale $\lambda > 0$ has PDF and CDF:

$$f(x; k, \lambda) = \frac{k}{\lambda}\left(\frac{x}{\lambda}\right)^{k-1} \exp\!\left(-\left(\frac{x}{\lambda}\right)^k\right), \quad x \ge 0$$

$$F(x; k, \lambda) = 1 - \exp\!\left(-\left(\frac{x}{\lambda}\right)^k\right), \quad x \ge 0$$

**Inverse CDF** (quantile function):

$$F^{-1}(p) = \lambda \left(-\ln(1 - p)\right)^{1/k}$$

**Moments:**

$$E[X] = \lambda\,\Gamma\!\left(1 + \frac{1}{k}\right)$$

$$\text{Var}(X) = \lambda^2 \left[\Gamma\!\left(1 + \frac{2}{k}\right) - \Gamma^2\!\left(1 + \frac{1}{k}\right)\right]$$

**Hazard function** (failure rate):

$$h(x) = \frac{f(x)}{1 - F(x)} = \frac{k}{\lambda}\left(\frac{x}{\lambda}\right)^{k-1}$$

This is increasing for $k > 1$, constant for $k = 1$, and decreasing for $k < 1$.

**Special cases**: $k = 1 \Rightarrow \text{Exponential}(\lambda)$. $k = 2 \Rightarrow \text{Rayleigh}(\lambda/\sqrt{2})$.

**Parameter constraints**: `period` $> 0$, $k > 0$, $\lambda > 0$. Output is bounded $[0, 1]$.

```
WEIBULLDIST(source, period, shape, scale):
    // Phase 1: min-max normalization
    min_val = min(source[0..period-1])
    max_val = max(source[0..period-1])
    range = max_val - min_val
    x = range > 0 ? (source - min_val) / range : 0.5

    // Phase 2: closed-form CDF
    safe_x = max(0, x)
    ratio = safe_x / scale
    raised = pow(ratio, shape)
    return 1.0 - exp(-raised)
```


## Performance Profile

### Operation Count (Streaming Mode)

Weibull CDF = 1 - exp(-(x/lambda)^k) — closed form with one pow() + one exp().

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Input validation (k, lambda > 0; x >= 0) | 3 | 2 cy | ~6 cy |
| (x / lambda)^k via exp(k * log(x/lambda)) | 1 | 30 cy | ~30 cy |
| exp(negated power) | 1 | 20 cy | ~20 cy |
| 1 - exp result | 1 | 1 cy | ~1 cy |
| NaN guard + state update | 1 | 2 cy | ~2 cy |
| **Total** | **O(1)** | — | **~59 cy** |

O(1) closed-form evaluation. pow() via exp(k*log(x)) is the dominant cost (~30 cy). When k is an integer, integer pow() reduces to repeated multiply (~5 cy).

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| log(x/lambda) | Partial | _mm256_log_pd with SVML |
| k * log result | Yes | Vector multiply with broadcast k |
| exp() | Partial | _mm256_exp_pd with SVML |
| 1 - exp | Yes | Vector subtract |

With SVML: nearly full vectorization. Without SVML: scalar loop but trivially parallelizable. Expected 3× batch speedup with SVML.

## Resources

- Weibull, W. "A Statistical Distribution Function of Wide Applicability." Journal of Applied Mechanics, 1951.
- Frechet, M. "Sur la loi de probabilite de l'ecart maximum." Ann. Soc. Polon. Math., 1927.
- Rinne, H. "The Weibull Distribution: A Handbook." CRC Press, 2009.
- Abernethy, R.B. "The New Weibull Handbook." 5th edition, 2006.
- Johnson, N.L., Kotz, S. & Balakrishnan, N. "Continuous Univariate Distributions, Vol. 1." Wiley, 1994.
