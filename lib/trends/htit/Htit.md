# HTIT: Hilbert Transform Instantaneous Trend

## What It Does

The Hilbert Transform Instantaneous Trend (HTIT) is a sophisticated trend-following indicator that uses digital signal processing (DSP) techniques to filter out market cycles and isolate the underlying trend. Unlike traditional moving averages that use fixed periods, HTIT adapts to the dominant cycle period of the market, allowing it to track the "instantaneous" trend with minimal lag while maintaining smoothness.

## Historical Context

Developed by John Ehlers, a pioneer in applying DSP to technical analysis, the HTIT was introduced as part of his research into market cycles. Ehlers argued that financial markets are composed of a trend component and a cycle component. By accurately measuring the cycle using the Hilbert Transform, one can subtract it or filter it out to reveal the true trend, offering a more scientific approach than arbitrary moving averages.

## How It Works

### The Core Idea

The indicator works by decomposing the price data into "In-Phase" and "Quadrature" components (like a complex number in electrical engineering). These components allow the calculation of the dominant cycle period (how long the current market wave is). Once the cycle period is known, the indicator computes a trendline that averages price over that specific period, effectively neutralizing the cycle's influence.

### Mathematical Foundation

The process involves several DSP steps:

1. **Smoothing:** A 4-bar Weighted Moving Average (WMA) removes high-frequency noise.
2. **Detrending:** A high-pass filter removes the static trend to isolate the oscillating component.
3. **Hilbert Transform:** Generates In-Phase ($I$) and Quadrature ($Q$) components to measure phase.
4. **Period Measurement:** A Homodyne Discriminator uses the phase rate of change to calculate the Dominant Cycle ($DC$) period.
5. **Instantaneous Trend:** The price is averaged over the calculated $DC$ period (or a smoothed version of it).

$$ IT[i] = \frac{1}{DC} \sum_{k=0}^{DC-1} Price[i-k] $$

### Implementation Details

Our implementation follows Ehlers' original code structure but optimized for C#.

- **Complexity:** O(1) per update (constant time DSP operations).
- **Adaptivity:** The lookback period for the final average changes dynamically with every bar.
- **Smoothing:** The final trendline undergoes additional 4-bar smoothing to remove jaggedness caused by period switching.

## Configuration

| Parameter | Default | Purpose | Adjustment Guidelines |
|-----------|---------|---------|----------------------|
| None | N/A | Fully adaptive | HTIT does not require user parameters; it measures the market directly. |

**Configuration note:** The lack of parameters is a feature, not a bug. It prevents "curve fitting" and ensures the indicator relies on measured market properties rather than user guesses.

## Performance Profile

| Operation | Complexity | Description |
|-----------|------------|-------------------|
| Streaming update | O(1) | Fixed set of DSP equations per bar |
| Bar correction | O(1) | Efficient state rollback |
| Batch processing | O(N) | Single pass through data |
| Memory footprint | O(1) | Fixed size buffers for delay lines (approx 50 bars) |

## Interpretation

### Trading Signals

#### Trend Direction

- **Bullish:** Price > HTIT. The instantaneous trend is rising.
- **Bearish:** Price < HTIT. The instantaneous trend is falling.

#### Crossovers

- **Signal:** Price crossing the HTIT line is a primary signal. Because HTIT adapts to the cycle, these crossovers often occur near the inflection points of the trend.

### When It Works Best

- **Cyclical Markets:** HTIT excels when the market has a recognizable rhythm or cycle, as it can accurately measure and filter it.
- **Trend Reversals:** It is often faster than SMA/EMA at detecting reversals because it shortens its period when cycles become shorter/faster.

### When It Struggles

- **Chaotic Markets:** If the market has no dominant cycle (white noise), the period measurement can become erratic, causing the trendline to wiggle.

## Architecture Notes

This implementation makes specific trade-offs:

### Choice: Homodyne Discriminator

- **Alternative:** Dual Differentiator or Phase Accumulator.
- **Trade-off:** Complexity vs Stability.
- **Rationale:** The Homodyne Discriminator is Ehlers' preferred method for robust cycle measurement in noisy financial data.

### Choice: Fixed Buffers

- **Alternative:** Dynamic Lists.
- **Trade-off:** Memory usage.
- **Rationale:** Using fixed-size circular buffers for the delay lines (Detrender, Q, I) ensures zero allocation during updates.

## References

- Ehlers, John F. "Rocket Science for Traders: Digital Signal Processing Applications." Wiley, 2001.
- Ehlers, John F. "Cybernetic Analysis for Stocks and Futures." Wiley, 2004.

## C# Usage

### Streaming Updates (Single Instance)

```csharp
using QuanTAlib;

var htit = new Htit();

// Process each new bar
TValue result = htit.Update(new TValue(timestamp, closePrice));
Console.WriteLine($"HTIT: {result.Value:F2}");

// Check if buffer is full (requires some history to establish cycle)
if (htit.IsHot)
{
    // Indicator is fully initialized
}
```

### Batch Processing (Historical Data)

```csharp
// TSeries API
TSeries prices = ...;
TSeries htitValues = Htit.Batch(prices);

// Span API (High Performance)
double[] prices = new double[1000];
double[] output = new double[1000];
Htit.Batch(prices.AsSpan(), output.AsSpan());
```

### Bar Correction (isNew Parameter)

```csharp
var htit = new Htit();

// New bar
htit.Update(new TValue(time, 100), isNew: true);

// Intra-bar update
htit.Update(new TValue(time, 101), isNew: false); // Replaces 100 with 101
```
