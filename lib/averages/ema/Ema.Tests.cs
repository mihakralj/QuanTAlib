using System;
using Xunit;
using QuanTAlib;

namespace QuanTAlib.Tests;

public class EmaTests
{
    [Fact]
    public void Ema_Constructor_Period_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Ema(0));
        Assert.Throws<ArgumentException>(() => new Ema(-1));
        
        var ema = new Ema(10);
        Assert.NotNull(ema);
    }

    [Fact]
    public void Ema_Constructor_Alpha_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Ema(0.0));
        Assert.Throws<ArgumentException>(() => new Ema(-0.1));
        Assert.Throws<ArgumentException>(() => new Ema(1.1));
        
        var ema = new Ema(0.5);
        Assert.NotNull(ema);
    }

    [Fact]
    public void Ema_Calc_ReturnsValue()
    {
        var ema = new Ema(10);
        
        Assert.Equal(0, ema.Value.Value);
        
        TValue result = ema.Update(new TValue(DateTime.Now, 100));
        
        Assert.True(result.Value > 0);
        Assert.Equal(result.Value, ema.Value.Value);
    }

    [Fact]
    public void Ema_Calc_IsNew_AcceptsParameter()
    {
        var ema = new Ema(10);
        
        ema.Update(new TValue(DateTime.Now, 100), isNew: true);
        double value1 = ema.Value;
        
        ema.Update(new TValue(DateTime.Now, 105), isNew: true);
        double value2 = ema.Value;
        
        // Values should change with new bars
        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Ema_Calc_IsNew_False_UpdatesValue()
    {
        var ema = new Ema(10);
        
        ema.Update(new TValue(DateTime.Now, 100));
        ema.Update(new TValue(DateTime.Now, 110), isNew: true);
        double beforeUpdate = ema.Value;
        
        ema.Update(new TValue(DateTime.Now, 120), isNew: false);
        double afterUpdate = ema.Value;
        
        // Update should change the value
        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void Ema_Reset_ClearsState()
    {
        var ema = new Ema(10);
        
        ema.Update(new TValue(DateTime.Now, 100));
        ema.Update(new TValue(DateTime.Now, 105));
        double valueBefore = ema.Value;
        
        ema.Reset();
        
        Assert.Equal(0, ema.Value.Value);
        
        // After reset, should accept new values
        ema.Update(new TValue(DateTime.Now, 50));
        Assert.NotEqual(0, ema.Value.Value);
        Assert.NotEqual(valueBefore, ema.Value.Value);
    }

    [Fact]
    public void Ema_Properties_Accessible()
    {
        var ema = new Ema(10);
        
        Assert.Equal(0, ema.Value.Value);
        Assert.False(ema.IsHot);
        
        ema.Update(new TValue(DateTime.Now, 100));
        
        Assert.NotEqual(0, ema.Value.Value);
    }

    [Fact]
    public void Ema_IsHot_BecomesTrueAfterWarmup()
    {
        var ema = new Ema(10);
        
        // Initially IsHot should be false
        Assert.False(ema.IsHot);
        
        // Feed values until it warms up
        // Warmup condition is state.E <= 1e-10
        // state.E starts at 1.0 and decays by (1 - alpha) each step
        // alpha = 2 / (10 + 1) = 2/11 ~= 0.1818
        // (1 - alpha) ~= 0.8181
        // 1.0 * (0.8181)^n <= 1e-10
        // n * log(0.8181) <= log(1e-10)
        // n * -0.200 <= -23.02
        // n >= 115 steps roughly
        
        int steps = 0;
        while (!ema.IsHot && steps < 1000)
        {
            ema.Update(new TValue(DateTime.Now, 100));
            steps++;
        }
        
        Assert.True(ema.IsHot);
        Assert.True(steps > 0); // Should take some steps
    }

    [Fact]
    public void Ema_PeriodEquivalence_BothConstructorsWork()
    {
        int period = 20;
        double alpha = 2.0 / (period + 1);
        
        var emaPeriod = new Ema(period);
        var emaAlpha = new Ema(alpha);
        
        // Both should accept Calc calls and produce same result
        TValue result1 = emaPeriod.Update(new TValue(DateTime.Now, 100));
        TValue result2 = emaAlpha.Update(new TValue(DateTime.Now, 100));
        
        Assert.Equal(result1.Value, result2.Value, 1e-10);
    }

    [Fact]
    public void Ema_IterativeCorrections_RestoreToOriginalState()
    {
        var ema = new Ema(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);
        
        // Feed 10 new values
        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            ema.Update(tenthInput, isNew: true);
        }
        
        // Remember EMA state after 10 values
        double emaAfterTen = ema.Value;
        
        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            ema.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }
        
        // Feed the remembered 10th input again with isNew=false
        TValue finalEma = ema.Update(tenthInput, isNew: false);
        
        // EMA should match the original state after 10 values
        Assert.Equal(emaAfterTen, finalEma.Value, 1e-10);
    }

    [Fact]
    public void Ema_BatchCalc_MatchesIterativeCalc()
    {
        var emaIterative = new Ema(10);
        var emaBatch = new Ema(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);
        
        // Generate data
        var series = new TSeries();
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }
        
        // Calculate iteratively
        var iterativeResults = new TSeries();
        foreach (var item in series)
        {
            iterativeResults.Add(emaIterative.Update(item));
        }
        
        // Calculate batch
        var batchResults = emaBatch.Update(series);
        
        // Compare
        Assert.Equal(iterativeResults.Count, batchResults.Count);
        for (int i = 0; i < iterativeResults.Count; i++)
        {
            Assert.Equal(iterativeResults[i].Value, batchResults[i].Value, 1e-10);
            Assert.Equal(iterativeResults[i].Time, batchResults[i].Time);
        }
    }

    [Fact]
    public void Ema_Result_ImplicitConversionToDouble()
    {
        var ema = new Ema(10);
        ema.Update(new TValue(DateTime.Now, 100));
        
        // This should compile and work because TValue has implicit conversion to double
        double result = ema.Value;
        
        Assert.Equal(100.0, result, 1e-10);
    }
}
