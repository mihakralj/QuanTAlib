# QuantLib - quantitative technical indicators for Quantower and other C#-based trading platorms
[![Nuget](https://img.shields.io/nuget/v/QuantLib?style=flat-square)](https://www.nuget.org/packages/QuantLib/)
[![Nuget](https://img.shields.io/nuget/dt/QuantLib?style=flat-square)](https://www.nuget.org/packages/QuantLib/)
[![GitHub watchers](https://img.shields.io/github/watchers/mihakralj/QuantLib?style=flat-square)](https://github.com/mihakralj/QuantLib/watchers)
![GitHub last commit](https://img.shields.io/github/last-commit/mihakralj/QuantLib)
![Codacy grade](https://img.shields.io/codacy/grade/b1f9109222234c87bce45f1fd4c63aee?style=flat-square)

[![.NET7.0](https://img.shields.io/badge/.NET-7.0-yellow?style=flat-square)]()
[![.NET6.0](https://img.shields.io/badge/.NET-6.0-blue?style=flat-square)]()
[![.NET5.0](https://img.shields.io/badge/.NET-5.0-blue?style=flat-square)]()
[![.NET4.8](https://img.shields.io/badge/.NET-4.8-blue?style=flat-square)]()
[![GitHub license](https://img.shields.io/github/license/mihakralj/QuantLib?style=flat-square)](Docs/LICENSE)

Quantitative Library (**QuantLib**) is an easy-to-use C# library for quantitative technical analysis with base algorithms, charts, signals and strategies useful for trading securities with [Quantower](https://www.quantower.com/), [MultiCharts.NET](https://www.multicharts.com/net/") and other C#-based trading platforms.

**QuantLib** is written with some specific design criteria in mind - this is a list of reasons why there is '*yet another C# TA library*':

*   Written in native C# - no code conversion from TALIB or other existing TA libraries
*   No usage of Decimal datatypes, Linq, interface abstractions, or static classes (all for performance reasons)
*   Support both **historical data analysis** (working on bulk of historical arrays) and **real-time analysis** (adding one data item at the time without re-calculating the whole history)
*   Separation of calculations (**algos**) and visualizations (**charts**)
*   Handle early data right - no hiding of poor calculations with NaN values (unless explicitly requested)
*   Preservation of time-value integrity of each data throughout the calculation chain (each data point has a timestamp)

QuantLib does not provide OHLCV quotes - but it can easily connect to any  data feeds - see Docs for examples.

See [Getting Started](https://github.com/mihakralj/QuantLib/blob/main/Docs/getting_started.ipynb) .NET interactive notebook to get a feel how library works.

[**List of available and planned indicators**](https://github.com/mihakralj/QuantLib/blob/main/Docs/indicators.md). **So. Much. To. Do...**
