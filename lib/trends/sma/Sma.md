# SMA: Simple Moving Average

## What It Does

The Simple Moving Average (SMA) is the most fundamental indicator in technical analysis. It calculates the unweighted mean of the previous $N$ data points. By smoothing out price fluctuations, it helps traders identify the direction of the trend and potential support/resistance levels.

## Historical Context

The concept of a moving average dates back to the early 20th century, used by statisticians to smooth time series data. In financial markets, it became a cornerstone of technical analysis with the advent of computing, allowing traders to filter out "noise" and focus on the underlying trend.

## How It Works

### The Core Idea

The SMA treats every price in the lookback window equally. A price from 10 days ago has the same influence on the average as the price from today. This "democracy" of data points makes it stable but slow to react to recent changes compared to weighted averages like EMA or WMA.

### Mathematical Foundation

$$ SMA_t = \frac{P_t + P_{t-1} + \dots + P_{t-n+1}}{n} $$

Where:

- $P$ = Price
- $n$ = Period length

### Implementation Details: O(1) Streaming

A naive implementation sums all $N$ prices every bar, resulting in $O(N)$ complexity. We optimize this to **O(1)** using a sliding window algorithm:

$$ Sum_{new} = Sum_{old} - P_{leaving} + P_{entering} $$
$$ SMA_{new} = \frac{Sum_{new}}{n} $$

This ensures that calculating an SMA(200) takes the exact same amount of CPU time as an SMA(10).

## Configuration

| Parameter | Default | Purpose | Adjustment Guidelines |
|-----------|---------|---------|----------------------|
| Period | 10 | Lookback window | Short (10-20) for short-term trends; Medium (50) for intermediate; Long (200) for major trends. |

## Performance Profile

| Operation | Complexity | Description |
|-----------|------------|-------------------|
| Streaming update | O(1) | Sliding window sum |
| Bar correction | O(1) | Efficient state rollback |
| Batch processing | O(N) | Single pass through data |
| Memory footprint | O(period) | RingBuffer for lookback window |

## Interpretation

### Trading Signals

#### Trend Direction

- **Uptrend:** Price > SMA and SMA slope is positive.
- **Downtrend:** Price < SMA and SMA slope is negative.

#### Crossovers

- **Golden Cross:** Short-term SMA (e.g., 50) crosses above Long-term SMA (e.g., 200). Bullish.
- **Death Cross:** Short-term SMA crosses below Long-term SMA. Bearish.

#### Support/Resistance

- The 50-day and 200-day SMAs are widely watched by institutions and often act as self-fulfilling support or resistance levels.

### When It Works Best

- **Strong Trends:** In clearly trending markets, SMA keeps you on the right side of the move.

### When It Struggles

- **Sideways Markets:** In ranging markets, price will constantly cross the SMA, generating false signals (whipsaws).

## Architecture Notes

This implementation makes specific trade-offs:

### Choice: RingBuffer for History

- **Implementation:** Uses a circular buffer to store the last $N$ prices.
- **Rationale:** Necessary to know which value is leaving the window ($P_{leaving}$) for the O(1) update.

### Choice: Periodic Resync

- **Implementation:** Recalculates the full sum every few thousand ticks.
- **Rationale:** Prevents floating-point errors from accumulating in the running sum over very long data streams.

## References

- Murphy, John J. "Technical Analysis of the Financial Markets." New York Institute of Finance, 1999.

## C# Usage

### Streaming Updates (Single Instance)

```csharp
using QuanTAlib;

var sma = new Sma(period: 20);

// Process each new bar
TValue result = sma.Update(new TValue(timestamp, closePrice));
Console.WriteLine($"SMA: {result.Value:F2}");

// Check if buffer is full
if (sma.IsHot)
{
    // Indicator is fully initialized
}
```

### Batch Processing (Historical Data)

```csharp
// TSeries API
TSeries prices = ...;
TSeries smaValues = Sma.Batch(prices, period: 20);

// Span API (High Performance)
double[] prices = new double[1000];
double[] output = new double[1000];
Sma.Calculate(prices.AsSpan(), output.AsSpan(), period: 20);
```

### Bar Correction (isNew Parameter)

```csharp
var sma = new Sma(20);

// New bar
sma.Update(new TValue(time, 100), isNew: true);

// Intra-bar update
sma.Update(new TValue(time, 101), isNew: false); // Replaces 100 with 101
