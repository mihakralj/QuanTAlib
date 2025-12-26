# Benchmarks

Performance claims require measurement. QuanTAlib is benchmarked against established libraries: TA-Lib and Tulip (industry-standard C libraries accessed via P/Invoke), Skender.Stock.Indicators and Ooples.FinancialIndicators (popular .NET implementations).

## Test Environment

- **Data**: 500,000 bars
- **Parameters**: Period 220 (sufficient scale to expose algorithmic inefficiencies)
- **Framework**: NET 10.0.0 (10.0.25.52411), X64 AOT AVX-512F+CD+BW+DQ+VL+VBMI
- **Hardware**: AMD Ryzen 9 9950X 16-Core Processor (4.30 GHz) supporting **AVX-512** SIMD and **FMA** (Fused Multiply-Add)

These results represent what current-generation server CPUs achieve in production.

## SIMD (Single Instruction, Multiple Data) and FMA (Fused Multiply-Add)

The library automatically detects and utilizes the highest available instruction set (AVX-512, AVX2, or NEON). This allows processing multiple data points simultaneously:

- **AVX-512**: Processes 8 `double` values per cycle (512-bit vectors).
- **AVX2**: Processes 4 `double` values per cycle (256-bit vectors).
- **NEON**: Processes 2 `double` values per cycle (128-bit vectors).

QuanTAlib also leverages **Fused Multiply-Add (FMA)** instructions (FMA3) wherever possible - for scalar and vector math. FMA performs a multiplication and addition in a single CPU cycle (`a * b + c`) with a single rounding step. This provides two distinct advantages:

1. **Throughput**: Doubling the floating-point operations per cycle compared to separate multiply and add instructions.
2. **Precision**: Reducing cumulative rounding errors in iterative calculations like moving averages and standard deviations.

In algorithms heavily reliant on convolution or dot products (like WMA, LinReg, or Correlation), SIMD and FMA usage contributes significantly to the observed speedup over traditional implementations.

## Benchmark Results

### Simple Moving Average (SMA)

QuanTAlib's Span mode calculates 500,000 SMA values in 319 microseconds with zero memory allocations. That's **0.64 nanoseconds per value**. For context, a single L1 cache access takes approximately 1 nanosecond on modern CPUs, so moving averages are being calculated faster than data can be fetched from the nearest cache level.

| Library | Mean Time | Allocations | Relative Speed |
| ------- | --------- | ----------- | -------------- |
| **QuanTAlib (Span)** | **318.5 μs** | **0 B** | **1.00x (baseline)** |
| TA-Lib | 370.7 μs | 34 B | 1.16x slower |
| Tulip | 362.5 μs | 0 B | 1.14x slower |
| Skender | 75,147 μs | 50.8 MB | 236x slower |
| Ooples | 482,786 μs | 151 MB | 1,516x slower |

### Exponential Moving Average (EMA)

QuanTAlib outperforms C library performance at 356 microseconds — significantly faster than Tulip's 709μs and TA-Lib's 707μs. Using **Fused Multiply-Add (FMA)** instructions for the hot path of EMA beats heavily optimized C code.

| Library | Mean Time | Allocations | Relative Speed |
| ------- | --------- | ----------- | -------------- |
| **QuanTAlib (Span)** | **355.9 μs** | **0 B** | **1.00x (baseline)** |
| TA-Lib | 707.3 μs | 36 B | 1.99x slower |
| Tulip | 709.1 μs | 0 B | 1.99x slower |
| Skender | 31,085 μs | 50.8 MB | 87x slower |
| Ooples | 18,454 μs | 79.3 MB | 52x slower |

### Weighted Moving Average (WMA)

QuanTAlib's WMA beats both C libraries — 313 microseconds versus Tulip's 377μs and TA-Lib's 364μs. This isn't a measurement error. Pure C# with proper SIMD vectorization outperforms C code that predates AVX-512 optimizations.

| Library | Mean Time | Allocations | Relative Speed |
| ------- | --------- | ----------- | -------------- |
| **QuanTAlib (Span)** | **312.7 μs** | **0 B** | **1.00x (baseline)** |
| TA-Lib | 364.2 μs | 34 B | 1.16x slower |
| Tulip | 376.6 μs | 0 B | 1.20x slower |
| Skender | 103,489 μs | 50.8 MB | 331x slower |
| Ooples | 73,595 μs | 70.9 MB | 235x slower |

### Hull Moving Average (HMA)

HMA requires multiple moving average calculations — traditionally expensive. QuanTAlib processes 500,000 bars in 963 microseconds. Tulip takes 2,272 microseconds. Skender requires 270,665 microseconds. (TALib doesn't include HMA calculation) That's a 2.36x improvement over optimized C and a 281x improvement over standard .NET implementations.

| Library | Mean Time | Allocations | Relative Speed |
| ------- | --------- | ----------- | -------------- |
| **QuanTAlib (Span)** | **963.3 μs** | **0 B** | **1.00x (baseline)** |
| TA-Lib | -- | -- | -- |
| Tulip | 2,272.2 μs | 153 B | 2.36x slower |
| Skender | 270,665 μs | 235.9 MB | 281x slower |
| Ooples | 120,369 μs | 108.7 MB | 125x slower |

## Multi-mode Comparison

The benchmarks above show Span mode. Here's how all four modes compare using EMA as representative:

| QuanTAlib Mode | Mean Time | Allocations | Use Case |
| -------------- | --------- | ----------- | -------- |
| Span | 355.9 μs | 0 B | Maximum speed, batch processing |
| Streaming | 464.2 μs | 42 B | Real-time updates, minimal overhead |
| Batch (TSeries) | 943.3 μs | 8.0 MB | Time-aligned series with metadata |
| Eventing | 3,055.5 μs | 16.8 MB | Reactive architectures with event infrastructure |

Even QuanTAlib's slowest mode (Eventing with complete event infrastructure and 16MB of allocations) processes 500,000 EMA values in 3.1 milliseconds — faster than Ooples' 18 milliseconds and Skender's 31 milliseconds for the same calculation.

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
