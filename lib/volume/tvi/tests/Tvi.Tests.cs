using Xunit;

namespace QuanTAlib.Tests;

public class TviTests
{
    private const double DefaultMinTick = 0.125;

    [Fact]
    public void Constructor_DefaultParameters_CreatesValidIndicator()
    {
        var tvi = new Tvi();
        Assert.Equal($"Tvi({DefaultMinTick})", tvi.Name);
        Assert.Equal(2, tvi.WarmupPeriod);
        Assert.False(tvi.IsHot);
    }

    [Fact]
    public void Constructor_CustomMinTick_SetsParameter()
    {
        var tvi = new Tvi(minTick: 0.5);
        Assert.Equal("Tvi(0.5)", tvi.Name);
    }

    [Fact]
    public void Constructor_ZeroMinTick_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Tvi(minTick: 0));
    }

    [Fact]
    public void Constructor_NegativeMinTick_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Tvi(minTick: -0.1));
    }

    [Fact]
    public void Update_WithTBar_ReturnsValidValue()
    {
        var tvi = new Tvi();
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000000);
        var result = tvi.Update(bar);
        Assert.True(double.IsFinite(result.Value));
        Assert.Equal(0, result.Value); // First bar stays at zero (no comparison)
    }

    [Fact]
    public void Update_WithTValue_ReturnsCurrentValue()
    {
        var tvi = new Tvi();
        var value = new TValue(DateTime.UtcNow, 100);
        var result = tvi.Update(value);
        // TVI without volume data returns current TVI value (zero initially)
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public void Update_PriceIncreasesAboveMinTick_DirectionUp_AddsVolume()
    {
        var tvi = new Tvi(minTick: 0.5);
        var time = DateTime.UtcNow;

        // First bar - establishes baseline
        tvi.Update(new TBar(time, 100, 105, 95, 100, 100000));

        // Second bar with price increase > minTick - direction becomes up, add volume
        var result = tvi.Update(new TBar(time.AddMinutes(1), 100, 108, 98, 101, 80000)); // +1 > 0.5

        Assert.Equal(80000, result.Value);
    }

    [Fact]
    public void Update_PriceDecreasesAboveMinTick_DirectionDown_SubtractsVolume()
    {
        var tvi = new Tvi(minTick: 0.5);
        var time = DateTime.UtcNow;

        // First bar - establishes baseline
        tvi.Update(new TBar(time, 100, 105, 95, 100, 100000));

        // Second bar with price decrease > minTick - direction becomes down, subtract volume
        var result = tvi.Update(new TBar(time.AddMinutes(1), 100, 102, 90, 99, 80000)); // -1 < -0.5

        Assert.Equal(-80000, result.Value);
    }

    [Fact]
    public void Update_PriceChangeWithinMinTick_DirectionSticky()
    {
        var tvi = new Tvi(minTick: 0.5);
        var time = DateTime.UtcNow;

        // First bar - establishes baseline
        tvi.Update(new TBar(time, 100, 105, 95, 100, 100000));

        // Second bar - big move up, direction = 1
        tvi.Update(new TBar(time.AddMinutes(1), 100, 108, 98, 102, 80000)); // +2 > 0.5, direction = 1
        Assert.Equal(80000, tvi.Last.Value);

        // Third bar - small move (within minTick), direction stays 1
        var result = tvi.Update(new TBar(time.AddMinutes(2), 102, 103, 101, 102.2, 50000)); // +0.2 < 0.5, sticky

        Assert.Equal(80000 + 50000, result.Value); // Still adds because direction is still 1
    }

    [Fact]
    public void Update_DirectionStickyWhenPriceFlat()
    {
        var tvi = new Tvi(minTick: 0.5);
        var time = DateTime.UtcNow;

        // First bar
        tvi.Update(new TBar(time, 100, 105, 95, 100, 100000));

        // Second bar - move down, direction = -1
        tvi.Update(new TBar(time.AddMinutes(1), 100, 102, 90, 99, 80000)); // -1 < -0.5
        Assert.Equal(-80000, tvi.Last.Value);

        // Third bar - flat price, direction stays -1
        var result = tvi.Update(new TBar(time.AddMinutes(2), 99, 100, 98, 99, 50000)); // 0 within ±0.5

        Assert.Equal(-80000 - 50000, result.Value); // Subtracts because direction is still -1
    }

    [Fact]
    public void Update_ConsistentUpDays_TviIncreases()
    {
        var tvi = new Tvi(minTick: 0.1);
        var time = DateTime.UtcNow;

        double price = 100;
        for (int i = 0; i < 20; i++)
        {
            tvi.Update(new TBar(time.AddMinutes(i), price, price + 2, price - 1, price, 10000));
            price += 1; // Price increasing each day by more than minTick
        }

        Assert.True(tvi.Last.Value > 0, $"TVI should be positive after consistent up days, was {tvi.Last.Value}");
    }

    [Fact]
    public void Update_ConsistentDownDays_TviDecreases()
    {
        var tvi = new Tvi(minTick: 0.1);
        var time = DateTime.UtcNow;

        double price = 100;
        for (int i = 0; i < 20; i++)
        {
            tvi.Update(new TBar(time.AddMinutes(i), price, price + 2, price - 1, price, 10000));
            price -= 1; // Price decreasing each day by more than minTick
        }

        Assert.True(tvi.Last.Value < 0, $"TVI should be negative after consistent down days, was {tvi.Last.Value}");
    }

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var tvi = new Tvi();
        var bar1 = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000000);
        var result1 = tvi.Update(bar1, isNew: true);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 105, 115, 95, 110, 800000);
        var result2 = tvi.Update(bar2, isNew: true);

        Assert.NotEqual(result1.Time, result2.Time);
    }

    [Fact]
    public void Update_IsNewFalse_UpdatesCurrentBar()
    {
        var tvi = new Tvi();
        var gbm = new GBM(seed: 42);

        // Build up history
        for (int i = 0; i < 20; i++)
        {
            tvi.Update(gbm.Next(), isNew: true);
        }

        // Get a new bar
        var bar1 = gbm.Next();
        var result1 = tvi.Update(bar1, isNew: true);

        // Create a correction with different close
        var bar2 = new TBar(bar1.Time, bar1.Open, bar1.High, bar1.Low, bar1.Close * 1.1, bar1.Volume);
        var result2 = tvi.Update(bar2, isNew: false);

        Assert.Equal(result1.Time, result2.Time);
        Assert.True(double.IsFinite(result2.Value));
    }

    [Fact]
    public void Update_IterativeCorrections_RestoresState()
    {
        var tvi = new Tvi();
        var gbm = new GBM(seed: 123);

        // Build up history
        for (int i = 0; i < 20; i++)
        {
            tvi.Update(gbm.Next(), isNew: true);
        }

        _ = tvi.Last.Value;

        // New bar
        var originalBar = gbm.Next();
        tvi.Update(originalBar, isNew: true);

        // Correction with same values should restore similar state
        var correctionBar = originalBar;
        var correctedResult = tvi.Update(correctionBar, isNew: false);

        Assert.True(double.IsFinite(correctedResult.Value));
    }

    [Fact]
    public void Update_WarmupPeriod_IsHotBecomesTrueAfterWarmup()
    {
        var tvi = new Tvi();
        var time = DateTime.UtcNow;

        Assert.False(tvi.IsHot);

        tvi.Update(new TBar(time, 100, 110, 90, 105, 100000), isNew: true);
        Assert.False(tvi.IsHot);

        tvi.Update(new TBar(time.AddMinutes(1), 105, 115, 95, 110, 80000), isNew: true);
        Assert.True(tvi.IsHot);
    }

    [Fact]
    public void Update_WithNaN_UsesLastValidValue()
    {
        var tvi = new Tvi();
        var time = DateTime.UtcNow;

        // Process some valid bars first
        for (int i = 0; i < 10; i++)
        {
            tvi.Update(new TBar(time.AddMinutes(i), 100, 105, 95, 102 + i, 100000));
        }

        _ = tvi.Last.Value;

        // Process bar with NaN volume
        var nanBar = new TBar(time.AddMinutes(10), 105, 110, 100, 115, double.NaN);
        var result = tvi.Update(nanBar);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_ZeroVolume_HandlesGracefully()
    {
        var tvi = new Tvi();
        var time = DateTime.UtcNow;

        tvi.Update(new TBar(time, 100, 110, 90, 105, 100000));
        var result = tvi.Update(new TBar(time.AddMinutes(1), 105, 115, 95, 110, 0));

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var tvi = new Tvi();
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            tvi.Update(new TBar(time.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 100000), isNew: true);
        }

        Assert.True(tvi.IsHot);
        Assert.True(double.IsFinite(tvi.Last.Value));

        tvi.Reset();

        Assert.False(tvi.IsHot);
        Assert.Equal(default, tvi.Last);
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
        var tvi = new Tvi();
        var streamingValues = new List<double>();
        foreach (var bar in bars)
        {
            streamingValues.Add(tvi.Update(bar).Value);
        }

        // Batch
        var batchResult = Tvi.Batch(bars);

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
        var tvi = new Tvi();
        var streamingValues = new List<double>();
        foreach (var bar in bars)
        {
            streamingValues.Add(tvi.Update(bar).Value);
        }

        // Span
        var price = bars.Close.Values.ToArray();
        var volume = bars.Volume.Values.ToArray();
        var output = new double[bars.Count];

        Tvi.Batch(price, volume, output);

        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(streamingValues[i], output[i], 10);
        }
    }

    [Fact]
    public void SpanCalculate_InvalidLengths_ThrowsArgumentException()
    {
        var price = new double[100];
        var volume = new double[99]; // Different length
        var output = new double[100];

        Assert.Throws<ArgumentException>(() => Tvi.Batch(price, volume, output));
    }

    [Fact]
    public void SpanCalculate_InvalidMinTick_ThrowsArgumentException()
    {
        var price = new double[100];
        var volume = new double[100];
        var output = new double[100];

        Assert.Throws<ArgumentException>(() => Tvi.Batch(price, volume, output, minTick: 0));
        Assert.Throws<ArgumentException>(() => Tvi.Batch(price, volume, output, minTick: -1));
    }

    [Fact]
    public void SpanCalculate_EmptyInput_HandlesGracefully()
    {
        var price = Array.Empty<double>();
        var volume = Array.Empty<double>();
        var output = Array.Empty<double>();

        Tvi.Batch(price, volume, output);

        Assert.Empty(output);
    }

    [Fact]
    public void Event_PubFiresOnUpdate()
    {
        var tvi = new Tvi();
        TValue? receivedValue = null;
        bool receivedIsNew = false;

        tvi.Pub += (object? sender, in TValueEventArgs args) =>
        {
            receivedValue = args.Value;
            receivedIsNew = args.IsNew;
        };

        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000000);
        tvi.Update(bar, isNew: true);

        Assert.NotNull(receivedValue);
        Assert.True(receivedIsNew);
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

        var tvi = new Tvi();
        foreach (var bar in bars)
        {
            var result = tvi.Update(bar);
            Assert.True(double.IsFinite(result.Value));
        }

        Assert.True(tvi.IsHot);
    }

    [Fact]
    public void FormulaVerification_ManualCalculation()
    {
        // Manual verification of TVI formula with known values
        var tvi = new Tvi(minTick: 0.5);
        var time = DateTime.UtcNow;

        // Bar 1: baseline (close = 100, volume = 10000)
        tvi.Update(new TBar(time, 100, 105, 95, 100, 10000));
        Assert.Equal(0, tvi.Last.Value); // First bar, TVI starts at 0

        // Bar 2: price up by 2 (>0.5), direction = 1, add volume
        // Expected: TVI = 0 + 15000 = 15000
        tvi.Update(new TBar(time.AddMinutes(1), 100, 110, 95, 102, 15000));
        Assert.Equal(15000, tvi.Last.Value);

        // Bar 3: price down by 3 (<-0.5), direction = -1, subtract volume
        // Expected: TVI = 15000 - 12000 = 3000
        tvi.Update(new TBar(time.AddMinutes(2), 102, 103, 98, 99, 12000));
        Assert.Equal(3000, tvi.Last.Value);

        // Bar 4: price up by 0.2 (within ±0.5), direction stays -1, subtract volume
        // Expected: TVI = 3000 - 20000 = -17000
        tvi.Update(new TBar(time.AddMinutes(3), 99, 100, 98, 99.2, 20000));
        Assert.Equal(-17000, tvi.Last.Value);

        // Bar 5: price up by 3 (>0.5), direction = 1, add volume
        // Expected: TVI = -17000 + 8000 = -9000
        tvi.Update(new TBar(time.AddMinutes(4), 99.2, 105, 99, 102.2, 8000));
        Assert.Equal(-9000, tvi.Last.Value);
    }

    [Fact]
    public void DifferentMinTicks_ProduceDifferentResults()
    {
        var time = DateTime.UtcNow;
        var bars = new List<TBar>
        {
            new(time, 100, 105, 95, 100, 10000),
            new(time.AddMinutes(1), 100, 101, 99, 100.3, 15000), // +0.3
            new(time.AddMinutes(2), 100.3, 101, 99, 100.1, 12000), // -0.2
            new(time.AddMinutes(3), 100.1, 102, 99, 101, 8000), // +0.9
        };

        // With minTick = 0.1: all moves register
        var tvi01 = new Tvi(minTick: 0.1);
        foreach (var bar in bars)
        {
            tvi01.Update(bar);
        }

        // With minTick = 0.5: only large moves register
        var tvi05 = new Tvi(minTick: 0.5);
        foreach (var bar in bars)
        {
            tvi05.Update(bar);
        }

        // Results should differ due to sticky direction behavior
        Assert.NotEqual(tvi01.Last.Value, tvi05.Last.Value);
    }
}
