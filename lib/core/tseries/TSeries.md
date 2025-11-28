# TSeries: Time Series Data

## Overview

`TSeries` is a high-performance container for time-series data. Unlike a standard `List<TValue>`, it uses a **Structure of Arrays (SoA)** layout internally. This means it stores timestamps and values in separate contiguous arrays (`List<long>` and `List<double>`).

This layout is critical for performance because it allows:
1.  **SIMD Optimization**: The `Values` property returns a `ReadOnlySpan<double>` that can be directly processed by CPU vector instructions (AVX/SSE).
2.  **Cache Locality**: Iterating over values doesn't load timestamps into the CPU cache, and vice versa.

## Structure

```csharp
public class TSeries : IReadOnlyList<TValue>
{
    // Internal SoA storage
    protected readonly List<long> _t;
    protected readonly List<double> _v;

    // Public accessors
    public ReadOnlySpan<double> Values => ...; // Zero-copy access
    public ReadOnlySpan<long> Times => ...;    // Zero-copy access
    
    public TValue Last { get; }
    public int Count { get; }
}
```

## Key Features

*   **SoA Layout**: Optimized for numerical computing and SIMD.
*   **Zero-Copy Access**: `Values` and `Times` properties expose internal storage as Spans without copying.
*   **Streaming Support**: The `Add` method supports `isNew` parameter to handle intra-bar updates (replacing the last value instead of appending).
*   **Event Publishing**: Optional `Pub` event for reactive pipelines.

## Usage

### Creating and Adding Data
```csharp
var series = new TSeries();
series.Add(DateTime.Now, 100.0); // isNew=true by default
```

### Streaming Updates
```csharp
// New bar
series.Add(time, 100.0, isNew: true);

// Update current bar (e.g. price change within same minute)
series.Add(time, 101.0, isNew: false); 
```

### SIMD Processing
```csharp
// Calculate average using SIMD
double avg = series.Values.AverageSIMD();
