# RMA: Running Moving Average

## What It Does

The Running Moving Average (RMA), also known as the Smoothed Moving Average (SMMA) or Modified Moving Average (MMA), is an exponential moving average with a specific smoothing factor of $1/N$. It is most famous for being the smoothing method used by J. Welles Wilder Jr. in his core indicators, including the RSI (Relative Strength Index), ATR (Average True Range), and ADX (Average Directional Index).

## Historical Context

Introduced by J. Welles Wilder Jr. in his seminal 1978 book *"New Concepts in Technical Trading Systems"*, the RMA was designed to be easily calculated by hand. Wilder needed a method that incorporated all historical data (unlike a simple moving average that drops old data) but was simpler to update than a standard EMA.

## How It Works

### The Core Idea

The RMA is essentially an Exponential Moving Average (EMA) but with a much slower reaction time for the same period $N$. While a standard EMA uses a smoothing factor of $\alpha = 2/(N+1)$, the RMA uses $\alpha = 1/N$.

This means an RMA of period 14 is roughly equivalent to an EMA of period 27 ($2N-1$). This slower response makes it exceptionally stable and suitable for smoothing highly volatile data like True Range.

### Mathematical Foundation

The formula is recursive:

$$ RMA_{today} = \frac{(RMA_{yesterday} \times (N - 1)) + Price_{today}}{N} $$

Which is mathematically equivalent to an EMA with $\alpha = 1/N$:

$$ RMA_{today} = \alpha \times Price_{today} + (1 - \alpha) \times RMA_{yesterday} $$

Where:

- $N$ = Period length
- $\alpha = 1/N$

### Implementation Details

Our implementation uses the recursive formula for O(1) updates.

- **Initialization:** The first value is typically a Simple Moving Average (SMA) of the first $N$ data points, as defined by Wilder.
- **Precision:** We use double-precision floating point to minimize error accumulation over long series.

## Configuration

| Parameter | Default | Purpose | Adjustment Guidelines |
|-----------|---------|---------|----------------------|
| Period | 14 | Lookback window | Standard is 14 (Wilder's default). |

## Performance Profile

| Operation | Complexity | Description |
|-----------|------------|-------------------|
| Streaming update | O(1) | Simple scalar math |
| Bar correction | O(1) | Efficient state rollback |
| Batch processing | O(N) | Single pass through data |
| Memory footprint | O(1) | Minimal state (previous value only) |

## Interpretation

### Trading Signals

#### Trend Filter

- **Direction:** Because RMA is slower than EMA, it acts as an excellent long-term trend filter.
- **Support/Resistance:** In strong trends, price often respects the RMA line as dynamic support/resistance.

### When It Works Best

- **Smoothing Volatility:** RMA is the gold standard for smoothing volatile sub-indicators (like True Range to get ATR) because it doesn't react jerkily to single spikes.

### When It Struggles

- **Fast Reversals:** Due to its lag (approx $2N-1$ EMA equivalent), it is too slow for catching rapid market turns.

## Architecture Notes

This implementation makes specific trade-offs:

### Choice: Wilder's Initialization

- **Implementation:** The first value is the SMA of the first $N$ bars.
- **Rationale:** Strict adherence to Wilder's definition ensures values match standard platforms (TradingView, etc.) exactly.

## References

- Wilder, J. Welles Jr. "New Concepts in Technical Trading Systems." Trend Research, 1978.

## C# Usage

### Streaming Updates (Single Instance)

```csharp
using QuanTAlib;

var rma = new Rma(period: 14);

// Process each new bar
TValue result = rma.Update(new TValue(timestamp, closePrice));
Console.WriteLine($"RMA: {result.Value:F2}");

// Check if buffer is full
if (rma.IsHot)
{
    // Indicator is fully initialized
}
```

### Batch Processing (Historical Data)

```csharp
// TSeries API
TSeries prices = ...;
TSeries rmaValues = Rma.Batch(prices, period: 14);

// Span API (High Performance)
double[] prices = new double[1000];
double[] output = new double[1000];
Rma.Calculate(prices.AsSpan(), output.AsSpan(), period: 14);
```

### Bar Correction (isNew Parameter)

```csharp
var rma = new Rma(14);

// New bar
rma.Update(new TValue(time, 100), isNew: true);

// Intra-bar update
rma.Update(new TValue(time, 101), isNew: false); // Replaces 100 with 101
