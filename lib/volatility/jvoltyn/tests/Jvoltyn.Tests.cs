namespace QuanTAlib.Tests;

public class JvoltynTests
{
    private const double Tolerance = 1e-9;

    private static TSeries GenerateTestData(int count = 100)
    {
        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = new TSeries(count);
        for (int i = 0; i < bars.Count; i++)
        {
            series.Add(new TValue(bars[i].Time, bars[i].Close));
        }
        return series;
    }

    // ============== Constructor & Parameter Validation ==============

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Jvoltyn(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Jvoltyn(-1));

        var jvoltyn = new Jvoltyn(10);
        Assert.NotNull(jvoltyn);
    }

    [Fact]
    public void Constructor_SetsCorrectName()
    {
        var jvoltyn = new Jvoltyn(7);
        Assert.Equal("Jvoltyn(7)", jvoltyn.Name);
        Assert.True(jvoltyn.WarmupPeriod > 0);

        var jvoltyn2 = new Jvoltyn(14);
        Assert.Equal("Jvoltyn(14)", jvoltyn2.Name);
    }

    // ============== Basic Functionality ==============

    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var jvoltyn = new Jvoltyn(10);
        var series = GenerateTestData(100);

        foreach (var value in series)
        {
            jvoltyn.Update(value);
        }

        Assert.True(double.IsFinite(jvoltyn.Last.Value));
    }

    [Fact]
    public void Calc_ReturnsNormalizedValue()
    {
        var jvoltyn = new Jvoltyn(10);
        var input = new TValue(DateTime.UtcNow, 100.0);

        Assert.InRange(jvoltyn.Last.Value, -Tolerance, Tolerance); // Initially zero

        TValue result = jvoltyn.Update(input);

        // First value should be 0 (normalized from d=1)
        Assert.Equal(0.0, result.Value, Tolerance);
        Assert.Equal(result.Value, jvoltyn.Last.Value, Tolerance);
    }

    [Fact]
    public void FirstValue_ReturnsZero()
    {
        var jvoltyn = new Jvoltyn(10);
        var input = new TValue(DateTime.UtcNow, 100.0);

        TValue result = jvoltyn.Update(input);

        // First bar returns 0 (normalized minimum volatility)
        Assert.Equal(0.0, result.Value, Tolerance);
    }

    [Fact]
    public void OutputRange_IsZeroToHundred()
    {
        var jvoltyn = new Jvoltyn(10);
        var series = GenerateTestData(500);

        foreach (var value in series)
        {
            var result = jvoltyn.Update(value);
            // Output should be in [0, 100] range
            Assert.True(result.Value >= 0.0 - Tolerance, $"Value {result.Value} below 0");
            Assert.True(result.Value <= 100.0 + Tolerance, $"Value {result.Value} above 100");
        }
    }

    [Fact]
    public void Properties_Accessible()
    {
        var jvoltyn = new Jvoltyn(10);

        Assert.InRange(jvoltyn.Last.Value, -Tolerance, Tolerance); // Initially zero
        Assert.False(jvoltyn.IsHot);
        Assert.Contains("Jvoltyn", jvoltyn.Name, StringComparison.Ordinal);
        Assert.True(jvoltyn.WarmupPeriod > 0);

        var input = new TValue(DateTime.UtcNow, 100.0);
        jvoltyn.Update(input);

        // After first bar, value should be 0 (minimum volatility normalized)
        Assert.Equal(0.0, jvoltyn.Last.Value, Tolerance);
    }

    [Fact]
    public void BandProperties_Accessible()
    {
        var jvoltyn = new Jvoltyn(10);
        var input = new TValue(DateTime.UtcNow, 100.0);

        jvoltyn.Update(input);

        // After first bar, bands should be initialized to the input value
        Assert.Equal(100.0, jvoltyn.UpperBand, Tolerance);
        Assert.Equal(100.0, jvoltyn.LowerBand, Tolerance);
    }

    [Fact]
    public void RawVolatility_Accessible()
    {
        var jvoltyn = new Jvoltyn(10);
        var input = new TValue(DateTime.UtcNow, 100.0);

        jvoltyn.Update(input);

        // RawVolatility should be 1.0 (minimum) after first bar
        Assert.Equal(1.0, jvoltyn.RawVolatility, Tolerance);
    }

    // ============== State Management & Bar Correction ==============

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var jvoltyn = new Jvoltyn(10);
        var series = GenerateTestData(50);

        // Feed enough values to build up volatility history
        for (int i = 0; i < 49; i++)
        {
            jvoltyn.Update(series[i], isNew: true);
        }
        double valueBefore = jvoltyn.Last.Value;

        // Add one more value with isNew=true
        jvoltyn.Update(series[49], isNew: true);
        double valueAfter = jvoltyn.Last.Value;

        // Both should be valid volatility values
        Assert.True(double.IsFinite(valueBefore));
        Assert.True(double.IsFinite(valueAfter));
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var jvoltyn = new Jvoltyn(10);
        var series = GenerateTestData(50);

        // Feed enough bars to have meaningful volatility
        for (int i = 0; i < 49; i++)
        {
            jvoltyn.Update(series[i], isNew: true);
        }

        // Add one more value with isNew=true
        jvoltyn.Update(series[49], isNew: true);
        double beforeUpdate = jvoltyn.Last.Value;

        // Update same bar with different value (isNew=false)
        var modifiedInput = new TValue(series[49].Time, series[49].Value + 50.0);
        jvoltyn.Update(modifiedInput, isNew: false);
        double afterUpdate = jvoltyn.Last.Value;

        // Values should be different after the correction
        Assert.True(Math.Abs(beforeUpdate - afterUpdate) > Tolerance);
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var jvoltyn = new Jvoltyn(10);
        var series = GenerateTestData(100);

        // Feed first 99
        for (int i = 0; i < 99; i++)
        {
            jvoltyn.Update(series[i]);
        }

        // Update with 100th point (isNew=true)
        jvoltyn.Update(series[99], true);

        // Update with modified 100th point (isNew=false)
        var modifiedInput = new TValue(series[99].Time, series[99].Value + 50.0);
        double val2 = jvoltyn.Update(modifiedInput, false).Value;

        // Create new instance and feed up to modified
        var jvoltyn2 = new Jvoltyn(10);
        for (int i = 0; i < 99; i++)
        {
            jvoltyn2.Update(series[i]);
        }
        double val3 = jvoltyn2.Update(modifiedInput, true).Value;

        Assert.Equal(val3, val2, Tolerance);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var jvoltyn = new Jvoltyn(5);
        var series = GenerateTestData(20);

        // Feed 10 new values
        TValue tenthValue = default;
        for (int i = 0; i < 10; i++)
        {
            tenthValue = series[i];
            jvoltyn.Update(tenthValue, isNew: true);
        }

        // Remember state after 10 values
        double stateAfterTen = jvoltyn.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 10; i < 19; i++)
        {
            jvoltyn.Update(series[i], isNew: false);
        }

        // Feed the remembered 10th value again with isNew=false
        TValue finalResult = jvoltyn.Update(tenthValue, isNew: false);

        // State should match the original state after 10 values
        Assert.Equal(stateAfterTen, finalResult.Value, Tolerance);
    }

    [Fact]
    public void Reset_Works()
    {
        var jvoltyn = new Jvoltyn(10);
        var series = GenerateTestData(50);

        foreach (var value in series)
        {
            jvoltyn.Update(value);
        }

        jvoltyn.Reset();
        Assert.InRange(jvoltyn.Last.Value, -Tolerance, Tolerance); // Reset to zero
        Assert.False(jvoltyn.IsHot);

        // After reset, first value should be 0 (minimum normalized volatility)
        jvoltyn.Update(series[0]);
        Assert.Equal(0.0, jvoltyn.Last.Value, Tolerance);
    }

    // ============== Warmup & Convergence ==============

    [Fact]
    public void IsHot_BecomesTrueAfterWarmup()
    {
        var jvoltyn = new Jvoltyn(5);

        Assert.False(jvoltyn.IsHot);

        var series = GenerateTestData(200);

        int steps = 0;
        while (!jvoltyn.IsHot && steps < series.Count)
        {
            jvoltyn.Update(series[steps]);
            steps++;
        }

        Assert.True(jvoltyn.IsHot);
        Assert.True(steps > 0);
    }

    [Fact]
    public void WarmupPeriod_IsPositive()
    {
        var jvoltyn = new Jvoltyn(10);
        Assert.True(jvoltyn.WarmupPeriod > 0);

        var jvoltyn2 = new Jvoltyn(20);
        Assert.True(jvoltyn2.WarmupPeriod > 0);

        // WarmupPeriod should increase with the period parameter
        Assert.True(jvoltyn2.WarmupPeriod >= jvoltyn.WarmupPeriod);
    }

    // ============== NaN/Infinity Handling ==============

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var jvoltyn = new Jvoltyn(5);

        var input1 = new TValue(DateTime.UtcNow, 100.0);
        jvoltyn.Update(input1);

        var input2 = new TValue(DateTime.UtcNow.AddMinutes(1), 110.0);
        jvoltyn.Update(input2);

        // Feed NaN value
        var inputWithNaN = new TValue(DateTime.UtcNow.AddMinutes(2), double.NaN);
        var resultAfterNaN = jvoltyn.Update(inputWithNaN);

        // Result should be finite
        Assert.True(double.IsFinite(resultAfterNaN.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var jvoltyn = new Jvoltyn(5);

        var input1 = new TValue(DateTime.UtcNow, 100.0);
        jvoltyn.Update(input1);

        var input2 = new TValue(DateTime.UtcNow.AddMinutes(1), 110.0);
        jvoltyn.Update(input2);

        // Feed Infinity value
        var inputWithInf = new TValue(DateTime.UtcNow.AddMinutes(2), double.PositiveInfinity);
        var resultAfterInf = jvoltyn.Update(inputWithInf);

        // Result should be finite
        Assert.True(double.IsFinite(resultAfterInf.Value));
    }

    [Fact]
    public void BatchNaN_Safe()
    {
        var jvoltyn = new Jvoltyn(5);
        var series = GenerateTestData(20);

        // Feed some values
        for (int i = 0; i < 10; i++)
        {
            jvoltyn.Update(series[i]);
        }

        // Feed multiple NaN values
        for (int i = 0; i < 5; i++)
        {
            var nanInput = new TValue(DateTime.UtcNow.AddMinutes(10 + i), double.NaN);
            var result = jvoltyn.Update(nanInput);
            Assert.True(double.IsFinite(result.Value));
        }
    }

    // ============== Consistency Tests ==============

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        var jvoltynIterative = new Jvoltyn(10);
        var series = GenerateTestData(100);

        // Calculate iteratively
        var iterativeResults = new TSeries();
        foreach (var value in series)
        {
            iterativeResults.Add(jvoltynIterative.Update(value));
        }

        // Calculate batch
        var batchResults = Jvoltyn.Batch(series, 10);

        // Compare
        Assert.Equal(iterativeResults.Count, batchResults.Count);
        for (int i = 0; i < iterativeResults.Count; i++)
        {
            Assert.Equal(iterativeResults[i].Value, batchResults[i].Value, Tolerance);
        }
    }

    [Fact]
    public void TSeries_Update_MatchesStreaming()
    {
        var jvoltyn1 = new Jvoltyn(10);
        var jvoltyn2 = new Jvoltyn(10);
        var series = GenerateTestData(100);

        // Streaming
        foreach (var value in series)
        {
            jvoltyn1.Update(value);
        }

        // Batch
        jvoltyn2.Update(series);

        Assert.Equal(jvoltyn1.Last.Value, jvoltyn2.Last.Value, Tolerance);
    }

    [Fact]
    public void SpanCalc_MatchesStreaming()
    {
        var jvoltyn = new Jvoltyn(10);
        var series = GenerateTestData(100);

        // Stream all values first
        foreach (var value in series)
        {
            jvoltyn.Update(value);
        }
        double streamingLast = jvoltyn.Last.Value;

        // Span calculation
        var output = new double[series.Count];
        Jvoltyn.Batch(series.Values, output, 10);

        // Compare last value (after warmup)
        Assert.Equal(streamingLast, output[series.Count - 1], 1e-6);
    }

    [Fact]
    public void Chainability_Works()
    {
        var jvoltyn = new Jvoltyn(10);
        var series = GenerateTestData(50);

        var result = jvoltyn.Update(series);
        Assert.Equal(50, result.Count);
        Assert.Equal(jvoltyn.Last.Value, result.Last.Value);
    }

    // ============== Normalization Validation ==============

    [Fact]
    public void NormalizedOutput_MatchesJvoltyTransformation()
    {
        var jvolty = new Jvolty(10);
        var jvoltyn = new Jvoltyn(10);
        var series = GenerateTestData(100);

        // Feed both with same data
        foreach (var value in series)
        {
            jvolty.Update(value);
            jvoltyn.Update(value);
        }

        // Jvoltyn output should be (Jvolty - 1) * 100 / (logParam - 1)
        // RawVolatility property gives us the raw d value
        double rawD = jvoltyn.RawVolatility;
        double expectedJvolty = jvolty.Last.Value;

        // They should have the same raw d value
        Assert.Equal(expectedJvolty, rawD, Tolerance);
    }

    [Fact]
    public void FlatValues_ReturnsZero()
    {
        var jvoltyn = new Jvoltyn(5);

        // All values are the same
        for (int i = 0; i < 50; i++)
        {
            var input = new TValue(DateTime.UtcNow.AddMinutes(i), 100.0);
            jvoltyn.Update(input);
        }

        // Normalized volatility should be 0 for flat values (d=1 -> normalized=0)
        Assert.Equal(0.0, jvoltyn.Last.Value, Tolerance);
    }

    // ============== Static Batch Method ==============

    [Fact]
    public void StaticBatch_Works()
    {
        var series = GenerateTestData(50);

        var results = Jvoltyn.Batch(series, 10);

        Assert.Equal(50, results.Count);
        Assert.True(double.IsFinite(results.Last.Value));
    }

    // ============== Span API Tests ==============

    [Fact]
    public void Calculate_ValidatesLengths()
    {
        var source = new double[10];
        var output = new double[5]; // Wrong size

        var ex = Assert.Throws<ArgumentException>(() => Jvoltyn.Batch(source, output, 10));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Calculate_EmptySource_NoException()
    {
        var source = Array.Empty<double>();
        var output = Array.Empty<double>();

        var exception = Record.Exception(() => Jvoltyn.Batch(source, output, 10));
        Assert.Null(exception);
    }

    [Fact]
    public void Calculate_InvalidPeriod_ThrowsArgumentException()
    {
        var source = new double[10];
        var output = new double[10];

        Assert.Throws<ArgumentOutOfRangeException>(() => Jvoltyn.Batch(source, output, 0));
    }

    // ============== Edge Cases ==============

    [Fact]
    public void SingleValue_ReturnsZero()
    {
        var jvoltyn = new Jvoltyn(10);
        var input = new TValue(DateTime.UtcNow, 100.0);

        var result = jvoltyn.Update(input);

        Assert.True(double.IsFinite(result.Value));
        Assert.Equal(0.0, result.Value, Tolerance); // First bar = normalized 0
    }

    [Fact]
    public void Period1_Works()
    {
        var jvoltyn = new Jvoltyn(1);
        var series = GenerateTestData(10);

        foreach (var value in series)
        {
            var result = jvoltyn.Update(value);
            Assert.True(double.IsFinite(result.Value));
            Assert.True(result.Value >= 0.0 - Tolerance);
            Assert.True(result.Value <= 100.0 + Tolerance);
        }
    }

    [Fact]
    public void HighVolatility_IncreasesValue()
    {
        var jvoltyn = new Jvoltyn(10);

        // Start with stable values
        for (int i = 0; i < 20; i++)
        {
            var input = new TValue(DateTime.UtcNow.AddMinutes(i), 100.0 + (i * 0.1));
            jvoltyn.Update(input);
        }

        double lowVolatility = jvoltyn.Last.Value;

        // Create high volatility spike
        var spike = new TValue(DateTime.UtcNow.AddMinutes(21), 150.0);
        jvoltyn.Update(spike);

        double highVolatility = jvoltyn.Last.Value;

        // High volatility should produce higher normalized value
        Assert.True(highVolatility > lowVolatility);
    }

    [Fact]
    public void Bands_TrackPrice()
    {
        var jvoltyn = new Jvoltyn(10);

        // Feed increasing prices
        for (int i = 0; i < 20; i++)
        {
            var input = new TValue(DateTime.UtcNow.AddMinutes(i), 100.0 + i);
            jvoltyn.Update(input);
        }

        // Upper band should track the highest recent prices
        Assert.True(jvoltyn.UpperBand > 100.0);
        // Lower band should lag behind due to adaptive decay
        Assert.True(jvoltyn.LowerBand < jvoltyn.UpperBand);
    }

    // ============== Event Publishing ==============

    [Fact]
    public void PubEvent_Fires()
    {
        var jvoltyn = new Jvoltyn(10);
        bool eventFired = false;

        jvoltyn.Pub += (object? sender, in TValueEventArgs args) => eventFired = true;

        var input = new TValue(DateTime.UtcNow, 100.0);
        jvoltyn.Update(input);

        Assert.True(eventFired);
    }

    [Fact]
    public void EventChaining_Works()
    {
        var series = GenerateTestData(50);

        var jvoltyn = new Jvoltyn(10);
        var sma = new Sma(jvoltyn, 5); // Chain SMA to Jvoltyn output

        foreach (var value in series)
        {
            jvoltyn.Update(value);
        }

        Assert.True(double.IsFinite(sma.Last.Value));
    }

    // ============== Additional Tests ==============

    [Fact]
    public void LargeDataset_Completes()
    {
        var jvoltyn = new Jvoltyn(20);
        var series = GenerateTestData(5000);

        foreach (var value in series)
        {
            jvoltyn.Update(value);
        }

        Assert.True(jvoltyn.IsHot);
        Assert.True(double.IsFinite(jvoltyn.Last.Value));
        Assert.True(jvoltyn.Last.Value >= 0.0);
        Assert.True(jvoltyn.Last.Value <= 100.0);
    }

    [Fact]
    public void DifferentPeriods_ProduceValidValues()
    {
        var series = GenerateTestData(200);

        var jvoltyn1 = new Jvoltyn(5);
        var jvoltyn2 = new Jvoltyn(10);
        var jvoltyn3 = new Jvoltyn(20);

        foreach (var value in series)
        {
            jvoltyn1.Update(value);
            jvoltyn2.Update(value);
            jvoltyn3.Update(value);
        }

        Assert.True(double.IsFinite(jvoltyn1.Last.Value));
        Assert.True(double.IsFinite(jvoltyn2.Last.Value));
        Assert.True(double.IsFinite(jvoltyn3.Last.Value));
        Assert.True(jvoltyn1.Last.Value >= 0.0);
        Assert.True(jvoltyn2.Last.Value >= 0.0);
        Assert.True(jvoltyn3.Last.Value >= 0.0);
    }

    [Fact]
    public void SourceChaining_Works()
    {
        var series = GenerateTestData(200);

        // Create source TSeries that publishes events
        var sourceSeries = new TSeries();
        var jvoltyn = new Jvoltyn(sourceSeries, 10);

        // Feed data through the source (need enough for warmup)
        foreach (var value in series)
        {
            sourceSeries.Add(value);
        }

        // Should have valid output
        Assert.True(double.IsFinite(jvoltyn.Last.Value));
        Assert.True(jvoltyn.Last.Value >= 0.0); // Minimum normalized volatility
    }

#pragma warning disable S2699 // Test contains Assert.True and Assert.InRange - analyzer false positive
    [Fact]
    public void Prime_Works()
    {
        var jvoltyn = new Jvoltyn(5);
        var values = new double[] { 100.0, 101.0, 99.5, 102.0, 98.0, 103.0 };

        jvoltyn.Prime(values);

        double lastValue = jvoltyn.Last.Value;
        Assert.True(double.IsFinite(lastValue), "Last value should be finite after Prime");
        Assert.InRange(lastValue, 0.0, 100.0); // Normalized volatility in [0, 100]
    }
#pragma warning restore S2699
}
