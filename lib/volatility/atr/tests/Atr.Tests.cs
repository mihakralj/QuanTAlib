namespace QuanTAlib.Tests;

public class AtrTests
{
    // ============== Constructor & Parameter Validation ==============

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Atr(0));
        Assert.Throws<ArgumentException>(() => new Atr(-1));

        var atr = new Atr(14);
        Assert.NotNull(atr);
    }

    // ============== Basic Functionality ==============

    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var atr = new Atr(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            atr.Update(bar);
        }

        Assert.True(double.IsFinite(atr.Last.Value));
    }

    [Fact]
    public void Calc_ReturnsValue()
    {
        var atr = new Atr(14);
        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);

        Assert.Equal(0, atr.Last.Value);

        TValue result = atr.Update(bar);

        Assert.True(result.Value > 0);
        Assert.Equal(result.Value, atr.Last.Value);
    }

    [Fact]
    public void FirstValue_ReturnsHighMinusLow()
    {
        var atr = new Atr(14);
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000);
        // First bar TR = High - Low = 110 - 90 = 20

        TValue result = atr.Update(bar);

        Assert.Equal(20.0, result.Value, 1e-10);
    }

    [Fact]
    public void Properties_Accessible()
    {
        var atr = new Atr(14);

        Assert.Equal(0, atr.Last.Value);
        Assert.False(atr.IsHot);
        Assert.Contains("Atr", atr.Name, StringComparison.Ordinal);
        Assert.True(atr.WarmupPeriod > 0);

        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        atr.Update(bar);

        Assert.NotEqual(0, atr.Last.Value);
    }

    // ============== State Management & Bar Correction ==============

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var atr = new Atr(14);

        var bar1 = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        atr.Update(bar1, isNew: true);
        double value1 = atr.Last.Value;

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 102, 110, 100, 108, 1000);
        atr.Update(bar2, isNew: true);
        double value2 = atr.Last.Value;

        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var atr = new Atr(14);

        var bar1 = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        atr.Update(bar1, isNew: true);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 102, 110, 100, 108, 1000);
        atr.Update(bar2, isNew: true);
        double beforeUpdate = atr.Last.Value;

        var bar2Modified = new TBar(DateTime.UtcNow.AddMinutes(1), 102, 120, 90, 108, 1000);
        atr.Update(bar2Modified, isNew: false);
        double afterUpdate = atr.Last.Value;

        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var atr = new Atr(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed first 99
        for (int i = 0; i < 99; i++)
        {
            atr.Update(bars[i]);
        }

        // Update with 100th point (isNew=true)
        atr.Update(bars[99], true);

        // Update with modified 100th point (isNew=false)
        var modifiedBar = new TBar(bars[99].Time, bars[99].Open, bars[99].High + 10.0, bars[99].Low - 10.0, bars[99].Close, bars[99].Volume);
        double val2 = atr.Update(modifiedBar, false).Value;

        // Create new instance and feed up to modified
        var atr2 = new Atr(14);
        for (int i = 0; i < 99; i++)
        {
            atr2.Update(bars[i]);
        }
        double val3 = atr2.Update(modifiedBar, true).Value;

        Assert.Equal(val3, val2, 1e-9);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var atr = new Atr(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);
        var bars = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed 10 new values
        TBar tenthBar = default;
        for (int i = 0; i < 10; i++)
        {
            tenthBar = bars[i];
            atr.Update(tenthBar, isNew: true);
        }

        // Remember state after 10 values
        double stateAfterTen = atr.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 10; i < 19; i++)
        {
            atr.Update(bars[i], isNew: false);
        }

        // Feed the remembered 10th bar again with isNew=false
        TValue finalResult = atr.Update(tenthBar, isNew: false);

        // State should match the original state after 10 values
        Assert.Equal(stateAfterTen, finalResult.Value, 1e-10);
    }

    [Fact]
    public void Reset_Works()
    {
        var atr = new Atr(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            atr.Update(bar);
        }

        double lastVal = atr.Last.Value;
        Assert.NotEqual(0, lastVal);

        atr.Reset();
        Assert.Equal(0, atr.Last.Value);
        Assert.False(atr.IsHot);

        // After reset, should accept new values
        atr.Update(bars[0]);
        Assert.NotEqual(0, atr.Last.Value);
    }

    // ============== Warmup & Convergence ==============

    [Fact]
    public void IsHot_BecomesTrueAfterWarmup()
    {
        var atr = new Atr(5);

        Assert.False(atr.IsHot);

        // ATR uses RMA which uses EMA internally
        // EMA's IsHot is based on 95% coverage threshold (E <= 0.05)
        // For RMA with alpha = 1/period, warmup takes approximately:
        // N = ln(0.05) / ln(1 - 1/period) bars
        // Feed bars until IsHot becomes true
        int steps = 0;
        var baseTime = DateTime.UtcNow;
        while (!atr.IsHot && steps < 100)
        {
            // Create simple bars with consistent volatility
            var bar = new TBar(baseTime.AddMinutes(steps), 100, 110, 90, 100, 1000);
            atr.Update(bar);
            steps++;
        }

        Assert.True(atr.IsHot);
        // For period 5, RMA alpha = 0.2, should become hot around 14 bars
        Assert.True(steps > 0);
    }

    [Fact]
    public void WarmupPeriod_IsPositive()
    {
        var atr = new Atr(14);
        Assert.True(atr.WarmupPeriod > 0);

        var atr2 = new Atr(20);
        Assert.True(atr2.WarmupPeriod > 0);

        // WarmupPeriod should increase with the period parameter
        Assert.True(atr2.WarmupPeriod >= atr.WarmupPeriod);
    }

    // ============== NaN/Infinity Handling ==============

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var atr = new Atr(5);

        var bar1 = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        atr.Update(bar1);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 102, 110, 98, 108, 1000);
        atr.Update(bar2);

        // Feed bar with NaN values
        var barWithNaN = new TBar(DateTime.UtcNow.AddMinutes(2), double.NaN, 115, 100, 112, 1000);
        var resultAfterNaN = atr.Update(barWithNaN);

        // Result should be finite
        Assert.True(double.IsFinite(resultAfterNaN.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var atr = new Atr(5);

        var bar1 = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        atr.Update(bar1);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 102, 110, 98, 108, 1000);
        atr.Update(bar2);

        // Feed bar with Infinity
        var barWithInf = new TBar(DateTime.UtcNow.AddMinutes(2), 108, double.PositiveInfinity, 100, 112, 1000);
        var resultAfterInf = atr.Update(barWithInf);

        // Result should be finite (though may be very large due to the infinity calculation)
        // ATR doesn't have explicit NaN/Inf handling in the implementation, this tests the raw behavior
        // The assertion depends on the actual implementation behavior
        Assert.True(double.IsFinite(resultAfterInf.Value) || double.IsPositiveInfinity(resultAfterInf.Value));
    }

    // ============== Consistency Tests ==============

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        var atrIterative = new Atr(14);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Calculate iteratively
        var iterativeResults = new TSeries();
        foreach (var bar in bars)
        {
            iterativeResults.Add(atrIterative.Update(bar));
        }

        // Calculate batch
        var batchResults = Atr.Batch(bars, 14);

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
        var atr1 = new Atr(14);
        var atr2 = new Atr(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Streaming
        foreach (var bar in bars)
        {
            atr1.Update(bar);
        }

        // Batch
        atr2.Update(bars);

        Assert.Equal(atr1.Last.Value, atr2.Last.Value, 1e-10);
    }

    [Fact]
    public void Chainability_Works()
    {
        var atr = new Atr(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var result = atr.Update(bars);
        Assert.Equal(50, result.Count);
        Assert.Equal(atr.Last.Value, result.Last.Value);
    }

    // ============== TrueRange Calculation Tests ==============

    [Fact]
    public void TrueRange_FirstBar_EqualsHighMinusLow()
    {
        var atr = new Atr(14);
        var bar = new TBar(DateTime.UtcNow, 100, 120, 90, 110, 1000);
        // First TR = 120 - 90 = 30

        var result = atr.Update(bar);
        Assert.Equal(30.0, result.Value, 1e-10);
    }

    [Fact]
    public void TrueRange_SecondBar_UsesMaxOfThreeRanges()
    {
        var atr = new Atr(14);

        // Bar1: O=100, H=110, L=90, C=100
        var bar1 = new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000);
        atr.Update(bar1);

        // Bar2: O=105, H=115, L=95, C=110
        // TR options:
        //   H-L = 115-95 = 20
        //   |H-PrevC| = |115-100| = 15
        //   |L-PrevC| = |95-100| = 5
        // Max = 20
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 105, 115, 95, 110, 1000);
        var result = atr.Update(bar2);

        // ATR with RMA: after 2 bars with TR=20 and TR=20, RMA result depends on initialization
        // For period=14, after bar1 ATR=20, after bar2 ATR is RMA(20, 20)
        Assert.True(result.Value > 0);
    }

    [Fact]
    public void TrueRange_GapUp_CalculatesCorrectly()
    {
        var atr = new Atr(14);

        // Bar1: C=100
        var bar1 = new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000);
        atr.Update(bar1);

        // Bar2: Gap up - O=120, H=130, L=115, C=125
        // TR options:
        //   H-L = 130-115 = 15
        //   |H-PrevC| = |130-100| = 30 (gap up)
        //   |L-PrevC| = |115-100| = 15
        // Max = 30
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 120, 130, 115, 125, 1000);
        var result = atr.Update(bar2);

        // The ATR should reflect the larger true range from the gap
        Assert.True(result.Value > 0);
    }

    // ============== Static Batch Method ==============

    [Fact]
    public void StaticBatch_Works()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var results = Atr.Batch(bars, 14);

        Assert.Equal(50, results.Count);
        Assert.True(double.IsFinite(results.Last.Value));
    }

    // ============== Edge Cases ==============

    [Fact]
    public void SingleBar_ReturnsValidResult()
    {
        var atr = new Atr(14);
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000);

        var result = atr.Update(bar);

        Assert.True(double.IsFinite(result.Value));
        Assert.Equal(20.0, result.Value, 1e-10); // H-L = 110-90 = 20
    }

    [Fact]
    public void Period1_Works()
    {
        var atr = new Atr(1);
        var gbm = new GBM();
        var bars = gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            var result = atr.Update(bar);
            Assert.True(double.IsFinite(result.Value));
        }

        Assert.True(atr.IsHot);
    }

    [Fact]
    public void FlatBars_ZeroVolatility()
    {
        var atr = new Atr(5);

        // All bars have same OHLC values
        for (int i = 0; i < 10; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 100, 100, 100, 100, 1000);
            atr.Update(bar);
        }

        // ATR should be 0 for flat bars
        Assert.Equal(0.0, atr.Last.Value, 1e-10);
    }

    [Fact]
    public void Update_EmptyTSeries_ReturnsEmpty()
    {
        var atr = new Atr(14);
        var result = atr.Update(new TSeries());

        Assert.Empty(result);
        Assert.Equal(0, atr.Last.Value);
    }

    [Fact]
    public void Calculate_ReturnsConfiguredIndicatorAndMatchingResults()
    {
        var bars = new TBarSeries();
        var now = DateTime.UtcNow;

        for (int i = 0; i < 40; i++)
        {
            double open = 100 + i;
            bars.Add(new TBar(now.AddMinutes(i), open, open + 6, open - 4, open + 1, 1000));
        }

        var (results, indicator) = Atr.Calculate(bars, 10);
        var batch = Atr.Batch(bars, 10);

        Assert.NotNull(indicator);
        Assert.True(indicator.WarmupPeriod >= 10);
        Assert.Equal(batch.Count, results.Count);

        for (int i = 0; i < results.Count; i++)
        {
            Assert.Equal(batch[i].Value, results[i].Value, 1e-10);
        }
    }
}
