# CsvFeed Class

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Feed                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `filePath`                      |
| **Outputs**      | Single series (CsvFeed)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | 1 bar                          |

- `CsvFeed` is a file-based feed implementation that loads historical OHLCV data from CSV files.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

`CsvFeed` is a file-based feed implementation that loads historical OHLCV data from CSV files. It supports both streaming access (simulating real-time playback) and batch retrieval.

## Key Features

* **Historical Data Loading**: Reads standard OHLCV CSV files.
* **Chronological Ordering**: Automatically reverses data if needed (assumes newest-first in file, provides oldest-first).
* **Streaming Interface**: Implements `IFeed` for consistent usage with other feed types.
* **Batch Retrieval**: Supports fetching specific time ranges via `Fetch()`.

## CSV Format Requirements

The file must have a header row and follow this column order:
`timestamp, open, high, low, close, volume`

* **Timestamp**: `YYYY-MM-DD` (assumed UTC midnight)
* **Prices/Volume**: Numeric values

Example:

```csv
Date,Open,High,Low,Close,Volume
2024-01-01,100.0,105.0,99.0,102.5,10000
2024-01-02,102.5,103.0,101.0,101.5,8500
```

## Class Definition

```csharp
public class CsvFeed : IFeed
{
    public CsvFeed(string filePath);
    public TBar Next(bool isNew = true);
    public TBarSeries Fetch(int count, long startTime, TimeSpan interval);
}
```

## Usage

### 1. Loading Data

```csharp
var feed = new CsvFeed("path/to/data.csv");
```

### 2. Streaming Data (Simulation)

```csharp
// Get first bar
var bar = feed.Next(isNew: true);

// Loop through all data
while (true)
{
    // Process bar...
    Console.WriteLine(bar);

    // Get next bar
    bool isNew = true;
    bar = feed.Next(ref isNew);

    // Stop if no more new data
    if (!isNew) break;
}
```

### 3. Fetching a Batch

```csharp
long startTime = new DateTime(2024, 1, 1).Ticks;
var batch = feed.Fetch(10, startTime, TimeSpan.FromDays(1));