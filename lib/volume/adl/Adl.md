# ADL - Accumulation/Distribution Line

The Accumulation/Distribution Line (ADL) measures the cumulative flow of money into and out of a security. It validates price trends by correlating volume with price close location within the high-low range.

## Architectural Design

We implement ADL as a stateful, streaming accumulator that maintains O(1) complexity for each new data point. Unlike window-based indicators, ADL carries its entire history in a single double-precision state variable.

### The "Close Location Value" (CLV)

The core mechanic relies on the Money Flow Multiplier (MFM), also known as CLV. This value ranges from -1 to +1:

* **+1**: Close equals High (Maximum Accumulation)
* **-1**: Close equals Low (Maximum Distribution)
* **0**: Close is exactly between High and Low

This approach avoids the noise of simple price changes, focusing instead on *where* the price settles relative to its intraday range.

$$MFM = \frac{(Close - Low) - (High - Close)}{High - Low}$$

$$MFV = MFM \times Volume$$

$$ADL_{current} = ADL_{previous} + MFV$$

### Zero-Allocation Implementation

Our implementation processes updates without heap allocations. The state consists of a single `double _lastAdl`.

* **Complexity**: O(1) per update.
* **Memory**: 16 bytes (state) + object overhead.
* **NaN Handling**: If `High == Low`, MFM is 0 to avoid division by zero. If inputs are `NaN`, the last valid ADL value is preserved.

## Usage

### Streaming API

The streaming API is designed for real-time event processing. It updates the state with each new bar and returns the latest value immediately.

```csharp
using QuanTAlib;

// Initialize
var adl = new Adl();

// Update loop
foreach (var bar in feed)
{
    var result = adl.Update(bar);
    Console.WriteLine($"ADL: {result.Value:F2}");
}
```

### Batch Processing

For historical analysis, the static `Calculate` method processes full datasets using optimized loops.

```csharp
var bars = GetHistory();
var adlSeries = Adl.Calculate(bars);
```

## Performance Benchmarks

Processing 10,000 bars on an Intel Core i9-13900K:

| Operation | Time | Allocations |
| :--- | :--- | :--- |
| Update (Single) | 2.1 ns | 0 bytes |
| Calculate (Batch) | 15 μs | 0 bytes (excluding output) |

## Validation

We validate correctness against three external authorities to 1e-9 precision:

| Library | Status | Notes |
| :--- | :--- | :--- |
| **Skender.Stock.Indicators** | ✅ Pass | Reference implementation |
| **TA-Lib** | ✅ Pass | Matches `AD` function |
| **Tulip Indicators** | ✅ Pass | Matches `ad` indicator |

See [Validation](../validation.md) for comprehensive test results.
