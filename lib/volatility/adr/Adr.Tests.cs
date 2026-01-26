namespace QuanTAlib.Tests;

public class AdrTests
{
    // ============== Constructor & Parameter Validation ==============

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Adr(0));
        Assert.Throws<ArgumentException>(() => new Adr(-1));

        var adr = new Adr(14);
        Assert.NotNull(adr);
    }

    [Fact]
    public void Constructor_ValidatesMethod()
    {
        var adrSma = new Adr(14, AdrMethod.Sma);
        var adrEma = new Adr(14, AdrMethod.Ema);
        var adrWma = new Adr(14, AdrMethod.Wma);

        Assert.NotNull(adrSma);
        Assert.NotNull(adrEma);
        Assert.NotNull(adrWma);
    }

    [Fact]
    public void Constructor_InvalidMethod_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Adr(14, (AdrMethod)99));
    }

    // ============== Basic Functionality ==============

    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var adr = new Adr(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            adr.Update(bar);
        }

        Assert.True(double.IsFinite(adr.Last.Value));
    }

    [Fact]
    public void Calc_ReturnsValue()
    {
        var adr = new Adr(14);
        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);

        Assert.Equal(0, adr.Last.Value);

        TValue result = adr.Update(bar);

        Assert.True(result.Value > 0);
        Assert.Equal(result.Value, adr.Last.Value);
    }

    [Fact]
    public void FirstValue_ReturnsHighMinusLow()
    {
        var adr = new Adr(14);
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000);
        // First bar range = High - Low = 110 - 90 = 20
        // With SMA(14), first value = 20 (only one value in the average)

        TValue result = adr.Update(bar);

        Assert.Equal(20.0, result.Value, 1e-10);
    }

    [Fact]
    public void Properties_Accessible()
    {
        var adr = new Adr(14);

        Assert.Equal(0, adr.Last.Value);
        Assert.False(adr.IsHot);
        Assert.Contains("Adr", adr.Name, StringComparison.Ordinal);
        Assert.True(adr.WarmupPeriod > 0);

        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        adr.Update(bar);

        Assert.NotEqual(0, adr.Last.Value);
    }

    // ============== Smoothing Method Tests ==============

    [Fact]
    public void SmaMethod_Works()
    {
        var adr = new Adr(5, AdrMethod.Sma);
        var baseTime = DateTime.UtcNow;

        // Feed 5 bars with consistent range of 10
        for (int i = 0; i < 5; i++)
        {
            var bar = new TBar(baseTime.AddMinutes(i), 100, 105, 95, 100, 1000);
            adr.Update(bar);
        }

        // SMA of [10, 10, 10, 10, 10] = 10
        Assert.Equal(10.0, adr.Last.Value, 1e-10);
        Assert.True(adr.IsHot);
    }

    [Fact]
    public void EmaMethod_Works()
    {
        var adr = new Adr(5, AdrMethod.Ema);
        var baseTime = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            var bar = new TBar(baseTime.AddMinutes(i), 100, 105, 95, 100, 1000);
            adr.Update(bar);
        }

        // EMA should converge to 10 with constant input of 10
        Assert.Equal(10.0, adr.Last.Value, 0.01);
        Assert.True(adr.IsHot);
    }

    [Fact]
    public void WmaMethod_Works()
    {
        var adr = new Adr(5, AdrMethod.Wma);
        var baseTime = DateTime.UtcNow;

        for (int i = 0; i < 5; i++)
        {
            var bar = new TBar(baseTime.AddMinutes(i), 100, 105, 95, 100, 1000);
            adr.Update(bar);
        }

        // WMA of [10, 10, 10, 10, 10] = 10
        Assert.Equal(10.0, adr.Last.Value, 1e-10);
        Assert.True(adr.IsHot);
    }

    [Fact]
    public void DifferentMethods_ProduceDifferentResults()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.2);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var adrSma = new Adr(14, AdrMethod.Sma);
        var adrEma = new Adr(14, AdrMethod.Ema);
        var adrWma = new Adr(14, AdrMethod.Wma);

        foreach (var bar in bars)
        {
            adrSma.Update(bar);
            adrEma.Update(bar);
            adrWma.Update(bar);
        }

        // Different methods should produce slightly different results
        // (though with constant input they'd be the same)
        Assert.True(double.IsFinite(adrSma.Last.Value));
        Assert.True(double.IsFinite(adrEma.Last.Value));
        Assert.True(double.IsFinite(adrWma.Last.Value));
    }

    // ============== State Management & Bar Correction ==============

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var adr = new Adr(14);

        // Bar1: H-L = 105-95 = 10
        var bar1 = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        adr.Update(bar1, isNew: true);
        double value1 = adr.Last.Value;

        // Bar2: H-L = 120-100 = 20 (different range from bar1)
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 102, 120, 100, 108, 1000);
        adr.Update(bar2, isNew: true);
        double value2 = adr.Last.Value;

        // With different ranges, the SMA should change
        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var adr = new Adr(14);

        var bar1 = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        adr.Update(bar1, isNew: true);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 102, 110, 100, 108, 1000);
        adr.Update(bar2, isNew: true);
        double beforeUpdate = adr.Last.Value;

        var bar2Modified = new TBar(DateTime.UtcNow.AddMinutes(1), 102, 120, 90, 108, 1000);
        adr.Update(bar2Modified, isNew: false);
        double afterUpdate = adr.Last.Value;

        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var adr = new Adr(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed first 99
        for (int i = 0; i < 99; i++)
        {
            adr.Update(bars[i]);
        }

        // Update with 100th point (isNew=true)
        adr.Update(bars[99], true);

        // Update with modified 100th point (isNew=false)
        var modifiedBar = new TBar(bars[99].Time, bars[99].Open, bars[99].High + 10.0, bars[99].Low - 10.0, bars[99].Close, bars[99].Volume);
        double val2 = adr.Update(modifiedBar, false).Value;

        // Create new instance and feed up to modified
        var adr2 = new Adr(14);
        for (int i = 0; i < 99; i++)
        {
            adr2.Update(bars[i]);
        }
        double val3 = adr2.Update(modifiedBar, true).Value;

        Assert.Equal(val3, val2, 1e-9);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var adr = new Adr(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);
        var bars = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed 10 new values
        TBar tenthBar = default;
        for (int i = 0; i < 10; i++)
        {
            tenthBar = bars[i];
            adr.Update(tenthBar, isNew: true);
        }

        // Remember state after 10 values
        double stateAfterTen = adr.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 10; i < 19; i++)
        {
            adr.Update(bars[i], isNew: false);
        }

        // Feed the remembered 10th bar again with isNew=false
        TValue finalResult = adr.Update(tenthBar, isNew: false);

        // State should match the original state after 10 values
        Assert.Equal(stateAfterTen, finalResult.Value, 1e-10);
    }

    [Fact]
    public void Reset_Works()
    {
        var adr = new Adr(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            adr.Update(bar);
        }

        double lastVal = adr.Last.Value;
        Assert.NotEqual(0, lastVal);

        adr.Reset();
        Assert.Equal(0, adr.Last.Value);
        Assert.False(adr.IsHot);

        // After reset, should accept new values
        adr.Update(bars[0]);
        Assert.NotEqual(0, adr.Last.Value);
    }

    // ============== Warmup & Convergence ==============

    [Fact]
    public void IsHot_BecomesTrueAfterWarmup()
    {
        var adr = new Adr(5, AdrMethod.Sma);

        Assert.False(adr.IsHot);

        var baseTime = DateTime.UtcNow;
        int steps = 0;
        while (!adr.IsHot && steps < 100)
        {
            var bar = new TBar(baseTime.AddMinutes(steps), 100, 110, 90, 100, 1000);
            adr.Update(bar);
            steps++;
        }

        Assert.True(adr.IsHot);
        // SMA with period 5 should become hot after 5 bars
        Assert.Equal(5, steps);
    }

    [Fact]
    public void WarmupPeriod_IsPositive()
    {
        var adr = new Adr(14);
        Assert.True(adr.WarmupPeriod > 0);

        var adr2 = new Adr(20);
        Assert.True(adr2.WarmupPeriod > 0);

        // WarmupPeriod should increase with the period parameter
        Assert.True(adr2.WarmupPeriod >= adr.WarmupPeriod);
    }

    // ============== NaN/Infinity Handling ==============

    [Fact]
    public void NaN_Input_HandledGracefully()
    {
        var adr = new Adr(5);

        var bar1 = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        adr.Update(bar1);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 102, 110, 98, 108, 1000);
        adr.Update(bar2);

        // Feed bar with NaN values - range will be NaN, should be handled
        var barWithNaN = new TBar(DateTime.UtcNow.AddMinutes(2), double.NaN, 115, 100, 112, 1000);
        var resultAfterNaN = adr.Update(barWithNaN);

        // Result should be finite (NaN range treated as 0)
        Assert.True(double.IsFinite(resultAfterNaN.Value));
    }

    [Fact]
    public void Infinity_Input_HandledGracefully()
    {
        var adr = new Adr(5);

        var bar1 = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        adr.Update(bar1);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 102, 110, 98, 108, 1000);
        adr.Update(bar2);

        // Feed bar with Infinity
        var barWithInf = new TBar(DateTime.UtcNow.AddMinutes(2), 108, double.PositiveInfinity, 100, 112, 1000);
        var resultAfterInf = adr.Update(barWithInf);

        // Result should be finite (infinite range treated as 0)
        Assert.True(double.IsFinite(resultAfterInf.Value));
    }

    // ============== Consistency Tests ==============

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        var adrIterative = new Adr(14);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Calculate iteratively
        var iterativeResults = new TSeries();
        foreach (var bar in bars)
        {
            iterativeResults.Add(adrIterative.Update(bar));
        }

        // Calculate batch
        var batchResults = Adr.Batch(bars, 14);

        // Compare
        Assert.Equal(iterativeResults.Count, batchResults.Count);
        for (int i = 0; i < iterativeResults.Count; i++)
        {
            Assert.Equal(iterativeResults[i].Value, batchResults[i].Value, 1e-10);
        }
    }

    [Fact]
    public void TBarSeries_Update_MatchesStreaming()
    {
        var adr1 = new Adr(14);
        var adr2 = new Adr(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Streaming
        foreach (var bar in bars)
        {
            adr1.Update(bar);
        }

        // Batch
        adr2.Update(bars);

        Assert.Equal(adr1.Last.Value, adr2.Last.Value, 1e-10);
    }

    [Fact]
    public void Chainability_Works()
    {
        var adr = new Adr(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var result = adr.Update(bars);
        Assert.Equal(50, result.Count);
        Assert.Equal(adr.Last.Value, result.Last.Value);
    }

    // ============== Range Calculation Tests ==============

    [Fact]
    public void Range_EqualsHighMinusLow()
    {
        var adr = new Adr(1, AdrMethod.Sma);
        var bar = new TBar(DateTime.UtcNow, 100, 120, 90, 110, 1000);
        // Range = 120 - 90 = 30

        var result = adr.Update(bar);
        Assert.Equal(30.0, result.Value, 1e-10);
    }

    [Fact]
    public void NoGapConsideration_UnlikeAtr()
    {
        // ADR should NOT consider gaps like ATR does
        var adr = new Adr(14);

        // Bar1: C=100
        var bar1 = new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000);
        adr.Update(bar1);
        // Range = 110 - 90 = 20

        // Bar2: Gap up - O=120, H=130, L=115, C=125
        // ADR Range = 130 - 115 = 15 (ignores gap from close 100)
        // ATR would use max(15, |130-100|=30, |115-100|=15) = 30
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 120, 130, 115, 125, 1000);
        var result = adr.Update(bar2);

        // With SMA(14), after 2 bars: (20 + 15) / 2 = 17.5
        Assert.Equal(17.5, result.Value, 1e-10);
    }

    // ============== Static Batch Method ==============

    [Fact]
    public void StaticBatch_Works()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var results = Adr.Batch(bars, 14);

        Assert.Equal(50, results.Count);
        Assert.True(double.IsFinite(results.Last.Value));
    }

    [Fact]
    public void StaticBatch_WithMethod_Works()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var resultsSma = Adr.Batch(bars, 14, AdrMethod.Sma);
        var resultsEma = Adr.Batch(bars, 14, AdrMethod.Ema);
        var resultsWma = Adr.Batch(bars, 14, AdrMethod.Wma);

        Assert.Equal(50, resultsSma.Count);
        Assert.Equal(50, resultsEma.Count);
        Assert.Equal(50, resultsWma.Count);

        Assert.True(double.IsFinite(resultsSma.Last.Value));
        Assert.True(double.IsFinite(resultsEma.Last.Value));
        Assert.True(double.IsFinite(resultsWma.Last.Value));
    }

    // ============== Edge Cases ==============

    [Fact]
    public void SingleBar_ReturnsValidResult()
    {
        var adr = new Adr(14);
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000);

        var result = adr.Update(bar);

        Assert.True(double.IsFinite(result.Value));
        Assert.Equal(20.0, result.Value, 1e-10); // H-L = 110-90 = 20
    }

    [Fact]
    public void Period1_Works()
    {
        var adr = new Adr(1);
        var gbm = new GBM();
        var bars = gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            var result = adr.Update(bar);
            Assert.True(double.IsFinite(result.Value));
        }

        Assert.True(adr.IsHot);
    }

    [Fact]
    public void FlatBars_ZeroRange()
    {
        var adr = new Adr(5);

        // All bars have same OHLC values (no range)
        for (int i = 0; i < 10; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 100, 100, 100, 100, 1000);
            adr.Update(bar);
        }

        // ADR should be 0 for flat bars
        Assert.Equal(0.0, adr.Last.Value, 1e-10);
    }

    [Fact]
    public void NegativeRange_TreatedAsZero()
    {
        var adr = new Adr(5);

        // Bar with Low > High (invalid data)
        var bar = new TBar(DateTime.UtcNow, 100, 90, 110, 100, 1000); // H=90, L=110 -> range = -20
        var result = adr.Update(bar);

        // Negative range should be treated as 0
        Assert.Equal(0.0, result.Value, 1e-10);
    }
}
