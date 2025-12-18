# CONV: Convolution Indicator

## What It Does

The Convolution Indicator (CONV) is a generalized filtering tool that applies a custom set of weights (a "kernel") to a window of historical data. Unlike standard moving averages that use fixed formulas (equal weights for SMA, linear for WMA), CONV allows you to define *any* weighting scheme you can imagine. It is the fundamental building block for creating custom digital signal processing filters, edge detectors, or specialized smoothing algorithms.

## Historical Context

Convolution is a mathematical operation fundamental to signal processing, image processing, and physics. In finance, it gained traction with the rise of quantitative trading, where analysts needed more flexibility than standard indicators provided. By treating price data as a signal and applying convolution kernels, traders can design filters that isolate specific frequencies, detect patterns, or perform advanced smoothing that adapts to specific market characteristics.

## How It Works

### The Core Idea

Imagine a sliding window over your price data. You have a list of "weights" (the kernel) of the same length as the window. To get the result for the current bar, you multiply each price in the window by its corresponding weight and sum them up.

- If your kernel is `[0.2, 0.2, 0.2, 0.2, 0.2]`, you've recreated a 5-period SMA.
- If your kernel is `[0.1, 0.2, 0.3, 0.4]`, you've recreated a 4-period WMA (unnormalized).
- If your kernel is `[-1, 1]`, you've created a momentum indicator (Price - Previous Price).

### Mathematical Foundation

For a kernel $K$ of length $n$ and a price series $P$:

$$CONV_t = \sum_{i=0}^{n-1} (P_{t-i} \cdot K_{n-1-i})$$

In our implementation, the kernel is applied such that the last element of the kernel ($K_{n-1}$) multiplies the most recent price ($P_t$), and the first element ($K_0$) multiplies the oldest price in the window ($P_{t-n+1}$).

### Implementation Details

The `Conv` indicator uses a **RingBuffer** to store the price history efficiently. The calculation is a dot product between the kernel and the buffered data.

- **Update Complexity:** O(K), where K is the kernel length.
- **Memory:** O(K) to store the buffer and the kernel.
- **Optimization:** We use `Span<T>` and SIMD-optimized dot product operations where available to ensure high performance even with large kernels.

## Configuration

| Parameter | Default | Purpose | Adjustment Guidelines |
|-----------|---------|---------|----------------------|
| Kernel | (Required) | Array of weights | Defines the filter behavior. Must not be empty. |

**Note:** The kernel is not automatically normalized. If you want a moving average that tracks price levels, the sum of your kernel weights should equal 1.0. If the sum is 0 (e.g., `[-1, 1]`), it will act as an oscillator.

## C# Usage

### Streaming Updates (Single Instance)

```csharp
using QuanTAlib;

// Create a custom kernel (e.g., a 3-period weighted average)
double[] weights = { 0.1, 0.3, 0.6 }; 
var conv = new Conv(weights);

// Process each new bar
TValue result = conv.Update(new TValue(timestamp, closePrice));
Console.WriteLine($"Conv: {result.Value:F2}");
```

### Batch Processing (Historical Data)

```csharp
// TSeries API
TSeries prices = ...;
double[] kernel = { 0.2, 0.2, 0.2, 0.2, 0.2 }; // 5-period SMA
TSeries sma5 = Conv.Batch(prices, kernel);

// Span API (High Performance)
double[] prices = new double[1000];
double[] output = new double[1000];
double[] edgeDetector = { -1, 1 }; // Simple difference
Conv.Batch(prices.AsSpan(), output.AsSpan(), edgeDetector);
```

### Bar Correction (isNew Parameter)

```csharp
var conv = new Conv(new[] { 0.5, 0.5 });

// New bar
conv.Update(new TValue(time, 100), isNew: true);

// Intra-bar update
conv.Update(new TValue(time, 101), isNew: false); // Replaces 100 with 101
```

## Performance Profile

| Operation | Complexity | Description |
|-----------|------------|-------------------|
| Streaming update | O(K) | Linear scan (dot product) of the kernel |
| Bar correction | O(K) | Re-calculates dot product |
| Batch processing | O(N * K) | Sliding window dot product |
| Memory footprint | O(K) | RingBuffer + Kernel array |

## Interpretation

### Trading Signals

Signals depend entirely on the kernel you design:

- **Smoothing:** Use positive weights that sum to 1. (e.g., Gaussian, Triangle).
- **Differentiation:** Use weights that sum to 0 to detect rate of change. (e.g., `[-1, 1]` for velocity, `[1, -2, 1]` for acceleration).
- **Edge Detection:** Use kernels like `[-1, 0, 1]` (Sobel-like) to detect sharp price movements.

### When It Works Best

- **Custom Research:** When standard indicators don't fit your specific hypothesis.
- **Signal Processing:** When applying filters from other domains (audio, image) to financial time series.

## Architecture Notes

This implementation makes specific trade-offs:

### Choice: No Automatic Normalization

- **Alternative:** Automatically divide weights by their sum.
- **Trade-off:** User must normalize manually if desired.
- **Rationale:** Allows for oscillators (sum=0) and amplifiers (sum > 1), providing maximum flexibility.

### Choice: RingBuffer Implementation

- **Alternative:** Array copy.
- **Trade-off:** Slightly complex indexing logic.
- **Rationale:** Zero allocation during updates is critical for high-frequency trading applications.

## References

- Smith, Steven W. "The Scientist and Engineer's Guide to Digital Signal Processing." California Technical Publishing, 1997.
- Ehlers, John F. "Cycle Analytics for Traders." Wiley, 2013.
