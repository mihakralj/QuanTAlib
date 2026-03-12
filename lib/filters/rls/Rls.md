# RLS: Recursive Least Squares Adaptive Filter

> *The man who has no patience has no wisdom.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Filter                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `order` (default 16), `lambda` (default 0.99)                      |
| **Outputs**      | Single series (RLS)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `order + 1` bars                          |
| **PineScript**   | [rls.pine](rls.pine)                       |
| **Signature**    | [rls_signature](rls_signature.md) |


- The Recursive Least Squares (RLS) adaptive filter is the Rolls-Royce of adaptive FIR filters.
- Parameterized by `order` (default 16), `lambda` (default 0.99).
- Output range: Tracks input.
- Requires `order + 1` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

## Introduction

The Recursive Least Squares (RLS) adaptive filter is the Rolls-Royce of adaptive FIR filters. Where LMS crawls toward the Wiener solution one gradient step at a time, RLS arrives in approximately *order* iterations by maintaining an inverse correlation matrix $P$ that captures the full second-order statistics of the input signal. The trade-off is computational: $O(n^2)$ per bar versus LMS's $O(n)$, where $n$ is the filter order. For orders below 64, the convergence advantage typically outweighs the cost.

## Historical Context

RLS traces its lineage to Gauss's method of least squares (1795) and Kalman's recursive state estimation (1960). The exponentially-weighted RLS form — with forgetting factor $\lambda$ — was formalized in the signal processing literature of the 1970s and 1980s, primarily by Haykin, Widrow, and Ljung. Unlike LMS, which adapts proportionally to the instantaneous gradient, RLS minimizes the weighted sum of all past squared errors, making it optimal in a least-squares sense at every time step.

In financial applications, RLS excels at tracking non-stationary price dynamics. The forgetting factor $\lambda$ controls the effective memory horizon: $\lambda = 0.99$ gives a memory of roughly $1/(1-\lambda) = 100$ bars, while $\lambda = 0.95$ compresses memory to 20 bars. This makes RLS particularly suited for regime changes and structural breaks where LMS's fixed step size is too slow to react.

The implementation here follows the standard RLS algorithm with no look-ahead, no leakage, and no regularization beyond the initial $P = \delta I$ scaling.

## Architecture and Physics

### 1. Adaptive Weight Vector

The filter maintains $n$ weights $w_0, w_1, \ldots, w_{n-1}$ that adapt to predict the current input from its recent history:

$$\hat{y}(t) = \sum_{i=0}^{n-1} w_i \cdot x(t-i-1)$$

The prediction uses values $x(t-1)$ through $x(t-n)$ — no look-ahead.

### 2. Inverse Correlation Matrix

The core of RLS is the $n \times n$ inverse correlation matrix $P$, initialized to $\delta I$ where $\delta = 100$ represents high initial uncertainty. This matrix is updated recursively at each step, avoiding the $O(n^3)$ cost of explicit matrix inversion.

### 3. Gain Vector

The Kalman-like gain vector determines how much each weight adjusts in response to prediction error:

$$k(t) = \frac{P(t-1) \cdot x(t)}{\lambda + x(t)^T \cdot P(t-1) \cdot x(t)}$$

### 4. Weight and Matrix Update

After computing the a priori error $e(t) = d(t) - \hat{y}(t)$:

$$w(t) = w(t-1) + k(t) \cdot e(t)$$

$$P(t) = \frac{1}{\lambda}\left(P(t-1) - k(t) \cdot x(t)^T \cdot P(t-1)\right)$$

### 5. Forgetting Factor

The forgetting factor $\lambda \in (0, 1]$ exponentially discounts past observations. The effective memory window is approximately $1/(1-\lambda)$ samples.

| $\lambda$ | Effective Memory | Use Case |
|-----------|------------------|----------|
| 1.00 | Infinite (growing) | Stationary signals |
| 0.99 | ~100 bars | Moderate non-stationarity |
| 0.95 | ~20 bars | Fast-changing dynamics |
| 0.90 | ~10 bars | Highly non-stationary |

## Mathematical Foundation

### Transfer Function (z-domain)

RLS is a time-varying FIR filter. At convergence on a stationary signal, the weight vector approaches the Wiener solution:

$$w_{opt} = R^{-1} p$$

where $R$ is the input autocorrelation matrix and $p$ is the cross-correlation vector between input and desired signal. The z-domain transfer function at convergence is:

$$H(z) = \sum_{i=0}^{n-1} w_i \cdot z^{-(i+1)}$$

### Convergence Analysis

RLS converges in approximately $n$ iterations (where $n$ is the filter order), compared to LMS which requires $O(n / \mu_{\text{eff}})$ iterations. This is because RLS effectively pre-whitens the input through the $P$ matrix, decorrelating the gradient components.

### Stability Condition

The algorithm is stable when $0 < \lambda \leq 1$ and $\delta > 0$. The initial $P = \delta I$ determines convergence speed: larger $\delta$ means faster initial adaptation but potentially larger transient errors.

### Parameter Mapping

| Parameter | Symbol | Default | Range | Effect |
|-----------|--------|---------|-------|--------|
| Order | $n$ | 16 | $[2, 64]$ | Filter taps; higher = more modeling capacity |
| Lambda | $\lambda$ | 0.99 | $(0, 1]$ | Forgetting factor; lower = shorter memory |
| Delta | $\delta$ | 100.0 | $(0, \infty)$ | Initial P scaling; higher = faster initial adaptation |

## Performance Profile

### Operation Count Per Bar

| Operation | Count | Notes |
|-----------|-------|-------|
| Prediction ($w^T x$) | $O(n)$ | FMA inner product |
| $P \cdot x$ | $O(n^2)$ | Matrix-vector multiply |
| Gain vector $k$ | $O(n)$ | Scalar division + scale |
| Weight update | $O(n)$ | $w += k \cdot e$ |
| P update | $O(n^2)$ | Rank-1 outer product subtraction |
| **Total** | **$O(n^2)$** | Dominated by P operations |

### Memory Usage

| Component | Size | Notes |
|-----------|------|-------|
| Weights $w$ | $2n$ doubles | Current + snapshot |
| Matrix $P$ | $2n^2$ doubles | Current + snapshot |
| Input buffer | $n+1$ doubles | RingBuffer |
| **Total** | **$2n^2 + 3n + 1$** | ~4 KB for order=16 |

### Quality Metrics

| Metric | Score (1-10) | Notes |
|--------|:---:|-------|
| Smoothness | 7 | Tracks signal closely |
| Lag | 2 | Minimal prediction lag |
| Overshoot | 4 | Can overshoot in transients |
| Noise rejection | 7 | Good with appropriate $\lambda$ |
| Adaptability | 9 | Fast convergence to optimal |
| Computational cost | 4 | $O(n^2)$ limits practical order |

## Validation

RLS is a custom adaptive filter with no direct equivalent in standard TA libraries. Validation uses self-consistency tests.

| Test | Method | Result |
|------|--------|--------|
| Convergence | MSE decreases on sine wave | First quarter MSE > last quarter MSE |
| Streaming = Span | Mode parity | Match to $10^{-9}$ |
| Determinism | Two identical runs | Match to $10^{-15}$ |
| Constant input | Converge to constant | $\|{y - 50}\| < 1$ |
| Price tracking | Correlation test | $r > 0.5$ |
| Stability | 5000-bar dataset | All outputs finite |
| NaN safety | Interspersed NaN | All outputs finite |
| Faster than LMS | Step response comparison | RLS error < LMS error |
| Lambda sensitivity | Different $\lambda$ values | Different outputs |

## Common Pitfalls

1. **Order too large.** RLS is $O(n^2)$; order 64 means 4096 multiply-adds per bar for the P update alone. Keep order ≤ 32 for real-time use. Impact: 4× latency per doubling of order.

2. **Lambda too small.** Values below 0.9 create a memory horizon of fewer than 10 bars, causing wild weight oscillations. The filter "forgets" useful history and tracks noise. Impact: output variance increases by 3-5×.

3. **P matrix blowup.** Without regularization, $P$ can grow unbounded when $\lambda < 1$ and the input lacks sufficient excitation. The implementation guards against this via the $\epsilon$-denominator clamp. Impact: numerical overflow → NaN propagation.

4. **Confusing lambda with LMS mu.** Lambda is a forgetting factor (higher = more memory), while LMS mu is a step size (higher = faster adaptation). They have opposite semantics despite both controlling adaptation speed.

5. **Initial transient.** The first ~order bars produce passthrough output while the buffer fills. The P matrix starts at $\delta I$, so the first few predictions after warmup may be large. Impact: 2-5 bars of unreliable output after warmup.

6. **Bar correction cost.** Each isNew=false correction requires restoring both the weight vector ($n$ copies) and the P matrix ($n^2$ copies). For order=16, that is 256+16 = 272 doubles copied per correction. Impact: correction cost proportional to $n^2$.

7. **Not suitable for SIMD.** The sequential dependency chain (P update depends on gain, gain depends on P·x) prevents vectorization of the inner loop. Unlike simple FIR filters, RLS cannot benefit from AVX2/SSE parallelism.

## References

- Haykin, S. (2002). *Adaptive Filter Theory*. 4th ed. Prentice Hall. Chapters 9-10.
- Ljung, L. & Soderstrom, T. (1983). *Theory and Practice of Recursive Identification*. MIT Press.
- Sayed, A.H. (2008). *Adaptive Filters*. Wiley-IEEE Press.
- Kalman, R.E. (1960). "A New Approach to Linear Filtering and Prediction Problems." *Journal of Basic Engineering*, 82(1), 35-45.
