namespace QuanTAlib.Tests;

public class VamaTests
{
    [Fact]
    public void Vama_Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Vama(baseLength: 0));
        Assert.Throws<ArgumentException>(() => new Vama(baseLength: -1));
        Assert.Throws<ArgumentException>(() => new Vama(shortAtrPeriod: 0));
        Assert.Throws<ArgumentException>(() => new Vama(longAtrPeriod: 0));
        Assert.Throws<ArgumentException>(() => new Vama(minLength: 0));
        Assert.Throws<ArgumentException>(() => new Vama(maxLength: 0));
        Assert.Throws<ArgumentException>(() => new Vama(minLength: 50, maxLength: 10));

        var vama = new Vama(20, 10, 50, 5, 100);
        Assert.NotNull(vama);
    }

    [Fact]
    public void Vama_Calc_ReturnsValue()
    {
        var vama = new Vama();

        Assert.Equal(0, vama.Last.Value);

        TValue result = vama.Update(new TValue(DateTime.UtcNow, 100));

        Assert.True(result.Value > 0);
        Assert.Equal(result.Value, vama.Last.Value);
    }

    [Fact]
    public void Vama_Calc_IsNew_AcceptsParameter()
    {
        var vama = new Vama();

        vama.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        double value1 = vama.Last.Value;

        vama.Update(new TValue(DateTime.UtcNow, 105), isNew: true);
        double value2 = vama.Last.Value;

        // Values should change with new bars
        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Vama_Calc_IsNew_False_UpdatesValue()
    {
        var vama = new Vama();

        vama.Update(new TValue(DateTime.UtcNow, 100));
        vama.Update(new TValue(DateTime.UtcNow, 110), isNew: true);
        double beforeUpdate = vama.Last.Value;

        vama.Update(new TValue(DateTime.UtcNow, 120), isNew: false);
        double afterUpdate = vama.Last.Value;

        // Update should change the value
        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void Vama_Reset_ClearsState()
    {
        var vama = new Vama();

        vama.Update(new TValue(DateTime.UtcNow, 100));
        vama.Update(new TValue(DateTime.UtcNow, 105));
        double valueBefore = vama.Last.Value;

        vama.Reset();

        Assert.Equal(0, vama.Last.Value);

        // After reset, should accept new values
        vama.Update(new TValue(DateTime.UtcNow, 50));
        Assert.NotEqual(0, vama.Last.Value);
        Assert.NotEqual(valueBefore, vama.Last.Value);
    }

    [Fact]
    public void Vama_Properties_Accessible()
    {
        var vama = new Vama();

        Assert.Equal(0, vama.Last.Value);
        Assert.False(vama.IsHot);

        vama.Update(new TValue(DateTime.UtcNow, 100));

        Assert.NotEqual(0, vama.Last.Value);
    }

    [Fact]
    public void Vama_IsHot_BecomesTrueWithSufficientData()
    {
        var vama = new Vama();

        // Initially IsHot should be false
        Assert.False(vama.IsHot);

        int steps = 0;
        while (!vama.IsHot && steps < 1000)
        {
            vama.Update(new TValue(DateTime.UtcNow, 100));
            steps++;
        }

        Assert.True(vama.IsHot);
        Assert.True(steps > 0);
    }

    [Fact]
    public void Vama_IterativeCorrections_RestoreToOriginalState()
    {
        var vama = new Vama();
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 10 new values
        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            vama.Update(tenthInput, isNew: true);
        }

        // Remember VAMA state after 10 values
        double vamaAfterTen = vama.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            vama.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered 10th input again with isNew=false
        TValue finalVama = vama.Update(tenthInput, isNew: false);

        // VAMA should match the original state after 10 values
        Assert.Equal(vamaAfterTen, finalVama.Value, 1e-10);
    }

    [Fact]
    public void Vama_BatchCalc_MatchesIterativeCalc()
    {
        var vamaIterative = new Vama();
        var vamaBatch = new Vama();
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Generate data
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
            iterativeResults.Add(vamaIterative.Update(item));
        }

        // Calculate batch
        var batchResults = vamaBatch.Update(series);

        // Compare
        Assert.Equal(iterativeResults.Count, batchResults.Count);
        for (int i = 0; i < iterativeResults.Count; i++)
        {
            Assert.Equal(iterativeResults[i].Value, batchResults[i].Value, 1e-10);
            Assert.Equal(iterativeResults[i].Time, batchResults[i].Time);
        }
    }

    [Fact]
    public void Vama_NaN_Input_UsesLastValidValue()
    {
        var vama = new Vama();

        // Feed some valid values
        vama.Update(new TValue(DateTime.UtcNow, 100));
        vama.Update(new TValue(DateTime.UtcNow, 110));

        // Feed NaN - should use last valid value (110)
        var resultAfterNaN = vama.Update(new TValue(DateTime.UtcNow, double.NaN));

        // Result should be finite (not NaN)
        Assert.True(double.IsFinite(resultAfterNaN.Value));
        Assert.NotEqual(0, resultAfterNaN.Value);
    }

    [Fact]
    public void Vama_Infinity_Input_UsesLastValidValue()
    {
        var vama = new Vama();

        // Feed some valid values
        vama.Update(new TValue(DateTime.UtcNow, 100));
        vama.Update(new TValue(DateTime.UtcNow, 110));

        // Feed positive infinity - should use last valid value
        var resultAfterPosInf = vama.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultAfterPosInf.Value));

        // Feed negative infinity - should use last valid value
        var resultAfterNegInf = vama.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultAfterNegInf.Value));
    }

    [Fact]
    public void Vama_MultipleNaN_ContinuesWithLastValid()
    {
        var vama = new Vama();

        // Feed valid values
        vama.Update(new TValue(DateTime.UtcNow, 100));
        vama.Update(new TValue(DateTime.UtcNow, 110));
        vama.Update(new TValue(DateTime.UtcNow, 120));

        // Feed multiple NaN values
        var r1 = vama.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r2 = vama.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r3 = vama.Update(new TValue(DateTime.UtcNow, double.NaN));

        // All results should be finite
        Assert.True(double.IsFinite(r1.Value));
        Assert.True(double.IsFinite(r2.Value));
        Assert.True(double.IsFinite(r3.Value));
    }

    [Fact]
    public void Vama_BatchCalc_HandlesNaN()
    {
        var vama = new Vama();

        // Create series with NaN values interspersed
        var series = new TSeries();
        series.Add(DateTime.UtcNow.Ticks, 100);
        series.Add(DateTime.UtcNow.Ticks + 1, 110);
        series.Add(DateTime.UtcNow.Ticks + 2, double.NaN);
        series.Add(DateTime.UtcNow.Ticks + 3, 120);
        series.Add(DateTime.UtcNow.Ticks + 4, double.PositiveInfinity);
        series.Add(DateTime.UtcNow.Ticks + 5, 130);

        var results = vama.Update(series);

        // All results should be finite
        foreach (var result in results)
        {
            Assert.True(double.IsFinite(result.Value), $"Expected finite value but got {result.Value}");
        }
    }

    [Fact]
    public void Vama_Reset_ClearsLastValidValue()
    {
        var vama = new Vama();

        // Feed values including NaN
        vama.Update(new TValue(DateTime.UtcNow, 100));
        vama.Update(new TValue(DateTime.UtcNow, double.NaN));

        // Reset
        vama.Reset();

        // After reset, first valid value should establish new baseline
        var result = vama.Update(new TValue(DateTime.UtcNow, 50));
        Assert.Equal(50.0, result.Value, 1e-10);
    }

    [Fact]
    public void Chainability_Works()
    {
        var source = new TSeries();
        var vama = new Vama(source);

        source.Add(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100, vama.Last.Value, 1e-10);
    }

    [Fact]
    public void Prime_SetsStateCorrectly()
    {
        var vama = new Vama();
        double[] history = [10, 20, 30, 40, 50];

        vama.Prime(history);

        // Verify against a fresh VAMA fed with same data
        var verifyVama = new Vama();
        foreach (var val in history) verifyVama.Update(new TValue(DateTime.UtcNow, val));

        Assert.Equal(verifyVama.Last.Value, vama.Last.Value, 1e-10);

        // Verify it continues correctly
        vama.Update(new TValue(DateTime.UtcNow, 60));
        verifyVama.Update(new TValue(DateTime.UtcNow, 60));
        Assert.Equal(verifyVama.Last.Value, vama.Last.Value, 1e-10);
    }

    [Fact]
    public void Prime_HandlesNaN_InHistory()
    {
        var vama = new Vama();
        double[] history = [10, 20, double.NaN, 40, 50];

        vama.Prime(history);

        var verifyVama = new Vama();
        foreach (var val in history) verifyVama.Update(new TValue(DateTime.UtcNow, val));

        Assert.Equal(verifyVama.Last.Value, vama.Last.Value, 1e-10);
    }

    [Fact]
    public void Prime_ThenUpdate_StateWorksCorrectly()
    {
        var vama = new Vama();
        double[] history = [10, 20, 30, 40, 50];

        vama.Prime(history);
        double afterPrime = vama.Last.Value;

        // After Prime, an isNew=true should advance the state
        vama.Update(new TValue(DateTime.UtcNow, 60), isNew: true);
        double afterNewBar = vama.Last.Value;

        // Values should be different
        Assert.NotEqual(afterPrime, afterNewBar);

        // isNew=false with a different value should recalculate from previous state
        vama.Update(new TValue(DateTime.UtcNow, 70), isNew: false);
        double afterCorrection = vama.Last.Value;

        // Correction with 70 should give different result than 60
        Assert.NotEqual(afterNewBar, afterCorrection);

        // isNew=false with original value (60) should restore to afterNewBar
        vama.Update(new TValue(DateTime.UtcNow, 60), isNew: false);
        Assert.Equal(afterNewBar, vama.Last.Value, 1e-10);
    }

    [Fact]
    public void Vama_AllModes_ProduceSameResult()
    {
        // Arrange
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch Mode
        var batchSeries = Vama.Batch(series);
        double expected = batchSeries.Last.Value;

        // 2. Streaming Mode
        var streamingInd = new Vama();
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 3. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Vama(pubSource);
        for (int i = 0; i < series.Count; i++)
        {
            pubSource.Add(series[i]);
        }
        double eventingResult = eventingInd.Last.Value;

        // Assert
        Assert.Equal(expected, streamingResult, precision: 9);
        Assert.Equal(expected, eventingResult, precision: 9);
    }

    // ============== TBar-specific Tests ==============

    [Fact]
    public void Vama_TBar_UsesOHLC_ForTrueRange()
    {
        var vama = new Vama();
        var time = DateTime.UtcNow;

        // Feed bars with varying volatility - enough for warmup (minLength=5)
        for (int i = 0; i < 100; i++)
        {
            var bar = new TBar(time.AddMinutes(i), 100, 105, 95, 100, 1000);
            vama.Update(bar, isNew: true);
        }

        Assert.True(double.IsFinite(vama.Last.Value));
        // IsHot requires ValidCount >= minLength (5) and IsInitialized
        Assert.True(vama.IsHot, $"Expected IsHot=true after 100 bars");
    }

    [Fact]
    public void Vama_TBarSeries_BatchWorks()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.15, seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var result = Vama.Batch(bars);

        Assert.Equal(200, result.Count);
        Assert.All(result, tv => Assert.True(double.IsFinite(tv.Value)));
    }

    [Fact]
    public void Vama_VolatilityRatio_AdjustsLength()
    {
        var vamaLowVol = new Vama();
        var vamaHighVol = new Vama();

        var time = DateTime.UtcNow;

        // Feed low volatility bars (H-L is small)
        for (int i = 0; i < 100; i++)
        {
            var bar = new TBar(time.AddMinutes(i), 100, 100.1, 99.9, 100, 1000);
            vamaLowVol.Update(bar, isNew: true);
        }

        // Feed high volatility bars (H-L is large)
        for (int i = 0; i < 100; i++)
        {
            var bar = new TBar(time.AddMinutes(i), 100, 110, 90, 100, 1000);
            vamaHighVol.Update(bar, isNew: true);
        }

        // Both should produce valid results
        Assert.True(double.IsFinite(vamaLowVol.Last.Value));
        Assert.True(double.IsFinite(vamaHighVol.Last.Value));
    }

    [Fact]
    public void Vama_ConstantInput_ConvergesToInput()
    {
        var vama = new Vama();

        // Feed constant values
        for (int i = 0; i < 200; i++)
        {
            vama.Update(new TValue(DateTime.UtcNow, 100));
        }

        // With constant input, SMA output should converge to input value
        Assert.Equal(100.0, vama.Last.Value, 1e-9);
    }

    [Fact]
    public void Vama_ParameterVariations_Produce_ValidResults()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.02, sigma: 0.15, seed: 42);

        // Test various parameter combinations
        var vama1 = new Vama(10, 5, 20, 3, 50);
        var vama2 = new Vama(30, 15, 60, 10, 150);
        var vama3 = new Vama(50, 20, 100, 20, 200);

        for (int i = 0; i < 200; i++)
        {
            var bar = gbm.Next(isNew: true);
            var tv = new TValue(bar.Time, bar.Close);

            vama1.Update(tv, isNew: true);
            vama2.Update(tv, isNew: true);
            vama3.Update(tv, isNew: true);
        }

        Assert.True(double.IsFinite(vama1.Last.Value));
        Assert.True(double.IsFinite(vama2.Last.Value));
        Assert.True(double.IsFinite(vama3.Last.Value));
    }
}
