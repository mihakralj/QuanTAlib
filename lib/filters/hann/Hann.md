# Hann: Hann FIR Filter

> "The Hanning window whispers where the Boxcar screams. Smoothness is not just an aesthetic; it's a mathematical necessity."

Hann (Hann Filter) is a Finite Impulse Response (FIR) smoothing filter that applies a Hann window to time series data. Named after Julius von Hann, this filter uses a cosine-sum window function that tapers inputs to zero at the edges. This tapering process significantly reduces spectral leakage and provides excellent high-frequency noise attenuation compared to a Simple Moving Average (SMA).

## Historical Context

The Hann window (often incorrectly called "Hanning" due to similarity with "Hamming") is a staple in digital signal processing, particularly in spectral analysis and windowing before Fourier Transforms. In financial time series, applying a Hann window as a convolution kernel results in a weighted moving average that prioritizes central data points while gracefully diminishing the influence of older and newest data points in the window, resulting in a smooth, lag-aware signal.

## Architecture & Physics

The implementation uses a classic FIR architecture with a normalized Hann kernel.

* **Kernel Construction:** The weights are derived from the inverted cosine function.
* **Tapering:** The weights start at zero, rise to a peak at the center, and fall back to zero. This "bell-like" shape (though mathematically distinct from Gaussian) ensures smooth transitions.
* **Normalization:** Weights are dynamically normalized so that $\sum w_i = 1$. This handling is crucial for maintaining price scale parity and handling `NaN` values robustly.
* **Boundary Handling:** The filter uses a `RingBuffer` to maintain the sliding window. During the startup phase (warmup), the active partial window is processed, and weights are renormalized to ensure valid output from the very first bar.

### Smoothness vs. Lag

* **Smoothness:** Excellent. The cosine taper eliminates the discontinuities found in rectangular windows (SMA), making the derivative of the output much cleaner.
* **Lag:** As a symmetrical FIR filter, the group delay is constant and equals $(N-1)/2$. While this introduces lag, the phase response is linear.

## Mathematical Foundation

### 1. Weight Calculation

For a window of length $N$, the weight $w$ at index $i$ (where $0 \le i \le N-1$) is:

$$ w_i = 0.5 \cdot \left(1 - \cos\left(\frac{2\pi i}{N-1}\right)\right) $$

### 2. Normalization

To ensure unity gain:

$$ W_i = \frac{w_i}{\sum_{k=0}^{N-1} w_k} $$

### 3. Convolution

The filtered value $y_t$ is the convolution of the input time series $x$ and the normalized weights $W$:

$$ y_t = \sum_{i=0}^{N-1} x_{t-i} \cdot W_i $$

> **Note:** $w_0$ and $w_{N-1}$ are mathematically zero. This means the effective window width is slightly narrower than $N$ in terms of data usage, but $N$ is preserved for phase characteristics.

## Performance Profile

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | 15 ns/bar | SIMD-optimized static calculation; RingBuffer-optimized streaming. |
| **Allocations** | 0 | Core update loop is allocation-free. |
| **Complexity** | O(N) | Linear with respect to window length. |
| **Accuracy** | 10/10 | Exact FIR implementation. |
| **Timeliness** | 7/10 | Similar lag to other centered windows, but superior noise rejection. |
| **Overshoot** | 0 | Non-negative weights ensure no overshoot (monotonous step response). |
| **Smoothness** | 9/10 | Highly smooth output due to cosine tapering. |

## Validation

The implementation is validated against a reference logic matching Pine Script's behavior.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **Reference** | ✅ | Matches expected convolution values with $10^{-9}$ precision. |
| **Pine Script** | ✅ | Logic aligned with TradingView's Hann implementation. |

## C# Usage

```csharp
// Initialize with length=20
var hann = new Hann(length: 20);

// Update with a new value
var result = hann.Update(new TValue(DateTime.UtcNow, 100.0));

// Static calculation on a span
Hann.Calculate(inputSpan, outputSpan, length: 20);

// Use with a publisher
var hannLive = new Hann(source, length: 20);
