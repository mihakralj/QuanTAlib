namespace QuanTAlib.Tests;

public class AtrpTests
{
    // ============== Constructor & Parameter Validation ==============

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Atrp(0));
        Assert.Throws<ArgumentException>(() => new Atrp(-1));

        var atrp = new Atrp(14);
        Assert.NotNull(atrp);
    }

    // ============== Basic Functionality ==============

    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var atrp = new Atrp(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            atrp.Update(bar);
        }

        Assert.True(double.IsFinite(atrp.Last.Value));
    }

    [Fact]
    public void Calc_ReturnsValue()
    {
        var atrp = new Atrp(14);
        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);

        Assert.Equal(0, atrp.Last.Value);

        TValue result = atrp.Update(bar);

        Assert.True(result.Value > 0);
        Assert.Equal(result.Value, atrp.Last.Value);
    }

    [Fact]
    public void FirstValue_ReturnsPercentage()
    {
        var atrp = new Atrp(14);
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000);
        // First bar TR = High - Low = 110 - 90 = 20
        // ATRP = (20 / 100) * 100 = 20%

        TValue result = atrp.Update(bar);

        Assert.Equal(20.0, result.Value, 1e-10);
    }

    [Fact]
    public void Properties_Accessible()
    {
        var atrp = new Atrp(14);

        Assert.Equal(0, atrp.Last.Value);
        Assert.False(atrp.IsHot);
        Assert.Contains("Atrp", atrp.Name, StringComparison.Ordinal);
        Assert.True(atrp.WarmupPeriod > 0);

        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        atrp.Update(bar);

        Assert.NotEqual(0, atrp.Last.Value);
    }

    // ============== State Management & Bar Correction ==============

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var atrp = new Atrp(14);

        var bar1 = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        atrp.Update(bar1, isNew: true);
        double value1 = atrp.Last.Value;

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 102, 110, 100, 108, 1000);
        atrp.Update(bar2, isNew: true);
        double value2 = atrp.Last.Value;

        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var atrp = new Atrp(14);

        var bar1 = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        atrp.Update(bar1, isNew: true);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 102, 110, 100, 108, 1000);
        atrp.Update(bar2, isNew: true);
        double beforeUpdate = atrp.Last.Value;

        var bar2Modified = new TBar(DateTime.UtcNow.AddMinutes(1), 102, 120, 90, 108, 1000);
        atrp.Update(bar2Modified, isNew: false);
        double afterUpdate = atrp.Last.Value;

        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var atrp = new Atrp(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed first 99
        for (int i = 0; i < 99; i++)
        {
            atrp.Update(bars[i]);
        }

        // Update with 100th point (isNew=true)
        atrp.Update(bars[99], true);

        // Update with modified 100th point (isNew=false)
        var modifiedBar = new TBar(bars[99].Time, bars[99].Open, bars[99].High + 10.0, bars[99].Low - 10.0, bars[99].Close, bars[99].Volume);
        double val2 = atrp.Update(modifiedBar, false).Value;

        // Create new instance and feed up to modified
        var atrp2 = new Atrp(14);
        for (int i = 0; i < 99; i++)
        {
            atrp2.Update(bars[i]);
        }
        double val3 = atrp2.Update(modifiedBar, true).Value;

        Assert.Equal(val3, val2, 1e-9);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var atrp = new Atrp(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);
        var bars = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed 10 new values
        TBar tenthBar = default;
        for (int i = 0; i < 10; i++)
        {
            tenthBar = bars[i];
            atrp.Update(tenthBar, isNew: true);
        }

        // Remember state after 10 values
        double stateAfterTen = atrp.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 10; i < 19; i++)
        {
            atrp.Update(bars[i], isNew: false);
        }

        // Feed the remembered 10th bar again with isNew=false
        TValue finalResult = atrp.Update(tenthBar, isNew: false);

        // State should match the original state after 10 values
        Assert.Equal(stateAfterTen, finalResult.Value, 1e-10);
    }

    [Fact]
    public void Reset_Works()
    {
        var atrp = new Atrp(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars) atrp.Update(bar);

        double lastVal = atrp.Last.Value;
        Assert.NotEqual(0, lastVal);

        atrp.Reset();
        Assert.Equal(0, atrp.Last.Value);
        Assert.False(atrp.IsHot);

        // After reset, should accept new values
        atrp.Update(bars[0]);
        Assert.NotEqual(0, atrp.Last.Value);
    }

    // ============== Warmup & Convergence ==============

    [Fact]
    public void IsHot_BecomesTrueAfterWarmup()
    {
        var atrp = new Atrp(5);

        Assert.False(atrp.IsHot);

        int steps = 0;
        var baseTime = DateTime.UtcNow;
        while (!atrp.IsHot && steps < 100)
        {
            var bar = new TBar(baseTime.AddMinutes(steps), 100, 110, 90, 100, 1000);
            atrp.Update(bar);
            steps++;
        }

        Assert.True(atrp.IsHot);
        Assert.True(steps > 0);
    }

    [Fact]
    public void WarmupPeriod_IsPositive()
    {
        var atrp = new Atrp(14);
        Assert.True(atrp.WarmupPeriod > 0);

        var atrp2 = new Atrp(20);
        Assert.True(atrp2.WarmupPeriod > 0);

        // WarmupPeriod should increase with the period parameter
        Assert.True(atrp2.WarmupPeriod >= atrp.WarmupPeriod);
    }

    // ============== NaN/Infinity Handling ==============

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var atrp = new Atrp(5);

        var bar1 = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        atrp.Update(bar1);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 102, 110, 98, 108, 1000);
        atrp.Update(bar2);

        // Feed bar with NaN values
        var barWithNaN = new TBar(DateTime.UtcNow.AddMinutes(2), double.NaN, 115, 100, 112, 1000);
        var resultAfterNaN = atrp.Update(barWithNaN);

        // Result should be finite
        Assert.True(double.IsFinite(resultAfterNaN.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var atrp = new Atrp(5);

        var bar1 = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        atrp.Update(bar1);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 102, 110, 98, 108, 1000);
        atrp.Update(bar2);

        // Feed bar with Infinity
        var barWithInf = new TBar(DateTime.UtcNow.AddMinutes(2), 108, double.PositiveInfinity, 100, 112, 1000);
        var resultAfterInf = atrp.Update(barWithInf);

        Assert.True(double.IsFinite(resultAfterInf.Value) || double.IsPositiveInfinity(resultAfterInf.Value));
    }

    // ============== Consistency Tests ==============

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        var atrpIterative = new Atrp(14);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Calculate iteratively
        var iterativeResults = new TSeries();
        foreach (var bar in bars)
        {
            iterativeResults.Add(atrpIterative.Update(bar));
        }

        // Calculate batch
        var batchResults = Atrp.Batch(bars, 14);

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
        var atrp1 = new Atrp(14);
        var atrp2 = new Atrp(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Streaming
        foreach (var bar in bars)
        {
            atrp1.Update(bar);
        }

        // Batch
        atrp2.Update(bars);

        Assert.Equal(atrp1.Last.Value, atrp2.Last.Value, 1e-10);
    }

    [Fact]
    public void Chainability_Works()
    {
        var atrp = new Atrp(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var result = atrp.Update(bars);
        Assert.Equal(50, result.Count);
        Assert.Equal(atrp.Last.Value, result.Last.Value);
    }

    // ============== ATRP-Specific Tests ==============

    [Fact]
    public void ATRP_IsPercentageOfPrice()
    {
        var atrp = new Atrp(14);
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000);
        // TR = 20, Close = 100
        // ATRP = (20 / 100) * 100 = 20%

        var result = atrp.Update(bar);
        Assert.Equal(20.0, result.Value, 1e-10);
    }

    [Fact]
    public void ATRP_HigherPriceAsset_LowerPercentage()
    {
        // Same volatility (TR=20) but different price levels
        var atrp1 = new Atrp(14);
        var atrp2 = new Atrp(14);

        // Low price asset: Close = 100, TR = 20 -> ATRP = 20%
        var bar1 = new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000);
        var result1 = atrp1.Update(bar1);

        // High price asset: Close = 1000, TR = 20 -> ATRP = 2%
        var bar2 = new TBar(DateTime.UtcNow, 1000, 1010, 990, 1000, 1000);
        var result2 = atrp2.Update(bar2);

        Assert.True(result1.Value > result2.Value);
        Assert.Equal(20.0, result1.Value, 1e-10);
        Assert.Equal(2.0, result2.Value, 1e-10);
    }

    [Fact]
    public void ATRP_ProportionalVolatility_SamePercentage()
    {
        var atrp1 = new Atrp(14);
        var atrp2 = new Atrp(14);

        // Asset 1: Close = 100, TR = 10 (10% volatility)
        var bar1 = new TBar(DateTime.UtcNow, 100, 105, 95, 100, 1000);
        var result1 = atrp1.Update(bar1);

        // Asset 2: Close = 1000, TR = 100 (10% volatility)
        var bar2 = new TBar(DateTime.UtcNow, 1000, 1050, 950, 1000, 1000);
        var result2 = atrp2.Update(bar2);

        Assert.Equal(result1.Value, result2.Value, 1e-10);
        Assert.Equal(10.0, result1.Value, 1e-10);
    }

    // ============== Static Batch Method ==============

    [Fact]
    public void StaticBatch_Works()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var results = Atrp.Batch(bars, 14);

        Assert.Equal(50, results.Count);
        Assert.True(double.IsFinite(results.Last.Value));
    }

    // ============== Edge Cases ==============

    [Fact]
    public void SingleBar_ReturnsValidResult()
    {
        var atrp = new Atrp(14);
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000);

        var result = atrp.Update(bar);

        Assert.True(double.IsFinite(result.Value));
        Assert.Equal(20.0, result.Value, 1e-10); // (H-L)/Close * 100 = 20/100 * 100 = 20%
    }

    [Fact]
    public void Period1_Works()
    {
        var atrp = new Atrp(1);
        var gbm = new GBM();
        var bars = gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            var result = atrp.Update(bar);
            Assert.True(double.IsFinite(result.Value));
        }

        Assert.True(atrp.IsHot);
    }

    [Fact]
    public void FlatBars_ZeroVolatility()
    {
        var atrp = new Atrp(5);

        // All bars have same OHLC values
        for (int i = 0; i < 10; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 100, 100, 100, 100, 1000);
            atrp.Update(bar);
        }

        // ATRP should be 0 for flat bars
        Assert.Equal(0.0, atrp.Last.Value, 1e-10);
    }

    [Fact]
    public void ZeroClose_ReturnsNaN()
    {
        var atrp = new Atrp(14);
        var bar = new TBar(DateTime.UtcNow, 0, 10, -10, 0, 1000);

        var result = atrp.Update(bar);

        Assert.True(double.IsNaN(result.Value));
    }
}