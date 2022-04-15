# QuantLib - quantitative technical indicators for Quantower and other C#-based trading platorms

![GitHub last commit](https://img.shields.io/github/last-commit/mihakralj/QuantLib)
[![.NET library build/test](https://github.com/mihakralj/QuantLib/actions/workflows/main_automation.yml/badge.svg)](https://github.com/mihakralj/QuantLib/actions/workflows/main_automation.yml)
[![Codacy grade](https://img.shields.io/codacy/grade/b1f9109222234c87bce45f1fd4c63aee)](https://app.codacy.com/gh/mihakralj/QuantLib/dashboard)
[![codecov](https://codecov.io/gh/mihakralj/QuantLib/branch/main/graph/badge.svg?style=flat-square&token=YNMJRGKMTJ)](https://codecov.io/gh/mihakralj/QuantLib)

[![Nuget](https://img.shields.io/nuget/v/QuantLib)](https://www.nuget.org/packages/QuantLib/)
[![Nuget](https://img.shields.io/nuget/dt/QuantLib)](https://www.nuget.org/packages/QuantLib/)
[![GitHub watchers](https://img.shields.io/github/watchers/mihakralj/QuantLib)](https://github.com/mihakralj/QuantLib/watchers)

[![.NET7.0](https://img.shields.io/badge/.NET-7.0-yellow)](https://dotnet.microsoft.com/en-us/download/dotnet/7.0)
[![.NET6.0](https://img.shields.io/badge/.NET-6.0-blue)](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)
[![.NET5.0](https://img.shields.io/badge/.NET-5.0-blue)](https://dotnet.microsoft.com/en-us/download/dotnet/5.0)
[![.NET4.8](https://img.shields.io/badge/.NET-4.8-blue)](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48)
[![GitHub license](https://img.shields.io/github/license/mihakralj/QuantLib)](Docs/LICENSE)

Quantitative Library (**QuantLib**) is an easy-to-use C# library for quantitative technical analysis with base algorithms, charts, signals and strategies useful for trading securities with [Quantower](https://www.quantower.com/), [MultiCharts.NET](https://www.multicharts.com/net/") and other C#-based trading platforms.

**QuantLib** is written with some specific design criteria in mind - this is a list of reasons why there is '_yet another C# TA library_':

- Written in native C# - no code conversion from TA-LIB or other imported/converted TA libraries
- No usage of Decimal datatypes, LINQ, interface abstractions, or static classes (all for performance reasons)
- Supports both **historical data analysis** (working on bulk of historical arrays) and **real-time analysis** (adding one data item at the time without the need to re-calculate the whole history)
- Separation of calculations (**algos**) and visualizations (**charts**)
- Handle early data right - no hiding of poor calculations with NaN values (unless explicitly requested), data is as valid as mathematically possible from the first value
- Preservation of time-value integrity of each data throughout the calculation chain (each data point has a timestamp)
- Usage of events - each data series is an event publisher, each indicator is a subscriber - this allows seamless data flow between indicators without the need of plumbing (see [MACD example](https://github.com/mihakralj/QuantLib/blob/main/Docs/macd_example.ipynb) to understand how events allow chaining of indicators)

QuantLib does not provide OHLCV quotes - but it can easily connect to any data feeds. There are some data feed classess
available (**RND_Feed** for random OHLCV, **YAHOO_Feed** for Yahoo Finance daily stock data)

See [Getting Started](https://github.com/mihakralj/QuantLib/blob/main/Docs/getting_started.ipynb) .NET interactive notebook to get a feel how library works. Developers can use QuantLib in .NET interactive or in console apps, but the best
usage of the library is withing C#-enabled trading platforms - see **QuanTower_Charts** folder for Quantower examples.

[**List of available and planned indicators**](https://github.com/mihakralj/QuantLib/blob/main/Docs/coverage.md). **So. Much. To. Do...**
