# IFeed Interface

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Feed                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | None                      |
| **Outputs**      | Single series (IFeed)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | 1 bar                          |

- `IFeed` defines the standard contract for all data feeds in QuanTAlib, ensuring consistent behavior across different data sources (synthetic, file-based, or live API).
- No configurable parameters; computation is stateless per bar.
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.
- Feeds are the entry point of any indicator chain — all indicators subscribe to a feed or to another indicator's output.
- Common feed implementations include synthetic (GBM), file-based (CSV), and live API sources (Alpha Vantage).
- For backtesting, pair feeds with any trend, momentum, or volatility indicator to simulate streaming bar-by-bar processing.

`IFeed` defines the standard contract for all data feeds in QuanTAlib, ensuring consistent behavior across different data sources (synthetic, file-based, or live API).

## Key Concepts

* **Bidirectional Control**: The `Next(ref bool isNew)` method allows the consumer to request a new bar (`isNew = true`) or an update to the current bar (`isNew = false`).
* **Streaming**: Designed for bar-by-bar processing, simulating real-time data flow.
* **Batching**: Supports fetching historical data ranges via `Fetch()`.

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

* **`GBM`**: Geometric Brownian Motion generator (Synthetic).
* **`CsvFeed`**: Reads OHLCV data from CSV files (Historical).
* **`AlphaVantage`**: Fetches OHLCV data from the Alpha Vantage REST API (Historical/Live).
