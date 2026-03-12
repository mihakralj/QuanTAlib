# CRMA: Cubic Regression Moving Average

> *Linear regression tells you where the trend is going. Quadratic regression tells you it's curving. Cubic regression tells you the curve is changing its mind.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Trend (FIR MA)                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Crma)                       |
| **Output range** | Tracks input                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [crma.pine](crma.pine)                       |
| **Signature**    | [crma_signature](crma_signature.md) |

- CRMA fits a degree-3 polynomial $y = a_0 + a_1 x + a_2 x^2 + a_3 x^3$ to the most recent $N$ bars via ordinary least squares, then returns the fitt...
- Parameterized by `period`.
- Output range: Tracks input.
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

CRMA fits a degree-3 polynomial $y = a_0 + a_1 x + a_2 x^2 + a_3 x^3$ to the most recent $N$ bars via ordinary least squares, then returns the fitted endpoint value $a_0$. By capturing inflection and curvature that linear and quadratic models miss, CRMA tracks S-shaped reversals and accelerating trends with measurably lower endpoint error than LSMA or QRMA on non-stationary price series. The cost is a 4x4 linear system solve per bar, which is O(1) once power sums are accumulated in O(N).

## Historical Context

Polynomial regression as a smoothing technique dates to Legendre (1805) and Gauss (1809), who independently developed the method of least squares. The specific application of cubic (degree-3) polynomial fitting to financial time series emerged from the broader Savitzky-Golay filtering framework published in 1964, which showed that polynomial regression over a sliding window produces FIR filter coefficients with desirable frequency-domain properties.

CRMA occupies the sweet spot in the polynomial hierarchy. Degree-1 (LSMA) captures only linear trends. Degree-2 (QRMA) adds curvature but misses inflection points. Degree-3 (CRMA) captures inflection, the point where acceleration changes sign, which is precisely where trend reversals begin. Degree-4 and above risk Runge's phenomenon: oscillatory artifacts near window edges that amplify noise rather than suppress it.

The key implementation difference from textbook polynomial regression is the x-indexing convention. CRMA uses $x = 0$ for the newest bar and $x = N-1$ for the oldest. This means the fitted endpoint is simply $a_0$, the intercept, avoiding the numerical instability of evaluating $a_0 + a_1(N-1) + a_2(N-1)^2 + a_3(N-1)^3$ with large $N$.

## Architecture & Physics

### 1. Normal Equations Assembly

The polynomial fit requires solving $\mathbf{M} \cdot \mathbf{a} = \mathbf{r}$ where:

$$
M_{ij} = \sum_{k=0}^{N-1} x_k^{i+j}, \quad r_i = \sum_{k=0}^{N-1} x_k^i \cdot y_k, \quad i,j \in \{0,1,2,3\}
$$

Seven power sums ($S_0$ through $S_6$) and four cross-products ($r_0$ through $r_3$) are accumulated in a single O(N) pass over the circular buffer.

### 2. Gaussian Elimination with Partial Pivoting

The 4x4 augmented matrix is solved via Gaussian elimination with partial pivoting. Partial pivoting prevents division-by-zero and minimizes round-off amplification. The pivot search, row swap, and elimination are all O(1) operations on a fixed 4x4 system (64 element accesses, 48 multiply-adds).

### 3. Back-Substitution

After elimination produces an upper-triangular system, back-substitution extracts $a_3, a_2, a_1, a_0$ in four steps. The result $a_0$ is the fitted value at $x = 0$ (newest bar).

### 4. Singular Matrix Guard

If the pivot magnitude falls below $10^{-12}$, the system is treated as singular and the raw price is returned. This handles degenerate cases (e.g., all identical prices, $N < 4$ effective points).

## Mathematical Foundation

The cubic regression minimizes the sum of squared residuals:

$$
\min_{a_0, a_1, a_2, a_3} \sum_{k=0}^{N-1} \left( y_k - a_0 - a_1 x_k - a_2 x_k^2 - a_3 x_k^3 \right)^2
$$

Setting partial derivatives to zero yields the 4x4 normal equation system:

$$
\begin{bmatrix} S_0 & S_1 & S_2 & S_3 \\ S_1 & S_2 & S_3 & S_4 \\ S_2 & S_3 & S_4 & S_5 \\ S_3 & S_4 & S_5 & S_6 \end{bmatrix} \begin{bmatrix} a_0 \\ a_1 \\ a_2 \\ a_3 \end{bmatrix} = \begin{bmatrix} r_0 \\ r_1 \\ r_2 \\ r_3 \end{bmatrix}
$$

Where:

$$
S_m = \sum_{k=0}^{N-1} k^m, \quad r_m = \sum_{k=0}^{N-1} k^m \cdot y_k
$$

The power sums $S_m$ have closed-form expressions (Faulhaber's formulas), but accumulating them in the data loop adds negligible cost and avoids large intermediate products.

**Default parameters:** `period = 14`, `minPeriod = 4` (minimum for degree-3 fit).

**Pseudo-code (streaming):**

```
buffer ← circular_buffer(period)
buffer.push(price)
n ← min(bar_count, period)
if n < 4: return price

// Accumulate sums in O(n)
for i = 0 to n-1:
    x = i; x2 = x*x; x3 = x2*x
    S0 += 1; S1 += x; S2 += x2; S3 += x3
    S4 += x2*x2; S5 += x2*x3; S6 += x3*x3
    r0 += y[i]; r1 += x*y[i]; r2 += x2*y[i]; r3 += x3*y[i]

// Build 4×5 augmented matrix, solve via Gaussian elimination
M = [[S0,S1,S2,S3,r0], [S1,S2,S3,S4,r1], [S2,S3,S4,S5,r2], [S3,S4,S5,S6,r3]]
gaussian_eliminate_partial_pivot(M)
a = back_substitute(M)
return a[0]  // fitted value at x=0 (newest bar)
```

## Resources

- Legendre, A.-M. (1805). *Nouvelles méthodes pour la détermination des orbites des comètes*. Firmin Didot.
- Gauss, C.F. (1809). *Theoria motus corporum coelestium*. Perthes et Besser.
- Savitzky, A. & Golay, M.J.E. (1964). "Smoothing and Differentiation of Data by Simplified Least Squares Procedures." *Analytical Chemistry*, 36(8), 1627-1639.
- Press, W.H. et al. (2007). *Numerical Recipes*, 3rd ed. Cambridge University Press. Chapter 15: Modeling of Data.

## Performance Profile

### Operation Count (Streaming Mode)

CRMA(N) fits a degree-3 polynomial via least squares. The O(N) cost is in accumulating seven Faulhaber power sums plus four cross-products over the ring buffer each bar. The 4×4 Gaussian elimination is O(1) (fixed 64 operations regardless of N).

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Ring buffer push | 1 | 3 | ~3 |
| Power sum updates S0..S6 (7 sums × 2 ops) | ~2N | 1 | ~2N |
| Cross-product updates (4 × dot products) | ~4N | 2 | ~8N |
| 4×4 Gaussian elimination (fixed) | ~64 | 3 | ~192 |
| Polynomial evaluation at newest point | 4 | 3 | ~12 |
| **Total** | **~(6N + 64)** | — | **~(10N + 207) cycles** |

O(N) per bar. For default N = 14: ~347 cycles. Resync re-computes sums every 1000 ticks to prevent floating-point drift.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| Power sum accumulation (S0..S6) | Yes | Independent sums; `VADDPD` per term, 4 bars/lane |
| Cross-product dot products (ΣxᵏY) | Yes | `VFMADD231PD` across window; stride-1 pattern |
| 4×4 Gaussian elimination | No | Fixed scalar 64-op system; not worth SIMD setup |
| Polynomial evaluation | No | 4-term Horner; scalar is fastest for degree 3 |

Batch throughput for the sum and cross-product phases: AVX2 achieves ~4× scalar. Gaussian elimination and Horner evaluation remain scalar. Net batch speedup for N = 14, large series: approximately 2.5× over fully scalar.
