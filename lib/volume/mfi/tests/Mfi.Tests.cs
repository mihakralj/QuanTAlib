using Xunit;

namespace QuanTAlib.Tests;

public class MfiTests
{
    private const int DefaultPeriod = 14;

    [Fact]
    public void Constructor_DefaultParameters_CreatesValidIndicator()
    {
        var mfi = new Mfi();
        Assert.Equal($"Mfi({DefaultPeriod})", mfi.Name);
        Assert.Equal(DefaultPeriod, mfi.WarmupPeriod);
        Assert.False(mfi.IsHot);
    }

    [Fact]
    public void Constructor_CustomParameters_CreatesValidIndicator()
    {
        var mfi = new Mfi(period: 20);
        Assert.Equal("Mfi(20)", mfi.Name);
        Assert.Equal(20, mfi.WarmupPeriod);
    }

    [Fact]
    public void Constructor_InvalidPeriod_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Mfi(period: 0));
        Assert.Throws<ArgumentException>(() => new Mfi(period: -1));
    }

    [Fact]
    public void Update_WithTBar_ReturnsValidValue()
    {
        var mfi = new Mfi();
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000000);
        var result = mfi.Update(bar);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_WithTValue_ThrowsNotSupportedException()
    {
        var mfi = new Mfi();
        var value = new TValue(DateTime.UtcNow, 100);
        Assert.Throws<NotSupportedException>(() => mfi.Update(value));
    }

    [Fact]
    public void Update_ReturnsValuesBetween0And100()
    {
        var mfi = new Mfi(period: 5);
        var gbm = new GBM(seed: 42);

        for (int i = 0; i < 100; i++)
        {
            var result = mfi.Update(gbm.Next());
            Assert.True(result.Value >= 0 && result.Value <= 100, $"MFI value {result.Value} out of range [0, 100]");
        }
    }

    [Fact]
    public void Update_PriceIncrease_TrendsTowardHighMfi()
    {
        var mfi = new Mfi(period: 5);
        var time = DateTime.UtcNow;

        // Consistent uptrend should push MFI toward higher values
        for (int i = 0; i < 20; i++)
        {
            double basePrice = 100 + (i * 5); // Consistent price increase
            mfi.Update(new TBar(time.AddMinutes(i), basePrice, basePrice + 2, basePrice - 1, basePrice + 1, 100000));
        }

        // After consistent uptrend, MFI should be relatively high
        Assert.True(mfi.Last.Value > 50, $"MFI should be above 50 in uptrend, was {mfi.Last.Value}");
    }

    [Fact]
    public void Update_PriceDecrease_TrendsTowardLowMfi()
    {
        var mfi = new Mfi(period: 5);
        var time = DateTime.UtcNow;

        // Consistent downtrend should push MFI toward lower values
        for (int i = 0; i < 20; i++)
        {
            double basePrice = 500 - (i * 5); // Consistent price decrease
            mfi.Update(new TBar(time.AddMinutes(i), basePrice, basePrice + 1, basePrice - 2, basePrice - 1, 100000));
        }

        // After consistent downtrend, MFI should be relatively low
        Assert.True(mfi.Last.Value < 50, $"MFI should be below 50 in downtrend, was {mfi.Last.Value}");
    }

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var mfi = new Mfi();
        var bar1 = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000000);
        var result1 = mfi.Update(bar1, isNew: true);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 105, 115, 95, 110, 1100000);
        var result2 = mfi.Update(bar2, isNew: true);

        Assert.NotEqual(result1.Time, result2.Time);
    }

    [Fact]
    public void Update_IsNewFalse_UpdatesCurrentBar()
    {
        var mfi = new Mfi(period: 5);
        var gbm = new GBM(seed: 42);

        // Build up history with random walk (creates mixed positive/negative flows)
        for (int i = 0; i < 20; i++)
        {
            mfi.Update(gbm.Next(), isNew: true);
        }

        // Get current state
        var bar1 = gbm.Next();
        var result1 = mfi.Update(bar1, isNew: true);

        // Create a significantly different bar for correction
        var bar2 = new TBar(bar1.Time, bar1.Open * 0.9, bar1.High * 0.85, bar1.Low * 0.9, bar1.Close * 0.85, bar1.Volume * 2);
        var result2 = mfi.Update(bar2, isNew: false);

        Assert.Equal(result1.Time, result2.Time);
        Assert.NotEqual(result1.Value, result2.Value);
    }

    [Fact]
    public void Update_IterativeCorrections_RestoresState()
    {
        var mfi = new Mfi(period: 5);
        var gbm = new GBM(seed: 123);

        // Build up history with random walk (creates mixed positive/negative flows)
        for (int i = 0; i < 20; i++)
        {
            mfi.Update(gbm.Next(), isNew: true);
        }

        // New bar
        var originalBar = gbm.Next();
        var originalResult = mfi.Update(originalBar, isNew: true);

        // Correction with significantly different values
        var correctionBar = new TBar(originalBar.Time, originalBar.Open * 0.8, originalBar.High * 0.75, originalBar.Low * 0.8, originalBar.Close * 0.75, originalBar.Volume * 3);
        var correctedResult = mfi.Update(correctionBar, isNew: false);

        Assert.NotEqual(originalResult.Value, correctedResult.Value);
        Assert.True(double.IsFinite(correctedResult.Value));
    }

    [Fact]
    public void Update_WarmupPeriod_IsHotBecomesTrueAfterWarmup()
    {
        var mfi = new Mfi(period: 5);
        var time = DateTime.UtcNow;

        Assert.False(mfi.IsHot);

        for (int i = 0; i < 4; i++)
        {
            mfi.Update(new TBar(time.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 100000), isNew: true);
            Assert.False(mfi.IsHot);
        }

        mfi.Update(new TBar(time.AddMinutes(4), 105, 115, 95, 110, 100000), isNew: true);
        Assert.True(mfi.IsHot);
    }

    [Fact]
    public void Update_WithNaN_UsesLastValidValue()
    {
        var mfi = new Mfi(period: 5);
        var time = DateTime.UtcNow;

        // Process some valid bars first
        for (int i = 0; i < 10; i++)
        {
            mfi.Update(new TBar(time.AddMinutes(i), 100, 105, 95, 102, 100000));
        }

        // Process bar with NaN volume
        var nanBar = new TBar(time.AddMinutes(10), 105, 110, 100, 108, double.NaN);
        var result = mfi.Update(nanBar);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_ZeroVolume_HandlesGracefully()
    {
        var mfi = new Mfi(period: 5);
        var time = DateTime.UtcNow;

        mfi.Update(new TBar(time, 100, 110, 90, 105, 100000));
        var result = mfi.Update(new TBar(time.AddMinutes(1), 105, 115, 95, 110, 0));

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_FlatPrice_NeutralMfi()
    {
        var mfi = new Mfi(period: 5);
        var time = DateTime.UtcNow;

        // First bar establishes baseline
        mfi.Update(new TBar(time, 100, 105, 95, 100, 100000));

        // Subsequent bars with same typical price
        for (int i = 1; i < 10; i++)
        {
            mfi.Update(new TBar(time.AddMinutes(i), 100, 105, 95, 100, 100000));
        }

        // With no positive or negative flow, MFI should be neutral (50)
        Assert.Equal(50.0, mfi.Last.Value, 5);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var mfi = new Mfi(period: 5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            mfi.Update(new TBar(time.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 100000), isNew: true);
        }

        Assert.True(mfi.IsHot);
        Assert.True(double.IsFinite(mfi.Last.Value));

        mfi.Reset();

        Assert.False(mfi.IsHot);
        Assert.Equal(default, mfi.Last);
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
        var mfi = new Mfi();
        var streamingValues = new List<double>();
        foreach (var bar in bars)
        {
            streamingValues.Add(mfi.Update(bar).Value);
        }

        // Batch
        var batchResult = Mfi.Batch(bars);

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
        var mfi = new Mfi();
        var streamingValues = new List<double>();
        foreach (var bar in bars)
        {
            streamingValues.Add(mfi.Update(bar).Value);
        }

        // Span
        var high = bars.High.Values.ToArray();
        var low = bars.Low.Values.ToArray();
        var close = bars.Close.Values.ToArray();
        var volume = bars.Volume.Values.ToArray();
        var output = new double[bars.Count];

        Mfi.Batch(high, low, close, volume, output);

        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(streamingValues[i], output[i], 10);
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

        Assert.Throws<ArgumentException>(() => Mfi.Batch(high, low, close, volume, output));
    }

    [Fact]
    public void SpanCalculate_InvalidPeriod_ThrowsArgumentException()
    {
        var high = new double[100];
        var low = new double[100];
        var close = new double[100];
        var volume = new double[100];
        var output = new double[100];

        Assert.Throws<ArgumentException>(() => Mfi.Batch(high, low, close, volume, output, period: 0));
    }

    [Fact]
    public void SpanCalculate_EmptyInput_HandlesGracefully()
    {
        var high = Array.Empty<double>();
        var low = Array.Empty<double>();
        var close = Array.Empty<double>();
        var volume = Array.Empty<double>();
        var output = Array.Empty<double>();

        Mfi.Batch(high, low, close, volume, output);

        Assert.Empty(output);
    }

    [Fact]
    public void Event_PubFiresOnUpdate()
    {
        var mfi = new Mfi();
        TValue? receivedValue = null;
        bool receivedIsNew = false;

        mfi.Pub += (object? sender, in TValueEventArgs args) =>
        {
            receivedValue = args.Value;
            receivedIsNew = args.IsNew;
        };

        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000000);
        mfi.Update(bar, isNew: true);

        Assert.NotNull(receivedValue);
        Assert.True(receivedIsNew);
    }

    [Fact]
    public void CustomPeriods_AffectsResults()
    {
        var bars = new TBarSeries();
        var gbm = new GBM(seed: 42);

        for (int i = 0; i < 100; i++)
        {
            bars.Add(gbm.Next());
        }

        var mfi1 = new Mfi(period: 7);
        var mfi2 = new Mfi(period: 21);

        foreach (var bar in bars)
        {
            mfi1.Update(bar);
            mfi2.Update(bar);
        }

        // Different periods should produce different results
        Assert.NotEqual(mfi1.Last.Value, mfi2.Last.Value);
    }

    [Fact]
    public void LargeDataset_HandlesWithoutError()
    {
        var bars = new TBarSeries();
        var gbm = new GBM(seed: 42);

        for (int i = 0; i < 10000; i++)
        {
            bars.Add(gbm.Next());
        }

        var mfi = new Mfi();
        foreach (var bar in bars)
        {
            var result = mfi.Update(bar);
            Assert.True(double.IsFinite(result.Value));
            Assert.True(result.Value >= 0 && result.Value <= 100);
        }

        Assert.True(mfi.IsHot);
    }
}
