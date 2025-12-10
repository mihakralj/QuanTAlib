using Xunit;
using System;

namespace QuanTAlib.Tests;

public class T3Tests
{
    [Fact]
    public void T3_Constructor_Period_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new T3(0));
        Assert.Throws<ArgumentException>(() => new T3(-1));

        var t3 = new T3(10);
        Assert.NotNull(t3);
    }

    [Fact]
    public void T3_ConstantInput_ConvergesToInput()
    {
        var t3 = new T3(5, 0.7);
        double input = 100.0;
        
        // Feed enough values for T3 to converge (it has 6 cascaded EMAs)
        for(int i = 0; i < 100; i++)
        {
            t3.Update(new TValue(DateTime.UtcNow, input));
        }

        Assert.Equal(input, t3.Last.Value, 1e-9);
    }

    [Fact]
    public void T3_Parameters_AffectResult()
    {
        // Different volume factors should produce different results for changing data
        var t3_low_v = new T3(10, 0.1);
        var t3_high_v = new T3(10, 0.9);

        var series = new TSeries();
        series.Add(DateTime.UtcNow, 100);
        series.Add(DateTime.UtcNow.AddMinutes(1), 110);
        series.Add(DateTime.UtcNow.AddMinutes(2), 120);

        t3_low_v.Update(series);
        t3_high_v.Update(series);

        Assert.NotEqual(t3_low_v.Last.Value, t3_high_v.Last.Value);
    }

    [Fact]
    public void T3_Reset_ResetsState()
    {
        var t3 = new T3(10);
        t3.Update(new TValue(DateTime.UtcNow, 100));
        t3.Update(new TValue(DateTime.UtcNow, 110));

        Assert.True(t3.IsHot);
        Assert.NotEqual(0, t3.Last.Value);

        t3.Reset();

        Assert.False(t3.IsHot);
        Assert.Equal(0, t3.Last.Value);

        // Should accept new data as if fresh
        t3.Update(new TValue(DateTime.UtcNow, 50));
        Assert.Equal(50, t3.Last.Value, 1e-9); // First value logic: output = input
    }

    [Fact]
    public void T3_Eventing_Works()
    {
        var source = new TSeries();
        var t3 = new T3(source, 10);
        double lastVal = 0;

        t3.Pub += (v) => lastVal = v.Value;

        source.Add(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100, lastVal, 1e-9);
        
        source.Add(new TValue(DateTime.UtcNow, 110));
        Assert.NotEqual(100, lastVal);
        Assert.NotEqual(0, lastVal);
    }

    [Fact]
    public void T3_SpanTests()
    {
        var series = new TSeries();
        int count = 100;
        for(int i=0; i<count; i++) 
            series.Add(DateTime.UtcNow.AddMinutes(i), 100 + i);

        var t3 = new T3(10);
        var resSeries = t3.Update(series);

        var resSpan = new double[count];
        // Correctly use Span.CopyTo
        T3.Calculate(series, 10).Values.CopyTo(resSpan.AsSpan());

        // Check last values match
        Assert.Equal(resSeries.Last.Value, resSpan[count-1], 1e-9);
    }

    [Fact]
    public void T3_BarCorrection_WithNaN_RestoresPreviousValidValue()
    {
        var t3 = new T3(10);
        var time = DateTime.UtcNow;

        // Step 1: Update with valid value
        t3.Update(new TValue(time, 100), isNew: true);
        
        // Step 2: Update with another valid value
        t3.Update(new TValue(time.AddMinutes(1), 200), isNew: true);
        double valAfter200 = t3.Last.Value;

        // Step 3: Correct with NaN (should use 100)
        t3.Update(new TValue(time.AddMinutes(1), double.NaN), isNew: false);
        double valAfterNaN = t3.Last.Value;

        // Step 4: Correct with 100 (should match NaN result)
        t3.Update(new TValue(time.AddMinutes(1), 100), isNew: false);
        double valAfter100 = t3.Last.Value;

        Assert.NotEqual(valAfter200, valAfterNaN); // Should not be the same as 200
        Assert.Equal(valAfter100, valAfterNaN, 1e-9); // Should be the same as using 100
    }
}
