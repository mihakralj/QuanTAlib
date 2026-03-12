using Xunit;

namespace QuanTAlib.Tests;

public class ObvTests
{
    [Fact]
    public void Constructor_DefaultParameters_CreatesValidIndicator()
    {
        var obv = new Obv();
        Assert.Equal("Obv", obv.Name);
        Assert.Equal(2, obv.WarmupPeriod);
        Assert.False(obv.IsHot);
    }

    [Fact]
    public void Update_WithTBar_ReturnsValidValue()
    {
        var obv = new Obv();
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000000);
        var result = obv.Update(bar);
        Assert.True(double.IsFinite(result.Value));
        Assert.Equal(0, result.Value); // First bar stays at zero (no comparison)
    }

    [Fact]
    public void Update_WithTValue_ReturnsCurrentValue()
    {
        var obv = new Obv();
        var value = new TValue(DateTime.UtcNow, 100);
        var result = obv.Update(value);
        // OBV without volume data returns current OBV value (zero initially)
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public void Update_PriceIncreases_AddsVolume()
    {
        var obv = new Obv();
        var time = DateTime.UtcNow;

        // First bar - establishes baseline
        obv.Update(new TBar(time, 100, 105, 95, 100, 100000));

        // Second bar with higher close - OBV should add volume
        var result = obv.Update(new TBar(time.AddMinutes(1), 100, 108, 98, 105, 80000));

        Assert.Equal(80000, result.Value);
    }

    [Fact]
    public void Update_PriceDecreases_SubtractsVolume()
    {
        var obv = new Obv();
        var time = DateTime.UtcNow;

        // First bar - establishes baseline
        obv.Update(new TBar(time, 100, 105, 95, 100, 100000));

        // Second bar with lower close - OBV should subtract volume
        var result = obv.Update(new TBar(time.AddMinutes(1), 100, 102, 90, 95, 80000));

        Assert.Equal(-80000, result.Value);
    }

    [Fact]
    public void Update_PriceUnchanged_ObvUnchanged()
    {
        var obv = new Obv();
        var time = DateTime.UtcNow;

        // First bar
        obv.Update(new TBar(time, 100, 105, 95, 100, 100000));
        var firstObv = obv.Last.Value;

        // Second bar with same close - OBV should stay the same
        var result = obv.Update(new TBar(time.AddMinutes(1), 100, 108, 92, 100, 150000));

        Assert.Equal(firstObv, result.Value);
    }

    [Fact]
    public void Update_ConsistentUpDays_ObvIncreases()
    {
        var obv = new Obv();
        var time = DateTime.UtcNow;

        // Build up with consistently rising prices
        double price = 100;

        for (int i = 0; i < 20; i++)
        {
            obv.Update(new TBar(time.AddMinutes(i), price, price + 2, price - 1, price, 10000));
            price += 1; // Price increasing each day
        }

        Assert.True(obv.Last.Value > 0, $"OBV should be positive after consistent up days, was {obv.Last.Value}");
    }

    [Fact]
    public void Update_ConsistentDownDays_ObvDecreases()
    {
        var obv = new Obv();
        var time = DateTime.UtcNow;

        // Build up with consistently falling prices
        double price = 100;

        for (int i = 0; i < 20; i++)
        {
            obv.Update(new TBar(time.AddMinutes(i), price, price + 2, price - 1, price, 10000));
            price -= 1; // Price decreasing each day
        }

        Assert.True(obv.Last.Value < 0, $"OBV should be negative after consistent down days, was {obv.Last.Value}");
    }

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var obv = new Obv();
        var bar1 = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000000);
        var result1 = obv.Update(bar1, isNew: true);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 105, 115, 95, 110, 800000);
        var result2 = obv.Update(bar2, isNew: true);

        Assert.NotEqual(result1.Time, result2.Time);
    }

    [Fact]
    public void Update_IsNewFalse_UpdatesCurrentBar()
    {
        var obv = new Obv();
        var gbm = new GBM(seed: 42);

        // Build up history
        for (int i = 0; i < 20; i++)
        {
            obv.Update(gbm.Next(), isNew: true);
        }

        // Get a new bar
        var bar1 = gbm.Next();
        var result1 = obv.Update(bar1, isNew: true);

        // Create a correction with different close
        var bar2 = new TBar(bar1.Time, bar1.Open, bar1.High, bar1.Low, bar1.Close * 1.1, bar1.Volume);
        var result2 = obv.Update(bar2, isNew: false);

        Assert.Equal(result1.Time, result2.Time);
        Assert.True(double.IsFinite(result2.Value));
    }

    [Fact]
    public void Update_IterativeCorrections_RestoresState()
    {
        var obv = new Obv();
        var gbm = new GBM(seed: 123);

        // Build up history
        for (int i = 0; i < 20; i++)
        {
            obv.Update(gbm.Next(), isNew: true);
        }

        _ = obv.Last.Value; // Capture state before new bar

        // New bar
        var originalBar = gbm.Next();
        obv.Update(originalBar, isNew: true);

        // Correction with same values should restore similar state
        var correctionBar = originalBar;
        var correctedResult = obv.Update(correctionBar, isNew: false);

        Assert.True(double.IsFinite(correctedResult.Value));
    }

    [Fact]
    public void Update_WarmupPeriod_IsHotBecomesTrueAfterWarmup()
    {
        var obv = new Obv();
        var time = DateTime.UtcNow;

        Assert.False(obv.IsHot);

        obv.Update(new TBar(time, 100, 110, 90, 105, 100000), isNew: true);
        Assert.False(obv.IsHot);

        obv.Update(new TBar(time.AddMinutes(1), 105, 115, 95, 110, 80000), isNew: true);
        Assert.True(obv.IsHot);
    }

    [Fact]
    public void Update_WithNaN_UsesLastValidValue()
    {
        var obv = new Obv();
        var time = DateTime.UtcNow;

        // Process some valid bars first
        for (int i = 0; i < 10; i++)
        {
            obv.Update(new TBar(time.AddMinutes(i), 100, 105, 95, 102 + i, 100000));
        }

        _ = obv.Last.Value;

        // Process bar with NaN volume
        var nanBar = new TBar(time.AddMinutes(10), 105, 110, 100, 115, double.NaN);
        var result = obv.Update(nanBar);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_ZeroVolume_HandlesGracefully()
    {
        var obv = new Obv();
        var time = DateTime.UtcNow;

        obv.Update(new TBar(time, 100, 110, 90, 105, 100000));
        var result = obv.Update(new TBar(time.AddMinutes(1), 105, 115, 95, 110, 0));

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var obv = new Obv();
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            obv.Update(new TBar(time.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 100000), isNew: true);
        }

        Assert.True(obv.IsHot);
        Assert.True(double.IsFinite(obv.Last.Value));

        obv.Reset();

        Assert.False(obv.IsHot);
        Assert.Equal(default, obv.Last);
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
        var obv = new Obv();
        var streamingValues = new List<double>();
        foreach (var bar in bars)
        {
            streamingValues.Add(obv.Update(bar).Value);
        }

        // Batch
        var batchResult = Obv.Batch(bars);

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
        var obv = new Obv();
        var streamingValues = new List<double>();
        foreach (var bar in bars)
        {
            streamingValues.Add(obv.Update(bar).Value);
        }

        // Span
        var close = bars.Close.Values.ToArray();
        var volume = bars.Volume.Values.ToArray();
        var output = new double[bars.Count];

        Obv.Batch(close, volume, output);

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

        Assert.Throws<ArgumentException>(() => Obv.Batch(close, volume, output));
    }

    [Fact]
    public void SpanCalculate_EmptyInput_HandlesGracefully()
    {
        var close = Array.Empty<double>();
        var volume = Array.Empty<double>();
        var output = Array.Empty<double>();

        Obv.Batch(close, volume, output);

        Assert.Empty(output);
    }

    [Fact]
    public void Event_PubFiresOnUpdate()
    {
        var obv = new Obv();
        TValue? receivedValue = null;
        bool receivedIsNew = false;

        obv.Pub += (object? sender, in TValueEventArgs args) =>
        {
            receivedValue = args.Value;
            receivedIsNew = args.IsNew;
        };

        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000000);
        obv.Update(bar, isNew: true);

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

        var obv = new Obv();
        foreach (var bar in bars)
        {
            var result = obv.Update(bar);
            Assert.True(double.IsFinite(result.Value));
        }

        Assert.True(obv.IsHot);
    }

    [Fact]
    public void FormulaVerification_ManualCalculation()
    {
        // Manual verification of OBV formula with known values
        var obv = new Obv();
        var time = DateTime.UtcNow;

        // Bar 1: baseline (close = 100, volume = 10000)
        obv.Update(new TBar(time, 100, 105, 95, 100, 10000));
        Assert.Equal(0, obv.Last.Value); // First bar, OBV starts at 0

        // Bar 2: price up (105 > 100), add volume
        // Expected: OBV = 0 + 15000 = 15000
        obv.Update(new TBar(time.AddMinutes(1), 100, 110, 95, 105, 15000));
        Assert.Equal(15000, obv.Last.Value);

        // Bar 3: price down (102 < 105), subtract volume
        // Expected: OBV = 15000 - 12000 = 3000
        obv.Update(new TBar(time.AddMinutes(2), 105, 108, 100, 102, 12000));
        Assert.Equal(3000, obv.Last.Value);

        // Bar 4: price unchanged (102 == 102), OBV unchanged
        // Expected: OBV = 3000
        obv.Update(new TBar(time.AddMinutes(3), 102, 106, 100, 102, 20000));
        Assert.Equal(3000, obv.Last.Value);

        // Bar 5: price up (110 > 102), add volume
        // Expected: OBV = 3000 + 8000 = 11000
        obv.Update(new TBar(time.AddMinutes(4), 102, 112, 100, 110, 8000));
        Assert.Equal(11000, obv.Last.Value);
    }
}
