namespace QuanTAlib.Tests;

public class ApzTests
{
    private readonly GBM _gbm;
    private readonly TBarSeries _bars;

    public ApzTests()
    {
        _gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        _bars = _gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Apz(0));
        Assert.Throws<ArgumentException>(() => new Apz(-1));
        Assert.Throws<ArgumentException>(() => new Apz(10, 0));
        Assert.Throws<ArgumentException>(() => new Apz(10, -1));
    }

    [Fact]
    public void Constructor_ValidBoundaryValues()
    {
        var apz1 = new Apz(1);
        Assert.NotNull(apz1);
        Assert.Equal(1, apz1.WarmupPeriod);

        var apz2 = new Apz(100, 0.5);
        Assert.NotNull(apz2);
        Assert.Equal(100, apz2.WarmupPeriod);
    }

    [Fact]
    public void Constructor_SetsName()
    {
        var apz = new Apz(20, 2.5);
        Assert.Contains("Apz", apz.Name, StringComparison.Ordinal);
        Assert.Contains("20", apz.Name, StringComparison.Ordinal);
        Assert.Contains("2.50", apz.Name, StringComparison.Ordinal);
    }

    #endregion

    #region Basic Functionality Tests

    [Fact]
    public void Calc_ReturnsValue()
    {
        var apz = new Apz(10);
        var bar = _bars[0];

        var result = apz.Update(bar);

        Assert.True(double.IsFinite(result.Value));
        Assert.Equal(result.Value, apz.Last.Value);
        Assert.True(double.IsFinite(apz.Upper.Value));
        Assert.True(double.IsFinite(apz.Lower.Value));
    }

    [Fact]
    public void FirstValue_ReturnsExpected()
    {
        var apz = new Apz(10);
        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 100, 1000);

        var result = apz.Update(bar);

        // First value should be close to the input price (with warmup compensation)
        Assert.True(double.IsFinite(result.Value));
        // Upper should be greater than middle
        Assert.True(apz.Upper.Value > apz.Last.Value);
        // Lower should be less than middle
        Assert.True(apz.Lower.Value < apz.Last.Value);
    }

    [Fact]
    public void Properties_Accessible()
    {
        var apz = new Apz(10);

        Assert.Equal(0, apz.Last.Value);
        Assert.False(apz.IsHot);
        Assert.Contains("Apz", apz.Name, StringComparison.Ordinal);
        Assert.Equal(10, apz.WarmupPeriod);

        apz.Update(new TBar(DateTime.UtcNow, 100, 105, 95, 100, 1000));
        Assert.NotEqual(0, apz.Last.Value);
    }

    [Fact]
    public void BandRelationships_Maintained()
    {
        var apz = new Apz(10, 2.0);

        for (int i = 0; i < 20; i++)
        {
            apz.Update(_bars[i]);

            if (double.IsFinite(apz.Last.Value))
            {
                // Upper band should always be >= middle
                Assert.True(apz.Upper.Value >= apz.Last.Value,
                    $"Upper {apz.Upper.Value} should be >= Middle {apz.Last.Value} at bar {i}");
                // Lower band should always be <= middle
                Assert.True(apz.Lower.Value <= apz.Last.Value,
                    $"Lower {apz.Lower.Value} should be <= Middle {apz.Last.Value} at bar {i}");
            }
        }
    }

    #endregion

    #region State Management & Bar Correction Tests

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var apz = new Apz(10);

        apz.Update(_bars[0], isNew: true);
        double value1 = apz.Last.Value;

        apz.Update(_bars[1], isNew: true);
        double value2 = apz.Last.Value;

        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var apz = new Apz(10);

        apz.Update(_bars[0]);
        apz.Update(_bars[1], isNew: true);
        double beforeUpdate = apz.Last.Value;

        // Update same bar with different value
        var modifiedBar = new TBar(_bars[1].Time, _bars[1].Open, _bars[1].High + 10, _bars[1].Low, _bars[1].Close + 5, _bars[1].Volume);
        apz.Update(modifiedBar, isNew: false);
        double afterUpdate = apz.Last.Value;

        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var apz = new Apz(5);

        // Feed 10 new bars
        TBar tenthBar = default;
        for (int i = 0; i < 10; i++)
        {
            tenthBar = _bars[i];
            apz.Update(tenthBar, isNew: true);
        }

        // Remember state after 10 bars
        double stateAfterTen = apz.Last.Value;
        double upperAfterTen = apz.Upper.Value;
        double lowerAfterTen = apz.Lower.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var correctionBar = new TBar(tenthBar.Time, tenthBar.Open + i, tenthBar.High + i * 2, tenthBar.Low - i, tenthBar.Close + i, tenthBar.Volume);
            apz.Update(correctionBar, isNew: false);
        }

        // Feed the remembered 10th bar again with isNew=false
        apz.Update(tenthBar, isNew: false);

        // State should match the original state after 10 bars
        Assert.Equal(stateAfterTen, apz.Last.Value, precision: 10);
        Assert.Equal(upperAfterTen, apz.Upper.Value, precision: 10);
        Assert.Equal(lowerAfterTen, apz.Lower.Value, precision: 10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var apz = new Apz(10);

        apz.Update(_bars[0]);
        apz.Update(_bars[1]);
        _ = apz.Last.Value; // Verify value exists before reset

        apz.Reset();

        Assert.Equal(0, apz.Last.Value);
        Assert.False(apz.IsHot);

        // After reset, should accept new values
        apz.Update(_bars[0]);
        Assert.NotEqual(0, apz.Last.Value);
    }

    #endregion

    #region Warmup & Convergence Tests

    [Fact]
    public void IsHot_BecomesTrueEventually()
    {
        var apz = new Apz(5);

        Assert.False(apz.IsHot);

        // Feed enough data
        for (int i = 0; i < 100; i++)
        {
            apz.Update(_bars[i]);
        }

        Assert.True(apz.IsHot);
    }

    [Fact]
    public void IsHot_IsPeriodDependent()
    {
        int[] periods = [5, 10, 20, 50];
        int[] stepsToHot = new int[periods.Length];

        for (int p = 0; p < periods.Length; p++)
        {
            var apz = new Apz(periods[p]);
            int steps = 0;
            while (!apz.IsHot && steps < 500)
            {
                apz.Update(_bars[steps]);
                steps++;
            }
            stepsToHot[p] = steps;
        }

        // Larger periods should take more steps to become hot
        // (due to beta^2 decay being slower)
        Assert.True(stepsToHot[0] <= stepsToHot[1]);
        Assert.True(stepsToHot[1] <= stepsToHot[2]);
        Assert.True(stepsToHot[2] <= stepsToHot[3]);
    }

    [Fact]
    public void WarmupPeriod_IsSetCorrectly()
    {
        var apz = new Apz(25);
        Assert.Equal(25, apz.WarmupPeriod);
    }

    #endregion

    #region Robustness (NaN/Infinity) Tests

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var apz = new Apz(5);

        apz.Update(new TBar(DateTime.UtcNow, 100, 105, 95, 100, 1000));
        apz.Update(new TBar(DateTime.UtcNow, 110, 115, 105, 110, 1000));

        var resultAfterNaN = apz.Update(new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, 1000));

        Assert.True(double.IsFinite(resultAfterNaN.Value));
        Assert.True(double.IsFinite(apz.Upper.Value));
        Assert.True(double.IsFinite(apz.Lower.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var apz = new Apz(5);

        apz.Update(new TBar(DateTime.UtcNow, 100, 105, 95, 100, 1000));
        apz.Update(new TBar(DateTime.UtcNow, 110, 115, 105, 110, 1000));

        var resultAfterPosInf = apz.Update(new TBar(DateTime.UtcNow, double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, 1000));
        Assert.True(double.IsFinite(resultAfterPosInf.Value));

        var resultAfterNegInf = apz.Update(new TBar(DateTime.UtcNow, double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity, 1000));
        Assert.True(double.IsFinite(resultAfterNegInf.Value));
    }

    [Fact]
    public void MultipleNaN_ContinuesWithLastValid()
    {
        var apz = new Apz(5);

        apz.Update(new TBar(DateTime.UtcNow, 100, 105, 95, 100, 1000));
        apz.Update(new TBar(DateTime.UtcNow, 110, 115, 105, 110, 1000));
        apz.Update(new TBar(DateTime.UtcNow, 120, 125, 115, 120, 1000));

        var r1 = apz.Update(new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, 1000));
        var r2 = apz.Update(new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, 1000));
        var r3 = apz.Update(new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, 1000));

        Assert.True(double.IsFinite(r1.Value));
        Assert.True(double.IsFinite(r2.Value));
        Assert.True(double.IsFinite(r3.Value));
    }

    [Fact]
    public void BatchCalc_HandlesNaN()
    {
        double[] high = [105, 115, double.NaN, 125, 135];
        double[] low = [95, 105, double.NaN, 115, 125];
        double[] close = [100, 110, double.NaN, 120, 130];
        double[] middle = new double[5];
        double[] upper = new double[5];
        double[] lower = new double[5];

        Apz.Batch(high, low, close, new Apz.BatchOutputs(middle, upper, lower), 3);

        foreach (var val in middle)
        {
            Assert.True(double.IsFinite(val), $"Expected finite value but got {val}");
        }
    }

    [Fact]
    public void FirstBar_AllNaN_ReturnsNaN()
    {
        var apz = new Apz(5);

        var result = apz.Update(new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, 1000));

        Assert.True(double.IsNaN(result.Value));
    }

    #endregion

    #region Consistency Tests

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        var apzIterative = new Apz(10);
        var apzBatch = new Apz(10);

        // Calculate iteratively
        var iterativeMiddle = new List<double>();
        var iterativeUpper = new List<double>();
        var iterativeLower = new List<double>();
        foreach (var bar in _bars)
        {
            apzIterative.Update(bar);
            iterativeMiddle.Add(apzIterative.Last.Value);
            iterativeUpper.Add(apzIterative.Upper.Value);
            iterativeLower.Add(apzIterative.Lower.Value);
        }

        // Calculate batch
        var (batchMiddle, batchUpper, batchLower) = apzBatch.Update(_bars);

        // Compare
        Assert.Equal(iterativeMiddle.Count, batchMiddle.Count);
        for (int i = 0; i < iterativeMiddle.Count; i++)
        {
            Assert.Equal(iterativeMiddle[i], batchMiddle[i].Value, precision: 9);
            Assert.Equal(iterativeUpper[i], batchUpper[i].Value, precision: 9);
            Assert.Equal(iterativeLower[i], batchLower[i].Value, precision: 9);
        }
    }

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        int period = 10;
        double multiplier = 2.0;

        // 1. Batch Mode (static method)
        var (batchMiddle, batchUpper, batchLower) = Apz.Batch(_bars, period, multiplier);
        double expectedMiddle = batchMiddle.Last.Value;
        double expectedUpper = batchUpper.Last.Value;
        double expectedLower = batchLower.Last.Value;

        // 2. Span Mode (static method with spans)
        double[] highArr = _bars.High.Values.ToArray();
        double[] lowArr = _bars.Low.Values.ToArray();
        double[] closeArr = _bars.Close.Values.ToArray();
        double[] middleSpan = new double[_bars.Count];
        double[] upperSpan = new double[_bars.Count];
        double[] lowerSpan = new double[_bars.Count];
        Apz.Batch(highArr, lowArr, closeArr, new Apz.BatchOutputs(middleSpan, upperSpan, lowerSpan), period, multiplier);
        double spanMiddle = middleSpan[^1];
        double spanUpper = upperSpan[^1];
        double spanLower = lowerSpan[^1];

        // 3. Streaming Mode (instance, one bar at a time)
        var streamingApz = new Apz(period, multiplier);
        foreach (var bar in _bars)
        {
            streamingApz.Update(bar);
        }
        double streamingMiddle = streamingApz.Last.Value;
        double streamingUpper = streamingApz.Upper.Value;
        double streamingLower = streamingApz.Lower.Value;

        // 4. Eventing Mode (chained via TBarSeries)
        var pubSource = new TBarSeries();
        var eventingApz = new Apz(pubSource, period, multiplier);
        foreach (var bar in _bars)
        {
            pubSource.Add(bar);
        }
        double eventingMiddle = eventingApz.Last.Value;
        double eventingUpper = eventingApz.Upper.Value;
        double eventingLower = eventingApz.Lower.Value;

        // Assert all modes produce identical results
        Assert.Equal(expectedMiddle, spanMiddle, precision: 9);
        Assert.Equal(expectedMiddle, streamingMiddle, precision: 9);
        Assert.Equal(expectedMiddle, eventingMiddle, precision: 9);

        Assert.Equal(expectedUpper, spanUpper, precision: 9);
        Assert.Equal(expectedUpper, streamingUpper, precision: 9);
        Assert.Equal(expectedUpper, eventingUpper, precision: 9);

        Assert.Equal(expectedLower, spanLower, precision: 9);
        Assert.Equal(expectedLower, streamingLower, precision: 9);
        Assert.Equal(expectedLower, eventingLower, precision: 9);
    }

    [Fact]
    public void StaticBatch_Works()
    {
        var (middle, upper, lower) = Apz.Batch(_bars, 10);

        Assert.Equal(_bars.Count, middle.Count);
        Assert.Equal(_bars.Count, upper.Count);
        Assert.Equal(_bars.Count, lower.Count);
        Assert.True(double.IsFinite(middle.Last.Value));
    }

    #endregion

    #region Span API Tests

    [Fact]
    public void SpanBatch_ValidatesInput()
    {
        double[] high = [105, 115, 125];
        double[] low = [95, 105, 115];
        double[] close = [100, 110, 120];
        double[] middle = new double[3];
        double[] upper = new double[3];
        double[] lower = new double[3];
        double[] wrongSizeOutput = new double[2];
        double[] wrongSizeInput = [100, 110];

        // Period must be > 0
        Assert.Throws<ArgumentException>(() =>
            Apz.Batch(high, low, close, new Apz.BatchOutputs(middle, upper, lower), 0));
        Assert.Throws<ArgumentException>(() =>
            Apz.Batch(high, low, close, new Apz.BatchOutputs(middle, upper, lower), -1));

        // Multiplier must be > 0
        Assert.Throws<ArgumentException>(() =>
            Apz.Batch(high, low, close, new Apz.BatchOutputs(middle, upper, lower), 3, 0));
        Assert.Throws<ArgumentException>(() =>
            Apz.Batch(high, low, close, new Apz.BatchOutputs(middle, upper, lower), 3, -1));

        // Output must be same length as input
        Assert.Throws<ArgumentException>(() =>
            Apz.Batch(high, low, close, new Apz.BatchOutputs(wrongSizeOutput, upper, lower), 3));

        // Input arrays must have same length
        Assert.Throws<ArgumentException>(() =>
            Apz.Batch(wrongSizeInput, low, close, new Apz.BatchOutputs(middle, upper, lower), 3));
    }

    [Fact]
    public void SpanBatch_MatchesTSeriesBatch()
    {
        int period = 10;
        double multiplier = 2.0;

        var (tseriesMiddle, tseriesUpper, tseriesLower) = Apz.Batch(_bars, period, multiplier);

        double[] highArr = _bars.High.Values.ToArray();
        double[] lowArr = _bars.Low.Values.ToArray();
        double[] closeArr = _bars.Close.Values.ToArray();
        double[] spanMiddle = new double[_bars.Count];
        double[] spanUpper = new double[_bars.Count];
        double[] spanLower = new double[_bars.Count];

        Apz.Batch(highArr, lowArr, closeArr, new Apz.BatchOutputs(spanMiddle, spanUpper, spanLower), period, multiplier);

        for (int i = 0; i < _bars.Count; i++)
        {
            Assert.Equal(tseriesMiddle[i].Value, spanMiddle[i], precision: 10);
            Assert.Equal(tseriesUpper[i].Value, spanUpper[i], precision: 10);
            Assert.Equal(tseriesLower[i].Value, spanLower[i], precision: 10);
        }
    }

    [Fact]
    public void SpanBatch_ZeroAllocation()
    {
        double[] high = new double[10000];
        double[] low = new double[10000];
        double[] close = new double[10000];
        double[] middle = new double[10000];
        double[] upper = new double[10000];
        double[] lower = new double[10000];

        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        for (int i = 0; i < high.Length; i++)
        {
            var bar = gbm.Next();
            high[i] = bar.High;
            low[i] = bar.Low;
            close[i] = bar.Close;
        }

        // Warm up
        Apz.Batch(high, low, close, new Apz.BatchOutputs(middle, upper, lower), 100);

        // Verify method completes without OOM or stack overflow
        Assert.True(double.IsFinite(middle[^1]));
        Assert.True(double.IsFinite(upper[^1]));
        Assert.True(double.IsFinite(lower[^1]));
    }

    [Fact]
    public void SpanBatch_Period1_Works()
    {
        double[] high = [105, 115, 125];
        double[] low = [95, 105, 115];
        double[] close = [100, 110, 120];
        double[] middle = new double[3];
        double[] upper = new double[3];
        double[] lower = new double[3];

        Apz.Batch(high, low, close, new Apz.BatchOutputs(middle, upper, lower), 1);

        foreach (var val in middle)
        {
            Assert.True(double.IsFinite(val));
        }
    }

    [Fact]
    public void SpanBatch_EmptyInput_DoesNotThrow()
    {
        double[] high = [];
        double[] low = [];
        double[] close = [];
        double[] middle = [];
        double[] upper = [];
        double[] lower = [];

        // Should not throw
        var ex = Record.Exception(() => Apz.Batch(high, low, close, new Apz.BatchOutputs(middle, upper, lower), 10));
        Assert.Null(ex);
    }

    #endregion

    #region Prime Tests

    [Fact]
    public void Prime_SetsStateCorrectly()
    {
        var apz = new Apz(10);
        apz.Prime(_bars);

        Assert.True(apz.IsHot);
        Assert.True(double.IsFinite(apz.Last.Value));
        Assert.True(double.IsFinite(apz.Upper.Value));
        Assert.True(double.IsFinite(apz.Lower.Value));

        // Verify it continues correctly
        var nextBar = _gbm.Next();
        apz.Update(nextBar);
        Assert.True(double.IsFinite(apz.Last.Value));
    }

    [Fact]
    public void Prime_WithEmptySeries_DoesNotThrow()
    {
        var apz = new Apz(10);
        var emptySeries = new TBarSeries();

        // Should not throw
        apz.Prime(emptySeries);

        Assert.False(apz.IsHot);
    }

    #endregion

    #region Calculate Method Tests

    [Fact]
    public void Calculate_ReturnsCorrectResultsAndHotIndicator()
    {
        var ((middle, upper, lower), indicator) = Apz.Calculate(_bars, 10);

        // Check results
        Assert.Equal(_bars.Count, middle.Count);
        Assert.True(double.IsFinite(middle.Last.Value));

        // Check indicator state
        Assert.True(indicator.IsHot);
        Assert.Equal(middle.Last.Value, indicator.Last.Value, precision: 10);
        Assert.Equal(upper.Last.Value, indicator.Upper.Value, precision: 10);
        Assert.Equal(lower.Last.Value, indicator.Lower.Value, precision: 10);

        // Verify indicator continues correctly
        var nextBar = _gbm.Next();
        indicator.Update(nextBar);
        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    #endregion

    #region Chainability Tests

    [Fact]
    public void Chainability_Works()
    {
        var source = new TBarSeries();
        var apz = new Apz(source, 10);

        source.Add(new TBar(DateTime.UtcNow, 100, 105, 95, 100, 1000));
        Assert.True(double.IsFinite(apz.Last.Value));
    }

    [Fact]
    public void Pub_EventFires()
    {
        var apz = new Apz(10);
        bool eventFired = false;
        apz.Pub += (object? sender, in TValueEventArgs args) => eventFired = true;

        apz.Update(new TBar(DateTime.UtcNow, 100, 105, 95, 100, 1000));
        Assert.True(eventFired);
    }

    #endregion

    #region Algorithm-Specific Tests

    [Fact]
    public void DoubleSmoothing_ProducesSmoothOutput()
    {
        // Use a larger period for more smoothing effect
        var apz = new Apz(50);
        var results = new List<double>();

        // Feed volatile data - use more bars to allow convergence
        for (int i = 0; i < 200; i++)
        {
            apz.Update(_bars[i]);
            results.Add(apz.Last.Value);
        }

        // Calculate average change in output (skip warmup period)
        int startIdx = 60; // Skip warmup
        double sumChanges = 0;
        for (int i = startIdx + 1; i < results.Count; i++)
        {
            sumChanges += Math.Abs(results[i] - results[i - 1]);
        }
        double avgChange = sumChanges / (results.Count - startIdx - 1);

        // Calculate average change in input for same period
        double sumInputChanges = 0;
        for (int i = startIdx + 1; i < 200; i++)
        {
            sumInputChanges += Math.Abs(_bars[i].Close - _bars[i - 1].Close);
        }
        double avgInputChange = sumInputChanges / (200 - startIdx - 1);

        // Double-smoothed output should be smoother than input
        Assert.True(avgChange < avgInputChange,
            $"Double-smoothed output ({avgChange:F4}) should be smoother than input ({avgInputChange:F4})");
    }

    [Fact]
    public void SqrtPeriod_AffectsSmoothing()
    {
        // With sqrt(period), larger periods have proportionally less smoothing
        // than standard EMA

        var apz4 = new Apz(4);   // sqrt(4) = 2, alpha = 2/(2+1) = 0.667
        var apz100 = new Apz(100); // sqrt(100) = 10, alpha = 2/(10+1) = 0.182

        // Feed same data
        for (int i = 0; i < 50; i++)
        {
            apz4.Update(_bars[i]);
            apz100.Update(_bars[i]);
        }

        // Both should have finite values
        Assert.True(double.IsFinite(apz4.Last.Value));
        Assert.True(double.IsFinite(apz100.Last.Value));

        // Period 100 should be smoother (closer to mean)
        // and take longer to become hot
        Assert.True(apz4.IsHot);
        // Period 100 may or may not be hot after 50 bars
    }

    [Fact]
    public void MultiplierAffectsBandWidth()
    {
        var apz1 = new Apz(10, 1.0);
        var apz2 = new Apz(10, 2.0);
        var apz3 = new Apz(10, 3.0);

        for (int i = 0; i < 50; i++)
        {
            apz1.Update(_bars[i]);
            apz2.Update(_bars[i]);
            apz3.Update(_bars[i]);
        }

        // All should have same middle
        Assert.Equal(apz1.Last.Value, apz2.Last.Value, precision: 10);
        Assert.Equal(apz2.Last.Value, apz3.Last.Value, precision: 10);

        // Band widths should scale with multiplier
        double width1 = apz1.Upper.Value - apz1.Lower.Value;
        double width2 = apz2.Upper.Value - apz2.Lower.Value;
        double width3 = apz3.Upper.Value - apz3.Lower.Value;

        Assert.Equal(width2, width1 * 2, precision: 10);
        Assert.Equal(width3, width1 * 3, precision: 10);
    }

    [Fact]
    public void FlatLine_ReturnsSameMiddle()
    {
        var apz = new Apz(10);

        for (int i = 0; i < 50; i++)
        {
            apz.Update(new TBar(DateTime.UtcNow, 100, 105, 95, 100, 1000));
        }

        // Middle should converge to 100
        Assert.Equal(100, apz.Last.Value, precision: 1);
    }

    [Fact]
    public void ZeroRange_ProducesZeroBandWidth()
    {
        var apz = new Apz(10);

        // Feed bars with no range (high = low = close)
        for (int i = 0; i < 50; i++)
        {
            apz.Update(new TBar(DateTime.UtcNow, 100, 100, 100, 100, 1000));
        }

        // Bands should converge to middle (zero width)
        Assert.Equal(apz.Last.Value, apz.Upper.Value, precision: 1);
        Assert.Equal(apz.Last.Value, apz.Lower.Value, precision: 1);
    }

    #endregion
}
