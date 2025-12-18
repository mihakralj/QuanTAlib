# VIDYA: Variable Index Dynamic Average

## What It Does

The Variable Index Dynamic Average (VIDYA) is an adaptive moving average that automatically adjusts its smoothing speed based on market volatility. When the market is volatile and trending, VIDYA speeds up to capture the move. When the market is quiet or consolidating, VIDYA slows down to filter out noise. It uses the Chande Momentum Oscillator (CMO) as its volatility index.

## Historical Context

Developed by Tushar Chande and introduced in his 1994 book *"The New Technical Trader"*, VIDYA was one of the first "intelligent" moving averages. Chande recognized that a fixed-period moving average is always a compromise. VIDYA solves this by dynamically varying its effective period bar-by-bar.

## How It Works

### The Core Idea

VIDYA is essentially an Exponential Moving Average (EMA) where the smoothing factor ($\alpha$) is not constant. Instead, $\alpha$ is scaled by a "Volatility Index" (VI).

- **High Volatility:** VI is high $\rightarrow$ $\alpha$ increases $\rightarrow$ VIDYA reacts faster.
- **Low Volatility:** VI is low $\rightarrow$ $\alpha$ decreases $\rightarrow$ VIDYA reacts slower.

### Mathematical Foundation

1. **Volatility Index (VI):**
   We use the absolute value of the Chande Momentum Oscillator (CMO) over period $N$.
   $$ VI = |CMO(N)| = \left| \frac{\sum Up - \sum Down}{\sum Up + \sum Down} \right| $$
   $VI$ ranges from 0 (no trend) to 1 (strong trend).

2. **Smoothing Factor ($\alpha$):**
   $$ \alpha_{base} = \frac{2}{N+1} $$
   $$ \alpha_{dynamic} = \alpha_{base} \times VI $$

3. **Update Formula:**
   $$ VIDYA_{today} = \alpha_{dynamic} \times Price_{today} + (1 - \alpha_{dynamic}) \times VIDYA_{yesterday} $$

### Implementation Details

Our implementation calculates CMO and VIDYA in a single pass.

- **Complexity:** O(1) per update (CMO is O(1) via running sums).
- **Efficiency:** Uses the same optimized structure as our standard EMA.

## Configuration

| Parameter | Default | Purpose | Adjustment Guidelines |
|-----------|---------|---------|----------------------|
| Period | 14 | Lookback window | Standard lookback for both CMO and the base EMA. |

## Performance Profile

| Operation | Complexity | Description |
|-----------|------------|-------------------|
| Streaming update | O(1) | CMO update + EMA update |
| Bar correction | O(1) | Efficient state rollback |
| Batch processing | O(N) | Single pass through data |
| Memory footprint | O(period) | RingBuffer for CMO calculation |

## Interpretation

### Trading Signals

#### Trend Following

- **Support/Resistance:** VIDYA is excellent at identifying dynamic support and resistance levels because it flattens out during consolidations (providing a clear "shelf" of support) and slopes steeply during trends.

#### Crossovers

- **Price Crossover:** Price crossing VIDYA is a standard trend entry signal. Because VIDYA adapts to volatility, these signals are often more reliable than SMA crossovers in choppy markets.

### When It Works Best

- **Breakouts:** VIDYA excels at catching breakouts from low-volatility consolidations because its effective period shortens (speeds up) as soon as volatility expands.

### When It Struggles

- **Grinding Trends:** In a slow, low-volatility grind upwards, VIDYA might lag more than a standard EMA because the low volatility keeps the smoothing factor small.

## Architecture Notes

This implementation makes specific trade-offs:

### Choice: CMO as Volatility Index

- **Implementation:** Uses Chande Momentum Oscillator.
- **Rationale:** This is the original definition by Chande. Other variants (like using Efficiency Ratio) exist but are technically different indicators (e.g., KAMA).

## References

- Chande, Tushar. "The New Technical Trader." Wiley, 1994.
- Chande, Tushar. "Adapting Moving Averages To Market Volatility." *Technical Analysis of Stocks & Commodities*, Mar 1992.

## C# Usage

### Streaming Updates (Single Instance)

```csharp
using QuanTAlib;

var vidya = new Vidya(period: 14);

// Process each new bar
TValue result = vidya.Update(new TValue(timestamp, closePrice));
Console.WriteLine($"VIDYA: {result.Value:F2}");

// Check if buffer is full
if (vidya.IsHot)
{
    // Indicator is fully initialized
}
```

### Batch Processing (Historical Data)

```csharp
// TSeries API
TSeries prices = ...;
TSeries vidyaValues = Vidya.Batch(prices, period: 14);

// Span API (High Performance)
double[] prices = new double[1000];
double[] output = new double[1000];
Vidya.Calculate(prices.AsSpan(), output.AsSpan(), period: 14);
```

### Bar Correction (isNew Parameter)

```csharp
var vidya = new Vidya(14);

// New bar
vidya.Update(new TValue(time, 100), isNew: true);

// Intra-bar update
vidya.Update(new TValue(time, 101), isNew: false); // Replaces 100 with 101
