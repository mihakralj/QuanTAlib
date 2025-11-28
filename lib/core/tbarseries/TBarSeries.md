# TBarSeries Class

`TBarSeries` is a high-performance collection of OHLCV bars implemented using a Structure of Arrays (SoA) layout. This design optimizes memory access patterns and enables efficient SIMD operations while providing convenient object-oriented views.

## Key Features

- **Structure of Arrays (SoA)**: Stores Time, Open, High, Low, Close, and Volume in separate contiguous arrays rather than an array of structs. This improves cache locality for operations that only need specific components (e.g., calculating SMA on Close prices).
- **Zero-Copy Views**: Exposes `TSeries` properties (`Open`, `High`, `Low`, `Close`, `Volume`) that view the underlying data without copying.
- **Streaming Support**: Efficiently handles real-time data updates with `Add(bar, isNew: false)`.
- **Memory Efficient**: Minimizes object overhead by using shared internal lists.

## Class Definition

```csharp
public class TBarSeries : IReadOnlyList<TBar>
{
    // Views
    public TSeries Open { get; }
    public TSeries High { get; }
    public TSeries Low { get; }
    public TSeries Close { get; }
    public TSeries Volume { get; }

    // Aliases
    public TSeries O => Open;
    public TSeries H => High;
    public TSeries L => Low;
    public TSeries C => Close;
    public TSeries V => Volume;
}
```

## Core Methods

| Method | Description |
|--------|-------------|
| `Add(TBar bar, bool isNew = true)` | Adds a new bar or updates the last one. |
| `Add(DateTime time, double o, double h, double l, double c, double v, bool isNew)` | Adds raw values directly. |
| `Count` | Returns the number of bars. |
| `Last` | Returns the most recent `TBar`. |

## Usage

### Creating and Populating
```csharp
var bars = new TBarSeries();

// Add a new bar
long now = DateTime.UtcNow.Ticks;
bars.Add(new TBar(now, 100, 105, 95, 102, 1000), isNew: true);

// Update the last bar (e.g., real-time feed update)
bars.Add(new TBar(now, 100, 106, 95, 104, 1500), isNew: false);
```

### Accessing Data
```csharp
// Access entire bar
TBar lastBar = bars.Last;

// Access specific component series (Zero-Copy)
TSeries closes = bars.Close;
double lastClose = closes.Last.Value;

// Access via indexer
TBar firstBar = bars[0];
```

### Performance Note
Because `TBarSeries` uses SoA layout, iterating over a single component (like `Close` prices) is extremely cache-efficient. The CPU prefetcher can load contiguous doubles without loading the interleaved Open, High, Low, or Volume data.
