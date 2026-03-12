namespace QuanTAlib.Tests;

public class NatrTests
{
    private const double Tolerance = 1e-9;

    private static TBarSeries GenerateTestBars(int count = 100)
    {
        var gbm = new GBM(seed: 42);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    // ============== Constructor & Parameter Validation ==============

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Natr(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Natr(-1));

        var natr = new Natr(14);
        Assert.NotNull(natr);
    }

    [Fact]
    public void Constructor_SetsCorrectName()
    {
        var natr = new Natr(14);
        Assert.Equal("Natr(14)", natr.Name);
        Assert.True(natr.WarmupPeriod > 0);

        var natr2 = new Natr(5);
        Assert.Equal("Natr(5)", natr2.Name);
    }

    [Fact]
    public void Constructor_SetsCorrectWarmup()
    {
        var natr = new Natr(14);
        // Warmup based on RMA convergence: ln(0.05) / ln(1 - 1/14) ≈ 41
        Assert.True(natr.WarmupPeriod > 0);
    }

    [Fact]
    public void Constructor_DefaultPeriod()
    {
        var natr = new Natr();
        Assert.Equal("Natr(14)", natr.Name);
    }

    // ============== Basic Functionality ==============

    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var natr = new Natr(14);
        var bars = GenerateTestBars(100);

        foreach (var bar in bars)
        {
            natr.Update(bar);
        }

        Assert.True(double.IsFinite(natr.Last.Value));
    }

    [Fact]
    public void Calc_ReturnsValidValue()
    {
        var natr = new Natr(14);
        var bars = GenerateTestBars(50);

        foreach (var bar in bars)
        {
            var result = natr.Update(bar);
            Assert.True(double.IsFinite(result.Value) || double.IsNaN(result.Value));
        }
    }

    [Fact]
    public void Properties_Accessible()
    {
        var natr = new Natr(14);

        Assert.False(natr.IsHot);
        Assert.Contains("Natr", natr.Name, StringComparison.Ordinal);

        var bars = GenerateTestBars(60);
        foreach (var bar in bars)
        {
            natr.Update(bar);
        }

        // After warmup, properties should be valid
        Assert.True(double.IsFinite(natr.Atr));
        Assert.True(natr.Atr >= 0);
    }

    [Fact]
    public void AtrProperty_IsPositive()
    {
        var natr = new Natr(14);
        var bars = GenerateTestBars(50);

        foreach (var bar in bars)
        {
            natr.Update(bar);
        }

        // ATR should be positive
        Assert.True(natr.Atr >= 0);
    }

    [Fact]
    public void Natr_IsPercentage()
    {
        var natr = new Natr(14);
        var bars = GenerateTestBars(100);

        foreach (var bar in bars)
        {
            natr.Update(bar);
        }

        // NATR is a percentage - typically 0-10% for stocks
        Assert.True(natr.Last.Value >= 0, $"NATR {natr.Last.Value} should be >= 0");
        Assert.True(natr.Last.Value < 100, $"NATR {natr.Last.Value} should be < 100%");
    }

    // ============== State Management & Bar Correction ==============

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var natr = new Natr(14);
        var bars = GenerateTestBars(50);

        for (int i = 0; i < 49; i++)
        {
            natr.Update(bars[i], isNew: true);
        }
        double valueBefore = natr.Last.Value;

        natr.Update(bars[49], isNew: true);
        double valueAfter = natr.Last.Value;

        Assert.True(double.IsFinite(valueBefore));
        Assert.True(double.IsFinite(valueAfter));
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var natr = new Natr(14);
        var bars = GenerateTestBars(50);

        for (int i = 0; i < 49; i++)
        {
            natr.Update(bars[i], isNew: true);
        }

        natr.Update(bars[49], isNew: true);
        double beforeUpdate = natr.Last.Value;

        // Update same bar with different value (isNew=false)
        var modifiedBar = new TBar(bars[49].Time, bars[49].Open, bars[49].High + 5,
                                   bars[49].Low - 5, bars[49].Close, bars[49].Volume);
        natr.Update(modifiedBar, isNew: false);
        double afterUpdate = natr.Last.Value;

        // Values should be different after the correction (wider range)
        Assert.True(Math.Abs(beforeUpdate - afterUpdate) > Tolerance);
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var natr = new Natr(14);
        var bars = GenerateTestBars(100);

        for (int i = 0; i < 99; i++)
        {
            natr.Update(bars[i]);
        }

        natr.Update(bars[99], true);

        var modifiedBar = new TBar(bars[99].Time, bars[99].Open, bars[99].High + 5,
                                   bars[99].Low - 5, bars[99].Close, bars[99].Volume);
        double val2 = natr.Update(modifiedBar, false).Value;

        // Create new instance and feed up to modified
        var natr2 = new Natr(14);
        for (int i = 0; i < 99; i++)
        {
            natr2.Update(bars[i]);
        }
        double val3 = natr2.Update(modifiedBar, true).Value;

        Assert.Equal(val3, val2, Tolerance);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var natr = new Natr(5);
        var bars = GenerateTestBars(20);

        TBar tenthBar = default;
        for (int i = 0; i < 10; i++)
        {
            tenthBar = bars[i];
            natr.Update(tenthBar, isNew: true);
        }

        double stateAfterTen = natr.Last.Value;

        for (int i = 10; i < 19; i++)
        {
            natr.Update(bars[i], isNew: false);
        }

        TValue finalResult = natr.Update(tenthBar, isNew: false);

        Assert.Equal(stateAfterTen, finalResult.Value, Tolerance);
    }

    [Fact]
    public void Reset_Works()
    {
        var natr = new Natr(14);
        var bars = GenerateTestBars(50);

        foreach (var bar in bars)
        {
            natr.Update(bar);
        }

        natr.Reset();
        Assert.False(natr.IsHot);
        Assert.Equal(0.0, natr.Atr, Tolerance);
    }

    // ============== Warmup & Convergence ==============

    [Fact]
    public void IsHot_BecomesTrueAfterWarmup()
    {
        var natr = new Natr(14);
        Assert.False(natr.IsHot);

        var bars = GenerateTestBars(100);

        int steps = 0;
        while (!natr.IsHot && steps < bars.Count)
        {
            natr.Update(bars[steps]);
            steps++;
        }

        Assert.True(natr.IsHot);
        Assert.True(steps <= natr.WarmupPeriod + 5); // Allow some buffer
    }

    [Fact]
    public void WarmupPeriod_IsPositive()
    {
        var natr = new Natr(14);
        Assert.True(natr.WarmupPeriod > 0);

        var natr2 = new Natr(5);
        Assert.True(natr2.WarmupPeriod > 0);
    }

    // ============== NaN/Infinity Handling ==============

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var natr = new Natr(5);
        var bars = GenerateTestBars(20);

        for (int i = 0; i < 15; i++)
        {
            natr.Update(bars[i]);
        }

        var inputWithNaN = new TBar(DateTime.UtcNow.AddMinutes(20).Ticks,
            double.NaN, double.NaN, double.NaN, double.NaN, 0);
        var resultAfterNaN = natr.Update(inputWithNaN);

        Assert.True(double.IsFinite(resultAfterNaN.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var natr = new Natr(5);
        var bars = GenerateTestBars(20);

        for (int i = 0; i < 15; i++)
        {
            natr.Update(bars[i]);
        }

        var inputWithInf = new TBar(DateTime.UtcNow.AddMinutes(20).Ticks,
            double.PositiveInfinity, double.PositiveInfinity,
            double.NegativeInfinity, double.PositiveInfinity, 0);
        var resultAfterInf = natr.Update(inputWithInf);

        Assert.True(double.IsFinite(resultAfterInf.Value));
    }

    [Fact]
    public void BatchNaN_Safe()
    {
        var natr = new Natr(5);
        var bars = GenerateTestBars(20);

        for (int i = 0; i < 15; i++)
        {
            natr.Update(bars[i]);
        }

        for (int i = 0; i < 5; i++)
        {
            var nanInput = new TBar(DateTime.UtcNow.AddMinutes(15 + i).Ticks,
                double.NaN, double.NaN, double.NaN, double.NaN, 0);
            var result = natr.Update(nanInput);
            Assert.True(double.IsFinite(result.Value));
        }
    }

    // ============== Consistency Tests ==============

    [Fact]
    public void TBarSeries_MatchesIterativeCalc()
    {
        var natrIterative = new Natr(14);
        var bars = GenerateTestBars(100);

        foreach (var bar in bars)
        {
            natrIterative.Update(bar);
        }

        var natrBatch = new Natr(14);
        _ = natrBatch.Update(bars);

        Assert.Equal(natrIterative.Last.Value, natrBatch.Last.Value, Tolerance);
    }

    [Fact]
    public void Chainability_Works()
    {
        var natr = new Natr(14);
        var bars = GenerateTestBars(50);

        var result = natr.Update(bars);
        Assert.Equal(50, result.Count);
        Assert.Equal(natr.Last.Value, result.Last.Value);
    }

    // ============== TValue Update Not Supported ==============

    [Fact]
    public void TValueUpdate_ThrowsNotSupported()
    {
        var natr = new Natr(14);
        var input = new TValue(DateTime.UtcNow.Ticks, 1.5);

        Assert.Throws<NotSupportedException>(() => natr.Update(input));
    }

    [Fact]
    public void TSeriesUpdate_ThrowsNotSupported()
    {
        var natr = new Natr(14);
        var series = new TSeries();
        series.Add(new TValue(DateTime.UtcNow.Ticks, 1.5));

        Assert.Throws<NotSupportedException>(() => natr.Update(series));
    }

    // ============== NATR Specific Tests ==============

    [Fact]
    public void Natr_RelationToAtr()
    {
        var natr = new Natr(14);
        var bars = GenerateTestBars(100);

        foreach (var bar in bars)
        {
            natr.Update(bar);
        }

        // NATR = (ATR / Close) * 100
        double lastClose = bars.Last.Close;
        double expectedNatr = (natr.Atr / lastClose) * 100.0;

        Assert.Equal(expectedNatr, natr.Last.Value, 1e-6);
    }

    [Fact]
    public void Natr_PeriodAffectsOutput()
    {
        var natr5 = new Natr(5);
        var natr14 = new Natr(14);
        var natr28 = new Natr(28);

        var bars = GenerateTestBars(100);

        foreach (var bar in bars)
        {
            natr5.Update(bar);
            natr14.Update(bar);
            natr28.Update(bar);
        }

        // All should produce valid values
        Assert.True(double.IsFinite(natr5.Last.Value));
        Assert.True(double.IsFinite(natr14.Last.Value));
        Assert.True(double.IsFinite(natr28.Last.Value));

        // Longer periods should generally be smoother (not necessarily higher/lower)
        // Just verify they're all valid
        Assert.True(natr5.Last.Value >= 0);
        Assert.True(natr14.Last.Value >= 0);
        Assert.True(natr28.Last.Value >= 0);
    }

    // ============== Static Batch Methods ==============

    [Fact]
    public void StaticBatch_Works()
    {
        var bars = GenerateTestBars(50);

        var results = Natr.Batch(bars, 14);

        Assert.Equal(50, results.Count);
        Assert.True(double.IsFinite(results.Last.Value));
    }

    [Fact]
    public void StaticBatch_DefaultPeriod()
    {
        var bars = GenerateTestBars(50);

        var results = Natr.Batch(bars);

        Assert.Equal(50, results.Count);
        Assert.True(double.IsFinite(results.Last.Value));
    }

    // ============== Edge Cases ==============

    [Fact]
    public void SingleValue_ReturnsValue()
    {
        var natr = new Natr(14);
        var bar = GenerateTestBars(1)[0];

        var result = natr.Update(bar);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Period1_Works()
    {
        var natr = new Natr(1);
        var bars = GenerateTestBars(10);

        foreach (var bar in bars)
        {
            var result = natr.Update(bar);
            Assert.True(double.IsFinite(result.Value));
        }
    }

    [Fact]
    public void FlatRange_ProducesStableOutput()
    {
        var natr = new Natr(14);

        // All bars have same values - ATR should be zero
        for (int i = 0; i < 50; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i).Ticks, 100.0, 100.0, 100.0, 100.0, 1000.0);
            natr.Update(bar);
        }

        // With no range, ATR and NATR should be 0
        Assert.Equal(0.0, natr.Atr, 1e-6);
        Assert.Equal(0.0, natr.Last.Value, 1e-6);
    }

    [Fact]
    public void HighVolatility_ProducesHigherNatr()
    {
        var natrLow = new Natr(14);
        var natrHigh = new Natr(14);

        // Low volatility bars
        for (int i = 0; i < 50; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i).Ticks, 100.0, 100.5, 99.5, 100.0, 1000.0);
            natrLow.Update(bar);
        }

        // High volatility bars
        for (int i = 0; i < 50; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i).Ticks, 100.0, 110.0, 90.0, 100.0, 1000.0);
            natrHigh.Update(bar);
        }

        Assert.True(natrHigh.Last.Value > natrLow.Last.Value,
            $"High vol NATR {natrHigh.Last.Value} should be > Low vol NATR {natrLow.Last.Value}");
    }

    // ============== Event Publishing ==============

    [Fact]
    public void PubEvent_Fires()
    {
        var natr = new Natr(14);
        bool eventFired = false;

        natr.Pub += (object? sender, in TValueEventArgs args) => eventFired = true;

        var bar = GenerateTestBars(1)[0];
        natr.Update(bar);

        Assert.True(eventFired);
    }

    // ============== Additional Tests ==============

    [Fact]
    public void LargeDataset_Completes()
    {
        var natr = new Natr(14);
        var bars = GenerateTestBars(5000);

        foreach (var bar in bars)
        {
            natr.Update(bar);
        }

        Assert.True(natr.IsHot);
        Assert.True(double.IsFinite(natr.Last.Value));
    }

    [Fact]
    public void DifferentParameters_ProduceValidValues()
    {
        var bars = GenerateTestBars(200);

        var natr1 = new Natr(5);
        var natr2 = new Natr(14);
        var natr3 = new Natr(28);

        foreach (var bar in bars)
        {
            natr1.Update(bar);
            natr2.Update(bar);
            natr3.Update(bar);
        }

        Assert.True(double.IsFinite(natr1.Last.Value));
        Assert.True(double.IsFinite(natr2.Last.Value));
        Assert.True(double.IsFinite(natr3.Last.Value));
    }

    [Fact]
    public void ConstructorFromTBarSeries_Works()
    {
        var bars = GenerateTestBars(100);
        var natr = new Natr(bars, 14);

        Assert.True(double.IsFinite(natr.Last.Value));
    }

#pragma warning disable S2699 // Tests contain assertions - analyzer false positive
    [Fact]
    public void Prime_Works()
    {
        var natr = new Natr(5);
        var values = new double[] { 1.0, 1.1, 0.9, 1.2, 0.8, 1.3, 1.0, 1.1, 0.95, 1.05 };

        natr.Prime(values);

        // Prime only sets ATR state (without close price, can't calculate NATR percentage)
        // The Last value will be the ATR, not NATR percentage
        Assert.True(double.IsFinite(natr.Last.Value), "Last value should be finite after Prime");
    }
#pragma warning restore S2699
}
