# ALMA: Arnaud Legoux Moving Average

## What It Does

The Arnaud Legoux Moving Average (ALMA) is designed to solve the classic trade-off between smoothness and responsiveness in moving averages. By using a Gaussian distribution (bell curve) to determine weights, ALMA allows you to shift the peak influence of the window to any point in time—typically towards the most recent data. The result is a filter that is remarkably smooth yet reacts quickly to price changes, minimizing the lag often found in SMAs or EMAs.

## Historical Context

Developed by Arnaud Legoux and Dimitris Kouzis-Loukas in 2009, ALMA was created to improve upon traditional moving averages by applying digital signal processing principles to financial data. The authors sought to create a filter that could reduce noise (smoothness) without introducing significant delay (lag), a common problem in technical analysis.

## How It Works

### The Core Idea

Think of a standard moving average as a window of prices. An SMA gives them all equal weight. A WMA gives them linear weight. ALMA applies a "bell curve" of weights across the window.

You can control two main things:

1. **Offset:** Where the peak of the bell curve sits. An offset of 0.85 means the peak weight is applied to the recent 85% mark of the window (very responsive). An offset of 0.5 puts the peak in the middle (like a centered moving average).
2. **Sigma:** How wide or narrow the bell curve is. A higher sigma makes the curve sharper (more focused weights), while a lower sigma makes it flatter (more like an SMA).

### Mathematical Foundation

The weight $W_i$ for the $i$-th price in the window is calculated using the Gaussian function:

$$W_i = \exp\left( - \frac{(i - \text{offset})^2}{2\sigma^2} \right)$$

Where:

- $i$ is the index in the window (0 to period-1)
- $\text{offset} = \lfloor \text{period} \times \text{offset\_param} \rfloor$
- $\sigma = \text{period} / \text{sigma\_param}$

The final ALMA value is the weighted sum of prices divided by the sum of weights:

$$ALMA = \frac{\sum (P_i \cdot W_i)}{\sum W_i}$$

### Implementation Details

Our implementation precomputes the Gaussian weights during initialization since they depend only on the parameters, not the price data. This avoids expensive `Math.Exp` calls during the update loop.

For the calculation, we use a **RingBuffer** to store the price window. The weighted sum is computed using optimized dot product operations. When the buffer is full, we split the operation into two parts (head-to-end and start-to-head) to handle the circular nature of the buffer efficiently without copying data.

## Configuration

| Parameter | Default | Purpose | Adjustment Guidelines |
|-----------|---------|---------|----------------------|
| Period | 9 | Lookback window | Typical values: 9-20 for short term, 50+ for long term. |
| Offset | 0.85 | Peak position (0-1) | 0.85 = Responsive (standard); 0.50 = Smoother, more lag; 0.99 = Extremely reactive |
| Sigma | 6.0 | Curve width | 6.0 = Standard focus; Lower = Flatter (closer to SMA); Higher = Sharper focus |

**Configuration note:** The default combination (Period 9, Offset 0.85, Sigma 6) is widely used as a responsive trend filter.

## Performance Profile

| Operation | Complexity | Description |
|-----------|------------|-------------------|
| Streaming update | O(period) | Vectorized dot product (SIMD optimized) |
| Bar correction | O(period) | Re-calculates weighted sum |
| Batch processing | O(n * period) | Vectorized loop with precomputed weights |
| Memory footprint | O(period) | RingBuffer + Weight array |

**Note:** While ALMA is O(period) per update (unlike WMA's O(1)), the use of AVX/SSE intrinsics via `SimdExtensions` makes it extremely fast.

## Interpretation

### Trading Signals

#### Trend Following

- **Uptrend:** Price is above ALMA, and ALMA is sloping upwards.
- **Downtrend:** Price is below ALMA, and ALMA is sloping downwards.

#### Crossovers

- **Price Crossover:** Price crossing ALMA is a common signal. Because ALMA is smooth, these signals tend to be more reliable than SMA crossovers in noisy markets.
- **Dual ALMA:** Using two ALMAs (e.g., Period 9 vs Period 20) creates a crossover system.

### When It Works Best

- **Noisy Markets:** ALMA's Gaussian filtering excels at removing random price fluctuations while keeping the trend line intact.
- **Trend Reversals:** The high offset (0.85) allows ALMA to turn quickly when the trend changes, reducing the "give back" profit loss common with lagging indicators.

### When It Struggles

- **Ranging Markets:** Like all moving averages, ALMA will flatten out in a sideways market and price will oscillate around it, generating false signals.

## Comparison: ALMA vs EMA vs SMA

| Aspect | ALMA | EMA | SMA |
|--------|-----|-----|-----|
| **Weighting** | Gaussian (Bell Curve) | Exponential | Equal |
| **Lag** | Very Low (Configurable) | Low | High |
| **Smoothness** | High | Low | High |
| **Responsiveness** | High | High | Low |
| **Overshoot** | Minimal | Can overshoot | None |

**Summary:** ALMA is often considered a superior moving average because it offers the smoothness of an SMA with the responsiveness of an EMA.

## Architecture Notes

This implementation makes specific trade-offs:

### Choice: Precomputed Weights

- **Alternative:** Calculate Gaussian function on the fly.
- **Trade-off:** Higher memory usage (array of doubles) for faster updates.
- **Rationale:** `Math.Exp` is expensive. Precomputing is essential for performance.

### Choice: RingBuffer with DotProduct

- **Alternative:** Array copy or List.
- **Trade-off:** Slightly more complex indexing logic.
- **Rationale:** Zero allocation during updates. The `DotProduct` extension method is optimized for SIMD where possible.

### Choice: SIMD Vectorization

- **Implementation:** Uses `SimdExtensions.DotProduct` to perform the convolution.
- **Benefit:** Leverages hardware intrinsics (AVX2, SSE) to process multiple data points in parallel.
- **Impact:** Significant throughput increase for larger periods compared to scalar loops.

### Choice: Hybrid Memory Management

- **Strategy:** Uses `stackalloc` for small buffers (period < 1024) and `ArrayPool<double>.Shared` for larger ones.
- **Rationale:** Ensures **zero heap allocations** during the critical `Calculate` loop, preventing GC pressure while handling any period size safely.

### Choice: Optimized Normalization

- **Strategy:** Pre-calculates `_invWeightSum` (1 / sum of weights).
- **Rationale:** Replaces expensive division operations with faster multiplication in the hot path.

### Choice: Custom Convolution Logic (vs Conv.cs)

- **Decision:** Implemented custom convolution logic instead of wrapping `QuanTAlib.Conv`.
- **Rationale:** ALMA requires dynamic normalization during the warmup phase (partial window), which a generic convolution kernel does not support efficiently. Embedding the logic avoids the overhead of an adapter layer and allows for specific optimizations like incremental weight sum updates.

## References

- Legoux, Arnaud. "ALMA: Arnaud Legoux Moving Average."

## C# Usage

### Streaming Updates (Single Instance)

```csharp
using QuanTAlib;

var alma = new Alma(period: 9, offset: 0.85, sigma: 6.0);

// Process each new bar
TValue result = alma.Update(new TValue(timestamp, closePrice));
Console.WriteLine($"ALMA: {result.Value:F2}");

// Check if buffer is full
if (alma.IsHot)
{
    // Indicator is fully initialized
}
```

### Batch Processing (Historical Data)

```csharp
// TSeries API (object-oriented)
TSeries prices = ...; 
TSeries almaValues = Alma.Batch(prices, period: 9, offset: 0.85, sigma: 6.0);

// High-performance Span API (zero allocation)
double[] prices = new double[10000];
double[] output = new double[10000];
Alma.Calculate(prices.AsSpan(), output.AsSpan(), period: 9, offset: 0.85, sigma: 6.0);
```

### Bar Correction (isNew Parameter)

```csharp
var alma = new Alma(9);

// New bar arrives
alma.Update(new TValue(time, 100.5), isNew: true);

// Intra-bar price updates (real-time tick data)
alma.Update(new TValue(time, 101.0), isNew: false); // Updates current bar
alma.Update(new TValue(time, 100.8), isNew: false); // Updates current bar

// Next bar
alma.Update(new TValue(time + 60, 101.2), isNew: true); // Advances state
```

### Event-Driven Architecture

```csharp
var source = new TSeries();
var alma = new Alma(source, period: 9);

// Subscribe to ALMA output
alma.Pub += (value) => {
    Console.WriteLine($"New ALMA value: {value.Value}");
};

// Feeding source automatically triggers the chain
source.Add(new TValue(DateTime.Now, 105.2));
```
