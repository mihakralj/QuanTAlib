using Xunit;

namespace QuanTAlib.Tests;

public class EomTests
{
    private const int DefaultPeriod = 14;
    private const double DefaultVolumeScale = 10000;

    [Fact]
    public void Constructor_DefaultParameters_CreatesValidIndicator()
    {
        var eom = new Eom();
        Assert.Equal($"Eom({DefaultPeriod},{DefaultVolumeScale:F0})", eom.Name);
        Assert.Equal(DefaultPeriod + 1, eom.WarmupPeriod);
        Assert.False(eom.IsHot);
    }

    [Fact]
    public void Constructor_CustomParameters_CreatesValidIndicator()
    {
        var eom = new Eom(period: 20, volumeScale: 50000);
        Assert.Equal("Eom(20,50000)", eom.Name);
        Assert.Equal(21, eom.WarmupPeriod);
    }

    [Fact]
    public void Constructor_InvalidPeriod_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Eom(period: 0));
        Assert.Throws<ArgumentException>(() => new Eom(period: -1));
    }

    [Fact]
    public void Constructor_InvalidVolumeScale_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Eom(volumeScale: 0));
        Assert.Throws<ArgumentException>(() => new Eom(volumeScale: -1));
    }

    [Fact]
    public void Update_WithTBar_ReturnsValidValue()
    {
        var eom = new Eom();
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000000);
        var result = eom.Update(bar);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_WithTValue_ThrowsNotSupportedException()
    {
        var eom = new Eom();
        var value = new TValue(DateTime.UtcNow, 100);
        Assert.Throws<NotSupportedException>(() => eom.Update(value));
    }

    [Fact]
    public void Update_FirstBar_ReturnsZero()
    {
        var eom = new Eom();
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000000);
        var result = eom.Update(bar);
        // First bar has no previous midpoint, so raw EOM is 0
        Assert.Equal(0.0, result.Value);
    }

    [Fact]
    public void Update_PriceIncrease_ReturnsPositiveValue()
    {
        var eom = new Eom(period: 1, volumeScale: 10000);
        // First bar
        eom.Update(new TBar(DateTime.UtcNow, 100, 105, 95, 102, 100000));
        // Second bar with price increase
        var result = eom.Update(new TBar(DateTime.UtcNow, 102, 115, 100, 112, 100000));
        Assert.True(result.Value > 0, "Price increase should result in positive EOM");
    }

    [Fact]
    public void Update_PriceDecrease_ReturnsNegativeValue()
    {
        var eom = new Eom(period: 1, volumeScale: 10000);
        // First bar
        eom.Update(new TBar(DateTime.UtcNow, 110, 115, 105, 112, 100000));
        // Second bar with price decrease
        var result = eom.Update(new TBar(DateTime.UtcNow, 108, 105, 90, 92, 100000));
        Assert.True(result.Value < 0, "Price decrease should result in negative EOM");
    }

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var eom = new Eom();
        var bar1 = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000000);
        var result1 = eom.Update(bar1, isNew: true);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 105, 115, 95, 110, 1100000);
        var result2 = eom.Update(bar2, isNew: true);

        Assert.NotEqual(result1.Time, result2.Time);
    }

    [Fact]
    public void Update_IsNewFalse_UpdatesCurrentBar()
    {
        var eom = new Eom();
        var time = DateTime.UtcNow;
        var bar1 = new TBar(time, 100, 110, 90, 105, 1000000);
        eom.Update(bar1, isNew: true);

        var bar2 = new TBar(time.AddMinutes(1), 105, 115, 95, 110, 1100000);
        var result1 = eom.Update(bar2, isNew: true);

        // Update same bar with different values (using same time)
        var bar2Updated = new TBar(time.AddMinutes(1), 105, 120, 95, 118, 1200000);
        var result2 = eom.Update(bar2Updated, isNew: false);

        Assert.Equal(result1.Time, result2.Time);
        Assert.NotEqual(result1.Value, result2.Value);
    }

    [Fact]
    public void Update_IterativeCorrections_UpdatesCurrentValue()
    {
        var eom = new Eom(period: 3);
        var time = DateTime.UtcNow;

        // Build up some state
        eom.Update(new TBar(time, 100, 110, 90, 105, 100000), isNew: true);
        eom.Update(new TBar(time.AddMinutes(1), 105, 115, 95, 110, 110000), isNew: true);

        // Original bar 3
        var bar3 = new TBar(time.AddMinutes(2), 110, 120, 100, 115, 120000);
        var originalResult = eom.Update(bar3, isNew: true);

        // Make a correction with different values
        var correctionBar = new TBar(time.AddMinutes(2), 100, 150, 80, 130, 200000);
        var correctedResult = eom.Update(correctionBar, isNew: false);

        // Values should differ due to different bar data
        Assert.NotEqual(originalResult.Value, correctedResult.Value);

        // Verify the correction actually changed the value
        Assert.True(double.IsFinite(correctedResult.Value));
    }

    [Fact]
    public void Update_WarmupPeriod_IsHotBecomesTrueAfterWarmup()
    {
        var eom = new Eom(period: 3);
        var time = DateTime.UtcNow;

        Assert.False(eom.IsHot);
        eom.Update(new TBar(time, 100, 110, 90, 105, 100000), isNew: true);
        Assert.False(eom.IsHot);
        eom.Update(new TBar(time.AddMinutes(1), 105, 115, 95, 110, 110000), isNew: true);
        Assert.False(eom.IsHot);
        eom.Update(new TBar(time.AddMinutes(2), 110, 120, 100, 115, 120000), isNew: true);

        // After period bars, should be hot (count >= period and has prev midpoint)
        Assert.True(eom.IsHot);
    }

    [Fact]
    public void Update_WithNaN_UsesLastValidValue()
    {
        var eom = new Eom(period: 3, volumeScale: 10000);

        // Process some valid bars first
        eom.Update(new TBar(DateTime.UtcNow, 100, 105, 95, 102, 100000));
        eom.Update(new TBar(DateTime.UtcNow.AddMinutes(1), 102, 108, 98, 105, 110000));

        // Process bar with NaN volume (will cause NaN in calculation)
        var nanBar = new TBar(DateTime.UtcNow.AddMinutes(2), 105, 110, 100, 108, double.NaN);
        var result = eom.Update(nanBar);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_ZeroPriceRange_ReturnsZeroEom()
    {
        var eom = new Eom(period: 1, volumeScale: 10000);
        eom.Update(new TBar(DateTime.UtcNow, 100, 100, 100, 100, 100000));
        var result = eom.Update(new TBar(DateTime.UtcNow.AddMinutes(1), 105, 105, 105, 105, 100000));
        Assert.Equal(0.0, result.Value);
    }

    [Fact]
    public void Update_ZeroVolume_ReturnsZeroEom()
    {
        var eom = new Eom(period: 1, volumeScale: 10000);
        eom.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 105, 100000));
        var result = eom.Update(new TBar(DateTime.UtcNow.AddMinutes(1), 105, 115, 95, 110, 0));
        Assert.Equal(0.0, result.Value);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var eom = new Eom(period: 3);
        var time = DateTime.UtcNow;

        // Process some bars
        eom.Update(new TBar(time, 100, 110, 90, 105, 100000), isNew: true);
        eom.Update(new TBar(time.AddMinutes(1), 105, 115, 95, 110, 110000), isNew: true);
        eom.Update(new TBar(time.AddMinutes(2), 110, 120, 100, 115, 120000), isNew: true);

        Assert.True(eom.IsHot);

        eom.Reset();

        Assert.False(eom.IsHot);
        Assert.Equal(default, eom.Last);
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
        var eom = new Eom();
        var streamingValues = new List<double>();
        foreach (var bar in bars)
        {
            streamingValues.Add(eom.Update(bar).Value);
        }

        // Batch
        var batchResult = Eom.Calculate(bars);

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
        var eom = new Eom();
        var streamingValues = new List<double>();
        foreach (var bar in bars)
        {
            streamingValues.Add(eom.Update(bar).Value);
        }

        // Span
        var high = bars.High.Values.ToArray();
        var low = bars.Low.Values.ToArray();
        var volume = bars.Volume.Values.ToArray();
        var spanValues = new double[bars.Count];

        Eom.Calculate(high, low, volume, spanValues);

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
        var volume = new double[100];
        var output = new double[100];

        Assert.Throws<ArgumentException>(() => Eom.Calculate(high, low, volume, output));
    }

    [Fact]
    public void SpanCalculate_InvalidPeriod_ThrowsArgumentException()
    {
        var high = new double[100];
        var low = new double[100];
        var volume = new double[100];
        var output = new double[100];

        Assert.Throws<ArgumentException>(() => Eom.Calculate(high, low, volume, output, period: 0));
    }

    [Fact]
    public void SpanCalculate_InvalidVolumeScale_ThrowsArgumentException()
    {
        var high = new double[100];
        var low = new double[100];
        var volume = new double[100];
        var output = new double[100];

        Assert.Throws<ArgumentException>(() => Eom.Calculate(high, low, volume, output, volumeScale: 0));
    }

    [Fact]
    public void SpanCalculate_LargeData_UsesArrayPool()
    {
        int size = 1000; // > 256 threshold
        var high = new double[size];
        var low = new double[size];
        var volume = new double[size];
        var output = new double[size];

        for (int i = 0; i < size; i++)
        {
            high[i] = 110 + i * 0.1;
            low[i] = 90 + i * 0.1;
            volume[i] = 100000;
        }

        // Should not throw
        Eom.Calculate(high, low, volume, output);
        Assert.True(double.IsFinite(output[size - 1]));
    }

    [Fact]
    public void Event_PubFiresOnUpdate()
    {
        var eom = new Eom();
        TValue? receivedValue = null;
        bool receivedIsNew = false;

        eom.Pub += (object? sender, in TValueEventArgs args) =>
        {
            receivedValue = args.Value;
            receivedIsNew = args.IsNew;
        };

        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000000);
        eom.Update(bar, isNew: true);

        Assert.NotNull(receivedValue);
        Assert.True(receivedIsNew);
    }

    [Fact]
    public void VolumeScale_AffectsResult()
    {
        var eom1 = new Eom(period: 1, volumeScale: 10000);
        var eom2 = new Eom(period: 1, volumeScale: 100000);

        var bar1 = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000000);
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 105, 120, 95, 115, 1000000);

        eom1.Update(bar1); eom1.Update(bar2);
        eom2.Update(bar1); eom2.Update(bar2);

        // Different volume scales should produce different results
        Assert.NotEqual(eom1.Last.Value, eom2.Last.Value);
    }

}
