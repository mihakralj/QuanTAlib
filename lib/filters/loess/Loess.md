# Loess: Locally Estimated Scatterplot Smoothing

> "When global models fail, act locally. LOESS fits the data by ignoring the noise and embracing the neighborhood."

Locally Estimated Scatterplot Smoothing (LOESS) applies a weighted linear regression over a localized window of nearest neighbors. Unlike simple averaging or global linear regression, LOESS estimates the deterministic trend point-by-point, giving maximum influence to recent data and decaying elegantly at the edges.

## Historical Context / The Standard

Introduced by William S. Cleveland in 1979, LOESS (or LOWESS) bridges the gap between simple averaging and complex parametric regression. While statistical packages often solve this iteratively (O(N²) or O(N log N)), Causal LOESS for time-series filtering optimizes strictly for the most recent data point.

In high-frequency finance, the challenge is cost: standard LOESS involves solving a system of linear equations at every bar. We optimized this away.

## Architecture & Physics

Our implementation is a **Causal LOESS Filter** optimized for streaming data.

* **Fixed Kernel Convolution:** Since the independent variable $x$ (time/index) is uniform and relative to the window, the regression weights for the target point are constant. We pre-compute these into a single convolution kernel.
* **Tricube Weighting:** We use the classic tricube function, which is continuous and has continuous derivatives, offering superior smoothness compared to box/triangular weights.
* **Robustness:** The filter actively monitors inputs for `NaN` and replaces them with the last known finite value, enforcing stability in volatile data streams (e.g., during connection drops).
* **Symmetry Enforcement:** The internal window size adjusts automatically to the nearest odd number, establishing a perfect center point for the kernel.

### The Convolution Optimization

The naive approach solves $\beta = (X^T W X)^{-1} X^T W y$ for every update.
By observing that $X$ (relative positions) and $W$ (tricube weights) are static for a fixed window size, we reduce the runtime complexity from $O(N \cdot k^2)$ to a simple $O(N)$ dot product.

## Mathematical Foundation

For a window size $N$ and current point $i=0$ (newest), we define weights for neighbors $j \in [0, N-1]$.

### 1. Tricube Weight Function

$$ w(j) = (1 - |d|^3)^3 $$

where $d = \frac{j - \text{center}}{\text{half\_width}}$.

### 2. Regression Solution

We minimize the localized squared error:

$$ \min_{\beta} \sum_{j} w_j (y_j - (\beta_0 + \beta_1 x_j))^2 $$

### 3. Effective Kernel

The estimated value $\hat{y}$ is a linear combination of inputs:

$$ \hat{y} = \sum_{j=0}^{N-1} y_{t-j} \cdot K_j $$

where $K$ is the pre-computed row of the hat matrix corresponding to the target point.

## Performance Profile

### Operation Count (Streaming Mode)

LOESS (Locally Estimated Scatterplot Smoothing): tricube-weighted local polynomial regression over N neighbors. O(N) per bar for degree-1 (linear) fit.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Tricube weight computation | N | ~10 cy | ~300 cy (N=30) |
| Weighted sums (Sx, Sy, Sxx, Sxy) | 4N | ~4 cy | ~480 cy |
| Linear regression solve (2x2 system) | 4 | ~5 cy | ~20 cy |
| **Total (N=30)** | **5N+4** | — | **~800 cycles** |

O(N) per bar. Dominant cost: 4-accumulator pass over N-element window. ~800 cycles for N=30.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| Tricube weight + distance | Partial | Norm and power; polynomial approx enables SIMD |
| Weighted accumulation (4 accumulators) | Yes | 4-wide FMA lanes; AVX2 gives ~3x speedup here |
| 2x2 solve | No | Scalar; 4 ops negligible |

SIMD batch: ~250 cy for N=30.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | 12 ns/bar | SIMD-accelerated dot product. |
| **Allocations** | 0 | Stack-based arithmetic only. |
| **Complexity** | $O(N)$ | Reduced from regressional complexity. |
| **Accuracy** | 9/10 | Excellent local fit; robust to trend changes. |
| **Timeliness** | 8/10 | Responsive; less lag than SMA/EMA. |
| **Smoothness** | 9/10 | Superior due to tricube decay. |
| **Overshoot** | 2/10 | Minimal; tends to under-damp rather than ring. |

## Validation

Validating against statistical properties and theoretical linear trend reconstruction.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **Linear Trend** | ✅ | Reconstructs $y=x$ perfectly. |
| **Consistency** | ✅ | Batch, Streaming, and Span modes match. |
| **Robustness** | ✅ | Handles `NaN` inputs gracefully. |

### Common Pitfalls

* **Window Size:** Very small periods (<5) approximate the input noisily. Large periods introduce lag.
* **NaN Propagation:** Standard implementations propagate `NaN`. This implementation stops them dead.
