# TBarSeries: OHLCV Data Container

## What It Does

`TBarSeries` is a high-performance collection of OHLCV bars. It is the primary data structure for managing historical and real-time market data in QuanTAlib. It uses a **Structure of Arrays (SoA)** layout to optimize memory access and enable efficient SIMD operations across individual price components.

## Design Philosophy

A naive implementation of a bar series would be a `List<TBar>`. However, this is inefficient for technical analysis. Most indicators only need one component at a time (e.g., SMA uses Close prices). Iterating over a `List<TBar>` to get Close prices loads unnecessary Open, High, Low, and Volume data into the CPU cache, wasting bandwidth.

`TBarSeries` solves this by storing each component in its own contiguous array. This allows:

* **Component Views**: You can access `Close` prices as a `TSeries` without copying data.
* **Cache Efficiency**: Iterating over `Close` prices loads *only* Close prices.
* **Unified Time**: All component series share a single Time array, ensuring synchronization.

## How It Works

Internally, `TBarSeries` maintains six parallel lists:

1. `_t` (Time)
2. `_o` (Open)
3. `_h` (High)
4. `_l` (Low)
5. `_c` (Close)
6. `_v` (Volume)

It exposes these internal lists as `TSeries` properties (`Open`, `High`, `Low`, `Close`, `Volume`), which act as read-only views into the master data.

## Structure

### Definition

```csharp
public class TBarSeries : IReadOnlyList<TBar>
{
    // Component Views (TSeries)
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

### Core Methods

| Method | Description |
| ------ | ------ |
| `Add(TBar bar, bool isNew)` | Adds a bar or updates the last one. |
| `Add(DateTime time, double o, double h, double l, double c, double v)` | Adds raw values directly. |
| `Count` | Returns the number of bars. |
| `Last` | Returns the most recent `TBar`. |

## Usage

### Creating and Populating

```csharp
var bars = new TBarSeries();

// Add a new bar
bars.Add(new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000));

// Add raw values
bars.Add(DateTime.UtcNow, 100, 105, 95, 102, 1000);
```

### Accessing Data

```csharp
// Get the last full bar
TBar lastBar = bars.Last;

// Get the Close series (Zero-Copy)
TSeries closes = bars.Close;

// Calculate SMA on Close prices
var sma = new Sma(14);
var result = sma.Calculate(bars.Close);
```

### Streaming Updates

```csharp
// New minute starts
bars.Add(newBar, isNew: true);

// Price updates within the same minute
bars.Add(updatedBar, isNew: false); // Updates the last bar in place
```

## Performance Profile

### Operation Count (Streaming Mode)

TBarSeries stores OHLCV as separate List<T> fields (SoA layout) for cache-friendly sequential access.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Add new TBar (5 List.Add calls) | 5 | 3 cy | ~15 cy |
| Access span for SIMD | 1 | 2 cy | ~2 cy |
| Pub event fire | 1 | 5 cy | ~5 cy |
| **Total per bar** | **O(1)** | — | **~22 cy** |

SoA layout enables SIMD processing: each field array is contiguous in memory. CollectionsMarshal.AsSpan avoids copying.

* **Memory Layout**: SoA (Structure of Arrays).
* **Component Access**: Zero-copy `TSeries` views.
* **Iteration**: Cache-friendly for single-component analysis.

## Integration

`TBarSeries` is the standard input for multi-input indicators (like ATR, ADX) and the primary data source for trading strategies.

* **Indicators**: Can be passed to indicators that require full bar data.
* **Strategies**: Provides the historical context needed for signal generation.

## Architecture Notes

* **Shared Storage**: The `TSeries` views (`Open`, `Close`, etc.) do not own their data; they point to the internal lists of the `TBarSeries`. This means modifying the `TBarSeries` automatically updates all views.
* **Synchronization**: Because all views share the same `_t` (Time) list, they are guaranteed to be perfectly synchronized.

## References

* [Structure of Arrays (SoA)](https://en.wikipedia.org/wiki/AOS_and_SOA)
* [Data Locality](https://gameprogrammingpatterns.com/data-locality.html)
