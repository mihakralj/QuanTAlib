namespace QuanTAlib.Tests;

public class UltoscTests
{
    // ============== Constructor & Parameter Validation ==============

    [Fact]
    public void Constructor_InvalidPeriod1_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Ultosc(0, 14, 28));
        Assert.Throws<ArgumentException>(() => new Ultosc(-1, 14, 28));
    }

    [Fact]
    public void Constructor_InvalidPeriod2_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Ultosc(7, 0, 28));
        Assert.Throws<ArgumentException>(() => new Ultosc(7, -1, 28));
    }

    [Fact]
    public void Constructor_InvalidPeriod3_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Ultosc(7, 14, 0));
        Assert.Throws<ArgumentException>(() => new Ultosc(7, 14, -1));
    }

    [Fact]
    public void Constructor_Period1NotLessThanPeriod2_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Ultosc(14, 14, 28));
        Assert.Throws<ArgumentException>(() => new Ultosc(15, 14, 28));
    }

    [Fact]
    public void Constructor_Period2NotLessThanPeriod3_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Ultosc(7, 28, 28));
        Assert.Throws<ArgumentException>(() => new Ultosc(7, 29, 28));
    }

    [Fact]
    public void Constructor_ValidParameters_Succeeds()
    {
        var ultosc = new Ultosc(7, 14, 28);
        Assert.NotNull(ultosc);

        var ultosc2 = new Ultosc(5, 10, 20);
        Assert.NotNull(ultosc2);
    }

    // ============== Basic Functionality ==============

    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var ultosc = new Ultosc(7, 14, 28);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            ultosc.Update(bar);
        }

        Assert.True(double.IsFinite(ultosc.Last.Value));
    }

    [Fact]
    public void Calc_ReturnsValue()
    {
        var ultosc = new Ultosc(7, 14, 28);
        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);

        Assert.Equal(0, ultosc.Last.Value);

        TValue result = ultosc.Update(bar);

        Assert.True(result.Value > 0);
        Assert.Equal(result.Value, ultosc.Last.Value);
    }

    [Fact]
    public void FirstValue_ReturnsValidOscillator()
    {
        var ultosc = new Ultosc(7, 14, 28);
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000);
        // First bar: BP = Close - Low = 105 - 90 = 15
        // TR = High - Low = 110 - 90 = 20
        // Avg = BP/TR = 15/20 = 0.75 for all periods
        // UO = 100 * (4*0.75 + 2*0.75 + 0.75) / 7 = 100 * 5.25/7 = 75

        TValue result = ultosc.Update(bar);

        Assert.Equal(75.0, result.Value, 1e-10);
    }

    [Fact]
    public void Properties_Accessible()
    {
        var ultosc = new Ultosc(7, 14, 28);

        Assert.Equal(0, ultosc.Last.Value);
        Assert.False(ultosc.IsHot);
        Assert.Contains("Ultosc", ultosc.Name, StringComparison.Ordinal);
        Assert.Equal(28, ultosc.WarmupPeriod);

        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        ultosc.Update(bar);

        Assert.NotEqual(0, ultosc.Last.Value);
    }

    // ============== State Management & Bar Correction ==============

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var ultosc = new Ultosc(7, 14, 28);

        var bar1 = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        ultosc.Update(bar1, isNew: true);
        double value1 = ultosc.Last.Value;

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 102, 110, 100, 108, 1000);
        ultosc.Update(bar2, isNew: true);
        double value2 = ultosc.Last.Value;

        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var ultosc = new Ultosc(7, 14, 28);

        var bar1 = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        ultosc.Update(bar1, isNew: true);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 102, 110, 100, 108, 1000);
        ultosc.Update(bar2, isNew: true);
        double beforeUpdate = ultosc.Last.Value;

        var bar2Modified = new TBar(DateTime.UtcNow.AddMinutes(1), 102, 120, 90, 108, 1000);
        ultosc.Update(bar2Modified, isNew: false);
        double afterUpdate = ultosc.Last.Value;

        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var ultosc = new Ultosc(7, 14, 28);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed first 99
        for (int i = 0; i < 99; i++)
        {
            ultosc.Update(bars[i]);
        }

        // Update with 100th point (isNew=true)
        ultosc.Update(bars[99], true);

        // Update with modified 100th point (isNew=false)
        var modifiedBar = new TBar(bars[99].Time, bars[99].Open, bars[99].High + 10.0, bars[99].Low - 10.0, bars[99].Close, bars[99].Volume);
        double val2 = ultosc.Update(modifiedBar, false).Value;

        // Create new instance and feed up to modified
        var ultosc2 = new Ultosc(7, 14, 28);
        for (int i = 0; i < 99; i++)
        {
            ultosc2.Update(bars[i]);
        }
        double val3 = ultosc2.Update(modifiedBar, true).Value;

        Assert.Equal(val3, val2, 1e-9);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var ultosc = new Ultosc(3, 5, 7);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);
        var bars = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed 10 new values
        TBar tenthBar = default;
        for (int i = 0; i < 10; i++)
        {
            tenthBar = bars[i];
            ultosc.Update(tenthBar, isNew: true);
        }

        // Remember state after 10 values
        double stateAfterTen = ultosc.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 10; i < 19; i++)
        {
            ultosc.Update(bars[i], isNew: false);
        }

        // Feed the remembered 10th bar again with isNew=false
        TValue finalResult = ultosc.Update(tenthBar, isNew: false);

        // State should match the original state after 10 values
        Assert.Equal(stateAfterTen, finalResult.Value, 1e-10);
    }

    [Fact]
    public void Reset_Works()
    {
        var ultosc = new Ultosc(7, 14, 28);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars) ultosc.Update(bar);

        double lastVal = ultosc.Last.Value;
        Assert.NotEqual(0, lastVal);

        ultosc.Reset();
        Assert.Equal(0, ultosc.Last.Value);
        Assert.False(ultosc.IsHot);

        // After reset, should accept new values
        ultosc.Update(bars[0]);
        Assert.NotEqual(0, ultosc.Last.Value);
    }

    // ============== Warmup & Convergence ==============

    [Fact]
    public void IsHot_BecomesTrueAfterWarmup()
    {
        var ultosc = new Ultosc(3, 5, 7);

        Assert.False(ultosc.IsHot);

        int steps = 0;
        var baseTime = DateTime.UtcNow;
        while (!ultosc.IsHot && steps < 100)
        {
            var bar = new TBar(baseTime.AddMinutes(steps), 100, 110, 90, 100, 1000);
            ultosc.Update(bar);
            steps++;
        }

        Assert.True(ultosc.IsHot);
        Assert.True(steps > 0);
    }

    [Fact]
    public void WarmupPeriod_IsPositive()
    {
        var ultosc = new Ultosc(7, 14, 28);
        Assert.True(ultosc.WarmupPeriod > 0);
        Assert.Equal(28, ultosc.WarmupPeriod);

        var ultosc2 = new Ultosc(5, 10, 20);
        Assert.Equal(20, ultosc2.WarmupPeriod);
    }

    // ============== NaN/Infinity Handling ==============

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var ultosc = new Ultosc(3, 5, 7);

        var bar1 = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        ultosc.Update(bar1);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 102, 110, 98, 108, 1000);
        ultosc.Update(bar2);

        // Feed bar with NaN values
        var barWithNaN = new TBar(DateTime.UtcNow.AddMinutes(2), double.NaN, 115, 100, 112, 1000);
        var resultAfterNaN = ultosc.Update(barWithNaN);

        // Result should be finite
        Assert.True(double.IsFinite(resultAfterNaN.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var ultosc = new Ultosc(3, 5, 7);

        var bar1 = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        ultosc.Update(bar1);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 102, 110, 98, 108, 1000);
        ultosc.Update(bar2);

        // Feed bar with Infinity
        var barWithInf = new TBar(DateTime.UtcNow.AddMinutes(2), 108, double.PositiveInfinity, 100, 112, 1000);
        var resultAfterInf = ultosc.Update(barWithInf);

        // Result should be finite or infinity (depending on implementation)
        Assert.True(double.IsFinite(resultAfterInf.Value) || double.IsPositiveInfinity(resultAfterInf.Value));
    }

    // ============== Consistency Tests ==============

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        var ultoscIterative = new Ultosc(7, 14, 28);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Calculate iteratively
        var iterativeResults = new TSeries();
        foreach (var bar in bars)
        {
            iterativeResults.Add(ultoscIterative.Update(bar));
        }

        // Calculate batch
        var batchResults = Ultosc.Batch(bars, 7, 14, 28);

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
        var ultosc1 = new Ultosc(7, 14, 28);
        var ultosc2 = new Ultosc(7, 14, 28);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Streaming
        foreach (var bar in bars)
        {
            ultosc1.Update(bar);
        }

        // Batch
        ultosc2.Update(bars);

        Assert.Equal(ultosc1.Last.Value, ultosc2.Last.Value, 1e-10);
    }

    [Fact]
    public void Chainability_Works()
    {
        var ultosc = new Ultosc(7, 14, 28);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var result = ultosc.Update(bars);
        Assert.Equal(50, result.Count);
        Assert.Equal(ultosc.Last.Value, result.Last.Value);
    }

    // ============== Oscillator Range Tests ==============

    [Fact]
    public void Oscillator_ReturnsValueBetween0And100()
    {
        var ultosc = new Ultosc(7, 14, 28);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            var result = ultosc.Update(bar);
            Assert.InRange(result.Value, 0.0, 100.0);
        }
    }

    [Fact]
    public void StrongUptrend_ReturnsHighValues()
    {
        var ultosc = new Ultosc(3, 5, 7);
        var baseTime = DateTime.UtcNow;

        // Create strong uptrend bars where Close is always at High
        for (int i = 0; i < 20; i++)
        {
            double basePrice = 100 + (i * 5); // Rising prices
            var bar = new TBar(baseTime.AddMinutes(i), basePrice, basePrice + 10, basePrice - 2, basePrice + 10, 1000);
            ultosc.Update(bar);
        }

        // In strong uptrend with Close at High, BP/TR should be high
        Assert.True(ultosc.Last.Value > 50);
    }

    [Fact]
    public void StrongDowntrend_ReturnsLowValues()
    {
        var ultosc = new Ultosc(3, 5, 7);
        var baseTime = DateTime.UtcNow;

        // Create strong downtrend bars where Close is always at Low
        for (int i = 0; i < 20; i++)
        {
            double basePrice = 200 - (i * 5); // Falling prices
            var bar = new TBar(baseTime.AddMinutes(i), basePrice, basePrice + 2, basePrice - 10, basePrice - 10, 1000);
            ultosc.Update(bar);
        }

        // In strong downtrend with Close at Low, BP/TR should be low
        Assert.True(ultosc.Last.Value < 50);
    }

    // ============== Static Batch Method ==============

    [Fact]
    public void StaticBatch_Works()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var results = Ultosc.Batch(bars, 7, 14, 28);

        Assert.Equal(50, results.Count);
        Assert.True(double.IsFinite(results.Last.Value));
    }

    // ============== Edge Cases ==============

    [Fact]
    public void SingleBar_ReturnsValidResult()
    {
        var ultosc = new Ultosc(7, 14, 28);
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000);

        var result = ultosc.Update(bar);

        Assert.True(double.IsFinite(result.Value));
        // BP = Close - Low = 105 - 90 = 15
        // TR = High - Low = 110 - 90 = 20
        // Avg = 15/20 = 0.75
        // UO = 100 * (4*0.75 + 2*0.75 + 0.75) / 7 = 75
        Assert.Equal(75.0, result.Value, 1e-10);
    }

    [Fact]
    public void FlatBars_ReturnsFifty()
    {
        var ultosc = new Ultosc(3, 5, 7);

        // All bars have same OHLC values (flat market)
        for (int i = 0; i < 20; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 100, 100, 100, 100, 1000);
            ultosc.Update(bar);
        }

        // For flat bars: BP = 0, TR = 0, so BP/TR = 0/0 handled as 0.5
        // UO = 100 * 0.5 * 7 / 7 = 50
        Assert.Equal(50.0, ultosc.Last.Value, 1e-10);
    }

    [Fact]
    public void CloseAtHigh_ReturnsHundred()
    {
        var ultosc = new Ultosc(3, 5, 7);

        // All bars have Close at High
        for (int i = 0; i < 20; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 100, 110, 90, 110, 1000);
            ultosc.Update(bar);
        }

        // BP = Close - TrueLow = 110 - 90 = 20
        // TR = TrueHigh - TrueLow = 110 - 90 = 20
        // Avg = 20/20 = 1.0
        // UO = 100 * (4*1 + 2*1 + 1) / 7 = 100
        Assert.Equal(100.0, ultosc.Last.Value, 1e-10);
    }

    [Fact]
    public void CloseAtLow_ReturnsZero()
    {
        var ultosc = new Ultosc(3, 5, 7);

        // All bars have Close at Low
        for (int i = 0; i < 20; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 100, 110, 90, 90, 1000);
            ultosc.Update(bar);
        }

        // BP = Close - TrueLow = 90 - 90 = 0
        // TR = TrueHigh - TrueLow = 110 - 90 = 20
        // Avg = 0/20 = 0.0
        // UO = 100 * (4*0 + 2*0 + 0) / 7 = 0
        Assert.Equal(0.0, ultosc.Last.Value, 1e-10);
    }
}
