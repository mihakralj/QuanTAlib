namespace QuanTAlib.Tests;

#pragma warning disable S2245 // Random is acceptable for simulation/testing purposes
public class SsfTests
{
    [Fact]
    public void Ssf_Constructor_Period_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Ssf(0));
        Assert.Throws<ArgumentException>(() => new Ssf(-1));

        var ssf = new Ssf(10);
        Assert.NotNull(ssf);
    }

    [Fact]
    public void Ssf_Calc_ReturnsValue()
    {
        var ssf = new Ssf(10);

        Assert.Equal(0, ssf.Last.Value);

        TValue result = ssf.Update(new TValue(DateTime.UtcNow, 100));

        Assert.True(result.Value > 0);
        Assert.Equal(result.Value, ssf.Last.Value);
    }

    [Fact]
    public void Ssf_Calc_IsNew_AcceptsParameter()
    {
        var ssf = new Ssf(10);

        ssf.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        double value1 = ssf.Last.Value;

        ssf.Update(new TValue(DateTime.UtcNow, 105), isNew: true);
        double value2 = ssf.Last.Value;

        // Values should change with new bars
        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Ssf_Calc_IsNew_False_UpdatesValue()
    {
        var ssf = new Ssf(10);

        ssf.Update(new TValue(DateTime.UtcNow, 100));
        ssf.Update(new TValue(DateTime.UtcNow, 110), isNew: true);
        double beforeUpdate = ssf.Last.Value;

        ssf.Update(new TValue(DateTime.UtcNow, 120), isNew: false);
        double afterUpdate = ssf.Last.Value;

        // Update should change the value
        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void Ssf_Reset_ClearsState()
    {
        var ssf = new Ssf(10);

        ssf.Update(new TValue(DateTime.UtcNow, 100));
        ssf.Update(new TValue(DateTime.UtcNow, 105));
        double valueBefore = ssf.Last.Value;

        ssf.Reset();

        Assert.Equal(0, ssf.Last.Value);

        // After reset, should accept new values
        ssf.Update(new TValue(DateTime.UtcNow, 50));
        Assert.NotEqual(0, ssf.Last.Value);
        Assert.NotEqual(valueBefore, ssf.Last.Value);
    }

    [Fact]
    public void Ssf_Properties_Accessible()
    {
        var ssf = new Ssf(10);

        Assert.Equal(0, ssf.Last.Value);
        Assert.False(ssf.IsHot);

        ssf.Update(new TValue(DateTime.UtcNow, 100));

        Assert.NotEqual(0, ssf.Last.Value);
    }

    [Fact]
    public void Ssf_IsHot_BecomesTrueAfterWarmup()
    {
        var ssf = new Ssf(10);

        // Initially IsHot should be false
        Assert.False(ssf.IsHot);

        int steps = 0;
        while (!ssf.IsHot && steps < 1000)
        {
            ssf.Update(new TValue(DateTime.UtcNow, 100));
            steps++;
        }

        Assert.True(ssf.IsHot);
        Assert.True(steps > 0);
        Assert.Equal(10, steps); // WarmupPeriod is period
    }

    [Fact]
    public void Ssf_IterativeCorrections_RestoreToOriginalState()
    {
        var ssf = new Ssf(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 10 new values
        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            ssf.Update(tenthInput, isNew: true);
        }

        // Remember SSF state after 10 values
        double ssfAfterTen = ssf.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            ssf.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered 10th input again with isNew=false
        TValue finalSsf = ssf.Update(tenthInput, isNew: false);

        // SSF should match the original state after 10 values
        Assert.Equal(ssfAfterTen, finalSsf.Value, 1e-10);
    }

    [Fact]
    public void Ssf_BatchCalc_MatchesIterativeCalc()
    {
        var ssfIterative = new Ssf(10);
        var ssfBatch = new Ssf(10);
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
            iterativeResults.Add(ssfIterative.Update(item));
        }

        // Calculate batch
        var batchResults = ssfBatch.Update(series);

        // Compare
        Assert.Equal(iterativeResults.Count, batchResults.Count);
        for (int i = 0; i < iterativeResults.Count; i++)
        {
            Assert.Equal(iterativeResults[i].Value, batchResults[i].Value, 1e-10);
            Assert.Equal(iterativeResults[i].Time, batchResults[i].Time);
        }
    }

    [Fact]
    public void Ssf_NaN_Input_UsesLastValidValue()
    {
        var ssf = new Ssf(10);

        // Feed some valid values
        ssf.Update(new TValue(DateTime.UtcNow, 100));
        ssf.Update(new TValue(DateTime.UtcNow, 110));

        // Feed NaN - should use last valid value (110)
        var resultAfterNaN = ssf.Update(new TValue(DateTime.UtcNow, double.NaN));

        // Result should be finite (not NaN)
        Assert.True(double.IsFinite(resultAfterNaN.Value));
        // SSF should continue to evolve
        Assert.NotEqual(0, resultAfterNaN.Value);
    }

    [Fact]
    public void Ssf_Infinity_Input_UsesLastValidValue()
    {
        var ssf = new Ssf(10);

        // Feed some valid values
        ssf.Update(new TValue(DateTime.UtcNow, 100));
        ssf.Update(new TValue(DateTime.UtcNow, 110));

        // Feed positive infinity - should use last valid value
        var resultAfterPosInf = ssf.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultAfterPosInf.Value));

        // Feed negative infinity - should use last valid value
        var resultAfterNegInf = ssf.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultAfterNegInf.Value));
    }

    [Fact]
    public void Ssf_SpanBatch_MatchesTSeriesBatch()
    {
        var series = new TSeries();
        double[] source = new double[100];
        double[] output = new double[100];

        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            source[i] = bar.Close;
            series.Add(bar.Time, bar.Close);
        }

        // Calculate with TSeries API
        var tseriesResult = Ssf.Calculate(series, 10).Results;

        // Calculate with Span API
        Ssf.Calculate(source.AsSpan(), output.AsSpan(), 10);

        // Compare results
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(tseriesResult[i].Value, output[i], 1e-9);
        }
    }

    [Fact]
    public void Ssf_AllModes_ProduceSameResult()
    {
        // Arrange
        const int period = 10;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch Mode
        var batchSeries = Ssf.Calculate(series, period).Results;
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var tValues = series.Values.ToArray();
        var spanInput = new ReadOnlySpan<double>(tValues);
        var spanOutput = new double[tValues.Length];
        Ssf.Calculate(spanInput, spanOutput, period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Ssf(period);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Ssf(pubSource, period);
        for (int i = 0; i < series.Count; i++)
        {
            pubSource.Add(series[i]);
        }
        double eventingResult = eventingInd.Last.Value;

        // Assert
        Assert.Equal(expected, spanResult, precision: 9);
        Assert.Equal(expected, streamingResult, precision: 9);
        Assert.Equal(expected, eventingResult, precision: 9);
    }
}
