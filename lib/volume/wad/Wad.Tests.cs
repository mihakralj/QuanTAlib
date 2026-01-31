namespace QuanTAlib.Tests;

public class WadTests
{
    [Fact]
    public void Wad_BasicCalculation_ReturnsExpectedValues()
    {
        // Arrange
        var wad = new Wad();
        var time = DateTime.UtcNow;

        // Bar 1: First bar, WAD = 0 (no previous close)
        var bar1 = new TBar(time, 100, 105, 95, 100, 1000);
        var val1 = wad.Update(bar1);
        Assert.Equal(0, val1.Value);

        // Bar 2: Close=110 > PrevClose=100, TrueLow = min(92, 100) = 92
        // PM = 110 - 92 = 18, Vol = 2000
        // AD = 18 * 2000 = 36000, WAD = 0 + 36000 = 36000
        var bar2 = new TBar(time.AddMinutes(1), 100, 115, 92, 110, 2000);
        var val2 = wad.Update(bar2);
        Assert.Equal(36000, val2.Value);

        // Bar 3: Close=105 < PrevClose=110, TrueHigh = max(108, 110) = 110
        // PM = 105 - 110 = -5, Vol = 1500
        // AD = -5 * 1500 = -7500, WAD = 36000 - 7500 = 28500
        var bar3 = new TBar(time.AddMinutes(2), 110, 108, 102, 105, 1500);
        var val3 = wad.Update(bar3);
        Assert.Equal(28500, val3.Value);
    }

    [Fact]
    public void Wad_CloseUnchanged_ZeroPriceMovement()
    {
        var wad = new Wad();
        var time = DateTime.UtcNow;

        // Bar 1
        var bar1 = new TBar(time, 100, 105, 95, 100, 1000);
        wad.Update(bar1);

        // Bar 2: Close=100 == PrevClose=100 -> PM = 0
        var bar2 = new TBar(time.AddMinutes(1), 100, 110, 90, 100, 2000);
        var val2 = wad.Update(bar2);
        Assert.Equal(0, val2.Value);
    }

    [Fact]
    public void Wad_IsNew_False_UpdatesSameBar()
    {
        var wad = new Wad();
        var time = DateTime.UtcNow;

        // Initial bar
        var bar1 = new TBar(time, 100, 105, 95, 100, 1000);
        wad.Update(bar1, isNew: true);
        Assert.Equal(0, wad.Last.Value);

        // Bar 2: Close=110 > PrevClose=100
        var bar2 = new TBar(time.AddMinutes(1), 100, 115, 92, 110, 2000);
        wad.Update(bar2, isNew: true);
        Assert.Equal(36000, wad.Last.Value);

        // Update same bar with different data (isNew=false)
        // Close=108 > PrevClose=100, TrueLow = min(92, 100) = 92
        // PM = 108 - 92 = 16, Vol = 1000
        // AD = 16 * 1000 = 16000, WAD = 0 + 16000 = 16000
        var bar2Update = new TBar(time.AddMinutes(1), 100, 115, 92, 108, 1000);
        wad.Update(bar2Update, isNew: false);
        Assert.Equal(16000, wad.Last.Value);
    }

    [Fact]
    public void Wad_Reset_ClearsState()
    {
        var wad = new Wad();
        var time = DateTime.UtcNow;

        var bar1 = new TBar(time, 100, 105, 95, 100, 1000);
        wad.Update(bar1);
        var bar2 = new TBar(time.AddMinutes(1), 100, 115, 92, 110, 2000);
        wad.Update(bar2);

        Assert.True(wad.IsHot);
        Assert.NotEqual(0, wad.Last.Value);

        wad.Reset();
        Assert.False(wad.IsHot);
        Assert.Equal(0, wad.Last.Value);
    }

    [Fact]
    public void Wad_TValueUpdate_ThrowsNotSupportedException()
    {
        var wad = new Wad();
        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 100, 1000);
        wad.Update(bar);

        Assert.Throws<NotSupportedException>(() => wad.Update(new TValue(DateTime.UtcNow, 15)));
    }

    [Fact]
    public void Wad_Name_IsCorrect()
    {
        Assert.Equal("WAD", Wad.Name);
    }

    [Fact]
    public void Wad_PubEvent_FiresOnUpdate()
    {
        var wad = new Wad();
        bool eventFired = false;
        wad.Pub += (object? sender, in TValueEventArgs args) => eventFired = true;

        wad.Update(new TBar(DateTime.UtcNow, 100, 105, 95, 100, 1000));
        Assert.True(eventFired);
    }

    [Fact]
    public void Wad_UpdateTBarSeries_ReturnsCorrectSeries()
    {
        var wad = new Wad();
        var bars = new TBarSeries();
        var time = DateTime.UtcNow;

        bars.Add(new TBar(time, 100, 105, 95, 100, 1000));
        bars.Add(new TBar(time.AddMinutes(1), 100, 115, 92, 110, 2000));
        bars.Add(new TBar(time.AddMinutes(2), 110, 108, 102, 105, 1500));

        var result = wad.Update(bars);

        Assert.Equal(3, result.Count);
        Assert.Equal(0, result[0].Value);
        Assert.Equal(36000, result[1].Value);
        Assert.Equal(28500, result[2].Value);
    }

    [Fact]
    public void Wad_CalculateTBarSeries_ReturnsCorrectSeries()
    {
        var bars = new TBarSeries();
        var time = DateTime.UtcNow;

        bars.Add(new TBar(time, 100, 105, 95, 100, 1000));
        bars.Add(new TBar(time.AddMinutes(1), 100, 115, 92, 110, 2000));
        bars.Add(new TBar(time.AddMinutes(2), 110, 108, 102, 105, 1500));

        var result = Wad.Calculate(bars);

        Assert.Equal(3, result.Count);
        Assert.Equal(0, result[0].Value);
        Assert.Equal(36000, result[1].Value);
        Assert.Equal(28500, result[2].Value);
    }

    [Fact]
    public void Wad_CalculateSpan_ReturnsCorrectValues()
    {
        double[] high = { 105, 115, 108 };
        double[] low = { 95, 92, 102 };
        double[] close = { 100, 110, 105 };
        double[] volume = { 1000, 2000, 1500 };
        double[] output = new double[3];

        Wad.Calculate(high, low, close, volume, output);

        Assert.Equal(0, output[0]);
        Assert.Equal(36000, output[1]);
        Assert.Equal(28500, output[2]);
    }

    [Fact]
    public void Wad_CalculateSpan_ThrowsOnMismatchedLengths()
    {
        double[] high = { 105, 115 };
        double[] low = { 95, 92 };
        double[] close = { 100, 110 };
        double[] volume = { 1000 }; // Short
        double[] output = new double[2];

        Assert.Throws<ArgumentException>(() =>
            Wad.Calculate(high, low, close, volume, output));
    }

    [Fact]
    public void Wad_Calculate_EmptySeries_ReturnsEmpty()
    {
        var bars = new TBarSeries();
        var result = Wad.Calculate(bars);
        Assert.Empty(result);
    }

    [Fact]
    public void Wad_CalculateSpan_LargeDataset()
    {
        const int count = 1000;
        double[] high = new double[count];
        double[] low = new double[count];
        double[] close = new double[count];
        double[] volume = new double[count];
        double[] output = new double[count];

        // Setup: Ascending close pattern
        for (int i = 0; i < count; i++)
        {
            close[i] = 100 + i;
            high[i] = close[i] + 5;
            low[i] = close[i] - 5;
            volume[i] = 100;
        }

        Wad.Calculate(high, low, close, volume, output);

        // First bar should be 0
        Assert.Equal(0, output[0]);

        // All subsequent bars should have positive accumulation since close is always rising
        for (int i = 1; i < count; i++)
        {
            Assert.True(output[i] > output[i - 1], $"WAD should increase at index {i}");
        }
    }

    [Fact]
    public void Wad_StreamingMatchesBatch()
    {
        var bars = new TBarSeries();
        var gbm = new GBM();

        // Generate bars using GBM
        for (int i = 0; i < 100; i++)
        {
            bars.Add(gbm.Next());
        }

        // Batch calculation
        var batchResult = Wad.Calculate(bars);

        // Streaming calculation
        var wad = new Wad();
        var streamingResult = wad.Update(bars);

        // Compare results
        Assert.Equal(batchResult.Count, streamingResult.Count);
        for (int i = 0; i < batchResult.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, streamingResult[i].Value, precision: 10);
        }
    }

    [Fact]
    public void Wad_IsHot_BecomesTrue_AfterFirstBar()
    {
        var wad = new Wad();
        Assert.False(wad.IsHot);

        wad.Update(new TBar(DateTime.UtcNow, 100, 105, 95, 100, 1000));
        Assert.True(wad.IsHot);
    }
}