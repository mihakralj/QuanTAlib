# ALMA: Arnaud Legoux Moving Average

> "If you want to smooth data without looking like you're driving using the rear-view mirror, you use a Gaussian filter. ALMA is that filter, dressed up for Wall Street."

ALMA (Arnaud Legoux Moving Average) is a superior alternative to the standard SMA or EMA. It uses a Gaussian distribution to determine the weights of the moving average, allowing you to shift the "center of gravity" of the window. This gives you control over the trade-off between smoothness and responsiveness that other averages can only dream of.

## Historical Context

Developed by Arnaud Legoux and Dimitris Kouzis-Loukas in 2009, ALMA was a response to the inherent lag in traditional moving averages. While Hull (HMA) and Jurik (JMA) tried to solve lag through complex algorithms, Legoux went back to signal processing basics: the Gaussian filter. It's elegant, mathematically sound, and doesn't rely on "magic numbers."

## Architecture & Physics

ALMA is essentially a Finite Impulse Response (FIR) filter with Gaussian coefficients. Unlike an SMA (rectangular window) or WMA (triangular window), ALMA uses a bell curve.

The "physics" of ALMA are defined by three parameters:

1. **Period**: The window size.
2. **Offset**: Determines where the peak of the Gaussian curve sits. An offset of 0.85 (default) pushes the weight towards the most recent data, reducing lag significantly while maintaining smoothness.
3. **Sigma**: The standard deviation of the bell curve. A higher sigma (e.g., 6.0) makes the curve sharper, focusing weights tightly around the offset.

### Zero-Allocation Design

Our implementation is a study in memory discipline.

- **Precomputed Weights**: The Gaussian weights are calculated once in the constructor.
- **RingBuffer**: We use a circular buffer to store the price window, avoiding array shifts.
- **SIMD Optimization**: The weighted sum calculation uses `Vector<double>` dot products where possible, or optimized loop unrolling.
- **Stack Allocation**: For the static `Calculate` method, we use `stackalloc` for small periods to avoid heap pressure entirely.

## Mathematical Foundation

The weight $W_i$ for the $i$-th element in the window is calculated as:

$$ m = \text{offset} \times (\text{period} - 1) $$

$$ s = \frac{\text{period}}{\text{sigma}} $$

$$ W_i = \exp \left( - \frac{(i - m)^2}{2s^2} \right) $$

The ALMA value is the weighted sum of the prices divided by the sum of the weights:

$$ \text{ALMA} = \frac{\sum_{i=0}^{N-1} P_{t-i} \cdot W_{N-1-i}}{\sum_{i=0}^{N-1} W_i} $$

## Performance Profile

ALMA is computationally heavier than an SMA due to the exponential weights, but since these are precomputed, the runtime cost is strictly $O(1)$ per update.

| Metric | Complexity | Notes |
| :--- | :--- | :--- |
| **Throughput** | Moderate | Gaussian calculation per bar |
| **Complexity** | O(N) | Window iteration required |
| **Accuracy** | 9/10 | Gaussian weights preserve structure well |
| **Timeliness** | 8/10 | Tunable offset allows for very low lag |
| **Overshoot** | 9/10 | Minimal overshoot if tuned right |
| **Smoothness** | 9/10 | Very smooth due to Gaussian curve |

## Validation

Validated against Python's `pandas-ta` and custom reference implementations.

| Provider | Error Tolerance | Notes |
| :--- | :--- | :--- |
| **Pandas-TA** | $10^{-9}$ | Exact match on Gaussian weights |
| **Manual Calc** | $10^{-12}$ | Verified against Excel implementation |

### Common Pitfalls

1. **Offset Confusion**: An offset of 1.0 makes it extremely responsive but noisy (essentially the current price). An offset of 0.5 makes it a centered moving average (great for smoothing, terrible for trading due to repainting if used as such, but ALMA doesn't repaint). The sweet spot is 0.85.
2. **Sigma Sensitivity**: A low sigma (e.g., 1.0) makes the filter look like a rectangular window (SMA). A high sigma makes it look like a spike. Keep it around 6.0.
