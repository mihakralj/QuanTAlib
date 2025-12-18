# TRIMA: Triangular Moving Average

## What It Does

The Triangular Moving Average (TRIMA) is a weighted moving average where the weights are assigned in a triangular pattern. The most recent data and the oldest data carry the least weight, while the data in the middle of the period carries the most weight. This creates a double-smoothing effect that produces a line much smoother than a Simple Moving Average (SMA) or Exponential Moving Average (EMA), making it ideal for identifying the primary trend without the distraction of short-term noise.

## Historical Context

While the concept of triangular weighting has roots in statistical signal processing, it was popularized in technical analysis as a way to solve the "whipsaw" problem of SMAs. By de-emphasizing the most recent data (which is often noisy), TRIMA focuses on the "consensus" of value over the period.

## How It Works

### The Core Idea

TRIMA is mathematically equivalent to a "double SMA."

- **SMA:** Average of $N$ prices.
- **TRIMA:** Average of an Average. Specifically, an SMA of period $X$ applied to an SMA of period $X$.

Because it averages an average, it is extremely smooth. However, this double smoothing comes at the cost of increased lag. It will turn significantly later than an EMA or SMA.

### Mathematical Foundation

The weights form a triangle. For a period of 5:

- Weights: 1, 2, 3, 2, 1
- Sum of weights: $1+2+3+2+1 = 9$

Formula:
$$ TRIMA = \frac{\sum (Price_i \times Weight_i)}{\sum Weights} $$

Equivalent Calculation (Double SMA):
$$ TRIMA(N) \approx SMA(SMA(Price, \lceil N/2 \rceil), \lfloor N/2 \rfloor + 1) $$

### Implementation Details

Our implementation uses the Double SMA method for O(1) efficiency.

- **Complexity:** O(1) per update (two sliding window sums).
- **Stability:** Inherits the stability of SMA.

## Configuration

| Parameter | Default | Purpose | Adjustment Guidelines |
|-----------|---------|---------|----------------------|
| Period | 14 | Lookback window | Standard lookback. |

## C# Usage

### Streaming Updates (Single Instance)

```csharp
using QuanTAlib;

var trima = new Trima(period: 14);

// Process each new bar
TValue result = trima.Update(new TValue(timestamp, closePrice));
Console.WriteLine($"TRIMA: {result.Value:F2}");

// Check if buffer is full
if (trima.IsHot)
{
    // Indicator is fully initialized
}
```

### Batch Processing (Historical Data)

```csharp
// TSeries API
TSeries prices = ...;
TSeries trimaValues = Trima.Batch(prices, period: 14);

// Span API (High Performance)
double[] prices = new double[1000];
double[] output = new double[1000];
Trima.Calculate(prices.AsSpan(), output.AsSpan(), period: 14);
```

### Bar Correction (isNew Parameter)

```csharp
var trima = new Trima(14);

// New bar
trima.Update(new TValue(time, 100), isNew: true);

// Intra-bar update
trima.Update(new TValue(time, 101), isNew: false); // Replaces 100 with 101
```

## Performance Profile

| Operation | Complexity | Description |
|-----------|------------|-------------------|
| Streaming update | O(1) | Two sliding window sums |
| Bar correction | O(1) | Efficient state rollback |
| Batch processing | O(N) | Single pass through data |
| Memory footprint | O(period) | RingBuffers for the two internal SMAs |

## Interpretation

### Trading Signals

#### Trend Identification

- **Primary Trend:** TRIMA is excellent for visualizing the "major" trend. If TRIMA is rising, the long-term direction is up, regardless of short-term chops.

### When It Works Best

- **Visual Clarity:** Traders often use TRIMA not for signals, but to declutter charts and see the underlying market structure.

### When It Struggles

- **Timing Entries:** Due to its significant lag, TRIMA is poor for timing entries or exits. It is a lagging indicator, not a leading one.

## Architecture Notes

This implementation makes specific trade-offs:

### Choice: Double SMA Composition

- **Implementation:** Composed of two `Sma` objects.
- **Rationale:** This is mathematically equivalent to the weighted sum method but allows us to reuse the O(1) optimization of the `Sma` class.

## References

- Merrill, Arthur A. "Filtered Waves." *Technical Analysis of Stocks & Commodities*.
