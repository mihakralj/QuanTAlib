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

TA libraries face a fundamental choice: accept approximations for simplicity OR enforce math rigor. QuanTAlib chooses rigor.

**Quan**titative **TA** **lib**rary (QuanTAlib) is a C# library built on the premise that you shouldn't have to choose. Modern CPUs process 4-8 FLOPS per cycle via SIMD. Modern .NET exposes memory layouts making hardware acceleration trivial. QuanTAlib exploits both. **Result:** mathematically rigorous indicators at speeds making real-time multi-symbol analysis practical on ordinary hardware.

## Key Features

- **Zero Allocation**: Hot paths are allocation-free. No GC pauses during trading.
- **SIMD Accelerated**: Uses AVX2/AVX-512 for 8x throughput on modern CPUs.
- **O(1) Streaming**: Constant time updates regardless of lookback period.
- **Platform Agnostic**: Runs on .NET 8/9/10, compatible with Quantower, NinjaTrader, QuantConnect.
- **Mathematically Rigorous**: Validated against original research papers and established libraries.

## Indicators

| Category | What It Measures | Representative Indicators |
| -------- | ---------------- | ------------------------- |
| [**Trends**](../lib/trends/_index.md) | Direction and strength of price movement through smoothing and filtering | SMA, EMA, WMA, HMA, JMA, KAMA, ALMA, DEMA, TEMA, T3 |
| [**Volatility**](../lib/volatility/_index.md) | Size and variability of price movements | ATR, StdDev, Bollinger Bands, Keltner Channels, Historical Volatility |
| [**Momentum**](../lib/momentum/_index.md) | Speed and magnitude of price changes | RSI, Stochastic, CCI, Williams %R, MACD, Momentum, ROC |
| [**Volume**](../lib/volume/_index.md) | Trading activity and price-volume relationships | OBV, VWAP, Volume ROC, A/D, MFI |
| [**Channels**](../lib/channels/_index.md) | Price boundaries and range definitions | Donchian Channels, Keltner Channels, Price Channels |
| [**Statistics**](../lib/statistics/_index.md) | Mathematical relationships between price series | Correlation, Covariance, Beta, Z-Score, Linear Regression |
| [**Numerics**](../lib/numerics/_index.md) | Mathematical transformations and signal processing | Convolution, Filters, Integration, Differentiation, Smoothing |
| [**Errors**](../lib/errors/_index.md) | Measurement accuracy and model fit quality | MAE, RMSE, Residuals, R-Squared |
| [**Forecasts**](../lib/forecasts/_index.md) | Future price prediction and projection | Linear Regression Forecast, Moving Average Projection |
| [**Cycles**](../lib/cycles/_index.md) | Periodic patterns and dominant frequencies | Hilbert Transform, Dominant Cycle, Instantaneous Phase, Sine Wave |

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

*See [Benchmarks](benchmarks.md) for full details and methodology.*

## Documentation

- [**Architecture**](architecture.md): Learn about SoA layout, SIMD, and design philosophy.
- [**Indicators**](INDICATORS.md): Full catalog of available indicators and their mathematical families.
- [**Benchmarks**](benchmarks.md): Detailed performance evidence and test methodology.
- [**Usage Guides**](usage.md): Detailed patterns for Span, Streaming, Batch, and Eventing modes.
- [**Integration**](integration.md): Setup guides for Quantower, NinjaTrader, and QuantConnect.
- [**Glossary**](glossary.md): Definitions of core QuanTAlib concepts, types, and terminology.
