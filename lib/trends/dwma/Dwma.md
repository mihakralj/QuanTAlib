# DWMA: Double Weighted Moving Average

## What It Does

The Double Weighted Moving Average (DWMA) is a smoothing indicator that applies a Weighted Moving Average (WMA) twice. By smoothing the data once and then smoothing the result again, DWMA produces an exceptionally clean curve that filters out significant market noise. The trade-off is increased lag compared to a single WMA, making it more suitable for identifying major trends rather than short-term scalping.

## Historical Context

While the concept of double smoothing dates back to the early days of technical analysis (with the Triangular Moving Average being a close cousin), the DWMA gained utility as computing power allowed traders to easily chain indicators. It represents a logical extension of the WMA for traders who found the standard WMA too jittery but appreciated its linear weighting scheme.

## How It Works

### The Core Idea

Think of DWMA as a "filter of a filter."

1. First, you calculate a standard WMA of the price. This removes high-frequency noise but leaves some jaggedness.
2. Then, you calculate a WMA of that *first* WMA. This polishes the curve, resulting in a very smooth line that clearly defines the underlying trend direction.

### Mathematical Foundation

1. Calculate the first WMA: $WMA_1 = WMA(Price, n)$
2. Calculate the second WMA: $DWMA = WMA(WMA_1, n)$

Where $n$ is the period length.

Because WMA uses linear weighting (triangle weights), applying it twice creates a weighting structure that resembles a bell curve (Gaussian-like), giving the most weight to the center of the lookback window and tapering off smoothly at both ends.

### Implementation Details

Our implementation wraps two instances of the `Wma` class.

- **Complexity:** O(1) per update (since WMA is O(1)).
- **Memory:** O(period) to store the buffers for both internal WMAs.
- **Warmup:** Requires roughly $2 \times period$ bars to fully stabilize.

## Configuration

| Parameter | Default | Purpose | Adjustment Guidelines |
|-----------|---------|---------|----------------------|
| Period | 14 | Lookback window | Shorter = Faster trend detection; Longer = Major trend identification |

**Configuration note:** A DWMA(10) will have roughly the same lag as a WMA(15-20) but will be significantly smoother.

## C# Usage

### Streaming Updates (Single Instance)

```csharp
using QuanTAlib;

var dwma = new Dwma(period: 14);

// Process each new bar
TValue result = dwma.Update(new TValue(timestamp, closePrice));
Console.WriteLine($"DWMA: {result.Value:F2}");

// Check if buffer is full
if (dwma.IsHot)
{
    // Indicator is fully initialized
}
```

### Batch Processing (Historical Data)

```csharp
// TSeries API
TSeries prices = ...;
TSeries dwmaValues = Dwma.Batch(prices, period: 14);

// Span API (High Performance)
double[] prices = new double[1000];
double[] output = new double[1000];
Dwma.Calculate(prices.AsSpan(), output.AsSpan(), period: 14);
```

### Bar Correction (isNew Parameter)

```csharp
var dwma = new Dwma(14);

// New bar
dwma.Update(new TValue(time, 100), isNew: true);

// Intra-bar update
dwma.Update(new TValue(time, 101), isNew: false); // Replaces 100 with 101
```

## Performance Profile

| Operation | Complexity | Description |
|-----------|------------|-------------------|
| Streaming update | O(1) | Two O(1) WMA updates |
| Bar correction | O(1) | Efficient state rollback |
| Batch processing | O(N) | Two passes over the data |
| Memory footprint | O(period) | Two RingBuffers |

## Interpretation

### Trading Signals

#### Trend Identification

- **Major Trend:** DWMA is excellent for defining the "background" trend. If price is above DWMA, the bias is bullish.
- **Support/Resistance:** Due to its smoothness, DWMA often acts as dynamic support in uptrends and resistance in downtrends.

#### Crossovers

- **Price Crossover:** Price crossing DWMA signals a major trend change.
- **DWMA/WMA Crossover:** Using a WMA(14) crossing a DWMA(14) creates a signal similar to MACD but directly on the price chart.

### When It Works Best

- **Long-Term Trends:** DWMA filters out the "noise" of daily volatility, letting you stay in a trade during minor pullbacks.
- **Visual Clarity:** It produces a very clean line on the chart, reducing visual clutter.

### When It Struggles

- **Scalping:** The double smoothing introduces too much lag for very short-term trading.
- **Reversals:** DWMA will be slow to recognize a sharp V-bottom or V-top reversal.

## Comparison: DWMA vs WMA vs SMA

| Aspect | WMA | DWMA | SMA |
|--------|-----|------|-----|
| **Lag** | Moderate | High | High |
| **Smoothness** | Moderate | Very High | High |
| **Responsiveness** | Moderate | Low | Low |
| **Weighting** | Linear | Bell-curve-like | Equal |

**Summary:** Use DWMA when smoothness is your priority and you are willing to accept some lag to avoid false signals.

## Architecture Notes

This implementation makes specific trade-offs:

### Choice: Composition

- **Alternative:** Implement a single "Double Weighted" formula.
- **Trade-off:** Slight function call overhead.
- **Rationale:** Reusing the optimized `Wma` class ensures correctness and benefits from any future optimizations to the base WMA (like SIMD).

### Choice: Temporary Buffer for Batch

- **Alternative:** Single pass calculation.
- **Trade-off:** Memory allocation for intermediate results.
- **Rationale:** Calculating DWMA in a single pass is mathematically complex and hard to vectorize. Two optimized WMA passes are faster and easier to maintain.

## References

- Kaufman, Perry J. "Trading Systems and Methods." Wiley, 2013.
