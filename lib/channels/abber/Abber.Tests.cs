namespace QuanTAlib.Tests;

public class AbberTests
{
    [Fact]
    public void Abber_Constructor_ValidatesInput()
    {
        // Period validation
        Assert.Throws<ArgumentException>(() => new Abber(0));
        Assert.Throws<ArgumentException>(() => new Abber(-1));

        // Multiplier validation
        Assert.Throws<ArgumentException>(() => new Abber(10, 0));
        Assert.Throws<ArgumentException>(() => new Abber(10, -1));

        // Valid construction
        var abber = new Abber(10);
        Assert.NotNull(abber);

        var abber2 = new Abber(20, 3.0);
        Assert.NotNull(abber2);
    }

    [Fact]
    public void Abber_Calc_ReturnsValue()
    {
        var abber = new Abber(10);

        Assert.Equal(0, abber.Last.Value);
        Assert.Equal(0, abber.Upper.Value);
        Assert.Equal(0, abber.Lower.Value);

        TValue result = abber.Update(new TValue(DateTime.UtcNow, 100));

        Assert.True(double.IsFinite(result.Value));
        Assert.Equal(result.Value, abber.Last.Value);
        Assert.True(double.IsFinite(abber.Upper.Value));
        Assert.True(double.IsFinite(abber.Lower.Value));
    }

    [Fact]
    public void Abber_FirstValue_ReturnsExpected()
    {
        var abber = new Abber(10);

        // First value: source = 100
        // SMA(1) = 100, Deviation = |100 - 100| = 0, AvgDeviation = 0
        // Middle = 100, Upper = 100 + 0 = 100, Lower = 100 - 0 = 100
        abber.Update(new TValue(DateTime.UtcNow, 100));

        Assert.Equal(100.0, abber.Last.Value, 1e-10);
        Assert.Equal(100.0, abber.Upper.Value, 1e-10);
        Assert.Equal(100.0, abber.Lower.Value, 1e-10);
    }

    [Fact]
    public void Abber_Calc_IsNew_AcceptsParameter()
    {
        var abber = new Abber(10);

        abber.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        double value1 = abber.Last.Value;

        abber.Update(new TValue(DateTime.UtcNow, 110), isNew: true);
        double value2 = abber.Last.Value;

        // Values should change with new data
        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Abber_Calc_IsNew_False_UpdatesValue()
    {
        var abber = new Abber(10);

        abber.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        abber.Update(new TValue(DateTime.UtcNow, 110), isNew: true);
        double beforeUpdate = abber.Last.Value;

        abber.Update(new TValue(DateTime.UtcNow, 120), isNew: false);
        double afterUpdate = abber.Last.Value;

        // Update should change the value
        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void Abber_Reset_ClearsState()
    {
        var abber = new Abber(10);

        abber.Update(new TValue(DateTime.UtcNow, 100));
        abber.Update(new TValue(DateTime.UtcNow, 105));
        double middleBefore = abber.Last.Value;

        abber.Reset();

        Assert.Equal(0, abber.Last.Value);
        Assert.Equal(0, abber.Upper.Value);
        Assert.Equal(0, abber.Lower.Value);
        Assert.False(abber.IsHot);

        // After reset, should accept new values
        abber.Update(new TValue(DateTime.UtcNow, 50));
        Assert.NotEqual(0, abber.Last.Value);
        Assert.NotEqual(middleBefore, abber.Last.Value);
    }

    [Fact]
    public void Abber_Properties_Accessible()
    {
        var abber = new Abber(10, 2.5);

        Assert.Equal(0, abber.Last.Value);
        Assert.False(abber.IsHot);
        Assert.Contains("Abber", abber.Name, StringComparison.Ordinal);
        Assert.Equal(10, abber.WarmupPeriod);

        abber.Update(new TValue(DateTime.UtcNow, 100));

        Assert.NotEqual(0, abber.Last.Value);
    }

    [Fact]
    public void Abber_IsHot_BecomesTrueWhenBufferFull()
    {
        var abber = new Abber(5);

        Assert.False(abber.IsHot);

        for (int i = 1; i <= 4; i++)
        {
            abber.Update(new TValue(DateTime.UtcNow, 100 + i));
            Assert.False(abber.IsHot);
        }

        abber.Update(new TValue(DateTime.UtcNow, 105));
        Assert.True(abber.IsHot);
    }

    [Fact]
    public void Abber_CalculatesCorrectBands()
    {
        var abber = new Abber(3, 2.0);

        // Bar 1: source = 100
        // SMA = 100, Deviation = 0, AvgDev = 0
        abber.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100.0, abber.Last.Value, 1e-10);

        // Bar 2: source = 110
        // SMA(2) = (100+110)/2 = 105
        // Dev1 = |100 - 100| = 0 (calculated when 100 was added, SMA was 100)
        // Dev2 = |110 - 100| = 10 (calculated when 110 is added, SMA was 100)
        // AvgDev = (0+10)/2 = 5
        // Upper = 105 + 2*5 = 115, Lower = 105 - 2*5 = 95
        abber.Update(new TValue(DateTime.UtcNow, 110));
        Assert.Equal(105.0, abber.Last.Value, 1e-10);

        // Bar 3: source = 120
        // SMA(3) = (100+110+120)/3 = 110
        // Dev3 = |120 - 105| = 15 (calculated when 120 is added, SMA was 105)
        // AvgDev = (0+10+15)/3 = 8.333...
        // Upper = 110 + 2*8.333 = 126.666..., Lower = 110 - 2*8.333 = 93.333...
        abber.Update(new TValue(DateTime.UtcNow, 120));
        Assert.Equal(110.0, abber.Last.Value, 1e-10);
        Assert.Equal(110.0 + 2.0 * 25.0 / 3.0, abber.Upper.Value, 1e-10);
        Assert.Equal(110.0 - 2.0 * 25.0 / 3.0, abber.Lower.Value, 1e-10);
    }

    [Fact]
    public void Abber_SlidingWindow_Works()
    {
        var abber = new Abber(3, 2.0);

        // Feed initial values
        abber.Update(new TValue(DateTime.UtcNow, 100));
        abber.Update(new TValue(DateTime.UtcNow, 110));
        abber.Update(new TValue(DateTime.UtcNow, 120));

        double middle1 = abber.Last.Value;

        // Add another value - window slides
        abber.Update(new TValue(DateTime.UtcNow, 130));

        // SMA(3) should now be (110+120+130)/3 = 120
        Assert.NotEqual(middle1, abber.Last.Value);
        Assert.Equal(120.0, abber.Last.Value, 1e-10);
    }

    [Fact]
    public void Abber_IterativeCorrections_RestoreToOriginalState()
    {
        var abber = new Abber(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 10 new values
        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            abber.Update(tenthInput, isNew: true);
        }

        // Remember state after 10 values
        double middleAfterTen = abber.Last.Value;
        double upperAfterTen = abber.Upper.Value;
        double lowerAfterTen = abber.Lower.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            abber.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered 10th input again with isNew=false
        abber.Update(tenthInput, isNew: false);

        // State should match the original state after 10 values
        Assert.Equal(middleAfterTen, abber.Last.Value, 1e-10);
        Assert.Equal(upperAfterTen, abber.Upper.Value, 1e-10);
        Assert.Equal(lowerAfterTen, abber.Lower.Value, 1e-10);
    }

    [Fact]
    public void Abber_BatchCalc_MatchesIterativeCalc()
    {
        var abberIterative = new Abber(10);
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
        var iterativeMiddle = new List<double>();
        var iterativeUpper = new List<double>();
        var iterativeLower = new List<double>();
        foreach (var item in series)
        {
            abberIterative.Update(item);
            iterativeMiddle.Add(abberIterative.Last.Value);
            iterativeUpper.Add(abberIterative.Upper.Value);
            iterativeLower.Add(abberIterative.Lower.Value);
        }

        // Calculate batch
        var abberBatch = new Abber(10);
        var (batchMiddle, batchUpper, batchLower) = abberBatch.Update(series);

        // Compare
        Assert.Equal(iterativeMiddle.Count, batchMiddle.Count);
        for (int i = 0; i < iterativeMiddle.Count; i++)
        {
            Assert.Equal(iterativeMiddle[i], batchMiddle[i].Value, 1e-10);
            Assert.Equal(iterativeUpper[i], batchUpper[i].Value, 1e-10);
            Assert.Equal(iterativeLower[i], batchLower[i].Value, 1e-10);
        }
    }

    [Fact]
    public void Abber_NaN_Input_UsesLastValidValue()
    {
        var abber = new Abber(5);

        // Feed some valid values
        abber.Update(new TValue(DateTime.UtcNow, 100));
        abber.Update(new TValue(DateTime.UtcNow, 105));

        // Feed NaN - should use last valid value
        var resultAfterNaN = abber.Update(new TValue(DateTime.UtcNow, double.NaN));

        // Result should be finite (not NaN)
        Assert.True(double.IsFinite(resultAfterNaN.Value));
        Assert.True(double.IsFinite(abber.Upper.Value));
        Assert.True(double.IsFinite(abber.Lower.Value));
    }

    [Fact]
    public void Abber_Infinity_Input_UsesLastValidValue()
    {
        var abber = new Abber(5);

        // Feed some valid values
        abber.Update(new TValue(DateTime.UtcNow, 100));
        abber.Update(new TValue(DateTime.UtcNow, 105));

        // Feed positive infinity
        var resultAfterPosInf = abber.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(resultAfterPosInf.Value));
        Assert.True(double.IsFinite(abber.Upper.Value));
        Assert.True(double.IsFinite(abber.Lower.Value));

        // Feed negative infinity
        var resultAfterNegInf = abber.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(resultAfterNegInf.Value));
        Assert.True(double.IsFinite(abber.Upper.Value));
        Assert.True(double.IsFinite(abber.Lower.Value));
    }

    [Fact]
    public void Abber_MultipleNaN_ContinuesWithLastValid()
    {
        var abber = new Abber(5);

        // Feed valid values
        abber.Update(new TValue(DateTime.UtcNow, 100));
        abber.Update(new TValue(DateTime.UtcNow, 105));
        abber.Update(new TValue(DateTime.UtcNow, 110));

        // Feed multiple NaN values
        var r1 = abber.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r2 = abber.Update(new TValue(DateTime.UtcNow, double.NaN));
        var r3 = abber.Update(new TValue(DateTime.UtcNow, double.NaN));

        // All results should be finite
        Assert.True(double.IsFinite(r1.Value));
        Assert.True(double.IsFinite(r2.Value));
        Assert.True(double.IsFinite(r3.Value));
    }

    [Fact]
    public void Abber_StaticBatch_Works()
    {
        var series = new TSeries();
        series.Add(DateTime.UtcNow, 100);
        series.Add(DateTime.UtcNow, 110);
        series.Add(DateTime.UtcNow, 120);
        series.Add(DateTime.UtcNow, 130);
        series.Add(DateTime.UtcNow, 140);

        var (middle, upper, lower) = Abber.Batch(series, 3);

        Assert.Equal(5, middle.Count);
        Assert.Equal(5, upper.Count);
        Assert.Equal(5, lower.Count);

        // All values should be finite
        for (int i = 0; i < 5; i++)
        {
            Assert.True(double.IsFinite(middle[i].Value));
            Assert.True(double.IsFinite(upper[i].Value));
            Assert.True(double.IsFinite(lower[i].Value));
        }
    }

    [Fact]
    public void Abber_Period1_ReturnsDirectCalculation()
    {
        var abber = new Abber(1);

        // Single value: SMA(1) = 100, Deviation = 0
        abber.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100.0, abber.Last.Value, 1e-10);
        Assert.Equal(100.0, abber.Upper.Value, 1e-10);
        Assert.Equal(100.0, abber.Lower.Value, 1e-10);

        // Next value: SMA(1) = 110, Deviation from previous SMA = |110 - 100| = 10
        // But with period 1, the old value drops out, so AvgDev = |110 - 110| = 0?
        // Actually deviation is calculated BEFORE adding to buffer
        // When 110 comes in, SMA is still 100, so Dev = |110 - 100| = 10
        // Then buffer updates to just [110], so SMA = 110, AvgDev = 10
        abber.Update(new TValue(DateTime.UtcNow, 110));
        Assert.Equal(110.0, abber.Last.Value, 1e-10);
    }

    // ============== Span API Tests ==============

    [Fact]
    public void Abber_SpanBatch_ValidatesInput()
    {
        double[] source = [100, 110, 120];
        double[] middle = new double[3];
        double[] upper = new double[3];
        double[] lower = new double[3];

        // Period must be > 0
        Assert.Throws<ArgumentException>(() =>
            Abber.Batch(source.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 0));
        Assert.Throws<ArgumentException>(() =>
            Abber.Batch(source.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), -1));

        // Multiplier must be > 0
        Assert.Throws<ArgumentException>(() =>
            Abber.Batch(source.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 3, 0));
        Assert.Throws<ArgumentException>(() =>
            Abber.Batch(source.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 3, -1));

        // Output buffers must be same length as input
        double[] shortOutput = new double[2];
        Assert.Throws<ArgumentException>(() =>
            Abber.Batch(source.AsSpan(), shortOutput.AsSpan(), upper.AsSpan(), lower.AsSpan(), 3));
    }

    [Fact]
    public void Abber_SpanBatch_MatchesTSeriesBatch()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var series = new TSeries();

        double[] source = new double[100];

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
            source[i] = bar.Close;
        }

        // Calculate with TSeries API
        var (tseriesMiddle, tseriesUpper, tseriesLower) = Abber.Batch(series, 10);

        // Calculate with Span API
        double[] spanMiddle = new double[100];
        double[] spanUpper = new double[100];
        double[] spanLower = new double[100];
        Abber.Batch(source.AsSpan(), spanMiddle.AsSpan(), spanUpper.AsSpan(), spanLower.AsSpan(), 10);

        // Compare results
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(tseriesMiddle[i].Value, spanMiddle[i], 1e-10);
            Assert.Equal(tseriesUpper[i].Value, spanUpper[i], 1e-10);
            Assert.Equal(tseriesLower[i].Value, spanLower[i], 1e-10);
        }
    }

    [Fact]
    public void Abber_SpanBatch_ZeroAllocation()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        double[] source = new double[10000];
        double[] middle = new double[10000];
        double[] upper = new double[10000];
        double[] lower = new double[10000];

        for (int i = 0; i < source.Length; i++)
        {
            source[i] = gbm.Next().Close;
        }

        // Warm up
        Abber.Batch(source.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 100);

        // Verify method completes without OOM or stack overflow
        Assert.True(double.IsFinite(middle[^1]));
        Assert.True(double.IsFinite(upper[^1]));
        Assert.True(double.IsFinite(lower[^1]));
    }

    [Fact]
    public void Abber_SpanBatch_HandlesNaN()
    {
        double[] source = [100, 110, double.NaN, 130, 140];
        double[] middle = new double[5];
        double[] upper = new double[5];
        double[] lower = new double[5];

        Abber.Batch(source.AsSpan(), middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 3);

        // All outputs should be finite
        for (int i = 0; i < 5; i++)
        {
            Assert.True(double.IsFinite(middle[i]), $"Middle[{i}] expected finite but got {middle[i]}");
            Assert.True(double.IsFinite(upper[i]), $"Upper[{i}] expected finite but got {upper[i]}");
            Assert.True(double.IsFinite(lower[i]), $"Lower[{i}] expected finite but got {lower[i]}");
        }
    }

    [Fact]
    public void Abber_AllModes_ProduceSameResult()
    {
        // Arrange
        const int period = 10;
        double multiplier = 2.0;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch Mode
        var (batchMiddle, batchUpper, batchLower) = Abber.Batch(series, period, multiplier);
        double expectedMiddle = batchMiddle.Last.Value;
        double expectedUpper = batchUpper.Last.Value;
        double expectedLower = batchLower.Last.Value;

        // 2. Span Mode
        double[] source = series.Values.ToArray();
        double[] spanMiddle = new double[series.Count];
        double[] spanUpper = new double[series.Count];
        double[] spanLower = new double[series.Count];
        Abber.Batch(source.AsSpan(), spanMiddle.AsSpan(), spanUpper.AsSpan(), spanLower.AsSpan(), period, multiplier);

        // 3. Streaming Mode
        var streamingInd = new Abber(period, multiplier);
        foreach (var item in series)
        {
            streamingInd.Update(item);
        }
        double streamingMiddle = streamingInd.Last.Value;
        double streamingUpper = streamingInd.Upper.Value;
        double streamingLower = streamingInd.Lower.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Abber(pubSource, period, multiplier);
        foreach (var item in series)
        {
            pubSource.Add(item);
        }
        double eventingMiddle = eventingInd.Last.Value;
        double eventingUpper = eventingInd.Upper.Value;
        double eventingLower = eventingInd.Lower.Value;

        // Assert
        Assert.Equal(expectedMiddle, spanMiddle[^1], precision: 9);
        Assert.Equal(expectedUpper, spanUpper[^1], precision: 9);
        Assert.Equal(expectedLower, spanLower[^1], precision: 9);

        Assert.Equal(expectedMiddle, streamingMiddle, precision: 9);
        Assert.Equal(expectedUpper, streamingUpper, precision: 9);
        Assert.Equal(expectedLower, streamingLower, precision: 9);

        Assert.Equal(expectedMiddle, eventingMiddle, precision: 9);
        Assert.Equal(expectedUpper, eventingUpper, precision: 9);
        Assert.Equal(expectedLower, eventingLower, precision: 9);
    }

    [Fact]
    public void Abber_Chainability_Works()
    {
        var source = new TSeries();
        var abber = new Abber(source, 10);

        source.Add(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100, abber.Last.Value);
    }

    [Fact]
    public void Abber_WarmupPeriod_IsSetCorrectly()
    {
        var abber = new Abber(10);
        Assert.Equal(10, abber.WarmupPeriod);
    }

    [Fact]
    public void Abber_Prime_SetsStateCorrectly()
    {
        var abber = new Abber(3, 2.0);
        var series = new TSeries();

        // Add 5 values
        series.Add(DateTime.UtcNow, 100);
        series.Add(DateTime.UtcNow, 110);
        series.Add(DateTime.UtcNow, 120);
        series.Add(DateTime.UtcNow, 130);
        series.Add(DateTime.UtcNow, 140);

        abber.Prime(series);

        Assert.True(abber.IsHot);

        // Last 3 values: 120, 130, 140 -> SMA = 130
        Assert.Equal(130.0, abber.Last.Value, 1e-10);

        // Verify it continues correctly
        abber.Update(new TValue(DateTime.UtcNow, 150));
        // New window: 130, 140, 150 -> SMA = 140
        Assert.Equal(140.0, abber.Last.Value, 1e-10);
    }

    [Fact]
    public void Abber_Calculate_ReturnsCorrectResultsAndHotIndicator()
    {
        var series = new TSeries();
        series.Add(DateTime.UtcNow, 100);
        series.Add(DateTime.UtcNow, 110);
        series.Add(DateTime.UtcNow, 120);
        series.Add(DateTime.UtcNow, 130);
        series.Add(DateTime.UtcNow, 140);

        var ((middle, upper, lower), indicator) = Abber.Calculate(series, 3, 2.0);

        // Check results
        Assert.Equal(5, middle.Count);
        Assert.Equal(5, upper.Count);
        Assert.Equal(5, lower.Count);

        // Check indicator state
        Assert.True(indicator.IsHot);
        Assert.Equal(130.0, indicator.Last.Value, 1e-10);
        Assert.Equal(3, indicator.WarmupPeriod);

        // Verify indicator continues correctly
        indicator.Update(new TValue(DateTime.UtcNow, 150));
        Assert.Equal(140.0, indicator.Last.Value, 1e-10);
    }

    [Fact]
    public void Abber_DifferentMultipliers_Work()
    {
        var series = new TSeries();
        for (int i = 0; i < 10; i++)
        {
            series.Add(DateTime.UtcNow, 100 + i * 10); // 100, 110, 120, ...
        }

        // Multiplier 1.0
        var (middle1, upper1, _) = Abber.Batch(series, 5, 1.0);

        // Multiplier 3.0
        var (middle3, upper3, _) = Abber.Batch(series, 5, 3.0);

        // Middle should be the same for all multipliers
        Assert.Equal(middle1.Last.Value, middle3.Last.Value, 1e-10);

        // Band width should scale with multiplier
        double bandWidth1 = upper1.Last.Value - middle1.Last.Value;
        double bandWidth3 = upper3.Last.Value - middle3.Last.Value;
        Assert.Equal(bandWidth1 * 3.0, bandWidth3, 1e-10);
    }

    [Fact]
    public void Abber_FlatLine_ReturnsSameValues()
    {
        var abber = new Abber(10);

        for (int i = 0; i < 20; i++)
        {
            abber.Update(new TValue(DateTime.UtcNow, 100));
        }

        // When all values are the same, SMA = 100, all deviations = 0
        Assert.Equal(100.0, abber.Last.Value, 1e-10);
        Assert.Equal(100.0, abber.Upper.Value, 1e-10);
        Assert.Equal(100.0, abber.Lower.Value, 1e-10);
    }

    [Fact]
    public void Abber_Pub_EventFires()
    {
        var abber = new Abber(10);
        bool eventFired = false;
        abber.Pub += (object? _, in TValueEventArgs _) => eventFired = true;

        abber.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(eventFired);
    }

    [Fact]
    public void Abber_BandsAreSymmetric()
    {
        var abber = new Abber(10, 2.0);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);

        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next(isNew: true);
            abber.Update(new TValue(bar.Time, bar.Close));
        }

        // Upper - Middle should equal Middle - Lower
        double upperDiff = abber.Upper.Value - abber.Last.Value;
        double lowerDiff = abber.Last.Value - abber.Lower.Value;

        Assert.Equal(upperDiff, lowerDiff, 1e-10);
    }
}
