# ATR: Average True Range

## What It Does

The Average True Range (ATR) is a technical analysis indicator that measures market volatility by decomposing the entire range of an asset price for that period. Unlike other indicators that measure trend direction, ATR measures the *degree* of price movement. High ATR values indicate high volatility (large price swings), while low ATR values indicate low volatility (consolidation).

## Historical Context

Introduced by J. Welles Wilder Jr. in his 1978 book *"New Concepts in Technical Trading Systems"*, ATR was originally designed for commodities markets, which are often more volatile than stocks. Wilder realized that looking at the simple High-Low range was insufficient because it ignored gaps between the previous close and the current open. He introduced the concept of "True Range" to capture the full extent of market activity.

## How It Works

### The Core Idea

ATR answers the question: "How much does this asset typically move in a single bar?"
It does this by first calculating the "True Range" (TR) for each bar, which accounts for gaps, and then smoothing these TR values using a Running Moving Average (RMA).

### Mathematical Foundation

1. **True Range (TR):**
   The True Range is the greatest of the following three values:

   - Current High - Current Low
   - |Current High - Previous Close|
   - |Current Low - Previous Close|

   $$ TR = \max(High - Low, |High - Close_{prev}|, |Low - Close_{prev}|) $$

2. **Average True Range (ATR):**
   The ATR is an RMA (Wilder's Smoothing) of the True Range values over period $N$.
   $$ ATR_{today} = \frac{(ATR_{yesterday} \times (N-1)) + TR_{today}}{N} $$

### Implementation Details

Our implementation uses the `Rma` indicator internally to smooth the calculated True Range.

- **Complexity:** O(1) per update.
- **Initialization:** For the very first bar, TR is simply High - Low (since there is no previous close).

## Configuration

| Parameter | Default | Purpose | Adjustment Guidelines |
|-----------|---------|---------|----------------------|
| Period | 14 | Lookback window | Standard is 14. Shorter (e.g., 7) = more sensitive to recent volatility spikes. Longer (e.g., 21) = smoother measure of volatility. |

## Performance Profile

| Operation | Complexity | Description |
|-----------|------------|-------------------|
| Streaming update | O(1) | TR calculation + RMA update |
| Bar correction | O(1) | Efficient state rollback |
| Batch processing | O(N) | Single pass through data |
| Memory footprint | O(1) | Minimal state (previous bar + RMA state) |

## Interpretation

### Trading Signals

#### Volatility Measurement

- **High ATR:** Indicates a volatile market. Stops should be wider to avoid noise.
- **Low ATR:** Indicates a quiet market. A breakout from a low-ATR consolidation is often explosive.

#### Stop Loss Placement

- **Chandelier Exit:** Many traders place trailing stops at $Close - (Multiplier \times ATR)$.
- **Position Sizing:** ATR is crucial for volatility-based position sizing (e.g., the "Turtle Trading" system). If ATR is high, trade smaller size; if ATR is low, trade larger size.

### When It Works Best

- **Risk Management:** ATR is arguably the most important indicator for risk management, helping traders normalize risk across different assets.

### When It Struggles

- **Direction:** ATR tells you nothing about direction. A crashing market and a rocketing market can both have high ATR.

## Architecture Notes

This implementation makes specific trade-offs:

### Choice: RMA Smoothing

- **Implementation:** Uses `Rma` (Wilder's Smoothing).
- **Rationale:** Strict adherence to Wilder's original definition. Some platforms offer SMA-smoothed ATR, but that is technically a different indicator.

## References

- Wilder, J. Welles Jr. "New Concepts in Technical Trading Systems." Trend Research, 1978.

## C# Usage

### Streaming Updates (Single Instance)

```csharp
using QuanTAlib;

var atr = new Atr(period: 14);

// Process each new bar
TBar bar = new TBar(time, open, high, low, close, volume);
TValue result = atr.Update(bar);

Console.WriteLine($"ATR: {result.Value:F2}");

// Check if buffer is full
if (atr.IsHot)
{
    // Indicator is fully initialized
}
```

### Batch Processing (Historical Data)

```csharp
// TBarSeries API
TBarSeries bars = ...;
TSeries atrValues = Atr.Batch(bars, period: 14);
```

### Bar Correction (isNew Parameter)

```csharp
var atr = new Atr(14);

// New bar
atr.Update(bar, isNew: true);

// Intra-bar update
atr.Update(updatedBar, isNew: false); // Replaces last calculation
