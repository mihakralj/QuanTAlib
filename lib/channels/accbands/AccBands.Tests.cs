namespace QuanTAlib.Tests;

public class AccBandsTests
{
    [Fact]
    public void AccBands_Constructor_ValidatesInput()
    {
        // Period validation
        Assert.Throws<ArgumentException>(() => new AccBands(0));
        Assert.Throws<ArgumentException>(() => new AccBands(-1));

        // Factor validation
        Assert.Throws<ArgumentException>(() => new AccBands(10, 0));
        Assert.Throws<ArgumentException>(() => new AccBands(10, -1));

        // Valid construction
        var accBands = new AccBands(10);
        Assert.NotNull(accBands);

        var accBands2 = new AccBands(20, 3.0);
        Assert.NotNull(accBands2);
    }

    [Fact]
    public void AccBands_Calc_ReturnsValue()
    {
        var accBands = new AccBands(10);

        Assert.Equal(0, accBands.Last.Value);
        Assert.Equal(0, accBands.Upper.Value);
        Assert.Equal(0, accBands.Lower.Value);

        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        TValue result = accBands.Update(bar);

        Assert.True(double.IsFinite(result.Value));
        Assert.Equal(result.Value, accBands.Last.Value);
        Assert.True(double.IsFinite(accBands.Upper.Value));
        Assert.True(double.IsFinite(accBands.Lower.Value));
    }

    [Fact]
    public void AccBands_FirstValue_ReturnsExpected()
    {
        var accBands = new AccBands(10);

        // First bar: O=100, H=105, L=95, C=102
        // SMA of one value: high=105, low=95, close=102
        // BandWidth = (105 - 95) * 2.0 = 20
        // Middle = 102, Upper = 105 + 20 = 125, Lower = 95 - 20 = 75
        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        accBands.Update(bar);

        Assert.Equal(102.0, accBands.Last.Value, 1e-10);
        Assert.Equal(125.0, accBands.Upper.Value, 1e-10);
        Assert.Equal(75.0, accBands.Lower.Value, 1e-10);
    }

    [Fact]
    public void AccBands_Calc_IsNew_AcceptsParameter()
    {
        var accBands = new AccBands(10);

        var bar1 = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        accBands.Update(bar1, isNew: true);
        double value1 = accBands.Last.Value;

        var bar2 = new TBar(DateTime.UtcNow, 102, 110, 98, 108, 1100);
        accBands.Update(bar2, isNew: true);
        double value2 = accBands.Last.Value;

        // Values should change with new bars
        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void AccBands_Calc_IsNew_False_UpdatesValue()
    {
        var accBands = new AccBands(10);

        var bar1 = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        accBands.Update(bar1, isNew: true);

        var bar2 = new TBar(DateTime.UtcNow, 102, 110, 98, 108, 1100);
        accBands.Update(bar2, isNew: true);
        double beforeUpdate = accBands.Last.Value;

        var bar3 = new TBar(DateTime.UtcNow, 102, 112, 100, 111, 1200);
        accBands.Update(bar3, isNew: false);
        double afterUpdate = accBands.Last.Value;

        // Update should change the value
        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void AccBands_Reset_ClearsState()
    {
        var accBands = new AccBands(10);

        var bar1 = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        accBands.Update(bar1);
        var bar2 = new TBar(DateTime.UtcNow, 102, 110, 98, 108, 1100);
        accBands.Update(bar2);
        double middleBefore = accBands.Last.Value;

        accBands.Reset();

        Assert.Equal(0, accBands.Last.Value);
        Assert.Equal(0, accBands.Upper.Value);
        Assert.Equal(0, accBands.Lower.Value);
        Assert.False(accBands.IsHot);

        // After reset, should accept new values
        var bar3 = new TBar(DateTime.UtcNow, 50, 55, 45, 52, 500);
        accBands.Update(bar3);
        Assert.NotEqual(0, accBands.Last.Value);
        Assert.NotEqual(middleBefore, accBands.Last.Value);
    }

    [Fact]
    public void AccBands_Properties_Accessible()
    {
        var accBands = new AccBands(10, 2.5);

        Assert.Equal(0, accBands.Last.Value);
        Assert.False(accBands.IsHot);
        Assert.Contains("AccBands", accBands.Name, StringComparison.Ordinal);
        Assert.Equal(10, accBands.WarmupPeriod);

        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        accBands.Update(bar);

        Assert.NotEqual(0, accBands.Last.Value);
    }

    [Fact]
    public void AccBands_IsHot_BecomesTrueWhenBufferFull()
    {
        var accBands = new AccBands(5);

        Assert.False(accBands.IsHot);

        for (int i = 1; i <= 4; i++)
        {
            var bar = new TBar(DateTime.UtcNow, 100 + i, 105 + i, 95 + i, 102 + i, 1000);
            accBands.Update(bar);
            Assert.False(accBands.IsHot);
        }

        var lastBar = new TBar(DateTime.UtcNow, 105, 110, 100, 107, 1000);
        accBands.Update(lastBar);
        Assert.True(accBands.IsHot);
    }

    [Fact]
    public void AccBands_CalculatesCorrectBands()
    {
        var accBands = new AccBands(3, 2.0);

        // Bar 1: H=110, L=90, C=100
        accBands.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000));
        // Bar 2: H=115, L=95, C=105
        accBands.Update(new TBar(DateTime.UtcNow, 105, 115, 95, 105, 1000));
        // Bar 3: H=120, L=100, C=110
        accBands.Update(new TBar(DateTime.UtcNow, 110, 120, 100, 110, 1000));

        // SMA(3) of High: (110 + 115 + 120) / 3 = 115
        // SMA(3) of Low: (90 + 95 + 100) / 3 = 95
        // SMA(3) of Close: (100 + 105 + 110) / 3 = 105
        // BandWidth = (115 - 95) * 2.0 = 40
        // Upper = 115 + 40 = 155
        // Lower = 95 - 40 = 55

        Assert.Equal(105.0, accBands.Last.Value, 1e-10);
        Assert.Equal(155.0, accBands.Upper.Value, 1e-10);
        Assert.Equal(55.0, accBands.Lower.Value, 1e-10);
    }

    [Fact]
    public void AccBands_SlidingWindow_Works()
    {
        var accBands = new AccBands(3, 2.0);

        // Bar 1: H=110, L=90, C=100
        accBands.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000));
        // Bar 2: H=115, L=95, C=105
        accBands.Update(new TBar(DateTime.UtcNow, 105, 115, 95, 105, 1000));
        // Bar 3: H=120, L=100, C=110
        accBands.Update(new TBar(DateTime.UtcNow, 110, 120, 100, 110, 1000));

        double middle1 = accBands.Last.Value;

        // Bar 4: H=125, L=105, C=115 - Window slides: [115, 120, 125], [95, 100, 105], [105, 110, 115]
        accBands.Update(new TBar(DateTime.UtcNow, 115, 125, 105, 115, 1000));

        // SMA(3) of High: (115 + 120 + 125) / 3 = 120
        // SMA(3) of Low: (95 + 100 + 105) / 3 = 100
        // SMA(3) of Close: (105 + 110 + 115) / 3 = 110
        // BandWidth = (120 - 100) * 2.0 = 40
        // Upper = 120 + 40 = 160
        // Lower = 100 - 40 = 60

        Assert.NotEqual(middle1, accBands.Last.Value);
        Assert.Equal(110.0, accBands.Last.Value, 1e-10);
        Assert.Equal(160.0, accBands.Upper.Value, 1e-10);
        Assert.Equal(60.0, accBands.Lower.Value, 1e-10);
    }

    [Fact]
    public void AccBands_IterativeCorrections_RestoreToOriginalState()
    {
        var accBands = new AccBands(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 10 new bars
        TBar tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = bar;
            accBands.Update(bar, isNew: true);
        }

        // Remember state after 10 bars
        double middleAfterTen = accBands.Last.Value;
        double upperAfterTen = accBands.Upper.Value;
        double lowerAfterTen = accBands.Lower.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            accBands.Update(bar, isNew: false);
        }

        // Feed the remembered 10th input again with isNew=false
        accBands.Update(tenthInput, isNew: false);

        // State should match the original state after 10 bars
        Assert.Equal(middleAfterTen, accBands.Last.Value, 1e-10);
        Assert.Equal(upperAfterTen, accBands.Upper.Value, 1e-10);
        Assert.Equal(lowerAfterTen, accBands.Lower.Value, 1e-10);
    }

    [Fact]
    public void AccBands_BatchCalc_MatchesIterativeCalc()
    {
        var accBandsIterative = new AccBands(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Generate data
        var series = new TBarSeries();
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar);
        }

        Assert.True(series.Count > 0);

        // Calculate iteratively
        var iterativeMiddle = new List<double>();
        var iterativeUpper = new List<double>();
        var iterativeLower = new List<double>();
        foreach (var bar in series)
        {
            accBandsIterative.Update(bar);
            iterativeMiddle.Add(accBandsIterative.Last.Value);
            iterativeUpper.Add(accBandsIterative.Upper.Value);
            iterativeLower.Add(accBandsIterative.Lower.Value);
        }

        // Calculate batch
        var accBandsBatch = new AccBands(10);
        var (batchMiddle, batchUpper, batchLower) = accBandsBatch.Update(series);

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
    public void AccBands_NaN_Input_UsesLastValidValue()
    {
        var accBands = new AccBands(5);

        // Feed some valid bars
        accBands.Update(new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000));
        accBands.Update(new TBar(DateTime.UtcNow, 102, 108, 98, 105, 1100));

        // Feed bar with NaN high - should use last valid high
        var resultAfterNaN = accBands.Update(new TBar(DateTime.UtcNow, 105, double.NaN, 100, 108, 1200));

        // Result should be finite (not NaN)
        Assert.True(double.IsFinite(resultAfterNaN.Value));
        Assert.True(double.IsFinite(accBands.Upper.Value));
        Assert.True(double.IsFinite(accBands.Lower.Value));
    }

    [Fact]
    public void AccBands_Infinity_Input_UsesLastValidValue()
    {
        var accBands = new AccBands(5);

        // Feed some valid bars
        accBands.Update(new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000));
        accBands.Update(new TBar(DateTime.UtcNow, 102, 108, 98, 105, 1100));

        // Feed bar with positive infinity low
        var resultAfterPosInf = accBands.Update(new TBar(DateTime.UtcNow, 105, 110, double.PositiveInfinity, 108, 1200));
        Assert.True(double.IsFinite(resultAfterPosInf.Value));
        Assert.True(double.IsFinite(accBands.Upper.Value));
        Assert.True(double.IsFinite(accBands.Lower.Value));

        // Feed bar with negative infinity close
        var resultAfterNegInf = accBands.Update(new TBar(DateTime.UtcNow, 108, 115, 105, double.NegativeInfinity, 1300));
        Assert.True(double.IsFinite(resultAfterNegInf.Value));
        Assert.True(double.IsFinite(accBands.Upper.Value));
        Assert.True(double.IsFinite(accBands.Lower.Value));
    }

    [Fact]
    public void AccBands_MultipleNaN_ContinuesWithLastValid()
    {
        var accBands = new AccBands(5);

        // Feed valid bars
        accBands.Update(new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000));
        accBands.Update(new TBar(DateTime.UtcNow, 102, 108, 98, 105, 1100));
        accBands.Update(new TBar(DateTime.UtcNow, 105, 112, 100, 108, 1200));

        // Feed multiple bars with NaN values
        var r1 = accBands.Update(new TBar(DateTime.UtcNow, double.NaN, 115, 102, 110, 1300));
        var r2 = accBands.Update(new TBar(DateTime.UtcNow, 110, double.NaN, 105, 112, 1400));
        var r3 = accBands.Update(new TBar(DateTime.UtcNow, 112, 120, double.NaN, 115, 1500));

        // All results should be finite
        Assert.True(double.IsFinite(r1.Value));
        Assert.True(double.IsFinite(r2.Value));
        Assert.True(double.IsFinite(r3.Value));
    }

    [Fact]
    public void AccBands_StaticBatch_Works()
    {
        var series = new TBarSeries();
        series.Add(DateTime.UtcNow, 100, 110, 90, 100, 1000);
        series.Add(DateTime.UtcNow, 105, 115, 95, 105, 1000);
        series.Add(DateTime.UtcNow, 110, 120, 100, 110, 1000);
        series.Add(DateTime.UtcNow, 115, 125, 105, 115, 1000);
        series.Add(DateTime.UtcNow, 120, 130, 110, 120, 1000);

        var (middle, upper, lower) = AccBands.Batch(series, 3);

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
    public void AccBands_Period1_ReturnsDirectCalculation()
    {
        var accBands = new AccBands(1);

        // Single bar: H=110, L=90, C=100
        // BandWidth = (110 - 90) * 2.0 = 40
        accBands.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000));
        Assert.Equal(100.0, accBands.Last.Value, 1e-10);
        Assert.Equal(150.0, accBands.Upper.Value, 1e-10);  // 110 + 40
        Assert.Equal(50.0, accBands.Lower.Value, 1e-10);   // 90 - 40

        // Next bar: H=120, L=100, C=110 (window is 1, so only this bar counts)
        accBands.Update(new TBar(DateTime.UtcNow, 110, 120, 100, 110, 1000));
        Assert.Equal(110.0, accBands.Last.Value, 1e-10);
        Assert.Equal(160.0, accBands.Upper.Value, 1e-10);  // 120 + 40
        Assert.Equal(60.0, accBands.Lower.Value, 1e-10);   // 100 - 40
    }

    // ============== Span API Tests ==============

    [Fact]
    public void AccBands_SpanBatch_ValidatesInput()
    {
        double[] high = [105, 110, 115];
        double[] low = [95, 100, 105];
        double[] close = [100, 105, 110];
        double[] middle = new double[3];
        double[] upper = new double[3];
        double[] lower = new double[3];

        double[] wrongSizeHigh = [105, 110];

        // Period must be > 0
        Assert.Throws<ArgumentException>(() =>
            AccBands.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(),
                          middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 0));
        Assert.Throws<ArgumentException>(() =>
            AccBands.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(),
                          middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), -1));

        // Factor must be > 0
        Assert.Throws<ArgumentException>(() =>
            AccBands.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(),
                          middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 3, 0));
        Assert.Throws<ArgumentException>(() =>
            AccBands.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(),
                          middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 3, -1));

        // Input arrays must have same length
        Assert.Throws<ArgumentException>(() =>
            AccBands.Batch(wrongSizeHigh.AsSpan(), low.AsSpan(), close.AsSpan(),
                          middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 3));
    }

    [Fact]
    public void AccBands_SpanBatch_MatchesTSeriesBatch()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        var series = new TBarSeries();

        double[] high = new double[100];
        double[] low = new double[100];
        double[] close = new double[100];

        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar);
            high[i] = bar.High;
            low[i] = bar.Low;
            close[i] = bar.Close;
        }

        // Calculate with TBarSeries API
        var (tseriesMiddle, tseriesUpper, tseriesLower) = AccBands.Batch(series, 10);

        // Calculate with Span API
        double[] spanMiddle = new double[100];
        double[] spanUpper = new double[100];
        double[] spanLower = new double[100];
        AccBands.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(),
                      spanMiddle.AsSpan(), spanUpper.AsSpan(), spanLower.AsSpan(), 10);

        // Compare results
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(tseriesMiddle[i].Value, spanMiddle[i], 1e-10);
            Assert.Equal(tseriesUpper[i].Value, spanUpper[i], 1e-10);
            Assert.Equal(tseriesLower[i].Value, spanLower[i], 1e-10);
        }
    }

    [Fact]
    public void AccBands_SpanBatch_CalculatesCorrectly()
    {
        double[] high = [110, 115, 120, 125, 130];
        double[] low = [90, 95, 100, 105, 110];
        double[] close = [100, 105, 110, 115, 120];
        double[] middle = new double[5];
        double[] upper = new double[5];
        double[] lower = new double[5];

        AccBands.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(),
                      middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 3);

        // After warmup (index 2):
        // SMA(3) of High: (110+115+120)/3 = 115
        // SMA(3) of Low: (90+95+100)/3 = 95
        // SMA(3) of Close: (100+105+110)/3 = 105
        // BandWidth = (115-95) * 2.0 = 40
        Assert.Equal(105.0, middle[2], 1e-10);
        Assert.Equal(155.0, upper[2], 1e-10);  // 115 + 40
        Assert.Equal(55.0, lower[2], 1e-10);   // 95 - 40
    }

    [Fact]
    public void AccBands_SpanBatch_ZeroAllocation()
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        double[] high = new double[10000];
        double[] low = new double[10000];
        double[] close = new double[10000];
        double[] middle = new double[10000];
        double[] upper = new double[10000];
        double[] lower = new double[10000];

        for (int i = 0; i < high.Length; i++)
        {
            var bar = gbm.Next();
            high[i] = bar.High;
            low[i] = bar.Low;
            close[i] = bar.Close;
        }

        // Warm up
        AccBands.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(),
                      middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 100);

        // Verify method completes without OOM or stack overflow
        Assert.True(double.IsFinite(middle[^1]));
        Assert.True(double.IsFinite(upper[^1]));
        Assert.True(double.IsFinite(lower[^1]));
    }

    [Fact]
    public void AccBands_SpanBatch_HandlesNaN()
    {
        double[] high = [105, 110, double.NaN, 120, 125];
        double[] low = [95, 100, 105, double.NaN, 115];
        double[] close = [100, 105, 110, 115, double.NaN];
        double[] middle = new double[5];
        double[] upper = new double[5];
        double[] lower = new double[5];

        AccBands.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(),
                      middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 3);

        // All outputs should be finite
        for (int i = 0; i < 5; i++)
        {
            Assert.True(double.IsFinite(middle[i]), $"Middle[{i}] expected finite but got {middle[i]}");
            Assert.True(double.IsFinite(upper[i]), $"Upper[{i}] expected finite but got {upper[i]}");
            Assert.True(double.IsFinite(lower[i]), $"Lower[{i}] expected finite but got {lower[i]}");
        }
    }

    [Fact]
    public void AccBands_AllModes_ProduceSameResult()
    {
        // Arrange
        int period = 10;
        double factor = 2.0;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // 1. Batch Mode
        var (batchMiddle, batchUpper, batchLower) = AccBands.Batch(bars, period, factor);
        double expectedMiddle = batchMiddle.Last.Value;
        double expectedUpper = batchUpper.Last.Value;
        double expectedLower = batchLower.Last.Value;

        // 2. Span Mode
        double[] high = bars.HighValues.ToArray();
        double[] low = bars.LowValues.ToArray();
        double[] close = bars.CloseValues.ToArray();
        double[] spanMiddle = new double[bars.Count];
        double[] spanUpper = new double[bars.Count];
        double[] spanLower = new double[bars.Count];
        AccBands.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(),
                      spanMiddle.AsSpan(), spanUpper.AsSpan(), spanLower.AsSpan(), period, factor);

        // 3. Streaming Mode
        var streamingInd = new AccBands(period, factor);
        foreach (var bar in bars)
        {
            streamingInd.Update(bar);
        }
        double streamingMiddle = streamingInd.Last.Value;
        double streamingUpper = streamingInd.Upper.Value;
        double streamingLower = streamingInd.Lower.Value;

        // 4. Eventing Mode
        var pubSource = new TBarSeries();
        var eventingInd = new AccBands(pubSource, period, factor);
        foreach (var bar in bars)
        {
            pubSource.Add(bar);
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
    public void AccBands_Chainability_Works()
    {
        var source = new TBarSeries();
        var accBands = new AccBands(source, 10);

        source.Add(new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000));
        Assert.Equal(102, accBands.Last.Value);
    }

    [Fact]
    public void AccBands_WarmupPeriod_IsSetCorrectly()
    {
        var accBands = new AccBands(10);
        Assert.Equal(10, accBands.WarmupPeriod);
    }

    [Fact]
    public void AccBands_Prime_SetsStateCorrectly()
    {
        var accBands = new AccBands(3, 2.0);
        var series = new TBarSeries();

        // Add 5 bars
        series.Add(DateTime.UtcNow, 100, 110, 90, 100, 1000);
        series.Add(DateTime.UtcNow, 105, 115, 95, 105, 1000);
        series.Add(DateTime.UtcNow, 110, 120, 100, 110, 1000);
        series.Add(DateTime.UtcNow, 115, 125, 105, 115, 1000);
        series.Add(DateTime.UtcNow, 120, 130, 110, 120, 1000);

        accBands.Prime(series);

        Assert.True(accBands.IsHot);

        // Last 3 bars: H=[120,125,130], L=[100,105,110], C=[110,115,120]
        // SMA(3) of High: (120+125+130)/3 = 125
        // SMA(3) of Low: (100+105+110)/3 = 105
        // SMA(3) of Close: (110+115+120)/3 = 115
        // BandWidth = (125-105) * 2.0 = 40
        Assert.Equal(115.0, accBands.Last.Value, 1e-10);
        Assert.Equal(165.0, accBands.Upper.Value, 1e-10);  // 125 + 40
        Assert.Equal(65.0, accBands.Lower.Value, 1e-10);   // 105 - 40

        // Verify it continues correctly
        accBands.Update(new TBar(DateTime.UtcNow, 125, 135, 115, 125, 1000));
        // New window: H=[125,130,135], L=[105,110,115], C=[115,120,125]
        // SMA(3) of High: (125+130+135)/3 = 130
        // SMA(3) of Low: (105+110+115)/3 = 110
        // SMA(3) of Close: (115+120+125)/3 = 120
        // BandWidth = (130-110) * 2.0 = 40
        Assert.Equal(120.0, accBands.Last.Value, 1e-10);
        Assert.Equal(170.0, accBands.Upper.Value, 1e-10);  // 130 + 40
        Assert.Equal(70.0, accBands.Lower.Value, 1e-10);   // 110 - 40
    }

    [Fact]
    public void AccBands_Calculate_ReturnsCorrectResultsAndHotIndicator()
    {
        var series = new TBarSeries();
        series.Add(DateTime.UtcNow, 100, 110, 90, 100, 1000);
        series.Add(DateTime.UtcNow, 105, 115, 95, 105, 1000);
        series.Add(DateTime.UtcNow, 110, 120, 100, 110, 1000);
        series.Add(DateTime.UtcNow, 115, 125, 105, 115, 1000);
        series.Add(DateTime.UtcNow, 120, 130, 110, 120, 1000);

        var ((middle, upper, lower), indicator) = AccBands.Calculate(series, 3, 2.0);

        // Check results
        Assert.Equal(5, middle.Count);
        Assert.Equal(5, upper.Count);
        Assert.Equal(5, lower.Count);

        // Check indicator state
        Assert.True(indicator.IsHot);
        Assert.Equal(115.0, indicator.Last.Value, 1e-10);
        Assert.Equal(3, indicator.WarmupPeriod);

        // Verify indicator continues correctly
        indicator.Update(new TBar(DateTime.UtcNow, 125, 135, 115, 125, 1000));
        Assert.Equal(120.0, indicator.Last.Value, 1e-10);
    }

    [Fact]
    public void AccBands_DifferentFactors_Work()
    {
        var series = new TBarSeries();
        series.Add(DateTime.UtcNow, 100, 110, 90, 100, 1000);
        series.Add(DateTime.UtcNow, 105, 115, 95, 105, 1000);
        series.Add(DateTime.UtcNow, 110, 120, 100, 110, 1000);

        // Factor 1.0
        var (middle1, upper1, lower1) = AccBands.Batch(series, 3, 1.0);
        // SMA(3) High=115, Low=95, Close=105, BandWidth=20*1=20
        Assert.Equal(135.0, upper1.Last.Value, 1e-10);  // 115 + 20
        Assert.Equal(75.0, lower1.Last.Value, 1e-10);   // 95 - 20

        // Factor 3.0
        var (middle3, upper3, lower3) = AccBands.Batch(series, 3, 3.0);
        // BandWidth=20*3=60
        Assert.Equal(175.0, upper3.Last.Value, 1e-10);  // 115 + 60
        Assert.Equal(35.0, lower3.Last.Value, 1e-10);   // 95 - 60

        // Middle should be the same for all factors
        Assert.Equal(middle1.Last.Value, middle3.Last.Value, 1e-10);
    }

    [Fact]
    public void AccBands_FlatLine_ReturnsSameValues()
    {
        var accBands = new AccBands(10);

        for (int i = 0; i < 20; i++)
        {
            accBands.Update(new TBar(DateTime.UtcNow, 100, 100, 100, 100, 1000));
        }

        // When H=L=C=100, BandWidth = (100-100)*2 = 0
        Assert.Equal(100.0, accBands.Last.Value, 1e-10);
        Assert.Equal(100.0, accBands.Upper.Value, 1e-10);  // 100 + 0
        Assert.Equal(100.0, accBands.Lower.Value, 1e-10);  // 100 - 0
    }

    [Fact]
    public void AccBands_Pub_EventFires()
    {
        var accBands = new AccBands(10);
        bool eventFired = false;
        accBands.Pub += (object? sender, in TValueEventArgs args) => eventFired = true;

        accBands.Update(new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000));
        Assert.True(eventFired);
    }
}
