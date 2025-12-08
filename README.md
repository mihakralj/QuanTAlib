[![Lines of Code](https://sonarcloud.io/api/project_badges/measure?project=mihakralj_QuanTAlib&metric=ncloc)](https://sonarcloud.io/summary/overall?id=mihakralj_QuanTAlib)
[![Codacy grade](https://img.shields.io/codacy/grade/b1f9109222234c87bce45f1fd4c63aee?style=flat-square)](https://app.codacy.com/gh/mihakralj/QuanTAlib/dashboard)
[![codecov](https://codecov.io/gh/mihakralj/QuanTAlib/branch/main/graph/badge.svg?style=flat-square&token=YNMJRGKMTJ?style=flat-square)](https://codecov.io/gh/mihakralj/QuanTAlib)
[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=mihakralj_QuanTAlib&metric=security_rating)](https://sonarcloud.io/summary/new_code?id=mihakralj_QuanTAlib)
[![CodeFactor](https://www.codefactor.io/repository/github/mihakralj/quantalib/badge/main)](https://www.codefactor.io/repository/github/mihakralj/quantalib/overview/main)

[![Nuget](https://img.shields.io/nuget/v/QuanTAlib?style=flat-square)](https://www.nuget.org/packages/QuanTAlib/)
![GitHub last commit](https://img.shields.io/github/last-commit/mihakralj/QuanTAlib)
[![Nuget](https://img.shields.io/nuget/dt/QuanTAlib?style=flat-square)](https://www.nuget.org/packages/QuanTAlib/)
[![GitHub watchers](https://img.shields.io/github/watchers/mihakralj/QuanTAlib?style=flat-square)](https://github.com/mihakralj/QuanTAlib/watchers)
[![.NET](https://img.shields.io/badge/.NET-8.0%20|%209.0%20|%2010.0-blue?style=flat-square)](https://dotnet.microsoft.com/en-us/download/dotnet)

# QuanTAlib - Quantitative Technical Analysis Library

**Quan**titative **TA** **lib**rary (QuanTAlib) is a high-performance C# library for quantitative technical analysis, designed for [Quantower](https://www.quantower.com/) and other C#-based trading platforms.

## Key Features

- **Real-time streaming** - Indicators calculate results from incoming data without re-processing history
- **Update/correction support** - Last value can be recalculated multiple times before advancing to next bar
- **Valid from first bar** - Mathematically correct results from the first value with `IsHot` warmup indicator
- **SIMD-optimized** - Hardware-accelerated vector operations (AVX/SSE) for batch processing
- **Zero-allocation hot paths** - Minimal GC pressure for high-frequency scenarios

## Architecture

QuanTAlib uses a **Structure of Arrays (SoA)** memory layout optimized for numerical computing:

```
┌─────────────────────────────────────────────────────────────┐
│                    Core Data Types                          │
├─────────────────────────────────────────────────────────────┤
│  TValue (16 bytes)     │  Time-value pair (long + double)  │
│  TBar (48 bytes)       │  OHLCV bar (long + 5 doubles)     │
│  TSeries               │  Time series with SoA layout      │
│  TBarSeries            │  OHLCV series with SoA layout     │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│                    Data Feeds                               │
├─────────────────────────────────────────────────────────────┤
│  IFeed                 │  Unified feed interface            │
│  GBM                   │  Geometric Brownian Motion sim     │
│  CsvFeed               │  CSV file reader                   │
└─────────────────────────────────────────────────────────────┘

```

### Performance Design

The SoA layout stores timestamps and values in separate contiguous arrays:

```csharp
// TSeries internal structure
protected readonly List<long> _t;    // Timestamps (contiguous)
protected readonly List<double> _v;  // Values (contiguous)

// Direct SIMD access via Span<T>
ReadOnlySpan<double> values = series.Values;
double avg = values.AverageSIMD();  // Hardware-accelerated
```

This enables:
- **Cache locality** - Sequential memory access patterns
- **SIMD vectorization** - Process 4-8 values per CPU instruction
- **Zero-copy access** - `CollectionsMarshal.AsSpan()` exposes internal arrays

## Quick Start

### Installation

```bash
dotnet add package QuanTAlib
```

### Basic Usage

```csharp
using QuanTAlib;

// Create EMA indicator
var ema = new Ema(period: 10);

// Streaming mode - process one value at a time
TValue result = ema.Update(new TValue(DateTime.Now, price), isNew: true);

// Update current bar (e.g., price tick within same minute)
result = ema.Update(new TValue(DateTime.Now, newPrice), isNew: false);

// Batch mode - process entire series
var series = new TSeries();
series.Add(prices);  // Add historical data
TSeries emaResults = Ema.Calculate(series, period: 10);
```

### Multi-Period Analysis with SIMD

```csharp
// Calculate multiple EMAs in parallel using SIMD
int[] periods = { 9, 12, 26 };
var emaVector = new EmaVector(periods);

// Single update calculates all periods
TValue[] results = emaVector.Update(new TValue(time, price));
Console.WriteLine($"EMA(9)={results[0]}, EMA(12)={results[1]}, EMA(26)={results[2]}");
```

### Using Data Feeds

```csharp
// Geometric Brownian Motion simulator
var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2);
TBarSeries bars = gbm.Fetch(count: 1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

// CSV file reader
var csv = new CsvFeed("data/daily_IBM.csv");
TBar bar = csv.Next(isNew: true);
```

## Installation to Quantower

Copy DLL files to Quantower installation:

```
<Quantower_root>\Settings\Scripts\Indicators\QuanTAlib\Trends\Trends.dll
```

Where `<Quantower_root>` is the directory containing `Start.lnk`.

## Project Structure

```
QuanTAlib/
├── lib/
│   ├── core/
│   │   ├── tvalue/      # TValue struct
│   │   ├── tseries/     # TSeries class
│   │   ├── tbar/        # TBar struct
│   │   ├── tbarseries/  # TBarSeries class
│   │   └── simd/        # SIMD extensions
│   ├── trends/
│   │   └── ema/         # EMA indicator + tests + docs
│   └── feeds/
│       ├── csv/         # CSV file feed
│       └── gbm/         # GBM simulator
└── quantower/           # Quantower integration
```

Each indicator follows a consistent file pattern:
- `Indicator.cs` - Core implementation
- `Indicator.Tests.cs` - Unit tests
- `Indicator.Validation.Tests.cs` - Cross-validation with other libraries
- `Indicator.md` - Documentation
- `Indicator.Notebook.dib` - Interactive notebook
- `Indicator.Quantower.cs` - Quantower wrapper

## Validation

QuanTAlib validates results against established TA libraries:

- [TA-LIB](https://www.ta-lib.org/function.html) - Industry standard C library
- [Skender Stock Indicators](https://dotnet.stockindicators.dev/) - Popular .NET library
- [Tulip Indicators](https://tulipindicators.org/) - High-performance C library

## Requirements

- .NET 8.0, 9.0, or 10.0
- Hardware with AVX/SSE support recommended for optimal SIMD performance

## License

Apache License 2.0 - See [LICENSE](LICENSE) for details.

## Contributing

Contributions welcome! Each indicator should include:
1. Core implementation with streaming support
2. Unit tests covering edge cases
3. Validation tests against reference libraries
4. Documentation with mathematical formulas
5. Quantower wrapper (optional)

## Links

- [GitHub Repository](https://github.com/mihakralj/QuanTAlib)
- [NuGet Package](https://www.nuget.org/packages/QuanTAlib/)
- [Quantower Platform](https://www.quantower.com/)
