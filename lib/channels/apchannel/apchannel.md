# APCHANNEL: Adaptive Price Channel

> "A channel isn't a prediction—it's an acknowledgment that price has inertia and boundaries."

Adaptive Price Channel transforms the classic high-low tracking problem into an exponentially weighted persistence model. Instead of rigid lookback windows, APCHANNEL applies EMA smoothing to price extremes, creating support and resistance zones that adapt to volatility without lag spikes.

## The Problem with Fixed Windows

Traditional channels use simple moving averages or fixed-period lookbacks. Close at 100, high at 110, low at 90. Twenty bars later, those extremes drop off the calculation cliff—instant discontinuity. Price didn't forget yesterday's resistance. The math did.

APCHANNEL solves this with exponential decay. Recent extremes dominate. Ancient extremes fade but never vanish. The channel breathes with the market instead of stuttering through arbitrary cutoffs.

## Architecture & Physics

APCHANNEL maintains two independent exponential moving averages: one tracking highs, another tracking lows. The alpha parameter controls decay rate—think of it as the channel's memory span.

### Memory vs Responsiveness

Alpha creates a trade-off architects know well: fast response or stable structure.

* **High alpha (0.7-0.9)**: Tracks price tightly. Responds to every wiggle. Channel contracts and expands rapidly. Good for scalping, bad for filtering noise.
* **Low alpha (0.1-0.2)**: Smooth, stable bands. Ignores minor fluctuations. Channel defines macro support/resistance. Good for trend following, bad for fast entries.

The math is straightforward EMA recursion:

``` math
HighEMA[i] = α × High[i] + (1 - α) × HighEMA[i-1]
LowEMA[i] = α × Low[i] + (1 - α) × LowEMA[i-1]
```

QuanTAlib uses `Math.FusedMultiplyAdd` for this calculation—single rounding step, better precision, often faster on modern CPUs.

### O(1) Constant Time

Each bar update requires exactly two multiplications and two additions. No loops. No history scans. O(1) complexity regardless of how much data precedes the current bar. This is why EMA-based channels outperform SMA-based alternatives in streaming environments.

## Mathematical Foundation

### 1. Exponential Moving Average

For each price extreme (high and low):

$$\text{EMA}_t = \alpha \cdot P_t + (1 - \alpha) \cdot \text{EMA}_{t-1}$$

Where:

* $\alpha$ = smoothing factor (0 < α ≤ 1)
* $P_t$ = price at time $t$
* $\text{EMA}_{t-1}$ = previous EMA value

### 2. Channel Bands

$$\text{UpperBand}_t = \alpha \cdot \text{High}_t + (1 - \alpha) \cdot \text{UpperBand}_{t-1}$$

$$\text{LowerBand}_t = \alpha \cdot \text{Low}_t + (1 - \alpha) \cdot \text{LowerBand}_{t-1}$$

### 3. Midpoint (Primary Output)

$$\text{Midpoint}_t = \frac{\text{UpperBand}_t + \text{LowerBand}_t}{2}$$

### 4. Relationship to Period

APCHANNEL uses alpha directly, but can be converted to/from period:

$$\alpha = \frac{2}{N + 1}$$

Where $N$ = equivalent period for 2/(N+1) weighting scheme.

## Performance Profile

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | 8 ns/bar | FMA optimization, zero allocation |
| **Allocations** | 0 | Streaming mode heap-free |
| **Complexity** | O(1) | Constant time per update |
| **Accuracy** | 10 | Mathematically exact EMA |
| **Timeliness** | 8 | Alpha-dependent, no lookahead |
| **Overshoot** | 3 | High alpha can whipsaw |
| **Smoothness** | 7 | Exponential weighting reduces noise |

**Warmup Period**: $\lceil 3/\alpha \rceil$ bars for ~95% convergence.

**SIMD Support**: Partial. Recursive EMA dependency prevents full vectorization, but high/low processing can be parallelized.

## Validation

APCHANNEL implementation validated against mathematical EMA properties:

| Test | Status | Notes |
| :--- | :--- | :--- |
| **Manual Calculation** | ✅ | Matches hand-computed EMA values |
| **Skender EMA** | ✅ | High/low bands match Skender.GetEma() |
| **Mode Consistency** | ✅ | Streaming, Span, Batch produce identical results |
| **NaN Handling** | ✅ | Carries forward last valid value |

No external library provides APCHANNEL directly (it's a custom PineScript indicator), so validation focuses on verifying the EMA components against established libraries.

## Usage Examples

### Basic Usage (Streaming)

```csharp
var apc = new Apchannel(alpha: 0.2);

foreach (var bar in bars)
{
    apc.Add(bar);
    Console.WriteLine($"Upper: {apc.UpperBand:F2}, Lower: {apc.LowerBand:F2}, Mid: {apc.Last.Value:F2}");
}
```

### Batch Processing

```csharp
var (results, indicator) = Apchannel.Calculate(bars, alpha: 0.2);

// results contains TBarSeries where:
// - High = UpperBand
// - Low = LowerBand
// - Close = Midpoint

// indicator is primed and ready for live updates
indicator.Add(nextBar);
```

### Span-Based (High Performance)

```csharp
double[] highs = bars.Select(b => b.High).ToArray();
double[] lows = bars.Select(b => b.Low).ToArray();
double[] upperBand = new double[highs.Length];
double[] lowerBand = new double[lows.Length];

Apchannel.Calculate(highs, lows, upperBand, lowerBand, alpha: 0.2);
```

### Event-Driven (Chained)

```csharp
var barSource = new TBarSeries();
var apc = new Apchannel(barSource, alpha: 0.2);

apc.Pub += (s, e) => {
    Console.WriteLine($"Channel updated: {e.Value.Value:F2}");
};

barSource.Add(newBar); // Triggers calculation and event
```

## Parameter Selection

### By Trading Style

| Style | Alpha | Period Equiv | Rationale |
| :--- | :--- | :--- | :--- |
| **Scalping** | 0.7-0.9 | 2-3 | Tight bands, fast reaction |
| **Day Trading** | 0.3-0.5 | 4-6 | Balance speed and stability |
| **Swing Trading** | 0.15-0.25 | 8-13 | Smooth macro support/resistance |
| **Position Trading** | 0.05-0.1 | 20-40 | Wide bands, filter noise |

### Alpha vs Period Conversion

```csharp
// Period to Alpha
double alpha = 2.0 / (period + 1);

// Alpha to Period (approximate)
int period = (int)Math.Round(2.0 / alpha - 1);
```

## Common Pitfalls

### Confusing Alpha with Period

Alpha is **not** a lookback period. Alpha = 0.2 doesn't mean "20 bars." It means "20% of today's value, 80% of yesterday's state." The effective memory span is roughly $3/\alpha$ bars for 95% convergence.

### Expecting Hard Boundaries

APCHANNEL bands are **zones**, not walls. Price can (and will) exceed them during strong trends or volatility spikes. Treat them as probabilistic support/resistance, not absolute constraints.

### Over-Optimizing Alpha

Tuning alpha to recent data is curve-fitting. Markets change regimes. An alpha that worked perfectly last month may fail next month. Pick a value that matches your trading timeframe and stick with it.

### Ignoring Warmup

The first $\lceil 3/\alpha \rceil$ bars are stabilization phase. `IsHot` property tracks this. Using early values for entries can produce false signals as the channel converges.

## Implementation Notes

QuanTAlib's APCHANNEL uses several optimizations:

1. **FMA Instructions**: `Math.FusedMultiplyAdd(decay, prevEMA, alpha * newValue)` combines multiplication and addition with single rounding, improving both precision and performance on modern CPUs.

2. **Record Struct State**: All scalar state variables packed into a single `record struct` for value semantics, automatic equality, and efficient rollback during bar corrections.

3. **Zero-Allocation Streaming**: The `Update` method allocates no heap memory. EMA state updated in-place. Critical for high-frequency environments.

4. **NaN Resilience**: Invalid inputs (NaN, Infinity) substituted with last valid values. Channel never crashes, never propagates garbage.

5. **Partial SIMD**: While EMA's recursive nature prevents full vectorization, high and low processing can run in parallel on AVX2-capable hardware.

## See Also

* [EMA](../../trends/ema/ema.md) - The underlying smoothing mechanism
* [BBANDS](../bbands/bbands.md) - Volatility-based channel alternative
* [KCHANNEL](../kchannel/kchannel.md) - ATR-based channel with different adaptation logic
* [DCHANNEL](../dchannel/dchannel.md) - Simple high/low channel without smoothing

---

**License**: MIT  
**Source**: [lib/channels/apchannel/apchannel.cs](apchannel.cs)  
**Tests**: [apchannel.Tests.cs](apchannel.Tests.cs) | [apchannel.Validation.Tests.cs](apchannel.Validation.Tests.cs)
