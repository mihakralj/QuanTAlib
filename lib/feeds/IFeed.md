# IFeed Interface

`IFeed` defines the standard contract for all data feeds in QuanTAlib, ensuring consistent behavior across different data sources (synthetic, file-based, or live API).

## Key Concepts

- **Bidirectional Control**: The `Next(ref bool isNew)` method allows the consumer to request a new bar (`isNew = true`) or an update to the current bar (`isNew = false`).
- **Streaming**: Designed for bar-by-bar processing, simulating real-time data flow.
- **Batching**: Supports fetching historical data ranges via `Fetch()`.

## Interface Definition

```csharp
public interface IFeed
{
    /// <summary>
    /// Gets the next bar with full control over new/update state.
    /// </summary>
    TBar Next(ref bool isNew);

    /// <summary>
    /// Convenience overload for simple next-bar requests.
    /// </summary>
    TBar Next(bool isNew = true);

    /// <summary>
    /// Retrieves a batch of historical bars.
    /// </summary>
    TBarSeries Fetch(int count, long startTime, TimeSpan interval);
}
```

## Implementation Guidelines

When implementing `IFeed`:

1. **State Management**: Maintain the current position in the data source.
2. **End of Data**: When data is exhausted, `Next` should return the last valid bar and set `isNew` to `false`.
3. **Intra-bar Updates**: If the source supports it (e.g., live ticks), `Next(isNew: false)` should return the updated state of the current bar. If not supported (e.g., CSV), it should return the current bar unchanged.
4. **Thread Safety**: Implementations are generally not required to be thread-safe unless specified.

## Implementations

- **`GBM`**: Geometric Brownian Motion generator (Synthetic).
- **`CsvFeed`**: Reads OHLCV data from CSV files (Historical).
