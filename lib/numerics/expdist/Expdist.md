# EXPDIST: Exponential Distribution CDF

> *The exponential distribution models the time between events — memoryless waiting distilled into a single rate parameter.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Numeric                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period` (default 50), `lambda` (default 3.0)                      |
| **Outputs**      | Single series (Expdist)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [expdist.pine](expdist.pine)                       |

- The Exponential Distribution CDF transforms a min-max normalized price into the cumulative distribution function of the exponential distribution, p...
- Parameterized by `period` (default 50), `lambda` (default 3.0).
- Output range: Varies (see docs).
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The Exponential Distribution CDF transforms a min-max normalized price into the cumulative distribution function of the exponential distribution, producing an output in $[0, 1]$. The exponential distribution models memoryless waiting times: the probability that a normalized value falls below a threshold depends only on the rate parameter $\lambda$, not on any history. Higher $\lambda$ values compress the CDF curve toward zero, making the indicator more sensitive to small normalized deviations. With $O(N)$ normalization and $O(1)$ CDF evaluation, EXPDIST provides a nonlinear percentile ranking that emphasizes the lower end of the price range while compressing the upper end.

## Historical Context

The exponential distribution is the continuous analog of the geometric distribution, first studied systematically by Agner Krarup Erlang (1909) in the context of telephone call modeling. Its defining property is memorylessness: $P(X > s + t \mid X > s) = P(X > t)$, making it the unique continuous distribution where the conditional probability of waiting another $t$ units is independent of time already elapsed.

In quantitative finance, the exponential CDF appears in several contexts: modeling inter-arrival times of trades (market microstructure), as a probability integral transform for goodness-of-fit testing, and as a nonlinear rescaling that emphasizes proximity to recent lows. The min-max normalization step maps raw prices into $[0, 1]$, and the CDF then provides a probabilistic interpretation: the output represents the probability that an exponentially-distributed random variable with rate $\lambda$ would fall at or below the normalized price level.

Unlike the normal or Student-t CDFs, the exponential CDF has a closed-form expression requiring only a single `exp()` call. This makes it computationally attractive for real-time applications where the heavier special-function machinery (incomplete beta, error function) of other distributions is unnecessary.

## Architecture and Physics

The indicator follows the standard two-phase pattern used across all distribution CDF indicators in this library:

**Phase 1: Min-max normalization** scans the lookback window of `period` bars to find the minimum and maximum values, then maps the current source value to $[0, 1]$:

$$x = \frac{\text{source} - \text{min}}{\text{max} - \text{min}}$$

If the range is zero (flat price), $x$ defaults to 0.5. This normalization is $O(N)$ per bar where $N$ is the period.

**Phase 2: CDF evaluation** applies the exponential CDF in $O(1)$:

$$F(x) = 1 - e^{-\lambda x}$$

with the boundary condition $F(x) = 0$ for $x \le 0$.

**Rate parameter effects**: $\lambda = 1$ gives a gentle S-curve with $F(0.5) \approx 0.39$. $\lambda = 3$ (default) gives $F(0.5) \approx 0.78$, strongly biasing toward 1.0 for values in the upper half of the range. $\lambda = 10$ saturates near 1.0 for almost any positive normalized value, functioning as a near-binary above/below-midpoint indicator.

## Mathematical Foundation

The exponential distribution with rate parameter $\lambda > 0$ has PDF and CDF:

$$f(x; \lambda) = \lambda e^{-\lambda x}, \quad x \ge 0$$

$$F(x; \lambda) = 1 - e^{-\lambda x}, \quad x \ge 0$$

**Moments of the exponential distribution:**

$$E[X] = \frac{1}{\lambda}, \quad \text{Var}(X) = \frac{1}{\lambda^2}, \quad \text{Skew} = 2, \quad \text{Kurt} = 6$$

**Inverse CDF** (quantile function):

$$F^{-1}(p) = -\frac{\ln(1 - p)}{\lambda}$$

The **memoryless property**:

$$P(X > s + t \mid X > s) = P(X > t) = e^{-\lambda t}$$

**Parameter constraints**: `period` $> 0$, $\lambda > 0$. Output is bounded $[0, 1]$.

```
EXPDIST(source, period, lambda):
    // Phase 1: min-max normalization
    min_val = min(source[0..period-1])
    max_val = max(source[0..period-1])
    range = max_val - min_val
    x = range > 0 ? (source - min_val) / range : 0.5

    // Phase 2: exponential CDF
    if x <= 0: return 0.0
    return 1.0 - exp(-lambda * x)
```


## Performance Profile

### Operation Count (Streaming Mode)

Exponential distribution CDF = 1 - exp(-lambda * x) — a trivially cheap closed-form evaluation.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Input validation (lambda > 0; x >= 0) | 2 | 2 cy | ~4 cy |
| lambda * x multiply | 1 | 3 cy | ~3 cy |
| exp(-lambda*x) | 1 | 20 cy | ~20 cy |
| 1 - exp result | 1 | 1 cy | ~1 cy |
| NaN guard + state update | 1 | 2 cy | ~2 cy |
| **Total** | **O(1)** | — | **~30 cy** |

Cheapest distribution implementation — single exp() call dominates. No series expansion, no iterative solver.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| lambda * x | Yes | Vector<double> multiply |
| exp() | Partial | _mm256_exp_pd with SVML; or scalar loop |
| 1 - result | Yes | Vector subtract |

With SVML exp: 4 outputs per AVX2 cycle. Without SVML: scalar loop but still O(1) per output. Batch is trivially parallelizable.

## Resources

- Erlang, A.K. "The Theory of Probabilities and Telephone Conversations." Nyt Tidsskrift for Matematik B, 1909.
- Johnson, N.L., Kotz, S. & Balakrishnan, N. "Continuous Univariate Distributions, Vol. 1." Wiley, 1994.
- Ross, S. "Introduction to Probability Models." Academic Press, 12th edition, 2019.
- Cont, R. "Empirical Properties of Asset Returns: Stylized Facts and Statistical Issues." Quantitative Finance, 2001.
