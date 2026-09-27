[![Codacy grade](https://app.codacy.com/project/badge/Grade/c8be6c08f5514e95b84d37e661a6ec27)](https://app.codacy.com/gh/mihakralj/QuanTAlib/dashboard?utm_source=gh&utm_medium=referral&utm_content=&utm_campaign=Badge_grade)
[![codecov](https://codecov.io/gh/mihakralj/QuanTAlib/branch/main/graph/badge.svg?style=flat-square&token=YNMJRGKMTJ?style=flat-square)](https://codecov.io/gh/mihakralj/QuanTAlib)
[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=mihakralj_QuanTAlib&metric=security_rating)](https://sonarcloud.io/summary/new_code?id=mihakralj_QuanTAlib)
[![CodeFactor](https://www.codefactor.io/repository/github/mihakralj/quantalib/badge/main)](https://www.codefactor.io/repository/github/mihakralj/quantalib/overview/main)
[![Nuget](https://img.shields.io/nuget/v/QuanTAlib?style=flat-square)](https://www.nuget.org/packages/QuanTAlib/)
![GitHub last commit](https://img.shields.io/github/last-commit/mihakralj/QuanTAlib)
[![Nuget](https://img.shields.io/nuget/dt/QuanTAlib?style=flat-square)](https://www.nuget.org/packages/QuanTAlib/)

[![.NET](https://img.shields.io/badge/.NET-10.0-blue?style=flat-square)](https://dotnet.microsoft.com/en-us/download/dotnet)
[![Indicators](https://img.shields.io/badge/%23%20Indicators-447-blue?style=flat-square)](lib/_index.md)

# QuanTAlib

447 technical indicators. One library. Brutal architectural trade-offs for absolute speed.

[⭐ Documentation pages →](https://mihakralj.github.io/QuanTAlib/)

QuanTAlib exists because I got tired of validating other people's indicators. Every implementation is cross-checked against TA-Lib, Tulip, Skender, and Pandas-TA. Where they disagree, we went to the original papers. Where the papers disagree, we picked the math that doesn't lie.

Same indicators, same results: **C#**, **Python**, and **PineScript**.

### How Fast?

C# native AOT-compiled code spits out half a million bars of SMA in 288 microseconds. That is faster (per value) than a single L1 cache miss on any fancy new CPU. Achieved by trading object allocation for contiguous memory spans, slapping Fused Multiply-Add (FMA) on everything, and forcing SIMD vectorized paths. You want speed? We dictate the heap.

| Library | SMA (500K bars) | Allocations | Relative time |
| :--- | ---: | ---: | :--- |
| **QuanTAlib** | **288 μs** | **0 B** | **1×** |
| TA-Lib (C++)| 365 μs | 32 B | 1.3× slower |
| Wickra (Rust)| 1,562 μs | 4 MB | 5.4× slower |
| Skender (C#)| 68,378 μs | 42 MB | 238× slower |
| Ooples (C#)| 346,187 μs | 151 MB | 1,202× slower |

Measured with BenchmarkDotNet v0.15.3 on .NET 10 x64 (AVX-512), 500K bars, period 220. [Benchmark project →](perf/perf.csproj)

Skender and Ooples are measured on their list-based APIs; QuanTAlib on its zero-allocation span API. Same input, same output — different memory shape.

[Full benchmarks →](docs/benchmarks.md)

## Install

| Platform | Install | Guide |
| :--- | :--- | :--- |
| **.NET** | `dotnet add package QuanTAlib` | [Architecture](docs/architecture.md) . [API Reference](docs/api.md) |
| **Python** | `pip install quantalib` | [Python Guide](docs/python.md) |
| **PineScript v6** | Copy-paste to TradingView | [PineScript Guide](docs/pinescript.md) |

`pip install quantalib` ships a compiled native wheel — no source build, no C++ toolchain:

| Platform | Wheel tag |
| :--- | :--- |
| Windows x64 | `win_amd64` |
| Linux x64 | `manylinux_2_17_x86_64` |
| Linux arm64 (AWS Graviton) | `manylinux_2_17_aarch64` |
| macOS arm64 (Apple Silicon) | `macosx_11_0_arm64` |
| macOS x64 (Intel) | `macosx_10_13_x86_64` |

## Show Me the Code

### C# Streaming (Real-time incoming data, value by value)

```csharp
using QuanTAlib;

var sma = new Sma(period: 14);
var result = sma.Update(110.4);

if (result.IsHot)
    Console.WriteLine($"SMA: {result.Value}");
```

State lives inside the indicator. No list of historic bars. No LINQ chains allocating their way to thermal throttling. Call `.Update()`, get answer.

### C# — batch (500K bars in microseconds)

```csharp
double[] prices = LoadHistoricalData();
double[] results = new double[prices.Length];

Sma.Batch(prices.AsSpan(), results.AsSpan(), period: 14);
```

Contiguous memory. AVX-512 vectorization. The Garbage Collector sleeps through the whole thing and nobody wakes it.


### Python

```python
import quantalib as qtl
import numpy as np

prices = np.random.default_rng(42).normal(100, 2, size=500_000)
sma = qtl.sma(prices, period=14)       # 447 indicators, similar syntax
```

Works with NumPy, pandas, polars, and PyArrow. NativeAOT compiled, ships as a binary. No CLR runtime dragged along for the ride.  
[Full Python guide →](docs/python.md)

### PineScript

Every indicator ships as a standalone .pine file. Open it. Copy it. Paste it into TradingView. No magic, no dependencies, just math that matches the C# and Python versions to the 10th decimal.  
[Full PineScript guide →](docs/pinescript.md)

---

## 447 Indicators

| Category | Count | What It Measures | Examples |
| :--- | :---: | :--- | :--- |
| [**Core**](lib/core/_index.md) | 8 | Price transforms, building blocks | AVGPRICE, MEDPRICE, TYPPRICE, HA |
| [**Trends (FIR)**](lib/trends_FIR/_index.md) | 33 | Finite impulse response averages | SMA, WMA, HMA, ALMA, TRIMA, LSMA |
| [**Trends (IIR)**](lib/trends_IIR/_index.md) | 36 | Infinite impulse response averages | EMA, DEMA, TEMA, T3, JMA, KAMA, VIDYA |
| [**Filters**](lib/filters/_index.md) | 39 | Signal processing, noise reduction | Kalman, Butterworth, Gaussian, Savitzky-Golay |
| [**Oscillators**](lib/oscillators/_index.md) | 61 | Bounded/centered oscillators | RSI, MACD, Stochastic, CCI, Fisher, Williams %R |
| [**Dynamics**](lib/dynamics/_index.md) | 27 | Trend strength and direction | ADX, Aroon, SuperTrend, Ichimoku, Vortex |
| [**Momentum**](lib/momentum/_index.md) | 20 | Speed of price changes | ROC, Momentum, Velocity, TSI, Qstick |
| [**Volatility**](lib/volatility/_index.md) | 26 | Price variability | ATR, Bollinger Width, Historical Vol, True Range |
| [**Volume**](lib/volume/_index.md) | 28 | Trading activity | OBV, VWAP, MFI, CMF, ADL, Force Index |
| [**Statistics**](lib/statistics/_index.md) | 37 | Statistical measures | Correlation, Variance, Skewness, Z-Score |
| [**Channels**](lib/channels/_index.md) | 24 | Price boundaries | Bollinger Bands, Keltner, Donchian |
| [**Cycles**](lib/cycles/_index.md) | 18 | Cycle analysis | Hilbert Transform, Homodyne, Ehlers Sine Wave |
| [**Reversals**](lib/reversals/_index.md) | 15 | Pattern detection | Pivot Points, Fractals, Swings |
| [**Forecasts**](lib/forecasts/_index.md) | 1 | Predictive indicators | Time Series Forecast |
| [**Errors**](lib/errors/_index.md) | 26 | Error metrics, loss functions | RMSE, MAE, MAPE, SMAPE, R² |
| [**Numerics**](lib/numerics/_index.md) | 36 | Mathematical transforms | Log, Exp, Sigmoid, Normalize, FFT |
| [**Signals**](lib/signals/_index.md) | 12 | Strategy primitives: predicates, triggers, guards | ABOVE, BELOW, ISRISING, ISFALLING |

**[Browse all 447 indicators →](lib/_index.md)**

## Architecture (the short version)

**Streaming mode:** O(1) per update. Fixed memory. State maintained internally. Feed it ticks, get answers. No history buffer, no lookback window allocation, no *please pass me the last 200 bars so I can warm-up* nonsense.

**Batch mode:** Structure-of-Arrays memory layout. SIMD vectorized. FMA everywhere the hardware allows. Processes contiguous `Span<double>` with zero heap allocation. Your profiler will be confused by the absence of GC pressure.

**Dual-state management:** Bars can be corrected mid-stream (because real-time feeds are liars). The indicator tracks both confirmed and pending state so corrections don't require a full recalculation.

[Full architecture docs →](docs/architecture.md)

## Validation

Every indicator is cross-validated against reference implementations using Geometric Brownian Motion (GBM) generated test data. Not cherry-picked sine waves. Geometric Brownian Motion with realistic drift and volatility, because indicators that only work on textbook inputs are not indicators: they are demos.

[Validation matrices →](docs/validation.md)  
[Error metrics →](docs/errors.md)  
[Trend comparison →](docs/trendcomparison.md)

## Documentation

**Architecture & API:** [Architecture](docs/architecture.md) · [API Reference](docs/api.md) · [Usage Patterns](docs/usage.md) · [Integration](docs/integration.md) (Quantower, *NinjaTrader*, *QuantConnect*)

**Analysis:** [Benchmarks](docs/benchmarks.md) · [Validation](docs/validation.md) · [MA Qualities](docs/ma-qualities.md) · [Glossary](docs/glossary.md)

**Code Quality Assurance:** [NDepend](https://www.ndepend.com/) · [Codacy](https://app.codacy.com/gh/mihakralj/QuanTAlib/dashboard) · [SonarCloud](https://sonarcloud.io/summary/new_code?id=mihakralj_QuanTAlib) · [CodeFactor](https://www.codefactor.io/repository/github/mihakralj/quantalib/overview/main)

## Stability

In development since 2022. Four years, 700+ commits, 447 indicators — every one cross-validated against at least three independent implementations on realistic GBM data. The public API is frozen: breaking changes fail the build, so your code doesn't wake up one morning to a surprise. Versioning is SemVer with a one-minor-version `[Obsolete]` deprecation window — deprecate today, remove next release, no guillotine.

Bugs get triaged fast. [Open an issue](https://github.com/mihakralj/QuanTAlib/issues) — every report is read, and real bugs jump the queue ahead of the Dependabot noise. Questions, feature ideas, and "why does this number look wrong" all land in the same place. If something is broken and you say nothing, it stays broken. If you say something, it gets fixed.

**Where to ask:** [GitHub Issues](https://github.com/mihakralj/QuanTAlib/issues) — bugs, questions, and ideas all live there. [Versioning policy →](docs/versioning.md)

## License

[Apache 2.0](LICENSE). Not MIT. Not BSD. [Deliberately →](docs/license.md)
