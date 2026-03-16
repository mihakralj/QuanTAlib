
namespace QuanTAlib.Tests;

public class AdTests
{
    [Fact]
    public void Ad_BasicCalculation_ReturnsExpectedValues()
    {
        // Arrange
        var ad = new Ad();
        var time = DateTime.UtcNow;

        // Bar 1: Close=10, High=12, Low=8. Range=4.
        // MFM = ((10-8) - (12-10)) / 4 = (2 - 2) / 4 = 0.
        // Vol = 100. MFV = 0. AD = 0.
        var bar1 = new TBar(time, 10, 12, 8, 10, 100);
        var val1 = ad.Update(bar1);
        Assert.Equal(0, val1.Value);

        // Bar 2: Close=12, High=12, Low=8. Range=4.
        // MFM = ((12-8) - (12-12)) / 4 = (4 - 0) / 4 = 1.
        // Vol = 200. MFV = 200. AD = 0 + 200 = 200.
        var bar2 = new TBar(time.AddMinutes(1), 10, 12, 8, 12, 200);
        var val2 = ad.Update(bar2);
        Assert.Equal(200, val2.Value);

        // Bar 3: Close=8, High=12, Low=8. Range=4.
        // MFM = ((8-8) - (12-8)) / 4 = (0 - 4) / 4 = -1.
        // Vol = 100. MFV = -100. AD = 200 - 100 = 100.
        var bar3 = new TBar(time.AddMinutes(2), 12, 12, 8, 8, 100);
        var val3 = ad.Update(bar3);
        Assert.Equal(100, val3.Value);
    }

    [Fact]
    public void Ad_IsNew_False_UpdatesSameBar()
    {
        var ad = new Ad();
        var time = DateTime.UtcNow;

        // Initial update
        // MFM = 1, Vol = 100 -> AD = 100
        var bar1 = new TBar(time, 10, 12, 8, 12, 100);
        ad.Update(bar1, isNew: true);
        Assert.Equal(100, ad.Last.Value);

        // Update same bar with different volume
        // MFM = 1, Vol = 200 -> AD = 200 (replaces previous 100)
        var bar1Update = new TBar(time, 10, 12, 8, 12, 200);
        ad.Update(bar1Update, isNew: false);
        Assert.Equal(200, ad.Last.Value);
    }

    [Fact]
    public void Ad_Reset_ClearsState()
    {
        var ad = new Ad();
        var bar = new TBar(DateTime.UtcNow, 10, 12, 8, 12, 100);
        ad.Update(bar);

        Assert.True(ad.IsHot);
        Assert.NotEqual(0, ad.Last.Value);

        ad.Reset();
        Assert.False(ad.IsHot);
        Assert.Equal(0, ad.Last.Value);
    }

    [Fact]
    public void Ad_HighEqualsLow_HandlesDivisionByZero()
    {
        var ad = new Ad();
        // High = Low = 10. Range = 0. MFM should be 0.
        var bar = new TBar(DateTime.UtcNow, 10, 10, 10, 10, 100);
        var val = ad.Update(bar);
        Assert.Equal(0, val.Value);
    }

    [Fact]
    public void Ad_TValueUpdate_ThrowsNotSupportedException()
    {
        var ad = new Ad();
        var bar = new TBar(DateTime.UtcNow, 10, 12, 8, 12, 100);
        ad.Update(bar); // AD = 100

        // Update with TValue should throw since AD requires OHLCV bar data
        Assert.Throws<NotSupportedException>(() => ad.Update(new TValue(DateTime.UtcNow, 15)));
    }

    [Fact]
    public void Ad_Name_IsCorrect()
    {
        Assert.Equal("AD", Ad.Name);
    }

    [Fact]
    public void Ad_PubEvent_FiresOnUpdate()
    {
        var ad = new Ad();
        bool eventFired = false;
        ad.Pub += (object? sender, in TValueEventArgs args) => eventFired = true;

        ad.Update(new TBar(DateTime.UtcNow, 10, 12, 8, 10, 100));
        Assert.True(eventFired);
    }

    [Fact]
    public void Ad_UpdateTBarSeries_ReturnsCorrectSeries()
    {
        var ad = new Ad();
        var bars = new TBarSeries();
        var time = DateTime.UtcNow;

        // Add same bars as in BasicCalculation
        bars.Add(new TBar(time, 10, 12, 8, 10, 100)); // AD=0
        bars.Add(new TBar(time.AddMinutes(1), 10, 12, 8, 12, 200)); // AD=200
        bars.Add(new TBar(time.AddMinutes(2), 12, 12, 8, 8, 100)); // AD=100

        var result = ad.Update(bars);

        Assert.Equal(3, result.Count);
        Assert.Equal(0, result[0].Value);
        Assert.Equal(200, result[1].Value);
        Assert.Equal(100, result[2].Value);
    }

    [Fact]
    public void Ad_CalculateTBarSeries_ReturnsCorrectSeries()
    {
        var bars = new TBarSeries();
        var time = DateTime.UtcNow;

        bars.Add(new TBar(time, 10, 12, 8, 10, 100));
        bars.Add(new TBar(time.AddMinutes(1), 10, 12, 8, 12, 200));
        bars.Add(new TBar(time.AddMinutes(2), 12, 12, 8, 8, 100));

        var result = Ad.Batch(bars);

        Assert.Equal(3, result.Count);
        Assert.Equal(0, result[0].Value);
        Assert.Equal(200, result[1].Value);
        Assert.Equal(100, result[2].Value);
    }

    [Fact]
    public void Ad_CalculateSpan_ReturnsCorrectValues()
    {
        double[] high = { 12, 12, 12 };
        double[] low = { 8, 8, 8 };
        double[] close = { 10, 12, 8 };
        double[] volume = { 100, 200, 100 };
        double[] output = new double[3];

        Ad.Batch(high, low, close, volume, output);

        Assert.Equal(0, output[0]);
        Assert.Equal(200, output[1]);
        Assert.Equal(100, output[2]);
    }

    [Fact]
    public void Ad_CalculateSpan_ThrowsOnMismatchedLengths()
    {
        double[] high = { 10, 11 };
        double[] low = { 9, 10 };
        double[] close = { 9.5, 10.5 };
        double[] volume = { 100 }; // Short
        double[] output = new double[2];

        Assert.Throws<ArgumentException>(() =>
            Ad.Batch(high, low, close, volume, output));
    }

    [Fact]
    public void Ad_Calculate_EmptySeries_ReturnsEmpty()
    {
        var bars = new TBarSeries();
        var result = Ad.Batch(bars);
        Assert.Empty(result);
    }

    [Fact]
    public void Ad_CalculateSpan_SimdPath_ReturnsCorrectValues()
    {
        const int count = 100; // Enough to trigger SIMD
        double[] high = new double[count];
        double[] low = new double[count];
        double[] close = new double[count];
        double[] volume = new double[count];
        double[] output = new double[count];

        // Setup: High=12, Low=8, Close=12 (MFM=1), Vol=10
        // Expected AD increments by 10 each step.
        for (int i = 0; i < count; i++)
        {
            high[i] = 12;
            low[i] = 8;
            close[i] = 12;
            volume[i] = 10;
        }

        Ad.Batch(high, low, close, volume, output);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal((i + 1) * 10, output[i]);
        }
    }
}
