namespace QuanTAlib;

/// <summary>
/// Interface for data feeds that provide TBar (OHLCV) data.
/// Implementations include synthetic generators (GBM), API-based feeds (AlphaVantage),
/// file readers (CSV), and real-time streams (WebSocket).
/// </summary>
public interface IFeed
{
    /// <summary>
    /// Gets the next bar from the feed with full bidirectional control.
    /// </summary>
    /// <param name="isNew">
    /// Input: Request for new bar (true) or update current bar (false).
    /// Output: Actual behavior - may differ if feed cannot honor request (e.g., end of data).
    /// </param>
    /// <returns>The bar (new or updated)</returns>
    TBar Next(ref bool isNew);

    /// <summary>
    /// Gets the next bar from the feed with simple control.
    /// </summary>
    /// <param name="isNew">Request for new bar (true) or update current bar (false). Defaults to true.</param>
    /// <returns>The bar (new or updated)</returns>
    TBar Next(bool isNew = true);

    /// <summary>
    /// Gets multiple bars in batch with explicit time parameters.
    /// </summary>
    /// <param name="count">Number of bars to retrieve</param>
    /// <param name="startTime">Starting timestamp for first bar (in ticks)</param>
    /// <param name="interval">Time interval between bars</param>
    /// <returns>Series containing the requested bars</returns>
    TBarSeries Fetch(int count, long startTime, TimeSpan interval);
}
