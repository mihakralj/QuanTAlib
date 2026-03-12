namespace QuanTAlib.Tests;

public class PwmaTests
{
    [Fact]
    public void Pwma_Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Pwma(0));
        Assert.Throws<ArgumentException>(() => new Pwma(-1));
        Assert.Throws<NullReferenceException>(() => new Pwma(null!, 10));

        var pwma = new Pwma(10);
        Assert.NotNull(pwma);
    }

    [Fact]
    public void Pwma_Calc_ReturnsValue()
    {
        var pwma = new Pwma(10);

        Assert.Equal(0, pwma.Last.Value);

        TValue result = pwma.Update(new TValue(DateTime.UtcNow, 100));

        Assert.True(result.Value > 0);
        Assert.Equal(result.Value, pwma.Last.Value);
    }

    [Fact]
    public void Pwma_FirstValue_ReturnsItself()
    {
        var pwma = new Pwma(10);

        TValue result = pwma.Update(new TValue(DateTime.UtcNow, 100));

        Assert.Equal(100.0, result.Value, 1e-10);
    }

    [Fact]
    public void Pwma_Calc_IsNew_AcceptsParameter()
    {
        var pwma = new Pwma(10);

        pwma.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        double value1 = pwma.Last.Value;

        pwma.Update(new TValue(DateTime.UtcNow, 200), isNew: true);
        double value2 = pwma.Last.Value;

        // Values should change with new bars
        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Pwma_Calc_IsNew_False_UpdatesValue()
    {
        var pwma = new Pwma(10);

        pwma.Update(new TValue(DateTime.UtcNow, 100));
        pwma.Update(new TValue(DateTime.UtcNow, 110), isNew: true);
        double beforeUpdate = pwma.Last.Value;

        pwma.Update(new TValue(DateTime.UtcNow, 120), isNew: false);
        double afterUpdate = pwma.Last.Value;

        // Update should change the value
        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void Pwma_Reset_ClearsState()
    {
        var pwma = new Pwma(10);

        pwma.Update(new TValue(DateTime.UtcNow, 100));
        pwma.Update(new TValue(DateTime.UtcNow, 105));
        double valueBefore = pwma.Last.Value;

        pwma.Reset();

        Assert.Equal(0, pwma.Last.Value);

        // After reset, should accept new values
        pwma.Update(new TValue(DateTime.UtcNow, 50));
        Assert.NotEqual(0, pwma.Last.Value);
        Assert.NotEqual(valueBefore, pwma.Last.Value);
    }

    [Fact]
    public void Pwma_Properties_Accessible()
    {
        var pwma = new Pwma(10);

        Assert.Equal(0, pwma.Last.Value);
        Assert.False(pwma.IsHot);

        pwma.Update(new TValue(DateTime.UtcNow, 100));

        Assert.NotEqual(0, pwma.Last.Value);
    }

    [Fact]
    public void Pwma_IsHot_BecomesTrueWhenBufferFull()
    {
        var pwma = new Pwma(5);

        Assert.False(pwma.IsHot);

        for (int i = 1; i <= 4; i++)
        {
            pwma.Update(new TValue(DateTime.UtcNow, i * 10));
            Assert.False(pwma.IsHot);
        }

        pwma.Update(new TValue(DateTime.UtcNow, 50));
        Assert.True(pwma.IsHot);
    }

    [Fact]
    public void Pwma_CalculatesCorrectWeightedAverage()
    {
        var pwma = new Pwma(3);

        pwma.Update(new TValue(DateTime.UtcNow, 10));
        pwma.Update(new TValue(DateTime.UtcNow, 20));
        pwma.Update(new TValue(DateTime.UtcNow, 30));

        // PWMA(3) of 10,20,30 = (1^2*10 + 2^2*20 + 3^2*30) / (1^2 + 2^2 + 3^2)
        // = (1*10 + 4*20 + 9*30) / (1 + 4 + 9)
        // = (10 + 80 + 270) / 14
        // = 360 / 14 = 25.7142857...
        Assert.Equal(360.0 / 14.0, pwma.Last.Value, 1e-10);
    }

    [Fact]
    public void Pwma_SlidingWindow_Works()
    {
        var pwma = new Pwma(3);

        pwma.Update(new TValue(DateTime.UtcNow, 10));
        pwma.Update(new TValue(DateTime.UtcNow, 20));
        pwma.Update(new TValue(DateTime.UtcNow, 30));

        // PWMA(3) of 10,20,30 = 360/14
        Assert.Equal(360.0 / 14.0, pwma.Last.Value, 1e-10);

        pwma.Update(new TValue(DateTime.UtcNow, 40));

        // PWMA(3) of 20,30,40 = (1^2*20 + 2^2*30 + 3^2*40) / 14
        // = (20 + 120 + 360) / 14 = 500 / 14 = 35.7142857...
        Assert.Equal(500.0 / 14.0, pwma.Last.Value, 1e-10);
    }

    [Fact]
    public void Pwma_IterativeCorrections_RestoreToOriginalState()
    {
        var pwma = new Pwma(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 10 new values
        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            pwma.Update(tenthInput, isNew: true);
        }

        // Remember PWMA state after 10 values
        double pwmaAfterTen = pwma.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            pwma.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered 10th input again with isNew=false
        TValue finalPwma = pwma.Update(tenthInput, isNew: false);

        // PWMA should match the original state after 10 values
        Assert.Equal(pwmaAfterTen, finalPwma.Value, 1e-10);
    }

    [Fact]
    public void Pwma_BatchCalc_MatchesIterativeCalc()
    {
        var pwmaIterative = new Pwma(10);
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
            iterativeResults.Add(pwmaIterative.Update(item));
        }

        // Calculate batch
        var batchResults = Pwma.Batch(series, 10);

        // Compare
        Assert.Equal(iterativeResults.Count, batchResults.Count);
        for (int i = 0; i < iterativeResults.Count; i++)
        {
            Assert.Equal(iterativeResults[i].Value, batchResults[i].Value, 1e-10);
            Assert.Equal(iterativeResults[i].Time, batchResults[i].Time);
        }
    }

    [Fact]
    public void Pwma_NaN_Input_UsesLastValidValue()
    {
        var pwma = new Pwma(5);

        // Feed some valid values
        pwma.Update(new TValue(DateTime.UtcNow, 100));
        pwma.Update(new TValue(DateTime.UtcNow, 110));

        // Feed NaN - should use last valid value (110)
        var resultAfterNaN = pwma.Update(new TValue(DateTime.UtcNow, double.NaN));

        // Result should be finite (not NaN)
        Assert.True(double.IsFinite(resultAfterNaN.Value));
        Assert.NotEqual(0, resultAfterNaN.Value);
    }

    [Fact]
    public void Pwma_Infinity_Input_UsesLastValidValue()
    {
        var pwma = new Pwma(5);

        // Feed some valid values
        pwma.Update(new TValue(DateTime.UtcNow, 100));
        pwma.Update(new TValue(DateTime.UtcNow, 110));

        // Feed positive infinity - should use last valid value
        var resultAfterPosInf = pwma.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultAfterPosInf.Value));

        // Feed negative infinity - should use last valid value
        var resultAfterNegInf = pwma.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultAfterNegInf.Value));
    }

    [Fact]
    public void Pwma_StaticCalculate_Works()
    {
        var series = new TSeries();
        series.Add(DateTime.UtcNow.Ticks, 10);
        series.Add(DateTime.UtcNow.Ticks + 1, 20);
        series.Add(DateTime.UtcNow.Ticks + 2, 30);

        var results = Pwma.Batch(series, 3);

        Assert.Equal(3, results.Count);
        // PWMA(3) for last 3 values [10,20,30]: 360/14
        Assert.Equal(360.0 / 14.0, results.Last.Value, 1e-10);
    }

    [Fact]
    public void Pwma_MoreWeightOnRecentValues_ThanWma()
    {
        var pwma = new Pwma(3);
        var wma = new Wma(3);

        // Feed same values to both
        pwma.Update(new TValue(DateTime.UtcNow, 10));
        wma.Update(new TValue(DateTime.UtcNow, 10));
        pwma.Update(new TValue(DateTime.UtcNow, 20));
        wma.Update(new TValue(DateTime.UtcNow, 20));
        pwma.Update(new TValue(DateTime.UtcNow, 100));  // High recent value
        wma.Update(new TValue(DateTime.UtcNow, 100));

        // PWMA should be higher than WMA because it weights the high recent value even more (parabolically)
        // WMA = (1*10 + 2*20 + 3*100) / 6 = 350/6 = 58.333...
        // PWMA = (1*10 + 4*20 + 9*100) / 14 = 990/14 = 70.714...
        Assert.True(pwma.Last.Value > wma.Last.Value);
        Assert.Equal(990.0 / 14.0, pwma.Last.Value, 1e-10);
        Assert.Equal(350.0 / 6.0, wma.Last.Value, 1e-10);
    }

    // ============== Span API Tests ==============

    [Fact]
    public void Pwma_SpanCalc_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[5];
        double[] wrongSizeOutput = new double[3];

        // Period must be > 0
        Assert.Throws<ArgumentException>(() => Pwma.Batch(source.AsSpan(), output.AsSpan(), 0));
        Assert.Throws<ArgumentException>(() => Pwma.Batch(source.AsSpan(), output.AsSpan(), -1));

        // Output must be same length as source
        Assert.Throws<ArgumentException>(() => Pwma.Batch(source.AsSpan(), wrongSizeOutput.AsSpan(), 3));
    }

    [Fact]
    public void Pwma_SpanCalc_MatchesTSeriesCalc()
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
        var tseriesResult = Pwma.Batch(series, 10);

        // Calculate with Span API
        Pwma.Batch(source.AsSpan(), output.AsSpan(), 10);

        // Compare results
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(tseriesResult[i].Value, output[i], 1e-10);
        }
    }

    [Fact]
    public void Pwma_SpanCalc_CalculatesCorrectly()
    {
        double[] source = [10, 20, 30];
        double[] output = new double[3];

        Pwma.Batch(source.AsSpan(), output.AsSpan(), 3);

        // PWMA(3) warmup:
        // i=0: 10 (1^2*10 / 1^2) = 10
        // i=1: (1^2*10 + 2^2*20) / (1^2 + 2^2) = (10 + 80) / 5 = 90/5 = 18
        // i=2: (1^2*10 + 2^2*20 + 3^2*30) / (1^2 + 2^2 + 3^2) = (10 + 80 + 270) / 14 = 360/14 = 25.714...
        Assert.Equal(10.0, output[0], 1e-10);
        Assert.Equal(18.0, output[1], 1e-10);
        Assert.Equal(360.0 / 14.0, output[2], 1e-10);
    }

    [Fact]
    public void Pwma_AllModes_ProduceSameResult()
    {
        // Arrange
        const int period = 10;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch Mode
        var batchSeries = Pwma.Batch(series, period);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var tValues = series.Values.ToArray();
        var spanInput = new ReadOnlySpan<double>(tValues);
        var spanOutput = new double[tValues.Length];
        Pwma.Batch(spanInput, spanOutput, period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Pwma(period);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Pwma(pubSource, period);
        for (int i = 0; i < series.Count; i++)
        {
            pubSource.Add(series[i]);
        }
        double eventingResult = eventingInd.Last.Value;

        // Assert
        Assert.Equal(expected, spanResult, precision: 9);
        Assert.Equal(expected, streamingResult, precision: 8);
        Assert.Equal(expected, eventingResult, precision: 8);
    }
}
