using Xunit;

namespace QuanTAlib.Tests;

public class PvrTests
{
    [Fact]
    public void Constructor_CreatesValidIndicator()
    {
        var pvr = new Pvr();
        Assert.Equal("Pvr", pvr.Name);
        Assert.Equal(1, pvr.WarmupPeriod);
        Assert.False(pvr.IsHot);
    }

    [Fact]
    public void Update_WithTBar_ReturnsValidValue()
    {
        var pvr = new Pvr();
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000000);
        var result = pvr.Update(bar);
        Assert.True(result.Value >= 0 && result.Value <= 4);
    }

    [Fact]
    public void Update_FirstBar_ReturnsZero()
    {
        var pvr = new Pvr();
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000000);
        var result = pvr.Update(bar);
        Assert.Equal(0.0, result.Value);
        Assert.False(pvr.IsHot);
    }

    [Fact]
    public void Update_PriceUpVolumeUp_ReturnsOne()
    {
        var pvr = new Pvr();
        var time = DateTime.UtcNow;

        pvr.Update(new TBar(time, 100, 105, 95, 100, 1000));
        var result = pvr.Update(new TBar(time.AddMinutes(1), 102, 107, 97, 102, 1500));

        Assert.Equal(1.0, result.Value);
    }

    [Fact]
    public void Update_PriceUpVolumeDown_ReturnsTwo()
    {
        var pvr = new Pvr();
        var time = DateTime.UtcNow;

        pvr.Update(new TBar(time, 100, 105, 95, 100, 1500));
        var result = pvr.Update(new TBar(time.AddMinutes(1), 102, 107, 97, 102, 1000));

        Assert.Equal(2.0, result.Value);
    }

    [Fact]
    public void Update_PriceDownVolumeDown_ReturnsThree()
    {
        var pvr = new Pvr();
        var time = DateTime.UtcNow;

        pvr.Update(new TBar(time, 100, 105, 95, 100, 1500));
        var result = pvr.Update(new TBar(time.AddMinutes(1), 98, 103, 93, 98, 1000));

        Assert.Equal(3.0, result.Value);
    }

    [Fact]
    public void Update_PriceDownVolumeUp_ReturnsFour()
    {
        var pvr = new Pvr();
        var time = DateTime.UtcNow;

        pvr.Update(new TBar(time, 100, 105, 95, 100, 1000));
        var result = pvr.Update(new TBar(time.AddMinutes(1), 98, 103, 93, 98, 1500));

        Assert.Equal(4.0, result.Value);
    }

    [Fact]
    public void Update_PriceUnchanged_ReturnsZero()
    {
        var pvr = new Pvr();
        var time = DateTime.UtcNow;

        pvr.Update(new TBar(time, 100, 105, 95, 100, 1000));
        var result = pvr.Update(new TBar(time.AddMinutes(1), 100, 108, 92, 100, 1500));

        Assert.Equal(0.0, result.Value);
    }

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var pvr = new Pvr();
        var bar1 = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000000);
        var result1 = pvr.Update(bar1, isNew: true);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 105, 115, 95, 110, 1100000);
        var result2 = pvr.Update(bar2, isNew: true);

        Assert.NotEqual(result1.Time, result2.Time);
    }

    [Fact]
    public void Update_IsNewFalse_UpdatesCurrentBar()
    {
        var pvr = new Pvr();
        var time = DateTime.UtcNow;

        // First bar
        pvr.Update(new TBar(time, 100, 105, 95, 100, 1000), isNew: true);

        // Second bar - price up, volume up -> 1
        var result1 = pvr.Update(new TBar(time.AddMinutes(1), 102, 107, 97, 102, 1500), isNew: true);
        Assert.Equal(1.0, result1.Value);

        // Correction - price up, volume down -> 2
        var result2 = pvr.Update(new TBar(time.AddMinutes(1), 102, 107, 97, 102, 800), isNew: false);
        Assert.Equal(2.0, result2.Value);
    }

    [Fact]
    public void Update_IterativeCorrections_RestoresState()
    {
        var pvr = new Pvr();
        var time = DateTime.UtcNow;

        // Build up state
        for (int i = 0; i < 10; i++)
        {
            pvr.Update(new TBar(time.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 100000 + (i * 10000)), isNew: true);
        }

        // New bar
        var originalBar = new TBar(time.AddMinutes(10), 120, 130, 110, 125, 250000);
        var originalResult = pvr.Update(originalBar, isNew: true);

        // Correction
        var correctionBar = new TBar(time.AddMinutes(10), 110, 120, 100, 105, 50000);
        var correctedResult = pvr.Update(correctionBar, isNew: false);

        Assert.NotEqual(originalResult.Value, correctedResult.Value);
    }

    [Fact]
    public void Update_WarmupPeriod_IsHotBecomesTrueAfterFirstBar()
    {
        var pvr = new Pvr();
        var time = DateTime.UtcNow;

        Assert.False(pvr.IsHot);

        pvr.Update(new TBar(time, 100, 105, 95, 100, 1000), isNew: true);
        Assert.False(pvr.IsHot);

        pvr.Update(new TBar(time.AddMinutes(1), 102, 107, 97, 102, 1500), isNew: true);
        Assert.True(pvr.IsHot);
    }

    [Fact]
    public void Update_WithNaN_UsesLastValidValue()
    {
        var pvr = new Pvr();
        var time = DateTime.UtcNow;

        pvr.Update(new TBar(time, 100, 105, 95, 100, 1000));
        pvr.Update(new TBar(time.AddMinutes(1), 102, 107, 97, 102, 1500));

        // NaN values
        var result = pvr.Update(new TBar(time.AddMinutes(2), double.NaN, double.NaN, double.NaN, double.NaN, double.NaN));

        Assert.True(result.Value >= 0 && result.Value <= 4);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var pvr = new Pvr();
        var time = DateTime.UtcNow;

        for (int i = 0; i < 5; i++)
        {
            pvr.Update(new TBar(time.AddMinutes(i), 100 + i, 110 + i, 90 + i, 105 + i, 100000), isNew: true);
        }

        Assert.True(pvr.IsHot);

        pvr.Reset();

        Assert.False(pvr.IsHot);
        Assert.Equal(default, pvr.Last);
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
        var pvr = new Pvr();
        var streamingValues = new List<double>();
        foreach (var bar in bars)
        {
            streamingValues.Add(pvr.Update(bar).Value);
        }

        // Batch
        var batchResult = Pvr.Batch(bars);

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
        var pvr = new Pvr();
        var streamingValues = new List<double>();
        foreach (var bar in bars)
        {
            streamingValues.Add(pvr.Update(bar).Value);
        }

        // Span
        var price = bars.Close.Values.ToArray();
        var volume = bars.Volume.Values.ToArray();
        var spanOutput = new double[bars.Count];

        Pvr.Batch(price, volume, spanOutput);

        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(streamingValues[i], spanOutput[i], 10);
        }
    }

    [Fact]
    public void SpanCalculate_InvalidLengths_ThrowsArgumentException()
    {
        var price = new double[100];
        var volume = new double[100];
        var output = new double[99]; // Different length

        Assert.Throws<ArgumentException>(() => Pvr.Batch(price, volume, output));
    }

    [Fact]
    public void SpanCalculate_EmptyInput_HandlesGracefully()
    {
        var price = Array.Empty<double>();
        var volume = Array.Empty<double>();
        var output = Array.Empty<double>();

        // Should not throw
        Pvr.Batch(price, volume, output);

        Assert.Empty(output);
    }

    [Fact]
    public void Event_PubFiresOnUpdate()
    {
        var pvr = new Pvr();
        TValue? receivedValue = null;
        bool receivedIsNew = false;

        pvr.Pub += (object? sender, in TValueEventArgs args) =>
        {
            receivedValue = args.Value;
            receivedIsNew = args.IsNew;
        };

        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000000);
        pvr.Update(bar, isNew: true);

        Assert.NotNull(receivedValue);
        Assert.True(receivedIsNew);
    }

    [Fact]
    public void Update_AllPossibleOutputs_AreValid()
    {
        var pvr = new Pvr();
        var time = DateTime.UtcNow;

        // Collect all unique PVR values
        var values = new HashSet<double>();

        // Generate various scenarios
        var scenarios = new[]
        {
            (100.0, 1000.0, 105.0, 1500.0), // price up, volume up -> 1
            (100.0, 1500.0, 105.0, 1000.0), // price up, volume down -> 2
            (100.0, 1500.0, 95.0, 1000.0),  // price down, volume down -> 3
            (100.0, 1000.0, 95.0, 1500.0),  // price down, volume up -> 4
            (100.0, 1000.0, 100.0, 1500.0), // price unchanged -> 0
        };

        foreach (var (p1, v1, p2, v2) in scenarios)
        {
            pvr.Reset();
            pvr.Update(new TBar(time, p1, p1 + 5, p1 - 5, p1, v1));
            var result = pvr.Update(new TBar(time.AddMinutes(1), p2, p2 + 5, p2 - 5, p2, v2));
            values.Add(result.Value);
        }

        // Should have all 5 possible values
        Assert.Contains(0.0, values);
        Assert.Contains(1.0, values);
        Assert.Contains(2.0, values);
        Assert.Contains(3.0, values);
        Assert.Contains(4.0, values);
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

        var pvr = new Pvr();
        foreach (var bar in bars)
        {
            var result = pvr.Update(bar);
            Assert.True(result.Value >= 0 && result.Value <= 4);
        }

        Assert.True(pvr.IsHot);
    }
}
