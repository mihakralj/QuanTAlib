using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QuanTAlib;

/// <summary>
/// Parsed OHLCV data from a CSV line.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct ParsedOhlcv(long Time, double Open, double High, double Low, double Close, double Volume);

/// <summary>
/// Mutable state for parsing OHLCV columns. Used as ref parameter to reduce method signature size.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal ref struct OhlcvParseState
{
    public long Time;
    public double Open;
    public double High;
    public double Low;
    public double Close;
    public double Volume;
}

/// <summary>
/// CSV file feed for loading historical OHLCV data.
/// Loads data in constructor and streams through it with Next() or returns batches with Fetch().
/// CSV format: timestamp,open,high,low,close,volume (header required)
/// Timestamp format: YYYY-MM-DD (UTC midnight assumed)
/// </summary>
[SkipLocalsInit]
public sealed class CsvFeed : IFeed
{
    private int _currentIndex;
    private TBar _currentBar;
    private bool _hasCurrentBar;

    /// <summary>
    /// Gets the total number of bars available in the CSV file.
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// Gets the file path of the loaded CSV.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets whether there are more bars to stream.
    /// </summary>
    public bool HasMore => _currentIndex < Count;

    /// <summary>
    /// Gets the current streaming position (0-based index).
    /// </summary>
    public int CurrentIndex => _currentIndex;

    /// <summary>
    /// Gets whether the feed has a current bar in progress.
    /// </summary>
    public bool HasCurrentBar => _hasCurrentBar;

    /// <summary>
    /// Loads CSV file and prepares data for streaming.
    /// Data is reversed to chronological order (oldest first).
    /// </summary>
    /// <param name="filePath">Path to CSV file</param>
    /// <exception cref="ArgumentException">Thrown when filePath is null or empty</exception>
    /// <exception cref="FileNotFoundException">Thrown when the specified file does not exist</exception>
    /// <exception cref="InvalidDataException">Thrown when CSV file is empty or contains only header</exception>
    /// <exception cref="FormatException">Thrown when CSV format is invalid</exception>
    public CsvFeed(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"CSV file not found: {filePath}", filePath);

        FilePath = filePath;
        Data = LoadFromCsv(filePath);
        Count = Data.Count;
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

        // Pre-allocate arrays for bulk loading (SoA layout)
        long[] t = new long[dataLines.Count];
        double[] o = new double[dataLines.Count];
        double[] h = new double[dataLines.Count];
        double[] l = new double[dataLines.Count];
        double[] c = new double[dataLines.Count];
        double[] v = new double[dataLines.Count];

        for (int i = 0; i < dataLines.Count; i++)
        {
            var line = dataLines[i];
            int originalLineNumber = dataLines.Count - i + 1;

            var parsed = ParseCsvLine(line, originalLineNumber);
            t[i] = parsed.Time;
            o[i] = parsed.Open;
            h[i] = parsed.High;
            l[i] = parsed.Low;
            c[i] = parsed.Close;
            v[i] = parsed.Volume;
        }

        // Bulk add to series
        series.Add(t, o, h, l, c, v);

        return series;
    }

    /// <summary>
    /// Parses a single CSV line into OHLCV components.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ParsedOhlcv ParseCsvLine(string line, int lineNumber)
    {
        // Use Span-based splitting for reduced allocations
        ReadOnlySpan<char> lineSpan = line.AsSpan();

        int col = 0;
        int start = 0;
        OhlcvParseState state = default;

        for (int i = 0; i < lineSpan.Length; i++)
        {
            if (lineSpan[i] == ',')
            {
                var segment = lineSpan[start..i].Trim();
                ParseColumn(segment, col, lineNumber, line, ref state);
                col++;
                start = i + 1;
            }
        }

        // Process the last segment after the final comma
        if (start <= lineSpan.Length)
        {
            var segment = lineSpan[start..].Trim();
            ParseColumn(segment, col, lineNumber, line, ref state);
            col++;
        }

        if (col != 6)
        {
            throw new FormatException($"Invalid CSV format at line {lineNumber}. Expected 6 columns, found {col}");
        }

        return new ParsedOhlcv(state.Time, state.Open, state.High, state.Low, state.Close, state.Volume);
    }

    /// <summary>
    /// Parses a single column value into the appropriate OHLCV field.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ParseColumn(
        ReadOnlySpan<char> segment,
        int col,
        int lineNumber,
        string line,
        ref OhlcvParseState state)
    {
        switch (col)
        {
            case 0: // Timestamp
                if (!DateTime.TryParseExact(segment, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var timestamp))
                {
                    throw new FormatException($"Failed to parse timestamp at line {lineNumber}: {line}");
                }
                state.Time = timestamp.Ticks;
                break;
            case 1: // Open
                if (!double.TryParse(segment, NumberStyles.Float, CultureInfo.InvariantCulture, out state.Open))
                {
                    throw new FormatException($"Failed to parse open price at line {lineNumber}: {line}");
                }
                break;
            case 2: // High
                if (!double.TryParse(segment, NumberStyles.Float, CultureInfo.InvariantCulture, out state.High))
                {
                    throw new FormatException($"Failed to parse high price at line {lineNumber}: {line}");
                }
                break;
            case 3: // Low
                if (!double.TryParse(segment, NumberStyles.Float, CultureInfo.InvariantCulture, out state.Low))
                {
                    throw new FormatException($"Failed to parse low price at line {lineNumber}: {line}");
                }
                break;
            case 4: // Close
                if (!double.TryParse(segment, NumberStyles.Float, CultureInfo.InvariantCulture, out state.Close))
                {
                    throw new FormatException($"Failed to parse close price at line {lineNumber}: {line}");
                }
                break;
            case 5: // Volume
                if (!double.TryParse(segment, NumberStyles.Float, CultureInfo.InvariantCulture, out state.Volume))
                {
                    throw new FormatException($"Failed to parse volume at line {lineNumber}: {line}");
                }
                break;
            default:
                // Extra columns are ignored - this handles the default case requirement
                break;
        }
    }

    /// <summary>
    /// Gets the next bar with full bidirectional control.
    /// When end of data reached, returns last bar and sets isNew=false.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TBar Next(ref bool isNew)
    {
        if (Count == 0)
        {
            isNew = false;
            return default;
        }

        if (isNew || !_hasCurrentBar)
        {
            if (_currentIndex >= Count)
            {
                isNew = false;
                return _currentBar;
            }

            _currentBar = Data[_currentIndex];
            _currentIndex++;
            _hasCurrentBar = true;
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
    /// <param name="count">Number of bars to retrieve (must be positive)</param>
    /// <param name="startTime">Starting timestamp in ticks</param>
    /// <param name="interval">Time interval between bars</param>
    /// <returns>A TBarSeries containing the matched bars</returns>
    /// <exception cref="ArgumentException">Thrown when count is not positive</exception>
    public TBarSeries Fetch(int count, long startTime, TimeSpan interval)
    {
        if (count <= 0)
            throw new ArgumentException("Count must be positive", nameof(count));

        var result = new TBarSeries(count);

        // Find starting index using binary search for better performance
        int startIndex = FindStartIndex(startTime);

        if (startIndex == -1)
            return result;

        // Collect bars matching interval
        long expectedTime = startTime;
        int collected = 0;
        long tolerance = interval.Ticks / 2;

        for (int i = startIndex; i < Count && collected < count; i++)
        {
            var bar = Data[i];

            // Check if bar time matches expected time (within tolerance)
            long timeDiff = Math.Abs(bar.Time - expectedTime);

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
                expectedTime += gaps * interval.Ticks;

                if (Math.Abs(bar.Time - expectedTime) <= tolerance)
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

    /// <summary>
    /// Finds the starting index for the given start time using binary search.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int FindStartIndex(long startTime)
    {
        if (Count == 0)
            return -1;

        if (Data[0].Time >= startTime)
            return 0;

        if (Data[Count - 1].Time < startTime)
            return -1;

        int left = 0;
        int right = Count - 1;

        while (left < right)
        {
            int mid = left + (right - left) / 2;

            if (Data[mid].Time < startTime)
                left = mid + 1;
            else
                right = mid;
        }

        return left;
    }

    /// <summary>
    /// Resets the streaming position to the beginning.
    /// </summary>
    public void Reset()
    {
        _currentIndex = 0;
        _hasCurrentBar = false;
        _currentBar = default;
    }

    /// <summary>
    /// Resets the streaming position to a specific index.
    /// </summary>
    /// <param name="index">The index to reset to (must be valid)</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is out of range</exception>
    public void Reset(int index)
    {
        if (index < 0 || index > Count)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Index must be between 0 and {Count}");

        _currentIndex = index;
        _hasCurrentBar = false;
        _currentBar = default;
    }

    /// <summary>
    /// Gets the bar at the specified index without affecting streaming position.
    /// </summary>
    /// <param name="index">The index of the bar to retrieve</param>
    /// <returns>The bar at the specified index</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is out of range</exception>
    public TBar GetBar(int index)
    {
        if (index < 0 || index >= Count)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Index must be between 0 and {Count - 1}");

        return Data[index];
    }

    /// <summary>
    /// Gets the underlying data series (read-only access).
    /// </summary>
    public TBarSeries Data { get; }
}
