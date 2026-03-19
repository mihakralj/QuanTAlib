[![Codacy grade](https://app.codacy.com/project/badge/Grade/c8be6c08f5514e95b84d37e661a6ec27)](https://app.codacy.com/gh/mihakralj/QuanTAlib/dashboard?utm_source=gh&utm_medium=referral&utm_content=&utm_campaign=Badge_grade)
[![codecov](https://codecov.io/gh/mihakralj/QuanTAlib/branch/main/graph/badge.svg?style=flat-square&token=YNMJRGKMTJ?style=flat-square)](https://codecov.io/gh/mihakralj/QuanTAlib)
[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=mihakralj_QuanTAlib&metric=security_rating)](https://sonarcloud.io/summary/new_code?id=mihakralj_QuanTAlib)
[![CodeFactor](https://www.codefactor.io/repository/github/mihakralj/quantalib/badge/main)](https://www.codefactor.io/repository/github/mihakralj/quantalib/overview/main)
[![Nuget](https://img.shields.io/nuget/v/QuanTAlib?style=flat-square)](https://www.nuget.org/packages/QuanTAlib/)
![GitHub last commit](https://img.shields.io/github/last-commit/mihakralj/QuanTAlib)
[![Nuget](https://img.shields.io/nuget/dt/QuanTAlib?style=flat-sq[![Codacy grade](https://app.codacy.com/project/badge/Grade/c8be6c08f5514e95b84d37e661a6ec27)](https://app.codacy.com/gh/mihakralj/QuanTAlib/dashboard?utm_source=gh&utm_medium=referral&utm_content=&utm_campaign=Badge_grade)
[![codecov](https://codecov.io/gh/mihakralj/QuanTAlib/branch/main/graph/badge.svg?style=flat-square&token=YNMJRGKMTJ?style=flat-square)](https://codecov.io/gh/mihakralj/QuanTAlib)
[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=mihakralj_QuanTAlib&metric=security_rating)](https://sonarcloud.io/summary/new_code?id=mihakralj_QuanTAlib)
[![CodeFactor](https://www.codefactor.io/repository/github/mihakralj/quantalib/badge/main)](https://www.codefactor.io/repository/github/mihakralj/quantalib/overview/main)
[![Nuget](https://img.shields.io/nuget/v/QuanTAlib?style=flat-square)](https://www.nuget.org/packages/QuanTAlib/)
![GitHub last commit](https://img.shields.io/github/last-commit/mihakralj/QuanTAlib)
[![Nuget](https://img.shields.io/nuget/dt/QuanTAlib?style=flat-sq[![Codacy grade](https://app.codacy.com/project/badge/Grade/c8be6c08f5514e95b84d37e661a6ec27)](https://app.codacy.com/gh/mihakralj/QuanTAlib/dashboard?utm_source=gh&utm_medium=referral&utm_content=&utm_campaign=Badge_grade)
[![codecov](https://codecov.io/gh/mihakralj/QuanTAlib/branch/main/graph/badge.svg?style=flat-square&token=YNMJRGKMTJ?style=flat-square)](https://codecov.io/gh/mihakralj/QuanTAlib)
[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=mihakralj_QuanTAlib&metric=security_rating)](https://sonarcloud.io/summary/new_code?id=mihakralj_QuanTAlib)
[![CodeFactor](https://www.codefactor.io/repository/github/mihakralj/quantalib/badge/main)](https://www.codefactor.io/repository/github/mihakralj/quantalib/overview/main)
[![Nuget](https://img.shields.io/nuget/v/QuanTAlib?style=flat-square)](https://www.nuget.org/packages/QuanTAlib/)
![GitHub last commit](https://img.shields.io/github/last-commit/mihakralj/QuanTAlib)
[![Nuget](https://img.shields.io/nuget/dt/QuanTAlib?style=flat-square)](https://www.nuget.org/packages/QuanTAlib/)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue?style=flat-square)](https://dotnet.microsoft.com/en-us/download/dotnet)

[![Indicators](https://img.shields.io/badge/%23%20Indicators-393-blue?style=flat-square)](lib/_index.md)
[![Classes](docs/img/classes.svg)](docs/ndepend.md)
[![Files](docs/img/files.svg)](docs/ndepend.md)
[![Methods](docs/img/methods.svg)](docs/ndepend.md)
[![Lines of Code](docs/img/loc.svg)](docs/ndepend.md)
[![Public APIs](docs/img/public-api.svg)](docs/ndepend.md)
[![Comments](docs/img/comments.svg)](docs/ndepend.md)

# QuanTAlib 0.8.9

393 technical indicators. One library. Brutal architectural trade-offs for absolute speed.

[⭐ Documentation pages →](mihakralj.github.io/QuanTAlib/)

QuanTAlib exists because I got tired of validating other people's indicators. Every implementation is cross-checked against TA-Lib, Tulip, Skender, and Pandas-TA. Where they disagree, we went to the original papers. Where the papers disagree, we picked the math that doesn't lie.

Same indicators, same results: **C#**, **Python**, and **PineScript**.

### How Fast?

C# native AOT-compiled code spits out half a million bars of SMA in 328 microseconds. That is faster (per value) than a single L1 cache miss on any fancy new CPU. Achieved by trading object allocation for contiguous memory spans, slapping Fused Multiply-Add (FMA) on everything, and forcing SIMD vectorized paths. You want speed? We dictate the heap.

| Library | SMA (500K bars) | Allocations | Reality Check |
| :--- | ---: | ---: | :--- |
| **QuanTAlib** | **328 μs** | **0 B** | baseline |
| TA-Lib (C++)| 365 μs | 32 B | 1.1× slower |
| Tulip (C++)| 370 μs | 0 B | 1.1× slower |
| Skender (C#)| 68,436 μs | 42 MB | 209× slower |
| Ooples (c#)| 347,453 μs | 151 MB | 1,060× slower |
[Full benchmarks →](docs/benchmarks.md)

## Install

| Platform | Install | Guide |
| :--- | :--- | :--- |
| **.net** | `dotnet add package QuanTAlib` | [Architecture](docs/architecture.md) . [API Reference](docs/api.md) |
| **Python** | `pip install quantalib` | [Python Guide](docs/python.md) |
| **PineScript v6** | Copy-paste to TradingView | [PineScript Guide](docs/pinescript.md) |

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
sma = qtl.sma(prices, period=14)       # 393 indicators, similar syntax
```

Works with NumPy, pandas, polars, and PyArrow. NativeAOT compiled, ships as a binary. No CLR runtime dragged along for the ride.  
[Full Python guide →](docs/python.md)

### PineScript

Every indicator ships as a standalone .pine file. Open it. Copy it. Paste it into TradingView. No magic, no dependencies, just math that matches the C# and Python versions to the 10th decimal.  
[Full PineScript guide →](docs/pinescript.md)

---

## 393 Indicators

| Category | Count | What It Measures | Examples |
| :--- | :---: | :--- | :--- |
| [**Core**](lib/core/_index.md) | 8 | Price transforms, building blocks | AVGPRICE, MEDPRICE, TYPPRICE, HA |
| [**Trends (FIR)**](lib/trends_FIR/_index.md) | 33 | Finite impulse response averages | SMA, WMA, HMA, ALMA, TRIMA, LSMA |
| [**Trends (IIR)**](lib/trends_IIR/_index.md) | 36 | Infinite impulse response averages | EMA, DEMA, TEMA, T3, JMA, KAMA, VIDYA |
| [**Filters**](lib/filters/_index.md) | 37 | Signal processing, noise reduction | Kalman, Butterworth, Gaussian, Savitzky-Golay |
| [**Oscillators**](lib/oscillators/_index.md) | 48 | Bounded/centered oscillators | RSI, MACD, Stochastic, CCI, Fisher, Williams %R |
| [**Dynamics**](lib/dynamics/_index.md) | 21 | Trend strength and direction | ADX, Aroon, SuperTrend, Ichimoku, Vortex |
| [**Momentum**](lib/momentum/_index.md) | 19 | Speed of price changes | ROC, Momentum, Velocity, TSI, Qstick |
| [**Volatility**](lib/volatility/_index.md) | 26 | Price variability | ATR, Bollinger Width, Historical Vol, True Range |
| [**Volume**](lib/volume/_index.md) | 27 | Trading activity | OBV, VWAP, MFI, CMF, ADL, Force Index |
| [**Statistics**](lib/statistics/_index.md) | 35 | Statistical measures | Correlation, Variance, Skewness, Z-Score |
| [**Channels**](lib/channels/_index.md) | 23 | Price boundaries | Bollinger Bands, Keltner, Donchian |
| [**Cycles**](lib/cycles/_index.md) | 14 | Cycle analysis | Hilbert Transform, Homodyne, Ehlers Sine Wave |
| [**Reversals**](lib/reversals/_index.md) | 12 | Pattern detection | Pivot Points, Fractals, Swings |
| [**Forecasts**](lib/forecasts/_index.md) | 1 | Predictive indicators | Time Series Forecast |
| [**Errors**](lib/errors/_index.md) | 26 | Error metrics, loss functions | RMSE, MAE, MAPE, SMAPE, R² |
| [**Numerics**](lib/numerics/_index.md) | 27 | Mathematical transforms | Log, Exp, Sigmoid, Normalize, FFT |

**[Browse all 393 indicators →](lib/_index.md)**

## Architecture (the short version)

**Streaming mode:** O(1) per update. Fixed memory. State maintained internally. Feed it ticks, get answers. No history buffer, no lookback window allocation, no *please pass me the last 200 bars so I can waarm-up* nonsense.

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

## ⚠️ Fair Warning

Not yet 1.0.0. There is exactly one engineer behind this, running on mass amounts of caffeine and an irrational conviction that all technical indicators should be correct to the 9th decimal place. APIs will change. Things will break. Some indicators might produce values that make your quantitative models question the meaning of existence.

If you find something broken and don't [open an issue](https://github.com/mihakralj/QuanTAlib/issues), it will stay broken - I will have no idea. The backlog of things to fix is already longer than a Bollinger Band on a meme stock. Your bug reports make this library better. Your silence makes me brew more coffee.

## License

[Apache 2.0](LICENSE). Not MIT. Not BSD. [Deliberately →](docs/license.md)
