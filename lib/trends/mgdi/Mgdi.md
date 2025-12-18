# MGDI: McGinley Dynamic Indicator

## What It Does

The McGinley Dynamic Indicator (MGDI) is a smoothing mechanism designed to track market prices more effectively than traditional moving averages. Unlike the SMA or EMA, which use fixed time periods, the MGDI automatically adjusts its speed based on the market's velocity. It minimizes "price separation" (the gap between the price and the average) and "price hugs" (whipsaws), providing a more reliable trend line that adapts to changing volatility.

## Historical Context

Invented by John R. McGinley, a Certified Market Technician, the indicator was designed to address the flaws of conventional moving averages—specifically their inability to adjust to the speed of the market. McGinley argued that moving averages should not be relied upon as trading signals themselves but rather as a mechanism to track the market's "steering mechanism."

## How It Works

### The Core Idea

The MGDI incorporates an automatic adjustment factor that speeds up or slows down the indicator based on the ratio of the current price to the indicator's previous value.

- **Uptrends:** When prices rise quickly, the indicator slows down to avoid overreacting to false breakouts.
- **Downtrends:** When prices fall, the indicator speeds up to track the decline closely, reflecting the panic nature of sell-offs.

### Mathematical Foundation

The formula is recursive:

$$ MGDI_{new} = MGDI_{prev} + \frac{Price - MGDI_{prev}}{k \times N \times (\frac{Price}{MGDI_{prev}})^4} $$

Where:

- $N$ = Period (typically 14)
- $k$ = Constant (typically 0.6, representing 60%)
- The term $(\frac{Price}{MGDI_{prev}})^4$ is the accelerator/decelerator.

**Analysis of the Adjustment Factor:**

- If $Price > MGDI$ (Uptrend), the ratio is $>1$. Raised to the 4th power, it becomes large, increasing the denominator. A larger denominator reduces the adjustment step, making the MGDI move **slower**.
- If $Price < MGDI$ (Downtrend), the ratio is $<1$. Raised to the 4th power, it becomes small, decreasing the denominator. A smaller denominator increases the adjustment step, making the MGDI move **faster**.

## Configuration

| Parameter | Default | Purpose | Adjustment Guidelines |
|-----------|---------|---------|----------------------|
| Period | 14 | Base lookback window | Standard is 14. Adjust based on the timeframe (e.g., 10 for short-term, 20+ for long-term). |
| K | 0.6 | Sensitivity constant | 0.6 (60%) is the standard. Lower values make it more sensitive; higher values make it smoother. |

## Performance Profile

| Operation | Complexity | Description |
|-----------|------------|-------------------|
| Streaming update | O(1) | Constant time recursive calculation |
| Batch processing | O(n) | Fast sequential processing |
| Memory footprint | O(1) | Minimal state (previous value only) |

## Interpretation

### Trading Signals

#### Trend Following

- **Support/Resistance:** The MGDI acts as a dynamic support line in uptrends and resistance in downtrends.
- **Price Relation:**
  - Price > MGDI: Bullish bias.
  - Price < MGDI: Bearish bias.

#### Crossovers

- While not primarily a crossover indicator, price crossing the MGDI can signal a trend reversal. However, due to its smoothing nature, these signals are often lagging compared to more aggressive indicators.

### When It Works Best

- **Volatile Markets:** Its ability to adjust speed makes it superior to SMA/EMA in markets with erratic volatility or sudden crashes.

### When It Struggles

- **Range-Bound Markets:** Like most trend-following indicators, it can flatten out and provide little directional insight in sideways markets.

### Architecture Notes

This implementation makes specific trade-offs:

### Choice: Ratio Clamping

- **Implementation:** The price/MGDI ratio is clamped between 0.3 and 3.0.
- **Rationale:** Prevents the denominator from becoming effectively zero (causing explosion) or infinitely large (causing stagnation) in extreme data scenarios.

### Choice: Recursive State

- **Implementation:** Stores only the last MGDI value.
- **Rationale:** The formula is purely recursive, requiring no historical buffer, making it extremely memory efficient.

## References

- [Investopedia: McGinley Dynamic Indicator](https://www.investopedia.com/terms/m/mcginley-dynamic.asp)
- [Stock Indicators for .NET: McGinley Dynamic](https://dotnet.stockindicators.dev/indicators/Dynamic/)

## C# Usage

### Streaming Updates (Single Instance)

```csharp
using QuanTAlib;

var mgdi = new Mgdi(period: 14, k: 0.6);

// Process each new bar
TValue result = mgdi.Update(new TValue(timestamp, closePrice));
Console.WriteLine($"MGDI: {result.Value:F2}");

// Check if buffer is full
if (mgdi.IsHot)
{
    // Indicator is fully initialized
}
```

### Batch Processing (Historical Data)

```csharp
// TSeries API (object-oriented)
TSeries prices = ...;
TSeries mgdiValues = Mgdi.Batch(prices, period: 14, k: 0.6);

// High-performance Span API (zero allocation)
double[] prices = new double[10000];
double[] output = new double[10000];
Mgdi.Calculate(prices.AsSpan(), output.AsSpan(), period: 14, k: 0.6);
```

### Event-Driven Architecture

```csharp
var source = new TSeries();
var mgdi = new Mgdi(source, period: 14);

// Subscribe to MGDI output
mgdi.Pub += (value) => {
    Console.WriteLine($"New MGDI value: {value.Value}");
};

// Feeding source automatically triggers the chain
source.Add(new TValue(DateTime.Now, 105.2));
