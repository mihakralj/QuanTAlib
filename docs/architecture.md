# Architecture

QuanTAlib is built on a specific set of architectural decisions designed to maximize performance on modern hardware while maintaining mathematical correctness.

## Three Core Decisions

Three design decisions define QuanTAlib's performance characteristics:

### 1. Structure of Arrays (SoA) Memory Layout

Timestamps and values are stored in separate contiguous arrays rather than interleaved in objects. This seemingly minor change enables direct SIMD vectorization—CPU processes eight values in a single instruction instead of one at a time. The performance difference is measurable: averaging 10,000 values takes 2.4μs with SIMD versus 18.7μs with scalar operations. That's an 8x improvement just from rearranging memory.

### 2. O(1) Streaming Algorithms

Constant computational complexity is maintained per incoming data point regardless of lookback period. A 14-period RSI and a 200-period RSI both process new bars in 0.4μs. Traditional batch recalculation approaches scale linearly with period length, introducing variable latency that makes real-time processing unpredictable. QuanTAlib accepts higher memory overhead (40-60 bytes per indicator instance) to guarantee predictable timing when processing hundreds of symbols simultaneously.

### 3. Explicit Initialization Handling

Meaningful values are returned from the first bar while confidence is exposed through the `IsHot` property. A 14-period SMA calculates results starting at bar 1 using whatever data is available—the math to calculate averages works with limited history, just not at full precision for a period of 14. Other libraries either hide these early values (returning NaN or null) or output numbers without indicating their veracity. QuanTAlib returns usable values immediately and sets `IsHot = true` when the indicator has accumulated enough data to guarantee correctness of results.

## Four Operating Modes

Trading systems have different needs. Backtesting engines process years of historical data in batch. Real-time systems update indicators bar-by-bar as new data arrives. Event-driven architectures react to indicator changes asynchronously. QuanTAlib provides four modes optimized for these distinct patterns.

### Span Mode: Direct Memory Operations

Operates directly on `Span<double>` without allocating objects. You provide raw arrays, QuanTAlib returns calculated arrays. Zero garbage collection pressure, maximum speed, minimal abstraction. This mode exists for one purpose: processing large datasets as fast as physically possible on current hardware.

**When to use:** Batch processing historical data, backtesting engines, research environments where you're calculating thousands of indicators across years of data.

### Batch Mode: TSeries Objects

Wraps calculations in TSeries objects that maintain timestamps, handle array resizing, and provide time-based indexing. You add price data with timestamps, QuanTAlib returns a time-aligned series with metadata. This is Span mode with a protective wrapper that handles the tedious details.

**When to use:** Historical analysis where you want time alignment without sacrificing too much performance. Research notebooks, strategy prototyping, exploratory analysis.

### Streaming Mode: Real-Time Updates

Processes one bar at a time, maintaining internal state between updates. Call `Update(TValue, isNew)` with each new price, get the current indicator value. The `isNew` parameter distinguishes between new bars and updates to the current bar (handling the common pattern where the last bar's values change as new ticks arrive).

**When to use:** Live trading systems, real-time charting, tick-by-tick analysis. Any scenario where data arrives sequentially and you need immediate results.

### Eventing Mode: Reactive Architectures

Extends streaming mode with full event infrastructure. Indicators raise events when values change, when warmup completes (`IsHot`), or when significant conditions occur. Build reactive chains where one indicator's output triggers another's calculation, creating complex analytical pipelines that respond to market conditions.

**When to use:** Complex trading systems with conditional logic, risk management systems that react to volatility changes, or any architecture where indicators need to communicate state changes rather than just return values.

## Memory Layout Details

The library uses a Structure of Arrays (SoA) approach for its core data structures.

- **TSeries**: Internally maintains two `List<T>` collections:
  - `List<long> _t`: Timestamps (ticks)
  - `List<double> _v`: Values
- **Access**: Data is exposed via `ReadOnlySpan<double>` properties, allowing zero-copy access to the underlying memory for SIMD operations.

This layout is cache-friendly. When calculating an average, the CPU loads a cache line filled entirely with values, without wasting space on interleaved timestamps or object headers.

## SIMD Implementation

QuanTAlib leverages .NET's `System.Runtime.Intrinsics` to access hardware-specific instructions (AVX2, AVX-512).

- **Vectorization**: Operations like summation, min/max finding, and element-wise arithmetic are vectorized.
- **Fallback**: The library checks for hardware support at runtime. If AVX2 is not available, it falls back to scalar implementations, ensuring compatibility with older hardware (though at reduced speed).
- **Zero-Allocation**: SIMD operations are performed on `Span<T>` and `ReadOnlySpan<T>`, ensuring no heap allocations occur during the calculation phase.

## Design Philosophy

1. **Correctness First**: Validation is performed against original research papers and established libraries.
2. **Performance by Default**: Algorithms and data structures that are naturally fast are chosen.
3. **No Hidden Allocations**: Hot paths are allocation-free to prevent GC pauses.
4. **Transparency**: The internal state (like `IsHot`) is exposed so you know exactly what the indicator is doing.
