# TBar Struct

`TBar` is a lightweight, immutable struct representing a single OHLCV (Open, High, Low, Close, Volume) bar. It is designed for high-performance financial data processing with minimal memory overhead.

## Key Features

- **Memory Efficient**: Pure data type occupying exactly 48 bytes (1 `long` + 5 `double`s).
- **Immutable**: Thread-safe by design.
- **Zero-Copy Conversions**: Efficiently converts to `TValue` for individual price components (Open, High, Low, Close, Volume).
- **Computed Properties**: Provides on-demand calculation of common price averages (HL2, HLC3, etc.) without storage overhead.
- **SIMD Compatible**: Layout is optimized for potential vectorization in collection types.

## Structure Definition

```csharp
public readonly struct TBar : IEquatable<TBar>
{
    public readonly long Time;    // Unix ticks
    public readonly double Open;
    public readonly double High;
    public readonly double Low;
    public readonly double Close;
    public readonly double Volume;
}
```

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `Time` | `long` | Timestamp in ticks. |
| `Open` | `double` | Opening price. |
| `High` | `double` | Highest price. |
| `Low` | `double` | Lowest price. |
| `Close` | `double` | Closing price. |
| `Volume` | `double` | Traded volume. |
| `AsDateTime` | `DateTime` | `Time` converted to UTC DateTime. |

### Computed Averages
These properties are calculated on the fly:
- `HL2`: (High + Low) / 2
- `OC2`: (Open + Close) / 2
- `OHL3`: (Open + High + Low) / 3
- `HLC3`: (High + Low + Close) / 3
- `OHLC4`: (Open + High + Low + Close) / 4
- `HLCC4`: (High + Low + Close + Close) / 4

### TValue Accessors
Efficiently access components as `TValue` (Time-Value pair):
- `O`: (Time, Open)
- `H`: (Time, High)
- `L`: (Time, Low)
- `C`: (Time, Close)
- `V`: (Time, Volume)

## Usage

### Creating a TBar
```csharp
long now = DateTime.UtcNow.Ticks;
var bar = new TBar(now, 100.0, 105.0, 95.0, 102.0, 1000.0);
```

### Implicit Conversions
```csharp
double closePrice = bar;      // Implicitly converts to Close price
TValue value = bar;           // Implicitly converts to (Time, Close)
DateTime dt = bar;            // Implicitly converts to DateTime
