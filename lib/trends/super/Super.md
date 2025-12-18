# SuperTrend: SuperTrend Indicator

## What It Does

The SuperTrend indicator is a popular trend-following tool that combines price action with volatility. It plots a line above or below the price to indicate the current trend direction and potential stop-loss levels. When the price is above the SuperTrend line, the trend is bullish (green). When the price is below the line, the trend is bearish (red).

## Historical Context

Created by Olivier Seban, the SuperTrend indicator was designed to be a simple, visual system for identifying trends and managing trailing stops. It gained massive popularity in retail trading communities due to its clear "buy/sell" visual nature and its ability to filter out minor fluctuations while keeping traders in major moves.

## How It Works

### The Core Idea

SuperTrend uses the Average True Range (ATR) to measure market volatility. It then calculates a "Basic Upper Band" and "Basic Lower Band" based on the average price (HL2) plus/minus a multiple of the ATR.

The "SuperTrend" line itself is a stateful logic that switches between the Upper and Lower bands based on price action:

- If price closes above the Upper Band, the trend flips to Bullish, and the line becomes the Lower Band.
- If price closes below the Lower Band, the trend flips to Bearish, and the line becomes the Upper Band.

### Mathematical Foundation

1. **ATR Calculation:** Calculate the Average True Range for period $N$.
2. **Basic Bands:**
   $$ Upper_{basic} = \frac{High + Low}{2} + (Multiplier \times ATR) $$
   $$ Lower_{basic} = \frac{High + Low}{2} - (Multiplier \times ATR) $$
3. **Final Bands (Trailing Logic):**
   - $Upper_{final}$: If current $Upper_{basic} < prev Upper_{final}$ or $prev Close > prev Upper_{final}$, then $Upper_{basic}$, else $prev Upper_{final}$.
   - $Lower_{final}$: If current $Lower_{basic} > prev Lower_{final}$ or $prev Close < prev Lower_{final}$, then $Lower_{basic}$, else $prev Lower_{final}$.
4. **SuperTrend Logic:**
   - If Trend is Bullish: $SuperTrend = Lower_{final}$
   - If Trend is Bearish: $SuperTrend = Upper_{final}$

### Implementation Details

Our implementation maintains the state of the trend and the trailing bands.

- **Complexity:** O(1) per update.
- **State:** Requires tracking the previous trend direction, previous final bands, and previous close.

## Configuration

| Parameter | Default | Purpose | Adjustment Guidelines |
|-----------|---------|---------|----------------------|
| Period | 10 | ATR Lookback | 10 is standard. Shorter = more volatile ATR. |
| Multiplier | 3.0 | Band width | 3.0 is standard. Lower (e.g., 2.0) = tighter stops, more signals. Higher (e.g., 4.0) = wider stops, fewer signals. |

## Performance Profile

| Operation | Complexity | Description |
|-----------|------------|-------------------|
| Streaming update | O(1) | ATR update + logic checks |
| Bar correction | O(1) | Efficient state rollback |
| Batch processing | O(N) | Single pass through data |
| Memory footprint | O(period) | RingBuffer for ATR calculation |

## Interpretation

### Trading Signals

#### Trend Reversal

- **Buy Signal:** Price closes above the SuperTrend line (Trend flips from Bearish to Bullish).
- **Sell Signal:** Price closes below the SuperTrend line (Trend flips from Bullish to Bearish).

#### Trailing Stop

- The SuperTrend line itself serves as an excellent trailing stop-loss level. In an uptrend, place stops just below the green line. In a downtrend, place stops just above the red line.

### When It Works Best

- **Trending Markets:** SuperTrend excels at capturing large moves and keeping you in the trade until the trend actually reverses.

### When It Struggles

- **Sideways Markets:** In choppy, range-bound markets, price will frequently cross the line, causing "whipsaws" (rapid buy/sell signals that result in losses).

## Architecture Notes

This implementation makes specific trade-offs:

### Choice: ATR Smoothing

- **Implementation:** Uses RMA (Wilder's Smoothing) for ATR calculation.
- **Rationale:** Standard definition of ATR uses RMA. Using SMA or EMA would deviate from the standard SuperTrend formula found on most platforms.

## References

- Seban, Olivier. "Tout le monde mérite d'être riche" (Everyone Deserves to Be Rich).

## C# Usage

### Streaming Updates (Single Instance)

```csharp
using QuanTAlib;

var super = new SuperTrend(period: 10, multiplier: 3.0);

// Process each new bar
TBar bar = new TBar(time, open, high, low, close, volume);
TValue result = super.Update(bar);

Console.WriteLine($"SuperTrend: {result.Value:F2}");
Console.WriteLine($"Trend: {(result.IsBullish ? "Bullish" : "Bearish")}");

// Check if buffer is full
if (super.IsHot)
{
    // Indicator is fully initialized
}
```

### Batch Processing (Historical Data)

```csharp
// TBarSeries API
TBarSeries bars = ...;
TSeries superValues = SuperTrend.Batch(bars, period: 10, multiplier: 3.0);
```

### Bar Correction (isNew Parameter)

```csharp
var super = new SuperTrend(10, 3.0);

// New bar
super.Update(bar, isNew: true);

// Intra-bar update
super.Update(updatedBar, isNew: false); // Replaces last calculation
