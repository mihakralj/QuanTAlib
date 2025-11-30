using System.Globalization;
using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// CSV file feed for loading historical OHLCV data.
/// Loads data in constructor and streams through it with Next() or returns batches with Fetch().
/// CSV format: timestamp,open,high,low,close,volume (header required)
/// Timestamp format: YYYY-MM-DD (UTC midnight assumed)
/// </summary>
public class CsvFeed : IFeed
{
    private readonly TBarSeries _data;

    // Streaming state
    private int _currentIndex;
    private TBar _currentBar;
    private bool _hasCurrentBar;

    /// <summary>
    /// Loads CSV file and prepares data for streaming.
    /// Data is reversed to chronological order (oldest first).
    /// </summary>
    /// <param name="filePath">Path to CSV file</param>
    public CsvFeed(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"CSV file not found: {filePath}", filePath);

        _data = LoadFromCsv(filePath);
        _currentIndex = 0;
    }

    /// <summary>
    /// Parses CSV file into TBarSeries.
    /// Expected format: timestamp,open,high,low,close,volume
    /// Memory-efficient: reads lines into list, reverses in-place (no LINQ allocations).
    /// </summary>
    private static TBarSeries LoadFromCsv(string filePath)
    {
        var dataLines = new List<string>();
        using (var reader = new StreamReader(filePath))
        {
            var header = reader.ReadLine();
            if (header is null)
                throw new InvalidDataException("CSV file is empty");

            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (!string.IsNullOrWhiteSpace(line))
                    dataLines.Add(line);
            }
        }

        if (dataLines.Count == 0)
            throw new InvalidDataException("CSV file contains only header, no data");

        // Reverse in-place to chronological order (oldest first)
        dataLines.Reverse();

        var series = new TBarSeries(dataLines.Count);

        for (int i = 0; i < dataLines.Count; i++)
        {
            var line = dataLines[i];

            var parts = line.Split(',');
            if (parts.Length != 6)
                throw new FormatException($"Invalid CSV format at line {i + 2}. Expected 6 columns, found {parts.Length}");

            try
            {
                // Parse timestamp (YYYY-MM-DD format, assume UTC midnight)
                var timestamp = DateTime.ParseExact(parts[0].Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

                // Parse OHLCV values
                double open = double.Parse(parts[1].Trim(), CultureInfo.InvariantCulture);
                double high = double.Parse(parts[2].Trim(), CultureInfo.InvariantCulture);
                double low = double.Parse(parts[3].Trim(), CultureInfo.InvariantCulture);
                double close = double.Parse(parts[4].Trim(), CultureInfo.InvariantCulture);
                double volume = double.Parse(parts[5].Trim(), CultureInfo.InvariantCulture);

                series.Add(timestamp, open, high, low, close, volume, isNew: true);
            }
            catch (Exception ex) when (ex is FormatException or OverflowException)
            {
                throw new FormatException($"Failed to parse CSV line {i + 2}: {line}", ex);
            }
        }

        return series;
    }

    /// <summary>
    /// Gets the next bar with full bidirectional control.
    /// When end of data reached, returns last bar and sets isNew=false.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TBar Next(ref bool isNew)
    {
        if (_data.Count == 0)
        {
            isNew = false;
            return default;
        }

        if (isNew || !_hasCurrentBar)
        {
            // Request for new bar
            if (_currentIndex >= _data.Count)
            {
                // End of data - return last bar and signal no more data
                isNew = false;
                return _currentBar;
            }

            _currentBar = _data[_currentIndex];
            _currentIndex++;
            _hasCurrentBar = true;
        }
        else
        {
            // Update current bar - CSV has no intra-bar updates, return same bar
            // No change to _currentBar or _currentIndex
        }

        return _currentBar;
    }

    /// <summary>
    /// Gets the next bar with simple control.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TBar Next(bool isNew = true)
    {
        return Next(ref isNew);
    }

    /// <summary>
    /// Returns a filtered subset of data matching the criteria.
    /// Resets streaming position to start of returned data.
    /// </summary>
    public TBarSeries Fetch(int count, long startTime, TimeSpan interval)
    {
        if (count <= 0)
            throw new ArgumentException("Count must be positive", nameof(count));

        var result = new TBarSeries(count);

        // Find starting index
        int startIndex = 0;
        for (int i = 0; i < _data.Count; i++)
        {
            if (_data[i].Time >= startTime)
            {
                startIndex = i;
                break;
            }
        }

        // Collect bars matching interval
        long expectedTime = startTime;
        int collected = 0;

        for (int i = startIndex; i < _data.Count && collected < count; i++)
        {
            var bar = _data[i];

            // Check if bar time matches expected time (within tolerance)
            long timeDiff = Math.Abs(bar.Time - expectedTime);
            long tolerance = interval.Ticks / 2; // Allow 50% tolerance

            if (timeDiff <= tolerance)
            {
                result.Add(bar, isNew: true);
                collected++;
                expectedTime += interval.Ticks;
            }
            else if (bar.Time > expectedTime)
            {
                // Gap in data - skip forward
                long gaps = (bar.Time - expectedTime) / interval.Ticks;
                expectedTime += (gaps + 1) * interval.Ticks;

                if (Math.Abs(bar.Time - expectedTime + interval.Ticks) <= tolerance)
                {
                    result.Add(bar, isNew: true);
                    collected++;
                    expectedTime += interval.Ticks;
                }
            }
        }

        // Reset streaming to start of returned data
        _currentIndex = startIndex;
        _hasCurrentBar = false;

        return result;
    }
}
