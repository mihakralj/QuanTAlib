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

Quantitative TA Library (**QuanTAlib**) is an easy-to-use C# library for quantitative technical analysis with base algorithms, charts, signals and strategies useful for trading securities with [Quantower](https://www.quantower.com/) and other C#-based trading platforms.

**QuanTAlib** is written with some specific design criteria in mind - this is a list of reasons why there is '_yet another C# TA library_':

- Written in native C# - no code conversion from TA-LIB or other imported/converted TA libraries
- No usage of Decimal datatypes, LINQ, interface abstractions, or static classes (all for performance reasons)
- Supports both **historical data analysis** (working on bulk of historical arrays) and **real-time analysis** (adding one data item at the time without the need to re-calculate the whole history)
- Separation of calculations (**algos**) and visualizations (**charts**)
- Handle early data right - no hiding of poor calculations with NaN values (unless explicitly requested), data is as valid as mathematically possible from the first value
- Preservation of time-value integrity of each data throughout the calculation chain (each data point has a timestamp)
- Usage of events - each data series is an event publisher, each indicator is a subscriber - this allows seamless data flow between indicators without the need of plumbing (see [MACD example](https://github.com/mihakralj/QuanTAlib/blob/main/docs/macd_example.ipynb) to understand how events allow chaining of indicators)

QuanTAlib does not provide OHLCV quotes - but it can easily connect to any data feeds. There are some data feed classess
available (**RND_Feed** for random OHLCV, **YAHOO_Feed** for Yahoo Finance daily stock data)

See [Getting Started](https://github.com/mihakralj/QuanTAlib/blob/main/Docs/getting_started.ipynb) .NET interactive notebook to get a feel how library works. Developers can use QuanTAlib in .NET interactive or in console apps, but the best
usage of the library is withing C#-enabled trading platforms - see **QuanTower_Charts** folder for Quantower examples.

[**List of available and planned indicators**](https://github.com/mihakralj/QuanTAlib/blob/main/docs/coverage.md). **So. Much. To. Do...**
