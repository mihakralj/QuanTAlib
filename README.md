[![Codacy grade](https://app.codacy.com/project/badge/Grade/c8be6c08f5514e95b84d37e661a6ec27)](https://app.codacy.com/gh/mihakralj/QuanTAlib/dashboard?utm_source=gh&utm_medium=referral&utm_content=&utm_campaign=Badge_grade)
[![codecov](https://codecov.io/gh/mihakralj/QuanTAlib/branch/main/graph/badge.svg?style=flat-square&token=YNMJRGKMTJ?style=flat-square)](https://codecov.io/gh/mihakralj/QuanTAlib)
[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=mihakralj_QuanTAlib&metric=security_rating)](https://sonarcloud.io/summary/new_code?id=mihakralj_QuanTAlib)
[![CodeFactor](https://www.codefactor.io/repository/github/mihakralj/quantalib/badge/main)](https://www.codefactor.io/repository/github/mihakralj/quantalib/overview/main)

[![Nuget](https://img.shields.io/nuget/v/QuanTAlib?style=flat-square)](https://www.nuget.org/packages/QuanTAlib/)
![GitHub last commit](https://img.shields.io/github/last-commit/mihakralj/QuanTAlib)
[![Nuget](https://img.shields.io/nuget/dt/QuanTAlib?style=flat-square)](https://www.nuget.org/packages/QuanTAlib/)
[![GitHub watchers](https://img.shields.io/github/watchers/mihakralj/QuanTAlib?style=flat-square)](https://github.com/mihakralj/QuanTAlib/watchers)
[![.NET](https://img.shields.io/badge/.NET-8.0%20|%2010.0-blue?style=flat-square)](https://dotnet.microsoft.com/en-us/download/dotnet)

# QuanTAlib - Quantitative Technical Indicators Without Compromises

TA libraries face a fundamental choice: accept approximations for simplicity OR enforce math rigor. We chose rigor.

**Quan**titative **TA** **lib**rary (QuanTAlib) is a C# library built on the premise that you shouldn't have to choose. Modern CPUs process 4-8 FLOPS per cycle via SIMD. Modern .NET exposes memory layouts making hardware acceleration trivial. QuanTAlib exploits both. **Result:** mathematically rigorous indicators at speeds making real-time multi-symbol analysis practical on ordinary hardware.

## What You Get

QuanTAlib provides technical indicators organized into mathematical families, often found in common charting software. Understanding these families helps choose the right tool for the analytical problem you're actually solving.

### [All Available Indicators](/_index.md)

| Category | What It Measures | Representative Indicators | When You Need It |
|----------|------------------|---------------------------|------------------|
| [**Trends**](trends/_index.md) | Direction and strength of price movement through smoothing and filtering | Simple Moving Average, Exponential Moving Aaverage, Weighted Moving Average, Hull Moving Average, Jurik Moving Average, Kaufmann Adaptive Moving Average | Starting point for most analysis. If you're looking at a chart, you're probably using at least one moving average. The simpler variants (SMA, EMA) work for trend identification. The exotic ones (Jurik, Ehlers) trade CPU cycles for reduced lag. It's a fair exchange — silicon is cheap, timing is expensive. |
| [**Volatility**](volatility/_index.md) | Size and variability of price movements | Average True Range, Standard Deviation, Bollinger Bands, Keltner Channels, Historical Volatility | Position sizing, stop-loss placement, and understanding market regime. ATR tells you how much instruments typically move—essential for risk management. Bollinger Bands show when volatility expands or contracts, helping identify potential breakouts or mean-reversion opportunities. |
| [**Momentum**](momentum/_index.md) | Speed and magnitude of price changes | Relative Strength Index, Stochastic, CCI, Williams %R, MACD, Momentum, ROC | Identifying overbought/oversold conditions and divergences. RSI oscillates between 0-100 by construction—it's the ratio of average gains to average losses. MACD compares two EMAs to show changes in trend strength. These get overused but remain useful when combined with other analysis. |
| [**Volume**](volume/_index.md) | Trading activity and price-volume relationships | OBV, VWAP, Volume Rate of Change, Accumulation/Distribution, MFI | Confirming price movements with volume participation. VWAP shows where institutional traders executed—prices far from VWAP suggest pressure in one direction. OBV accumulates volume on up days and subtracts it on down days, revealing whether volume confirms price trends. |
| [**Channels**](channels/_index.md) | Price boundaries and range definitions | Donchian Channels, Keltner Channels, Price Channels | Breakout strategies and range-bound trading. Donchian Channels mark highest high and lowest low over a period—breaks above/below suggest potential trend changes. Keltner uses ATR for volatility-adjusted bands. Less common than Bollinger Bands but useful for different trading styles. |
| [**Statistics**](statistics/_index.md) | Mathematical relationships between price series | Correlation, Covariance, Beta, Z-Score, Linear Regression | Portfolio analysis, pairs trading, and statistical arbitrage. Correlation measures how two instruments move together (ranging from -1 to +1). Beta quantifies systematic risk relative to a benchmark. Z-Score normalizes values for statistical comparison. These require understanding basic statistics to use correctly. |
| [**Numerics**](numerics/_index.md) | Mathematical transformations and signal processing | Convolution, Filters, Integration, Differentiation, Smoothing functions | Custom indicator development and advanced signal processing. This is the toolkit you use to build your own indicators rather than indicators you apply directly. Convolution lets you create custom filters. Differentiation extracts rate of change. Toolkit for building indicators rather than using them. If you need to differentiate a signal before smoothing it, you know who you are. If not, safe to ignore. |
| [**Errors**](errors/_index.md) | Measurement accuracy and model fit quality | MAE (Mean Absolute Error), RMSE, Residuals, R-Squared | Model validation and forecast quality assessment. After building a predictive model or regression, these metrics tell you how wrong you are on average. RMSE penalizes large errors more heavily than MAE. R-Squared explains what percentage of variance your model captures. Critical for anyone building quantitative strategies. |
| [**Forecasts**](forecasts/_index.md) | Future price prediction and projection | Linear Regression Forecast, Moving Average Projection, Trend Extrapolation | Predictive modeling and systematic strategy development. These attempt to project where prices will go based on historical patterns. Projects price based on historical patterns. Works beautifully until market changes regime, usually 5 minutes after you deploy capital. Useful as inputs to larger systems, dangerous when used as sole decision criteria. |
| [**Cycles**](cycles/_index.md) | Periodic patterns and dominant frequencies in price data | Hilbert Transform, Dominant Cycle, Instantaneous Phase, Sine Wave, MESA | Identifying and trading cyclical market behavior. John Ehlers (who apparently decided financial markets could be analyzed like electrical signals) developed most of these. They use signal processing techniques to decompose price into cycle components. They work beautifully when markets are cyclical. They work poorly when markets trend or trade randomly. The math is complex—phase relationships, frequency analysis—and knowing which market regime you're in becomes harder than using the indicators themselves. |

The categories aren't rigid boundaries—many indicators could fit multiple categories. KAMA is both a trend indicator and uses momentum calculations. Keltner Channels combine trends (moving average centerline) with volatility (ATR bands). The organization helps you understand what analytical problem each indicator solves rather than memorizing which arbitrary category someone assigned it to.

Start with Trends, Volatility, and Momentum if you're new to technical analysis. These provide the foundation most traders need. The specialized categories (Numerics, Errors, Forecasts, Cycles) solve specific problems you'll recognize when you encounter them.

## The Architecture That Makes This Possible

Three design decisions define QuanTAlib's performance characteristics:

**Structure of Arrays (SoA) memory layout** stores timestamps and values in separate contiguous arrays rather than interleaving them. This seemingly minor change enables direct SIMD vectorization—CPU processes eight values in a single instruction instead of one at a time. The performance difference is measurable: averaging 10,000 values takes 2.4μs with SIMD versus 18.7μs with scalar operations. That's an 8x improvement just from rearranging memory.

**O(1) streaming algorithms** maintain constant computational complexity per incoming data point regardless of lookback period. A 14-period RSI and a 200-period RSI both process new bars in 0.4μs. Traditional batch recalculation approaches scale linearly with period length, introducing variable latency that makes real-time processing unpredictable. QuanTAlib accepts higher memory overhead (40-60 bytes per indicator instance) to guarantee predictable timing when processing hundreds of symbols simultaneously.

**Explicit initialization handling** returns meaningful values from the first bar while exposing confidence through the `IsHot` property. A 14-period SMA calculates results starting at bar 1 using whatever data is available—the math to calculate averages works with limited history, just not at full precision for a period of 14. Other libraries either hide these early values (returning NaN or null) or output numbers without indicating their veracity. QuanTAlib returns usable values immediately and sets `IsHot = true` when the indicator has accumulated enough data to guarantee correctness of results. Bar 1-13 gives you working SMA values based on partial history. Bar 14 onwards gives you high-confidence results with complete mathematical foundation. Developer can use early indicator values as needed while knowing exactly when the indicator reaches full reliability.

These aren't novel inventions. They're established techniques from numerical computing applied to financial indicators. The architecture is straightforward once you decide that correctness and performance aren't trade-offs.

### Four Operating Modes for Different Requirements

Trading systems have different needs. Backtesting engines process years of historical data in batch. Real-time systems update indicators bar-by-bar as new data arrives. Event-driven architectures react to indicator changes asynchronously. QuanTAlib provides four modes optimized for these distinct patterns.

#### Span Mode: Direct Memory Operations

Operates directly on `Span<double>` without allocating objects. You provide raw arrays, QuanTAlib returns calculated arrays. Zero garbage collection pressure, maximum speed, minimal abstraction. This mode exists for one purpose: processing large datasets as fast as physically possible on current hardware.

**When to use:** Batch processing historical data, backtesting engines, research environments where you're calculating thousands of indicators across years of data. If you're profiling your system and indicator calculations appear in the trace, switch to Span mode.

**Trade-off:** No metadata, no time alignment, no validation. You manage memory, handle edge cases, and ensure your input arrays match in length. The performance gain justifies the responsibility.

#### Batch Mode: TSeries Objects

Wraps calculations in TSeries objects that maintain timestamps, handle array resizing, and provide time-based indexing. You add price data with timestamps, QuanTAlib returns a time-aligned series with metadata. This is Span mode with a protective wrapper that handles the tedious details.

**When to use:** Historical analysis where you want time alignment without sacrificing too much performance. Research notebooks, strategy prototyping, exploratory analysis. The 2-3x performance cost compared to Span mode is negligible when you're processing data once and analyzing results interactively.

**Trade-off:** Memory overhead from TSeries objects (16 bytes per value for timestamp-value pairs) and 2-3x slower than Span due to bounds checking and metadata management. Still faster than most libraries' fastest mode.

#### Streaming Mode: Real-Time Updates

Processes one bar at a time, maintaining internal state between updates. Call `Update(TValue, isNew)` with each new price, get the current indicator value. The `isNew` parameter distinguishes between new bars and updates to the current bar (handling the common pattern where the last bar's values change as new ticks arrive).

**When to use:** Live trading systems, real-time charting, tick-by-tick analysis. Any scenario where data arrives sequentially and you need immediate results. This is the natural mode for production trading systems that can't wait for batch processing.

**Trade-off:** Higher per-calculation cost (2-6x slower than Span) due to state management and single-value processing overhead. The flip side: predictable latency regardless of lookback period, which matters more in real-time systems than raw throughput.

#### Eventing Mode: Reactive Architectures

Extends streaming mode with full event infrastructure. Indicators raise events when values change, when warmup completes (`IsHot`), or when significant conditions occur. Build reactive chains where one indicator's output triggers another's calculation, creating complex analytical pipelines that respond to market conditions.

**When to use:** Complex trading systems with conditional logic ("calculate indicator B only when indicator A crosses threshold X"), risk management systems that react to volatility changes, or any architecture where indicators need to communicate state changes rather than just return values.

**Trade-off:** Event infrastructure adds 5-15x overhead compared to Span mode. You're paying for flexibility—the ability to build sophisticated reactive systems without manually checking every indicator's state on every update. Whether this cost is worth it depends on your architecture's complexity.

#### Choosing the Right Mode

The performance hierarchy is clear: **Span** > **Batch** > **Streaming** > **Eventing**. But faster isn't always better. A backtesting engine running historical analysis benefits from Span mode's raw speed. A live trading system needs Streaming mode's state management even though it's slower. An event-driven risk system justifies Eventing mode's overhead for the architectural benefits.

Most systems use multiple modes: Span or Batch for historical analysis and strategy validation, Streaming for live trading. The modes share identical mathematical implementations — you get the same calculated results regardless of mode. The difference is how you interact with the calculation, not what gets calculated.

## The Evidence

Performance claims require measurement. We benchmark QuanTAlib against established libraries: TA-Lib and Tulip (industry-standard C libraries accessed via P/Invoke), Skender.Stock.Indicators and Ooples.FinancialIndicators (popular .NET implementations).

All benchmark tests process 500,000 bars with period 220 — sufficient scale to expose algorithmic inefficiencies and realistic parameters for practical analysis.

**Test environment:** .NET 10.0 with AOT compilation on hardware supporting AVX-512 instructions. These results represent what current-generation server CPUs achieve in production.

### Simple Moving Average (SMA)

QuanTAlib's Span mode calculates 500,000 SMA values in 318 microseconds with zero memory allocations. That's 0.64 nanoseconds per value. For context, a single L1 cache access takes approximately 1 nanosecond on modern CPUs — we're calculating moving averages faster than fetching data from the nearest cache level.

| Library | Mean Time | Allocations | Relative Speed |
|---------|-----------|-------------|----------------|
| **QuanTAlib (Span)** | **318.3 μs** | **0 B** | **1.00x (baseline)** |
| TA-Lib | 356.4 μs | 34 B | 1.12x slower |
| Tulip | 359.3 μs | 0 B | 1.13x slower |
| Skender | 71,277 μs | 50.8 MB | 224x slower |
| Ooples | 500,793 μs | 151 MB | 1,573x slower |

### Exponential Moving Average (EMA)

QuanTAlib matches C library performance at 711 microseconds — within measurement error of Tulip's 708μs and TA-Lib's 713μs. Pure C# matching heavily optimized C code demonstrates what modern .NET achieves when you align memory layouts with hardware capabilities.

| Library | Mean Time | Allocations | Relative Speed |
|---------|-----------|-------------|----------------|
| **QuanTAlib (Span)** | **711.0 μs** | **0 B** | **1.00x** |
| TA-Lib | 712.9 μs | 36 B | 1.00x slower |
| Tulip | 708.1 μs | 0 B | 1.00x faster |
| Skender | 31,393 μs | 50.8 MB | 44x slower |
| Ooples | 18,860 μs | 79.3 MB | 27x slower |

### Weighted Moving Average (WMA)

QuanTAlib's WMA beats both C libraries — 296 microseconds versus Tulip's 372μs and TA-Lib's 360μs. This isn't a measurement error. Pure C# with proper SIMD vectorization outperforms C code that predates AVX-512 optimizations.

| Library | Mean Time | Allocations | Relative Speed |
|---------|-----------|-------------|----------------|
| **QuanTAlib (Span)** | **296.0 μs** | **0 B** | **1.00x (baseline)** |
| TA-Lib | 360.0 μs | 34 B | 1.22x slower |
| Tulip | 372.1 μs | 0 B | 1.26x slower |
| Skender | 103,254 μs | 50.8 MB | 349x slower |
| Ooples | 73,983 μs | 70.9 MB | 250x slower |

### Hull Moving Average (HMA)

HMA requires multiple moving average calculations — traditionally expensive. QuanTAlib processes 500,000 bars in 1,008 microseconds. Tulip takes 2,266 microseconds. Skender requires 251,694 microseconds. (TALib doesn't include HMA calculation) That's a 2.25x improvement over optimized C and a 250x improvement over standard .NET implementations.

| Library | Mean Time | Allocations | Relative Speed |
|---------|-----------|-------------|----------------|
| **QuanTAlib (Span)** | **1,007.8 μs** | **0 B** | **1.00x (baseline)** |
| TA-Lib | -- | -- | -- |
| Tulip | 2,266.0 μs | 152 B | 2.25x slower |
| Skender | 251,694 μs | 235.9 MB | 250x slower |
| Ooples | 123,234 μs | 108.7 MB | 122x slower |

### Zero-Allocation Execution

Notice the allocation column. QuanTAlib's Span mode allocates zero bytes during calculation. No garbage collection pauses, no memory pressure, no non-deterministic latency spikes. When processing thousands of indicators across hundreds of symbols, this matters — system's behavior becomes predictable.

### Multiple Operating Modes Performance

The benchmarks above show Span mode. Here's how all four modes compare using EMA as representative:

| QuanTAlib Mode | Mean Time | Allocations | Use Case |
|----------------|-----------|-------------|----------|
| Span | 711.0 μs | 0 B | Maximum speed, batch processing |
| Streaming | 721.9 μs | 44 B | Real-time updates, minimal overhead |
| Batch (TSeries) | 1,311.7 μs | 8.0 MB | Time-aligned series with metadata |
| Eventing | 2,928.4 μs | 16.8 MB | Reactive architectures with event infrastructure |

Even QuanTAlib's slowest mode (Eventing with complete event infrastructure and 16MB of allocations) processes 500,000 EMA values in 3 milliseconds — faster than Ooples' 19 milliseconds and Skender's 31 milliseconds for the same calculation.

### What This Means Practically

Processing 100 symbols with 20 indicators each in streaming mode requires approximately 15ms total computation time per bar update. System will spend more time deserializing market data from network protocols than calculating indicators.

The numbers reveal something important: QuanTAlib isn't just fast for a C# library. It's competitive with heavily optimized C implementations and exceeds them when the algorithm benefits from modern SIMD instructions that those C libraries haven't been updated to use.

Correctness matters more than speed. Every indicator is validated against the original research papers and cross-checked with established libraries. When implementations disagree, differences are documented. For example, Wilder's original RSI specification differs slightly from the TA-Lib implementation — we follow Wilder's 1978 paper and note where other libraries made different choices.

## Practical Considerations

QuanTAlib works with [Quantower](https://www.quantower.com/), NinjaTrader, QuantConnect, and other C#-based trading platforms. The library targets .NET 8.0, 9.0, and 10.0. SIMD acceleration requires hardware with AVX or SSE support, which includes essentially every processor manufactured since 2011. The performance improvements are substantial enough that running on hardware without SIMD support means accepting 5-8x slower execution.

Memory usage scales with the number of active indicators and their lookback periods. A typical setup (20-30 indicators across 100 symbols) requires approximately 50MB. This fits comfortably in L3 cache on modern CPUs, enabling the high-speed memory access patterns that make O(1) streaming performance possible.

Each indicator includes unit tests for edge cases (insufficient data, NaN inputs, zero-length series) and validation tests comparing results against reference implementations. When you find a bug—and you will, because all software has bugs—the test infrastructure makes fixes verifiable and prevents regressions.

## Getting Started

Install from NuGet:

```bash
dotnet add package QuanTAlib
```

Start with the simpler indicators (EMA, RSI, BBANDS) to understand the streaming model. The exotic stuff (Jurik dark arts indicators, Ehlers arcane magic calculations) can wait until you need them and understand them.

## Contributing

Contributions that add indicators, improve performance, or fix bugs are welcome. Each indicator should include:

1. Core implementation maintaining O(1) streaming complexity if possible
2. Unit tests covering edge cases and initialization behavior
3. Validation tests against at least one reference library (TA-Lib, Tulip, Skender Indicators)
4. Documentation with mathematical formulas and parameter guidance
