[![Codacy grade](https://app.codacy.com/project/badge/Grade/c8be6c08f5514e95b84d37e661a6ec27)](https://app.codacy.com/gh/mihakralj/QuanTAlib/dashboard?utm_source=gh&utm_medium=referral&utm_content=&utm_campaign=Badge_grade)
[![codecov](https://codecov.io/gh/mihakralj/QuanTAlib/branch/main/graph/badge.svg?style=flat-square&token=YNMJRGKMTJ?style=flat-square)](https://codecov.io/gh/mihakralj/QuanTAlib)
[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=mihakralj_QuanTAlib&metric=security_rating)](https://sonarcloud.io/summary/new_code?id=mihakralj_QuanTAlib)
[![CodeFactor](https://www.codefactor.io/repository/github/mihakralj/quantalib/badge/main)](https://www.codefactor.io/repository/github/mihakralj/quantalib/overview/main)  
[![Nuget](https://img.shields.io/nuget/v/QuanTAlib?style=flat-square)](https://www.nuget.org/packages/QuanTAlib/)
![GitHub last commit](https://img.shields.io/github/last-commit/mihakralj/QuanTAlib)
[![Nuget](https://img.shields.io/nuget/dt/QuanTAlib?style=flat-square)](https://www.nuget.org/packages/QuanTAlib/)
[![.NET](https://img.shields.io/badge/.NET-8.0%20|%2010.0-blue?style=flat-square)](https://dotnet.microsoft.com/en-us/download/dotnet)

[![Indicators](https://img.shields.io/badge/%23%20Indicators-298-blue?style=flat-square)](lib/_index.md)
[![Classes](ndepend/badges/classes.svg)](ndepend/ndependout/ndependreport.html)
[![Files](ndepend/badges/files.svg)](ndepend/ndependout/ndependreport.html)
[![Methods](ndepend/badges/methods.svg)](ndepend/ndependout/ndependreport.html)
[![Lines of Code](ndepend/badges/loc.svg)](ndepend/ndependout/ndependreport.html)  
[![Public APIs](ndepend/badges/public-api.svg)](ndepend/ndependout/ndependreport.html)
[![Comments](ndepend/badges/comments.svg)](ndepend/ndependout/ndependreport.html)

Static code analysis provided by [ndepend](https://www.ndepend.com/)  

# QuanTAlib - Quantitative Technical Indicators Without Compromises

TA libraries face a fundamental choice: accept approximations for simplicity OR enforce math rigor. QuanTAlib chooses rigor.

**Quan**titative **TA** **lib**rary (QuanTAlib) is a C# library built on the premise that you shouldn't have to choose. Modern CPUs process 4-8 FLOPS per cycle via SIMD. Modern .NET exposes memory layouts making hardware acceleration trivial. QuanTAlib exploits both. **Result:** mathematically rigorous indicators at speeds making real-time multi-symbol analysis practical on ordinary hardware.

## Key Features

- **Zero Allocation**: Hot paths are allocation-free. No GC pauses during trading.
- **SIMD Accelerated**: Uses AVX2/AVX-512 for 8x throughput on modern CPUs.
- **O(1) Streaming**: Constant time updates regardless of lookback period.
- **Platform Agnostic**: Runs on .NET 8/9/10, compatible with Quantower, NinjaTrader, QuantConnect.
- **Mathematically Rigorous**: Validated against original research papers and established libraries.

## Indicators

| Category | Count | What It Measures | Representative Indicators |
| -------- | :---: | ---------------- | ------------------------- |
| [**Trends (FIR)**](lib/trends_FIR/_index.md) | 17 | Finite Impulse Response moving averages | SMA, WMA, HMA, ALMA, TRIMA, LSMA, EPMA |
| [**Trends (IIR)**](lib/trends_IIR/_index.md) | 23 | Infinite Impulse Response moving averages | EMA, DEMA, TEMA, T3, JMA, KAMA, VIDYA |
| [**Filters**](lib/filters/_index.md) | 31 | Signal processing and noise reduction filters | Bessel, Butterworth, Gaussian, Savitzky-Golay, Ehlers Super Smoother |
| [**Oscillators**](lib/oscillators/_index.md) | 20 | Indicators that fluctuate around a center line | RSI, MACD, Stochastic, AO, APO, CCI, Ultimate Oscillator |
| [**Dynamics**](lib/dynamics/_index.md) | 18 | Trend strength and direction indicators | ADX, Aroon, SuperTrend, Vortex, Chop, Ichimoku |
| [**Momentum**](lib/momentum/_index.md) | 16 | Speed and magnitude of price changes | Momentum, ROC, Velocity, RSX, Qstick, KDJ |
| [**Volatility**](lib/volatility/_index.md) | 26 | Size and variability of price movements | ATR, Bollinger Band Width, Historical Volatility, True Range |
| [**Volume**](lib/volume/_index.md) | 26 | Trading activity and price-volume relationships | OBV, VWAP, MFI, ADL, CMF, TVI, Force Index |
| [**Statistics**](lib/statistics/_index.md) | 30 | Statistical measures and tests | Correlation, Variance, StdDev, Skewness, Kurtosis, Z-Score |
| [**Channels**](lib/channels/_index.md) | 23 | Price boundaries and range definitions | Bollinger Bands, Keltner Channels, Donchian Channels |
| [**Cycles**](lib/cycles/_index.md) | 14 | Cycle analysis and signal processing | Hilbert Transform, Homodyne, Phasor, Ehlers Sine Wave |
| [**Reversals**](lib/reversals/_index.md) | 12 | Pattern recognition and reversal detection | Pivot Points, Fractals, Swings, Pivot Components |
| [**Forecasts**](lib/forecasts/_index.md) | 1 | Predictive indicators and projections | Time Series Forecast, AFIRMA, Chande Forecast Oscillator |
| [**Errors**](lib/errors/_index.md) | 26 | Error metrics and loss functions | RMSE, MAE, MAPE, SMAPE, MASE, R-Squared |
| [**Numerics**](lib/numerics/_index.md) | 15 | Mathematical transformations | Log, Exp, Sqrt, Tanh, ReLU, Sigmoid |

**[Browse all 298 indicators →](lib/_index.md)**

## Quick Start

Install from NuGet:

```bash
dotnet add package QuanTAlib
```

Calculate an SMA in real-time:

```csharp
using QuanTAlib;

var sma = new Sma(period: 14);
double price = 100.0;

// Update with new price
var result = sma.Update(new TValue(DateTime.UtcNow, price));

if (result.IsHot)
{
    Console.WriteLine($"SMA: {result.Value}");
}
```

## Performance Snapshot

QuanTAlib is designed for speed. Here is how it compares calculating a 500,000 bar SMA against other libraries:

| Library | Mean Time | Allocations | Relative Speed |
| ------- | --------- | ----------- | -------------- |
| **QuanTAlib (Span)** | **318.3 μs** | **0 B** | **1.00x (baseline)** |
| TA-Lib | 356.4 μs | 34 B | 1.12x slower |
| Tulip Indicators | 359.3 μs | 0 B | 1.13x slower |
| Skender Indicators | 71,277 μs | 50.8 MB | 224x slower |

*See [Benchmarks](docs/benchmarks.md) for full details and methodology.*

## Documentation

### Core Concepts

- [**Architecture**](docs/architecture.md): Learn about SoA layout, SIMD, and design philosophy.
- [**API Reference**](docs/api.md): Deep dive into the Tri-Modal Architecture (Batch, Streaming, Priming).
- [**Indicators**](docs/indicators.md): Full catalog of available indicators and their mathematical families.
- [**Usage Guides**](docs/usage.md): Detailed patterns for Span, Streaming, Batch, and Eventing modes.
- [**Integration**](docs/integration.md): Setup guides for Quantower, NinjaTrader, and QuantConnect.

### Analysis & Validation

- [**Benchmarks**](docs/benchmarks.md): Detailed performance evidence and test methodology.
- [**Error Metrics**](docs/errors.md): Implementation details for 20+ error metrics and loss functions.
- [**Trend Comparison**](docs/trendcomparison.md): Comparative analysis of lag, smoothness, and accuracy.
- [**MA Qualities**](docs/ma-qualities.md): Theoretical framework for evaluating moving averages.
- [**Validation**](docs/validation.md): Verification matrices against TA-Lib, Skender, and other libraries.
- [**Glossary**](docs/glossary.md): Definitions of core QuanTAlib concepts, types, and terminology.
