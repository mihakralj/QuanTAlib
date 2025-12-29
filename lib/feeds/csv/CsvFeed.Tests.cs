
namespace QuanTAlib.Tests;

public class CsvFeedTests
{
    private const string TestCsvPath = "daily_IBM.csv";

    [Fact]
    public void Constructor_ValidFile_LoadsData()
    {
        var feed = new CsvFeed(TestCsvPath);
        Assert.NotNull(feed);
    }

    [Fact]
    public void Constructor_NonExistentFile_ThrowsFileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(() => new CsvFeed("nonexistent.csv"));
    }

    [Fact]
    public void Constructor_NullPath_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new CsvFeed(null!));
    }

    [Fact]
    public void Constructor_EmptyPath_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new CsvFeed(""));
    }

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
    public void Fetch_InvalidCount_ThrowsArgumentException()
    {
        var feed = new CsvFeed(TestCsvPath);

        var startTime = DateTime.UtcNow.Ticks;
        var interval = TimeSpan.FromDays(1);

        Assert.Throws<ArgumentException>(() => feed.Fetch(0, startTime, interval));
        Assert.Throws<ArgumentException>(() => feed.Fetch(-1, startTime, interval));
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
                $"Bar {i} time ({bars[i].AsDateTime}) should be after bar {i-1} time ({bars[i-1].AsDateTime})");
        }
    }

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
        Assert.True(series.Count == 0);
    }

    [Fact]
    public void Fetch_HandlesGapsCorrectly()
    {
        string tempCsv = Path.GetTempFileName() + ".csv";
        try
        {
            // Create CSV with gaps
            // Date, Open, High, Low, Close, Volume
            // 2023-01-01 (Sunday)
            // 2023-01-02 (Monday)
            // 2023-01-04 (Wednesday) - Gap of Tuesday
            // 2023-01-05 (Thursday)
            var lines = new[]
            {
                "Date,Open,High,Low,Close,Volume",
                "2023-01-05,103,104,102,103,1000",
                "2023-01-04,102,103,101,102,1000",
                "2023-01-02,101,102,100,101,1000",
                "2023-01-01,100,101,99,100,1000"
            };
            File.WriteAllLines(tempCsv, lines);

            var feed = new CsvFeed(tempCsv);
            var startTime = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
            var interval = TimeSpan.FromDays(1);

            // Fetch 5 bars. Should get 4 bars (Jan 1, 2, 4, 5).
            var series = feed.Fetch(10, startTime, interval);

            Assert.Equal(4, series.Count);
            Assert.Equal(startTime, series[0].Time); // Jan 1
            Assert.Equal(startTime + interval.Ticks, series[1].Time); // Jan 2
            // Gap here
            Assert.Equal(startTime + 3 * interval.Ticks, series[2].Time); // Jan 4
            Assert.Equal(startTime + 4 * interval.Ticks, series[3].Time); // Jan 5
        }
        finally
        {
            if (File.Exists(tempCsv))
                File.Delete(tempCsv);
        }
    }
}
