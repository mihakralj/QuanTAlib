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

## Mathematical Foundation

The weight $W_i$ for the $i$-th element in the window is calculated as:

$$ m = \text{offset} \times (\text{period} - 1) $$

$$ s = \frac{\text{period}}{\text{sigma}} $$

$$ W_i = \exp \left( - \frac{(i - m)^2}{2s^2} \right) $$

The ALMA value is the weighted sum of the prices divided by the sum of the weights:

$$ \text{ALMA} = \frac{\sum_{i=0}^{N-1} P_{t-i} \cdot W_{N-1-i}}{\sum_{i=0}^{N-1} W_i} $$

## Performance Profile

ALMA is computationally heavier than an SMA due to the exponential weights, but since these are precomputed, the runtime cost is strictly $O(1)$ per update.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | ★★★★☆ | Gaussian calculation per bar (precomputed weights). |
| **Allocations** | ★★★★★ | 0 bytes; hot path is allocation-free. |
| **Complexity** | ★★★☆☆ | O(N) window iteration required. |
| **Precision** | ★★★★★ | `double` precision preserves Gaussian structure. |

### Zero-Allocation Design

ALMA precomputes the Gaussian weights in the constructor. The `Update` method performs a simple dot product of the price window and the weight vector, requiring no heap allocations.

## Validation

Validation is performed against Skender and Ooples implementations.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Validated. |
| **Skender** | ✅ | Matches `GetAlma`. |
| **Ooples** | ✅ | Matches `CalculateArnaudLegouxMovingAverage`. |
| **TA-Lib** | ❌ | Not implemented. |
| **Tulip** | ❌ | Not implemented. |

### Common Pitfalls

1. **Offset Confusion**: An offset of 1.0 makes it extremely responsive but noisy (essentially the current price). An offset of 0.5 makes it a centered moving average (great for smoothing, terrible for trading due to repainting if used as such, but ALMA doesn't repaint). The sweet spot is 0.85.
2. **Sigma Sensitivity**: A low sigma (e.g., 1.0) makes the filter look like a rectangular window (SMA). A high sigma makes it look like a spike. Keep it around 6.0.
