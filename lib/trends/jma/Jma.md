# JMA: Jurik Moving Average

## What It Does

The Jurik Moving Average (JMA) is widely considered one of the best adaptive moving averages in the world. It is designed to provide superior smoothing with minimal lag, dynamically adjusting its response based on market volatility. Unlike standard moving averages that struggle to balance smoothness and responsiveness, JMA excels at both by using a sophisticated multi-stage algorithm that analyzes the volatility distribution of the market.

## Historical Context

Developed by Mark Jurik of Jurik Research, the JMA was originally a proprietary, closed-source indicator sold as a premium add-on for trading platforms. Its legendary status in the algorithmic trading community comes from its ability to filter out noise without introducing the significant delay common in other filters. While the original code remains proprietary, the version implemented here is a high-fidelity port of the widely accepted reverse-engineered algorithm used in professional trading circles.

## How It Works

### The Core Idea

JMA doesn't just look at price; it looks at the *volatility* of the price.

1. It maintains a distribution (histogram) of recent volatility.
2. It calculates a "reference volatility" by trimming outliers from this distribution.
3. It compares the current local volatility to this reference.
4. If the market is calm, it smooths more. If the market is volatile (breaking out), it reacts faster.

### Mathematical Foundation

The algorithm is complex and involves several stages:

1. **Adaptive Envelope:** Tracks the price with dynamic upper and lower bands.
2. **Volatility Analysis:** Computes a 10-bar SMA of the distance between price and the envelope.
3. **Trimmed Mean:** Maintains a 128-sample buffer of volatility, sorts it, and averages the middle 50% to find a stable "reference" volatility.
4. **Dynamic Exponent:** Calculates a smoothing factor based on the ratio of current volatility to reference volatility.
5. **IIR Filter:** Applies a dual-pole Infinite Impulse Response filter using the dynamic exponent to produce the final value.

### Implementation Details

Our implementation is optimized for performance:

- **Trimmed Mean:** Uses an efficient sorting algorithm on the volatility buffer.
- **Power Calculation:** Uses `Math.Exp` and `Math.Log` optimizations for the dynamic exponent.
- **Complexity:** O(N log N) for the sorting step (where N=128), which is effectively constant time O(1) relative to the data series length.

## Configuration

| Parameter | Default | Purpose | Adjustment Guidelines |
|-----------|---------|---------|----------------------|
| Period | 10 | Base smoothing length | 10 is standard. Shorter = faster, Longer = smoother. |
| Phase | 0 | Lag/Overshoot balance | -100 to +100. Negative = Lower lag, more overshoot. Positive = Smoother, more lag. |
| Power | 0.45 | Sensitivity curve | Legacy parameter. Controls the non-linear response curve. |

**Configuration note:** The `Phase` parameter is unique to JMA. A phase of 100 makes it act like a TEMA (very fast, some overshoot), while -100 makes it act like a Gaussian filter (no overshoot, more lag). 0 is the optimal balance.

## C# Usage

### Streaming Updates (Single Instance)

```csharp
using QuanTAlib;

var jma = new Jma(period: 10, phase: 0);

// Process each new bar
TValue result = jma.Update(new TValue(timestamp, closePrice));
Console.WriteLine($"JMA: {result.Value:F2}");

// Check if buffer is full (JMA needs a long warmup)
if (jma.IsHot)
{
    // Indicator is fully initialized
}
```

### Batch Processing (Historical Data)

```csharp
// TSeries API
TSeries prices = ...;
TSeries jmaValues = Jma.Batch(prices, period: 10, phase: 0);

// Span API (High Performance)
double[] prices = new double[1000];
double[] output = new double[1000];
Jma.Batch(prices.AsSpan(), output.AsSpan(), period: 10, phase: 0);
```

### Bar Correction (isNew Parameter)

```csharp
var jma = new Jma(10);

// New bar
jma.Update(new TValue(time, 100), isNew: true);

// Intra-bar update
jma.Update(new TValue(time, 101), isNew: false); // Replaces 100 with 101
```

## Performance Profile

| Operation | Complexity | Description |
|-----------|------------|-------------------|
| Streaming update | O(1)* | Constant time (sorting fixed 128-item buffer) |
| Bar correction | O(1) | Efficient state rollback |
| Batch processing | O(N) | Single pass through data |
| Memory footprint | O(1) | Fixed size buffers (approx 150 doubles) |

*Note: While technically O(1) per bar, the constant factor is higher than SMA/EMA due to the sorting of the volatility buffer.*

## Interpretation

### Trading Signals

#### Trend Identification

- **Clean Trend:** JMA is famous for drawing a "smooth line through the noise." If JMA is rising, the trend is up.
- **Early Reversal:** Because of its low lag, JMA often turns before other moving averages, giving an early warning of trend changes.

#### Crossovers

- **Price Crossover:** Price crossing JMA is a high-quality signal because JMA hugs the price closely without getting chopped up by noise.
- **JMA Ribbon:** Using multiple JMAs (e.g., JMA(10) and JMA(20)) creates a ribbon that expands in trends and contracts in consolidation.

### When It Works Best

- **All Markets:** JMA is designed to be a "universal" filter. It adapts to both trending and ranging markets.
- **Volatile Breakouts:** It excels at catching breakouts because it detects the surge in volatility and reduces its smoothing immediately.

### When It Struggles

- **Warmup:** JMA requires a significant amount of data (approx 60-100 bars) to stabilize its volatility distribution. It is not suitable for very short data series.

## Architecture Notes

This implementation makes specific trade-offs:

### Choice: Fixed 128-sample Volatility Buffer

- **Alternative:** Variable buffer based on period.
- **Trade-off:** Memory vs Adaptivity.
- **Rationale:** The original algorithm specifies a fixed window for volatility analysis to ensure consistent statistical significance of the trimmed mean.

### Choice: Trimmed Mean

- **Alternative:** Simple Mean or Median.
- **Trade-off:** Computation speed vs Robustness.
- **Rationale:** Trimmed mean (removing top/bottom 25%) is robust against outliers (price spikes) that would otherwise distort the volatility baseline.

## References

- Jurik, Mark. "Jurik Research." [http://www.jurikres.com/](http://www.jurikres.com/)
- "JMA - Jurik Moving Average." Technical Analysis of Stocks & Commodities.
