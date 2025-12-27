using Xunit;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class AdlTests
{
    [Fact]
    public void Adl_BasicCalculation_ReturnsExpectedValues()
    {
        // Arrange
        var adl = new Adl();
        var time = DateTime.UtcNow;
        
        // Bar 1: Close=10, High=12, Low=8. Range=4.
        // MFM = ((10-8) - (12-10)) / 4 = (2 - 2) / 4 = 0.
        // Vol = 100. MFV = 0. ADL = 0.
        var bar1 = new TBar(time, 10, 12, 8, 10, 100);
        var val1 = adl.Update(bar1);
        Assert.Equal(0, val1.Value);

        // Bar 2: Close=12, High=12, Low=8. Range=4.
        // MFM = ((12-8) - (12-12)) / 4 = (4 - 0) / 4 = 1.
        // Vol = 200. MFV = 200. ADL = 0 + 200 = 200.
        var bar2 = new TBar(time.AddMinutes(1), 10, 12, 8, 12, 200);
        var val2 = adl.Update(bar2);
        Assert.Equal(200, val2.Value);

        // Bar 3: Close=8, High=12, Low=8. Range=4.
        // MFM = ((8-8) - (12-8)) / 4 = (0 - 4) / 4 = -1.
        // Vol = 100. MFV = -100. ADL = 200 - 100 = 100.
        var bar3 = new TBar(time.AddMinutes(2), 12, 12, 8, 8, 100);
        var val3 = adl.Update(bar3);
        Assert.Equal(100, val3.Value);
    }

    [Fact]
    public void Adl_IsNew_False_UpdatesSameBar()
    {
        var adl = new Adl();
        var time = DateTime.UtcNow;

        // Initial update
        // MFM = 1, Vol = 100 -> ADL = 100
        var bar1 = new TBar(time, 10, 12, 8, 12, 100);
        adl.Update(bar1, isNew: true);
        Assert.Equal(100, adl.Last.Value);

        // Update same bar with different volume
        // MFM = 1, Vol = 200 -> ADL = 200 (replaces previous 100)
        var bar1Update = new TBar(time, 10, 12, 8, 12, 200);
        adl.Update(bar1Update, isNew: false);
        Assert.Equal(200, adl.Last.Value);
    }

    [Fact]
    public void Adl_Reset_ClearsState()
    {
        var adl = new Adl();
        var bar = new TBar(DateTime.UtcNow, 10, 12, 8, 12, 100);
        adl.Update(bar);
        
        Assert.True(adl.IsHot);
        Assert.NotEqual(0, adl.Last.Value);

        adl.Reset();
        Assert.False(adl.IsHot);
        Assert.Equal(0, adl.Last.Value);
    }

    [Fact]
    public void Adl_HighEqualsLow_HandlesDivisionByZero()
    {
        var adl = new Adl();
        // High = Low = 10. Range = 0. MFM should be 0.
        var bar = new TBar(DateTime.UtcNow, 10, 10, 10, 10, 100);
        var val = adl.Update(bar);
        Assert.Equal(0, val.Value);
    }
    
    [Fact]
    public void Adl_TValueUpdate_DoesNotChangeValue()
    {
        var adl = new Adl();
        var bar = new TBar(DateTime.UtcNow, 10, 12, 8, 12, 100);
        adl.Update(bar); // ADL = 100
        
        // Update with TValue (no volume info)
        adl.Update(new TValue(DateTime.UtcNow, 15));
        
        // Should remain 100
        Assert.Equal(100, adl.Last.Value);
    }

    [Fact]
    public void Adl_Name_IsCorrect()
    {
        Assert.Equal("ADL", Adl.Name);
    }

    [Fact]
    public void Adl_PubEvent_FiresOnUpdate()
    {
        var adl = new Adl();
        bool eventFired = false;
        adl.Pub += (object? sender, TValueEventArgs args) => eventFired = true;

        adl.Update(new TBar(DateTime.UtcNow, 10, 12, 8, 10, 100));
        Assert.True(eventFired);
    }

    [Fact]
    public void Adl_UpdateTBarSeries_ReturnsCorrectSeries()
    {
        var adl = new Adl();
        var bars = new TBarSeries();
        var time = DateTime.UtcNow;
        
        // Add same bars as in BasicCalculation
        bars.Add(new TBar(time, 10, 12, 8, 10, 100)); // ADL=0
        bars.Add(new TBar(time.AddMinutes(1), 10, 12, 8, 12, 200)); // ADL=200
        bars.Add(new TBar(time.AddMinutes(2), 12, 12, 8, 8, 100)); // ADL=100

        var result = adl.Update(bars);
        
        Assert.Equal(3, result.Count);
        Assert.Equal(0, result[0].Value);
        Assert.Equal(200, result[1].Value);
        Assert.Equal(100, result[2].Value);
    }

    [Fact]
    public void Adl_CalculateTBarSeries_ReturnsCorrectSeries()
    {
        var bars = new TBarSeries();
        var time = DateTime.UtcNow;
        
        bars.Add(new TBar(time, 10, 12, 8, 10, 100));
        bars.Add(new TBar(time.AddMinutes(1), 10, 12, 8, 12, 200));
        bars.Add(new TBar(time.AddMinutes(2), 12, 12, 8, 8, 100));

        var result = Adl.Calculate(bars);
        
        Assert.Equal(3, result.Count);
        Assert.Equal(0, result[0].Value);
        Assert.Equal(200, result[1].Value);
        Assert.Equal(100, result[2].Value);
    }
    
    [Fact]
    public void Adl_CalculateSpan_ReturnsCorrectValues()
    {
        double[] high = { 12, 12, 12 };
        double[] low = { 8, 8, 8 };
        double[] close = { 10, 12, 8 };
        double[] volume = { 100, 200, 100 };
        double[] output = new double[3];

        Adl.Calculate(high, low, close, volume, output);
        
        Assert.Equal(0, output[0]);
        Assert.Equal(200, output[1]);
        Assert.Equal(100, output[2]);
    }

    [Fact]
    public void Adl_CalculateSpan_ThrowsOnMismatchedLengths()
    {
        double[] high = { 10, 11 };
        double[] low = { 9, 10 };
        double[] close = { 9.5, 10.5 };
        double[] volume = { 100 }; // Short
        double[] output = new double[2];

        Assert.Throws<ArgumentException>(() => 
            Adl.Calculate(high, low, close, volume, output));
    }

    [Fact]
    public void Adl_Calculate_EmptySeries_ReturnsEmpty()
    {
        var bars = new TBarSeries();
        var result = Adl.Calculate(bars);
        Assert.Empty(result);
    }

    [Fact]
    public void Adl_CalculateSpan_SimdPath_ReturnsCorrectValues()
    {
        int count = 100; // Enough to trigger SIMD
        double[] high = new double[count];
        double[] low = new double[count];
        double[] close = new double[count];
        double[] volume = new double[count];
        double[] output = new double[count];

        // Setup: High=12, Low=8, Close=12 (MFM=1), Vol=10
        // Expected ADL increments by 10 each step.
        for (int i = 0; i < count; i++)
        {
            high[i] = 12;
            low[i] = 8;
            close[i] = 12;
            volume[i] = 10;
        }

        Adl.Calculate(high, low, close, volume, output);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal((i + 1) * 10, output[i]);
        }
    }
}
