# TSeries: Time Series Data Container

## What It Does

`TSeries` is a high-performance, memory-efficient container for time-series data. Unlike standard collections (like `List<TValue>`), it uses a **Structure of Arrays (SoA)** layout internally. This means it stores timestamps and values in separate contiguous arrays, optimizing memory access patterns for numerical processing and SIMD vectorization.

## Design Philosophy

Standard object-oriented collections (Array of Structures - AoS) are cache-inefficient for numerical algorithms. When calculating a moving average, the CPU only needs the values, but an AoS layout forces it to load interleaved timestamps into the cache, wasting bandwidth.

`TSeries` solves this by decoupling time and value storage:

* **Cache Locality**: Iterating over values loads only values.
* **SIMD Readiness**: The internal value array can be exposed directly as a `Span<double>` for AVX/SSE processing.
* **Zero-Copy Views**: Data is accessed without defensive copying, ensuring maximum throughput.

## How It Works

`TSeries` maintains two parallel internal lists:

1. `List<long> _t`: Stores timestamps.
2. `List<double> _v`: Stores values.

It implements `IReadOnlyList<TValue>`, allowing it to be treated as a standard collection of `TValue` structs when needed, but its true power lies in its column-oriented properties (`Values`, `Times`).

## Structure

### Definition

```csharp
public class TSeries : IReadOnlyList<TValue>, ITValuePublisher
```

### Core Properties

| Property | Type | Description |
|----------|------|-------------|
| `Values` | `ReadOnlySpan<double>` | Direct access to the value array (SIMD-ready). |
| `Times` | `ReadOnlySpan<long>` | Direct access to the timestamp array. |
| `Last` | `TValue` | The most recent time-value pair. |
| `Count` | `int` | Number of elements in the series. |
| `Name` | `string` | Optional identifier for the series. |

### Events

| Event | Type | Description |
|-------|------|-------------|
| `Pub` | `Action<TValue>` | Fired whenever a new value is added or updated. |

## Usage

### Creating and Populating

```csharp
var series = new TSeries();

// Add a new bar (isNew = true by default)
series.Add(DateTime.UtcNow, 100.0);

// Add multiple values
series.Add(new List<double> { 1.0, 2.0, 3.0 });
```

### Streaming Updates (Real-time)

`TSeries` supports "bar updates" where the last value changes until the bar closes.

```csharp
// New minute starts
series.Add(time, 100.0, isNew: true);

// Price updates within the same minute
series.Add(time, 101.0, isNew: false); // Overwrites last value
series.Add(time, 102.0, isNew: false); // Overwrites last value
```

### SIMD Processing

```csharp
// Calculate average using SIMD (via Span)
double sum = 0;
foreach (var v in series.Values) { sum += v; } // Compiler vectorizes this
```

### Reactive Subscription

```csharp
series.Pub += (item) => Console.WriteLine($"New value: {item}");
```

## Performance Profile

* **Memory Layout**: SoA (Structure of Arrays).
* **Access Speed**: O(1) for random access.
* **Iteration**: Cache-friendly linear scan.
* **SIMD**: Fully supported via `Values` span.

## Integration

`TSeries` is the standard output format for all indicators in QuanTAlib.

* **Input**: Can be fed into indicators via `Update(TSeries)`.
* **Output**: Indicators return `TSeries` from their `Calculate` methods.
* **Visualization**: Easily mappable to charting libraries due to separate Time/Value arrays.

## Architecture Notes

* **CollectionsMarshal**: Uses `CollectionsMarshal.AsSpan` to expose internal list storage as spans without copying. This is unsafe if the list is modified during span access, but provides maximum performance for single-threaded algorithms.
* **Virtual Methods**: `Add` is virtual to allow derived classes (like `TBarSeries` components) to intercept updates if necessary.

## References

* [Data-Oriented Design](https://en.wikipedia.org/wiki/Data-oriented_design)
* [SIMD in .NET](https://learn.microsoft.com/en-us/dotnet/standard/simd)
