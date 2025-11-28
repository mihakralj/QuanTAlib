# TValue: Time-Value Pair

## Overview

`TValue` is the fundamental building block of QuanTAlib. It represents a single data point in a time series, consisting of a timestamp and a double-precision floating-point value.

It is implemented as a lightweight `readonly struct` to ensure immutability and high performance (stack allocation, no GC overhead).

## Structure

```csharp
public readonly struct TValue
{
    public readonly long Time;   // Ticks (UTC)
    public readonly double Value; // Data value
    public readonly bool IsNew;   // Metadata for streaming (optional usage)
}
```

## Key Features

*   **Lightweight**: 24 bytes (long + double + bool + padding).
*   **Immutable**: Thread-safe by design.
*   **Implicit Conversions**: Can be implicitly converted to `double` (returns Value) and `DateTime` (returns Time).
*   **Performance**: Designed for high-frequency trading and large dataset processing.

## Usage

`TValue` is used throughout the library for:
*   Input to indicators (`Update(TValue)`).
*   Output from indicators (`Value` property).
*   Elements in `TSeries`.

## Constructors

*   `new TValue(long time, double value, bool isNew = true)`
*   `new TValue(DateTime time, double value, bool isNew = true)`
