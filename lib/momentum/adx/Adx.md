# ADX: Average Directional Index

## What It Does

The Average Directional Index (ADX) quantifies trend strength without regard to trend direction. It answers the critical question: "Is the market trending?" rather than "Which way is it going?" By isolating strength from direction, ADX allows traders to filter their strategies—deploying trend-following logic only when a trend is statistically present, and switching to mean-reversion when the market is ranging.

## Historical Context

J. Welles Wilder Jr. introduced the ADX in his seminal 1978 book, *New Concepts in Technical Trading Systems*. Wilder, a mechanical engineer turned real estate developer and trader, designed the ADX (along with RSI, ATR, and Parabolic SAR) to bring mathematical rigor to the then-subjective field of technical analysis. His goal was to create a system that could objectively distinguish between trending and non-trending markets.

## How It Works

### The Core Idea

ADX is built on the concept of "Directional Movement" (DM).

1. **Expansion:** It compares today's high/low with yesterday's high/low to see if the range has expanded up (+DM) or down (-DM).
2. **Normalization:** These expansions are normalized by the True Range (volatility) to create Directional Indicators (+DI and -DI).
3. **Difference:** The difference between +DI and -DI is calculated to find the "Directional Index" (DX).
4. **Smoothing:** The DX is smoothed (typically over 14 periods) to produce the ADX.

### Mathematical Foundation

1. **Directional Movement (DM):**
   $$+DM = \text{if } (H_t - H_{t-1}) > (L_{t-1} - L_t) \text{ and } (H_t - H_{t-1}) > 0 \text{ then } H_t - H_{t-1} \text{ else } 0$$
   $$-DM = \text{if } (L_{t-1} - L_t) > (H_t - H_{t-1}) \text{ and } (L_{t-1} - L_t) > 0 \text{ then } L_{t-1} - L_t \text{ else } 0$$

2. **Directional Indicators (DI):**
   $$+DI = 100 \times \frac{RMA(+DM, n)}{ATR(n)}$$
   $$-DI = 100 \times \frac{RMA(-DM, n)}{ATR(n)}$$

3. **Directional Index (DX):**
   $$DX = 100 \times \frac{|+DI - -DI|}{+DI + -DI}$$

4. **Average Directional Index (ADX):**
   $$ADX = RMA(DX, n)$$

Where $RMA$ is Wilder's Moving Average (an EMA with $\alpha = 1/n$).

### Implementation Details

Our implementation focuses on numerical stability and performance.

- **Zero-Allocation Updates:** The streaming `Update` method uses `stackalloc` for internal state calculations, ensuring zero heap allocations on the hot path.
- **Stabilization:** ADX is a "derivative of a derivative" (smoothed price -> smoothed range -> smoothed ratio -> smoothed result). It requires significant history to stabilize. We implement a proper warmup phase to prevent early erratic values.
- **Precision:** All internal calculations use double-precision floating point to minimize rounding errors in the recursive RMA steps.

## Configuration

| Parameter | Default | Purpose | Adjustment Guidelines |
|-----------|---------|---------|----------------------|
| Period | 14 | Lookback window | Wilder's standard is 14. Lower (7-10) = faster reaction; Higher (20-30) = smoother trend filter. |

**Configuration note:** ADX is notoriously slow to turn. Shortening the period makes it more responsive but increases noise.

## Performance Profile

| Operation | Complexity | Description |
|-----------|------------|-------------------|
| Streaming update | O(1) | Constant time recursive calculation |
| Bar correction | O(1) | Efficient state rollback |
| Batch processing | O(N) | Single pass through data |
| Memory footprint | O(1) | Minimal state (previous High/Low/Close + smoothed values) |

## Interpretation

### Trading Signals

#### Trend Strength

- **ADX < 20:** Weak trend or ranging market. Strategies: Mean reversion, oscillators.
- **ADX > 25:** Trend is emerging. Strategies: Breakout, trend following.
- **ADX > 40:** Strong trend. Strategies: Pullback entries.
- **ADX > 50:** Extremely strong trend. Watch for exhaustion (climax).

#### Trend Direction

- **+DI > -DI:** Bullish dominance.
- **-DI > +DI:** Bearish dominance.
- **Crossover:** +DI crossing -DI is often used as an entry signal, filtered by ADX > 20.

### When It Works Best

- **Trend Filtering:** The primary use case. Use ADX to decide *which* strategy to run. If ADX is rising, trade the trend. If ADX is falling or low, trade the range.

### When It Struggles

- **V-Reversals:** Because of the multiple smoothing layers, ADX lags significantly at sharp market turns. It may still indicate a strong trend when the market has already reversed.

## Architecture Notes

This implementation makes specific trade-offs:

### Choice: Recursive RMA

- **Alternative:** Simple Moving Average (SMA).
- **Trade-off:** History dependence.
- **Rationale:** Wilder specifically defined ADX using his own smoothing method (RMA). Using SMA would yield incorrect values compared to standard platforms.

### Choice: True Range Dependency

- **Alternative:** Simplified range (High - Low).
- **Trade-off:** Complexity.
- **Rationale:** True Range accounts for gaps between bars, which is critical for accurate volatility measurement in 24/7 markets or daily charts with overnight gaps.

## References

- Wilder, J. Welles. "New Concepts in Technical Trading Systems." Trend Research, 1978.
- [Investopedia - Average Directional Index (ADX)](https://www.investopedia.com/terms/a/adx.asp)

## C# Usage

### Streaming Updates (Single Instance)

```csharp
using QuanTAlib;

var adx = new Adx(period: 14);

// Process each new bar
// Note: ADX requires High, Low, and Close prices
TBar bar = new TBar(time, open, high, low, close, volume);
TValue result = adx.Update(bar);

Console.WriteLine($"ADX: {result.Value:F2}");

// Check if buffer is full (ADX needs significant warmup)
if (adx.IsHot)
{
    // Indicator is fully initialized
}
```

### Batch Processing (Historical Data)

```csharp
// TBarSeries API
TBarSeries bars = ...;
TSeries adxValues = Adx.Batch(bars, period: 14);

// Span API (High Performance)
// Requires separate arrays for High, Low, Close
double[] high = ...;
double[] low = ...;
double[] close = ...;
double[] output = new double[high.Length];

Adx.Calculate(high.AsSpan(), low.AsSpan(), close.AsSpan(), output.AsSpan(), period: 14);
```

### Bar Correction (isNew Parameter)

```csharp
var adx = new Adx(14);

// New bar
adx.Update(new TBar(time, o, h, l, c, v), isNew: true);

// Intra-bar update
adx.Update(new TBar(time, o, h, l, c, v), isNew: false); // Replaces last value
