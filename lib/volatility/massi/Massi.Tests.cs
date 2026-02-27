namespace QuanTAlib.Tests;

public class MassiTests
{
    private const double Tolerance = 1e-9;

    private static TBarSeries GenerateTestBars(int count = 100)
    {
        var gbm = new GBM(seed: 42);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    private static TSeries GenerateRangeData(int count = 100)
    {
        var bars = GenerateTestBars(count);
        var series = new TSeries(count);
        for (int i = 0; i < bars.Count; i++)
        {
            series.Add(new TValue(bars[i].Time, bars[i].High - bars[i].Low));
        }
        return series;
    }

    // ============== Constructor & Parameter Validation ==============

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Massi(0, 25));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Massi(-1, 25));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Massi(9, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Massi(9, -1));

        var massi = new Massi(9, 25);
        Assert.NotNull(massi);
    }

    [Fact]
    public void Constructor_SetsCorrectName()
    {
        var massi = new Massi(9, 25);
        Assert.Equal("Massi(9,25)", massi.Name);
        Assert.True(massi.WarmupPeriod > 0);

        var massi2 = new Massi(5, 10);
        Assert.Equal("Massi(5,10)", massi2.Name);
    }

    [Fact]
    public void Constructor_SetsCorrectWarmup()
    {
        var massi = new Massi(9, 25);
        // Warmup = emaLength + sumLength
        Assert.Equal(34, massi.WarmupPeriod);
    }

    // ============== Basic Functionality ==============

    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var massi = new Massi(9, 25);
        var bars = GenerateTestBars(100);

        foreach (var bar in bars)
        {
            massi.Update(bar);
        }

        Assert.True(double.IsFinite(massi.Last.Value));
    }

    [Fact]
    public void Calc_ReturnsValidValue()
    {
        var massi = new Massi(9, 25);
        var bars = GenerateTestBars(50);

        foreach (var bar in bars)
        {
            var result = massi.Update(bar);
            Assert.True(double.IsFinite(result.Value) || double.IsNaN(result.Value));
        }
    }

    [Fact]
    public void Properties_Accessible()
    {
        var massi = new Massi(9, 25);

        Assert.False(massi.IsHot);
        Assert.Contains("Massi", massi.Name, StringComparison.Ordinal);
        Assert.Equal(34, massi.WarmupPeriod);

        var bars = GenerateTestBars(50);
        foreach (var bar in bars)
        {
            massi.Update(bar);
        }

        // After warmup, properties should be valid
        Assert.True(double.IsFinite(massi.Ema1));
        Assert.True(double.IsFinite(massi.Ema2));
        Assert.True(double.IsFinite(massi.Ratio));
    }

    [Fact]
    public void EmaProperties_Accessible()
    {
        var massi = new Massi(9, 25);
        var bars = GenerateTestBars(50);

        foreach (var bar in bars)
        {
            massi.Update(bar);
        }

        // Ema1 and Ema2 should be positive (ranges are positive)
        Assert.True(massi.Ema1 >= 0);
        Assert.True(massi.Ema2 >= 0);
        // Ratio should be close to 1 normally
        Assert.True(massi.Ratio >= 0);
    }

    [Fact]
    public void Ratio_IsCloseToOne()
    {
        var massi = new Massi(9, 25);
        var bars = GenerateTestBars(100);

        foreach (var bar in bars)
        {
            massi.Update(bar);
        }

        // EMA1/EMA2 ratio typically oscillates around 1
        // For stable conditions, should be between 0.5 and 2.0
        Assert.True(massi.Ratio > 0.5 && massi.Ratio < 2.0,
            $"Ratio {massi.Ratio} outside expected range");
    }

    // ============== State Management & Bar Correction ==============

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var massi = new Massi(9, 25);
        var bars = GenerateTestBars(50);

        // Feed enough values to build up state
        for (int i = 0; i < 49; i++)
        {
            massi.Update(bars[i], isNew: true);
        }
        double valueBefore = massi.Last.Value;

        // Add one more value with isNew=true
        massi.Update(bars[49], isNew: true);
        double valueAfter = massi.Last.Value;

        // Both should be valid
        Assert.True(double.IsFinite(valueBefore));
        Assert.True(double.IsFinite(valueAfter));
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var massi = new Massi(9, 25);
        var bars = GenerateTestBars(50);

        // Feed enough bars
        for (int i = 0; i < 49; i++)
        {
            massi.Update(bars[i], isNew: true);
        }

        // Add one more value with isNew=true
        massi.Update(bars[49], isNew: true);
        double beforeUpdate = massi.Last.Value;

        // Update same bar with different value (isNew=false)
        var modifiedBar = new TBar(bars[49].Time, bars[49].Open, bars[49].High + 5,
                                   bars[49].Low - 5, bars[49].Close, bars[49].Volume);
        massi.Update(modifiedBar, isNew: false);
        double afterUpdate = massi.Last.Value;

        // Values should be different after the correction (wider range)
        Assert.True(Math.Abs(beforeUpdate - afterUpdate) > Tolerance);
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var massi = new Massi(9, 25);
        var bars = GenerateTestBars(100);

        // Feed first 99
        for (int i = 0; i < 99; i++)
        {
            massi.Update(bars[i]);
        }

        // Update with 100th bar (isNew=true)
        massi.Update(bars[99], true);

        // Update with modified 100th bar (isNew=false)
        var modifiedBar = new TBar(bars[99].Time, bars[99].Open, bars[99].High + 5,
                                   bars[99].Low - 5, bars[99].Close, bars[99].Volume);
        double val2 = massi.Update(modifiedBar, false).Value;

        // Create new instance and feed up to modified
        var massi2 = new Massi(9, 25);
        for (int i = 0; i < 99; i++)
        {
            massi2.Update(bars[i]);
        }
        double val3 = massi2.Update(modifiedBar, true).Value;

        Assert.Equal(val3, val2, Tolerance);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var massi = new Massi(5, 10);
        var bars = GenerateTestBars(20);

        // Feed 10 new values
        TBar tenthBar = default;
        for (int i = 0; i < 10; i++)
        {
            tenthBar = bars[i];
            massi.Update(tenthBar, isNew: true);
        }

        // Remember state after 10 values
        double stateAfterTen = massi.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 10; i < 19; i++)
        {
            massi.Update(bars[i], isNew: false);
        }

        // Feed the remembered 10th bar again with isNew=false
        TValue finalResult = massi.Update(tenthBar, isNew: false);

        // State should match the original state after 10 values
        Assert.Equal(stateAfterTen, finalResult.Value, Tolerance);
    }

    [Fact]
    public void Reset_Works()
    {
        var massi = new Massi(9, 25);
        var bars = GenerateTestBars(50);

        foreach (var bar in bars)
        {
            massi.Update(bar);
        }

        massi.Reset();
        Assert.False(massi.IsHot);
        Assert.Equal(0.0, massi.Ema1, Tolerance);
        Assert.Equal(0.0, massi.Ema2, Tolerance);
        Assert.Equal(0.0, massi.Ratio, Tolerance);
    }

    // ============== Warmup & Convergence ==============

    [Fact]
    public void IsHot_BecomesTrueAfterWarmup()
    {
        var massi = new Massi(9, 25);

        Assert.False(massi.IsHot);

        var bars = GenerateTestBars(50);

        int steps = 0;
        while (!massi.IsHot && steps < bars.Count)
        {
            massi.Update(bars[steps]);
            steps++;
        }

        Assert.True(massi.IsHot);
        Assert.Equal(34, steps); // emaLength + sumLength
    }

    [Fact]
    public void WarmupPeriod_IsPositive()
    {
        var massi = new Massi(9, 25);
        Assert.Equal(34, massi.WarmupPeriod);

        var massi2 = new Massi(5, 10);
        Assert.Equal(15, massi2.WarmupPeriod);
    }

    // ============== NaN/Infinity Handling ==============

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var massi = new Massi(5, 10);
        var bars = GenerateTestBars(20);

        // Feed some valid bars
        for (int i = 0; i < 15; i++)
        {
            massi.Update(bars[i]);
        }

        // Feed NaN value via TValue (range input)
        var inputWithNaN = new TValue(DateTime.UtcNow.AddMinutes(20), double.NaN);
        var resultAfterNaN = massi.Update(inputWithNaN);

        // Result should be finite
        Assert.True(double.IsFinite(resultAfterNaN.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var massi = new Massi(5, 10);
        var bars = GenerateTestBars(20);

        // Feed some valid bars
        for (int i = 0; i < 15; i++)
        {
            massi.Update(bars[i]);
        }

        // Feed Infinity value
        var inputWithInf = new TValue(DateTime.UtcNow.AddMinutes(20), double.PositiveInfinity);
        var resultAfterInf = massi.Update(inputWithInf);

        // Result should be finite
        Assert.True(double.IsFinite(resultAfterInf.Value));
    }

    [Fact]
    public void NegativeRange_UsesLastValidValue()
    {
        var massi = new Massi(5, 10);
        var bars = GenerateTestBars(20);

        // Feed some valid bars
        for (int i = 0; i < 15; i++)
        {
            massi.Update(bars[i]);
        }

        // Feed negative range value (invalid)
        var inputWithNeg = new TValue(DateTime.UtcNow.AddMinutes(20), -5.0);
        var resultAfterNeg = massi.Update(inputWithNeg);

        // Result should be finite
        Assert.True(double.IsFinite(resultAfterNeg.Value));
    }

    [Fact]
    public void BatchNaN_Safe()
    {
        var massi = new Massi(5, 10);
        var bars = GenerateTestBars(20);

        // Feed some values
        for (int i = 0; i < 15; i++)
        {
            massi.Update(bars[i]);
        }

        // Feed multiple NaN values
        for (int i = 0; i < 5; i++)
        {
            var nanInput = new TValue(DateTime.UtcNow.AddMinutes(15 + i), double.NaN);
            var result = massi.Update(nanInput);
            Assert.True(double.IsFinite(result.Value));
        }
    }

    // ============== Consistency Tests ==============

    [Fact]
    public void TBarSeries_MatchesIterativeCalc()
    {
        var massiIterative = new Massi(9, 25);
        var bars = GenerateTestBars(100);

        // Calculate iteratively
        foreach (var bar in bars)
        {
            massiIterative.Update(bar);
        }

        // Calculate batch
        var massiBatch = new Massi(9, 25);
        _ = massiBatch.Update(bars);

        // Compare last values
        Assert.Equal(massiIterative.Last.Value, massiBatch.Last.Value, Tolerance);
    }

    [Fact]
    public void TSeries_Update_MatchesStreaming()
    {
        var massi1 = new Massi(9, 25);
        var massi2 = new Massi(9, 25);
        var series = GenerateRangeData(100);

        // Streaming
        foreach (var value in series)
        {
            massi1.Update(value);
        }

        // Batch
        massi2.Update(series);

        Assert.Equal(massi1.Last.Value, massi2.Last.Value, Tolerance);
    }

    [Fact]
    public void SpanCalc_MatchesStreaming()
    {
        var massi = new Massi(9, 25);
        var series = GenerateRangeData(100);

        // Stream all values first
        foreach (var value in series)
        {
            massi.Update(value);
        }
        double streamingLast = massi.Last.Value;

        // Span calculation
        var output = new double[series.Count];
        Massi.Batch(series.Values, output, 9, 25);

        // Compare last value
        Assert.Equal(streamingLast, output[series.Count - 1], 1e-6);
    }

    [Fact]
    public void Chainability_Works()
    {
        var massi = new Massi(9, 25);
        var bars = GenerateTestBars(50);

        var result = massi.Update(bars);
        Assert.Equal(50, result.Count);
        Assert.Equal(massi.Last.Value, result.Last.Value);
    }

    // ============== Mass Index Specific Tests ==============

    [Fact]
    public void Massi_TypicalRange()
    {
        var massi = new Massi(9, 25);
        var bars = GenerateTestBars(200);

        foreach (var bar in bars)
        {
            massi.Update(bar);
        }

        // Mass Index sum typically ranges around 25 (sumLength * 1.0 ratio)
        // During normal conditions, expect 20-30 range
        Assert.True(massi.Last.Value > 10, $"MASSI {massi.Last.Value} unexpectedly low");
        Assert.True(massi.Last.Value < 40, $"MASSI {massi.Last.Value} unexpectedly high");
    }

    [Fact]
    public void Massi_SumLengthAffectsOutput()
    {
        var massi10 = new Massi(9, 10);
        var massi25 = new Massi(9, 25);
        var massi50 = new Massi(9, 50);

        var bars = GenerateTestBars(100);

        foreach (var bar in bars)
        {
            massi10.Update(bar);
            massi25.Update(bar);
            massi50.Update(bar);
        }

        // Longer sumLength should produce larger output (more terms summed)
        Assert.True(massi25.Last.Value > massi10.Last.Value,
            $"MASSI(9,25)={massi25.Last.Value} should be > MASSI(9,10)={massi10.Last.Value}");
        Assert.True(massi50.Last.Value > massi25.Last.Value,
            $"MASSI(9,50)={massi50.Last.Value} should be > MASSI(9,25)={massi25.Last.Value}");
    }

    // ============== Static Batch Methods ==============

    [Fact]
    public void StaticBatch_TBarSeries_Works()
    {
        var bars = GenerateTestBars(50);

        var results = Massi.Batch(bars, 9, 25);

        Assert.Equal(50, results.Count);
        Assert.True(double.IsFinite(results.Last.Value));
    }

    [Fact]
    public void StaticBatch_TSeries_Works()
    {
        var series = GenerateRangeData(50);

        var results = Massi.Batch(series, 9, 25);

        Assert.Equal(50, results.Count);
        Assert.True(double.IsFinite(results.Last.Value));
    }

    // ============== Span API Tests ==============

    [Fact]
    public void Calculate_ValidatesLengths()
    {
        var source = new double[10];
        var output = new double[5]; // Wrong size

        var ex = Assert.Throws<ArgumentException>(() => Massi.Batch(source, output, 9, 25));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Calculate_EmptySource_NoException()
    {
        var source = Array.Empty<double>();
        var output = Array.Empty<double>();

        var exception = Record.Exception(() => Massi.Batch(source, output, 9, 25));
        Assert.Null(exception);
    }

    [Fact]
    public void Calculate_InvalidParams_ThrowsArgumentException()
    {
        var source = new double[10];
        var output = new double[10];

        Assert.Throws<ArgumentOutOfRangeException>(() => Massi.Batch(source, output, 0, 25));
        Assert.Throws<ArgumentOutOfRangeException>(() => Massi.Batch(source, output, 9, 0));
    }

    // ============== Edge Cases ==============

    [Fact]
    public void SingleValue_ReturnsValue()
    {
        var massi = new Massi(9, 25);
        var bar = GenerateTestBars(1)[0];

        var result = massi.Update(bar);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Period1_Works()
    {
        var massi = new Massi(1, 1);
        var bars = GenerateTestBars(10);

        foreach (var bar in bars)
        {
            var result = massi.Update(bar);
            Assert.True(double.IsFinite(result.Value));
        }
    }

    [Fact]
    public void FlatRange_ProducesStableOutput()
    {
        var massi = new Massi(9, 25);

        // All bars have same range
        for (int i = 0; i < 50; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i).Ticks, 100.0, 101.0, 99.0, 100.5, 1000.0);
            massi.Update(bar);
        }

        // Output should be approximately sumLength (25) since ratio ≈ 1
        Assert.True(Math.Abs(massi.Last.Value - 25.0) < 1.0,
            $"MASSI {massi.Last.Value} should be close to 25 for flat range");
    }

    // ============== Event Publishing ==============

    [Fact]
    public void PubEvent_Fires()
    {
        var massi = new Massi(9, 25);
        bool eventFired = false;

        massi.Pub += (object? sender, in TValueEventArgs args) => eventFired = true;

        var bar = GenerateTestBars(1)[0];
        massi.Update(bar);

        Assert.True(eventFired);
    }

    [Fact]
    public void EventChaining_Works()
    {
        var bars = GenerateTestBars(50);

        var massi = new Massi(9, 25);
        var sma = new Sma(massi, 5); // Chain SMA to MASSI output

        foreach (var bar in bars)
        {
            massi.Update(bar);
        }

        Assert.True(double.IsFinite(sma.Last.Value));
    }

    // ============== Additional Tests ==============

    [Fact]
    public void LargeDataset_Completes()
    {
        var massi = new Massi(9, 25);
        var bars = GenerateTestBars(5000);

        foreach (var bar in bars)
        {
            massi.Update(bar);
        }

        Assert.True(massi.IsHot);
        Assert.True(double.IsFinite(massi.Last.Value));
    }

    [Fact]
    public void DifferentParameters_ProduceValidValues()
    {
        var bars = GenerateTestBars(200);

        var massi1 = new Massi(5, 10);
        var massi2 = new Massi(9, 25);
        var massi3 = new Massi(15, 50);

        foreach (var bar in bars)
        {
            massi1.Update(bar);
            massi2.Update(bar);
            massi3.Update(bar);
        }

        Assert.True(double.IsFinite(massi1.Last.Value));
        Assert.True(double.IsFinite(massi2.Last.Value));
        Assert.True(double.IsFinite(massi3.Last.Value));
    }

    [Fact]
    public void SourceChaining_Works()
    {
        var bars = GenerateTestBars(200);

        // Create source TSeries that publishes events
        var sourceSeries = new TSeries();
        var massi = new Massi(sourceSeries, 9, 25);

        // Feed data through the source (as range values)
        foreach (var bar in bars)
        {
            sourceSeries.Add(new TValue(bar.Time, bar.High - bar.Low));
        }

        // Should have valid output
        Assert.True(double.IsFinite(massi.Last.Value));
    }

#pragma warning disable S2699 // Test contains Assert.True and Assert.InRange - analyzer false positive
    [Fact]
    public void Prime_Works()
    {
        var massi = new Massi(5, 10);
        var values = new double[] { 1.0, 1.1, 0.9, 1.2, 0.8, 1.3, 1.0, 1.1, 0.95, 1.05 };

        massi.Prime(values);

        double lastValue = massi.Last.Value;
        Assert.True(double.IsFinite(lastValue), "Last value should be finite after Prime");
    }
#pragma warning restore S2699

    [Fact]
    public void TValueUpdate_TreatsValueAsRange()
    {
        var massi1 = new Massi(9, 25);
        var massi2 = new Massi(9, 25);

        var bars = GenerateTestBars(50);

        // Update with TBar
        foreach (var bar in bars)
        {
            massi1.Update(bar);
        }

        // Update with TValue (pre-calculated range)
        foreach (var bar in bars)
        {
            var range = bar.High - bar.Low;
            massi2.Update(new TValue(bar.Time, range));
        }

        // Both should produce same result
        Assert.Equal(massi1.Last.Value, massi2.Last.Value, Tolerance);
    }
}
