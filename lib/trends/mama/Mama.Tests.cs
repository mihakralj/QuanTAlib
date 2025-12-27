using System;
using Xunit;

namespace QuanTAlib;

public class MamaTests
{
    [Fact]
    public void Constructor_InvalidParameters_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Mama(fastLimit: 0.05, slowLimit: 0.5)); // fast < slow
        Assert.Throws<ArgumentException>(() => new Mama(fastLimit: 0.5, slowLimit: -0.1)); // slow < 0
        Assert.Throws<ArgumentException>(() => new Mama(fastLimit: 0.0, slowLimit: 0.05)); // fast <= 0
    }

    [Fact]
    public void Update_ValidInput_CalculatesMamaAndFama()
    {
        var mama = new Mama(fastLimit: 0.5, slowLimit: 0.05);
        var input = new TValue(DateTime.UtcNow, 100.0);

        var result = mama.Update(input);

        Assert.Equal(100.0, result.Value); // First value should be price
        Assert.Equal(100.0, mama.Fama.Value);
    }

    [Fact]
    public void Update_NaN_HandlesGracefully()
    {
        var mama = new Mama();
        var input = new TValue(DateTime.UtcNow, double.NaN);

        var result = mama.Update(input);

        // Should return 0.0 (last valid price default) instead of NaN to avoid state corruption
        Assert.Equal(0.0, result.Value);
    }

    [Fact]
    public void Update_Series_ReturnsSameCount()
    {
        var mama = new Mama();
        var source = new TSeries();
        source.Add(new TValue(DateTime.UtcNow, 100.0));
        source.Add(new TValue(DateTime.UtcNow.AddMinutes(1), 101.0));

        var result = mama.Update(source);

        Assert.Equal(source.Count, result.Count);
    }

    [Fact]
    public void Chain_Update_Works()
    {
        var mama = new Mama(0.5, 0.05);

        // Manually chain for test
        bool eventFired = false;
        mama.Pub += (object? sender, TValueEventArgs args) => eventFired = true;

        mama.Update(new TValue(DateTime.UtcNow, 100.0));

        Assert.True(eventFired);
    }

    [Fact]
    public void Update_Series_AppendsData()
    {
        var mama1 = new Mama();
        var mama2 = new Mama();

        var data = new TSeries();
        var now = DateTime.UtcNow;
        for (int i = 0; i < 50; i++)
        {
            data.Add(new TValue(now.AddMinutes(i), 100.0 + Math.Sin(i * 0.1) * 10));
        }

        // Case 1: Update all at once
        var result1 = mama1.Update(data);

        // Case 2: Update in chunks
        var chunk1 = new TSeries();
        var chunk2 = new TSeries();
        for (int i = 0; i < 25; i++) chunk1.Add(data[i]);
        for (int i = 25; i < 50; i++) chunk2.Add(data[i]);

        mama2.Update(chunk1);
        var result2 = mama2.Update(chunk2);

        // Verify final state is same
        Assert.Equal(mama1.Last.Value, mama2.Last.Value, 6);
        Assert.Equal(mama1.Fama.Value, mama2.Fama.Value, 6);

        // Verify the returned series from the second chunk matches the second half of the full result
        for (int i = 0; i < 25; i++)
        {
            Assert.Equal(result1[25 + i].Value, result2[i].Value, 6);
        }
    }

    [Fact]
    public void IsHot_BecomesTrueAfterWarmup()
    {
        var mama = new Mama();
        
        // MAMA needs 50 bars to warmup (Index > 50)
        for (int i = 0; i < 50; i++)
        {
            mama.Update(new TValue(DateTime.UtcNow, 100));
            Assert.False(mama.IsHot);
        }
        
        mama.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(mama.IsHot);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var mama = new Mama();
        for (int i = 0; i < 55; i++)
        {
            mama.Update(new TValue(DateTime.UtcNow, 100));
        }
        Assert.True(mama.IsHot);
        
        mama.Reset();
        
        Assert.False(mama.IsHot);
        Assert.True(double.IsNaN(mama.Last.Value));
    }

    [Fact]
    public void Update_BarCorrection_UpdatesCorrectly()
    {
        var mama = new Mama();
        
        // Warmup
        for (int i = 0; i < 10; i++)
        {
            mama.Update(new TValue(DateTime.UtcNow, 100));
        }
        
        // New bar
        var result1 = mama.Update(new TValue(DateTime.UtcNow, 110));
        
        // Update same bar with different value
        var result2 = mama.Update(new TValue(DateTime.UtcNow, 120), isNew: false);
        
        Assert.NotEqual(result1.Value, result2.Value);
        
        // Verify internal state by adding next bar
        var result3 = mama.Update(new TValue(DateTime.UtcNow, 130));
        Assert.True(double.IsFinite(result3.Value));
    }

    [Fact]
    public void Calculate_StaticMethod_MatchesObjectInstance()
    {
        var source = new TSeries();
        var gbm = new GBM(startPrice: 100, seed: 42);
        
        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next();
            source.Add(bar.C);
        }
        
        var mama = new Mama();
        var series1 = mama.Update(source);
        var series2 = Mama.Batch(source);
        
        Assert.Equal(series1.Count, series2.Count);
        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(series1[i].Value, series2[i].Value, 1e-9);
        }
    }

    [Fact]
    public void Calculate_Span_Matches_Update()
    {
        int count = 100;
        var data = new double[count];
        var gbm = new GBM(startPrice: 100, seed: 42);
        for (int i = 0; i < count; i++) data[i] = gbm.Next().Close;

        var output = new double[count];
        Mama.Calculate(data, output);

        var mama = new Mama();
        for (int i = 0; i < count; i++)
        {
            var res = mama.Update(new TValue(DateTime.UtcNow, data[i]));
            Assert.Equal(res.Value, output[i], precision: 8);
        }
    }

    [Fact]
    public void Calculate_Span_ThrowsOnSmallOutput()
    {
        var data = new double[10];
        var output = new double[5];
        Assert.Throws<ArgumentOutOfRangeException>(() => Mama.Calculate(data, output));
    }

    [Fact]
    public void Prime_PreloadsState()
    {
        var data = new double[60];
        var gbm = new GBM(startPrice: 100, seed: 42);
        for (int i = 0; i < 60; i++) data[i] = gbm.Next().Close;

        // 1. Prime with all but last value
        var mamaPrimed = new Mama();
        mamaPrimed.Prime(data.AsSpan().Slice(0, 59));

        // 2. Update with last value
        var resultPrimed = mamaPrimed.Update(new TValue(DateTime.UtcNow, data[59]));

        // 3. Run normal updates for comparison
        var mamaNormal = new Mama();
        TValue resultNormal = default;
        for (int i = 0; i < 60; i++)
        {
            resultNormal = mamaNormal.Update(new TValue(DateTime.UtcNow, data[i]));
        }

        Assert.True(mamaPrimed.IsHot);
        Assert.Equal(resultNormal.Value, resultPrimed.Value, precision: 9);
    }
}
