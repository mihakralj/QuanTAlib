# TBar: OHLCV Bar Struct

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Core                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | None                      |
| **Outputs**      | Multiple series (O, H, L, C, V)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | 1 bar                          |

- `TBar` is a lightweight, immutable struct representing a single OHLCV (Open, High, Low, Close, Volume) bar.
- No configurable parameters; computation is stateless per bar.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

## What It Does

`TBar` is a lightweight, immutable struct representing a single OHLCV (Open, High, Low, Close, Volume) bar. It serves as the fundamental unit for price data in QuanTAlib, designed to hold market data with minimal memory overhead while providing convenient accessors for common price derivations.

## Design Philosophy

Financial data processing often involves millions of bars. Storing these as classes would create massive GC pressure and memory fragmentation. `TBar` is designed as a **pure data struct** to ensure:

* **Compactness**: Occupies exactly 48 bytes (1 `long` + 5 `double`s), fitting efficiently in memory.
* **Immutability**: Thread-safe by default; values cannot change once created.
* **Zero-Cost Abstractions**: Computed properties (like `HL2`) are calculated on-demand, requiring no extra storage.

## How It Works

`TBar` is a `readonly record struct` that stores:

* **Time**: Timestamp in ticks.
* **Open, High, Low, Close**: Price components.
* **Volume**: Traded volume.

It includes implicit conversions to `double` (defaulting to Close price) and `TValue` (Time + Close), allowing it to be used interchangeably with simpler types in many contexts.

## Structure

### Definition

```csharp
public readonly record struct TBar(long Time, double Open, double High, double Low, double Close, double Volume);
```

### Core Properties

| Property | Type | Description |
| ------ | ------ | ------ |
| `Time` | `long` | Timestamp in ticks (UTC). |
| `Open` | `double` | Opening price. |
| `High` | `double` | Highest price. |
| `Low` | `double` | Lowest price. |
| `Close` | `double` | Closing price. |
| `Volume` | `double` | Traded volume. |

### Computed Properties (Zero-Storage)

| Property | Formula | Description |
| ------ | ------ | ------ |
| `HL2` | `(H + L) / 2` | Median Price. |
| `OC2` | `(O + C) / 2` | Midpoint Price. |
| `OHL3` | `(O + H + L) / 3` | Typical Price (Variant). |
| `HLC3` | `(H + L + C) / 3` | Typical Price. |
| `OHLC4` | `(O + H + L + C) / 4` | Weighted Close. |
| `HLCC4` | `(H + L + 2C) / 4` | Weighted Close (Variant). |

### TValue Accessors

Efficiently extracts components as `TValue` pairs:

* `O`, `H`, `L`, `C`, `V`

## Usage

### Creating a Bar

```csharp
var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
```

### Implicit Conversions

```csharp
TBar bar = ...;

// Treat as double (uses Close price)
double price = bar;

// Treat as TValue (Time + Close)
TValue tv = bar;

// Treat as DateTime
DateTime dt = bar;
```

### Using Computed Properties

```csharp
// Calculate Typical Price on the fly
double typical = bar.HLC3;
```

## Performance Profile

### Operation Count (Streaming Mode)

TBar is a 48-byte struct (DateTime + 5 doubles). Field access and construction are stack/register operations.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Struct construction (6 fields) | 6 | 1 cy | ~6 cy |
| Field read (O/H/L/C/V) | 1 | 0 cy | ~0 cy |
| TypicalPrice = (H+L+C)/3 | 1 | 2 cy | ~2 cy |
| **Total** | **O(1)** | — | **~8 cy** |

48-byte struct spans 3 cache lines but is typically stack-allocated. JIT may promote to registers for short-lived locals. No heap allocation.

* **Memory**: 48 bytes per instance.
* **Allocation**: 0 bytes (Stack allocated).
* **Access**: Direct field access (no property overhead).

## Integration

`TBar` is the primary input for:

* **TBarSeries**: A collection of bars.
* **Indicators**: Some indicators (like ATR) require full `TBar` input rather than just a single value.

## Architecture Notes

* **SkipLocalsInit**: Marked with `[SkipLocalsInit]` for performance in tight loops.
* **AggressiveInlining**: All computed properties are inlined to ensure they are as fast as writing the formula manually.

## References

* [OHLC Chart](https://en.wikipedia.org/wiki/Open-high-low-close_chart)
* [C# Record Structs](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record)