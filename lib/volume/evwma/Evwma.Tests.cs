namespace QuanTAlib.Tests;

public class EvwmaTests
{
    private readonly GBM _feed;
    private readonly TBarSeries _bars;

    public EvwmaTests()
    {
        _feed = new GBM();
        _bars = new TBarSeries();
        for (int i = 0; i < 1000; i++)
        {
            _bars.Add(_feed.Next());
        }
    }

    // ============ Constructor Tests ============

    [Fact]
    public void Constructor_DefaultPeriod_ShouldBe20()
    {
        var evwma = new Evwma();
        Assert.Equal("EVWMA(20)", evwma.Name);
    }

    [Fact]
    public void Constructor_WithPeriod_ShouldSetName()
    {
        var evwma = new Evwma(14);
        Assert.Equal("EVWMA(14)", evwma.Name);
    }

    [Fact]
    public void Constructor_ZeroPeriod_ShouldThrow()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Evwma(0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_ShouldThrow()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Evwma(-1));
        Assert.Equal("period", ex.ParamName);
    }

    // ============ Basic Calculation Tests ============

    [Fact]
    public void Update_ReturnsValidTValue()
    {
        var evwma = new Evwma(10);
        var bar = _bars[0];
        var result = evwma.Update(bar);

        Assert.NotEqual(default, result);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_FirstBar_ShouldBeClosePrice()
    {
        var evwma = new Evwma(10);
        var bar = new TBar(DateTime.UtcNow, 10, 15, 8, 12, 1000);
        var result = evwma.Update(bar);

        // EVWMA of first bar = close price (only one data point)
        Assert.Equal(12.0, result.Value, 10);
    }

    [Fact]
    public void Update_MultipleBarsSamePrice_ShouldReturnSameEvwma()
    {
        var evwma = new Evwma(10);
        // All bars have same close price = 100
        var bar1 = new TBar(DateTime.UtcNow, 100, 100, 100, 100, 100);
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 100, 100, 100, 100, 200);
        var bar3 = new TBar(DateTime.UtcNow.AddMinutes(2), 100, 100, 100, 100, 300);

        evwma.Update(bar1);
        evwma.Update(bar2);
        var result = evwma.Update(bar3);

        Assert.Equal(100.0, result.Value, 10);
    }

    [Fact]
    public void Update_VolumeWeighting_Works()
    {
        var evwma = new Evwma(10);
        // Bar 1: price=10, volume=100 → result = 10 (first bar)
        // Bar 2: price=20, volume=300
        // sumVol = 100 + 300 = 400, remainVol = 400 - 300 = 100
        // result = (100 * 10 + 300 * 20) / 400 = (1000 + 6000) / 400 = 17.5
        var bar1 = new TBar(DateTime.UtcNow, 10, 10, 10, 10, 100);
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 20, 20, 20, 20, 300);

        evwma.Update(bar1);
        var result = evwma.Update(bar2);

        Assert.Equal(17.5, result.Value, 10);
    }

    [Fact]
    public void Update_SlidingWindow_ShouldDropOldVolume()
    {
        // Period=2: rolling volume window holds 2 bars
        var evwma = new Evwma(2);

        // Bar 1: price=100, vol=1000
        var bar1 = new TBar(DateTime.UtcNow, 100, 100, 100, 100, 1000);
        evwma.Update(bar1);
        Assert.Equal(100.0, evwma.Last.Value, 10); // First bar = price

        // Bar 2: price=200, vol=1000
        // sumVol = 1000 + 1000 = 2000, remainVol = 2000 - 1000 = 1000
        // result = (1000 * 100 + 1000 * 200) / 2000 = 150
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 200, 200, 200, 200, 1000);
        evwma.Update(bar2);
        Assert.Equal(150.0, evwma.Last.Value, 10);

        // Bar 3: price=300, vol=1000
        // Old bar1 vol drops out: sumVol = 1000(bar2) + 1000(bar3) = 2000
        // remainVol = 2000 - 1000 = 1000
        // result = (1000 * 150 + 1000 * 300) / 2000 = 225
        var bar3 = new TBar(DateTime.UtcNow.AddMinutes(2), 300, 300, 300, 300, 1000);
        var result = evwma.Update(bar3);

        Assert.Equal(225.0, result.Value, 10);
    }

    [Fact]
    public void IsHot_AfterPeriodBars_ShouldBeTrue()
    {
        var evwma = new Evwma(10);
        Assert.False(evwma.IsHot);

        for (int i = 0; i < 9; i++)
        {
            evwma.Update(_bars[i]);
            Assert.False(evwma.IsHot);
        }

        evwma.Update(_bars[9]);
        Assert.True(evwma.IsHot);
    }

    [Fact]
    public void WarmupPeriod_ShouldMatchPeriod()
    {
        var evwma = new Evwma(14);
        Assert.Equal(14, evwma.WarmupPeriod);
    }

    // ============ Bar Correction Tests (isNew) ============

    [Fact]
    public void Update_IsNewTrue_ShouldAdvanceState()
    {
        var evwma = new Evwma(10);
        var bar1 = new TBar(DateTime.UtcNow, 10, 10, 10, 10, 100);
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 20, 20, 20, 20, 100);

        evwma.Update(bar1, isNew: true);
        var result1 = evwma.Last.Value;

        evwma.Update(bar2, isNew: true);
        var result2 = evwma.Last.Value;

        Assert.NotEqual(result1, result2);
    }

    [Fact]
    public void Update_IsNewFalse_ShouldRollback()
    {
        var evwma = new Evwma(10);
        var bar1 = new TBar(DateTime.UtcNow, 10, 10, 10, 10, 100);
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 20, 20, 20, 20, 100);
        var bar2Updated = new TBar(DateTime.UtcNow.AddMinutes(1), 15, 15, 15, 15, 100);

        evwma.Update(bar1, isNew: true);
        evwma.Update(bar2, isNew: true);
        var afterBar2 = evwma.Last.Value;

        // Correct bar2 with updated values
        evwma.Update(bar2Updated, isNew: false);
        var afterCorrection = evwma.Last.Value;

        Assert.NotEqual(afterBar2, afterCorrection);
    }

    [Fact]
    public void Update_IterativeCorrections_ShouldRestoreState()
    {
        var evwma = new Evwma(10);

        // Process first 10 bars
        for (int i = 0; i < 10; i++)
        {
            evwma.Update(_bars[i], isNew: true);
        }
        _ = evwma.Last.Value;

        // Process bar 11
        evwma.Update(_bars[10], isNew: true);
        var valueAfter11 = evwma.Last.Value;

        // Correct bar 11 multiple times with same data
        for (int i = 0; i < 5; i++)
        {
            evwma.Update(_bars[10], isNew: false);
        }
        var valueAfterCorrections = evwma.Last.Value;

        // Should get same result as after first processing of bar 11
        Assert.Equal(valueAfter11, valueAfterCorrections, 10);
    }

    // ============ Reset Tests ============

    [Fact]
    public void Reset_ShouldClearState()
    {
        var evwma = new Evwma(10);

        for (int i = 0; i < 100; i++)
        {
            evwma.Update(_bars[i]);
        }
        Assert.True(evwma.IsHot);

        evwma.Reset();

        Assert.False(evwma.IsHot);
        Assert.Equal(default, evwma.Last);
    }

    // ============ NaN/Infinity Handling ============

    [Fact]
    public void Update_NaN_ShouldUseLastValidValue()
    {
        var evwma = new Evwma(10);

        // First bar establishes valid values
        var bar1 = new TBar(DateTime.UtcNow, 10, 15, 8, 12, 1000);
        evwma.Update(bar1);

        // Second bar with NaN should use last valid
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);
        var result = evwma.Update(bar2);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_Infinity_ShouldUseLastValidValue()
    {
        var evwma = new Evwma(10);

        var bar1 = new TBar(DateTime.UtcNow, 10, 15, 8, 12, 1000);
        evwma.Update(bar1);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);
        var result = evwma.Update(bar2);

        Assert.True(double.IsFinite(result.Value));
    }

    // ============ TValue Input Tests ============

    [Fact]
    public void Update_TValue_ShouldWork()
    {
        var evwma = new Evwma(10);
        var input = new TValue(DateTime.UtcNow, 100.0);
        var result = evwma.Update(input);

        // With TValue, it uses value as price and volume=1
        Assert.Equal(100.0, result.Value, 10);
    }

    [Fact]
    public void Update_TValue_MultipleInputs()
    {
        var evwma = new Evwma(10);

        // TValue input assumes volume=1 for all
        // Bar 1: price=100, vol=1 → result = 100
        // Bar 2: price=200, vol=1
        // sumVol = 1 + 1 = 2, remainVol = 2 - 1 = 1
        // result = (1 * 100 + 1 * 200) / 2 = 150
        evwma.Update(new TValue(DateTime.UtcNow, 100.0));
        var result = evwma.Update(new TValue(DateTime.UtcNow.AddMinutes(1), 200.0));

        Assert.Equal(150.0, result.Value, 10);
    }

    // ============ Batch/Series Tests ============

    [Fact]
    public void Update_TBarSeries_ShouldReturnTSeries()
    {
        var evwma = new Evwma(10);
        var result = evwma.Update(_bars);

        Assert.NotNull(result);
        Assert.Equal(_bars.Count, result.Count);
    }

    [Fact]
    public void Calculate_Static_ShouldReturnTSeries()
    {
        var result = Evwma.Batch(_bars, 10);

        Assert.NotNull(result);
        Assert.Equal(_bars.Count, result.Count);
    }

    [Fact]
    public void Calculate_Static_WithDifferentPeriods_ShouldWork()
    {
        var result14 = Evwma.Batch(_bars, 14);
        var result50 = Evwma.Batch(_bars, 50);

        Assert.NotNull(result14);
        Assert.NotNull(result50);
        Assert.Equal(_bars.Count, result14.Count);
        Assert.Equal(_bars.Count, result50.Count);
    }

    // ============ Span API Tests ============

    [Fact]
    public void Calculate_Span_ShouldMatchBatch()
    {
        var batchResult = Evwma.Batch(_bars, 20);

        var price = _bars.Close.Values.ToArray();
        var volume = _bars.Volume.Values.ToArray();
        var spanOutput = new double[_bars.Count];

        Evwma.Batch(price, volume, spanOutput, 20);

        for (int i = 0; i < _bars.Count; i++)
        {
            Assert.Equal(batchResult.Values[i], spanOutput[i], 12);
        }
    }

    [Fact]
    public void Calculate_Span_MismatchedLengths_ShouldThrow()
    {
        var price = new double[100];
        var volume = new double[99]; // Mismatched
        var output = new double[100];

        Assert.Throws<ArgumentException>(() => Evwma.Batch(price, volume, output, 10));
    }

    [Fact]
    public void Calculate_Span_OutputLengthMismatch_ShouldThrow()
    {
        var price = new double[100];
        var volume = new double[100];
        var output = new double[50]; // Mismatched

        Assert.Throws<ArgumentException>(() => Evwma.Batch(price, volume, output, 10));
    }

    [Fact]
    public void Calculate_Span_ZeroPeriod_ShouldThrow()
    {
        var price = new double[100];
        var volume = new double[100];
        var output = new double[100];

        Assert.Throws<ArgumentException>(() => Evwma.Batch(price, volume, output, 0));
    }

    [Fact]
    public void Calculate_Span_NegativePeriod_ShouldThrow()
    {
        var price = new double[100];
        var volume = new double[100];
        var output = new double[100];

        Assert.Throws<ArgumentException>(() => Evwma.Batch(price, volume, output, -1));
    }

    // ============ Event Tests ============

    [Fact]
    public void Pub_ShouldFireOnUpdate()
    {
        var evwma = new Evwma(10);
        int eventCount = 0;

        evwma.Pub += (object? sender, in TValueEventArgs args) => eventCount++;

        evwma.Update(_bars[0]);
        evwma.Update(_bars[1]);

        Assert.Equal(2, eventCount);
    }

    // ============ Streaming/Batch Consistency ============

    [Fact]
    public void Streaming_ShouldMatchBatch()
    {
        // Streaming
        var evwma = new Evwma(20);
        var streamingResults = new List<double>();
        foreach (var bar in _bars)
        {
            streamingResults.Add(evwma.Update(bar).Value);
        }

        // Batch
        var batchResult = Evwma.Batch(_bars, 20);

        // Compare last 100 values
        for (int i = _bars.Count - 100; i < _bars.Count; i++)
        {
            Assert.Equal(batchResult.Values[i], streamingResults[i], 10);
        }
    }

    // ============ TSeries Calculate Tests ============

    [Fact]
    public void Calculate_TSeries_ShouldWork()
    {
        var sourceSeries = _bars.Close;
        var result = Evwma.Batch(sourceSeries, 20);

        Assert.NotNull(result);
        Assert.Equal(sourceSeries.Count, result.Count);
    }

    [Fact]
    public void Calculate_TSeries_ShouldMatchTValueStreaming()
    {
        var sourceSeries = _bars.Close;
        var batchResult = Evwma.Batch(sourceSeries, 20);

        // Streaming with TValue
        var evwma = new Evwma(20);
        var streamingResults = new List<double>();
        for (int i = 0; i < sourceSeries.Count; i++)
        {
            streamingResults.Add(evwma.Update(sourceSeries[i]).Value);
        }

        // Compare last 100 values
        for (int i = sourceSeries.Count - 100; i < sourceSeries.Count; i++)
        {
            Assert.Equal(batchResult.Values[i], streamingResults[i], 10);
        }
    }

    // ============ EVWMA-Specific Volume Behavior Tests ============

    [Fact]
    public void Update_HighVolumeBar_ShouldShiftMoreThanLowVolume()
    {
        // Two EVWMA instances, same initial state
        var evwma1 = new Evwma(10);
        var evwma2 = new Evwma(10);

        // Same warmup bars
        for (int i = 0; i < 10; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 100, 100, 100, 100, 1000);
            evwma1.Update(bar);
            evwma2.Update(bar);
        }

        // evwma1: new bar at price 200 with HIGH volume
        var highVolBar = new TBar(DateTime.UtcNow.AddMinutes(10), 200, 200, 200, 200, 10000);
        evwma1.Update(highVolBar);

        // evwma2: same price but LOW volume
        var lowVolBar = new TBar(DateTime.UtcNow.AddMinutes(10), 200, 200, 200, 200, 10);
        evwma2.Update(lowVolBar);

        // High volume bar should shift the average more toward 200
        Assert.True(evwma1.Last.Value > evwma2.Last.Value,
            $"High volume EVWMA ({evwma1.Last.Value}) should be closer to 200 than low volume EVWMA ({evwma2.Last.Value})");
    }

    [Fact]
    public void Update_ZeroVolume_ShouldNotChangeResult()
    {
        var evwma = new Evwma(10);

        var bar1 = new TBar(DateTime.UtcNow, 100, 100, 100, 100, 1000);
        evwma.Update(bar1);
        var afterFirst = evwma.Last.Value;

        // Zero volume bar should not affect the average
        var zeroVolBar = new TBar(DateTime.UtcNow.AddMinutes(1), 200, 200, 200, 200, 0);
        evwma.Update(zeroVolBar);

        // With zero volume, the denominator changes but curVol=0 means no new price impact
        // result = ((sumVol - 0) * prevResult + 0 * curPrice) / sumVol = prevResult
        Assert.Equal(afterFirst, evwma.Last.Value, 10);
    }
}
