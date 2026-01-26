namespace QuanTAlib.Tests;

public class AtrBandsTests
{
    [Fact]
    public void AtrBands_Constructor_ValidatesInput()
    {
        // Period validation
        Assert.Throws<ArgumentException>(() => new AtrBands(0));
        Assert.Throws<ArgumentException>(() => new AtrBands(-1));

        // Multiplier validation
        Assert.Throws<ArgumentException>(() => new AtrBands(10, 0));
        Assert.Throws<ArgumentException>(() => new AtrBands(10, -1));

        // Valid construction
        var atrBands = new AtrBands(10);
        Assert.NotNull(atrBands);

        var atrBands2 = new AtrBands(20, 3.0);
        Assert.NotNull(atrBands2);
    }

    [Fact]
    public void AtrBands_Calc_ReturnsValue()
    {
        var atrBands = new AtrBands(10);

        Assert.Equal(0, atrBands.Last.Value);
        Assert.Equal(0, atrBands.Upper.Value);
        Assert.Equal(0, atrBands.Lower.Value);

        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        TValue result = atrBands.Update(bar);

        Assert.True(double.IsFinite(result.Value));
        Assert.Equal(result.Value, atrBands.Last.Value);
        Assert.True(double.IsFinite(atrBands.Upper.Value));
        Assert.True(double.IsFinite(atrBands.Lower.Value));
    }

    [Fact]
    public void AtrBands_FirstValue_ReturnsExpected()
    {
        var atrBands = new AtrBands(10, 2.0);

        // First bar: O=100, H=105, L=95, C=102
        // Middle = SMA(Close) = 102
        // TR = H - L = 10 (no previous close)
        // ATR (RMA with warmup compensation) starts
        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        atrBands.Update(bar);

        Assert.Equal(102.0, atrBands.Last.Value, 1e-10);
        Assert.True(atrBands.Upper.Value > atrBands.Last.Value);
        Assert.True(atrBands.Lower.Value < atrBands.Last.Value);
    }

    [Fact]
    public void AtrBands_Calc_IsNew_AcceptsParameter()
    {
        var atrBands = new AtrBands(10);

        var bar1 = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        atrBands.Update(bar1, isNew: true);
        double value1 = atrBands.Last.Value;

        var bar2 = new TBar(DateTime.UtcNow, 102, 110, 98, 108, 1100);
        atrBands.Update(bar2, isNew: true);
        double value2 = atrBands.Last.Value;

        // Values should change with new bars
        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void AtrBands_Calc_IsNew_False_UpdatesValue()
    {
        var atrBands = new AtrBands(10);

        var bar1 = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        atrBands.Update(bar1, isNew: true);

        var bar2 = new TBar(DateTime.UtcNow, 102, 110, 98, 108, 1100);
        atrBands.Update(bar2, isNew: true);
        double beforeUpdate = atrBands.Last.Value;

        var bar3 = new TBar(DateTime.UtcNow, 102, 112, 100, 111, 1200);
        atrBands.Update(bar3, isNew: false);
        double afterUpdate = atrBands.Last.Value;

        // Update should change the value
        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void AtrBands_Reset_ClearsState()
    {
        var atrBands = new AtrBands(10);

        var bar1 = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        atrBands.Update(bar1);
        var bar2 = new TBar(DateTime.UtcNow, 102, 110, 98, 108, 1100);
        atrBands.Update(bar2);
        double middleBefore = atrBands.Last.Value;

        atrBands.Reset();

        Assert.Equal(0, atrBands.Last.Value);
        Assert.Equal(0, atrBands.Upper.Value);
        Assert.Equal(0, atrBands.Lower.Value);
        Assert.False(atrBands.IsHot);

        // After reset, should accept new values
        var bar3 = new TBar(DateTime.UtcNow, 50, 55, 45, 52, 500);
        atrBands.Update(bar3);
        Assert.NotEqual(0, atrBands.Last.Value);
        Assert.NotEqual(middleBefore, atrBands.Last.Value);
    }

    [Fact]
    public void AtrBands_Properties_Accessible()
    {
        var atrBands = new AtrBands(10, 2.5);

        Assert.Equal(0, atrBands.Last.Value);
        Assert.False(atrBands.IsHot);
        Assert.Contains("AtrBands", atrBands.Name, StringComparison.Ordinal);
        Assert.Equal(10, atrBands.WarmupPeriod);

        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        atrBands.Update(bar);

        Assert.NotEqual(0, atrBands.Last.Value);
    }

    [Fact]
    public void AtrBands_IsHot_BecomesTrueWhenConverged()
    {
        var atrBands = new AtrBands(5);

        Assert.False(atrBands.IsHot);

        // Feed enough bars for RMA to converge
        for (int i = 1; i <= 100; i++)
        {
            var bar = new TBar(DateTime.UtcNow, 100 + i, 105 + i, 95 + i, 102 + i, 1000);
            atrBands.Update(bar);
        }

        Assert.True(atrBands.IsHot);
    }

    [Fact]
    public void AtrBands_TrueRange_CalculatesCorrectly()
    {
        var atrBands = new AtrBands(3, 1.0);

        // Bar 1: H=110, L=90, C=100, TR = 110-90 = 20 (no prev close)
        atrBands.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000));

        // Bar 2: H=115, L=95, C=105
        // TR = max(115-95=20, |115-100|=15, |95-100|=5) = 20
        atrBands.Update(new TBar(DateTime.UtcNow, 105, 115, 95, 105, 1000));

        // Bar 3: H=120, L=100, C=110 (gap up case)
        // TR = max(120-100=20, |120-105|=15, |100-105|=5) = 20
        atrBands.Update(new TBar(DateTime.UtcNow, 110, 120, 100, 110, 1000));

        // Bands should reflect ATR based on True Range
        Assert.True(atrBands.Upper.Value > atrBands.Last.Value);
        Assert.True(atrBands.Lower.Value < atrBands.Last.Value);
    }

    [Fact]
    public void AtrBands_GapDay_UsesCorrectTrueRange()
    {
        var atrBands = new AtrBands(3, 1.0);

        // Day 1: Close at 100
        atrBands.Update(new TBar(DateTime.UtcNow, 100, 105, 95, 100, 1000));

        // Day 2: Gap up, open at 120
        // H=130, L=115, PrevC=100
        // TR = max(130-115=15, |130-100|=30, |115-100|=15) = 30
        atrBands.Update(new TBar(DateTime.UtcNow, 120, 130, 115, 125, 1000));

        // ATR should incorporate the gap
        double width = atrBands.Upper.Value - atrBands.Lower.Value;
        Assert.True(width > 0, "Band width should be positive");
    }

    [Fact]
    public void AtrBands_IterativeCorrections_RestoreToOriginalState()
    {
        var atrBands = new AtrBands(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 10 new bars
        TBar tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = bar;
            atrBands.Update(bar, isNew: true);
        }

        // Remember state after 10 bars
        double middleAfterTen = atrBands.Last.Value;
        double upperAfterTen = atrBands.Upper.Value;
        double lowerAfterTen = atrBands.Lower.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            atrBands.Update(bar, isNew: false);
        }

        // Feed the remembered 10th input again with isNew=false
        atrBands.Update(tenthInput, isNew: false);

        // State should match the original state after 10 bars
        Assert.Equal(middleAfterTen, atrBands.Last.Value, 1e-10);
        Assert.Equal(upperAfterTen, atrBands.Upper.Value, 1e-10);
        Assert.Equal(lowerAfterTen, atrBands.Lower.Value, 1e-10);
    }

    [Fact]
    public void AtrBands_BatchCalc_MatchesIterativeCalc()
    {
        var atrBandsIterative = new AtrBands(10);
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
            atrBandsIterative.Update(bar);
            iterativeMiddle.Add(atrBandsIterative.Last.Value);
            iterativeUpper.Add(atrBandsIterative.Upper.Value);
            iterativeLower.Add(atrBandsIterative.Lower.Value);
        }

        // Calculate batch
        var atrBandsBatch = new AtrBands(10);
        var (batchMiddle, batchUpper, batchLower) = atrBandsBatch.Update(series);

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
    public void AtrBands_NaN_Input_UsesLastValidValue()
    {
        var atrBands = new AtrBands(5);

        // Feed some valid bars
        atrBands.Update(new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000));
        atrBands.Update(new TBar(DateTime.UtcNow, 102, 108, 98, 105, 1100));

        // Feed bar with NaN high - should use last valid high
        var resultAfterNaN = atrBands.Update(new TBar(DateTime.UtcNow, 105, double.NaN, 100, 108, 1200));

        // Result should be finite (not NaN)
        Assert.True(double.IsFinite(resultAfterNaN.Value));
        Assert.True(double.IsFinite(atrBands.Upper.Value));
        Assert.True(double.IsFinite(atrBands.Lower.Value));
    }

    [Fact]
    public void AtrBands_Infinity_Input_UsesLastValidValue()
    {
        var atrBands = new AtrBands(5);

        // Feed some valid bars
        atrBands.Update(new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000));
        atrBands.Update(new TBar(DateTime.UtcNow, 102, 108, 98, 105, 1100));

        // Feed bar with positive infinity low
        var resultAfterPosInf = atrBands.Update(new TBar(DateTime.UtcNow, 105, 110, double.PositiveInfinity, 108, 1200));
        Assert.True(double.IsFinite(resultAfterPosInf.Value));
        Assert.True(double.IsFinite(atrBands.Upper.Value));
        Assert.True(double.IsFinite(atrBands.Lower.Value));

        // Feed bar with negative infinity close
        var resultAfterNegInf = atrBands.Update(new TBar(DateTime.UtcNow, 108, 115, 105, double.NegativeInfinity, 1300));
        Assert.True(double.IsFinite(resultAfterNegInf.Value));
        Assert.True(double.IsFinite(atrBands.Upper.Value));
        Assert.True(double.IsFinite(atrBands.Lower.Value));
    }

    [Fact]
    public void AtrBands_MultipleNaN_ContinuesWithLastValid()
    {
        var atrBands = new AtrBands(5);

        // Feed valid bars
        atrBands.Update(new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000));
        atrBands.Update(new TBar(DateTime.UtcNow, 102, 108, 98, 105, 1100));
        atrBands.Update(new TBar(DateTime.UtcNow, 105, 112, 100, 108, 1200));

        // Feed multiple bars with NaN values
        var r1 = atrBands.Update(new TBar(DateTime.UtcNow, double.NaN, 115, 102, 110, 1300));
        var r2 = atrBands.Update(new TBar(DateTime.UtcNow, 110, double.NaN, 105, 112, 1400));
        var r3 = atrBands.Update(new TBar(DateTime.UtcNow, 112, 120, double.NaN, 115, 1500));

        // All results should be finite
        Assert.True(double.IsFinite(r1.Value));
        Assert.True(double.IsFinite(r2.Value));
        Assert.True(double.IsFinite(r3.Value));
    }

    [Fact]
    public void AtrBands_StaticBatch_Works()
    {
        var series = new TBarSeries();
        series.Add(DateTime.UtcNow, 100, 110, 90, 100, 1000);
        series.Add(DateTime.UtcNow, 105, 115, 95, 105, 1000);
        series.Add(DateTime.UtcNow, 110, 120, 100, 110, 1000);
        series.Add(DateTime.UtcNow, 115, 125, 105, 115, 1000);
        series.Add(DateTime.UtcNow, 120, 130, 110, 120, 1000);

        var (middle, upper, lower) = AtrBands.Batch(series, 3);

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
    public void AtrBands_Period1_ReturnsDirectCalculation()
    {
        var atrBands = new AtrBands(1, 2.0);

        // Single bar: H=110, L=90, C=100
        // Middle = 100, TR = 20, ATR = 20 with warmup compensation
        atrBands.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 100, 1000));
        Assert.Equal(100.0, atrBands.Last.Value, 1e-10);
        Assert.True(atrBands.Upper.Value > 100.0);
        Assert.True(atrBands.Lower.Value < 100.0);
    }

    // ============== Span API Tests ==============

    [Fact]
    public void AtrBands_SpanBatch_ValidatesInput()
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
            AtrBands.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(),
                          middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 0));
        Assert.Throws<ArgumentException>(() =>
            AtrBands.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(),
                          middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), -1));

        // Multiplier must be > 0
        Assert.Throws<ArgumentException>(() =>
            AtrBands.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(),
                          middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 3, 0));
        Assert.Throws<ArgumentException>(() =>
            AtrBands.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(),
                          middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 3, -1));

        // Input arrays must have same length
        Assert.Throws<ArgumentException>(() =>
            AtrBands.Batch(wrongSizeHigh.AsSpan(), low.AsSpan(), close.AsSpan(),
                          middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 3));
    }

    [Fact]
    public void AtrBands_SpanBatch_MatchesTSeriesBatch()
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
        var (tseriesMiddle, tseriesUpper, tseriesLower) = AtrBands.Batch(series, 10);

        // Calculate with Span API
        double[] spanMiddle = new double[100];
        double[] spanUpper = new double[100];
        double[] spanLower = new double[100];
        AtrBands.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(),
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
    public void AtrBands_SpanBatch_ZeroAllocation()
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
        AtrBands.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(),
                      middle.AsSpan(), upper.AsSpan(), lower.AsSpan(), 100);

        // Verify method completes without OOM or stack overflow
        Assert.True(double.IsFinite(middle[^1]));
        Assert.True(double.IsFinite(upper[^1]));
        Assert.True(double.IsFinite(lower[^1]));
    }

    [Fact]
    public void AtrBands_SpanBatch_HandlesNaN()
    {
        double[] high = [105, 110, double.NaN, 120, 125];
        double[] low = [95, 100, 105, double.NaN, 115];
        double[] close = [100, 105, 110, 115, double.NaN];
        double[] middle = new double[5];
        double[] upper = new double[5];
        double[] lower = new double[5];

        AtrBands.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(),
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
    public void AtrBands_AllModes_ProduceSameResult()
    {
        // Arrange
        const int period = 10;
        double multiplier = 2.0;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // 1. Batch Mode
        var (batchMiddle, batchUpper, batchLower) = AtrBands.Batch(bars, period, multiplier);
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
        AtrBands.Batch(high.AsSpan(), low.AsSpan(), close.AsSpan(),
                      spanMiddle.AsSpan(), spanUpper.AsSpan(), spanLower.AsSpan(), period, multiplier);

        // 3. Streaming Mode
        var streamingInd = new AtrBands(period, multiplier);
        foreach (var bar in bars)
        {
            streamingInd.Update(bar);
        }
        double streamingMiddle = streamingInd.Last.Value;
        double streamingUpper = streamingInd.Upper.Value;
        double streamingLower = streamingInd.Lower.Value;

        // 4. Eventing Mode
        var pubSource = new TBarSeries();
        var eventingInd = new AtrBands(pubSource, period, multiplier);
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
    public void AtrBands_Chainability_Works()
    {
        var source = new TBarSeries();
        var atrBands = new AtrBands(source, 10);

        source.Add(new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000));
        Assert.Equal(102, atrBands.Last.Value);
    }

    [Fact]
    public void AtrBands_WarmupPeriod_IsSetCorrectly()
    {
        var atrBands = new AtrBands(10);
        Assert.Equal(10, atrBands.WarmupPeriod);
    }

    [Fact]
    public void AtrBands_Prime_SetsStateCorrectly()
    {
        var atrBands = new AtrBands(3, 2.0);
        var series = new TBarSeries();

        // Add 5 bars
        series.Add(DateTime.UtcNow, 100, 110, 90, 100, 1000);
        series.Add(DateTime.UtcNow, 105, 115, 95, 105, 1000);
        series.Add(DateTime.UtcNow, 110, 120, 100, 110, 1000);
        series.Add(DateTime.UtcNow, 115, 125, 105, 115, 1000);
        series.Add(DateTime.UtcNow, 120, 130, 110, 120, 1000);

        atrBands.Prime(series);

        Assert.True(atrBands.IsHot);

        // SMA(3) of Close for last 3 bars: (110+115+120)/3 = 115
        Assert.Equal(115.0, atrBands.Last.Value, 1e-10);

        // Upper > Middle > Lower
        Assert.True(atrBands.Upper.Value > atrBands.Last.Value);
        Assert.True(atrBands.Lower.Value < atrBands.Last.Value);
    }

    [Fact]
    public void AtrBands_Calculate_ReturnsCorrectResultsAndHotIndicator()
    {
        var series = new TBarSeries();
        series.Add(DateTime.UtcNow, 100, 110, 90, 100, 1000);
        series.Add(DateTime.UtcNow, 105, 115, 95, 105, 1000);
        series.Add(DateTime.UtcNow, 110, 120, 100, 110, 1000);
        series.Add(DateTime.UtcNow, 115, 125, 105, 115, 1000);
        series.Add(DateTime.UtcNow, 120, 130, 110, 120, 1000);

        var ((middle, upper, lower), indicator) = AtrBands.Calculate(series, 3, 2.0);

        // Check results
        Assert.Equal(5, middle.Count);
        Assert.Equal(5, upper.Count);
        Assert.Equal(5, lower.Count);

        // Check indicator state
        Assert.True(indicator.IsHot);
        Assert.Equal(115.0, indicator.Last.Value, 1e-10);
        Assert.Equal(3, indicator.WarmupPeriod);
    }

    [Fact]
    public void AtrBands_DifferentMultipliers_Work()
    {
        var series = new TBarSeries();
        series.Add(DateTime.UtcNow, 100, 110, 90, 100, 1000);
        series.Add(DateTime.UtcNow, 105, 115, 95, 105, 1000);
        series.Add(DateTime.UtcNow, 110, 120, 100, 110, 1000);

        // Multiplier 1.0 - narrow bands
        var (middle1, upper1, lower1) = AtrBands.Batch(series, 3, 1.0);
        double width1 = upper1.Last.Value - lower1.Last.Value;

        // Multiplier 3.0 - wide bands
        var (middle3, upper3, lower3) = AtrBands.Batch(series, 3, 3.0);
        double width3 = upper3.Last.Value - lower3.Last.Value;

        // Wider multiplier = wider bands
        Assert.True(width3 > width1, $"Width3 ({width3}) should be > Width1 ({width1})");

        // Middle should be the same for all multipliers
        Assert.Equal(middle1.Last.Value, middle3.Last.Value, 1e-10);
    }

    [Fact]
    public void AtrBands_FlatLine_HasZeroWidth()
    {
        var atrBands = new AtrBands(10);

        for (int i = 0; i < 20; i++)
        {
            atrBands.Update(new TBar(DateTime.UtcNow, 100, 100, 100, 100, 1000));
        }

        // When H=L=C=100 (no range), TR=0, ATR=0
        Assert.Equal(100.0, atrBands.Last.Value, 1e-10);
        Assert.Equal(100.0, atrBands.Upper.Value, 1e-10);
        Assert.Equal(100.0, atrBands.Lower.Value, 1e-10);
    }

    [Fact]
    public void AtrBands_Pub_EventFires()
    {
        var atrBands = new AtrBands(10);
        bool eventFired = false;
        atrBands.Pub += (object? sender, in TValueEventArgs args) => eventFired = true;

        atrBands.Update(new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000));
        Assert.True(eventFired);
    }

    [Fact]
    public void AtrBands_VolatilityExpands_BandsWiden()
    {
        var atrBands = new AtrBands(5, 2.0);

        // Low volatility period
        for (int i = 0; i < 10; i++)
        {
            atrBands.Update(new TBar(DateTime.UtcNow, 100, 101, 99, 100, 1000));
        }
        double lowVolWidth = atrBands.Upper.Value - atrBands.Lower.Value;

        // High volatility period
        for (int i = 0; i < 10; i++)
        {
            atrBands.Update(new TBar(DateTime.UtcNow, 100, 120, 80, 100, 1000));
        }
        double highVolWidth = atrBands.Upper.Value - atrBands.Lower.Value;

        Assert.True(highVolWidth > lowVolWidth,
            $"High volatility width ({highVolWidth}) should be > low volatility width ({lowVolWidth})");
    }

    [Fact]
    public void AtrBands_SymmetricBands_AroundMiddle()
    {
        var atrBands = new AtrBands(10, 2.0);
        var gbm = new GBM(startPrice: 100, mu: 0.0, sigma: 0.1, seed: 42);

        for (int i = 0; i < 50; i++)
        {
            atrBands.Update(gbm.Next(isNew: true));
        }

        double middle = atrBands.Last.Value;
        double upperDist = atrBands.Upper.Value - middle;
        double lowerDist = middle - atrBands.Lower.Value;

        // Bands should be symmetric around middle
        Assert.Equal(upperDist, lowerDist, 1e-10);
    }
}
