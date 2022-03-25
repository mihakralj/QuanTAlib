# QuantLib - quantitative technical indicators for Quantower and other C#-based trading platorms

QuantLib is a C# library with base algorithms, charts, signals and strategies useful for trading securities with [Quantower](https://www.quantower.com/) and other C# packages.

Library is written with some specific design criteria in mind - this is a list of reasons why there is 'yet another C# TA library':

- Written in C#, so it can be integrated with Quantower, Multicharts, NinjaTrader, Tickblaze, StockSharp...
- Supports both historical analysis (working on bulk of historical data) and real-time calculations (adding one data item at the time without re-calculating the whole history)
- Separation of calculations (algorithms) amd visualizations (charts)
- Handles early data right - no hiding of poor calculations with NaN values (unless requested)
- Preserves time-value integrity of data throughout the calculation chain (every data point has to have atimestamp)

QuantLib needs a feed of OHLCV quotes with timestamps and can calculate technical indicators regardless of the source, type of security, size of a data bar.

QuantLib is now published on nuget.org so it can be included in .NET interactive notebooks.

## Completed

- ADD - Add TSeries
- SUB - Subtract TSeries
- MUL - Multiply TSeries
- DIV - Divide TSeries
- SMA - Simple Moving Average
- EMA - Exponential Moving Average
- WMA - Weighted Moving average
- RMA - WildeR's Moving average
- DEMA - Double Exponential Moving Average
- TEMA - Triple Exponential Moving Average
- HMA - Hull moving average
- ZLEMA - Zero-Lag EMA moving average
- JMA - Jurik Moving Indicator
- HEMA - Hull-EMA moving average (a hybrid experiment, not a standard indicator )
