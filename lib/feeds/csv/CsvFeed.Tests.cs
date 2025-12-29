
namespace QuanTAlib.Tests;

public sealed class CsvFeedTests : IDisposable
{
    private const string TestCsvPath = "daily_IBM.csv";
    private readonly List<string> _tempFiles = new();
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var file in _tempFiles)
        {
            if (File.Exists(file))
            {
                try { File.Delete(file); } catch { /* ignore */ }
            }
        }
    }

    private string CreateTempCsv(string[] lines)
    {
        string tempPath = Path.GetTempFileName() + ".csv";
        File.WriteAllLines(tempPath, lines);
        _tempFiles.Add(tempPath);
        return tempPath;
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_ValidFile_LoadsData()
    {
        var feed = new CsvFeed(TestCsvPath);
        Assert.NotNull(feed);
        Assert.True(feed.Count > 0);
    }

    [Fact]
    public void Constructor_NonExistentFile_ThrowsFileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(() => new CsvFeed("nonexistent.csv"));
    }

    [Fact]
    public void Constructor_NullPath_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new CsvFeed(null!));
        Assert.Equal("filePath", ex.ParamName);
    }

    [Fact]
    public void Constructor_EmptyPath_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new CsvFeed(""));
        Assert.Equal("filePath", ex.ParamName);
    }

    [Fact]
    public void Constructor_WhitespacePath_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new CsvFeed("   "));
        Assert.Equal("filePath", ex.ParamName);
    }

    [Fact]
    public void Constructor_EmptyCsv_ThrowsInvalidDataException()
    {
        string tempCsv = CreateTempCsv(Array.Empty<string>());
        Assert.Throws<InvalidDataException>(() => new CsvFeed(tempCsv));
    }

    [Fact]
    public void Constructor_HeaderOnlyCsv_ThrowsInvalidDataException()
    {
        string tempCsv = CreateTempCsv(new[] { "timestamp,open,high,low,close,volume" });
        Assert.Throws<InvalidDataException>(() => new CsvFeed(tempCsv));
    }

    [Fact]
    public void Constructor_MalformedDate_ThrowsFormatException()
    {
        string tempCsv = CreateTempCsv(new[]
        {
            "timestamp,open,high,low,close,volume",
            "not-a-date,100,101,99,100,1000"
        });
        Assert.Throws<FormatException>(() => new CsvFeed(tempCsv));
    }

    [Fact]
    public void Constructor_MalformedPrice_ThrowsFormatException()
    {
        string tempCsv = CreateTempCsv(new[]
        {
            "timestamp,open,high,low,close,volume",
            "2023-01-01,not-a-number,101,99,100,1000"
        });
        Assert.Throws<FormatException>(() => new CsvFeed(tempCsv));
    }

    [Fact]
    public void Constructor_MissingColumns_ThrowsFormatException()
    {
        string tempCsv = CreateTempCsv(new[]
        {
            "timestamp,open,high,low,close,volume",
            "2023-01-01,100,101,99,100"  // Missing volume
        });
        Assert.Throws<FormatException>(() => new CsvFeed(tempCsv));
    }

    [Fact]
    public void Constructor_ExtraColumns_ThrowsFormatException()
    {
        // Extra columns should throw format exception (strict 6-column format)
        string tempCsv = CreateTempCsv(new[]
        {
            "timestamp,open,high,low,close,volume,extra",
            "2023-01-01,100,101,99,100,1000,extra_data"
        });
        Assert.Throws<FormatException>(() => new CsvFeed(tempCsv));
    }

    #endregion

    #region Property Tests

    [Fact]
    public void Count_ReturnsCorrectNumber()
    {
        var feed = new CsvFeed(TestCsvPath);
        Assert.True(feed.Count > 0);
        // IBM CSV has 100 rows of data
        Assert.Equal(100, feed.Count);
    }

    [Fact]
    public void FilePath_ReturnsLoadedPath()
    {
        var feed = new CsvFeed(TestCsvPath);
        Assert.Equal(TestCsvPath, feed.FilePath);
    }

    [Fact]
    public void HasMore_TrueAtStart()
    {
        var feed = new CsvFeed(TestCsvPath);
        Assert.True(feed.HasMore);
    }

    [Fact]
    public void HasMore_FalseWhenExhausted()
    {
        string tempCsv = CreateTempCsv(new[]
        {
            "timestamp,open,high,low,close,volume",
            "2023-01-01,100,101,99,100,1000"
        });
        var feed = new CsvFeed(tempCsv);

        Assert.True(feed.HasMore);
        feed.Next(isNew: true);
        Assert.False(feed.HasMore);
    }

    [Fact]
    public void CurrentIndex_StartsAtZero()
    {
        var feed = new CsvFeed(TestCsvPath);
        Assert.Equal(0, feed.CurrentIndex);
    }

    [Fact]
    public void CurrentIndex_IncrementsOnNext()
    {
        var feed = new CsvFeed(TestCsvPath);

        Assert.Equal(0, feed.CurrentIndex);
        feed.Next(isNew: true);
        Assert.Equal(1, feed.CurrentIndex);
        feed.Next(isNew: true);
        Assert.Equal(2, feed.CurrentIndex);
    }

    [Fact]
    public void CurrentIndex_DoesNotIncrementOnUpdate()
    {
        var feed = new CsvFeed(TestCsvPath);

        feed.Next(isNew: true);
        int indexAfterFirst = feed.CurrentIndex;

        feed.Next(isNew: false);
        Assert.Equal(indexAfterFirst, feed.CurrentIndex);
    }

    [Fact]
    public void HasCurrentBar_FalseAtStart()
    {
        var feed = new CsvFeed(TestCsvPath);
        Assert.False(feed.HasCurrentBar);
    }

    [Fact]
    public void HasCurrentBar_TrueAfterNext()
    {
        var feed = new CsvFeed(TestCsvPath);
        feed.Next(isNew: true);
        Assert.True(feed.HasCurrentBar);
    }

    [Fact]
    public void Data_ReturnsUnderlyingSeries()
    {
        var feed = new CsvFeed(TestCsvPath);
        var data = feed.Data;

        Assert.NotNull(data);
        Assert.Equal(feed.Count, data.Count);
    }

    #endregion

    #region Next Method Tests

    [Fact]
    public void Next_StreamsDataChronologically()
    {
        var feed = new CsvFeed(TestCsvPath);

        // Get first bar
        var bar1 = feed.Next(isNew: true);
        Assert.True(bar1.Time > 0);

        // Get second bar - should be later in time
        var bar2 = feed.Next(isNew: true);
        Assert.True(bar2.Time > bar1.Time);

        // Get third bar
        var bar3 = feed.Next(isNew: true);
        Assert.True(bar3.Time > bar2.Time);
    }

    [Fact]
    public void Next_WithRefParameter_StreamsCorrectly()
    {
        var feed = new CsvFeed(TestCsvPath);

        bool isNew = true;
        var bar1 = feed.Next(ref isNew);
        Assert.True(isNew); // Should still be true
        Assert.True(bar1.Time > 0);

        isNew = true;
        var bar2 = feed.Next(ref isNew);
        Assert.True(isNew);
        Assert.True(bar2.Time > bar1.Time);
    }

    [Fact]
    public void Next_UpdateCurrentBar_ReturnsSameBar()
    {
        var feed = new CsvFeed(TestCsvPath);

        // Get first bar
        var bar1 = feed.Next(isNew: true);

        // Update current bar (should return same bar)
        var bar2 = feed.Next(isNew: false);
        Assert.Equal(bar1.Time, bar2.Time);
        Assert.Equal(bar1.Close, bar2.Close);

        // Get next bar
        var bar3 = feed.Next(isNew: true);
        Assert.True(bar3.Time > bar1.Time);
    }

    [Fact]
    public void Next_EndOfData_SignalsNoMoreData()
    {
        var feed = new CsvFeed(TestCsvPath);

        // Stream through all data
        TBar lastBar = default;
        bool isNew = true;
        int count = 0;

        while (isNew && count < 200) // Safety limit
        {
            lastBar = feed.Next(ref isNew);
            count++;
        }

        // Should have reached end and isNew should be false
        Assert.False(isNew);
        Assert.True(lastBar.Time > 0);

        // Calling again should return same bar with isNew=false
        isNew = true;
        var finalBar = feed.Next(ref isNew);
        Assert.False(isNew);
        Assert.Equal(lastBar.Time, finalBar.Time);
    }

    [Fact]
    public void Next_EmptyData_ReturnsDefaultAndSignalsNoMore()
    {
        // Create a mock scenario - but since constructor throws on empty,
        // we test the behavior when all data is consumed
        string tempCsv = CreateTempCsv(new[]
        {
            "timestamp,open,high,low,close,volume",
            "2023-01-01,100,101,99,100,1000"
        });
        var feed = new CsvFeed(tempCsv);

        // Consume all data
        bool isNew = true;
        feed.Next(ref isNew);

        // Now at end
        isNew = true;
        var bar = feed.Next(ref isNew);
        Assert.False(isNew);
        Assert.Equal(100.0, bar.Close); // Returns last bar
    }

    [Fact]
    public void Next_DefaultParameter_IsNewTrue()
    {
        var feed = new CsvFeed(TestCsvPath);

        var bar1 = feed.Next(); // Default isNew = true
        var bar2 = feed.Next(); // Default isNew = true
        Assert.True(bar2.Time > bar1.Time);
    }

    #endregion

    #region Fetch Method Tests

    [Fact]
    public void Fetch_ReturnsCorrectNumberOfBars()
    {
        var feed = new CsvFeed(TestCsvPath);

        var startTime = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
        var interval = TimeSpan.FromDays(1);

        var series = feed.Fetch(10, startTime, interval);

        Assert.True(series.Count > 0);
        Assert.True(series.Count <= 10);
    }

    [Fact]
    public void Fetch_ZeroCount_ThrowsArgumentException()
    {
        var feed = new CsvFeed(TestCsvPath);
        var startTime = DateTime.UtcNow.Ticks;
        var interval = TimeSpan.FromDays(1);

        var ex = Assert.Throws<ArgumentException>(() => feed.Fetch(0, startTime, interval));
        Assert.Equal("count", ex.ParamName);
    }

    [Fact]
    public void Fetch_NegativeCount_ThrowsArgumentException()
    {
        var feed = new CsvFeed(TestCsvPath);
        var startTime = DateTime.UtcNow.Ticks;
        var interval = TimeSpan.FromDays(1);

        var ex = Assert.Throws<ArgumentException>(() => feed.Fetch(-1, startTime, interval));
        Assert.Equal("count", ex.ParamName);
    }

    [Fact]
    public void Fetch_ResetsStreamingPosition()
    {
        var feed = new CsvFeed(TestCsvPath);

        // Stream a few bars
        feed.Next(isNew: true);
        feed.Next(isNew: true);
        feed.Next(isNew: true);

        // Fetch from start
        var startTime = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
        feed.Fetch(5, startTime, TimeSpan.FromDays(1));

        // Next should now stream from fetched position
        var bar = feed.Next(isNew: true);
        Assert.True(bar.Time >= startTime);
    }

    [Fact]
    public void Fetch_ResetsHasCurrentBar()
    {
        var feed = new CsvFeed(TestCsvPath);

        feed.Next(isNew: true);
        Assert.True(feed.HasCurrentBar);

        var startTime = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
        feed.Fetch(5, startTime, TimeSpan.FromDays(1));

        Assert.False(feed.HasCurrentBar);
    }

    #endregion

    #region Reset Method Tests

    [Fact]
    public void Reset_ReturnsToStart()
    {
        var feed = new CsvFeed(TestCsvPath);

        // Advance several bars
        var firstBar = feed.Next(isNew: true);
        feed.Next(isNew: true);
        feed.Next(isNew: true);
        Assert.Equal(3, feed.CurrentIndex);

        // Reset
        feed.Reset();

        Assert.Equal(0, feed.CurrentIndex);
        Assert.True(feed.HasMore);
        Assert.False(feed.HasCurrentBar);

        // Next bar should be first bar again
        var afterReset = feed.Next(isNew: true);
        Assert.Equal(firstBar.Time, afterReset.Time);
        Assert.Equal(firstBar.Close, afterReset.Close);
    }

    [Fact]
    public void Reset_WithIndex_SetsCorrectPosition()
    {
        var feed = new CsvFeed(TestCsvPath);

        // Reset to middle
        int targetIndex = 50;
        feed.Reset(targetIndex);

        Assert.Equal(targetIndex, feed.CurrentIndex);
        Assert.False(feed.HasCurrentBar);

        // Next bar should be at that index
        var bar = feed.Next(isNew: true);
        var expectedBar = feed.GetBar(targetIndex);
        Assert.Equal(expectedBar.Time, bar.Time);
    }

    [Fact]
    public void Reset_WithNegativeIndex_ThrowsArgumentOutOfRangeException()
    {
        var feed = new CsvFeed(TestCsvPath);
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => feed.Reset(-1));
        Assert.Equal("index", ex.ParamName);
    }

    [Fact]
    public void Reset_WithIndexBeyondCount_ThrowsArgumentOutOfRangeException()
    {
        var feed = new CsvFeed(TestCsvPath);
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => feed.Reset(feed.Count + 1));
        Assert.Equal("index", ex.ParamName);
    }

    [Fact]
    public void Reset_WithIndexAtCount_IsValid()
    {
        // Resetting to exactly Count means "at end" - valid but no more data
        var feed = new CsvFeed(TestCsvPath);
        feed.Reset(feed.Count);

        Assert.Equal(feed.Count, feed.CurrentIndex);
        Assert.False(feed.HasMore);
    }

    #endregion

    #region GetBar Method Tests

    [Fact]
    public void GetBar_ReturnsCorrectBar()
    {
        var feed = new CsvFeed(TestCsvPath);

        // Get bar without affecting streaming
        var bar0 = feed.GetBar(0);
        var bar1 = feed.GetBar(1);

        // Streaming position unchanged
        Assert.Equal(0, feed.CurrentIndex);

        // Bars should be in chronological order
        Assert.True(bar1.Time > bar0.Time);
    }

    [Fact]
    public void GetBar_NegativeIndex_ThrowsArgumentOutOfRangeException()
    {
        var feed = new CsvFeed(TestCsvPath);
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => feed.GetBar(-1));
        Assert.Equal("index", ex.ParamName);
    }

    [Fact]
    public void GetBar_IndexAtCount_ThrowsArgumentOutOfRangeException()
    {
        var feed = new CsvFeed(TestCsvPath);
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => feed.GetBar(feed.Count));
        Assert.Equal("index", ex.ParamName);
    }

    [Fact]
    public void GetBar_DoesNotAffectStreaming()
    {
        var feed = new CsvFeed(TestCsvPath);

        // Stream first bar
        var streamed = feed.Next(isNew: true);
        int indexAfter = feed.CurrentIndex;

        // Random access
        var bar50 = feed.GetBar(50);
        Assert.True(bar50.Time > 0);

        // Streaming position unchanged
        Assert.Equal(indexAfter, feed.CurrentIndex);

        // Continue streaming
        var next = feed.Next(isNew: true);
        Assert.True(next.Time > streamed.Time);
    }

    [Fact]
    public void GetBar_ConsistentWithNext()
    {
        var feed = new CsvFeed(TestCsvPath);

        // Get bars via random access
        var bar0 = feed.GetBar(0);
        var bar1 = feed.GetBar(1);
        var bar2 = feed.GetBar(2);

        // Get same bars via streaming
        var streamed0 = feed.Next(isNew: true);
        var streamed1 = feed.Next(isNew: true);
        var streamed2 = feed.Next(isNew: true);

        Assert.Equal(bar0.Time, streamed0.Time);
        Assert.Equal(bar1.Time, streamed1.Time);
        Assert.Equal(bar2.Time, streamed2.Time);
    }

    #endregion

    #region OHLCV Validation Tests

    [Fact]
    public void LoadFromCsv_ParsesValuesCorrectly()
    {
        var feed = new CsvFeed(TestCsvPath);

        // Get first bar (oldest in chronological order)
        var bar = feed.Next(isNew: true);

        // Verify it has valid OHLCV data
        Assert.True(bar.Open > 0);
        Assert.True(bar.High >= bar.Open);
        Assert.True(bar.High >= bar.Close);
        Assert.True(bar.Low <= bar.Open);
        Assert.True(bar.Low <= bar.Close);
        Assert.True(bar.Close > 0);
        Assert.True(bar.Volume > 0);
    }

    [Fact]
    public void LoadFromCsv_DataInChronologicalOrder()
    {
        var feed = new CsvFeed(TestCsvPath);

        var bars = new List<TBar>();
        bool isNew = true;

        // Collect first 10 bars
        for (int i = 0; i < 10 && isNew; i++)
        {
            bars.Add(feed.Next(ref isNew));
        }

        // Verify chronological order (each bar later than previous)
        for (int i = 1; i < bars.Count; i++)
        {
            Assert.True(bars[i].Time > bars[i - 1].Time,
                $"Bar {i} time ({bars[i].AsDateTime}) should be after bar {i - 1} time ({bars[i - 1].AsDateTime})");
        }
    }

    [Fact]
    public void LoadFromCsv_AllBarsHaveValidOHLCV()
    {
        var feed = new CsvFeed(TestCsvPath);

        for (int i = 0; i < feed.Count; i++)
        {
            var bar = feed.GetBar(i);

            Assert.True(double.IsFinite(bar.Open), $"Bar {i} has non-finite Open");
            Assert.True(double.IsFinite(bar.High), $"Bar {i} has non-finite High");
            Assert.True(double.IsFinite(bar.Low), $"Bar {i} has non-finite Low");
            Assert.True(double.IsFinite(bar.Close), $"Bar {i} has non-finite Close");
            Assert.True(double.IsFinite(bar.Volume), $"Bar {i} has non-finite Volume");

            Assert.True(bar.High >= bar.Low, $"Bar {i}: High ({bar.High}) < Low ({bar.Low})");
            Assert.True(bar.High >= bar.Open, $"Bar {i}: High ({bar.High}) < Open ({bar.Open})");
            Assert.True(bar.High >= bar.Close, $"Bar {i}: High ({bar.High}) < Close ({bar.Close})");
            Assert.True(bar.Low <= bar.Open, $"Bar {i}: Low ({bar.Low}) > Open ({bar.Open})");
            Assert.True(bar.Low <= bar.Close, $"Bar {i}: Low ({bar.Low}) > Close ({bar.Close})");
        }
    }

    [Fact]
    public void LoadFromCsv_ParsesDecimalsCorrectly()
    {
        string tempCsv = CreateTempCsv(new[]
        {
            "timestamp,open,high,low,close,volume",
            "2023-01-01,100.1234,101.5678,99.9999,100.0001,1234567.89"
        });
        var feed = new CsvFeed(tempCsv);
        var bar = feed.Next(isNew: true);

        Assert.Equal(100.1234, bar.Open, precision: 4);
        Assert.Equal(101.5678, bar.High, precision: 4);
        Assert.Equal(99.9999, bar.Low, precision: 4);
        Assert.Equal(100.0001, bar.Close, precision: 4);
        Assert.Equal(1234567.89, bar.Volume, precision: 2);
    }

    [Fact]
    public void LoadFromCsv_ParsesNegativeValues()
    {
        // While negative prices are unusual, the parser should handle them
        string tempCsv = CreateTempCsv(new[]
        {
            "timestamp,open,high,low,close,volume",
            "2023-01-01,-100,50,-150,-50,1000"
        });
        var feed = new CsvFeed(tempCsv);
        var bar = feed.Next(isNew: true);

        Assert.Equal(-100, bar.Open);
        Assert.Equal(50, bar.High);
        Assert.Equal(-150, bar.Low);
        Assert.Equal(-50, bar.Close);
    }

    [Fact]
    public void LoadFromCsv_ParsesScientificNotation()
    {
        string tempCsv = CreateTempCsv(new[]
        {
            "timestamp,open,high,low,close,volume",
            "2023-01-01,1.5e2,2e2,1e2,1.75e2,1e6"
        });
        var feed = new CsvFeed(tempCsv);
        var bar = feed.Next(isNew: true);

        Assert.Equal(150, bar.Open);
        Assert.Equal(200, bar.High);
        Assert.Equal(100, bar.Low);
        Assert.Equal(175, bar.Close);
        Assert.Equal(1000000, bar.Volume);
    }

    #endregion

    #region IFeed Interface Tests

    [Fact]
    public void CsvFeed_WorksWithIFeedInterface()
    {
        IFeed feed = new CsvFeed(TestCsvPath);

        var bar1 = feed.Next(isNew: true);
        Assert.True(bar1.Time > 0);

        var bar2 = feed.Next(isNew: true);
        Assert.True(bar2.Time > bar1.Time);
    }

    [Fact]
    public void CsvFeed_IFeedRefOverload()
    {
        IFeed feed = new CsvFeed(TestCsvPath);

        bool isNew = true;
        var bar1 = feed.Next(ref isNew);
        Assert.True(bar1.Time > 0);

        isNew = false;
        var bar1Update = feed.Next(ref isNew);
        Assert.Equal(bar1.Time, bar1Update.Time);
    }

    [Fact]
    public void CsvFeed_IFeedFetch()
    {
        IFeed feed = new CsvFeed(TestCsvPath);

        var startTime = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
        var series = feed.Fetch(5, startTime, TimeSpan.FromDays(1));

        Assert.True(series.Count > 0);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void Next_MixedNewAndUpdate_WorksCorrectly()
    {
        var feed = new CsvFeed(TestCsvPath);

        var bar1 = feed.Next(isNew: true);
        var bar1Update = feed.Next(isNew: false);
        Assert.Equal(bar1.Time, bar1Update.Time);

        var bar2 = feed.Next(isNew: true);
        Assert.True(bar2.Time > bar1.Time);

        var bar2Update = feed.Next(isNew: false);
        Assert.Equal(bar2.Time, bar2Update.Time);

        var bar3 = feed.Next(isNew: true);
        Assert.True(bar3.Time > bar2.Time);
    }

    [Fact]
    public void Fetch_WithEarlyStartTime_ReturnsData()
    {
        var feed = new CsvFeed(TestCsvPath);

        // Start from very early date (before any data)
        var startTime = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
        var series = feed.Fetch(5, startTime, TimeSpan.FromDays(1));

        // Should return data starting from first available bar
        Assert.True(series.Count > 0);
    }

    [Fact]
    public void Fetch_WithFutureStartTime_ReturnsEmpty()
    {
        var feed = new CsvFeed(TestCsvPath);

        // Start from future date (after all data)
        var startTime = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
        var series = feed.Fetch(5, startTime, TimeSpan.FromDays(1));

        // Should return empty or minimal data
        Assert.Empty(series);
    }

    [Fact]
    public void Fetch_HandlesGapsCorrectly()
    {
        // Create CSV with gaps using helper
        string tempCsv = CreateTempCsv(new[]
        {
            "timestamp,open,high,low,close,volume",
            "2023-01-05,103,104,102,103,1000",
            "2023-01-04,102,103,101,102,1000",
            "2023-01-02,101,102,100,101,1000",
            "2023-01-01,100,101,99,100,1000"
        });

        var feed = new CsvFeed(tempCsv);
        var startTime = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
        var interval = TimeSpan.FromDays(1);

        // Fetch bars. Should get 4 bars (Jan 1, 2, 4, 5).
        var series = feed.Fetch(10, startTime, interval);

        Assert.Equal(4, series.Count);
        Assert.Equal(startTime, series[0].Time); // Jan 1
        Assert.Equal(startTime + interval.Ticks, series[1].Time); // Jan 2
        // Gap here (Jan 3 missing)
        Assert.Equal(startTime + 3 * interval.Ticks, series[2].Time); // Jan 4
        Assert.Equal(startTime + 4 * interval.Ticks, series[3].Time); // Jan 5
    }

    [Fact]
    public void SingleBar_StreamsAndEnds()
    {
        string tempCsv = CreateTempCsv(new[]
        {
            "timestamp,open,high,low,close,volume",
            "2023-01-01,100,101,99,100,1000"
        });
        var feed = new CsvFeed(tempCsv);

        Assert.Equal(1, feed.Count);
        Assert.True(feed.HasMore);

        bool isNew = true;
        var bar = feed.Next(ref isNew);
        Assert.True(isNew);
        Assert.Equal(100.0, bar.Close);
        Assert.False(feed.HasMore);

        // Try to get next
        isNew = true;
        var noMore = feed.Next(ref isNew);
        Assert.False(isNew); // Signals end
        Assert.Equal(bar.Time, noMore.Time); // Returns last bar
    }

    [Fact]
    public void WhitespaceInValues_Trimmed()
    {
        string tempCsv = CreateTempCsv(new[]
        {
            "timestamp,open,high,low,close,volume",
            "  2023-01-01  ,  100  ,  101  ,  99  ,  100  ,  1000  "
        });
        var feed = new CsvFeed(tempCsv);
        var bar = feed.Next(isNew: true);

        Assert.Equal(100.0, bar.Open);
        Assert.Equal(101.0, bar.High);
        Assert.Equal(99.0, bar.Low);
        Assert.Equal(100.0, bar.Close);
        Assert.Equal(1000.0, bar.Volume);
    }

    [Fact]
    public void ZeroValues_Accepted()
    {
        string tempCsv = CreateTempCsv(new[]
        {
            "timestamp,open,high,low,close,volume",
            "2023-01-01,0,0,0,0,0"
        });
        var feed = new CsvFeed(tempCsv);
        var bar = feed.Next(isNew: true);

        Assert.Equal(0.0, bar.Open);
        Assert.Equal(0.0, bar.High);
        Assert.Equal(0.0, bar.Low);
        Assert.Equal(0.0, bar.Close);
        Assert.Equal(0.0, bar.Volume);
    }

    [Fact]
    public void VeryLargeValues_Parsed()
    {
        string tempCsv = CreateTempCsv(new[]
        {
            "timestamp,open,high,low,close,volume",
            "2023-01-01,999999999.99,1000000000.01,999999999.00,999999999.50,9999999999999"
        });
        var feed = new CsvFeed(tempCsv);
        var bar = feed.Next(isNew: true);

        Assert.Equal(999999999.99, bar.Open, precision: 2);
        Assert.Equal(1000000000.01, bar.High, precision: 2);
        Assert.Equal(999999999.00, bar.Low, precision: 2);
        Assert.Equal(999999999.50, bar.Close, precision: 2);
        Assert.Equal(9999999999999.0, bar.Volume, precision: 0);
    }

    [Fact]
    public void ConsecutiveResets_WorkCorrectly()
    {
        var feed = new CsvFeed(TestCsvPath);

        feed.Next(isNew: true);
        feed.Next(isNew: true);
        feed.Reset();
        feed.Reset();
        feed.Reset();

        Assert.Equal(0, feed.CurrentIndex);
        Assert.False(feed.HasCurrentBar);
    }

    [Fact]
    public void StreamThenResetThenStream_Consistent()
    {
        var feed = new CsvFeed(TestCsvPath);

        // First pass
        var firstPass = new List<double>();
        for (int i = 0; i < 10; i++)
        {
            firstPass.Add(feed.Next(isNew: true).Close);
        }

        // Reset
        feed.Reset();

        // Second pass
        var secondPass = new List<double>();
        for (int i = 0; i < 10; i++)
        {
            secondPass.Add(feed.Next(isNew: true).Close);
        }

        // Should be identical
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal(firstPass[i], secondPass[i]);
        }
    }

    #endregion
}
