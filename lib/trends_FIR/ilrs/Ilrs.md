# ILRS: Integral of Linear Regression Slope

> "John Ehlers took the slope of a regression line, integrated it, and got a smoother trend follower. Differentiate to find direction, integrate to find position. Calculus: still useful after 300 years."

ILRS computes the linear regression slope over a rolling window, then accumulates it via discrete integration (running sum) to reconstruct a smoothed price-level signal. By differentiating (slope extraction) and reintegrating, ILRS acts as a low-pass filter that preserves trend direction while suppressing high-frequency noise more aggressively than LSMA. The integration step introduces a natural momentum quality: the output continues rising even as slope magnitude diminishes, making ILRS particularly effective for trend-following systems that need early exit signals based on slope deceleration.

## Historical Context

John Ehlers introduced ILRS in *Rocket Science for Traders* (Wiley, 2001) as part of his signal-processing approach to technical analysis. Ehlers recognized that most moving averages are essentially low-pass filters applied directly to price, but the differentiate-then-integrate approach offers a different noise profile. The linear regression slope acts as a first-derivative estimator, and the running sum reconstructs the original signal minus the high-frequency components that the regression window cannot track.

The concept has deep roots in control theory and signal processing. The "differentiate and integrate" technique is standard in PID controllers and phase-locked loops, where it provides better noise rejection than direct filtering when the signal's derivative is smoother than the signal itself. In financial time series, this condition holds when price changes are more persistent than price levels, a reasonable assumption during trending regimes.

ILRS differs from LSMA (which evaluates the regression line at the endpoint) in a crucial way: LSMA's output is bounded by the regression window, while ILRS accumulates indefinitely. This makes ILRS a non-stationary filter whose output drifts with the integrated slope, requiring periodic resynchronization to avoid floating-point drift over very long series.

## Architecture & Physics

### 1. Rolling Linear Regression Slope

The slope is computed via the standard least-squares formula over the circular buffer:

$$
\text{slope} = \frac{N \sum x_i y_i - \sum x_i \sum y_i}{N \sum x_i^2 - \left(\sum x_i\right)^2}
$$

The x-index sums ($\sum x$, $\sum x^2$) are computed analytically (Faulhaber's formulas), reducing the per-bar cost to a single O(N) pass for the y-dependent sums.

### 2. Discrete Integration

The integral is a simple running sum:

$$
\text{ILRS}_t = \text{ILRS}_{t-1} + \text{slope}_t
$$

This is O(1) per bar after the slope is computed.

### 3. Initialization

The integral is initialized to the first price value, ensuring the output starts at a reasonable level rather than zero.

### 4. Drift Management

Because ILRS accumulates slope indefinitely, floating-point precision degrades over millions of bars. A periodic resynchronization (e.g., every 1000 bars, re-anchor to slope-implied price) prevents meaningful drift.

## Mathematical Foundation

Given a window of $N$ prices $y_0, y_1, \ldots, y_{N-1}$ (oldest to newest), the regression slope is:

$$
b = \frac{N \sum_{i=0}^{N-1} i \cdot y_i - \left(\sum_{i=0}^{N-1} i\right)\left(\sum_{i=0}^{N-1} y_i\right)}{N \sum_{i=0}^{N-1} i^2 - \left(\sum_{i=0}^{N-1} i\right)^2}
$$

With analytical x-sums:

$$
\sum i = \frac{N(N-1)}{2}, \quad \sum i^2 = \frac{N(N-1)(2N-1)}{6}
$$

The ILRS output:

$$
\text{ILRS}_t = \text{ILRS}_{t-1} + b_t, \quad \text{ILRS}_0 = y_0
$$

**Default parameters:** `period = 14`, `minPeriod = 2`.

**Pseudo-code (streaming):**

```
buffer ← circular_buffer(period)
buffer.push(price)
n ← min(bar_count, period)

if n < 2:
    integral ← price
    return integral

// Analytical x-sums
sumX  = n*(n-1)/2
sumX2 = n*(n-1)*(2n-1)/6

// Data-dependent y-sums (O(n) pass)
sumY = 0; sumXY = 0
for i = 0 to n-1:
    sumY  += buffer[i]
    sumXY += i * buffer[i]

denomX = n * sumX2 - sumX * sumX
slope = (n * sumXY - sumX * sumY) / denomX

integral += slope
return integral
```

## Resources

- Ehlers, J.F. (2001). *Rocket Science for Traders: Digital Signal Processing Applications*. John Wiley & Sons.
- Ehlers, J.F. (2004). *Cybernetic Analysis for Stocks and Futures*. John Wiley & Sons.
- Kendall, M.G. & Stuart, A. (1979). *The Advanced Theory of Statistics*, Vol. 2. Griffin. Chapter 29: Regression.

## Performance Profile

### Operation Count (Streaming Mode)

ILRS(N) uses an incremental linear regression that maintains `SumY` and `SumXY` as O(1) running sums (subtract evicted, add new). The slope is derived in O(1) from these sums using the precomputed `sumX` and `denominator`. The integral accumulation is a single addition.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Ring buffer push | 1 | 3 | ~3 |
| SumY update (add new, subtract evicted) | 2 | 1 | ~2 |
| SumXY update (add new × x, subtract evicted × x_old) | 2 | 3 | ~6 |
| Slope: (N×SumXY − SumX×SumY) / denominator | 3 | 8 | ~24 |
| Integral accumulation: ILRS += slope | 1 | 1 | ~1 |
| **Total** | **9** | — | **~36 cycles** |

O(1) per bar after warmup (the incremental sum pattern removes the N-scan). For N = 14 default: ~36 cycles. Resync every 1000 bars prevents drift. WarmupPeriod = N.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| Running sum updates (SumY, SumXY) | Partial | Prefix-sum pattern enables vectorization with log₂N overhead |
| Slope formula | Yes | `VFNMADD`, `VDIVPD` once prefix sums are built |
| Integral (prefix sum of slopes) | Partial | Sequential scan; parallel prefix available but overhead > benefit for N < 1000 |

Batch mode can precompute prefix sums vectorially then compute all slopes in parallel. The integral sum remains a sequential dependency. Net speedup for large series: ~2× over scalar.
