# KAMA: Kaufman's Adaptive Moving Average

## What It Does

Kaufman's Adaptive Moving Average (KAMA) is an intelligent trend-following indicator that automatically adjusts its sensitivity based on market noise. When the market is trending smoothly, KAMA tightens its tracking to capture the move. When the market becomes choppy or sideways, KAMA relaxes its sensitivity to filter out the noise and avoid false signals.

## Historical Context

Developed by Perry Kaufman and introduced in his 1995 book "Smarter Trading," KAMA was designed to solve the "noise vs. lag" dilemma. Kaufman recognized that a static moving average is always a compromise: too slow for trends or too fast for noise. KAMA solves this by measuring the "Efficiency Ratio" of the price movement and adjusting its smoothing constant in real-time.

## How It Works

### The Core Idea

KAMA asks a simple question: "How efficient is the price movement?"

- If price moves from A to B in a straight line, it is highly efficient (Efficiency Ratio ≈ 1). KAMA speeds up.
- If price moves from A to B but zig-zags wildly along the way, it is inefficient (Efficiency Ratio ≈ 0). KAMA slows down.

### Mathematical Foundation

1. **Efficiency Ratio (ER):**
   $$ER = \frac{|\text{Change}|}{\text{Volatility}}$$
   - Change = Price today - Price N days ago (Net direction)
   - Volatility = Sum of absolute daily changes over N days (Total path length)

2. **Smoothing Constant (SC):**
   KAMA scales the ER to fit between a "Fast" EMA constant and a "Slow" EMA constant.
   $$SC = \left(ER \times (\text{fast} - \text{slow}) + \text{slow}\right)^2$$
   The squaring operation ($^2$) is crucial—it suppresses the response to noise, making KAMA remain flat in choppy markets until a genuine trend emerges.

3. **Update Formula:**
   $$KAMA_{today} = KAMA_{yesterday} + SC \times (Price_{today} - KAMA_{yesterday})$$

### Implementation Details

Our implementation is fully optimized for O(1) updates.

- **Complexity:** O(1) per update.
- **Efficiency:** We use a running sum algorithm for the volatility calculation, avoiding the need to re-sum the window every bar.
- **Precision:** Double-precision floating point ensures accuracy over long datasets.

## Configuration

| Parameter | Default | Purpose | Adjustment Guidelines |
|-----------|---------|---------|----------------------|
| Period | 10 | ER Lookback window | 10 is standard. Longer = more stable ER measurement. |
| Fast Period | 2 | Max speed (Trending) | 2 is standard. Lower = faster reaction to strong trends. |
| Slow Period | 30 | Min speed (Choppy) | 30 is standard. Higher = better noise filtering in ranges. |

**Configuration note:** The default settings (10, 2, 30) are widely used and robust. Adjusting the Slow Period to 80 or 100 can create an extremely stable filter for long-term trend following.

## C# Usage

### Streaming Updates (Single Instance)

```csharp
using QuanTAlib;

var kama = new Kama(period: 10, fastPeriod: 2, slowPeriod: 30);

// Process each new bar
TValue result = kama.Update(new TValue(timestamp, closePrice));
Console.WriteLine($"KAMA: {result.Value:F2}");

// Check if buffer is full
if (kama.IsHot)
{
    // Indicator is fully initialized
}
```

### Batch Processing (Historical Data)

```csharp
// TSeries API
TSeries prices = ...;
TSeries kamaValues = Kama.Batch(prices, period: 10);

// Span API (High Performance)
double[] prices = new double[1000];
double[] output = new double[1000];
Kama.Batch(prices.AsSpan(), output.AsSpan(), period: 10, fastPeriod: 2, slowPeriod: 30);
```

### Bar Correction (isNew Parameter)

```csharp
var kama = new Kama(10);

// New bar
kama.Update(new TValue(time, 100), isNew: true);

// Intra-bar update
kama.Update(new TValue(time, 101), isNew: false); // Replaces 100 with 101
```

## Performance Profile

| Operation | Complexity | Description |
|-----------|------------|-------------------|
| Streaming update | O(1) | Running sum for volatility + scalar math |
| Bar correction | O(1) | Efficient state rollback |
| Batch processing | O(N) | Single pass through data |
| Memory footprint | O(period) | RingBuffer for volatility calculation |

## Interpretation

### Trading Signals

#### Trend Identification

- **Flat Line:** One of KAMA's best features. When KAMA is flat, it indicates a noise-dominated market. Stay out or trade mean reversion.
- **Steep Slope:** When KAMA angles up or down sharply, it indicates a high-efficiency trend. Enter in the direction of the slope.

#### Crossovers

- **Price Crossover:** Price crossing KAMA is a reliable signal because KAMA tends to be far away from price during noise and close to price during trends.
- **KAMA Cross:** Crossing a short-term KAMA(10) with a long-term KAMA(100) is a powerful trend-following system.

### When It Works Best

- **Trend-Following:** KAMA is arguably the best moving average for trend-following systems because it minimizes "whipsaws" in sideways markets better than almost any other MA.

### When It Struggles

- **Sudden Shocks:** Because KAMA relies on the Efficiency Ratio, a sudden V-shaped reversal might initially look like "noise" (low efficiency) before KAMA realizes it's a new trend. It can lag slightly at the very start of a violent reversal.

## Architecture Notes

This implementation makes specific trade-offs:

### Choice: Running Sum for Volatility

- **Alternative:** Re-summing absolute differences every bar.
- **Trade-off:** State complexity vs CPU cycles.
- **Rationale:** O(1) performance is critical. Maintaining a running sum of volatility allows the indicator to scale to large periods without performance penalty.

## References

- Kaufman, Perry J. "Smarter Trading: Improving Performance in Changing Markets." McGraw-Hill, 1995.
- Kaufman, Perry J. "Trading Systems and Methods." Wiley, 2013.
