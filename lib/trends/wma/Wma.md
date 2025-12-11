# WMA: Weighted Moving Average

## Overview and Purpose

The Weighted Moving Average (WMA) is a technical indicator that applies progressively increasing weights to more recent price data. Emerging in the early 1950s during the formative years of technical analysis, WMA gained significant adoption among professional traders through the 1970s as computational methods became more accessible. The approach was formalized in Robert Colby's 1988 "Encyclopedia of Technical Market Indicators," establishing it as a staple in technical analysis software. Unlike the Simple Moving Average (SMA) which gives equal weight to all prices, WMA assigns greater importance to recent prices, creating a more responsive indicator that reacts faster to price changes while still providing effective noise filtering.

## Core Concepts

* **Linear weighting:** WMA applies progressively increasing weights to more recent price data, creating a recency bias that improves responsiveness
* **Market application:** Particularly effective for identifying trend changes earlier than SMA while maintaining better noise filtering than faster-responding averages like EMA
* **Timeframe flexibility:** Works effectively across all timeframes, with appropriate period adjustments for different trading horizons
* **O(1) complexity:** This implementation uses a dual running sum technique for constant-time updates regardless of period

The core innovation of WMA is its linear weighting scheme, which strikes a balance between the equal-weight approach of SMA and the exponential decay of EMA. This creates an intuitive and effective compromise that prioritizes recent data while maintaining a finite lookback period, making it particularly valuable for traders seeking to reduce lag without excessive sensitivity to price fluctuations.

## Common Settings and Parameters

| Parameter | Default | Function | When to Adjust |
|-----------|---------|----------|---------------|
| Period | 14 | Controls the lookback period | Increase for smoother signals in volatile markets, decrease for responsiveness |
| Source | Close | Price data used for calculation | Consider using HLC3 for a more balanced price representation |

**Pro Tip:** For most trading applications, using a WMA with period N provides better responsiveness than an SMA with the same period, while generating fewer whipsaws than an EMA with comparable responsiveness.

## Calculation and Mathematical Foundation

**Simplified explanation:**
WMA calculates a weighted average of prices where the most recent price receives the highest weight, and each progressively older price receives one unit less weight. For example, in a 5-period WMA, the most recent price gets a weight of 5, the next most recent a weight of 4, and so on, with the oldest price getting a weight of 1.

**Technical formula:**
$$WMA = \frac{\sum_{i=1}^{n} w_i \cdot P_i}{\sum_{i=1}^{n} w_i} = \frac{n \cdot P_n + (n-1) \cdot P_{n-1} + \ldots + 1 \cdot P_1}{\frac{n(n+1)}{2}}$$

Where:

* $n$ is the period length
* $P_i$ is the price at position $i$ (oldest to newest)
* $w_i = i$ (linear weights from 1 to n)
* Divisor $= \frac{n(n+1)}{2}$ (sum of weights 1 through n)

**O(1) Optimization - Dual Running Sums:**

This implementation uses an advanced O(1) algorithm that eliminates the need to loop through all period values on each bar. The key insight is maintaining two running sums:

1. **Unweighted sum (S)**: Simple sum of all values in the window
2. **Weighted sum (W)**: Sum of all weighted values

The recurrence relation for a full window is:
$$S_{new} = S - P_{oldest} + P_{new}$$
$$W_{new} = W - S_{old} + n \cdot P_{new}$$
$$WMA = \frac{W_{new}}{divisor}$$

This works because when all weights decrement by 1 (as the window slides), it's mathematically equivalent to subtracting the entire unweighted sum. The implementation:

* **During warmup**: Accumulates both sums as the window fills, computing denominator each bar
* **After warmup**: Uses cached denominator (constant at $\frac{n(n+1)}{2}$), updates both sums in constant time
* **Performance**: ~8 operations per bar regardless of period, vs ~100+ for naive O(n) implementation

> 🔍 **Technical Note:** Unlike EMA which theoretically considers all historical data (with diminishing influence), WMA has a finite memory, completely dropping prices that fall outside its lookback window. This creates a cleaner break from outdated market conditions. The O(1) optimization achieves 12-25x speedup over naive implementations while maintaining exact mathematical equivalence.

## C# Implementation

The library provides two implementations: a standard scalar version and a multi-period vector version for calculating multiple WMAs simultaneously.

### Single WMA (`Wma`)

The `Wma` class calculates a single weighted moving average with O(1) update complexity.

```csharp
using QuanTAlib;

// Initialize with period 10
var wma = new Wma(10);

// Streaming update
TValue result = wma.Update(new TValue(time, price));
Console.WriteLine($"Current WMA: {result.Value}");

// Access properties
Console.WriteLine($"Name: {wma.Name}");           // "Wma(10)"
Console.WriteLine($"IsHot: {wma.IsHot}");          // true when buffer is full

// Batch calculation (TSeries API)
TSeries source = ...;
TSeries results = Wma.Calculate(source, 10);

// High-performance Span API (zero allocation)
double[] prices = new double[10000];
double[] output = new double[10000];
Wma.Calculate(prices.AsSpan(), output.AsSpan(), period: 10);
```

### Zero-Allocation Span API

For performance-critical scenarios (backtesting, HFT), use the Span-based overload:

```csharp
// Allocate buffers once, reuse across calculations
double[] source = new double[200000];
double[] wmaOutput = new double[200000];

// Zero heap allocation during calculation
Wma.Calculate(source.AsSpan(), wmaOutput.AsSpan(), period: 100);

// Results are written directly to output buffer
Console.WriteLine($"Last WMA: {wmaOutput[^1]}");
```

**Benefits:**

* **Zero allocation**: No GC pressure during calculation
* **Cache-friendly**: Sequential memory access patterns
* **O(1) per-bar** via dual running sums
* **Compatible** with `ArrayPool<T>` for buffer management

### Bar Correction (isNew Parameter)

`Wma` supports intra-bar updates for real-time trading systems:

```csharp
var wma = new Wma(10);

// Process historical bars
for (int i = 0; i < historicalBars.Count; i++)
{
    wma.Update(historicalBars[i], isNew: true);
}

// Real-time: receive initial tick for new bar
wma.Update(new TValue(time, 100.5), isNew: true);

// Real-time: price updates within same bar
wma.Update(new TValue(time, 101.0), isNew: false);  // O(1) correction
wma.Update(new TValue(time, 100.8), isNew: false);  // O(1) correction

// Bar closes, next bar starts
wma.Update(new TValue(time + 1, 101.2), isNew: true);
```

**Implementation detail:** Bar correction is O(1) using scalar state save/restore, not buffer copying.

### Eventing and Reactive Support

This indicator implements the `ITValuePublisher` interface, enabling event-driven and reactive workflows.

* **Subscription:** Can be constructed with an `ITValuePublisher` (e.g., `TSeries`) to automatically update when the source emits a new value.
* **Publication:** Emits a `Pub` event with the new `TValue` whenever it is updated.

```csharp
using QuanTAlib;

// 1. Setup a source (publisher)
var source = new TSeries();

// 2. Create indicator subscribed to source
// It waits for events from 'source'
var wma = new Wma(source, period: 10);

// 3. Optional: Subscribe to indicator's output
wma.Pub += (item) => Console.WriteLine($"WMA Updated: {item.Value}");

// 4. Ingest data into source
// This triggers the chain: source -> wma -> Console.WriteLine
source.Add(new TValue(DateTime.Now, 100));
source.Add(new TValue(DateTime.Now, 105));
```

This pattern allows building complex, reactive processing pipelines without manual update loops.

### Handling Invalid Values (NaN/Infinity)

`Wma` uses **last-value substitution** for handling invalid inputs:

```csharp
var wma = new Wma(10);

// Valid values establish baseline
wma.Update(new TValue(time, 100));
wma.Update(new TValue(time, 110));

// NaN or Infinity inputs are replaced with last valid value (110)
var result = wma.Update(new TValue(time, double.NaN));
Console.WriteLine(double.IsFinite(result.Value)); // true

// Works identically for batch operations
var series = new TSeries();
series.Add(time, 100);
series.Add(time + 1, double.NaN);  // Will use 100
series.Add(time + 2, 120);
var results = wma.Update(series);  // All values are finite
```

**Behavior:**

* When `NaN`, `PositiveInfinity`, or `NegativeInfinity` is encountered, the last valid value is substituted
* This provides output continuity instead of propagating invalid values
* `Reset()` clears the last valid value, so the next valid input establishes a new baseline

### Performance Characteristics

| Operation | Complexity | Notes |
|-----------|------------|-------|
| Update (isNew=true) | O(1) | Dual running sums: `S = S - oldest + new; W = W - S_old + n*new` |
| Update (isNew=false) | O(1) | Scalar state restore + recalculate |
| Batch processing | O(n) | Where n is series length |
| Memory (single) | O(period) | One RingBuffer for values |
| Memory (state) | O(1) | 7 doubles for bar correction |

The implementation uses:

* **Dual running sums** for O(1) weighted average calculation
* **Scalar state save/restore** for O(1) bar correction
* **Pinned memory** in RingBuffer for cache-friendly access
* **CollectionsMarshal.SetCount** for zero-allocation batch processing
* **SIMD Acceleration** (AVX512/AVX2) for high-performance batch processing

## Interpretation Details

WMA can be used in various trading strategies:

* **Trend identification:** The direction of WMA indicates the prevailing trend with greater responsiveness than SMA
* **Signal generation:** Crossovers between price and WMA generate trade signals earlier than with SMA
* **Support/resistance levels:** WMA can act as dynamic support during uptrends and resistance during downtrends
* **Moving average crossovers:** When a shorter-period WMA crosses above a longer-period WMA, it signals a potential uptrend (and vice versa)
* **Trend strength assessment:** Distance between price and WMA can indicate trend strength

### WMA vs SMA vs EMA Comparison

| Aspect | WMA | SMA | EMA |
|--------|-----|-----|-----|
| Weighting | Linear (n, n-1, ..., 1) | Equal for all values | Exponential decay |
| Lag | Medium | Highest | Lowest |
| Sensitivity | Medium | Low | High |
| Noise filtering | Good | Best | Medium |
| Memory required | O(period) buffer | O(period) buffer | O(1) - no buffer |
| Window behavior | Finite, clean cutoff | Finite, abrupt exit | Infinite, gradual decay |
| Best use | Balanced responsiveness, crossover systems | Long-term trends, support/resistance | Short-term signals, momentum |

## Limitations and Considerations

* **Market conditions:** Still suboptimal in highly volatile or sideways markets where enhanced responsiveness may generate false signals
* **Lag factor:** While less than SMA, still introduces some lag in signal generation
* **Abrupt window exit:** The oldest price suddenly drops out of calculation when leaving the window, potentially causing small jumps
* **Step changes:** Linear weighting creates discrete steps in influence rather than a smooth decay
* **Complementary tools:** Best used with volume indicators and momentum oscillators for confirmation

## References

* Colby, Robert W. "The Encyclopedia of Technical Market Indicators." McGraw-Hill, 2002
* Murphy, John J. "Technical Analysis of the Financial Markets." New York Institute of Finance, 1999
* Kaufman, Perry J. "Trading Systems and Methods." Wiley, 2013
