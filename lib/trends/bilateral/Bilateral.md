# Bilateral Filter

> "Smoothing without blurring edges? It's not magic, it's just math."

The Bilateral Filter is a non-linear, edge-preserving, and noise-reducing smoothing filter. Unlike standard Gaussian filters that blur everything indiscriminately, the Bilateral Filter respects strong edges by weighting pixels based on both their spatial distance and their intensity difference (range).

## Historical Context

Originally developed for image processing by Tomasi and Manduchi (1998), the Bilateral Filter revolutionized denoising by solving the "blurring edges" problem inherent in linear filters. In financial time series, it serves a similar purpose: smoothing out noise (small fluctuations) while preserving significant price changes (edges/trends).

## Architecture & Physics

The filter operates in two domains simultaneously:

1. **Spatial Domain**: Weights decrease as distance from the current bar increases (like a Gaussian filter).
2. **Range Domain**: Weights decrease as the price difference from the current price increases.

This dual-weighting mechanism ensures that:

- Nearby prices with similar values have high influence (smoothing).
- Distant prices or prices with very different values have low influence (edge preservation).

### Complexity

The algorithm is $O(N)$ per update, where $N$ is the period length. While slower than $O(1)$ recursive filters (like EMA), it offers superior signal fidelity.

## Mathematical Foundation

The Bilateral Filter value at index $0$ (current) is calculated as:

$$ BF = \frac{\sum_{i=0}^{L-1} W_s(i) \cdot W_r(i) \cdot P_i}{\sum_{i=0}^{L-1} W_s(i) \cdot W_r(i)} $$

Where:

- $L$ is the length (period).
- $P_i$ is the price at index $i$ (0 is current).
- $W_s(i)$ is the spatial weight:
    $$ W_s(i) = \exp\left(-\frac{i^2}{2\sigma_s^2}\right) $$

- $W_r(i)$ is the range weight:
    $$ W_r(i) = \exp\left(-\frac{(P_0 - P_i)^2}{2\sigma_r^2}\right) $$

Parameters:

- $\sigma_s = \max(L \cdot \text{ratio}, 10^{-10})$
- $\sigma_r = \max(\text{StDev}(P, L) \cdot \text{mult}, 10^{-10})$

## Performance Profile

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | ~50ns/bar | O(N) complexity. |
| **Allocations** | 0 | Zero-allocation hot path. |
| **Complexity** | O(N) | N = Period. Requires full window iteration per update. |
| **Accuracy** | 10/10 | Matches reference implementation. |
| **Timeliness** | 8/10 | Low lag due to edge preservation. |
| **Smoothness** | 9/10 | Excellent noise reduction. |

### Zero-Allocation Design

The implementation uses a `RingBuffer` with pinned memory and `stackalloc` (conceptually, though implemented via direct span access) to ensure zero heap allocations during the `Update` cycle. Spatial weights are pre-calculated.

## Validation

Validated against a reference implementation mirroring the PineScript logic.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **PineScript** | ✅ | Logic matches exactly. |
| **Reference** | ✅ | Validated against C# reference. |

## Usage

```csharp
using QuanTAlib;

// Create a Bilateral filter with period 14
var bilateral = new Bilateral(14, sigmaSRatio: 0.5, sigmaRMult: 1.0);

// Update with new price
var result = bilateral.Update(new TValue(DateTime.UtcNow, 100.0));

Console.WriteLine($"Bilateral: {result.Value}");
