# POLYFIT: Polynomial Fitting

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Statistic                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`, `degree` (default 2)                      |
| **Outputs**      | Single series (Polyfit)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period` bars                          |

### TL;DR

- Polynomial Fitting computes a rolling polynomial regression of configurable degree over a lookback window, returning the fitted value at the curren...
- Parameterized by `period`, `degree` (default 2).
- Output range: Varies (see docs).
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

Polynomial Fitting computes a rolling polynomial regression of configurable degree over a lookback window, returning the fitted value at the current bar. Degree 1 produces a linear regression endpoint (identical to LSQR), degree 2 produces a quadratic fit that captures curvature, and degree 3 produces a cubic fit that captures inflection points. The implementation solves the normal equations $\mathbf{X}^T\mathbf{X}\mathbf{a} = \mathbf{X}^T\mathbf{y}$ via Gauss-Jordan elimination with partial pivoting, evaluating the resulting polynomial at $x = 1$ (the current bar position). With $O(Nd + d^3)$ complexity per bar where $N$ is the period and $d$ is the degree, POLYFIT provides a general-purpose curve-fitting tool that subsumes linear regression and extends it to arbitrary polynomial order.

## Historical Context

Polynomial regression traces to Adrien-Marie Legendre (1805) and Carl Friedrich Gauss (1809), who independently developed the method of least squares. The normal equations formulation provides the minimum-sum-of-squares solution in closed form, though numerical stability requires careful implementation. Gauss-Jordan elimination with partial pivoting (Jordan, 1873) is the standard approach for small systems like those arising in polynomial fitting with degrees 1-6.

In technical analysis, linear regression (degree 1) is well established via the Linear Regression Channel and LSQR indicators. Higher-degree fits are less common due to overfitting concerns, but degree 2 (quadratic) is useful for detecting acceleration/deceleration in trends, and degree 3 (cubic) can capture reversal patterns. The key insight is that higher degrees track price more closely but also amplify noise; the optimal degree depends on the signal-to-noise ratio and the lookback period.

The x-normalization step (mapping time indices to $[0, 1]$) is critical for numerical stability: without it, the Vandermonde matrix entries $x^d$ would span many orders of magnitude for typical lookback periods, causing catastrophic cancellation in the normal equations. With normalization, the matrix condition number remains manageable up to degree 6.

## Architecture and Physics

The implementation uses a circular buffer to maintain the last `period` values, with NaN substitution via last-valid-value tracking.

**Matrix assembly**: Constructs the $(d+1) \times (d+1)$ Gram matrix $\mathbf{G} = \mathbf{X}^T\mathbf{X}$ and right-hand side $\mathbf{r} = \mathbf{X}^T\mathbf{y}$ in a single pass over the data. The Vandermonde basis vectors are $[1, x, x^2, \ldots, x^d]$ where $x_i = i/(n-1)$ is the normalized time position. The matrix is symmetric so only the upper triangle needs explicit computation (mirrored to lower).

**Solver**: Gauss-Jordan elimination with partial pivoting transforms the augmented matrix $[\mathbf{G} | \mathbf{r}]$ into $[\mathbf{I} | \mathbf{a}]$. Partial pivoting selects the row with the largest absolute value in the current column to minimize round-off error. Singular or near-singular matrices (pivot $< 10^{-30}$) abort gracefully.

**Evaluation**: The polynomial $P(x) = a_0 + a_1 x + a_2 x^2 + \cdots + a_d x^d$ is evaluated at $x = 1.0$ (current bar, since time is normalized to $[0, 1]$). This gives the fitted value at the most recent observation.

**Degree clamping**: If `degree` exceeds `period - 1`, it is automatically reduced to prevent underdetermined systems.

## Mathematical Foundation

The polynomial model:

$$P(x) = \sum_{j=0}^{d} a_j x^j = a_0 + a_1 x + a_2 x^2 + \cdots + a_d x^d$$

The **normal equations** for least-squares fitting:

$$\mathbf{X}^T\mathbf{X}\,\mathbf{a} = \mathbf{X}^T\mathbf{y}$$

where $\mathbf{X}$ is the $n \times (d+1)$ Vandermonde matrix:

$$X_{ij} = x_i^j, \quad x_i = \frac{i}{n-1} \in [0, 1]$$

The Gram matrix elements:

$$G_{jk} = \sum_{i=0}^{n-1} x_i^{j+k}$$

The right-hand side:

$$r_j = \sum_{i=0}^{n-1} x_i^j \cdot y_i$$

**Gauss-Jordan with partial pivoting** reduces $[\mathbf{G} | \mathbf{r}]$ to $[\mathbf{I} | \mathbf{a}]$:

1. For each column $c$: find the row $p$ in $[c, d]$ with maximum $|G_{pc}|$
2. Swap rows $c$ and $p$
3. Scale row $c$ so the pivot becomes 1
4. Subtract multiples of row $c$ from all other rows

**Output**: $\hat{y}_{\text{current}} = P(1.0) = \sum_{j=0}^{d} a_j$

**Parameter constraints**: `period` $\ge 2$, `degree` $\ge 1$ (clamped to `period - 1`). Computational complexity: $O(nd + d^3)$.

```
POLYFIT(source, period, degree):
    d = min(degree, period - 1)
    m = d + 1
    normalize x_i = i / (n-1) for i in [0, n-1]

    // Build normal equations
    G = (m x m) matrix of zeros
    r = m-vector of zeros
    for each (x_i, y_i) in window:
        for j = 0 to d:
            r[j] += x_i^j * y_i
            for k = j to d:
                G[j][k] += x_i^(j+k)
                G[k][j] = G[j][k]    // symmetric

    // Gauss-Jordan with partial pivoting
    for col = 0 to d:
        pivot_row = argmax |G[row][col]| for row in [col, d]
        swap rows col and pivot_row in G and r
        scale row col by 1/G[col][col]
        eliminate col from all other rows

    // Evaluate at x = 1.0 (current bar)
    return sum(r[j] for j = 0 to d)
```


## Performance Profile

### Operation Count (Streaming Mode)

Polyfit uses a running-sum approach via Vandermonde normal equations for degree-1 (linear) regression, updated O(1) per bar with a sliding window ring buffer.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Ring buffer add/evict | 1 | 3 cy | ~3 cy |
| Update running sums (Sx, Sy, Sxx, Sxy) | 4 | 2 cy | ~8 cy |
| Solve 2x2 normal system | 1 | 6 cy | ~6 cy |
| FMA for slope/intercept | 2 | 1 cy | ~2 cy |
| NaN guard + state update | 1 | 2 cy | ~2 cy |
| **Total** | **O(1)** | — | **~21 cy** |

O(1) per update for linear (degree-1) fit using online normal equations. Higher-degree fits require O(degree^2) matrix solve per bar.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| Running sum accumulation | Yes | Vector<double> reduces 4 sums in parallel |
| Normal equation solve | Partial | 2×2 system trivially unrolled |
| Output projection | Yes | FMA for y = a*x + b across output span |

Batch span path can vectorize sum accumulation and output projection. Inner loop SIMD-friendly for degree-1 case.

## Resources

- Legendre, A.M. "Nouvelles methodes pour la determination des orbites des cometes." 1805.
- Gauss, C.F. "Theoria Motus Corporum Coelestium." 1809.
- Golub, G. & Van Loan, C. "Matrix Computations." 4th edition, Johns Hopkins University Press, 2013.
- Press, W.H. et al. "Numerical Recipes: The Art of Scientific Computing." 3rd edition, Cambridge University Press, 2007. Chapter 15 (Modeling of Data).
- Draper, N. & Smith, H. "Applied Regression Analysis." 3rd edition, Wiley, 1998.
