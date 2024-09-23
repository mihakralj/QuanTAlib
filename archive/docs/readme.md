# QuanTAlib - quantitative technical indicators for Quantower and other C#-based trading platorms

[![Lines of Code](https://sonarcloud.io/api/project_badges/measure?project=mihakralj_QuanTAlib&metric=ncloc)](https://sonarcloud.io/summary/overall?id=mihakralj_QuanTAlib)
[![Codacy grade](https://img.shields.io/codacy/grade/b1f9109222234c87bce45f1fd4c63aee?style=flat-square)](https://app.codacy.com/gh/mihakralj/QuanTAlib/dashboard)
[![codecov](https://codecov.io/gh/mihakralj/QuanTAlib/branch/main/graph/badge.svg?style=flat-square&token=YNMJRGKMTJ?style=flat-square)](https://codecov.io/gh/mihakralj/QuanTAlib)
[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=mihakralj_QuanTAlib&metric=security_rating)](https://sonarcloud.io/summary/new_code?id=mihakralj_QuanTAlib)
[![CodeFactor](https://www.codefactor.io/repository/github/mihakralj/quantalib/badge/main)](https://www.codefactor.io/repository/github/mihakralj/quantalib/overview/main)

[![Nuget](https://img.shields.io/nuget/v/QuanTAlib?style=flat-square)](https://www.nuget.org/packages/QuanTAlib/)
![GitHub last commit](https://img.shields.io/github/last-commit/mihakralj/QuanTAlib)
[![Nuget](https://img.shields.io/nuget/dt/QuanTAlib?style=flat-square)](https://www.nuget.org/packages/QuanTAlib/)
[![GitHub watchers](https://img.shields.io/github/watchers/mihakralj/QuanTAlib?style=flat-square)](https://github.com/mihakralj/QuanTAlib/watchers)
[![.NET7.0](https://img.shields.io/badge/.NET-7.0%20%7C%206.0%20%7C%204.8-blue?style=flat-square)](https://dotnet.microsoft.com/en-us/download/dotnet/7.0)

**Quan**titative **TA** **lib**rary (QuanTAlib) is a C# library of classess and methods for quantitative technical analysis useful for analyzing quotes with [Quantower](https://www.quantower.com/) and other C#-based trading platforms.

**QuanTAlib** is written with some specific design criteria in mind - why there is '_yet another C# TA library_':

- Prioritize **real-time data analysis** (series can add new data and indicator doesn't have to re-calculate the whole history)
- **Allow updates** to the last quote and adjusting the calculation to the still-forming bar
- **Calculate early data right** - output data is as valid as mathematically possible from the first value onwards

![Alt text](./img/quotes.gif)

If not obvious, QuanTAlib is intended for developers, and it does not focus on sources of OHLCV quotes. There are some very basic data feeds available to use in the learning process: `RND_Feed` and `GBM_Feed` for random data, `Yahoo_Feed` and `Alphavantage_Feed` for a quick grab of daily data of US stock market.

See [Getting Started](https://github.com/mihakralj/QuanTAlib/blob/main/Docs/getting_started.ipynb) .NET interactive notebook to get a feel how library works. Developers can use QuanTAlib in [Polyglot Notebooks](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.dotnet-interactive-vscode) or in console apps, but the best usage of the library is with C#-enabled trading platforms - see **QuanTower_Charts** folder for Quantower examples and check **Releases** for compiled Quantower DLL.

### Coverage

[List of all indicators - current and planned](indicators.md)

### Validation

QuanTAlib uses validation tests with four other TA libraries to assure accuracy and validity of results:

- [TA-LIB](https://www.ta-lib.org/function.html)
- [Skender Stock Indicators](https://dotnet.stockindicators.dev/)
- [Pandas-TA](https://twopirllc.github.io/pandas-ta/)
- [Tulip Indicators](https://tulipindicators.org/)

### Questions

[Some most common questions addressed](QA.md)