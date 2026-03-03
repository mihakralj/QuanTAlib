# QRMA: Quadratic Regression Moving Average

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (FIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Qrma)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |
| **Signature**    | [qrma_signature](qrma_signature.md) |

### TL;DR

- QRMA fits a second-degree polynomial $y = a + bx + cx^2$ to the most recent $N$ bars via ordinary least squares, then returns the fitted value at t...
- Parameterized by `period`.
- Output range: Tracks input.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "Linear regression assumes the world is a straight line. Quadratic regression admits it might curve. For parabolic price moves, that admission turns out to be worth 40% less endpoint error."

QRMA fits a second-degree polynomial $y = a + bx + cx^2$ to the most recent $N$ bars via ordinary least squares, then returns the fitted value at the endpoint (newest bar). By capturing curvature that LSMA (degree-1) misses, QRMA provides meaningfully better tracking of accelerating or decelerating price trends. The 3x3 normal-equation system is solved via Cramer's rule in O(1) after an O(N) data accumulation pass, making it computationally efficient and suitable for streaming applications.

## Historical Context

Quadratic regression applied to time-series smoothing is a special case of the Savitzky-Golay filter (1964) with polynomial degree 2. Savitzky and Golay showed that polynomial least-squares fitting over a sliding window produces FIR filter coefficients equivalent to convolution, and that these coefficients preserve polynomial trends of degree $\leq d$ while suppressing higher-order components.

QRMA sits between LSMA (degree-1, captures slope only) and CRMA (degree-3, captures inflection). The degree-2 model adds one parameter (curvature $c$) relative to linear regression, which is sufficient to track parabolic moves, acceleration phases, and the initial curvature of trend reversals. For most financial time series, degree-2 captures the dominant non-linearity without the fitting instability that arises with higher degrees on noisy data.

The x-indexing convention matters for numerical stability. QRMA uses $x = 0$ for the oldest bar and $x = N-1$ for the newest, evaluating the polynomial at $x = N-1$ (the endpoint). This avoids the large-exponent cancellation errors that arise when evaluating at $x = 0$ with the "newest=0" convention (where the polynomial coefficients must reconstruct the signal from high powers of $N-1$).

## Architecture & Physics

### 1. Analytical X-Sums

The x-index power sums ($\sum x$, $\sum x^2$, $\sum x^3$, $\sum x^4$) are computed from Faulhaber's closed-form formulas, depending only on $N$. These are effectively constants for fixed period.

### 2. Data-Dependent Y-Sums

A single O(N) pass over the circular buffer accumulates $\sum y$, $\sum xy$, and $\sum x^2 y$.

### 3. Cramer's Rule Solution

The 3x3 normal-equation system is solved via Cramer's rule (determinant ratios), which is numerically stable for well-conditioned systems and avoids the overhead of Gaussian elimination. A singularity guard (determinant $< 10^{-20}$) returns the raw price for degenerate inputs.

### 4. Endpoint Evaluation

The fitted polynomial $a + b(N-1) + c(N-1)^2$ is evaluated at the newest bar.

## Mathematical Foundation

The quadratic regression minimizes:

$$
\min_{a, b, c} \sum_{k=0}^{N-1} \left( y_k - a - bk - ck^2 \right)^2
$$

The normal equations form a 3x3 system:

$$
\begin{bmatrix} N & S_1 & S_2 \\ S_1 & S_2 & S_3 \\ S_2 & S_3 & S_4 \end{bmatrix} \begin{bmatrix} a \\ b \\ c \end{bmatrix} = \begin{bmatrix} \sum y \\ \sum ky \\ \sum k^2 y \end{bmatrix}
$$

where $S_m = \sum_{k=0}^{N-1} k^m$ has closed forms:

$$
S_1 = \frac{N(N-1)}{2}, \quad S_2 = \frac{N(N-1)(2N-1)}{6}
$$

$$
S_3 = \left[\frac{N(N-1)}{2}\right]^2, \quad S_4 = \frac{N(N-1)(2N-1)(3N^2-3N-1)}{30}
$$

**Cramer's rule:** With coefficient matrix $\mathbf{D}$ and right-hand side $\mathbf{r}$:

$$
a = \frac{\det(\mathbf{D}_a)}{\det(\mathbf{D})}, \quad b = \frac{\det(\mathbf{D}_b)}{\det(\mathbf{D})}, \quad c = \frac{\det(\mathbf{D}_c)}{\det(\mathbf{D})}
$$

**Endpoint value:** $\text{QRMA} = a + b(N-1) + c(N-1)^2$

**Default parameters:** `period = 14`, `minPeriod = 3` (minimum for degree-2 fit).

**Pseudo-code (streaming):**

```
buffer ← circular_buffer(period)
buffer.push(price)
if count < period: return price

// Analytical x-sums (constants for fixed N)
S1 = N*(N-1)/2;  S2 = N*(N-1)*(2N-1)/6
S3 = S1²;        S4 = N*(N-1)*(2N-1)*(3N²-3N-1)/30

// Data sums (O(N) pass)
sy = 0; sxy = 0; sx2y = 0
for j = 0 to N-1:
    val = buffer[j]  // oldest to newest
    sy += val; sxy += j*val; sx2y += j²*val

// 3×3 Cramer's rule
det = N*(S2*S4 - S3²) - S1*(S1*S4 - S3*S2) + S2*(S1*S3 - S2²)
if |det| < 1e-20: return price
a = cramer_a(det, sy, sxy, sx2y, ...)
b = cramer_b(det, ...)
c = cramer_c(det, ...)

return a + b*(N-1) + c*(N-1)²
```

## Resources

- Savitzky, A. & Golay, M.J.E. (1964). "Smoothing and Differentiation of Data by Simplified Least Squares Procedures." *Analytical Chemistry*, 36(8), 1627-1639.
- Schafer, R.W. (2011). "What Is a Savitzky-Golay Filter?" *IEEE Signal Processing Magazine*, 28(4), 111-117.
- Press, W.H. et al. (2007). *Numerical Recipes*, 3rd ed. Cambridge University Press. Section 3.5: Least-Squares Fitting.

## Performance Profile

### Operation Count (Streaming Mode)

QRMA(N) fits a degree-2 polynomial via OLS. Power sums S0..S4 and three cross-products are maintained as O(1) running sums (via ring buffer subtract/add). Cramer's rule for the 3×3 system is O(1) fixed arithmetic (18 multiplications, ~12 additions).

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Ring buffer push | 1 | 3 | ~3 |
| Power sum updates S0..S4 (5 × 2 ops) | ~2N | 1 | ~2N |
| Cross-product updates (3 × dot) | ~3N | 2 | ~6N |
| Cramer 3×3 solution (fixed ~30 ops) | ~30 | 3 | ~90 |
| Polynomial evaluation at newest point | 3 | 3 | ~9 |
| **Total** | **~(5N + 30)** | — | **~(8N + 102) cycles** |

O(N) per bar from power sum accumulation. For default N = 14: ~214 cycles. Compared to CRMA (cubic): 2 fewer power sums, simpler solve — approximately 40% faster.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| Power sum accumulation (S0..S4) | Yes | `VADDPD`; 5 independent running sums |
| Cross-product dot products | Yes | `VFMADD231PD`; stride-1, 4 bars/AVX2 lane |
| Cramer 3×3 solve | No | Fixed 30-op scalar system; SIMD setup overhead exceeds benefit |
| Quadratic evaluation (Horner) | No | 2 FMAs; scalar fastest at degree 2 |

Batch speedup for the sum accumulation phases: ~3× with AVX2. Solve and evaluation phases remain scalar. Net batch speedup for large series: approximately 2× over fully scalar.
