namespace QuanTAlib.Tests;

public class VwmaTests
{
    private readonly GBM _feed;
    private readonly TBarSeries _bars;

    public VwmaTests()
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
        var vwma = new Vwma();
        Assert.Equal("VWMA(20)", vwma.Name);
    }

    [Fact]
    public void Constructor_WithPeriod_ShouldSetName()
    {
        var vwma = new Vwma(14);
        Assert.Equal("VWMA(14)", vwma.Name);
    }

    [Fact]
    public void Constructor_ZeroPeriod_ShouldThrow()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Vwma(0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_ShouldThrow()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Vwma(-1));
        Assert.Equal("period", ex.ParamName);
    }

    // ============ Basic Calculation Tests ============

    [Fact]
    public void Update_ReturnsValidTValue()
    {
        var vwma = new Vwma(10);
        var bar = _bars[0];
        var result = vwma.Update(bar);

        Assert.NotEqual(default, result);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_FirstBar_ShouldBeClosePrice()
    {
        var vwma = new Vwma(10);
        var bar = new TBar(DateTime.UtcNow, 10, 15, 8, 12, 1000);
        var result = vwma.Update(bar);

        // VWMA of first bar = close price (only one data point)
        Assert.Equal(12.0, result.Value, 10);
    }

    [Fact]
    public void Update_MultipleBarsSamePrice_ShouldReturnSameVwma()
    {
        var vwma = new Vwma(10);
        // All bars have same close price = 100
        var bar1 = new TBar(DateTime.UtcNow, 100, 100, 100, 100, 100);
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 100, 100, 100, 100, 200);
        var bar3 = new TBar(DateTime.UtcNow.AddMinutes(2), 100, 100, 100, 100, 300);

        vwma.Update(bar1);
        vwma.Update(bar2);
        var result = vwma.Update(bar3);

        Assert.Equal(100.0, result.Value, 10);
    }

    [Fact]
    public void Update_VolumeWeighting_Works()
    {
        var vwma = new Vwma(10);
        // Bar 1: price=10, volume=100
        // Bar 2: price=20, volume=300
        // VWMA = (10*100 + 20*300) / (100+300) = (1000 + 6000) / 400 = 17.5
        var bar1 = new TBar(DateTime.UtcNow, 10, 10, 10, 10, 100);
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 20, 20, 20, 20, 300);

        vwma.Update(bar1);
        var result = vwma.Update(bar2);

        Assert.Equal(17.5, result.Value, 10);
    }

    [Fact]
    public void Update_SlidingWindow_ShouldDropOldValues()
    {
        var vwma = new Vwma(2);
        // Period = 2, so only last 2 bars count

        // Bar 1: price=10, volume=100
        var bar1 = new TBar(DateTime.UtcNow, 10, 10, 10, 10, 100);
        vwma.Update(bar1);

        // Bar 2: price=20, volume=100
        // VWMA = (10*100 + 20*100) / 200 = 15
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 20, 20, 20, 20, 100);
        vwma.Update(bar2);
        Assert.Equal(15.0, vwma.Last.Value, 10);

        // Bar 3: price=30, volume=100
        // Now bar1 drops out: VWMA = (20*100 + 30*100) / 200 = 25
        var bar3 = new TBar(DateTime.UtcNow.AddMinutes(2), 30, 30, 30, 30, 100);
        var result = vwma.Update(bar3);

        Assert.Equal(25.0, result.Value, 10);
    }

    [Fact]
    public void IsHot_AfterPeriodBars_ShouldBeTrue()
    {
        var vwma = new Vwma(10);
        Assert.False(vwma.IsHot);

        for (int i = 0; i < 9; i++)
        {
            vwma.Update(_bars[i]);
            Assert.False(vwma.IsHot);
        }

        vwma.Update(_bars[9]);
        Assert.True(vwma.IsHot);
    }

    [Fact]
    public void WarmupPeriod_ShouldMatchPeriod()
    {
        var vwma = new Vwma(14);
        Assert.Equal(14, vwma.WarmupPeriod);
    }

    // ============ Bar Correction Tests (isNew) ============

    [Fact]
    public void Update_IsNewTrue_ShouldAdvanceState()
    {
        var vwma = new Vwma(10);
        var bar1 = new TBar(DateTime.UtcNow, 10, 10, 10, 10, 100);
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 20, 20, 20, 20, 100);

        vwma.Update(bar1, isNew: true);
        var result1 = vwma.Last.Value;

        vwma.Update(bar2, isNew: true);
        var result2 = vwma.Last.Value;

        Assert.NotEqual(result1, result2);
    }

    [Fact]
    public void Update_IsNewFalse_ShouldRollback()
    {
        var vwma = new Vwma(10);
        var bar1 = new TBar(DateTime.UtcNow, 10, 10, 10, 10, 100);
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 20, 20, 20, 20, 100);
        var bar2Updated = new TBar(DateTime.UtcNow.AddMinutes(1), 15, 15, 15, 15, 100);

        vwma.Update(bar1, isNew: true);
        vwma.Update(bar2, isNew: true);
        var afterBar2 = vwma.Last.Value;

        // Correct bar2 with updated values
        vwma.Update(bar2Updated, isNew: false);
        var afterCorrection = vwma.Last.Value;

        Assert.NotEqual(afterBar2, afterCorrection);
    }

    [Fact]
    public void Update_IterativeCorrections_ShouldRestoreState()
    {
        var vwma = new Vwma(10);

        // Process first 10 bars
        for (int i = 0; i < 10; i++)
        {
            vwma.Update(_bars[i], isNew: true);
        }
        _ = vwma.Last.Value;

        // Process bar 11
        vwma.Update(_bars[10], isNew: true);
        var valueAfter11 = vwma.Last.Value;

        // Correct bar 11 multiple times with same data
        for (int i = 0; i < 5; i++)
        {
            vwma.Update(_bars[10], isNew: false);
        }
        var valueAfterCorrections = vwma.Last.Value;

        // Should get same result as after first processing of bar 11
        Assert.Equal(valueAfter11, valueAfterCorrections, 10);
    }

    // ============ Reset Tests ============

    [Fact]
    public void Reset_ShouldClearState()
    {
        var vwma = new Vwma(10);

        for (int i = 0; i < 100; i++)
        {
            vwma.Update(_bars[i]);
        }
        Assert.True(vwma.IsHot);

        vwma.Reset();

        Assert.False(vwma.IsHot);
        Assert.Equal(default, vwma.Last);
    }

    // ============ NaN/Infinity Handling ============

    [Fact]
    public void Update_NaN_ShouldUseLastValidValue()
    {
        var vwma = new Vwma(10);

        // First bar establishes valid values
        var bar1 = new TBar(DateTime.UtcNow, 10, 15, 8, 12, 1000);
        vwma.Update(bar1);

        // Second bar with NaN should use last valid
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);
        var result = vwma.Update(bar2);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_Infinity_ShouldUseLastValidValue()
    {
        var vwma = new Vwma(10);

        var bar1 = new TBar(DateTime.UtcNow, 10, 15, 8, 12, 1000);
        vwma.Update(bar1);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);
        var result = vwma.Update(bar2);

        Assert.True(double.IsFinite(result.Value));
    }

    // ============ TValue Input Tests ============

    [Fact]
    public void Update_TValue_ShouldWork()
    {
        var vwma = new Vwma(10);
        var input = new TValue(DateTime.UtcNow, 100.0);
        var result = vwma.Update(input);

        // With TValue, it uses value as price and volume=1
        Assert.Equal(100.0, result.Value, 10);
    }

    [Fact]
    public void Update_TValue_MultipleInputs()
    {
        var vwma = new Vwma(10);

        // TValue input assumes volume=1 for all
        // VWMA = (100*1 + 200*1) / 2 = 150
        vwma.Update(new TValue(DateTime.UtcNow, 100.0));
        var result = vwma.Update(new TValue(DateTime.UtcNow.AddMinutes(1), 200.0));

        Assert.Equal(150.0, result.Value, 10);
    }

    // ============ Batch/Series Tests ============

    [Fact]
    public void Update_TBarSeries_ShouldReturnTSeries()
    {
        var vwma = new Vwma(10);
        var result = vwma.Update(_bars);

        Assert.NotNull(result);
        Assert.Equal(_bars.Count, result.Count);
    }

    [Fact]
    public void Calculate_Static_ShouldReturnTSeries()
    {
        var result = Vwma.Batch(_bars, 10);

        Assert.NotNull(result);
        Assert.Equal(_bars.Count, result.Count);
    }

    [Fact]
    public void Calculate_Static_WithDifferentPeriods_ShouldWork()
    {
        var result14 = Vwma.Batch(_bars, 14);
        var result50 = Vwma.Batch(_bars, 50);

        Assert.NotNull(result14);
        Assert.NotNull(result50);
        Assert.Equal(_bars.Count, result14.Count);
        Assert.Equal(_bars.Count, result50.Count);
    }

    // ============ Span API Tests ============

    [Fact]
    public void Calculate_Span_ShouldMatchBatch()
    {
        var batchResult = Vwma.Batch(_bars, 20);

        var price = _bars.Close.Values.ToArray();
        var volume = _bars.Volume.Values.ToArray();
        var spanOutput = new double[_bars.Count];

        Vwma.Batch(price, volume, spanOutput, 20);

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

        Assert.Throws<ArgumentException>(() => Vwma.Batch(price, volume, output, 10));
    }

    [Fact]
    public void Calculate_Span_OutputLengthMismatch_ShouldThrow()
    {
        var price = new double[100];
        var volume = new double[100];
        var output = new double[50]; // Mismatched

        Assert.Throws<ArgumentException>(() => Vwma.Batch(price, volume, output, 10));
    }

    [Fact]
    public void Calculate_Span_ZeroPeriod_ShouldThrow()
    {
        var price = new double[100];
        var volume = new double[100];
        var output = new double[100];

        Assert.Throws<ArgumentException>(() => Vwma.Batch(price, volume, output, 0));
    }

    [Fact]
    public void Calculate_Span_NegativePeriod_ShouldThrow()
    {
        var price = new double[100];
        var volume = new double[100];
        var output = new double[100];

        Assert.Throws<ArgumentException>(() => Vwma.Batch(price, volume, output, -1));
    }

    // ============ Event Tests ============

    [Fact]
    public void Pub_ShouldFireOnUpdate()
    {
        var vwma = new Vwma(10);
        int eventCount = 0;

        vwma.Pub += (object? sender, in TValueEventArgs args) => eventCount++;

        vwma.Update(_bars[0]);
        vwma.Update(_bars[1]);

        Assert.Equal(2, eventCount);
    }

    // ============ Streaming/Batch Consistency ============

    [Fact]
    public void Streaming_ShouldMatchBatch()
    {
        // Streaming
        var vwma = new Vwma(20);
        var streamingResults = new List<double>();
        foreach (var bar in _bars)
        {
            streamingResults.Add(vwma.Update(bar).Value);
        }

        // Batch
        var batchResult = Vwma.Batch(_bars, 20);

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
        var result = Vwma.Batch(sourceSeries, 20);

        Assert.NotNull(result);
        Assert.Equal(sourceSeries.Count, result.Count);
    }

    [Fact]
    public void Calculate_TSeries_ShouldMatchTValueStreaming()
    {
        var sourceSeries = _bars.Close;
        var batchResult = Vwma.Batch(sourceSeries, 20);

        // Streaming with TValue
        var vwma = new Vwma(20);
        var streamingResults = new List<double>();
        for (int i = 0; i < sourceSeries.Count; i++)
        {
            streamingResults.Add(vwma.Update(sourceSeries[i]).Value);
        }

        // Compare last 100 values
        for (int i = sourceSeries.Count - 100; i < sourceSeries.Count; i++)
        {
            Assert.Equal(batchResult.Values[i], streamingResults[i], 10);
        }
    }
}