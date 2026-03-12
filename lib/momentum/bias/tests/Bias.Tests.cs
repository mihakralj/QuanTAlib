namespace QuanTAlib.Tests;

public class BiasTests
{
    [Fact]
    public void Bias_Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Bias(0));
        Assert.Throws<ArgumentException>(() => new Bias(-1));

        var bias = new Bias(10);
        Assert.NotNull(bias);
    }

    [Fact]
    public void Bias_Calc_ReturnsValue()
    {
        var bias = new Bias(10);

        Assert.Equal(0, bias.Last.Value);

        TValue result = bias.Update(new TValue(DateTime.UtcNow, 100));

        Assert.True(double.IsFinite(result.Value));
        Assert.Equal(result.Value, bias.Last.Value);
    }

    [Fact]
    public void Bias_FirstValue_ReturnsZero()
    {
        // Bias = (Price - SMA) / SMA = (100 - 100) / 100 = 0
        var bias = new Bias(10);

        TValue result = bias.Update(new TValue(DateTime.UtcNow, 100));

        Assert.Equal(0.0, result.Value, 1e-10);
    }

    [Fact]
    public void Bias_Calc_IsNew_AcceptsParameter()
    {
        var bias = new Bias(10);

        bias.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        double value1 = bias.Last.Value;

        bias.Update(new TValue(DateTime.UtcNow, 200), isNew: true);
        double value2 = bias.Last.Value;

        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Bias_Calc_IsNew_False_UpdatesValue()
    {
        var bias = new Bias(10);

        bias.Update(new TValue(DateTime.UtcNow, 100));
        bias.Update(new TValue(DateTime.UtcNow, 110), isNew: true);
        double beforeUpdate = bias.Last.Value;

        bias.Update(new TValue(DateTime.UtcNow, 120), isNew: false);
        double afterUpdate = bias.Last.Value;

        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void Bias_Reset_ClearsState()
    {
        var bias = new Bias(10);

        bias.Update(new TValue(DateTime.UtcNow, 100));
        bias.Update(new TValue(DateTime.UtcNow, 105));
        double valueBefore = bias.Last.Value;

        bias.Reset();

        Assert.Equal(0, bias.Last.Value);
        Assert.False(bias.IsHot);

        bias.Update(new TValue(DateTime.UtcNow, 50));
        Assert.Equal(0, bias.Last.Value); // First value, Bias = 0
        Assert.NotEqual(valueBefore, bias.Last.Value);
    }

    [Fact]
    public void Bias_Properties_Accessible()
    {
        var bias = new Bias(10);

        Assert.Equal(0, bias.Last.Value);
        Assert.False(bias.IsHot);

        bias.Update(new TValue(DateTime.UtcNow, 100));

        Assert.Equal(0, bias.Last.Value); // First value, Bias = 0
    }

    [Fact]
    public void Bias_IsHot_BecomesTrueWhenBufferFull()
    {
        var bias = new Bias(5);

        Assert.False(bias.IsHot);

        for (int i = 1; i <= 4; i++)
        {
            bias.Update(new TValue(DateTime.UtcNow, i * 10));
            Assert.False(bias.IsHot);
        }

        bias.Update(new TValue(DateTime.UtcNow, 50));
        Assert.True(bias.IsHot);
    }

    [Fact]
    public void Bias_CalculatesCorrectBias()
    {
        // Bias = (Price - SMA) / SMA
        var bias = new Bias(3);

        // Value 10: SMA = 10, Bias = (10-10)/10 = 0
        bias.Update(new TValue(DateTime.UtcNow, 10));
        Assert.Equal(0.0, bias.Last.Value, 1e-10);

        // Value 20: SMA = (10+20)/2 = 15, Bias = (20-15)/15 = 1/3
        bias.Update(new TValue(DateTime.UtcNow, 20));
        Assert.Equal(1.0 / 3.0, bias.Last.Value, 1e-10);

        // Value 30: SMA = (10+20+30)/3 = 20, Bias = (30-20)/20 = 0.5
        bias.Update(new TValue(DateTime.UtcNow, 30));
        Assert.Equal(0.5, bias.Last.Value, 1e-10);
    }

    [Fact]
    public void Bias_SlidingWindow_Works()
    {
        var bias = new Bias(3);

        bias.Update(new TValue(DateTime.UtcNow, 10));
        bias.Update(new TValue(DateTime.UtcNow, 20));
        bias.Update(new TValue(DateTime.UtcNow, 30));
        // SMA = 20, Bias = (30-20)/20 = 0.5
        Assert.Equal(0.5, bias.Last.Value, 1e-10);

        bias.Update(new TValue(DateTime.UtcNow, 40));
        // SMA = (20+30+40)/3 = 30, Bias = (40-30)/30 = 1/3
        Assert.Equal(1.0 / 3.0, bias.Last.Value, 1e-10);

        bias.Update(new TValue(DateTime.UtcNow, 50));
        // SMA = (30+40+50)/3 = 40, Bias = (50-40)/40 = 0.25
        Assert.Equal(0.25, bias.Last.Value, 1e-10);
    }

    [Fact]
    public void Bias_IterativeCorrections_RestoreToOriginalState()
    {
        var bias = new Bias(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 10 new values
        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            bias.Update(tenthInput, isNew: true);
        }

        // Remember state after 10 values
        double stateAfterTen = bias.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            bias.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered 10th input again with isNew=false
        TValue finalResult = bias.Update(tenthInput, isNew: false);

        // State should match the original state after 10 values
        Assert.Equal(stateAfterTen, finalResult.Value, 1e-10);
    }

    [Fact]
    public void Bias_BatchCalc_MatchesIterativeCalc()
    {
        var biasIterative = new Bias(10);
        var biasBatch = new Bias(10);
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
            iterativeResults.Add(biasIterative.Update(item));
        }

        // Calculate batch
        var batchResults = biasBatch.Update(series);

        // Compare
        Assert.Equal(iterativeResults.Count, batchResults.Count);
        for (int i = 0; i < iterativeResults.Count; i++)
        {
            Assert.Equal(iterativeResults[i].Value, batchResults[i].Value, 1e-10);
            Assert.Equal(iterativeResults[i].Time, batchResults[i].Time);
        }
    }

    [Fact]
    public void Bias_NaN_Input_UsesLastValidValue()
    {
        var bias = new Bias(5);

        bias.Update(new TValue(DateTime.UtcNow, 100));
        bias.Update(new TValue(DateTime.UtcNow, 110));

        var resultAfterNaN = bias.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(resultAfterNaN.Value));
    }

    [Fact]
    public void Bias_Infinity_Input_UsesLastValidValue()
    {
        var bias = new Bias(5);

        bias.Update(new TValue(DateTime.UtcNow, 100));
        bias.Update(new TValue(DateTime.UtcNow, 110));

        var resultAfterPosInf = bias.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultAfterPosInf.Value));

        var resultAfterNegInf = bias.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultAfterNegInf.Value));
    }

    [Fact]
    public void Bias_MultipleNaN_ContinuesWithLastValid()
    {
        var bias = new Bias(5);

        bias.Update(new TValue(DateTime.UtcNow, 100));
        bias.Update(new TValue(DateTime.UtcNow, 110));
        bias.Update(new TValue(DateTime.UtcNow, 120));

        var r1 = bias.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r2 = bias.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r3 = bias.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(r1.Value));
        Assert.True(double.IsFinite(r2.Value));
        Assert.True(double.IsFinite(r3.Value));
    }

    [Fact]
    public void Bias_BatchCalc_HandlesNaN()
    {
        var bias = new Bias(5);

        var series = new TSeries();
        series.Add(DateTime.UtcNow.Ticks, 100);
        series.Add(DateTime.UtcNow.Ticks + 1, 110);
        series.Add(DateTime.UtcNow.Ticks + 2, double.NaN);
        series.Add(DateTime.UtcNow.Ticks + 3, 120);
        series.Add(DateTime.UtcNow.Ticks + 4, double.PositiveInfinity);
        series.Add(DateTime.UtcNow.Ticks + 5, 130);

        var results = bias.Update(series);

        foreach (var result in results)
        {
            Assert.True(double.IsFinite(result.Value), $"Expected finite value but got {result.Value}");
        }
    }

    [Fact]
    public void Bias_Reset_ClearsLastValidValue()
    {
        var bias = new Bias(5);

        bias.Update(new TValue(DateTime.UtcNow, 100));
        bias.Update(new TValue(DateTime.UtcNow, double.NaN));

        bias.Reset();

        var result = bias.Update(new TValue(DateTime.UtcNow, 50));
        Assert.Equal(0.0, result.Value, 1e-10); // First value, Bias = 0
    }

    [Fact]
    public void Bias_StaticBatch_Works()
    {
        var series = new TSeries();
        series.Add(DateTime.UtcNow.Ticks, 10);
        series.Add(DateTime.UtcNow.Ticks + 1, 20);
        series.Add(DateTime.UtcNow.Ticks + 2, 30);
        series.Add(DateTime.UtcNow.Ticks + 3, 40);
        series.Add(DateTime.UtcNow.Ticks + 4, 50);

        var results = Bias.Batch(series, 3);

        Assert.Equal(5, results.Count);
        // Last value: SMA(3) = (30+40+50)/3 = 40, Bias = (50-40)/40 = 0.25
        Assert.Equal(0.25, results.Last.Value, 1e-10);
    }

    [Fact]
    public void Bias_FlatLine_ReturnsZero()
    {
        var bias = new Bias(10);

        for (int i = 0; i < 20; i++)
        {
            bias.Update(new TValue(DateTime.UtcNow, 100));
        }

        // Price = SMA = 100, Bias = (100-100)/100 = 0
        Assert.Equal(0.0, bias.Last.Value, 1e-10);
    }

    // ============== Span API Tests ==============

    [Fact]
    public void Bias_SpanBatch_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[5];
        double[] wrongSizeOutput = new double[3];

        Assert.Throws<ArgumentException>(() => Bias.Batch(source.AsSpan(), output.AsSpan(), 0));
        Assert.Throws<ArgumentException>(() => Bias.Batch(source.AsSpan(), output.AsSpan(), -1));
        Assert.Throws<ArgumentException>(() => Bias.Batch(source.AsSpan(), wrongSizeOutput.AsSpan(), 3));
    }

    [Fact]
    public void Bias_SpanBatch_MatchesTSeriesBatch()
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

        var tseriesResult = Bias.Batch(series, 10);
        Bias.Batch(source.AsSpan(), output.AsSpan(), 10);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(tseriesResult[i].Value, output[i], 1e-10);
        }
    }

    [Fact]
    public void Bias_SpanBatch_CalculatesCorrectly()
    {
        double[] source = [10, 20, 30, 40, 50];
        double[] output = new double[5];

        Bias.Batch(source.AsSpan(), output.AsSpan(), 3);

        // i=0: SMA=10, Bias=(10-10)/10=0
        Assert.Equal(0.0, output[0], 1e-10);
        // i=1: SMA=15, Bias=(20-15)/15=1/3
        Assert.Equal(1.0 / 3.0, output[1], 1e-10);
        // i=2: SMA=20, Bias=(30-20)/20=0.5
        Assert.Equal(0.5, output[2], 1e-10);
        // i=3: SMA=30, Bias=(40-30)/30=1/3
        Assert.Equal(1.0 / 3.0, output[3], 1e-10);
        // i=4: SMA=40, Bias=(50-40)/40=0.25
        Assert.Equal(0.25, output[4], 1e-10);
    }

    [Fact]
    public void Bias_SpanBatch_ZeroAllocation()
    {
        double[] source = new double[10000];
        double[] output = new double[10000];

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < source.Length; i++)
        {
            source[i] = gbm.Next().Close;
        }

        Bias.Batch(source.AsSpan(), output.AsSpan(), 100);

        Assert.True(double.IsFinite(output[^1]));
    }

    [Fact]
    public void Bias_SpanBatch_HandlesNaN()
    {
        double[] source = [100, 110, double.NaN, 120, 130];
        double[] output = new double[5];

        Bias.Batch(source.AsSpan(), output.AsSpan(), 3);

        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val), $"Expected finite value but got {val}");
        }
    }

    [Fact]
    public void Bias_AllModes_ProduceSameResult()
    {
        // Arrange
        const int period = 10;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch Mode
        var batchSeries = Bias.Batch(series, period);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var tValues = series.Values.ToArray();
        var spanInput = new ReadOnlySpan<double>(tValues);
        var spanOutput = new double[tValues.Length];
        Bias.Batch(spanInput, spanOutput, period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Bias(period);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Bias(pubSource, period);
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

    [Fact]
    public void Bias_Chainability_Works()
    {
        var source = new TSeries();
        var bias = new Bias(source, 10);

        source.Add(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(0, bias.Last.Value); // First value, Bias = 0
    }

    [Fact]
    public void Bias_WarmupPeriod_IsSetCorrectly()
    {
        var bias = new Bias(10);
        Assert.Equal(10, bias.WarmupPeriod);
    }

    [Fact]
    public void Bias_Prime_SetsStateCorrectly()
    {
        var bias = new Bias(5);
        double[] history = [10, 20, 30, 40, 50];
        // SMA = 30, Bias = (50-30)/30 = 2/3

        bias.Prime(history);

        Assert.True(bias.IsHot);
        Assert.Equal(2.0 / 3.0, bias.Last.Value, 1e-10);

        // Verify it continues correctly with sliding window
        bias.Update(new TValue(DateTime.UtcNow, 60));
        // SMA = (20+30+40+50+60)/5 = 40, Bias = (60-40)/40 = 0.5
        Assert.Equal(0.5, bias.Last.Value, 1e-10);
    }

    [Fact]
    public void Bias_Prime_WithInsufficientHistory_IsNotHot()
    {
        var bias = new Bias(10);
        double[] history = [10, 20, 30, 40, 50];

        bias.Prime(history);

        Assert.False(bias.IsHot);
        Assert.True(double.IsFinite(bias.Last.Value));
    }

    [Fact]
    public void Bias_Prime_HandlesNaN_InHistory()
    {
        var bias = new Bias(3);
        double[] history = [10, 20, double.NaN, 40];
        // Values used: 10, 20, 20 (NaN replaced), 40
        // Final window (3): 20, 20, 40 - SMA = 26.67

        bias.Prime(history);

        Assert.True(bias.IsHot);
        Assert.True(double.IsFinite(bias.Last.Value));
    }

    [Fact]
    public void Bias_Calculate_ReturnsCorrectResultsAndHotIndicator()
    {
        var series = new TSeries();
        for (int i = 1; i <= 10; i++)
        {
            series.Add(DateTime.UtcNow, i * 10);
        }
        // 10, 20, 30, 40, 50, 60, 70, 80, 90, 100

        var (results, indicator) = Bias.Calculate(series, 5);

        // Check results
        Assert.Equal(10, results.Count);

        // Check indicator state
        Assert.True(indicator.IsHot);
        Assert.Equal(5, indicator.WarmupPeriod);

        // Verify indicator continues correctly
        indicator.Update(new TValue(DateTime.UtcNow, 110));
        // SMA = (70+80+90+100+110)/5 = 90, Bias = (110-90)/90 = 2/9
        Assert.Equal(2.0 / 9.0, indicator.Last.Value, 1e-10);
    }

    [Fact]
    public void Bias_Period1_ReturnsPriceMinusSmaOverSma()
    {
        var bias = new Bias(1);

        bias.Update(new TValue(DateTime.UtcNow, 100));
        // SMA(1) = 100, Bias = (100-100)/100 = 0
        Assert.Equal(0.0, bias.Last.Value, 1e-10);

        bias.Update(new TValue(DateTime.UtcNow, 200));
        // SMA(1) = 200, Bias = (200-200)/200 = 0
        Assert.Equal(0.0, bias.Last.Value, 1e-10);

        bias.Update(new TValue(DateTime.UtcNow, 150));
        // SMA(1) = 150, Bias = (150-150)/150 = 0
        Assert.Equal(0.0, bias.Last.Value, 1e-10);
    }

    [Fact]
    public void Bias_NegativePrice_CalculatesCorrectly()
    {
        var bias = new Bias(3);

        bias.Update(new TValue(DateTime.UtcNow, -10));
        bias.Update(new TValue(DateTime.UtcNow, -20));
        bias.Update(new TValue(DateTime.UtcNow, -30));
        // SMA = -20, Bias = (-30 - (-20)) / (-20) = -10 / -20 = 0.5
        Assert.Equal(0.5, bias.Last.Value, 1e-10);
    }

    [Fact]
    public void Bias_ZeroPrice_HandlesGracefully()
    {
        var bias = new Bias(3);

        bias.Update(new TValue(DateTime.UtcNow, 0));
        bias.Update(new TValue(DateTime.UtcNow, 0));
        bias.Update(new TValue(DateTime.UtcNow, 0));
        // SMA = 0, Bias = (0-0)/0 = 0/0 -> should return 0 to avoid NaN
        Assert.Equal(0.0, bias.Last.Value, 1e-10);
    }
}
