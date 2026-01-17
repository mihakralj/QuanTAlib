# Benchmarks

Performance claims without measurement: just marketing. QuanTAlib benchmarks against established libraries: TA-Lib and Tulip (industry-standard C libraries accessed via P/Invoke), Skender.Stock.Indicators and Ooples.FinancialIndicators (popular .NET implementations).

## Test Environment

| Component | Specification |
| :-------- | :------------ |
| Data Size | 500,000 bars |
| Period | 220 (sufficient to expose algorithmic inefficiencies) |
| Framework | .NET 10.0.2 (10.0.225.61305), X64 RyuJIT |
| SIMD | AVX-512F+CD+BW+DQ+VL+VBMI |
| Benchmarking | BenchmarkDotNet v0.14.0 |

These results represent what current-generation CPUs achieve in production. Your mileage varies with older hardware, but relative performance ratios hold.

## SIMD and FMA: The Speed Multipliers

The library auto-detects and exploits the highest available instruction set. Processing multiple data points per clock cycle changes everything:

| Instruction Set | Vector Width | Doubles/Cycle | Hardware |
| :-------------- | -----------: | ------------: | :------- |
| AVX-512 | 512-bit | 8 | Modern server/desktop CPUs |
| AVX2 | 256-bit | 4 | Most x86-64 since 2013 |
| NEON | 128-bit | 2 | ARM processors (Apple Silicon, Raspberry Pi) |
| Scalar fallback | 64-bit | 1 | Everything else |

**Fused Multiply-Add (FMA)** performs `a * b + c` in a single cycle with one rounding step. Two advantages compound:

1. **Throughput**: Double the floating-point operations per cycle versus separate multiply/add
2. **Precision**: Cumulative rounding errors shrink in iterative calculations

In convolution-heavy algorithms (WMA, LinReg, Correlation), SIMD + FMA explains most of the observed speedup. The CPU stops being the bottleneck; memory bandwidth takes over.

## Benchmark Results

### Simple Moving Average (SMA)

QuanTAlib Span mode: 500,000 SMA values in 328 microseconds. Zero allocations. That works out to **0.66 nanoseconds per value**. For perspective: a single L1 cache access takes approximately 1 nanosecond. Moving averages calculating faster than cache fetch.

| Library | Mean Time | Allocations | Relative Speed |
| :------ | --------: | ----------: | :------------- |
| **QuanTAlib (Span)** | **327.9 μs** | **0 B** | **baseline** |
| TA-Lib | 365.4 μs | 32 B | 1.11× slower |
| Tulip | 370.2 μs | 0 B | 1.13× slower |
| Skender | 68,436 μs | 42.0 MB | 209× slower |
| Ooples | 347,453 μs | 151.3 MB | 1,060× slower |

Skender and Ooples allocate 42-151 MB for what should be a stateless calculation. Garbage collector wakes up, stretches, and ruins everyone's day.

### Exponential Moving Average (EMA)

QuanTAlib at 421 microseconds outperforms both C libraries: Tulip at 709 μs, TA-Lib at 709 μs. Beating heavily optimized C with managed code sounds improbable. The secret: **FMA instructions** on the hot path. Those C libraries predate AVX-512 optimizations by a decade.

| Library | Mean Time | Allocations | Relative Speed |
| :------ | --------: | ----------: | :------------- |
| **QuanTAlib (Span)** | **421.0 μs** | **0 B** | **baseline** |
| TA-Lib | 708.8 μs | 32 B | 1.68× slower |
| Tulip | 709.1 μs | 0 B | 1.68× slower |
| Ooples | 14,509 μs | 79.3 MB | 34× slower |
| Skender | 26,612 μs | 42.0 MB | 63× slower |

The 1.7× speedup over C libraries represents what happens when old code meets new silicon. Modern instruction sets exist; using them helps.

### Weighted Moving Average (WMA)

QuanTAlib WMA: 302 microseconds. Tulip: 378 μs. TA-Lib: 368 μs. Not a measurement error. Pure C# with proper SIMD vectorization beating C code that predates AVX-512 optimizations.

| Library | Mean Time | Allocations | Relative Speed |
| :------ | --------: | ----------: | :------------- |
| **QuanTAlib (Span)** | **302.4 μs** | **0 B** | **baseline** |
| TA-Lib | 367.6 μs | 32 B | 1.22× slower |
| Tulip | 378.0 μs | 0 B | 1.25× slower |
| Ooples | 75,169 μs | 70.9 MB | 249× slower |
| Skender | 100,253 μs | 42.0 MB | 332× slower |

WMA involves weighted summation: dot product territory. SIMD shines here. Eight multiplications per cycle instead of one.

### Hull Moving Average (HMA)

HMA requires multiple moving average calculations: traditionally expensive. QuanTAlib processes 500,000 bars in 983 microseconds. Tulip: 2,173 μs. Skender: 246,653 μs. TA-Lib lacks HMA implementation entirely.

| Library | Mean Time | Allocations | Relative Speed |
| :------ | --------: | ----------: | :------------- |
| **QuanTAlib (Span)** | **983.4 μs** | **0 B** | **baseline** |
| Tulip | 2,173.2 μs | 138 B | 2.21× slower |
| Ooples | 122,171 μs | 108.7 MB | 124× slower |
| Skender | 246,653 μs | 200.8 MB | 251× slower |
| TA-Lib | — | — | not implemented |

A 251× improvement over standard .NET implementations. Compound calculations expose implementation quality: inefficiencies multiply with each nested operation.

### Chaikin Oscillator (ADOSC)

Multi-input indicator using high, low, close, and volume. Tests OHLCV data handling efficiency.

| Library | Mean Time | Allocations | Relative Speed |
| :------ | --------: | ----------: | :------------- |
| **QuanTAlib (Span)** | **640.1 μs** | **0 B** | **baseline** |
| TA-Lib | 675.7 μs | 40 B | 1.06× slower |
| Tulip | 822.7 μs | 0 B | 1.29× slower |
| Ooples | 107,730 μs | 569.9 MB | 168× slower |
| Skender | 116,678 μs | 194.0 MB | 182× slower |

## Multi-Mode Comparison

Span mode represents maximum speed. Production code often needs different trade-offs. Here's how all four modes compare using EMA:

| QuanTAlib Mode | Mean Time | Allocations | Trade-off |
| :------------- | --------: | ----------: | :-------- |
| Span | 421.0 μs | 0 B | Maximum throughput, batch processing |
| Streaming | 1,528.7 μs | 177 B | Real-time updates, minimal overhead |
| Batch (TSeries) | 1,777.8 μs | 8.0 MB | Time-aligned series with metadata |
| Eventing | 3,463.6 μs | 16.8 MB | Reactive architectures with event infrastructure |

Even QuanTAlib's slowest mode (Eventing with complete event infrastructure, 16.8 MB allocations) processes 500,000 EMA values in 3.5 milliseconds. Still faster than Ooples at 14.5 ms and Skender at 26.6 ms for identical calculation. The "slow" path here beats other libraries' only path.

## Methodology

[BenchmarkDotNet](https://benchmarkdotnet.org/) handles all performance testing. The framework provides:

| Feature | Why It Matters |
| :------ | :------------- |
| Warmup iterations | Stabilizes JIT compilation before measurement |
| Statistical analysis | Mean, standard deviation, confidence intervals |
| Memory tracking | Allocation profiling per iteration |
| Environment isolation | Process affinity, GC control, noise reduction |

Raw timing loops lie. BenchmarkDotNet tells the truth, even when the truth hurts.

## Running Benchmarks Locally

Verify these results on your own hardware. Skepticism is healthy.

Clone the repository:

```bash
git clone https://github.com/mihakralj/QuanTAlib.git
cd QuanTAlib
```

Navigate to the performance project:

```bash
cd perf
```

Run benchmarks in Release configuration:

```bash
dotnet run -c Release
```

Debug builds include instrumentation that destroys performance measurements. Release configuration or the numbers mean nothing.

Results vary by CPU generation, but relative ratios (QuanTAlib vs competitors) remain consistent across hardware. The architectures that make something fast stay fast; the ones that allocate memory keep allocating.