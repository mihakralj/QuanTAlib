# TValue: Time-Value Pair

## What It Does

`TValue` is the fundamental atomic unit of data in QuanTAlib. It represents a single point in a time series, consisting of a timestamp and a double-precision floating-point value. It serves as the standard input and output format for all indicators and data streams.

## Design Philosophy

In high-frequency trading and quantitative analysis, memory allocation is a critical bottleneck. `TValue` is designed as a **lightweight, immutable struct** to ensure:

* **Zero Heap Allocation**: Being a struct, it lives on the stack or embedded in arrays, avoiding Garbage Collector (GC) pressure.
* **Thread Safety**: Immutability guarantees safe concurrent access.
* **Minimal Footprint**: Occupies exactly 16 bytes (8 bytes for `long` Time + 8 bytes for `double` Value), fitting efficiently in CPU cache lines.

## How It Works

`TValue` is implemented as a `readonly record struct`. It encapsulates:

* **Time**: A `long` representing ticks (UTC).
* **Value**: A `double` representing the data magnitude.

It supports implicit conversions to `double` (extracting the value) and `DateTime` (extracting the time), making it syntactically fluid to use in calculations.

## Structure

### Definition

```csharp
public readonly record struct TValue(long Time, double Value);
```

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Time` | `long` | Timestamp in ticks (UTC). |
| `Value` | `double` | The data value. |
| `AsDateTime` | `DateTime` | Helper to view `Time` as a `DateTime` object. |

### Constructors

| Constructor | Description |
|-------------|-------------|
| `new TValue(long time, double value)` | Creates a TValue from raw ticks. |
| `new TValue(DateTime time, double value)` | Creates a TValue from a DateTime object. |

## Usage

### Creating TValues

```csharp
// From DateTime
var t1 = new TValue(DateTime.UtcNow, 100.5);

// From Ticks
var t2 = new TValue(DateTime.UtcNow.Ticks, 100.5);
```

### Implicit Conversions

```csharp
TValue tv = new TValue(DateTime.UtcNow, 42.0);

// Implicitly converts to double
double val = tv; // 42.0

// Implicitly converts to DateTime
DateTime dt = tv; // DateTime object
```

### String Representation

```csharp
Console.WriteLine(tv); // Output: "[2024-01-01 12:00:00, 42.00]"
```

## Performance Profile

* **Memory**: 16 bytes per instance.
* **Allocation**: 0 bytes (Stack allocated).
* **Copying**: Cheap (fits in two 64-bit registers).

## Integration

`TValue` is the primary currency of the library:

* **Indicators**: `Update(TValue input)` accepts it.
* **Series**: `TSeries` stores collections of it.
* **Events**: `ITValuePublisher` broadcasts it.

## Architecture Notes

* **SkipLocalsInit**: The struct is marked with `[SkipLocalsInit]` to suppress zero-initialization of locals, squeezing out nanoseconds in tight loops.
* **AggressiveInlining**: All accessors and operators are inlined to ensure zero abstraction penalty.

## References

* [Structure of Arrays (SoA)](https://en.wikipedia.org/wiki/AOS_and_SOA)
* [C# Struct Performance](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/struct)
