# Benchmarks

Performance claims require measurement. QuanTAlib is benchmarked against established libraries: TA-Lib and Tulip (industry-standard C libraries accessed via P/Invoke), Skender.Stock.Indicators and Ooples.FinancialIndicators (popular .NET implementations).

## Test Environment

- **Framework**: .NET 10.0 with AOT compilation
- **Hardware**: Modern CPU supporting AVX-512 instructions
- **Data**: 500,000 bars
- **Parameters**: Period 220 (sufficient scale to expose algorithmic inefficiencies)

These results represent what current-generation server CPUs achieve in production.

## Benchmark Results

### Simple Moving Average (SMA)

QuanTAlib's Span mode calculates 500,000 SMA values in 318 microseconds with zero memory allocations. That's 0.64 nanoseconds per value. For context, a single L1 cache access takes approximately 1 nanosecond on modern CPUs, so moving averages are being calculated faster than data can be fetched from the nearest cache level.

| Library | Mean Time | Allocations | Relative Speed |
| ------- | --------- | ----------- | -------------- |
| **QuanTAlib (Span)** | **318.3 μs** | **0 B** | **1.00x (baseline)** |
| TA-Lib | 356.4 μs | 34 B | 1.12x slower |
| Tulip | 359.3 μs | 0 B | 1.13x slower |
| Skender | 71,277 μs | 50.8 MB | 224x slower |
| Ooples | 500,793 μs | 151 MB | 1,573x slower |

### Exponential Moving Average (EMA)

QuanTAlib matches C library performance at 711 microseconds — within measurement error of Tulip's 708μs and TA-Lib's 713μs. Pure C# matching heavily optimized C code demonstrates what modern .NET achieves when you align memory layouts with hardware capabilities.

| Library | Mean Time | Allocations | Relative Speed |
| ------- | --------- | ----------- | -------------- |
| **QuanTAlib (Span)** | **711.0 μs** | **0 B** | **1.00x** |
| TA-Lib | 712.9 μs | 36 B | 1.00x slower |
| Tulip | 708.1 μs | 0 B | 1.00x faster |
| Skender | 31,393 μs | 50.8 MB | 44x slower |
| Ooples | 18,860 μs | 79.3 MB | 27x slower |

### Weighted Moving Average (WMA)

QuanTAlib's WMA beats both C libraries — 296 microseconds versus Tulip's 372μs and TA-Lib's 360μs. This isn't a measurement error. Pure C# with proper SIMD vectorization outperforms C code that predates AVX-512 optimizations.

| Library | Mean Time | Allocations | Relative Speed |
| ------- | --------- | ----------- | -------------- |
| **QuanTAlib (Span)** | **296.0 μs** | **0 B** | **1.00x (baseline)** |
| TA-Lib | 360.0 μs | 34 B | 1.22x slower |
| Tulip | 372.1 μs | 0 B | 1.26x slower |
| Skender | 103,254 μs | 50.8 MB | 349x slower |
| Ooples | 73,983 μs | 70.9 MB | 250x slower |

### Hull Moving Average (HMA)

HMA requires multiple moving average calculations — traditionally expensive. QuanTAlib processes 500,000 bars in 1,008 microseconds. Tulip takes 2,266 microseconds. Skender requires 251,694 microseconds. (TALib doesn't include HMA calculation) That's a 2.25x improvement over optimized C and a 250x improvement over standard .NET implementations.

| Library | Mean Time | Allocations | Relative Speed |
| ------- | --------- | ----------- | -------------- |
| **QuanTAlib (Span)** | **1,007.8 μs** | **0 B** | **1.00x (baseline)** |
| TA-Lib | -- | -- | -- |
| Tulip | 2,266.0 μs | 152 B | 2.25x slower |
| Skender | 251,694 μs | 235.9 MB | 250x slower |
| Ooples | 123,234 μs | 108.7 MB | 122x slower |

## Multi-mode Comparison

The benchmarks above show Span mode. Here's how all four modes compare using EMA as representative:

| QuanTAlib Mode | Mean Time | Allocations | Use Case |
| -------------- | --------- | ----------- | -------- |
| Span | 711.0 μs | 0 B | Maximum speed, batch processing |
| Streaming | 721.9 μs | 44 B | Real-time updates, minimal overhead |
| Batch (TSeries) | 1,311.7 μs | 8.0 MB | Time-aligned series with metadata |
| Eventing | 2,928.4 μs | 16.8 MB | Reactive architectures with event infrastructure |

Even QuanTAlib's slowest mode (Eventing with complete event infrastructure and 16MB of allocations) processes 500,000 EMA values in 3 milliseconds — faster than Ooples' 19 milliseconds and Skender's 31 milliseconds for the same calculation.

## Methodology

[BenchmarkDotNet](https://benchmarkdotnet.org/) is used for all performance testing. This ensures:

- Warmup iterations to stabilize JIT compilation
- Statistical analysis of results (mean, standard deviation)
- Memory allocation tracking
- Environment isolation

## How to Run Benchmarks Yourself

You can run the benchmarks on your own hardware to verify these results.

1. Clone the repository:

    ```bash
    git clone https://github.com/mihakralj/QuanTAlib.git
    cd QuanTAlib
    ```

2. Navigate to the performance project:

    ```bash
    cd perf
    ```

3. Run the benchmarks:

    ```bash
    dotnet run -c Release
    ```

    *Note: Benchmarks must be run in Release configuration to enable optimizations.*
