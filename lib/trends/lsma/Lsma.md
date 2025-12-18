# LSMA: Least Squares Moving Average

## What It Does

The Least Squares Moving Average (LSMA), also known as the Moving Linear Regression or End Point Moving Average, calculates the linear regression line for a specified period and returns the value at the current bar (or a projected point). Unlike traditional moving averages that simply average past prices, LSMA fits a straight line to the data to minimize the sum of squared errors, providing a better representation of the trend direction and strength. It effectively projects where the price "should be" based on the recent linear trend.

## Historical Context

The concept of Least Squares is a fundamental statistical method dating back to Carl Friedrich Gauss in the early 19th century. In technical analysis, applying this method over a moving window allows traders to capture the dynamic trend of an asset. By focusing on the "line of best fit," LSMA attempts to filter out noise while maintaining a high degree of responsiveness to the underlying trend, distinguishing it from lag-prone averages like the SMA.

## How It Works

### The Core Idea

For every new bar, the LSMA looks back at the last $n$ prices and calculates the straight line that best fits those data points. The value of the LSMA is the endpoint of this line corresponding to the current time. If the trend is strongly up, the line will point up, and the LSMA value will likely be higher than the current price if the price has dipped, or lower if the price has surged ahead of the trend.

### Mathematical Foundation

For a period $n$, we fit a line $y = mx + b$ where $x$ represents the time index ($0$ to $n-1$).

The slope $m$ and intercept $b$ are calculated as:

$$ m = \frac{n \sum(xy) - \sum x \sum y}{n \sum(x^2) - (\sum x)^2} $$

$$ b = \frac{\sum y - m \sum x}{n} $$

The LSMA value is then calculated at the desired offset:

$$ LSMA = b + m \times (n - 1 + \text{offset}) $$

Where:

- $n$ = period length
- $x$ = time index
- $y$ = price value
- $\text{offset}$ = projection into future (positive) or past (negative)

### Implementation Details: O(1) Streaming

A naive implementation would recalculate the regression sums ($\sum x, \sum y, \sum xy, \sum x^2$) from scratch for every bar, leading to O(n) complexity.

We optimize this to **O(1)** by maintaining running sums:

1. $\sum x$ and $\sum x^2$ are constant for a fixed window size and coordinate system.
2. $\sum y$ is updated incrementally: $\sum y_{new} = \sum y_{old} - y_{oldest} + y_{new}$.
3. $\sum xy$ is updated using the formula:
   $$ \sum xy_{new} = \sum xy_{old} + \sum y_{prev} - n \cdot y_{oldest} $$

This allows the LSMA to update in constant time regardless of the period length. To ensure numerical stability, the sums are fully recalculated every 1,000 ticks.

## Configuration

| Parameter | Default | Purpose | Adjustment Guidelines |
|-----------|---------|---------|----------------------|
| Period | 14 | Lookback window | Shorter (5-10) for fast trend detection; Longer (20-50) for major trend filtering |
| Offset | 0 | Projection shift | 0 = current bar; >0 projects future; <0 retrieves past regression value |
| Source | Close | Price input | Can be applied to any data series |

## C# Usage

### Streaming Updates (Single Instance)

```csharp
using QuanTAlib;

var lsma = new Lsma(period: 14);

// Process each new bar
TValue result = lsma.Update(new TValue(timestamp, closePrice));
Console.WriteLine($"LSMA: {result.Value:F2}");

// Check if buffer is full
if (lsma.IsHot)
{
    // Indicator is fully initialized
}
```

### Batch Processing (Historical Data)

```csharp
// TSeries API (object-oriented)
TSeries prices = ...;
TSeries lsmaValues = Lsma.Batch(prices, period: 14);

// High-performance Span API (zero allocation)
double[] prices = new double[10000];
double[] output = new double[10000];
Lsma.Calculate(prices.AsSpan(), output.AsSpan(), period: 14);
```

### Bar Correction (isNew Parameter)

```csharp
var lsma = new Lsma(14);

// New bar arrives
lsma.Update(new TValue(time, 100.5), isNew: true);

// Intra-bar price updates (real-time tick data)
lsma.Update(new TValue(time, 101.0), isNew: false); // Updates current bar
lsma.Update(new TValue(time, 100.8), isNew: false); // Updates current bar

// Next bar
lsma.Update(new TValue(time + 60, 101.2), isNew: true); // Advances state
```

### Event-Driven Architecture

```csharp
var source = new TSeries();
var lsma = new Lsma(source, period: 14);

// Subscribe to LSMA output
lsma.Pub += (value) => {
    Console.WriteLine($"New LSMA value: {value.Value}");
};

// Feeding source automatically triggers the chain
source.Add(new TValue(DateTime.Now, 105.2));
```

### Handling Invalid Data

```csharp
var lsma = new Lsma(14);

lsma.Update(new TValue(time, 100));
lsma.Update(new TValue(time, double.NaN));  // Uses last valid value (100)
lsma.Update(new TValue(time, 110));         // Resumes normal calculation
```

## Performance Profile

| Operation | Complexity | Description |
|-----------|------------|-------------------|
| Streaming update | O(1) | Constant time regression update |
| Bar correction | O(1) | Efficient state rollback for real-time feeds |
| Batch processing | O(n) | Fast sequential processing |
| Memory footprint | O(period) | Uses a RingBuffer to store the lookback window |

## Interpretation

### Trading Signals

#### Trend Direction

- **Bullish:** LSMA is rising and price is above LSMA.
- **Bearish:** LSMA is falling and price is below LSMA.

#### Crossovers

- **Price Crossover:** Price crossing the LSMA line is often used as a signal of trend change.
- **Slope Change:** A change in the slope of the LSMA (e.g., from positive to negative) indicates a potential reversal.

### When It Works Best

- **Trending Markets:** LSMA provides a smooth, responsive trend line that hugs price action closer than SMA.
- **Reversals:** Due to its regression nature, it can identify turning points relatively quickly.

### When It Struggles

- **Sideways Markets:** Like other moving averages, it can produce whipsaws in ranging conditions, though the regression fit may offer slightly better noise filtering than a raw SMA.

### Architecture Notes

This implementation makes specific trade-offs:

### Choice: O(1) Regression Update

- **Alternative:** Recalculate regression sums every bar (O(n)).
- **Trade-off:** Requires maintaining running sums for $\sum y$ and $\sum xy$.
- **Rationale:** Essential for performance when using long periods or processing high-frequency data.

### Choice: Periodic Resync

- **Alternative:** Rely solely on incremental updates.
- **Trade-off:** Small CPU cost every 1,000 ticks.
- **Rationale:** Prevents floating-point error accumulation in the $\sum xy$ term, ensuring long-term accuracy.

## References

- [Linear Regression in Technical Analysis](https://www.investopedia.com/terms/l/linearregression.asp)
- [Least Squares Moving Average](https://www.tradingview.com/support/solutions/43000502584-least-squares-moving-average-lsma/)
