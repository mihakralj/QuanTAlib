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

## Python Benchmark: quantalib vs pandas-ta

The same indicators benchmarked in C# are also available through Python via NativeAOT shared library + ctypes FFI. This comparison measures the real-world cost of calling QuanTAlib from Python versus using pandas-ta (the most popular pure-Python technical analysis library).

### Test Environment

| Component | Specification |
| :-------- | :------------ |
| Data Size | 500,000 bars |
| Period | 220 |
| Python | 3.12.10 |
| NumPy | 2.2.6 |
| pandas | 3.0.1 |
| pandas-ta | 0.4.71b0 |
| quantalib | 0.8.7 (NativeAOT via ctypes) |

### quantalib vs pandas-ta

| Indicator | quantalib | pandas-ta | Speedup |
| :-------- | --------: | --------: | ------: |
| **SMA** | **1,308 μs** | 64,110 μs | **49×** |
| **EMA** | **1,083 μs** | 4,486 μs | **4.1×** |
| **WMA** | **1,223 μs** | 83,763 μs | **68×** |
| **HMA** | **2,709 μs** | 154,508 μs | **57×** |
| **ADOSC** | **1,655 μs** | 14,821 μs | **9.0×** |
| **SKEW** | **3,987 μs** | 8,077 μs | **2.0×** |

SMA and WMA expose the largest gaps. pandas-ta implements SMA as a rolling window in pure Python/numpy, while quantalib calls the same SIMD-optimized C# code (via NativeAOT) that beats TA-Lib in the C# benchmarks above. WMA at 68× faster reflects the dot-product advantage: eight FMA operations per cycle versus Python's element-at-a-time loop.

### quantalib vs pandas builtins

pandas itself provides optimized C implementations for common rolling operations. Fair comparison:

| Indicator | quantalib | pandas | Speedup |
| :-------- | --------: | -----: | ------: |
| **SMA** | **1,308 μs** | 6,950 μs (rolling.mean) | **5.3×** |
| **EMA** | **1,083 μs** | 3,652 μs (ewm.mean) | **3.4×** |
| **WMA** | **1,223 μs** | 686,561 μs (rolling+apply) | **561×** |
| **CORRELATION** | **20,450 μs** | 35,904 μs (rolling.corr) | **1.8×** |
| **SKEW** | **3,987 μs** | 8,164 μs (rolling.skew) | **2.0×** |

Even against pandas' C-optimized rolling operations, quantalib NativeAOT wins 2-5× on simple indicators. WMA is the extreme case: pandas lacks a native WMA implementation, falling back to `rolling().apply()` with a Python lambda — 561× slower.

### FFI Overhead

The ctypes foreign function interface adds approximately 5-15 microseconds per invocation. At 500,000 bars, this overhead disappears into noise. Below ~100 bars, FFI marshaling dominates and pandas-ta's pure-Python approach wins on latency. Above ~1,000 bars, NativeAOT SIMD takes over decisively.

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

### C# Benchmarks

```bash
cd perf
dotnet run -c Release
```

Debug builds include instrumentation that destroys performance measurements. Release configuration or the numbers mean nothing.

### Python Benchmarks

```bash
cd python
python tests/benchmark.py --bars 500000 --period 220 --iterations 10
```

Adjust `--bars` and `--period` to match your use case. The script degrades gracefully — if pandas-ta or quantalib is unavailable, it benchmarks whatever is installed.

Results vary by CPU generation, but relative ratios (QuanTAlib vs competitors) remain consistent across hardware. The architectures that make something fast stay fast; the ones that allocate memory keep allocating.
