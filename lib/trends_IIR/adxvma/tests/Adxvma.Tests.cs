namespace QuanTAlib.Tests;

public class AdxvmaTests
{
    // ==================== A) Constructor Validation ====================

    [Fact]
    public void Adxvma_Constructor_ThrowsOnZeroPeriod()
    {
        Assert.Throws<ArgumentException>(() => new Adxvma(period: 0));
    }

    [Fact]
    public void Adxvma_Constructor_ThrowsOnNegativePeriod()
    {
        Assert.Throws<ArgumentException>(() => new Adxvma(period: -1));
    }

    [Fact]
    public void Adxvma_Constructor_AcceptsValidPeriod()
    {
        var adxvma = new Adxvma(period: 14);
        Assert.NotNull(adxvma);
        Assert.Equal("Adxvma(14)", adxvma.Name);
    }

    [Fact]
    public void Adxvma_Constructor_PeriodOneIsValid()
    {
        var adxvma = new Adxvma(period: 1);
        Assert.NotNull(adxvma);
        Assert.Equal("Adxvma(1)", adxvma.Name);
    }

    [Fact]
    public void Adxvma_Constructor_DefaultPeriodIs14()
    {
        var adxvma = new Adxvma();
        Assert.Equal("Adxvma(14)", adxvma.Name);
    }

    // ==================== B) Basic Calculation ====================

    [Fact]
    public void Adxvma_Calc_ReturnsValue()
    {
        var adxvma = new Adxvma();

        Assert.Equal(0, adxvma.Last.Value);

        TValue result = adxvma.Update(new TValue(DateTime.UtcNow, 100));

        Assert.True(result.Value > 0);
        Assert.Equal(result.Value, adxvma.Last.Value);
    }

    [Fact]
    public void Adxvma_TBar_ReturnsValue()
    {
        var adxvma = new Adxvma();
        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);

        TValue result = adxvma.Update(bar, isNew: true);

        Assert.True(double.IsFinite(result.Value));
        Assert.Equal(result.Value, adxvma.Last.Value);
    }

    [Fact]
    public void Adxvma_TBar_UsesOHLC_ForTrueRange()
    {
        var adxvma = new Adxvma();
        var time = DateTime.UtcNow;

        // Feed bars with varying volatility
        for (int i = 0; i < 100; i++)
        {
            var bar = new TBar(time.AddMinutes(i), 100, 105, 95, 100, 1000);
            adxvma.Update(bar, isNew: true);
        }

        Assert.True(double.IsFinite(adxvma.Last.Value));
        Assert.True(adxvma.IsHot, "Expected IsHot=true after 100 bars");
    }

    [Fact]
    public void Adxvma_Properties_Accessible()
    {
        var adxvma = new Adxvma();

        Assert.Equal(0, adxvma.Last.Value);
        Assert.False(adxvma.IsHot);

        adxvma.Update(new TValue(DateTime.UtcNow, 100));

        Assert.NotEqual(0, adxvma.Last.Value);
    }

    // ==================== C) State + Bar Correction ====================

    [Fact]
    public void Adxvma_IsNew_True_AdvancesState()
    {
        var adxvma = new Adxvma();

        adxvma.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        double value1 = adxvma.Last.Value;

        adxvma.Update(new TValue(DateTime.UtcNow, 105), isNew: true);
        double value2 = adxvma.Last.Value;

        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Adxvma_IsNew_False_UpdatesValue()
    {
        var adxvma = new Adxvma();

        adxvma.Update(new TValue(DateTime.UtcNow, 100));
        adxvma.Update(new TValue(DateTime.UtcNow, 110), isNew: true);
        double beforeUpdate = adxvma.Last.Value;

        adxvma.Update(new TValue(DateTime.UtcNow, 120), isNew: false);
        double afterUpdate = adxvma.Last.Value;

        // Update should change the value
        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void Adxvma_IterativeCorrections_RestoreToOriginalState()
    {
        var adxvma = new Adxvma();
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 10 new values
        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            adxvma.Update(tenthInput, isNew: true);
        }

        // Remember state after 10 values
        double afterTen = adxvma.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            adxvma.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered 10th input again with isNew=false
        TValue restored = adxvma.Update(tenthInput, isNew: false);

        // Should match the original state after 10 values
        Assert.Equal(afterTen, restored.Value, 1e-10);
    }

    [Fact]
    public void Adxvma_TBar_BarCorrection_Works()
    {
        var adxvma = new Adxvma();
        var time = DateTime.UtcNow;

        // Feed some history
        for (int i = 0; i < 5; i++)
        {
            var bar = new TBar(time.AddMinutes(i), 100, 105, 95, 102, 1000);
            adxvma.Update(bar, isNew: true);
        }

        // New bar
        var newBar = new TBar(time.AddMinutes(5), 102, 108, 99, 106, 1200);
        adxvma.Update(newBar, isNew: true);
        double afterNewBar = adxvma.Last.Value;

        // Correction with different bar
        var corrBar = new TBar(time.AddMinutes(5), 103, 107, 100, 104, 1100);
        adxvma.Update(corrBar, isNew: false);
        double afterCorrection = adxvma.Last.Value;

        // Different bar data should produce different result
        Assert.NotEqual(afterNewBar, afterCorrection);

        // Correction with original bar should restore state
        adxvma.Update(newBar, isNew: false);
        Assert.Equal(afterNewBar, adxvma.Last.Value, 1e-10);
    }

    [Fact]
    public void Adxvma_Reset_ClearsState()
    {
        var adxvma = new Adxvma();

        adxvma.Update(new TValue(DateTime.UtcNow, 100));
        adxvma.Update(new TValue(DateTime.UtcNow, 105));
        double valueBefore = adxvma.Last.Value;

        adxvma.Reset();

        Assert.Equal(0, adxvma.Last.Value);

        // After reset, should accept new values
        adxvma.Update(new TValue(DateTime.UtcNow, 50));
        Assert.NotEqual(0, adxvma.Last.Value);
        Assert.NotEqual(valueBefore, adxvma.Last.Value);
    }

    // ==================== D) Warmup / Convergence ====================

    [Fact]
    public void Adxvma_IsHot_BecomesTrueAfterWarmup()
    {
        var adxvma = new Adxvma(period: 14);

        // IsHot requires barCount >= period * 2 = 28
        Assert.False(adxvma.IsHot);

        int steps = 0;
        while (!adxvma.IsHot && steps < 1000)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(steps), 100, 105, 95, 100, 1000);
            adxvma.Update(bar, isNew: true);
            steps++;
        }

        Assert.True(adxvma.IsHot);
        Assert.True(steps <= 28, $"Expected IsHot within 28 bars but took {steps}");
    }

    [Fact]
    public void Adxvma_WarmupPeriod_EqualsDoubleThePeriod()
    {
        var adxvma = new Adxvma(period: 10);
        Assert.Equal(20, adxvma.WarmupPeriod);

        var adxvma2 = new Adxvma(period: 14);
        Assert.Equal(28, adxvma2.WarmupPeriod);
    }

    [Fact]
    public void Adxvma_ConstantOHLC_ConvergesToClose()
    {
        var adxvma = new Adxvma();
        var time = DateTime.UtcNow;

        // Feed constant OHLC bars
        for (int i = 0; i < 200; i++)
        {
            var bar = new TBar(time.AddMinutes(i), 100, 100, 100, 100, 1000);
            adxvma.Update(bar, isNew: true);
        }

        // With constant input, should converge to close
        Assert.Equal(100.0, adxvma.Last.Value, 1e-9);
    }

    [Fact]
    public void Adxvma_ConstantTValue_ConvergesToInput()
    {
        var adxvma = new Adxvma();

        // Feed constant values via TValue (synthetic bar: O=H=L=C, TR=0)
        for (int i = 0; i < 200; i++)
        {
            adxvma.Update(new TValue(DateTime.UtcNow, 42.5));
        }

        // With constant input, ADXVMA should converge to input
        Assert.Equal(42.5, adxvma.Last.Value, 1e-9);
    }

    // ==================== E) Robustness ====================

    [Fact]
    public void Adxvma_NaN_Input_UsesLastValidValue()
    {
        var adxvma = new Adxvma();

        adxvma.Update(new TValue(DateTime.UtcNow, 100));
        adxvma.Update(new TValue(DateTime.UtcNow, 110));

        var resultAfterNaN = adxvma.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(resultAfterNaN.Value));
        Assert.NotEqual(0, resultAfterNaN.Value);
    }

    [Fact]
    public void Adxvma_Infinity_Input_UsesLastValidValue()
    {
        var adxvma = new Adxvma();

        adxvma.Update(new TValue(DateTime.UtcNow, 100));
        adxvma.Update(new TValue(DateTime.UtcNow, 110));

        var resultAfterPosInf = adxvma.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultAfterPosInf.Value));

        var resultAfterNegInf = adxvma.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultAfterNegInf.Value));
    }

    [Fact]
    public void Adxvma_MultipleNaN_ContinuesWithLastValid()
    {
        var adxvma = new Adxvma();

        adxvma.Update(new TValue(DateTime.UtcNow, 100));
        adxvma.Update(new TValue(DateTime.UtcNow, 110));
        adxvma.Update(new TValue(DateTime.UtcNow, 120));

        var r1 = adxvma.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r2 = adxvma.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r3 = adxvma.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(r1.Value));
        Assert.True(double.IsFinite(r2.Value));
        Assert.True(double.IsFinite(r3.Value));
    }

    [Fact]
    public void Adxvma_BatchCalc_HandlesNaN()
    {
        var adxvma = new Adxvma();

        var series = new TSeries();
        series.Add(DateTime.UtcNow.Ticks, 100);
        series.Add(DateTime.UtcNow.Ticks + 1, 110);
        series.Add(DateTime.UtcNow.Ticks + 2, double.NaN);
        series.Add(DateTime.UtcNow.Ticks + 3, 120);
        series.Add(DateTime.UtcNow.Ticks + 4, double.PositiveInfinity);
        series.Add(DateTime.UtcNow.Ticks + 5, 130);

        var results = adxvma.Update(series);

        foreach (var result in results)
        {
            Assert.True(double.IsFinite(result.Value), $"Expected finite value but got {result.Value}");
        }
    }

    [Fact]
    public void Adxvma_Reset_ClearsLastValidValue()
    {
        var adxvma = new Adxvma();

        adxvma.Update(new TValue(DateTime.UtcNow, 100));
        adxvma.Update(new TValue(DateTime.UtcNow, double.NaN));

        adxvma.Reset();

        // After reset, first valid value should establish new baseline
        var result = adxvma.Update(new TValue(DateTime.UtcNow, 50));
        Assert.Equal(50.0, result.Value, 1e-10);
    }

    // ==================== F) Consistency (All Modes Match) ====================

    [Fact]
    public void Adxvma_BatchCalc_MatchesIterativeCalc()
    {
        var adxvmaIterative = new Adxvma();
        var adxvmaBatch = new Adxvma();
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        var series = new TSeries();
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        Assert.True(series.Count > 0);

        // Calculate iteratively
        var iterativeResults = new TSeries();
        foreach (var item in series)
        {
            iterativeResults.Add(adxvmaIterative.Update(item));
        }

        // Calculate batch
        var batchResults = adxvmaBatch.Update(series);

        // Compare
        Assert.Equal(iterativeResults.Count, batchResults.Count);
        for (int i = 0; i < iterativeResults.Count; i++)
        {
            Assert.Equal(iterativeResults[i].Value, batchResults[i].Value, 1e-10);
            Assert.Equal(iterativeResults[i].Time, batchResults[i].Time);
        }
    }

    [Fact]
    public void Adxvma_TBarSeries_MatchesIterativeTBar()
    {
        var adxvmaIterative = new Adxvma();
        var adxvmaBatch = new Adxvma();
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Calculate iteratively with TBar
        var iterativeResults = new TSeries();
        foreach (var bar in bars)
        {
            iterativeResults.Add(adxvmaIterative.Update(bar, isNew: true));
        }

        // Calculate batch with TBarSeries
        var batchResults = adxvmaBatch.Update(bars);

        // Compare
        Assert.Equal(iterativeResults.Count, batchResults.Count);
        for (int i = 0; i < iterativeResults.Count; i++)
        {
            Assert.Equal(iterativeResults[i].Value, batchResults[i].Value, 1e-10);
        }
    }

    [Fact]
    public void Adxvma_AllModes_ProduceSameResult()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch Mode
        var batchSeries = Adxvma.Batch(series);
        double expected = batchSeries.Last.Value;

        // 2. Streaming Mode
        var streamingInd = new Adxvma();
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 3. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Adxvma(pubSource);
        for (int i = 0; i < series.Count; i++)
        {
            pubSource.Add(series[i]);
        }
        double eventingResult = eventingInd.Last.Value;

        // Assert
        Assert.Equal(expected, streamingResult, precision: 9);
        Assert.Equal(expected, eventingResult, precision: 9);
    }

    // ==================== G) TBar-specific Tests ====================

    [Fact]
    public void Adxvma_TBarSeries_BatchWorks()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var result = Adxvma.Batch(bars);

        Assert.Equal(200, result.Count);
        Assert.All(result, tv => Assert.True(double.IsFinite(tv.Value)));
    }

    [Fact]
    public void Adxvma_TValue_SyntheticBar_ProducesValidOutput()
    {
        // TValue creates synthetic bar: O=H=L=C → TR=0 → ADX→0 → sc→0 → flat line
        var adxvma = new Adxvma();
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            var result = adxvma.Update(new TValue(bar.Time, bar.Close), isNew: true);
            Assert.True(double.IsFinite(result.Value));
        }
    }

    [Fact]
    public void Adxvma_StrongTrend_HighADX_TracksPrice()
    {
        var adxvma = new Adxvma(period: 14);
        var time = DateTime.UtcNow;

        // Feed strong uptrend bars (large +DM consistently)
        for (int i = 0; i < 50; i++)
        {
            double price = 100 + (i * 2);
            var bar = new TBar(time.AddMinutes(i), price, price + 1, price - 0.5, price + 0.5, 1000);
            adxvma.Update(bar, isNew: true);
        }

        // In a strong trend, ADX is high so sc ≈ 1, ADXVMA should track price closely
        double adxvmaValue = adxvma.Last.Value;

        // Should be within reasonable proximity of recent prices
        Assert.True(adxvmaValue > 100, $"ADXVMA ({adxvmaValue}) should be well above 100 in a strong uptrend");
    }

    [Fact]
    public void Adxvma_Calculate_TBarSeries_ReturnsIndicator()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var (results, indicator) = Adxvma.Calculate(bars);

        Assert.Equal(200, results.Count);
        Assert.NotNull(indicator);
        Assert.True(indicator.IsHot);
        Assert.Equal(results.Last.Value, indicator.Last.Value, 1e-10);
    }

    [Fact]
    public void Adxvma_Calculate_TSeries_ReturnsIndicator()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var (results, indicator) = Adxvma.Calculate(series);

        Assert.Equal(200, results.Count);
        Assert.NotNull(indicator);
        Assert.True(indicator.IsHot);
        Assert.Equal(results.Last.Value, indicator.Last.Value, 1e-10);
    }

    // ==================== H) Chainability ====================

    [Fact]
    public void Adxvma_Chainability_Works()
    {
        var source = new TSeries();
        var adxvma = new Adxvma(source);

        source.Add(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100, adxvma.Last.Value, 1e-10);
    }

    // ==================== Prime Tests ====================

    [Fact]
    public void Adxvma_Prime_SetsStateCorrectly()
    {
        var adxvma = new Adxvma();
        double[] history = [10, 20, 30, 40, 50];

        adxvma.Prime(history);

        var verifyAdxvma = new Adxvma();
        foreach (var val in history)
        {
            verifyAdxvma.Update(new TValue(DateTime.UtcNow, val));
        }

        Assert.Equal(verifyAdxvma.Last.Value, adxvma.Last.Value, 1e-10);

        // Verify it continues correctly
        adxvma.Update(new TValue(DateTime.UtcNow, 60));
        verifyAdxvma.Update(new TValue(DateTime.UtcNow, 60));
        Assert.Equal(verifyAdxvma.Last.Value, adxvma.Last.Value, 1e-10);
    }

    [Fact]
    public void Adxvma_Prime_HandlesNaN_InHistory()
    {
        var adxvma = new Adxvma();
        double[] history = [10, 20, double.NaN, 40, 50];

        adxvma.Prime(history);

        var verifyAdxvma = new Adxvma();
        foreach (var val in history)
        {
            verifyAdxvma.Update(new TValue(DateTime.UtcNow, val));
        }

        Assert.Equal(verifyAdxvma.Last.Value, adxvma.Last.Value, 1e-10);
    }

    [Fact]
    public void Adxvma_Prime_ThenUpdate_StateWorksCorrectly()
    {
        var adxvma = new Adxvma();
        double[] history = [10, 20, 30, 40, 50];

        adxvma.Prime(history);
        double afterPrime = adxvma.Last.Value;

        // After Prime, isNew=true should advance the state
        adxvma.Update(new TValue(DateTime.UtcNow, 60), isNew: true);
        double afterNewBar = adxvma.Last.Value;

        Assert.NotEqual(afterPrime, afterNewBar);

        // isNew=false with different value should recalculate
        adxvma.Update(new TValue(DateTime.UtcNow, 70), isNew: false);
        double afterCorrection = adxvma.Last.Value;

        Assert.NotEqual(afterNewBar, afterCorrection);

        // isNew=false with original value should restore
        adxvma.Update(new TValue(DateTime.UtcNow, 60), isNew: false);
        Assert.Equal(afterNewBar, adxvma.Last.Value, 1e-10);
    }

    // ==================== Dispose Test ====================

    [Fact]
    public void Adxvma_Dispose_DoesNotThrow()
    {
        var adxvma = new Adxvma();
        adxvma.Update(new TValue(DateTime.UtcNow, 100));
        adxvma.Dispose();

        // Should be able to create a new one after dispose
        var adxvma2 = new Adxvma();
        Assert.NotNull(adxvma2);
    }

    // ==================== Parameter Variation Tests ====================

    [Fact]
    public void Adxvma_ParameterVariations_ProduceValidResults()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.15, seed: 42);

        var adxvma1 = new Adxvma(7);
        var adxvma2 = new Adxvma(14);
        var adxvma3 = new Adxvma(28);

        for (int i = 0; i < 200; i++)
        {
            var bar = gbm.Next(isNew: true);
            var tv = new TValue(bar.Time, bar.Close);

            adxvma1.Update(tv, isNew: true);
            adxvma2.Update(tv, isNew: true);
            adxvma3.Update(tv, isNew: true);
        }

        Assert.True(double.IsFinite(adxvma1.Last.Value));
        Assert.True(double.IsFinite(adxvma2.Last.Value));
        Assert.True(double.IsFinite(adxvma3.Last.Value));
    }

    [Fact]
    public void Adxvma_DifferentPeriods_DifferentSmoothness()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var results7 = Adxvma.Batch(bars, period: 7);
        var results28 = Adxvma.Batch(bars, period: 28);

        // Both should produce valid values, but longer period should be smoother
        Assert.All(results7, tv => Assert.True(double.IsFinite(tv.Value)));
        Assert.All(results28, tv => Assert.True(double.IsFinite(tv.Value)));

        // Different periods should produce different results
        Assert.NotEqual(results7.Last.Value, results28.Last.Value);
    }
}
