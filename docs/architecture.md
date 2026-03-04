# Architecture

QuanTAlib is built on specific architectural decisions designed to maximize performance on modern hardware while maintaining mathematical correctness. These decisions involve trade-offs. Understanding the trade-offs helps determine whether QuanTAlib fits a particular use case.

## Three Core Decisions

### 1. Structure of Arrays (SoA) Memory Layout

Traditional object-oriented design stores data as arrays of objects: `List<PriceBar>` where each `PriceBar` contains timestamp, open, high, low, close, volume. This layout is intuitive for humans. It is terrible for CPUs.

QuanTAlib stores timestamps and values in separate contiguous arrays. When calculating an average, the CPU loads a cache line filled entirely with price values, without wasting space on interleaved timestamps or object headers.

``` mermaid
graph LR
    subgraph AoS [Array of Structures: CPU chokes on interleaved data]
        direction LR
        A1[Time] --- A2[Price] --- A3[Time] --- A4[Price] --- A5[Time]
        style A1 fill:#550000
        style A3 fill:#550000
        style A5 fill:#550000
    end
    
    subgraph SoA [Structure of Arrays: Contiguous SIMD pipeline]
        direction LR
        S1[Price] --- S2[Price] --- S3[Price] --- S4[Price] --- S5[Price]
        style S1 fill:#005500
        style S2 fill:#005500
        style S3 fill:#005500
        style S4 fill:#005500
        style S5 fill:#005500
    end
```

The performance difference is measurable:

| Operation | SoA Layout | AoS Layout | Improvement |
| :-------- | ---------: | ---------: | ----------: |
| Average 10,000 values | 2.4 μs | 18.7 μs | 7.8× |
| Sum 100,000 values | 12.1 μs | 89.3 μs | 7.4× |
| Min/Max 500,000 values | 48.2 μs | 412.6 μs | 8.6× |

The gains come from two sources: cache efficiency (no wasted bytes) and SIMD vectorization (CPU processes 4-8 values per instruction). Both require contiguous memory.

### 2. O(1) Streaming Algorithms

A 14-period RSI and a 200-period RSI both process new bars in approximately 0.4 μs. The lookback period does not affect per-bar processing time.

Traditional batch approaches recalculate from scratch, scaling linearly with period. A 200-period indicator takes 14× longer than a 14-period indicator. This variable latency makes real-time processing unpredictable.

QuanTAlib maintains running state: partial sums, ring buffers, recursive coefficients. The cost is memory (40-60 bytes per indicator instance). The benefit is guaranteed constant-time updates regardless of period.

| Approach | 14-period | 200-period | 1000-period |
| :------- | --------: | ---------: | ----------: |
| Batch recalculation | 0.4 μs | 5.7 μs | 28.5 μs |
| O(1) streaming | 0.4 μs | 0.4 μs | 0.4 μs |

For single-symbol analysis, batch recalculation is fast enough. For 500 symbols updating simultaneously, O(1) streaming prevents the latency spikes that cause missed fills.

### 3. Explicit Initialization Handling

A 14-period SMA cannot produce a mathematically correct value until 14 bars have accumulated. Most libraries handle this in one of two ways:

1. **Return nothing**: NaN, null, or skip the first N-1 values entirely
2. **Return something**: Output numbers without indicating they are preliminary

QuanTAlib takes a third approach: return usable values immediately and expose confidence through the `IsHot` property.

```csharp
var sma = new Sma(14);
var result = sma.Update(new TValue(time, price));

// result.Value is always a number (the best estimate given available data)
// result.IsHot indicates whether enough data has accumulated
if (result.IsHot)
{
    // Full confidence: at least 14 bars processed
}
```

The math to calculate averages works with limited history. A 14-period SMA with 5 bars returns the average of those 5 bars. Not the 14-period average (that would require prescience), but a reasonable estimate that improves as data accumulates.

## Four Operating Modes

Trading systems have different data flow patterns. Backtesting engines process years of historical data in batch. Real-time systems update indicators bar-by-bar. Event-driven architectures react to changes asynchronously. QuanTAlib provides four modes optimized for these patterns.

``` mermaid
graph TD
    Event[Eventing Mode<br/>Reactive Chain] -->|Adds pub/sub overhead| Stream
    Stream[Streaming Mode<br/>Stateful O1 Updates] -->|Maintains state across| Span
    Batch[Batch Mode<br/>Time-Aligned TSeries] -->|Unwraps to| Span
    
    Span[Span Mode<br/>Stackalloc / Raw Memory]
    
    style Span fill:#003300,stroke:#00ff00,stroke-width:2px
```

### Span Mode

Operates directly on `Span<double>` without allocating objects. Raw arrays in, calculated arrays out. Zero garbage collection pressure, maximum speed, minimal abstraction.

```csharp
ReadOnlySpan<double> prices = GetPrices();
Span<double> output = stackalloc double[prices.Length];
Sma.Calculate(prices, output, period: 14);
```

**Use case:** Batch processing historical data, backtesting engines, research environments processing thousands of indicators across years of data.

**Trade-off:** No timestamps, no metadata, no safety rails. The caller manages array bounds, alignment, and interpretation.

### Batch Mode

Wraps calculations in TSeries objects that maintain timestamps, handle array resizing, and provide time-based indexing. Span mode with a protective wrapper.

```csharp
TSeries prices = LoadHistoricalData();
TSeries smaValues = Sma.Calculate(prices, period: 14);
```

**Use case:** Historical analysis requiring time alignment. Research notebooks, strategy prototyping, exploratory analysis.

**Trade-off:** 10-15% overhead versus Span mode for the convenience of timestamp management.

### Streaming Mode

Processes one bar at a time, maintaining internal state between updates. Call `Update()` with each new price, receive the current indicator value.

```csharp
var sma = new Sma(14);
foreach (var bar in liveStream)
{
    var result = sma.Update(new TValue(bar.Time, bar.Close), isNew: true);
}
```

The `isNew` parameter distinguishes between new bars and updates to the current bar. When the last bar's values change as new ticks arrive, pass `isNew: false` to update without advancing state.

**Use case:** Live trading systems, real-time charting, tick-by-tick analysis.

**Trade-off:** Per-bar overhead is higher than batch processing (function call, state management). For historical analysis, batch mode is faster.

### Eventing Mode

Extends streaming mode with event infrastructure. Indicators raise events when values change, enabling reactive chains where one indicator's output triggers another's calculation.

```csharp
var source = new TSeries();
var sma = new Sma(source, 14);
var ema = new Ema(source, 14);

sma.Pub += (s, e) => HandleSmaUpdate(e.Value);
source.Add(new TValue(DateTime.UtcNow, 100.0));  // Both indicators update
```

**Use case:** Complex trading systems with conditional logic, risk management systems reacting to volatility changes, pipelines where indicators communicate state.

**Trade-off:** Event dispatch overhead. For simple calculations, direct calls are faster.

## Memory Layout Details

### TSeries Internal Structure

```csharp
internal List<long> _t;    // Timestamps (ticks since epoch)
internal List<double> _v;  // Values
```

Two separate `List<T>` collections rather than `List<(long, double)>`. Access is exposed via `ReadOnlySpan<double>` properties, allowing zero-copy access to underlying memory.

### Cache Line Efficiency

A typical CPU cache line is 64 bytes. With SoA layout:

| Layout | Values per cache line | Utilization |
| :----- | --------------------: | ----------: |
| SoA (doubles only) | 8 | 100% |
| AoS (timestamp + value) | 4 | 100% |
| AoS (full PriceBar object) | 1-2 | 30-60% |

When iterating through values for calculation, SoA loads 8 relevant values per cache miss. AoS with full objects loads 1-2 values plus irrelevant data.

## SIMD Implementation

QuanTAlib uses `System.Runtime.Intrinsics` to access hardware vector instructions.

### Vectorization Strategy

| Operation | Scalar | AVX2 (4 doubles) | AVX-512 (8 doubles) |
| :-------- | -----: | ---------------: | ------------------: |
| Sum | 1 value/cycle | 4 values/cycle | 8 values/cycle |
| Min/Max | 1 value/cycle | 4 values/cycle | 8 values/cycle |
| Element-wise arithmetic | 1 value/cycle | 4 values/cycle | 8 values/cycle |

### Runtime Detection

```csharp
if (Avx512F.IsSupported)
    CalculateAvx512(source, output);
else if (Avx2.IsSupported)
    CalculateAvx2(source, output);
else
    CalculateScalar(source, output);
```

``` mermaid
graph TD
    Start{JIT Hardware Detection}
    
    AVX512[AVX-512 Vectorization<br/>8 doubles per instruction]
    AVX2[AVX2 Vectorization<br/>4 doubles per instruction]
    NEON[ARM NEON / AdvSimd<br/>2 doubles per instruction]
    Scalar[Scalar Fallback<br/>1 double per instruction]

    Start -->|Avx512F.IsSupported| AVX512
    Start -->|Avx2.IsSupported| AVX2
    Start -->|AdvSimd.IsSupported| NEON
    Start -->|Instruction Set Missing| Scalar
    
    style Start fill:#333
    style AVX512 fill:#004400
    style AVX2 fill:#444400
    style NEON fill:#003366
    style Scalar fill:#440000
```

The library checks hardware support at runtime. Systems without AVX2 fall back to scalar implementations. The code runs everywhere; speed varies with hardware capability.

### Allocation Discipline

SIMD operations are performed on `Span<T>` and `ReadOnlySpan<T>`. No heap allocations occur during calculation. This discipline extends throughout the hot path: no LINQ, no temporary objects, no closures that capture state.

## Design Philosophy

**Correctness first.** Validation against original research papers and established libraries. When sources disagree, trace to the original author or mathematical derivation.

**Performance by default.** Algorithms and data structures chosen for speed. The fast path is the default path.

**No hidden allocations.** Hot paths are allocation-free. The garbage collector stays asleep during trading hours.

**Transparency.** Internal state (`IsHot`, `WarmupPeriod`, `Last`) is exposed. No black boxes.