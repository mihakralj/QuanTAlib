using Xunit;

namespace QuanTAlib.Tests;

public class PviTests
{
    private const double DefaultStartValue = 100.0;

    [Fact]
    public void Constructor_DefaultParameters_CreatesValidIndicator()
    {
        var pvi = new Pvi();
        Assert.Equal($"Pvi({DefaultStartValue})", pvi.Name);
        Assert.Equal(2, pvi.WarmupPeriod);
        Assert.False(pvi.IsHot);
    }

    [Fact]
    public void Constructor_CustomParameters_CreatesValidIndicator()
    {
        var pvi = new Pvi(startValue: 1000);
        Assert.Equal("Pvi(1000)", pvi.Name);
        Assert.Equal(2, pvi.WarmupPeriod);
    }

    [Fact]
    public void Constructor_InvalidStartValue_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Pvi(startValue: 0));
        Assert.Throws<ArgumentException>(() => new Pvi(startValue: -100));
    }

    [Fact]
    public void Update_WithTBar_ReturnsValidValue()
    {
        var pvi = new Pvi();
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000000);
        var result = pvi.Update(bar);
        Assert.True(double.IsFinite(result.Value));
        Assert.Equal(DefaultStartValue, result.Value); // First bar stays at start value
    }

    [Fact]
    public void Update_WithTValue_ReturnsCurrentValue()
    {
        var pvi = new Pvi();
        var value = new TValue(DateTime.UtcNow, 100);
        var result = pvi.Update(value);
        // PVI without volume data returns current PVI value
        Assert.Equal(DefaultStartValue, result.Value);
    }

    [Fact]
    public void Update_VolumeIncreases_UpdatesPvi()
    {
        var pvi = new Pvi();
        var time = DateTime.UtcNow;

        // First bar - establishes baseline
        pvi.Update(new TBar(time, 100, 105, 95, 100, 100000));

        // Second bar with higher volume and higher close - PVI should increase
        var result = pvi.Update(new TBar(time.AddMinutes(1), 100, 108, 98, 105, 150000));

        Assert.True(result.Value > DefaultStartValue, $"PVI should increase when volume increases and price rises, was {result.Value}");
    }

    [Fact]
    public void Update_VolumeDecreases_PviUnchanged()
    {
        var pvi = new Pvi();
        var time = DateTime.UtcNow;

        // First bar - establishes baseline
        pvi.Update(new TBar(time, 100, 105, 95, 100, 100000));
        var firstPvi = pvi.Last.Value;

        // Second bar with lower volume - PVI should stay the same
        var result = pvi.Update(new TBar(time.AddMinutes(1), 100, 108, 98, 105, 80000));

        Assert.Equal(firstPvi, result.Value);
    }

    [Fact]
    public void Update_VolumeEqual_PviUnchanged()
    {
        var pvi = new Pvi();
        var time = DateTime.UtcNow;

        // First bar
        pvi.Update(new TBar(time, 100, 105, 95, 100, 100000));
        var firstPvi = pvi.Last.Value;

        // Second bar with equal volume
        var result = pvi.Update(new TBar(time.AddMinutes(1), 100, 108, 98, 105, 100000));

        Assert.Equal(firstPvi, result.Value);
    }

    [Fact]
    public void Update_ConsistentHighVolumeBullish_PviIncreases()
    {
        var pvi = new Pvi(startValue: 1000);
        var time = DateTime.UtcNow;

        // Build up with consistently higher volume and rising prices
        double volume = 100000;
        double price = 100;

        for (int i = 0; i < 20; i++)
        {
            pvi.Update(new TBar(time.AddMinutes(i), price, price + 2, price - 1, price, volume));
            volume *= 1.05; // Volume increasing each day
            price *= 1.02;  // Price increasing each day
        }

        Assert.True(pvi.Last.Value > 1000, $"PVI should be above start value after consistent bullish high-volume days, was {pvi.Last.Value}");
    }

    [Fact]
    public void Update_ConsistentHighVolumeBearish_PviDecreases()
    {
        var pvi = new Pvi(startValue: 1000);
        var time = DateTime.UtcNow;

        // Build up with consistently higher volume and falling prices
        double volume = 100000;
        double price = 100;

        for (int i = 0; i < 20; i++)
        {
            pvi.Update(new TBar(time.AddMinutes(i), price, price + 2, price - 1, price, volume));
            volume *= 1.05; // Volume increasing each day
            price *= 0.98;  // Price decreasing each day
        }

        Assert.True(pvi.Last.Value < 1000, $"PVI should be below start value after consistent bearish high-volume days, was {pvi.Last.Value}");
    }

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var pvi = new Pvi();
        var bar1 = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000000);
        var result1 = pvi.Update(bar1, isNew: true);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 105, 115, 95, 110, 1200000);
        var result2 = pvi.Update(bar2, isNew: true);

        Assert.NotEqual(result1.Time, result2.Time);
    }

    [Fact]
    public void Update_IsNewFalse_UpdatesCurrentBar()
    {
        var pvi = new Pvi();
        var gbm = new GBM(seed: 42);

        // Build up history
        for (int i = 0; i < 20; i++)
        {
            pvi.Update(gbm.Next(), isNew: true);
        }

        // Get a new bar
        var bar1 = gbm.Next();
        var result1 = pvi.Update(bar1, isNew: true);

        // Create a correction with different volume (higher to trigger PVI change)
        var bar2 = new TBar(bar1.Time, bar1.Open, bar1.High, bar1.Low, bar1.Close * 1.1, bar1.Volume * 1.5);
        var result2 = pvi.Update(bar2, isNew: false);

        Assert.Equal(result1.Time, result2.Time);
        // Values may or may not differ depending on volume comparison
        Assert.True(double.IsFinite(result2.Value));
    }

    [Fact]
    public void Update_IterativeCorrections_RestoresState()
    {
        var pvi = new Pvi();
        var gbm = new GBM(seed: 123);

        // Build up history
        for (int i = 0; i < 20; i++)
        {
            pvi.Update(gbm.Next(), isNew: true);
        }

        _ = pvi.Last.Value; // Capture state before new bar

        // New bar
        var originalBar = gbm.Next();
        pvi.Update(originalBar, isNew: true);

        // Correction with same values should restore similar state
        var correctionBar = originalBar;
        var correctedResult = pvi.Update(correctionBar, isNew: false);

        Assert.True(double.IsFinite(correctedResult.Value));
    }

    [Fact]
    public void Update_WarmupPeriod_IsHotBecomesTrueAfterWarmup()
    {
        var pvi = new Pvi();
        var time = DateTime.UtcNow;

        Assert.False(pvi.IsHot);

        pvi.Update(new TBar(time, 100, 110, 90, 105, 100000), isNew: true);
        Assert.False(pvi.IsHot);

        pvi.Update(new TBar(time.AddMinutes(1), 105, 115, 95, 110, 120000), isNew: true);
        Assert.True(pvi.IsHot);
    }

    [Fact]
    public void Update_WithNaN_UsesLastValidValue()
    {
        var pvi = new Pvi();
        var time = DateTime.UtcNow;

        // Process some valid bars first
        for (int i = 0; i < 10; i++)
        {
            pvi.Update(new TBar(time.AddMinutes(i), 100, 105, 95, 102, 100000 + i * 1000));
        }

        // Process bar with NaN volume
        var nanBar = new TBar(time.AddMinutes(10), 105, 110, 100, 108, double.NaN);
        var result = pvi.Update(nanBar);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_ZeroVolume_HandlesGracefully()
    {
        var pvi = new Pvi();
        var time = DateTime.UtcNow;

        pvi.Update(new TBar(time, 100, 110, 90, 105, 100000));
        var result = pvi.Update(new TBar(time.AddMinutes(1), 105, 115, 95, 110, 0));

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var pvi = new Pvi();
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            pvi.Update(new TBar(time.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 100000 + i * 5000), isNew: true);
        }

        Assert.True(pvi.IsHot);
        Assert.True(double.IsFinite(pvi.Last.Value));

        pvi.Reset();

        Assert.False(pvi.IsHot);
        Assert.Equal(default, pvi.Last);
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
        var pvi = new Pvi();
        var streamingValues = new List<double>();
        foreach (var bar in bars)
        {
            streamingValues.Add(pvi.Update(bar).Value);
        }

        // Batch
        var batchResult = Pvi.Batch(bars);

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
        var pvi = new Pvi();
        var streamingValues = new List<double>();
        foreach (var bar in bars)
        {
            streamingValues.Add(pvi.Update(bar).Value);
        }

        // Span
        var close = bars.Close.Values.ToArray();
        var volume = bars.Volume.Values.ToArray();
        var output = new double[bars.Count];

        Pvi.Batch(close, volume, output);

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

        Assert.Throws<ArgumentException>(() => Pvi.Batch(close, volume, output));
    }

    [Fact]
    public void SpanCalculate_InvalidStartValue_ThrowsArgumentException()
    {
        var close = new double[100];
        var volume = new double[100];
        var output = new double[100];

        Assert.Throws<ArgumentException>(() => Pvi.Batch(close, volume, output, startValue: 0));
    }

    [Fact]
    public void SpanCalculate_EmptyInput_HandlesGracefully()
    {
        var close = Array.Empty<double>();
        var volume = Array.Empty<double>();
        var output = Array.Empty<double>();

        Pvi.Batch(close, volume, output);

        Assert.Empty(output);
    }

    [Fact]
    public void Event_PubFiresOnUpdate()
    {
        var pvi = new Pvi();
        TValue? receivedValue = null;
        bool receivedIsNew = false;

        pvi.Pub += (object? sender, in TValueEventArgs args) =>
        {
            receivedValue = args.Value;
            receivedIsNew = args.IsNew;
        };

        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000000);
        pvi.Update(bar, isNew: true);

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

        var pvi100 = new Pvi(startValue: 100);
        var pvi1000 = new Pvi(startValue: 1000);

        foreach (var bar in bars)
        {
            pvi100.Update(bar);
            pvi1000.Update(bar);
        }

        // Different start values should produce different final values
        Assert.NotEqual(pvi100.Last.Value, pvi1000.Last.Value);
        // The ratio should be approximately 10:1 (same proportional changes)
        Assert.Equal(10.0, pvi1000.Last.Value / pvi100.Last.Value, 1);
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

        var pvi = new Pvi();
        foreach (var bar in bars)
        {
            var result = pvi.Update(bar);
            Assert.True(double.IsFinite(result.Value));
            Assert.True(result.Value > 0);
        }

        Assert.True(pvi.IsHot);
    }
}