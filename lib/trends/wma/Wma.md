# WMA: Weighted Moving Average

## What It Does

The Weighted Moving Average (WMA) addresses the lag issue inherent in Simple Moving Averages (SMA) by assigning linearly decreasing weights to historical prices. Recent data points carry significantly more influence than older ones, resulting in a trend indicator that reacts faster to price changes while maintaining better smoothness than exponential alternatives. It strikes a balance between responsiveness and noise reduction.

## Historical Context

While moving averages have been a staple of financial analysis since the early 20th century, the Weighted Moving Average gained prominence as traders sought a middle ground between the significant lag of the SMA and the potential hypersensitivity of the EMA. It became a standard tool in technical analysis packages in the 1980s, offering a mathematically straightforward way to prioritize recent market action without the infinite memory tail of exponential smoothing.

## How It Works

### The Core Idea

Imagine a 5-day WMA. Today's price is the most important, so it gets a weight of 5. Yesterday's price gets a weight of 4, and so on, back to the oldest price in the window which gets a weight of 1. You sum up all these weighted prices and divide by the sum of the weights (1+2+3+4+5 = 15). As the window moves forward, the oldest price drops off completely, and every other price effectively "slides down" in importance, with the new price taking the top weight.

### Mathematical Foundation

$$WMA = \frac{n \cdot P_n + (n-1) \cdot P_{n-1} + \ldots + 1 \cdot P_1}{\frac{n(n+1)}{2}}$$

Where:

- $n$ = period length
- $P_i$ = price at position $i$ (where $P_n$ is the most recent price)
- Denominator = $\frac{n(n+1)}{2}$ (the sum of weights from 1 to $n$, also known as the triangular number)

### Implementation Details: O(1) Streaming

A naive WMA implementation recalculates the entire weighted sum for each new bar, resulting in O(n) complexity. As the period grows, the calculation gets slower.

We use a dual running sum approach to achieve **O(1)** complexity:

1. Maintain a simple unweighted sum of prices ($S$).
2. Maintain the weighted sum ($W$).

When a new price ($P_{new}$) arrives and the oldest price ($P_{old}$) leaves the window:
$$W_{new} = W_{old} - S_{old} + (n \cdot P_{new})$$
$$S_{new} = S_{old} - P_{old} + P_{new}$$

This reduces the calculation to two subtractions, two additions, and one multiplication, regardless of the period length. To prevent floating-point drift from accumulating over millions of updates, we perform a full recalculation every 10,000 ticks.

## Configuration

| Parameter | Default | Purpose | Adjustment Guidelines |
|-----------|---------|---------|----------------------|
| Period | 14 | Lookback window | Shorter (5-10) = scalping/intraday; Longer (20-50) = swing/trend following |
| Source | Close | Price input | Typical usage is Close, but HL2 or HLC3 can provide smoother inputs |

## C# Usage

### Streaming Updates (Single Instance)

```csharp
using QuanTAlib;

var wma = new Wma(period: 14);

// Process each new bar
TValue result = wma.Update(new TValue(timestamp, closePrice));
Console.WriteLine($"WMA: {result.Value:F2}");

// Check if buffer is full
if (wma.IsHot)
{
    // Indicator is fully initialized
}
```

### Batch Processing (Historical Data)

```csharp
// TSeries API (object-oriented)
TSeries prices = ...; 
TSeries wmaValues = Wma.Batch(prices, period: 14);

// High-performance Span API (zero allocation)
double[] prices = new double[10000];
double[] output = new double[10000];
Wma.Batch(prices.AsSpan(), output.AsSpan(), period: 14);

// The Span API utilizes SIMD (AVX2, AVX512, Neon) for maximum performance
// on supported hardware.
```

### Bar Correction (isNew Parameter)

```csharp
var wma = new Wma(14);

// New bar arrives
wma.Update(new TValue(time, 100.5), isNew: true);

// Intra-bar price updates (real-time tick data)
wma.Update(new TValue(time, 101.0), isNew: false); // Updates current bar
wma.Update(new TValue(time, 100.8), isNew: false); // Updates current bar

// Next bar
wma.Update(new TValue(time + 60, 101.2), isNew: true); // Advances state
```

### Event-Driven Architecture

```csharp
var source = new TSeries();
var wma = new Wma(source, period: 14);

// Subscribe to WMA output
wma.Pub += (value) => {
    Console.WriteLine($"New WMA value: {value.Value}");
};

// Feeding source automatically triggers the chain
source.Add(new TValue(DateTime.Now, 105.2));
```

### Handling Invalid Data

```csharp
var wma = new Wma(14);

wma.Update(new TValue(time, 100));
wma.Update(new TValue(time, double.NaN));  // Uses last valid value (100)
wma.Update(new TValue(time, 110));         // Resumes normal calculation
```

## Performance Profile

| Operation | Complexity | Description |
|-----------|------------|-------------------|
| Streaming update | O(1) | Constant time regardless of period length |
| Bar correction | O(1) | Efficient state rollback for real-time feeds |
| Batch processing | O(n) | SIMD-optimized (AVX2/AVX512/Neon) for high throughput |
| Memory footprint | O(period) | Uses a RingBuffer to store the lookback window |

**Note:** The batch implementation automatically selects the best available SIMD instruction set (AVX512, AVX2, or ARM Neon) for the running hardware, falling back to a scalar implementation if necessary.

## Interpretation

### Trading Signals

#### Trend Identification

- **Uptrend:** Price is consistently above the WMA, and the WMA slope is positive.
- **Downtrend:** Price is consistently below the WMA, and the WMA slope is negative.

#### Crossovers

- **Price Crossover:** Price crossing above the WMA suggests a potential bullish reversal. Price crossing below suggests a bearish reversal.
- **Dual WMA:** Using two WMAs (e.g., 20 and 50). Fast crossing above Slow is a "Golden Cross" (bullish). Fast crossing below Slow is a "Death Cross" (bearish).

### When It Works Best

- **Trending Markets:** WMA excels in clearly defined trends where its reduced lag allows traders to enter and exit positions earlier than with an SMA.
- **Swing Trading:** The linear weighting aligns well with swing trading timeframes, capturing momentum shifts effectively.

### When It Struggles

- **Choppy/Sideways Markets:** Like all moving averages, WMA will generate false signals in range-bound markets.
- **Drop-off Effect:** Because the oldest price drops off the calculation entirely (weight goes from 1 to 0), a large price spike exiting the window can cause the WMA to move counter-intuitively, though less severely than an SMA.

## Architecture Notes

This implementation makes specific trade-offs:

### Choice: Dual Running Sums for O(1)

- **Alternative:** Recalculate weighted sum every bar (O(n)).
- **Trade-off:** Requires maintaining two state variables ($S$ and $W$) and a RingBuffer.
- **Rationale:** Critical for performance in real-time systems monitoring thousands of assets with long periods.

### Choice: Periodic Resync

- **Alternative:** Never resync.
- **Trade-off:** Small CPU cost every 10,000 ticks.
- **Rationale:** Floating-point errors accumulate in running sums. Periodic recalculation ensures long-running server stability.

#### Choice: SIMD for Batch

- **Alternative:** Scalar loop.
- **Trade-off:** Code complexity (multiple execution paths).
- **Rationale:** Batch processing is often the bottleneck in backtesting. SIMD provides 4-8x throughput improvement.

## References

- Colby, Robert W. "The Encyclopedia of Technical Market Indicators." McGraw-Hill, 2002.
- Murphy, John J. "Technical Analysis of the Financial Markets." New York Institute of Finance, 1999.
