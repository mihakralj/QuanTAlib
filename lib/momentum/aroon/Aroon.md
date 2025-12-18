# Aroon

A trend-following indicator that measures the *time* elapsed since the last highest high and lowest low. Unlike price-based oscillators, Aroon focuses on the temporal freshness of price extremes to gauge trend strength.

## What It Does

The Aroon indicator answers a simple question: "How long has it been since we saw a new high or low?"

It consists of two lines (Up and Down) and a derived Oscillator.

- **Aroon Up**: Quantifies how recent the last high was.
- **Aroon Down**: Quantifies how recent the last low was.
- **Aroon Oscillator**: The net difference, showing the dominant trend.

When a new high occurs today, Aroon Up hits 100. If no new high appears for the entire period, it drops to 0. This creates a clear metric for trend "staleness."

## Historical Context

Developed by Tushar Chande in 1995, the name "Aroon" is derived from the Sanskrit word for "Dawn's Early Light." Chande designed it to spot the beginning of a new trend (the dawn) rather than just confirming an existing one. While moving averages lag significantly, Aroon attempts to signal the moment price behavior shifts from consolidation to trending.

## How It Works

The calculation is purely time-based, normalized to a 0-100 scale.

### The Math

$$ \text{Aroon Up} = \frac{\text{Period} - \text{Days Since High}}{\text{Period}} \times 100 $$

$$ \text{Aroon Down} = \frac{\text{Period} - \text{Days Since Low}}{\text{Period}} \times 100 $$

$$ \text{Oscillator} = \text{Aroon Up} - \text{Aroon Down} $$

### The Logic

1. **Track Extremes**: We maintain a sliding window of the last $N$ bars.
2. **Find Distance**: We locate the index of the highest high and lowest low within that window.
3. **Normalize**:
    - If the high was today, `Days Since High` is 0, and Aroon Up is 100.
    - If the high was $N$ days ago, Aroon Up is 0.

## Configuration

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `period` | `int` | 14 | The lookback window for finding highs and lows. |

## Performance Profile

The implementation is optimized for minimal memory footprint, though computational complexity scales linearly with the period.

- **Complexity**: $O(P)$ per update, where $P$ is the period. The algorithm must scan the buffer to find the min/max indices.
- **Memory**: $O(P)$. It uses two circular buffers (`RingBuffer`) to store Highs and Lows.
- **Allocations**: Zero heap allocations during the `Update` cycle.

| Operation | Time Complexity | Space Complexity |
|-----------|-----------------|------------------|
| Update    | $O(P)$          | $O(P)$           |
| Batch     | $O(N \cdot P)$  | $O(N)$           |

*Note: For very large periods (e.g., >1000), the linear scan may become measurable, but for standard technical analysis periods (14-50), it is negligible.*

## Interpretation

Aroon is interpreted through specific thresholds and crossovers.

### 1. Trend Strength (The 70/30 Rule)

- **Strong Uptrend**: Aroon Up > 70.
- **Strong Downtrend**: Aroon Down > 70.
- **Consolidation**: Both lines < 50.

### 2. The Crossover (Trend Change)

- **Bullish**: Aroon Up crosses above Aroon Down.
- **Bearish**: Aroon Down crosses above Aroon Up.

### 3. The Oscillator

- **Positive**: Uptrend bias.
- **Negative**: Downtrend bias.
- **Zero Line Cross**: Confirms the trend reversal signaled by the Up/Down crossover.

## Architecture Notes

The `Aroon` class is a self-contained indicator that manages its own history buffers.

- **Data Requirements**: Requires `High` and `Low` prices. If updated with a single `TValue` (Close), it assumes High=Low=Close, which degrades the indicator's utility to a simple "time since highest close" metric.
- **Buffer Sizing**: The internal buffer size is `Period + 1` to correctly handle the "days since" calculation inclusive of the 0th day.
- **Properties**: The class exposes `Up`, `Down`, and `Last` (Oscillator) as separate `TValue` properties, allowing access to all three components from a single instance.

## References

- Chande, Tushar. *Beyond Technical Analysis: How to Develop and Implement a Winning Trading System*. Wiley, 1995.
- Investopedia: [Aroon Indicator](https://www.investopedia.com/terms/a/aroon.asp)

## C# Usage

```csharp
using QuanTAlib;

// 1. Initialize
var aroon = new Aroon(period: 25);

// 2. Process a Bar
var bar = new TBar(DateTime.UtcNow, open: 100, high: 105, low: 95, close: 102, volume: 1000);
var result = aroon.Update(bar);

// 3. Access Components
Console.WriteLine($"Oscillator: {result.Value:F2}"); // Main output
Console.WriteLine($"Aroon Up:   {aroon.Up.Value:F2}");
Console.WriteLine($"Aroon Down: {aroon.Down.Value:F2}");

// 4. Batch Calculation
var series = new TBarSeries();
// ... populate series ...
var aroonSeries = Aroon.Batch(series, period: 14);
