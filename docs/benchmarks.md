# Benchmarks

Performance claims without measurement: just marketing. QuanTAlib benchmarks against established libraries: TA-Lib (the industry-standard C library accessed via P/Invoke), Wickra (a Rust-core streaming library accessed via FFI), Skender.Stock.Indicators and Ooples.FinancialIndicators (popular .NET implementations).

## Test Environment

| Component | Specification |
| :-------- | :------------ |
| Data Size | 500,000 bars |
| Period | 220 (sufficient to expose algorithmic inefficiencies) |
| Framework | .NET 10.0.12 (10.0.1226.42308), X64 RyuJIT |
| SIMD | AVX-512F+CD+BW+DQ+VL+VBMI |
| Benchmarking | BenchmarkDotNet v0.15.3 |

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

QuanTAlib Span mode: 500,000 SMA values in 288 microseconds. Zero allocations. That works out to **0.58 nanoseconds per value**. For perspective: a single L1 cache access takes approximately 1 nanosecond. Moving averages calculating faster than cache fetch.

| Library | Mean Time | Allocations | Relative Speed |
| :------ | --------: | ----------: | :------------- |
| **QuanTAlib (Span)** | **287.9 μs** | **0 B** | **baseline** |
| TA-Lib | 364.8 μs | 32 B | 1.27× slower |
| Wickra | 1,561.5 μs | 4.0 MB | 5.42× slower |
| Skender | 68,377.5 μs | 42.0 MB | 238× slower |
| Ooples | 346,187.1 μs | 151.3 MB | 1,202× slower |

Skender and Ooples allocate 42-151 MB for what should be a stateless calculation. Garbage collector wakes up, stretches, and ruins everyone's day.

### Exponential Moving Average (EMA)

QuanTAlib at 424 microseconds outperforms both the C library TA-Lib (721 μs) and the Rust-core Wickra (1,628 μs). Beating heavily optimized native code with managed code sounds improbable. The secret: **FMA instructions** on the hot path. Those libraries predate AVX-512 optimizations by a decade.

| Library | Mean Time | Allocations | Relative Speed |
| :------ | --------: | ----------: | :------------- |
| **QuanTAlib (Span)** | **423.8 μs** | **0 B** | **baseline** |
| TA-Lib | 720.8 μs | 32 B | 1.70× slower |
| Wickra | 1,627.5 μs | 4.0 MB | 3.84× slower |
| Ooples | 14,783.5 μs | 79.3 MB | 34.9× slower |
| Skender | 25,939.1 μs | 42.0 MB | 61.2× slower |

The 1.7× speedup over the C library represents what happens when old code meets new silicon. Modern instruction sets exist; using them helps.

### Weighted Moving Average (WMA)

QuanTAlib WMA: 295 microseconds. TA-Lib: 367 μs. Wickra: 1,716 μs. Not a measurement error. Pure C# with proper SIMD vectorization beating C code that predates AVX-512 optimizations.

| Library | Mean Time | Allocations | Relative Speed |
| :------ | --------: | ----------: | :------------- |
| **QuanTAlib (Span)** | **294.7 μs** | **0 B** | **baseline** |
| TA-Lib | 366.5 μs | 32 B | 1.24× slower |
| Wickra | 1,715.6 μs | 4.0 MB | 5.82× slower |
| Ooples | 74,938.8 μs | 70.9 MB | 254× slower |
| Skender | 101,151.5 μs | 42.0 MB | 343× slower |

WMA involves weighted summation: dot product territory. SIMD shines here. Eight multiplications per cycle instead of one.

### Hull Moving Average (HMA)

HMA requires multiple moving average calculations: traditionally expensive. QuanTAlib processes 500,000 bars in 961 microseconds. Wickra: 3,849 μs. Skender: 255,203 μs. TA-Lib lacks HMA implementation entirely.

| Library | Mean Time | Allocations | Relative Speed |
| :------ | --------: | ----------: | :------------- |
| **QuanTAlib (Span)** | **961.4 μs** | **0 B** | **baseline** |
| Wickra | 3,849.1 μs | 4.0 MB | 4.00× slower |
| Ooples | 125,720.8 μs | 108.7 MB | 131× slower |
| Skender | 255,202.5 μs | 200.8 MB | 265× slower |
| TA-Lib | — | — | not implemented |

A 265× improvement over standard .NET implementations. Compound calculations expose implementation quality: inefficiencies multiply with each nested operation.

### Chaikin Oscillator (ADOSC)

Multi-input indicator using high, low, close, and volume. Tests OHLCV data handling efficiency.

| Library | Mean Time | Allocations | Relative Speed |
| :------ | --------: | ----------: | :------------- |
| **QuanTAlib (Span)** | **596.8 μs** | **0 B** | **baseline** |
| TA-Lib | 665.7 μs | 40 B | 1.12× slower |
| Wickra | 2,259.6 μs | 4.0 MB | 3.79× slower |
| Ooples | 108,952.4 μs | 569.9 MB | 183× slower |
| Skender | 134,249.4 μs | 194.0 MB | 225× slower |

### Pearson Correlation

A dual-input statistic computed over two series. QuanTAlib's SIMD batch path (1,338 μs) beats TA-Lib (2,157 μs) — the dual-input case where a C library is left behind, and Skender by 190×.

| Library | Mean Time | Allocations | Relative Speed |
| :------ | --------: | ----------: | :------------- |
| **QuanTAlib (Span)** | **1,337.6 μs** | **0 B** | **baseline** |
| TA-Lib | 2,157.2 μs | 32 B | 1.61× slower |
| Skender | 253,912.5 μs | 1.88 GB | 190× slower |

## Multi-Mode Comparison

Span mode represents maximum speed. Production code often needs different trade-offs. Here's how all four modes compare using EMA:

| QuanTAlib Mode | Mean Time | Allocations | Trade-off |
| :------------- | --------: | ----------: | :-------- |
| Span | 423.8 μs | 0 B | Maximum throughput, batch processing |
| Streaming | 1,536.8 μs | 176 B | Real-time updates, minimal overhead |
| Batch (TSeries) | 2,388.1 μs | 16.0 MB | Time-aligned series with metadata |
| Eventing | 3,381.1 μs | 16.8 MB | Reactive architectures with event infrastructure |

Even QuanTAlib's slowest mode (Eventing with complete event infrastructure, 16.8 MB allocations) processes 500,000 EMA values in 3.4 milliseconds. Still faster than Ooples at 14.8 ms and Skender at 25.9 ms for identical calculation. The "slow" path here beats other libraries' only path.

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
