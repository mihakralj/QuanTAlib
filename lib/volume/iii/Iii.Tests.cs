using Xunit;

namespace QuanTAlib.Tests;

public class IiiTests
{
    private const int DefaultPeriod = 14;

    [Fact]
    public void Constructor_DefaultParameters_CreatesValidIndicator()
    {
        var iii = new Iii();
        Assert.Equal($"Iii({DefaultPeriod})", iii.Name);
        Assert.Equal(DefaultPeriod, iii.WarmupPeriod);
        Assert.False(iii.IsHot);
    }

    [Fact]
    public void Constructor_CustomParameters_CreatesValidIndicator()
    {
        var iii = new Iii(period: 20, cumulative: true);
        Assert.Equal("Iii(20,Cum)", iii.Name);
        Assert.Equal(20, iii.WarmupPeriod);
    }

    [Fact]
    public void Constructor_InvalidPeriod_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Iii(period: 0));
        Assert.Throws<ArgumentException>(() => new Iii(period: -1));
    }

    [Fact]
    public void Update_WithTBar_ReturnsValidValue()
    {
        var iii = new Iii();
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000000);
        var result = iii.Update(bar);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_WithTValue_ThrowsNotSupportedException()
    {
        var iii = new Iii();
        var value = new TValue(DateTime.UtcNow, 100);
        Assert.Throws<NotSupportedException>(() => iii.Update(value));
    }

    [Fact]
    public void Update_CloseAtHigh_ReturnsPositiveValue()
    {
        var iii = new Iii(period: 1);
        // Close at high means position multiplier = +1
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 110, 100000);
        var result = iii.Update(bar);
        Assert.True(result.Value > 0, "Close at high should result in positive III");
    }

    [Fact]
    public void Update_CloseAtLow_ReturnsNegativeValue()
    {
        var iii = new Iii(period: 1);
        // Close at low means position multiplier = -1
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 90, 100000);
        var result = iii.Update(bar);
        Assert.True(result.Value < 0, "Close at low should result in negative III");
    }

    [Fact]
    public void Update_CloseAtMidpoint_ReturnsZero()
    {
        var iii = new Iii(period: 1);
        // Close at midpoint means position multiplier = 0
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 100, 100000);
        var result = iii.Update(bar);
        Assert.Equal(0.0, result.Value, 10);
    }

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var iii = new Iii();
        var bar1 = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000000);
        var result1 = iii.Update(bar1, isNew: true);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 105, 115, 95, 110, 1100000);
        var result2 = iii.Update(bar2, isNew: true);

        Assert.NotEqual(result1.Time, result2.Time);
    }

    [Fact]
    public void Update_IsNewFalse_UpdatesCurrentBar()
    {
        var iii = new Iii();
        var time = DateTime.UtcNow;
        var bar1 = new TBar(time, 100, 110, 90, 105, 1000000);
        iii.Update(bar1, isNew: true);

        var bar2 = new TBar(time.AddMinutes(1), 105, 115, 95, 110, 1100000);
        var result1 = iii.Update(bar2, isNew: true);

        // Update same bar with different values
        var bar2Updated = new TBar(time.AddMinutes(1), 105, 115, 95, 115, 1200000);
        var result2 = iii.Update(bar2Updated, isNew: false);

        Assert.Equal(result1.Time, result2.Time);
        Assert.NotEqual(result1.Value, result2.Value);
    }

    [Fact]
    public void Update_IterativeCorrections_UpdatesCurrentValue()
    {
        var iii = new Iii(period: 3);
        var time = DateTime.UtcNow;

        // Build up some state
        iii.Update(new TBar(time, 100, 110, 90, 105, 100000), isNew: true);
        iii.Update(new TBar(time.AddMinutes(1), 105, 115, 95, 110, 110000), isNew: true);

        // Original bar 3
        var bar3 = new TBar(time.AddMinutes(2), 110, 120, 100, 115, 120000);
        var originalResult = iii.Update(bar3, isNew: true);

        // Make a correction with different values
        var correctionBar = new TBar(time.AddMinutes(2), 100, 150, 80, 80, 200000);
        var correctedResult = iii.Update(correctionBar, isNew: false);

        // Values should differ due to different bar data
        Assert.NotEqual(originalResult.Value, correctedResult.Value);
        Assert.True(double.IsFinite(correctedResult.Value));
    }

    [Fact]
    public void Update_WarmupPeriod_IsHotBecomesTrueAfterWarmup()
    {
        var iii = new Iii(period: 3);
        var time = DateTime.UtcNow;

        Assert.False(iii.IsHot);
        iii.Update(new TBar(time, 100, 110, 90, 105, 100000), isNew: true);
        Assert.False(iii.IsHot);
        iii.Update(new TBar(time.AddMinutes(1), 105, 115, 95, 110, 110000), isNew: true);
        Assert.False(iii.IsHot);
        iii.Update(new TBar(time.AddMinutes(2), 110, 120, 100, 115, 120000), isNew: true);

        // After period bars, should be hot
        Assert.True(iii.IsHot);
    }

    [Fact]
    public void Update_WithNaN_UsesLastValidValue()
    {
        var iii = new Iii(period: 3);

        // Process some valid bars first
        iii.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 105, 100000));
        iii.Update(new TBar(DateTime.UtcNow.AddMinutes(1), 105, 115, 95, 110, 110000));

        // Process bar with NaN close (will cause NaN in calculation)
        var nanBar = new TBar(DateTime.UtcNow.AddMinutes(2), double.NaN, 120, 100, double.NaN, 120000);
        var result = iii.Update(nanBar);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_ZeroPriceRange_ReturnsZero()
    {
        var iii = new Iii(period: 1);
        // When high = low, range is 0, position multiplier is 0
        var bar = new TBar(DateTime.UtcNow, 100, 100, 100, 100, 100000);
        var result = iii.Update(bar);
        Assert.Equal(0.0, result.Value);
    }

    [Fact]
    public void Update_ZeroVolume_UsesMinimumVolume()
    {
        var iii = new Iii(period: 1);
        // Zero volume should be treated as minimum of 1
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 0);
        var result = iii.Update(bar);
        Assert.True(double.IsFinite(result.Value));
        // Position multiplier = (2*105 - 110 - 90) / 20 = 10/20 = 0.5
        // Raw III = 0.5 * 1 = 0.5
        Assert.Equal(0.5, result.Value, 10);
    }

    [Fact]
    public void Update_CumulativeMode_AccumulatesValues()
    {
        var iii = new Iii(period: 1, cumulative: true);
        var time = DateTime.UtcNow;

        // First bar with positive III
        var result1 = iii.Update(new TBar(time, 100, 110, 90, 110, 100), isNew: true);
        double firstValue = result1.Value;

        // Second bar with positive III
        var result2 = iii.Update(new TBar(time.AddMinutes(1), 100, 110, 90, 110, 100), isNew: true);

        // Cumulative should add up
        Assert.Equal(firstValue * 2, result2.Value, 10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var iii = new Iii(period: 3);
        var time = DateTime.UtcNow;

        // Process some bars
        iii.Update(new TBar(time, 100, 110, 90, 105, 100000), isNew: true);
        iii.Update(new TBar(time.AddMinutes(1), 105, 115, 95, 110, 110000), isNew: true);
        iii.Update(new TBar(time.AddMinutes(2), 110, 120, 100, 115, 120000), isNew: true);

        Assert.True(iii.IsHot);

        iii.Reset();

        Assert.False(iii.IsHot);
        Assert.Equal(default, iii.Last);
    }

    [Fact]
    public void BatchCalculate_MatchesStreaming()
    {
        var bars = new TBarSeries();
        var gbm = new GBM(seed: 42);

        for (int i = 0; i < 100; i++)
        {
            bars.Add(gbm.Next());
        }

        // Streaming
        var iii = new Iii();
        var streamingValues = new List<double>();
        foreach (var bar in bars)
        {
            streamingValues.Add(iii.Update(bar).Value);
        }

        // Batch
        var batchResult = Iii.Batch(bars);

        Assert.Equal(bars.Count, batchResult.Count);
        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(streamingValues[i], batchResult[i].Value, 10);
        }
    }

    [Fact]
    public void SpanCalculate_MatchesStreaming()
    {
        var bars = new TBarSeries();
        var gbm = new GBM(seed: 42);

        for (int i = 0; i < 100; i++)
        {
            bars.Add(gbm.Next());
        }

        // Streaming
        var iii = new Iii();
        var streamingValues = new List<double>();
        foreach (var bar in bars)
        {
            streamingValues.Add(iii.Update(bar).Value);
        }

        // Span
        var high = bars.High.Values.ToArray();
        var low = bars.Low.Values.ToArray();
        var close = bars.Close.Values.ToArray();
        var volume = bars.Volume.Values.ToArray();
        var spanValues = new double[bars.Count];

        Iii.Batch(high, low, close, volume, spanValues);

        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(streamingValues[i], spanValues[i], 10);
        }
    }

    [Fact]
    public void SpanCalculate_InvalidLengths_ThrowsArgumentException()
    {
        var high = new double[100];
        var low = new double[99]; // Different length
        var close = new double[100];
        var volume = new double[100];
        var output = new double[100];

        Assert.Throws<ArgumentException>(() => Iii.Batch(high, low, close, volume, output));
    }

    [Fact]
    public void SpanCalculate_InvalidPeriod_ThrowsArgumentException()
    {
        var high = new double[100];
        var low = new double[100];
        var close = new double[100];
        var volume = new double[100];
        var output = new double[100];

        Assert.Throws<ArgumentException>(() => Iii.Batch(high, low, close, volume, output, period: 0));
    }

    [Fact]
    public void SpanCalculate_LargeData_UsesArrayPool()
    {
        int size = 1000; // > 256 threshold
        var high = new double[size];
        var low = new double[size];
        var close = new double[size];
        var volume = new double[size];
        var output = new double[size];

        for (int i = 0; i < size; i++)
        {
            high[i] = 110 + i * 0.1;
            low[i] = 90 + i * 0.1;
            close[i] = 100 + i * 0.1;
            volume[i] = 100000;
        }

        // Should not throw
        Iii.Batch(high, low, close, volume, output);
        Assert.True(double.IsFinite(output[size - 1]));
    }

    [Fact]
    public void SpanCalculate_CumulativeMode_MatchesStreaming()
    {
        var bars = new TBarSeries();
        var gbm = new GBM(seed: 42);

        for (int i = 0; i < 50; i++)
        {
            bars.Add(gbm.Next());
        }

        // Streaming cumulative
        var iii = new Iii(period: 14, cumulative: true);
        var streamingValues = new List<double>();
        foreach (var bar in bars)
        {
            streamingValues.Add(iii.Update(bar).Value);
        }

        // Span cumulative
        var high = bars.High.Values.ToArray();
        var low = bars.Low.Values.ToArray();
        var close = bars.Close.Values.ToArray();
        var volume = bars.Volume.Values.ToArray();
        var spanValues = new double[bars.Count];

        Iii.Batch(high, low, close, volume, spanValues, period: 14, cumulative: true);

        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(streamingValues[i], spanValues[i], 10);
        }
    }

    [Fact]
    public void Event_PubFiresOnUpdate()
    {
        var iii = new Iii();
        TValue? receivedValue = null;
        bool receivedIsNew = false;

        iii.Pub += (object? sender, in TValueEventArgs args) =>
        {
            receivedValue = args.Value;
            receivedIsNew = args.IsNew;
        };

        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000000);
        iii.Update(bar, isNew: true);

        Assert.NotNull(receivedValue);
        Assert.True(receivedIsNew);
    }

    [Fact]
    public void PositionMultiplier_CalculatesCorrectly()
    {
        // Test specific position multiplier values
        var iii = new Iii(period: 1);

        // Close at 75% of range (high=110, low=90, close=105)
        // Position = (2*105 - 110 - 90) / (110-90) = 10/20 = 0.5
        var bar1 = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 200);
        var result1 = iii.Update(bar1);
        Assert.Equal(0.5 * 200, result1.Value, 10); // 0.5 * volume

        iii.Reset();

        // Close at 25% of range (high=110, low=90, close=95)
        // Position = (2*95 - 110 - 90) / (110-90) = -10/20 = -0.5
        var bar2 = new TBar(DateTime.UtcNow, 100, 110, 90, 95, 200);
        var result2 = iii.Update(bar2);
        Assert.Equal(-0.5 * 200, result2.Value, 10); // -0.5 * volume
    }
}