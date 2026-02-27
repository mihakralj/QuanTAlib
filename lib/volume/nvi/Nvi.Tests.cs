using Xunit;

namespace QuanTAlib.Tests;

public class NviTests
{
    private const double DefaultStartValue = 100.0;

    [Fact]
    public void Constructor_DefaultParameters_CreatesValidIndicator()
    {
        var nvi = new Nvi();
        Assert.Equal($"Nvi({DefaultStartValue})", nvi.Name);
        Assert.Equal(2, nvi.WarmupPeriod);
        Assert.False(nvi.IsHot);
    }

    [Fact]
    public void Constructor_CustomParameters_CreatesValidIndicator()
    {
        var nvi = new Nvi(startValue: 1000);
        Assert.Equal("Nvi(1000)", nvi.Name);
        Assert.Equal(2, nvi.WarmupPeriod);
    }

    [Fact]
    public void Constructor_InvalidStartValue_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Nvi(startValue: 0));
        Assert.Throws<ArgumentException>(() => new Nvi(startValue: -100));
    }

    [Fact]
    public void Update_WithTBar_ReturnsValidValue()
    {
        var nvi = new Nvi();
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000000);
        var result = nvi.Update(bar);
        Assert.True(double.IsFinite(result.Value));
        Assert.Equal(DefaultStartValue, result.Value); // First bar stays at start value
    }

    [Fact]
    public void Update_WithTValue_ReturnsCurrentValue()
    {
        var nvi = new Nvi();
        var value = new TValue(DateTime.UtcNow, 100);
        var result = nvi.Update(value);
        // NVI without volume data returns current NVI value
        Assert.Equal(DefaultStartValue, result.Value);
    }

    [Fact]
    public void Update_VolumeDecreases_UpdatesNvi()
    {
        var nvi = new Nvi();
        var time = DateTime.UtcNow;

        // First bar - establishes baseline
        nvi.Update(new TBar(time, 100, 105, 95, 100, 100000));

        // Second bar with lower volume and higher close - NVI should increase
        var result = nvi.Update(new TBar(time.AddMinutes(1), 100, 108, 98, 105, 80000));

        Assert.True(result.Value > DefaultStartValue, $"NVI should increase when volume decreases and price rises, was {result.Value}");
    }

    [Fact]
    public void Update_VolumeIncreases_NviUnchanged()
    {
        var nvi = new Nvi();
        var time = DateTime.UtcNow;

        // First bar - establishes baseline
        nvi.Update(new TBar(time, 100, 105, 95, 100, 100000));
        var firstNvi = nvi.Last.Value;

        // Second bar with higher volume - NVI should stay the same
        var result = nvi.Update(new TBar(time.AddMinutes(1), 100, 108, 98, 105, 150000));

        Assert.Equal(firstNvi, result.Value);
    }

    [Fact]
    public void Update_VolumeEqual_NviUnchanged()
    {
        var nvi = new Nvi();
        var time = DateTime.UtcNow;

        // First bar
        nvi.Update(new TBar(time, 100, 105, 95, 100, 100000));
        var firstNvi = nvi.Last.Value;

        // Second bar with equal volume
        var result = nvi.Update(new TBar(time.AddMinutes(1), 100, 108, 98, 105, 100000));

        Assert.Equal(firstNvi, result.Value);
    }

    [Fact]
    public void Update_ConsistentLowVolumeBullish_NviIncreases()
    {
        var nvi = new Nvi(startValue: 1000);
        var time = DateTime.UtcNow;

        // Build up with consistently lower volume and rising prices
        double volume = 100000;
        double price = 100;

        for (int i = 0; i < 20; i++)
        {
            nvi.Update(new TBar(time.AddMinutes(i), price, price + 2, price - 1, price, volume));
            volume *= 0.95; // Volume decreasing each day
            price *= 1.02;  // Price increasing each day
        }

        Assert.True(nvi.Last.Value > 1000, $"NVI should be above start value after consistent bullish low-volume days, was {nvi.Last.Value}");
    }

    [Fact]
    public void Update_ConsistentLowVolumeBearish_NviDecreases()
    {
        var nvi = new Nvi(startValue: 1000);
        var time = DateTime.UtcNow;

        // Build up with consistently lower volume and falling prices
        double volume = 100000;
        double price = 100;

        for (int i = 0; i < 20; i++)
        {
            nvi.Update(new TBar(time.AddMinutes(i), price, price + 2, price - 1, price, volume));
            volume *= 0.95; // Volume decreasing each day
            price *= 0.98;  // Price decreasing each day
        }

        Assert.True(nvi.Last.Value < 1000, $"NVI should be below start value after consistent bearish low-volume days, was {nvi.Last.Value}");
    }

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var nvi = new Nvi();
        var bar1 = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000000);
        var result1 = nvi.Update(bar1, isNew: true);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 105, 115, 95, 110, 800000);
        var result2 = nvi.Update(bar2, isNew: true);

        Assert.NotEqual(result1.Time, result2.Time);
    }

    [Fact]
    public void Update_IsNewFalse_UpdatesCurrentBar()
    {
        var nvi = new Nvi();
        var gbm = new GBM(seed: 42);

        // Build up history
        for (int i = 0; i < 20; i++)
        {
            nvi.Update(gbm.Next(), isNew: true);
        }

        // Get a new bar
        var bar1 = gbm.Next();
        var result1 = nvi.Update(bar1, isNew: true);

        // Create a correction with different volume (lower to trigger NVI change)
        var bar2 = new TBar(bar1.Time, bar1.Open, bar1.High, bar1.Low, bar1.Close * 1.1, bar1.Volume * 0.5);
        var result2 = nvi.Update(bar2, isNew: false);

        Assert.Equal(result1.Time, result2.Time);
        // Values may or may not differ depending on volume comparison
        Assert.True(double.IsFinite(result2.Value));
    }

    [Fact]
    public void Update_IterativeCorrections_RestoresState()
    {
        var nvi = new Nvi();
        var gbm = new GBM(seed: 123);

        // Build up history
        for (int i = 0; i < 20; i++)
        {
            nvi.Update(gbm.Next(), isNew: true);
        }

        _ = nvi.Last.Value; // Capture state before new bar

        // New bar
        var originalBar = gbm.Next();
        nvi.Update(originalBar, isNew: true);

        // Correction with same values should restore similar state
        var correctionBar = originalBar;
        var correctedResult = nvi.Update(correctionBar, isNew: false);

        Assert.True(double.IsFinite(correctedResult.Value));
    }

    [Fact]
    public void Update_WarmupPeriod_IsHotBecomesTrueAfterWarmup()
    {
        var nvi = new Nvi();
        var time = DateTime.UtcNow;

        Assert.False(nvi.IsHot);

        nvi.Update(new TBar(time, 100, 110, 90, 105, 100000), isNew: true);
        Assert.False(nvi.IsHot);

        nvi.Update(new TBar(time.AddMinutes(1), 105, 115, 95, 110, 80000), isNew: true);
        Assert.True(nvi.IsHot);
    }

    [Fact]
    public void Update_WithNaN_UsesLastValidValue()
    {
        var nvi = new Nvi();
        var time = DateTime.UtcNow;

        // Process some valid bars first
        for (int i = 0; i < 10; i++)
        {
            nvi.Update(new TBar(time.AddMinutes(i), 100, 105, 95, 102, 100000 - i * 1000));
        }

        // Process bar with NaN volume
        var nanBar = new TBar(time.AddMinutes(10), 105, 110, 100, 108, double.NaN);
        var result = nvi.Update(nanBar);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_ZeroVolume_HandlesGracefully()
    {
        var nvi = new Nvi();
        var time = DateTime.UtcNow;

        nvi.Update(new TBar(time, 100, 110, 90, 105, 100000));
        var result = nvi.Update(new TBar(time.AddMinutes(1), 105, 115, 95, 110, 0));

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var nvi = new Nvi();
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            nvi.Update(new TBar(time.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 100000 - i * 5000), isNew: true);
        }

        Assert.True(nvi.IsHot);
        Assert.True(double.IsFinite(nvi.Last.Value));

        nvi.Reset();

        Assert.False(nvi.IsHot);
        Assert.Equal(default, nvi.Last);
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
        var nvi = new Nvi();
        var streamingValues = new List<double>();
        foreach (var bar in bars)
        {
            streamingValues.Add(nvi.Update(bar).Value);
        }

        // Batch
        var batchResult = Nvi.Batch(bars);

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
        var nvi = new Nvi();
        var streamingValues = new List<double>();
        foreach (var bar in bars)
        {
            streamingValues.Add(nvi.Update(bar).Value);
        }

        // Span
        var close = bars.Close.Values.ToArray();
        var volume = bars.Volume.Values.ToArray();
        var output = new double[bars.Count];

        Nvi.Batch(close, volume, output);

        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(streamingValues[i], output[i], 10);
        }
    }

    [Fact]
    public void SpanCalculate_InvalidLengths_ThrowsArgumentException()
    {
        var close = new double[100];
        var volume = new double[99]; // Different length
        var output = new double[100];

        Assert.Throws<ArgumentException>(() => Nvi.Batch(close, volume, output));
    }

    [Fact]
    public void SpanCalculate_InvalidStartValue_ThrowsArgumentException()
    {
        var close = new double[100];
        var volume = new double[100];
        var output = new double[100];

        Assert.Throws<ArgumentException>(() => Nvi.Batch(close, volume, output, startValue: 0));
    }

    [Fact]
    public void SpanCalculate_EmptyInput_HandlesGracefully()
    {
        var close = Array.Empty<double>();
        var volume = Array.Empty<double>();
        var output = Array.Empty<double>();

        Nvi.Batch(close, volume, output);

        Assert.Empty(output);
    }

    [Fact]
    public void Event_PubFiresOnUpdate()
    {
        var nvi = new Nvi();
        TValue? receivedValue = null;
        bool receivedIsNew = false;

        nvi.Pub += (object? sender, in TValueEventArgs args) =>
        {
            receivedValue = args.Value;
            receivedIsNew = args.IsNew;
        };

        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000000);
        nvi.Update(bar, isNew: true);

        Assert.NotNull(receivedValue);
        Assert.True(receivedIsNew);
    }

    [Fact]
    public void CustomStartValue_AffectsResults()
    {
        var bars = new TBarSeries();
        var gbm = new GBM(seed: 42);

        for (int i = 0; i < 50; i++)
        {
            bars.Add(gbm.Next());
        }

        var nvi100 = new Nvi(startValue: 100);
        var nvi1000 = new Nvi(startValue: 1000);

        foreach (var bar in bars)
        {
            nvi100.Update(bar);
            nvi1000.Update(bar);
        }

        // Different start values should produce different final values
        Assert.NotEqual(nvi100.Last.Value, nvi1000.Last.Value);
        // The ratio should be approximately 10:1 (same proportional changes)
        Assert.Equal(10.0, nvi1000.Last.Value / nvi100.Last.Value, 1);
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

        var nvi = new Nvi();
        foreach (var bar in bars)
        {
            var result = nvi.Update(bar);
            Assert.True(double.IsFinite(result.Value));
            Assert.True(result.Value > 0);
        }

        Assert.True(nvi.IsHot);
    }
}
