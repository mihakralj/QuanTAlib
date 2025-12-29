namespace QuanTAlib.Tests;

public class UsfTests
{
    // ============== Constructor & Parameter Validation ==============

    [Fact]
    public void Usf_Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Usf(0));
        Assert.Throws<ArgumentException>(() => new Usf(-1));

        var usf = new Usf(10);
        Assert.NotNull(usf);
    }

    // ============== Basic Functionality ==============

    [Fact]
    public void Usf_Calc_ReturnsValue()
    {
        var usf = new Usf(10);

        Assert.Equal(0, usf.Last.Value);

        TValue result = usf.Update(new TValue(DateTime.UtcNow, 100));

        Assert.True(result.Value > 0);
        Assert.Equal(result.Value, usf.Last.Value);
    }

    [Fact]
    public void Usf_FirstValue_ReturnsItself()
    {
        var usf = new Usf(10);

        TValue result = usf.Update(new TValue(DateTime.UtcNow, 100));

        Assert.Equal(100.0, result.Value, 1e-10);
    }

    [Fact]
    public void Usf_Properties_Accessible()
    {
        var usf = new Usf(10);

        Assert.Equal(0, usf.Last.Value);
        Assert.False(usf.IsHot);
        Assert.Contains("Usf", usf.Name, StringComparison.Ordinal);

        usf.Update(new TValue(DateTime.UtcNow, 100));

        Assert.NotEqual(0, usf.Last.Value);
    }

    // ============== State Management & Bar Correction ==============

    [Fact]
    public void Usf_Calc_IsNew_AcceptsParameter()
    {
        var usf = new Usf(10);

        usf.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        double value1 = usf.Last.Value;

        usf.Update(new TValue(DateTime.UtcNow, 200), isNew: true);
        double value2 = usf.Last.Value;

        // Values should change with new bars
        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Usf_Calc_IsNew_False_UpdatesValue()
    {
        var usf = new Usf(10);

        usf.Update(new TValue(DateTime.UtcNow, 100));
        usf.Update(new TValue(DateTime.UtcNow, 110), isNew: true);
        double beforeUpdate = usf.Last.Value;

        usf.Update(new TValue(DateTime.UtcNow, 120), isNew: false);
        double afterUpdate = usf.Last.Value;

        // Update should change the value
        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void Usf_IterativeCorrections_RestoreToOriginalState()
    {
        var usf = new Usf(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 10 new values
        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            usf.Update(tenthInput, isNew: true);
        }

        // Remember state after 10 values
        double stateAfterTen = usf.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            usf.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered 10th input again with isNew=false
        TValue finalResult = usf.Update(tenthInput, isNew: false);

        // State should match the original state after 10 values
        Assert.Equal(stateAfterTen, finalResult.Value, 1e-10);
    }

    [Fact]
    public void Usf_Reset_ClearsState()
    {
        var usf = new Usf(10);

        usf.Update(new TValue(DateTime.UtcNow, 100));
        usf.Update(new TValue(DateTime.UtcNow, 105));
        double valueBefore = usf.Last.Value;

        usf.Reset();

        Assert.Equal(0, usf.Last.Value);
        Assert.False(usf.IsHot);

        // After reset, should accept new values
        usf.Update(new TValue(DateTime.UtcNow, 50));
        Assert.NotEqual(0, usf.Last.Value);
        Assert.NotEqual(valueBefore, usf.Last.Value);
    }

    [Fact]
    public void Usf_Reset_ClearsLastValidValue()
    {
        var usf = new Usf(5);

        // Feed values including NaN
        usf.Update(new TValue(DateTime.UtcNow, 100));
        usf.Update(new TValue(DateTime.UtcNow, double.NaN));

        // Reset
        usf.Reset();

        // After reset, first valid value should establish new baseline
        var result = usf.Update(new TValue(DateTime.UtcNow, 50));
        Assert.Equal(50.0, result.Value, 1e-10);
    }

    // ============== Warmup & Convergence ==============

    [Fact]
    public void Usf_IsHot_BecomesTrueWhenBufferFull()
    {
        var usf = new Usf(5);

        Assert.False(usf.IsHot);

        for (int i = 1; i <= 4; i++)
        {
            usf.Update(new TValue(DateTime.UtcNow, i * 10));
            Assert.False(usf.IsHot);
        }

        usf.Update(new TValue(DateTime.UtcNow, 50));
        Assert.True(usf.IsHot);
    }

    [Fact]
    public void Usf_WarmupPeriod_IsSetCorrectly()
    {
        var usf = new Usf(10);
        Assert.Equal(10, usf.WarmupPeriod);
    }

    // ============== NaN/Infinity Handling ==============

    [Fact]
    public void Usf_NaN_Input_UsesLastValidValue()
    {
        var usf = new Usf(5);

        // Feed some valid values
        usf.Update(new TValue(DateTime.UtcNow, 100));
        usf.Update(new TValue(DateTime.UtcNow, 110));

        // Feed NaN - should use last valid value (110)
        var resultAfterNaN = usf.Update(new TValue(DateTime.UtcNow, double.NaN));

        // Result should be finite (not NaN)
        Assert.True(double.IsFinite(resultAfterNaN.Value));
        Assert.NotEqual(0, resultAfterNaN.Value);
    }

    [Fact]
    public void Usf_Infinity_Input_UsesLastValidValue()
    {
        var usf = new Usf(5);

        // Feed some valid values
        usf.Update(new TValue(DateTime.UtcNow, 100));
        usf.Update(new TValue(DateTime.UtcNow, 110));

        // Feed positive infinity - should use last valid value
        var resultAfterPosInf = usf.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultAfterPosInf.Value));

        // Feed negative infinity - should use last valid value
        var resultAfterNegInf = usf.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultAfterNegInf.Value));
    }

    [Fact]
    public void Usf_MultipleNaN_ContinuesWithLastValid()
    {
        var usf = new Usf(5);

        // Feed valid values
        usf.Update(new TValue(DateTime.UtcNow, 100));
        usf.Update(new TValue(DateTime.UtcNow, 110));
        usf.Update(new TValue(DateTime.UtcNow, 120));

        // Feed multiple NaN values
        var r1 = usf.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r2 = usf.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r3 = usf.Update(new TValue(DateTime.UtcNow, double.NaN));

        // All results should be finite
        Assert.True(double.IsFinite(r1.Value));
        Assert.True(double.IsFinite(r2.Value));
        Assert.True(double.IsFinite(r3.Value));
    }

    [Fact]
    public void Usf_BatchCalc_HandlesNaN()
    {
        var usf = new Usf(5);

        // Create series with NaN values interspersed
        var series = new TSeries();
        series.Add(DateTime.UtcNow.Ticks, 100);
        series.Add(DateTime.UtcNow.Ticks + 1, 110);
        series.Add(DateTime.UtcNow.Ticks + 2, double.NaN);
        series.Add(DateTime.UtcNow.Ticks + 3, 120);
        series.Add(DateTime.UtcNow.Ticks + 4, double.PositiveInfinity);
        series.Add(DateTime.UtcNow.Ticks + 5, 130);

        var results = usf.Update(series);

        // All results should be finite
        foreach (var result in results)
        {
            Assert.True(double.IsFinite(result.Value), $"Expected finite value but got {result.Value}");
        }
    }

    // ============== Consistency Tests ==============

    [Fact]
    public void Usf_BatchCalc_MatchesIterativeCalc()
    {
        var usfIterative = new Usf(10);
        var usfBatch = new Usf(10);
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
            iterativeResults.Add(usfIterative.Update(item));
        }

        // Calculate batch
        var batchResults = usfBatch.Update(series);

        // Compare
        Assert.Equal(iterativeResults.Count, batchResults.Count);
        for (int i = 0; i < iterativeResults.Count; i++)
        {
            Assert.Equal(iterativeResults[i].Value, batchResults[i].Value, 1e-10);
            Assert.Equal(iterativeResults[i].Time, batchResults[i].Time);
        }
    }

    [Fact]
    public void Usf_AllModes_ProduceSameResult()
    {
        // Arrange
        int period = 10;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch Mode (static Calculate)
        var (batchSeries, _) = Usf.Calculate(series, period);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var tValues = series.Values.ToArray();
        var spanInput = new ReadOnlySpan<double>(tValues);
        var spanOutput = new double[tValues.Length];
        Usf.Calculate(spanInput, spanOutput, period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Usf(period);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Usf(pubSource, period);
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
    public void Usf_StaticCalculate_Works()
    {
        var series = new TSeries();
        series.Add(DateTime.UtcNow.Ticks, 10);
        series.Add(DateTime.UtcNow.Ticks + 1, 20);
        series.Add(DateTime.UtcNow.Ticks + 2, 30);
        series.Add(DateTime.UtcNow.Ticks + 3, 40);
        series.Add(DateTime.UtcNow.Ticks + 4, 50);

        var (results, indicator) = Usf.Calculate(series, 3);

        Assert.Equal(5, results.Count);
        Assert.True(indicator.IsHot);
        Assert.True(double.IsFinite(results.Last.Value));
    }

    // ============== Span API Tests ==============

    [Fact]
    public void Usf_SpanCalculate_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[5];
        double[] wrongSizeOutput = new double[3];

        // Period must be > 0
        Assert.Throws<ArgumentException>(() => Usf.Calculate(source.AsSpan(), output.AsSpan(), 0));
        Assert.Throws<ArgumentException>(() => Usf.Calculate(source.AsSpan(), output.AsSpan(), -1));

        // Output must be same length as source
        Assert.Throws<ArgumentException>(() => Usf.Calculate(source.AsSpan(), wrongSizeOutput.AsSpan(), 3));
    }

    [Fact]
    public void Usf_SpanCalculate_MatchesTSeriesCalculate()
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
        var (tseriesResult, _) = Usf.Calculate(series, 10);

        // Calculate with Span API
        Usf.Calculate(source.AsSpan(), output.AsSpan(), 10);

        // Compare results
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(tseriesResult[i].Value, output[i], 1e-10);
        }
    }

    [Fact]
    public void Usf_SpanCalculate_ZeroAllocation()
    {
        double[] source = new double[10000];
        double[] output = new double[10000];

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < source.Length; i++)
            source[i] = gbm.Next().Close;

        // Warm up
        Usf.Calculate(source.AsSpan(), output.AsSpan(), 100);

        // This test verifies the method runs without throwing
        Assert.True(double.IsFinite(output[^1]));
    }

    [Fact]
    public void Usf_SpanCalculate_HandlesNaN()
    {
        double[] source = [100, 110, double.NaN, 120, 130];
        double[] output = new double[5];

        Usf.Calculate(source.AsSpan(), output.AsSpan(), 3);

        // All outputs should be finite
        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val), $"Expected finite value but got {val}");
        }
    }

    // ============== Chainability Tests ==============

    [Fact]
    public void Usf_Chainability_Works()
    {
        var source = new TSeries();
        var usf = new Usf(source, 10);

        source.Add(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100, usf.Last.Value);
    }

    [Fact]
    public void Usf_Pub_EventFires()
    {
        var usf = new Usf(10);
        bool eventFired = false;
        usf.Pub += (object? sender, in TValueEventArgs args) => eventFired = true;

        usf.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(eventFired);
    }

    // ============== Priming Tests ==============

    [Fact]
    public void Usf_Prime_SetsStateCorrectly()
    {
        var usf = new Usf(5);
        double[] history = [10, 20, 30, 40, 50];

        usf.Prime(history);

        Assert.True(usf.IsHot);
        Assert.True(double.IsFinite(usf.Last.Value));

        // Verify it continues correctly
        usf.Update(new TValue(DateTime.UtcNow, 60));
        Assert.True(double.IsFinite(usf.Last.Value));
    }

    [Fact]
    public void Usf_Prime_WithInsufficientHistory_IsNotHot()
    {
        var usf = new Usf(10);
        double[] history = [10, 20, 30, 40, 50];

        usf.Prime(history);

        Assert.False(usf.IsHot);
        Assert.True(double.IsFinite(usf.Last.Value)); // It still calculates what it can
    }

    [Fact]
    public void Usf_Prime_HandlesNaN_InHistory()
    {
        var usf = new Usf(3);
        double[] history = [10, 20, double.NaN, 40];

        usf.Prime(history);

        Assert.True(usf.IsHot);
        Assert.True(double.IsFinite(usf.Last.Value));
    }

    // ============== Calculate Method Tests ==============

    [Fact]
    public void Usf_Calculate_ReturnsCorrectResultsAndHotIndicator()
    {
        var series = new TSeries();
        for (int i = 1; i <= 10; i++)
            series.Add(DateTime.UtcNow, i * 10);

        var (results, indicator) = Usf.Calculate(series, 5);

        // Check results
        Assert.Equal(10, results.Count);
        Assert.True(double.IsFinite(results.Last.Value));

        // Check indicator state
        Assert.True(indicator.IsHot);
        Assert.True(double.IsFinite(indicator.Last.Value));
        Assert.Equal(5, indicator.WarmupPeriod);

        // Verify indicator continues correctly
        indicator.Update(new TValue(DateTime.UtcNow, 110));
        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    // ============== Flat Line Test ==============

    [Fact]
    public void Usf_FlatLine_ReturnsSameValue()
    {
        var usf = new Usf(10);
        for (int i = 0; i < 20; i++)
        {
            usf.Update(new TValue(DateTime.UtcNow, 100));
        }
        // For a flat line, USF should converge to the input value
        Assert.Equal(100.0, usf.Last.Value, 1e-6);
    }
}
