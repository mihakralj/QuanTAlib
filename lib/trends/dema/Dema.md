# DEMA: Double Exponential Moving Average

## What It Does

The Double Exponential Moving Average (DEMA) is a faster, more responsive version of the traditional EMA. It was designed to reduce the lag inherent in trend-following indicators. Despite its name, it is not simply a "double smoothing" (which would increase lag); rather, it uses a clever combination of a single EMA and a double EMA to subtract lag from the original signal.

## Historical Context

Patrick Mulloy introduced DEMA in the January 1994 issue of *Technical Analysis of Stocks & Commodities* magazine. His goal was to create a moving average that could respond more quickly to market changes than the standard EMA, making it more suitable for the faster-paced trading environments that were emerging at the time.

## How It Works

### The Core Idea

Standard moving averages introduce lag. If you smooth a moving average again (EMA of EMA), you get a smoother line, but with *more* lag. Mulloy's insight was that the difference between the single EMA and the double EMA represents a measure of the "lag error." By adding this difference back to the single EMA, you can effectively cancel out much of the lag.

Think of it as:
`DEMA = EMA + (EMA - EMA_of_EMA)`
`DEMA = 2 * EMA - EMA_of_EMA`

### Mathematical Foundation

1. Calculate the EMA of the price: $EMA_1 = EMA(Price)$
2. Calculate the EMA of the first EMA: $EMA_2 = EMA(EMA_1)$
3. Calculate DEMA:
    $$DEMA = 2 \cdot EMA_1 - EMA_2$$

This formula effectively boosts the weighting of the most recent data, making the indicator turn faster than a standard EMA of the same period.

### Implementation Details

Our implementation uses a zero-lag initialization technique for the internal EMAs. Instead of waiting for the EMA to converge from 0 (which takes hundreds of bars), we use a "compensator" factor that scales the early values to be statistically valid immediately.

- **Complexity:** O(1) per update.
- **State:** Maintains two internal EMA states.
- **Convergence:** DEMA converges slightly slower than a single EMA because it depends on the second EMA stabilizing.

## Configuration

| Parameter | Default | Purpose | Adjustment Guidelines |
|-----------|---------|---------|----------------------|
| Period | 10 | Lookback window | Shorter = Scalping (very fast); Longer = Trend following |

**Configuration note:** Because DEMA is faster than EMA, you may need to use a slightly longer period (e.g., 14 instead of 10) to get comparable smoothness with better responsiveness.

## C# Usage

### Streaming Updates (Single Instance)

```csharp
using QuanTAlib;

var dema = new Dema(period: 10);

// Process each new bar
TValue result = dema.Update(new TValue(timestamp, closePrice));
Console.WriteLine($"DEMA: {result.Value:F2}");

// Check if buffer is full
if (dema.IsHot)
{
    // Indicator is fully initialized
}
```

### Batch Processing (Historical Data)

```csharp
// TSeries API
TSeries prices = ...;
TSeries demaValues = Dema.Calculate(prices, period: 10);

// Span API (High Performance)
double[] prices = new double[1000];
double[] output = new double[1000];
Dema.Calculate(prices.AsSpan(), output.AsSpan(), period: 10);
```

### Bar Correction (isNew Parameter)

```csharp
var dema = new Dema(10);

// New bar
dema.Update(new TValue(time, 100), isNew: true);

// Intra-bar update
dema.Update(new TValue(time, 101), isNew: false); // Replaces 100 with 101
```

## Performance Profile

| Operation | Complexity | Description |
|-----------|------------|-------------------|
| Streaming update | O(1) | Two EMA updates + one subtraction |
| Bar correction | O(1) | Efficient state rollback |
| Batch processing | O(N) | Single pass through data |
| Memory footprint | O(1) | Minimal state (4 doubles) |

## Interpretation

### Trading Signals

#### Trend Identification

- **Uptrend:** Price > DEMA.
- **Downtrend:** Price < DEMA.
- **Reversal:** Because DEMA turns so quickly, a change in slope is often an early warning of a trend change.

#### Crossovers

- **Price Crossover:** Price crossing DEMA is a very aggressive signal.
- **DEMA/EMA Crossover:** Using DEMA(20) crossing EMA(20) can signal a change in momentum strength.

### When It Works Best

- **Fast Trends:** DEMA shines in markets that move quickly and reverse sharply.
- **Scalping:** Its low lag makes it ideal for short-term trading on 1-minute or 5-minute charts.

### When It Struggles

- **Whipsaws:** Because it is so responsive, DEMA produces many false signals in choppy, sideways markets. It offers very little noise filtering compared to SMA or WMA.

## Comparison: DEMA vs EMA vs TEMA

| Aspect | EMA | DEMA | TEMA |
|--------|-----|------|------|
| **Lag** | Moderate | Low | Very Low |
| **Smoothness** | Moderate | Low | Very Low |
| **Responsiveness** | Moderate | High | Very High |
| **Overshoot** | Minimal | Moderate | High |

**Summary:** Use DEMA when EMA is too slow but you don't want the extreme volatility of TEMA (Triple EMA).

## Architecture Notes

This implementation makes specific trade-offs:

### Choice: Zero-Lag Initialization

- **Alternative:** Seed with first value or SMA.
- **Trade-off:** Slightly more complex math (`1/(1-decay)` scaling).
- **Rationale:** Provides valid values from the very first bar, eliminating the "warmup period" artifact common in other libraries.

### Choice: Double Precision State

- **Alternative:** Decimal.
- **Trade-off:** Precision vs Speed.
- **Rationale:** Double is significantly faster and provides sufficient precision for financial time series (15-17 digits).

## References

- Mulloy, Patrick G. "Smoothing Data With Faster Moving Averages." Technical Analysis of Stocks & Commodities, Jan. 1994.
