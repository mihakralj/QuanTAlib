namespace QuanTAlib.Tests;

public class RwmaTests
{
    private readonly GBM _feed;
    private readonly TBarSeries _bars;

    public RwmaTests()
    {
        _feed = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        _bars = new TBarSeries();
        for (int i = 0; i < 1000; i++)
        {
            _bars.Add(_feed.Next());
        }
    }

    // ============ A) Constructor Validation ============

    [Fact]
    public void Constructor_DefaultPeriod_ShouldBe14()
    {
        var rwma = new Rwma();
        Assert.Equal("Rwma(14)", rwma.Name);
    }

    [Fact]
    public void Constructor_WithPeriod_ShouldSetName()
    {
        var rwma = new Rwma(10);
        Assert.Equal("Rwma(10)", rwma.Name);
    }

    [Fact]
    public void Constructor_ZeroPeriod_ShouldThrow()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Rwma(0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_ShouldThrow()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Rwma(-1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_Period1_ShouldNotThrow()
    {
        var rwma = new Rwma(1);
        Assert.Equal("Rwma(1)", rwma.Name);
    }

    // ============ B) Basic Calculation ============

    [Fact]
    public void Update_ReturnsValidTValue()
    {
        var rwma = new Rwma(10);
        var bar = _bars[0];
        var result = rwma.Update(bar);

        Assert.NotEqual(default, result);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_FirstBar_ShouldBeClosePrice()
    {
        var rwma = new Rwma(10);
        var bar = new TBar(DateTime.UtcNow, 10, 15, 8, 12, 1000);
        var result = rwma.Update(bar);

        // RWMA of first bar: range=15-8=7, sumCR=12*7=84, sumR=7, RWMA=84/7=12
        Assert.Equal(12.0, result.Value, 10);
    }

    [Fact]
    public void Update_MultipleBarsSamePrice_ShouldReturnSameRwma()
    {
        var rwma = new Rwma(10);
        // All bars have same close and same range
        var bar1 = new TBar(DateTime.UtcNow, 95, 105, 95, 100, 100);
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 95, 105, 95, 100, 200);
        var bar3 = new TBar(DateTime.UtcNow.AddMinutes(2), 95, 105, 95, 100, 300);

        rwma.Update(bar1);
        rwma.Update(bar2);
        var result = rwma.Update(bar3);

        // All closes = 100, all ranges = 10, so RWMA = (100*10 + 100*10 + 100*10) / (10+10+10) = 100
        Assert.Equal(100.0, result.Value, 10);
    }

    [Fact]
    public void Update_RangeWeighting_Works()
    {
        var rwma = new Rwma(10);
        // Bar 1: close=10, range=2   (high=11, low=9)
        // Bar 2: close=20, range=6   (high=23, low=17)
        // RWMA = (10*2 + 20*6) / (2+6) = (20 + 120) / 8 = 17.5
        var bar1 = new TBar(DateTime.UtcNow, 10, 11, 9, 10, 100);
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 20, 23, 17, 20, 100);

        rwma.Update(bar1);
        var result = rwma.Update(bar2);

        Assert.Equal(17.5, result.Value, 10);
    }

    [Fact]
    public void Update_HighRangeBar_HasMoreInfluence()
    {
        var rwma = new Rwma(10);

        // Bar 1: close=10, high range (range=20)
        var bar1 = new TBar(DateTime.UtcNow, 10, 20, 0, 10, 100);
        // Bar 2: close=20, low range (range=2)
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 20, 21, 19, 20, 100);

        rwma.Update(bar1);
        var result = rwma.Update(bar2);

        // RWMA = (10*20 + 20*2) / (20+2) = (200+40)/22 = 10.909...
        double expected = ((10.0 * 20.0) + (20.0 * 2.0)) / (20.0 + 2.0);
        Assert.Equal(expected, result.Value, 10);

        // Should be closer to 10 (the high-range bar) than 20
        Assert.True(result.Value < 15, "RWMA should be weighted toward high-range bar's close");
    }

    [Fact]
    public void Update_SlidingWindow_ShouldDropOldValues()
    {
        var rwma = new Rwma(2);
        // Period = 2, so only last 2 bars count

        // Bar 1: close=10, range=4  (h=12, l=8)
        var bar1 = new TBar(DateTime.UtcNow, 10, 12, 8, 10, 100);
        rwma.Update(bar1);

        // Bar 2: close=20, range=4  (h=22, l=18)
        // RWMA = (10*4 + 20*4) / (4+4) = 120/8 = 15
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 20, 22, 18, 20, 100);
        rwma.Update(bar2);
        Assert.Equal(15.0, rwma.Last.Value, 10);

        // Bar 3: close=30, range=4  (h=32, l=28)
        // Now bar1 drops out: RWMA = (20*4 + 30*4) / (4+4) = 200/8 = 25
        var bar3 = new TBar(DateTime.UtcNow.AddMinutes(2), 30, 32, 28, 30, 100);
        var result = rwma.Update(bar3);

        Assert.Equal(25.0, result.Value, 10);
    }

    [Fact]
    public void Update_ZeroRange_DegeneratesToClose()
    {
        var rwma = new Rwma(10);
        // All bars have zero range (high == low == close)
        var bar1 = new TBar(DateTime.UtcNow, 50, 50, 50, 50, 100);
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 60, 60, 60, 60, 100);
        var bar3 = new TBar(DateTime.UtcNow.AddMinutes(2), 70, 70, 70, 70, 100);

        rwma.Update(bar1);
        rwma.Update(bar2);
        var result = rwma.Update(bar3);

        // All ranges = 0, so RWMA degenerates to current close = 70
        Assert.Equal(70.0, result.Value, 10);
    }

    // ============ C) State + Bar Correction (isNew) ============

    [Fact]
    public void IsHot_AfterPeriodBars_ShouldBeTrue()
    {
        var rwma = new Rwma(10);
        Assert.False(rwma.IsHot);

        for (int i = 0; i < 9; i++)
        {
            rwma.Update(_bars[i]);
            Assert.False(rwma.IsHot);
        }

        rwma.Update(_bars[9]);
        Assert.True(rwma.IsHot);
    }

    [Fact]
    public void WarmupPeriod_ShouldMatchPeriod()
    {
        var rwma = new Rwma(14);
        Assert.Equal(14, rwma.WarmupPeriod);
    }

    [Fact]
    public void Update_IsNewTrue_ShouldAdvanceState()
    {
        var rwma = new Rwma(10);
        var bar1 = new TBar(DateTime.UtcNow, 10, 15, 5, 10, 100);
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 20, 25, 15, 20, 100);

        rwma.Update(bar1, isNew: true);
        var result1 = rwma.Last.Value;

        rwma.Update(bar2, isNew: true);
        var result2 = rwma.Last.Value;

        Assert.NotEqual(result1, result2);
    }

    [Fact]
    public void Update_IsNewFalse_ShouldRollback()
    {
        var rwma = new Rwma(10);
        var bar1 = new TBar(DateTime.UtcNow, 10, 15, 5, 10, 100);
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 20, 25, 15, 20, 100);
        var bar2Updated = new TBar(DateTime.UtcNow.AddMinutes(1), 15, 18, 12, 15, 100);

        rwma.Update(bar1, isNew: true);
        rwma.Update(bar2, isNew: true);
        var afterBar2 = rwma.Last.Value;

        // Correct bar2 with updated values
        rwma.Update(bar2Updated, isNew: false);
        var afterCorrection = rwma.Last.Value;

        Assert.NotEqual(afterBar2, afterCorrection);
    }

    [Fact]
    public void Update_IterativeCorrections_ShouldRestoreState()
    {
        var rwma = new Rwma(10);

        // Process first 10 bars
        for (int i = 0; i < 10; i++)
        {
            rwma.Update(_bars[i], isNew: true);
        }
        _ = rwma.Last.Value;

        // Process bar 11
        rwma.Update(_bars[10], isNew: true);
        var valueAfter11 = rwma.Last.Value;

        // Correct bar 11 multiple times with same data
        for (int i = 0; i < 5; i++)
        {
            rwma.Update(_bars[10], isNew: false);
        }
        var valueAfterCorrections = rwma.Last.Value;

        // Should get same result as after first processing of bar 11
        Assert.Equal(valueAfter11, valueAfterCorrections, 10);
    }

    [Fact]
    public void Reset_ShouldClearState()
    {
        var rwma = new Rwma(10);

        for (int i = 0; i < 100; i++)
        {
            rwma.Update(_bars[i]);
        }
        Assert.True(rwma.IsHot);

        rwma.Reset();

        Assert.False(rwma.IsHot);
        Assert.Equal(default, rwma.Last);
    }

    // ============ D) Warmup/Convergence ============

    [Fact]
    public void IsHot_FlipsExactlyAtPeriod()
    {
        var rwma = new Rwma(5);

        for (int i = 0; i < 4; i++)
        {
            rwma.Update(_bars[i]);
            Assert.False(rwma.IsHot, $"IsHot should be false at bar {i}");
        }

        rwma.Update(_bars[4]);
        Assert.True(rwma.IsHot, "IsHot should be true at bar 4 (5th bar)");
    }

    [Fact]
    public void WarmupPeriod_DependsOnPeriod()
    {
        Assert.Equal(5, new Rwma(5).WarmupPeriod);
        Assert.Equal(20, new Rwma(20).WarmupPeriod);
        Assert.Equal(100, new Rwma(100).WarmupPeriod);
    }

    // ============ E) Robustness (NaN/Infinity) ============

    [Fact]
    public void Update_NaN_ShouldUseLastValidValue()
    {
        var rwma = new Rwma(10);

        // First bar establishes valid values
        var bar1 = new TBar(DateTime.UtcNow, 10, 15, 8, 12, 1000);
        rwma.Update(bar1);

        // Second bar with NaN should use last valid
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);
        var result = rwma.Update(bar2);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_Infinity_ShouldUseLastValidValue()
    {
        var rwma = new Rwma(10);

        var bar1 = new TBar(DateTime.UtcNow, 10, 15, 8, 12, 1000);
        rwma.Update(bar1);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);
        var result = rwma.Update(bar2);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_BatchNaN_ShouldRemainFinite()
    {
        var rwma = new Rwma(10);

        // Establish valid state
        for (int i = 0; i < 20; i++)
        {
            rwma.Update(_bars[i]);
        }

        // Send multiple NaN bars
        for (int i = 0; i < 5; i++)
        {
            var nanBar = new TBar(DateTime.UtcNow.AddMinutes(20 + i),
                double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);
            var result = rwma.Update(nanBar);
            Assert.True(double.IsFinite(result.Value), $"NaN bar {i} produced non-finite result");
        }
    }

    // ============ F) Consistency (4 API modes) ============

    [Fact]
    public void Streaming_ShouldMatchBatch()
    {
        int period = 14;

        // Streaming
        var rwma = new Rwma(period);
        var streamingResults = new List<double>();
        foreach (var bar in _bars)
        {
            streamingResults.Add(rwma.Update(bar).Value);
        }

        // Batch
        var batchResult = Rwma.Batch(_bars, period);

        // Compare last 100 values
        for (int i = _bars.Count - 100; i < _bars.Count; i++)
        {
            Assert.Equal(batchResult.Values[i], streamingResults[i], 10);
        }
    }

    [Fact]
    public void Batch_TBarSeries_ShouldMatchSpan()
    {
        int period = 14;

        var batchResult = Rwma.Batch(_bars, period);

        var close = _bars.Close.Values.ToArray();
        var high = _bars.High.Values.ToArray();
        var low = _bars.Low.Values.ToArray();
        var spanOutput = new double[_bars.Count];
        Rwma.Batch(close, high, low, spanOutput, period);

        for (int i = 0; i < _bars.Count; i++)
        {
            Assert.Equal(batchResult.Values[i], spanOutput[i], 12);
        }
    }

    [Fact]
    public void Eventing_ShouldMatchStreaming()
    {
        int period = 14;

        // Streaming
        var rwma1 = new Rwma(period);
        var streamingResults = new List<double>();
        foreach (var bar in _bars)
        {
            streamingResults.Add(rwma1.Update(bar).Value);
        }

        // Event-based
        var rwma2 = new Rwma(period);
        var eventResults = new List<double>();
        rwma2.Pub += (object? sender, in TValueEventArgs args) => eventResults.Add(args.Value.Value);
        foreach (var bar in _bars)
        {
            rwma2.Update(bar);
        }

        Assert.Equal(streamingResults.Count, eventResults.Count);
        for (int i = 0; i < streamingResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], eventResults[i], 12);
        }
    }

    // ============ G) Span API Tests ============

    [Fact]
    public void Batch_Span_MismatchedLengths_ShouldThrow()
    {
        var close = new double[100];
        var high = new double[99]; // Mismatched
        var low = new double[100];
        var output = new double[100];

        var ex = Assert.Throws<ArgumentException>(() => Rwma.Batch(close, high, low, output, 10));
        Assert.Equal("high", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_OutputLengthMismatch_ShouldThrow()
    {
        var close = new double[100];
        var high = new double[100];
        var low = new double[100];
        var output = new double[50]; // Mismatched

        var ex = Assert.Throws<ArgumentException>(() => Rwma.Batch(close, high, low, output, 10));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_ZeroPeriod_ShouldThrow()
    {
        var close = new double[100];
        var high = new double[100];
        var low = new double[100];
        var output = new double[100];

        var ex = Assert.Throws<ArgumentException>(() => Rwma.Batch(close, high, low, output, 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_NegativePeriod_ShouldThrow()
    {
        var close = new double[100];
        var high = new double[100];
        var low = new double[100];
        var output = new double[100];

        var ex = Assert.Throws<ArgumentException>(() => Rwma.Batch(close, high, low, output, -1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_NaN_ShouldNotPropagate()
    {
        var close = new double[] { 10, 20, double.NaN, 40, 50 };
        var high = new double[] { 15, 25, double.NaN, 45, 55 };
        var low = new double[] { 5, 15, double.NaN, 35, 45 };
        var output = new double[5];

        Rwma.Batch(close, high, low, output, 3);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"Output at index {i} is not finite: {output[i]}");
        }
    }

    [Fact]
    public void Batch_Span_LargeData_ShouldNotOverflow()
    {
        // Test with period > StackallocThreshold (256)
        int period = 300;
        int len = 500;
        var close = new double[len];
        var high = new double[len];
        var low = new double[len];
        var output = new double[len];

        for (int i = 0; i < len; i++)
        {
            close[i] = 100 + i;
            high[i] = 100 + i + 5;
            low[i] = 100 + i - 5;
        }

        Rwma.Batch(close, high, low, output, period);

        for (int i = 0; i < len; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"Output at index {i} is not finite");
        }
    }

    // ============ H) Chainability / Events ============

    [Fact]
    public void Pub_ShouldFireOnUpdate()
    {
        var rwma = new Rwma(10);
        int eventCount = 0;

        rwma.Pub += (object? sender, in TValueEventArgs args) => eventCount++;

        rwma.Update(_bars[0]);
        rwma.Update(_bars[1]);

        Assert.Equal(2, eventCount);
    }

    [Fact]
    public void Pub_EventArgs_ShouldContainCorrectValue()
    {
        var rwma = new Rwma(10);
        TValue? lastEventValue = null;

        rwma.Pub += (object? sender, in TValueEventArgs args) => lastEventValue = args.Value;

        var result = rwma.Update(_bars[0]);
        Assert.NotNull(lastEventValue);
        Assert.Equal(result.Value, lastEventValue.Value.Value, 12);
    }

    // ============ TValue Input Tests ============

    [Fact]
    public void Update_TValue_ShouldWork()
    {
        var rwma = new Rwma(10);
        var input = new TValue(DateTime.UtcNow, 100.0);
        var result = rwma.Update(input);

        // With TValue, high=low=close → range=0, degenerates to close
        Assert.Equal(100.0, result.Value, 10);
    }

    [Fact]
    public void Update_TValue_MultipleInputs_DegeneratesToClose()
    {
        var rwma = new Rwma(10);

        // TValue input: range always 0, so always degenerates to current close
        rwma.Update(new TValue(DateTime.UtcNow, 100.0));
        var result = rwma.Update(new TValue(DateTime.UtcNow.AddMinutes(1), 200.0));

        // All ranges 0 → fallback to current close = 200
        Assert.Equal(200.0, result.Value, 10);
    }

    // ============ Batch/Series Tests ============

    [Fact]
    public void Update_TBarSeries_ShouldReturnTSeries()
    {
        var rwma = new Rwma(10);
        var result = rwma.Update(_bars);

        Assert.NotNull(result);
        Assert.Equal(_bars.Count, result.Count);
    }

    [Fact]
    public void Batch_Static_ShouldReturnTSeries()
    {
        var result = Rwma.Batch(_bars, 10);

        Assert.NotNull(result);
        Assert.Equal(_bars.Count, result.Count);
    }

    [Fact]
    public void Batch_Static_WithDifferentPeriods_ShouldWork()
    {
        var result14 = Rwma.Batch(_bars, 14);
        var result50 = Rwma.Batch(_bars, 50);

        Assert.NotNull(result14);
        Assert.NotNull(result50);
        Assert.Equal(_bars.Count, result14.Count);
        Assert.Equal(_bars.Count, result50.Count);
    }

    // ============ TSeries Calculate Tests ============

    [Fact]
    public void Calculate_Static_ShouldReturnTSeriesAndIndicator()
    {
        var (results, indicator) = Rwma.Calculate(_bars, 14);

        Assert.NotNull(results);
        Assert.Equal(_bars.Count, results.Count);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void Batch_TSeries_ShouldWork()
    {
        var sourceSeries = _bars.Close;
        var result = Rwma.Batch(sourceSeries, 20);

        Assert.NotNull(result);
        Assert.Equal(sourceSeries.Count, result.Count);
    }

    // ============ Prime Tests ============

    [Fact]
    public void Prime_ShouldInitializeState()
    {
        var rwma = new Rwma(10);
        rwma.Prime(_bars);

        Assert.True(rwma.IsHot);
        Assert.True(double.IsFinite(rwma.Last.Value));
    }

    [Fact]
    public void Prime_ThenUpdate_ShouldContinueCorrectly()
    {
        var rwma1 = new Rwma(10);
        var rwma2 = new Rwma(10);

        // rwma1: process all bars
        for (int i = 0; i < 100; i++)
        {
            rwma1.Update(_bars[i]);
        }

        // rwma2: prime with first 50, then stream remaining
        var primeBars = new TBarSeries();
        for (int i = 0; i < 50; i++)
        {
            primeBars.Add(_bars[i]);
        }
        rwma2.Prime(primeBars);
        for (int i = 50; i < 100; i++)
        {
            rwma2.Update(_bars[i]);
        }

        // Both should produce the same result
        Assert.Equal(rwma1.Last.Value, rwma2.Last.Value, 10);
    }

    // ============ Algorithm-Specific Tests ============

    [Fact]
    public void RangeWeighting_VolatileBarHasMoreWeight()
    {
        var rwma = new Rwma(10);

        // Bar with large range (volatile) at close=50
        var volatileBar = new TBar(DateTime.UtcNow, 50, 70, 30, 50, 100); // range=40
        // Bar with small range (quiet) at close=100
        var quietBar = new TBar(DateTime.UtcNow.AddMinutes(1), 100, 101, 99, 100, 100); // range=2

        rwma.Update(volatileBar);
        var result = rwma.Update(quietBar);

        // RWMA = (50*40 + 100*2) / (40+2) = (2000+200)/42 = 52.38...
        double expected = ((50.0 * 40.0) + (100.0 * 2.0)) / 42.0;
        Assert.Equal(expected, result.Value, 10);

        // Should be much closer to 50 than 100
        Assert.True(result.Value < 60, "RWMA should strongly lean toward the volatile bar's close");
    }

    [Fact]
    public void StablePrice_ConstantRange_ShouldReturnSma()
    {
        var rwma = new Rwma(5);

        // When all bars have the same range, RWMA reduces to SMA of closes
        // because weights are all equal
        var now = DateTime.UtcNow;
        double[] closes = { 10, 20, 30, 40, 50 };

        for (int i = 0; i < 5; i++)
        {
            // All bars have range = 10
            var bar = new TBar(now.AddMinutes(i), closes[i], closes[i] + 5, closes[i] - 5, closes[i], 100);
            rwma.Update(bar);
        }

        // When all ranges equal, RWMA = SMA = (10+20+30+40+50)/5 = 30
        Assert.Equal(30.0, rwma.Last.Value, 10);
    }

    [Fact]
    public void Period1_ShouldReturnClose()
    {
        var rwma = new Rwma(1);

        var bar = new TBar(DateTime.UtcNow, 50, 60, 40, 55, 100);
        var result = rwma.Update(bar);

        // Period 1: only current bar, RWMA = close * range / range = close
        Assert.Equal(55.0, result.Value, 10);
    }

    [Fact]
    public void ConvexCombination_NeverExceedsPriceRange()
    {
        var rwma = new Rwma(20);
        var results = new List<double>();

        for (int i = 0; i < 100; i++)
        {
            results.Add(rwma.Update(_bars[i]).Value);
        }

        // Find min/max close in last 20 bars for the last few results
        for (int i = 80; i < 100; i++)
        {
            double minClose = double.MaxValue;
            double maxClose = double.MinValue;
            for (int j = i - 19; j <= i; j++)
            {
                double c = _bars[j].Close;
                if (c < minClose)
                {
                    minClose = c;
                }
                if (c > maxClose)
                {
                    maxClose = c;
                }
            }

            // RWMA is a convex combination — should be within [minClose, maxClose]
            Assert.True(results[i] >= minClose - 1e-9 && results[i] <= maxClose + 1e-9,
                $"RWMA at {i} ({results[i]}) should be within [{minClose}, {maxClose}]");
        }
    }
}
