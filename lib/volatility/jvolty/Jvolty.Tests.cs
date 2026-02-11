namespace QuanTAlib.Tests;

public class JvoltyTests
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
        Assert.Throws<ArgumentOutOfRangeException>(() => new Jvolty(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Jvolty(-1));

        var jvolty = new Jvolty(10);
        Assert.NotNull(jvolty);
    }

    [Fact]
    public void Constructor_SetsCorrectName()
    {
        var jvolty = new Jvolty(7);
        Assert.Equal("Jvolty(7)", jvolty.Name);
        Assert.True(jvolty.WarmupPeriod > 0);

        var jvolty2 = new Jvolty(14);
        Assert.Equal("Jvolty(14)", jvolty2.Name);
    }

    // ============== Basic Functionality ==============

    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var jvolty = new Jvolty(10);
        var series = GenerateTestData(100);

        foreach (var value in series)
        {
            jvolty.Update(value);
        }

        Assert.True(double.IsFinite(jvolty.Last.Value));
    }

    [Fact]
    public void Calc_ReturnsValue()
    {
        var jvolty = new Jvolty(10);
        var input = new TValue(DateTime.UtcNow, 100.0);

        Assert.InRange(jvolty.Last.Value, -Tolerance, Tolerance); // Initially zero

        TValue result = jvolty.Update(input);

        Assert.True(result.Value >= 1.0); // Minimum volatility is 1.0
        Assert.Equal(result.Value, jvolty.Last.Value, Tolerance);
    }

    [Fact]
    public void FirstValue_ReturnsMinimumVolatility()
    {
        var jvolty = new Jvolty(10);
        var input = new TValue(DateTime.UtcNow, 100.0);

        TValue result = jvolty.Update(input);

        Assert.Equal(1.0, result.Value, Tolerance); // First bar returns minimum volatility
    }

    [Fact]
    public void Properties_Accessible()
    {
        var jvolty = new Jvolty(10);

        Assert.InRange(jvolty.Last.Value, -Tolerance, Tolerance); // Initially zero
        Assert.False(jvolty.IsHot);
        Assert.Contains("Jvolty", jvolty.Name, StringComparison.Ordinal);
        Assert.True(jvolty.WarmupPeriod > 0);

        var input = new TValue(DateTime.UtcNow, 100.0);
        jvolty.Update(input);

        Assert.True(Math.Abs(jvolty.Last.Value) > Tolerance); // No longer zero
    }

    [Fact]
    public void BandProperties_Accessible()
    {
        var jvolty = new Jvolty(10);
        var input = new TValue(DateTime.UtcNow, 100.0);

        jvolty.Update(input);

        // After first bar, bands should be initialized to the input value
        Assert.Equal(100.0, jvolty.UpperBand, Tolerance);
        Assert.Equal(100.0, jvolty.LowerBand, Tolerance);
    }

    // ============== State Management & Bar Correction ==============

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var jvolty = new Jvolty(10);
        var series = GenerateTestData(50);

        // Feed enough values to build up volatility history
        for (int i = 0; i < 49; i++)
        {
            jvolty.Update(series[i], isNew: true);
        }
        double valueBefore = jvolty.Last.Value;

        // Add one more value with isNew=true
        jvolty.Update(series[49], isNew: true);
        double valueAfter = jvolty.Last.Value;

        // Both should be valid volatility values
        Assert.True(double.IsFinite(valueBefore));
        Assert.True(double.IsFinite(valueAfter));
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var jvolty = new Jvolty(10);
        var series = GenerateTestData(50);

        // Feed enough bars to have meaningful volatility
        for (int i = 0; i < 49; i++)
        {
            jvolty.Update(series[i], isNew: true);
        }

        // Add one more value with isNew=true
        jvolty.Update(series[49], isNew: true);
        double beforeUpdate = jvolty.Last.Value;

        // Update same bar with different value (isNew=false)
        var modifiedInput = new TValue(series[49].Time, series[49].Value + 50.0);
        jvolty.Update(modifiedInput, isNew: false);
        double afterUpdate = jvolty.Last.Value;

        // Values should be different after the correction
        Assert.True(Math.Abs(beforeUpdate - afterUpdate) > Tolerance);
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var jvolty = new Jvolty(10);
        var series = GenerateTestData(100);

        // Feed first 99
        for (int i = 0; i < 99; i++)
        {
            jvolty.Update(series[i]);
        }

        // Update with 100th point (isNew=true)
        jvolty.Update(series[99], true);

        // Update with modified 100th point (isNew=false)
        var modifiedInput = new TValue(series[99].Time, series[99].Value + 50.0);
        double val2 = jvolty.Update(modifiedInput, false).Value;

        // Create new instance and feed up to modified
        var jvolty2 = new Jvolty(10);
        for (int i = 0; i < 99; i++)
        {
            jvolty2.Update(series[i]);
        }
        double val3 = jvolty2.Update(modifiedInput, true).Value;

        Assert.Equal(val3, val2, Tolerance);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var jvolty = new Jvolty(5);
        var series = GenerateTestData(20);

        // Feed 10 new values
        TValue tenthValue = default;
        for (int i = 0; i < 10; i++)
        {
            tenthValue = series[i];
            jvolty.Update(tenthValue, isNew: true);
        }

        // Remember state after 10 values
        double stateAfterTen = jvolty.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 10; i < 19; i++)
        {
            jvolty.Update(series[i], isNew: false);
        }

        // Feed the remembered 10th value again with isNew=false
        TValue finalResult = jvolty.Update(tenthValue, isNew: false);

        // State should match the original state after 10 values
        Assert.Equal(stateAfterTen, finalResult.Value, Tolerance);
    }

    [Fact]
    public void Reset_Works()
    {
        var jvolty = new Jvolty(10);
        var series = GenerateTestData(50);

        foreach (var value in series)
        {
            jvolty.Update(value);
        }

        double lastVal = jvolty.Last.Value;
        Assert.True(Math.Abs(lastVal) > Tolerance); // Not zero

        jvolty.Reset();
        Assert.InRange(jvolty.Last.Value, -Tolerance, Tolerance); // Reset to zero
        Assert.False(jvolty.IsHot);

        // After reset, should accept new values
        jvolty.Update(series[0]);
        Assert.True(Math.Abs(jvolty.Last.Value) > Tolerance); // No longer zero
    }

    // ============== Warmup & Convergence ==============

    [Fact]
    public void IsHot_BecomesTrueAfterWarmup()
    {
        var jvolty = new Jvolty(5);

        Assert.False(jvolty.IsHot);

        var series = GenerateTestData(200);

        int steps = 0;
        while (!jvolty.IsHot && steps < series.Count)
        {
            jvolty.Update(series[steps]);
            steps++;
        }

        Assert.True(jvolty.IsHot);
        Assert.True(steps > 0);
    }

    [Fact]
    public void WarmupPeriod_IsPositive()
    {
        var jvolty = new Jvolty(10);
        Assert.True(jvolty.WarmupPeriod > 0);

        var jvolty2 = new Jvolty(20);
        Assert.True(jvolty2.WarmupPeriod > 0);

        // WarmupPeriod should increase with the period parameter
        Assert.True(jvolty2.WarmupPeriod >= jvolty.WarmupPeriod);
    }

    // ============== NaN/Infinity Handling ==============

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var jvolty = new Jvolty(5);

        var input1 = new TValue(DateTime.UtcNow, 100.0);
        jvolty.Update(input1);

        var input2 = new TValue(DateTime.UtcNow.AddMinutes(1), 110.0);
        jvolty.Update(input2);

        // Feed NaN value
        var inputWithNaN = new TValue(DateTime.UtcNow.AddMinutes(2), double.NaN);
        var resultAfterNaN = jvolty.Update(inputWithNaN);

        // Result should be finite
        Assert.True(double.IsFinite(resultAfterNaN.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var jvolty = new Jvolty(5);

        var input1 = new TValue(DateTime.UtcNow, 100.0);
        jvolty.Update(input1);

        var input2 = new TValue(DateTime.UtcNow.AddMinutes(1), 110.0);
        jvolty.Update(input2);

        // Feed Infinity value
        var inputWithInf = new TValue(DateTime.UtcNow.AddMinutes(2), double.PositiveInfinity);
        var resultAfterInf = jvolty.Update(inputWithInf);

        // Result should be finite
        Assert.True(double.IsFinite(resultAfterInf.Value));
    }

    [Fact]
    public void BatchNaN_Safe()
    {
        var jvolty = new Jvolty(5);
        var series = GenerateTestData(20);

        // Feed some values
        for (int i = 0; i < 10; i++)
        {
            jvolty.Update(series[i]);
        }

        // Feed multiple NaN values
        for (int i = 0; i < 5; i++)
        {
            var nanInput = new TValue(DateTime.UtcNow.AddMinutes(10 + i), double.NaN);
            var result = jvolty.Update(nanInput);
            Assert.True(double.IsFinite(result.Value));
        }
    }

    // ============== Consistency Tests ==============

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        var jvoltyIterative = new Jvolty(10);
        var series = GenerateTestData(100);

        // Calculate iteratively
        var iterativeResults = new TSeries();
        foreach (var value in series)
        {
            iterativeResults.Add(jvoltyIterative.Update(value));
        }

        // Calculate batch
        var batchResults = Jvolty.Batch(series, 10);

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
        var jvolty1 = new Jvolty(10);
        var jvolty2 = new Jvolty(10);
        var series = GenerateTestData(100);

        // Streaming
        foreach (var value in series)
        {
            jvolty1.Update(value);
        }

        // Batch
        jvolty2.Update(series);

        Assert.Equal(jvolty1.Last.Value, jvolty2.Last.Value, Tolerance);
    }

    [Fact]
    public void SpanCalc_MatchesStreaming()
    {
        var jvolty = new Jvolty(10);
        var series = GenerateTestData(100);

        // Stream all values first
        foreach (var value in series)
        {
            jvolty.Update(value);
        }
        double streamingLast = jvolty.Last.Value;

        // Span calculation
        var output = new double[series.Count];
        Jvolty.Batch(series.Values, output, 10);

        // Compare last value (after warmup)
        Assert.Equal(streamingLast, output[series.Count - 1], 1e-6);
    }

    [Fact]
    public void Chainability_Works()
    {
        var jvolty = new Jvolty(10);
        var series = GenerateTestData(50);

        var result = jvolty.Update(series);
        Assert.Equal(50, result.Count);
        Assert.Equal(jvolty.Last.Value, result.Last.Value);
    }

    // ============== Static Batch Method ==============

    [Fact]
    public void StaticBatch_Works()
    {
        var series = GenerateTestData(50);

        var results = Jvolty.Batch(series, 10);

        Assert.Equal(50, results.Count);
        Assert.True(double.IsFinite(results.Last.Value));
    }

    // ============== Span API Tests ==============

    [Fact]
    public void Calculate_ValidatesLengths()
    {
        var source = new double[10];
        var output = new double[5]; // Wrong size

        var ex = Assert.Throws<ArgumentException>(() => Jvolty.Batch(source, output, 10));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Calculate_EmptySource_NoException()
    {
        var source = Array.Empty<double>();
        var output = Array.Empty<double>();

        var exception = Record.Exception(() => Jvolty.Batch(source, output, 10));
        Assert.Null(exception);
    }

    [Fact]
    public void Calculate_InvalidPeriod_ThrowsArgumentException()
    {
        var source = new double[10];
        var output = new double[10];

        Assert.Throws<ArgumentOutOfRangeException>(() => Jvolty.Batch(source, output, 0));
    }

    // ============== Edge Cases ==============

    [Fact]
    public void SingleValue_ReturnsValidResult()
    {
        var jvolty = new Jvolty(10);
        var input = new TValue(DateTime.UtcNow, 100.0);

        var result = jvolty.Update(input);

        Assert.True(double.IsFinite(result.Value));
        Assert.Equal(1.0, result.Value, Tolerance); // First bar = minimum volatility
    }

    [Fact]
    public void Period1_Works()
    {
        var jvolty = new Jvolty(1);
        var series = GenerateTestData(10);

        foreach (var value in series)
        {
            var result = jvolty.Update(value);
            Assert.True(double.IsFinite(result.Value));
        }
    }

    [Fact]
    public void FlatValues_MinimumVolatility()
    {
        var jvolty = new Jvolty(5);

        // All values are the same
        for (int i = 0; i < 50; i++)
        {
            var input = new TValue(DateTime.UtcNow.AddMinutes(i), 100.0);
            jvolty.Update(input);
        }

        // Volatility should be at minimum (1.0) for flat values
        Assert.Equal(1.0, jvolty.Last.Value, Tolerance);
    }

    [Fact]
    public void HighVolatility_IncreasesExponent()
    {
        var jvolty = new Jvolty(10);

        // Start with stable values
        for (int i = 0; i < 20; i++)
        {
            var input = new TValue(DateTime.UtcNow.AddMinutes(i), 100.0 + (i * 0.1));
            jvolty.Update(input);
        }

        double lowVolatility = jvolty.Last.Value;

        // Create high volatility spike
        var spike = new TValue(DateTime.UtcNow.AddMinutes(21), 150.0);
        jvolty.Update(spike);

        double highVolatility = jvolty.Last.Value;

        // High volatility should be greater than low volatility
        Assert.True(highVolatility > lowVolatility);
    }

    [Fact]
    public void Bands_TrackPrice()
    {
        var jvolty = new Jvolty(10);

        // Feed increasing prices
        for (int i = 0; i < 20; i++)
        {
            var input = new TValue(DateTime.UtcNow.AddMinutes(i), 100.0 + i);
            jvolty.Update(input);
        }

        // Upper band should track the highest recent prices
        Assert.True(jvolty.UpperBand > 100.0);
        // Lower band should lag behind due to adaptive decay
        Assert.True(jvolty.LowerBand < jvolty.UpperBand);
    }

    // ============== Event Publishing ==============

    [Fact]
    public void PubEvent_Fires()
    {
        var jvolty = new Jvolty(10);
        bool eventFired = false;

        jvolty.Pub += (object? sender, in TValueEventArgs args) => eventFired = true;

        var input = new TValue(DateTime.UtcNow, 100.0);
        jvolty.Update(input);

        Assert.True(eventFired);
    }

    [Fact]
    public void EventChaining_Works()
    {
        var series = GenerateTestData(50);

        var jvolty = new Jvolty(10);
        var sma = new Sma(jvolty, 5); // Chain SMA to Jvolty output

        foreach (var value in series)
        {
            jvolty.Update(value);
        }

        Assert.True(double.IsFinite(sma.Last.Value));
    }

    // ============== Additional Tests ==============

    [Fact]
    public void LargeDataset_Completes()
    {
        var jvolty = new Jvolty(20);
        var series = GenerateTestData(5000);

        foreach (var value in series)
        {
            jvolty.Update(value);
        }

        Assert.True(jvolty.IsHot);
        Assert.True(double.IsFinite(jvolty.Last.Value));
    }

    [Fact]
    public void DifferentPeriods_ProduceValidValues()
    {
        var series = GenerateTestData(200);

        var jvolty1 = new Jvolty(5);
        var jvolty2 = new Jvolty(10);
        var jvolty3 = new Jvolty(20);

        foreach (var value in series)
        {
            jvolty1.Update(value);
            jvolty2.Update(value);
            jvolty3.Update(value);
        }

        Assert.True(double.IsFinite(jvolty1.Last.Value));
        Assert.True(double.IsFinite(jvolty2.Last.Value));
        Assert.True(double.IsFinite(jvolty3.Last.Value));
        Assert.True(jvolty1.Last.Value >= 1.0);
        Assert.True(jvolty2.Last.Value >= 1.0);
        Assert.True(jvolty3.Last.Value >= 1.0);
    }

    [Fact]
    public void SourceChaining_Works()
    {
        var series = GenerateTestData(200);

        // Create source TSeries that publishes events
        var sourceSeries = new TSeries();
        var jvolty = new Jvolty(sourceSeries, 10);

        // Feed data through the source (need enough for warmup)
        foreach (var value in series)
        {
            sourceSeries.Add(value);
        }

        // Should have valid output
        Assert.True(double.IsFinite(jvolty.Last.Value));
        Assert.True(jvolty.Last.Value >= 1.0); // Minimum volatility
    }

#pragma warning disable S2699 // Test contains Assert.True and Assert.InRange - analyzer false positive
    [Fact]
    public void Prime_Works()
    {
        var jvolty = new Jvolty(5);
        var values = new double[] { 100.0, 101.0, 99.5, 102.0, 98.0, 103.0 };

        jvolty.Prime(values);

        double lastValue = jvolty.Last.Value;
        Assert.True(double.IsFinite(lastValue), "Last value should be finite after Prime");
        Assert.InRange(lastValue, 1.0, double.MaxValue); // Volatility >= minimum (1.0)
    }
#pragma warning restore S2699
}