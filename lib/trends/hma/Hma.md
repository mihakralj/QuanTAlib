# HMA: Hull Moving Average

## What It Does

The Hull Moving Average (HMA) is famous for being "extremely fast and smooth." It solves the age-old problem of lag in moving averages by using weighted averages in a clever way to cancel out delay while simultaneously smoothing the data. The result is an indicator that hugs the price action tightly during trends but remains smooth enough to avoid false signals during minor corrections.

## Historical Context

Developed by Alan Hull in 2005, the HMA was introduced to the trading community as a solution to the lag vs. noise dilemma. Hull, an Australian mathematician and trader, realized that by over-weighting recent data using a specific combination of WMAs, he could virtually eliminate lag.

## How It Works

### The Core Idea

Hull's insight was based on the observation that if you take a short-term average and a long-term average, the difference between them can be used to predict where the price "should" be if there were no lag.

The algorithm has three steps:

1. Calculate a WMA with half the period ($n/2$). This is fast but noisy.
2. Calculate a WMA with the full period ($n$). This is slow but smooth.
3. Subtract the slow one from the fast one, double the result, and smooth *that* result with a WMA of the square root of the period ($\sqrt{n}$).

### Mathematical Foundation

$$HMA = WMA\left( \sqrt{n}, \quad 2 \cdot WMA\left(\frac{n}{2}, P\right) - WMA(n, P) \right)$$

Where:

- $n$ is the period.
- $P$ is the price series.
- $WMA(period, data)$ is the Weighted Moving Average.

The term $2 \cdot WMA(n/2) - WMA(n)$ creates a "velocity" vector that overshoots the price slightly to compensate for lag. The final $WMA(\sqrt{n})$ smooths out this overshoot.

### Implementation Details

Our implementation orchestrates three internal `Wma` instances.

- **Complexity:** O(1) per update (since WMA is O(1)).
- **Memory:** O(period) to store buffers for the three WMAs.
- **Optimization:** The batch calculation uses `ArrayPool` to minimize allocations for intermediate buffers and SIMD instructions for the vector math.

## Configuration

| Parameter | Default | Purpose | Adjustment Guidelines |
|-----------|---------|---------|----------------------|
| Period | 14 | Lookback window | Shorter (9-12) = Scalping; Longer (20-50) = Swing Trading |

**Configuration note:** HMA is significantly faster than SMA or EMA. An HMA(20) is often faster than an EMA(10).

## C# Usage

### Streaming Updates (Single Instance)

```csharp
using QuanTAlib;

var hma = new Hma(period: 14);

// Process each new bar
TValue result = hma.Update(new TValue(timestamp, closePrice));
Console.WriteLine($"HMA: {result.Value:F2}");

// Check if buffer is full
if (hma.IsHot)
{
    // Indicator is fully initialized
}
```

### Batch Processing (Historical Data)

```csharp
// TSeries API
TSeries prices = ...;
TSeries hmaValues = Hma.Batch(prices, period: 14);

// Span API (High Performance)
double[] prices = new double[1000];
double[] output = new double[1000];
Hma.Calculate(prices.AsSpan(), output.AsSpan(), period: 14);
```

### Bar Correction (isNew Parameter)

```csharp
var hma = new Hma(14);

// New bar
hma.Update(new TValue(time, 100), isNew: true);

// Intra-bar update
hma.Update(new TValue(time, 101), isNew: false); // Replaces 100 with 101
```

## Performance Profile

| Operation | Complexity | Description |
|-----------|------------|-------------------|
| Streaming update | O(1) | Three O(1) WMA updates |
| Bar correction | O(1) | Efficient state rollback |
| Batch processing | O(N) | Three passes over data + vector math |
| Memory footprint | O(period) | Three RingBuffers |

## Interpretation

### Trading Signals

#### Trend Identification

- **Slope Change:** Because HMA turns so quickly, the most common signal is simply the change in slope (turning up or turning down).
- **Price Crossover:** Price crossing the HMA is a very aggressive entry signal.

#### Crossovers

- **HMA Crossover:** HMA(9) crossing HMA(20) is a popular strategy for capturing short-term swings.

### When It Works Best

- **Swing Trading:** HMA is perfect for capturing the "meat" of a swing move. It gets you in early and gets you out before the reversal wipes out profits.

### When It Struggles

- **Overshoot:** In very choppy markets, the HMA can overshoot price spikes, creating a "hook" that looks like a reversal but is just a reaction to noise.

## Comparison: HMA vs EMA vs SMA

| Aspect | HMA | EMA | SMA |
|--------|-----|-----|-----|
| **Lag** | Very Low | Low | High |
| **Smoothness** | High | Moderate | High |
| **Responsiveness** | Very High | High | Low |
| **Overshoot** | Moderate | Low | None |

**Summary:** Use HMA when you need the absolute fastest reaction time without sacrificing smoothness.

## Architecture Notes

This implementation makes specific trade-offs:

### Choice: Composition

- **Alternative:** Single complex formula.
- **Trade-off:** Overhead of managing 3 objects.
- **Rationale:** Correctness. Implementing HMA from scratch is error-prone. Composing it from tested WMA units ensures reliability.

### Choice: ArrayPool for Batch

- **Alternative:** `new double[]`.
- **Trade-off:** Complexity of `Rent`/`Return`.
- **Rationale:** Batch processing often happens in tight loops (e.g., optimization). Allocating large arrays for intermediate results triggers GC. `ArrayPool` eliminates this pressure.

## References

- Hull, Alan. "Active Investing." Wrightbooks, 2005.
- [Alan Hull's Official HMA Description](https://alan.hull.com.au/hma.html)
