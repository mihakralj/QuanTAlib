using System;
using Xunit;

namespace QuanTAlib;

public class RsxTests
{
    private readonly GBM _gbm;

    public RsxTests()
    {
        _gbm = new GBM();
    }

    [Fact]
    public void Constructor_InvalidPeriod_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Rsx(0));
        Assert.Throws<ArgumentException>(() => new Rsx(-1));
    }

    [Fact]
    public void Update_ValidInput_ReturnsValidRsx()
    {
        var rsx = new Rsx(14);
        var result = rsx.Update(new TValue(DateTime.UtcNow, 100));
        Assert.InRange(result.Value, 0, 100);
    }

    [Fact]
    public void Update_NaN_UsesLastValidValue()
    {
        var rsx = new Rsx(14);
        rsx.Update(new TValue(DateTime.UtcNow, 100));
        var result = rsx.Update(new TValue(DateTime.UtcNow, double.NaN));
        
        // Should not be NaN
        Assert.False(double.IsNaN(result.Value));
        Assert.InRange(result.Value, 0, 100);
    }

    [Fact]
    public void Update_IsNew_Consistency()
    {
        var rsx = new Rsx(14);
        var time = DateTime.UtcNow;
        
        // Update with isNew=true
        var val1 = rsx.Update(new TValue(time, 100), true);
        
        // Update with isNew=false (same time, different value)
        rsx.Update(new TValue(time, 105), false);
        
        // Update with isNew=false (same time, original value) - should match val1 if state rollback works
        // Note: RSX is highly sensitive to path, so exact match might be tricky if intermediate states drift,
        // but for a single step rollback it should be very close.
        var val3 = rsx.Update(new TValue(time, 100), false);

        Assert.Equal(val1.Value, val3.Value, 1e-9);
    }

    [Fact]
    public void Calculate_Span_Matches_Update()
    {
        int period = 14;
        int count = 100;
        var bars = _gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;
        var rsx = new Rsx(period);
        
        var resultSeries = rsx.Update(series);
        
        var spanInput = series.Values.ToArray();
        var spanOutput = new double[count];
        Rsx.Calculate(spanInput, spanOutput, period);
        
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(resultSeries.Values[i], spanOutput[i], 1e-9);
        }
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var rsx = new Rsx(14);
        rsx.Update(new TValue(DateTime.UtcNow, 100));
        rsx.Reset();
        
        // After reset, it should behave like a new instance
        // RSX initializes with 0 filters.
        // If we feed it the same value, it should produce the same initial output.
        // However, RSX output depends on change (v8), so first value sets LastF8 but v8=0.
        
        var val1 = rsx.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(50.0, val1.Value); // Neutral start
    }
    
    [Fact]
    public void Chain_Works()
    {
        var rsx = new Rsx(14);
        var rsx2 = new Rsx(rsx, 14);
        
        var result = rsx2.Update(new TValue(DateTime.UtcNow, 100));
        Assert.False(double.IsNaN(result.Value));
    }
}
