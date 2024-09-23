# QuanTAlib - quantitative technical indicators for Quantower

## (and other C#-based trading platorms)

[![Lines of Code](https://sonarcloud.io/api/project_badges/measure?project=mihakralj_QuanTAlib&metric=ncloc)](https://sonarcloud.io/summary/overall?id=mihakralj_QuanTAlib)
[![Codacy grade](https://img.shields.io/codacy/grade/b1f9109222234c87bce45f1fd4c63aee?style=flat-square)](https://app.codacy.com/gh/mihakralj/QuanTAlib/dashboard)
[![codecov](https://codecov.io/gh/mihakralj/QuanTAlib/branch/main/graph/badge.svg?style=flat-square&token=YNMJRGKMTJ?style=flat-square)](https://codecov.io/gh/mihakralj/QuanTAlib)
[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=mihakralj_QuanTAlib&metric=security_rating)](https://sonarcloud.io/summary/new_code?id=mihakralj_QuanTAlib)
[![CodeFactor](https://www.codefactor.io/repository/github/mihakralj/quantalib/badge/main)](https://www.codefactor.io/repository/github/mihakralj/quantalib/overview/main)

[![Nuget](https://img.shields.io/nuget/v/QuanTAlib?style=flat-square)](https://www.nuget.org/packages/QuanTAlib/)
![GitHub last commit](https://img.shields.io/github/last-commit/mihakralj/QuanTAlib)
[![Nuget](https://img.shields.io/nuget/dt/QuanTAlib?style=flat-square)](https://www.nuget.org/packages/QuanTAlib/)
[![GitHub watchers](https://img.shields.io/github/watchers/mihakralj/QuanTAlib?style=flat-square)](https://github.com/mihakralj/QuanTAlib/watchers)
[![.NET8.0](https://img.shields.io/badge/.NET-8.0-blue?style=flat-square)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

**Quan**titative **TA** **lib**rary (QuanTAlib) is a C# library of classess and methods for quantitative technical analysis useful for analyzing quotes with [Quantower](https://www.quantower.com/) and other C#-based trading platforms.

**QuanTAlib** is written with some specific design criteria in mind - why there is '_yet another C# TA library_':

- Prioritize **real-time data analysis**: As new data items arrives, indicators don't have to re-calculate the entire history and can generate a result directly from the last item
- **Allow updates/corrections** of the last quote - QuanTAlib is re-calculating the last value as many times as required before continuing to the new bar
- **Calculate early data right** - calculated data is as valid as mathematically possible from the first value onwards - no blackout or warming-up periods. All indicators return data from the first bar, alongside with a flag `isHot` - defining if calculation is already stable.

![Alt text](./img/quotes.gif)

QuanTAlib is intended for developers and users of Quantower, therefore it does not focus on privind sources of OHLCV quotes. There are some very basic data feeds available to use in the learning process: `GBM_Feed` for Random (Geometric Brownian Motion) data, and `SyntheticVendor` data generator for Quantower.

### Coverage

[List of indicators - implemented and planned](indicators/indicators.md)

### Validation

QuanTAlib uses validation tests with four other TA libraries to assure accuracy and validity of results:

- [TA-LIB](https://www.ta-lib.org/function.html)
- [Skender Stock Indicators](https://dotnet.stockindicators.dev/)
- [Tulip Indicators](https://tulipindicators.org/)

