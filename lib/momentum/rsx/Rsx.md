# RSX - Jurik Relative Strength Index

A "noise-free" version of the Relative Strength Index (RSI) that eliminates the jaggedness of the original without introducing the lag of traditional smoothing. It produces a silky-smooth 0-100 oscillator that preserves the precise timing of market turns.

## What It Does

RSX solves the classic RSI dilemma: standard RSI is too twitchy (generating false signals), but smoothing it makes it too slow (missing the trade).

RSX replaces the simple moving averages in RSI with a sophisticated, cascading filter chain. This allows it to strip out high-frequency noise while tracking the underlying momentum with near-zero latency. The result is a curve that looks like a sine wave—clean, continuous, and devoid of the "jitter" that plagues standard oscillators.

## Historical Context

Mark Jurik developed RSX as part of his suite of "zero-lag" indicators. He recognized that the jagged nature of RSI made it difficult to programmatically detect peaks and valleys. By applying advanced signal processing techniques (similar to those used in guidance systems), he created an indicator that retains the familiar 0-100 scale of RSI but behaves with the smoothness of a much slower moving average.

## How It Works

The algorithm is significantly more complex than standard RSI, employing a multi-stage filter architecture.

### The Math

1. **Momentum Calculation**:
    $$ \text{Momentum} = (\text{Price}_t - \text{Price}_{t-1}) \times 100 $$

2. **Cascading Filters**:
    The momentum and the absolute momentum are each passed through a chain of three filter stages. Each stage consists of two coupled IIR filters.
    $$ \text{Stage}_1 \rightarrow \text{Stage}_2 \rightarrow \text{Stage}_3 $$
    This creates a "higher-order" smoothing effect that suppresses noise aggressively while maintaining phase alignment (low lag).

3. **Normalization**:
    $$ \text{RSX} = \left( \frac{\text{Smoothed Momentum}}{\text{Smoothed Abs Momentum}} + 1 \right) \times 50 $$

## Configuration

| Parameter | Type  | Default | Description                                              |
|-----------|-------|---------|----------------------------------------------------------|
| `period`  | `int` | 14      | The smoothing period. Typical values range from 8 to 40. |

## Performance Profile

While mathematically dense, the RSX implementation is highly optimized for execution speed.

- **Complexity**: $O(1)$ per update. The filter chain involves a fixed number of floating-point operations regardless of the period.
- **Memory**: Constant space. It stores the state variables for the 12 internal filter nodes (6 for momentum, 6 for absolute momentum).
- **Allocations**: Zero heap allocations during the `Update` cycle.

| Operation | Time Complexity | Space Complexity |
|-----------|-----------------|------------------|
| Update    | $O(1)$          | $O(1)$           |
| Batch     | $O(N)$          | $O(N)$           |

## Interpretation

RSX is interpreted exactly like RSI, but with higher confidence due to the lack of noise.

### 1. Overbought / Oversold

- **Overbought**: > 70 (or 80).
- **Oversold**: < 30 (or 20).
*Note: Because RSX is smoother, it spends less time "wiggling" in the extreme zones. An exit from the zone is a cleaner signal.*

### 2. Divergence

RSX is exceptional for spotting divergence because its peaks and valleys are distinct.

- **Bearish Divergence**: Price makes a higher high, RSX makes a lower high.
- **Bullish Divergence**: Price makes a lower low, RSX makes a higher low.

### 3. Trend Confirmation

- **Bullish**: RSX > 50.
- **Bearish**: RSX < 50.

## Architecture Notes

- **Filter Chain**: The class implements the Jurik filter chain directly rather than relying on external classes. This ensures maximum performance and encapsulation.
- **Warmup**: The filter requires a warmup period to stabilize. The `IsHot` property indicates when the internal state has converged.
- **Input**: Accepts `TValue` (Close price). Unlike DMX, it does not require High/Low data.

## References

- Jurik Research: [RSX - Relative Strength Quality Index](http://www.jurikres.com/catalog/ms_rsx.htm)
- ProRealCode: [Jurik RSX Implementation](https://www.prorealcode.com/prorealtime-indicators/jurik-rsx/)

## C# Usage

```csharp
using QuanTAlib;

// 1. Initialize
var rsx = new Rsx(period: 14);

// 2. Process a Value
// RSX typically uses Close price
var result = rsx.Update(new TValue(DateTime.UtcNow, 105.5));

Console.WriteLine($"RSX: {result.Value:F2}");

// 3. Batch Calculation
var series = new TBarSeries();
// ... populate series ...
var rsxSeries = Rsx.Batch(series.Close, period: 14);
