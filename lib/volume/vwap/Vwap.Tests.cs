namespace QuanTAlib.Tests;

public class VwapTests
{
    private readonly GBM _feed;
    private readonly TBarSeries _bars;

    public VwapTests()
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
    public void Constructor_DefaultPeriod_ShouldBeZero()
    {
        var vwap = new Vwap();
        Assert.Equal("VWAP", vwap.Name);
    }

    [Fact]
    public void Constructor_WithPeriod_ShouldSetName()
    {
        var vwap = new Vwap(390);
        Assert.Equal("VWAP(390)", vwap.Name);
    }

    [Fact]
    public void Constructor_NegativePeriod_ShouldThrow()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Vwap(-1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_ZeroPeriod_ShouldNotThrow()
    {
        var vwap = new Vwap(0);
        Assert.Equal("VWAP", vwap.Name);
    }

    // ============ Basic Calculation Tests ============

    [Fact]
    public void Update_ReturnsValidTValue()
    {
        var vwap = new Vwap();
        var bar = _bars[0];
        var result = vwap.Update(bar);

        Assert.NotEqual(default, result);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_FirstBar_ShouldBeTypicalPrice()
    {
        var vwap = new Vwap();
        var bar = new TBar(DateTime.UtcNow, 10, 15, 8, 12, 1000);
        var result = vwap.Update(bar);

        // VWAP of first bar = typical price = (H+L+C)/3 = (15+8+12)/3 = 11.666...
        double expectedTypicalPrice = (15.0 + 8.0 + 12.0) / 3.0;
        Assert.Equal(expectedTypicalPrice, result.Value, 10);
    }

    [Fact]
    public void Update_MultipleBarsSamePrice_ShouldReturnSameVwap()
    {
        var vwap = new Vwap();
        // All bars have same typical price = 10
        var bar1 = new TBar(DateTime.UtcNow, 10, 10, 10, 10, 100);
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 10, 10, 10, 10, 200);
        var bar3 = new TBar(DateTime.UtcNow.AddMinutes(2), 10, 10, 10, 10, 300);

        vwap.Update(bar1);
        vwap.Update(bar2);
        var result = vwap.Update(bar3);

        Assert.Equal(10.0, result.Value, 10);
    }

    [Fact]
    public void Update_VolumeWeighting_Works()
    {
        var vwap = new Vwap();
        // Bar 1: price=10, volume=100
        // Bar 2: price=20, volume=300
        // VWAP = (10*100 + 20*300) / (100+300) = (1000 + 6000) / 400 = 17.5
        var bar1 = new TBar(DateTime.UtcNow, 10, 10, 10, 10, 100);
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 20, 20, 20, 20, 300);

        vwap.Update(bar1);
        var result = vwap.Update(bar2);

        Assert.Equal(17.5, result.Value, 10);
    }

    [Fact]
    public void IsHot_AfterFirstBar_ShouldBeTrue()
    {
        var vwap = new Vwap();
        Assert.False(vwap.IsHot);

        vwap.Update(_bars[0]);
        Assert.True(vwap.IsHot);
    }

    [Fact]
    public void WarmupPeriod_ShouldBeOne()
    {
        var vwap = new Vwap();
        Assert.Equal(1, vwap.WarmupPeriod);
    }

    // ============ Bar Correction Tests (isNew) ============

    [Fact]
    public void Update_IsNewTrue_ShouldAdvanceState()
    {
        var vwap = new Vwap();
        var bar1 = new TBar(DateTime.UtcNow, 10, 10, 10, 10, 100);
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 20, 20, 20, 20, 100);

        vwap.Update(bar1, isNew: true);
        var result1 = vwap.Last.Value;

        vwap.Update(bar2, isNew: true);
        var result2 = vwap.Last.Value;

        Assert.NotEqual(result1, result2);
    }

    [Fact]
    public void Update_IsNewFalse_ShouldRollback()
    {
        var vwap = new Vwap();
        var bar1 = new TBar(DateTime.UtcNow, 10, 10, 10, 10, 100);
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 20, 20, 20, 20, 100);
        var bar2Updated = new TBar(DateTime.UtcNow.AddMinutes(1), 15, 15, 15, 15, 100);

        vwap.Update(bar1, isNew: true);
        vwap.Update(bar2, isNew: true);
        var afterBar2 = vwap.Last.Value;

        // Correct bar2 with updated values
        vwap.Update(bar2Updated, isNew: false);
        var afterCorrection = vwap.Last.Value;

        Assert.NotEqual(afterBar2, afterCorrection);
    }

    [Fact]
    public void Update_IterativeCorrections_ShouldRestoreState()
    {
        var vwap = new Vwap();

        // Process first 10 bars
        for (int i = 0; i < 10; i++)
        {
            vwap.Update(_bars[i], isNew: true);
        }
        _ = vwap.Last.Value; // capture state before bar 11

        // Process bar 11
        vwap.Update(_bars[10], isNew: true);
        var valueAfter11 = vwap.Last.Value;

        // Correct bar 11 multiple times with same data
        for (int i = 0; i < 5; i++)
        {
            vwap.Update(_bars[10], isNew: false);
        }
        var valueAfterCorrections = vwap.Last.Value;

        // Should get same result as after first processing of bar 11
        Assert.Equal(valueAfter11, valueAfterCorrections, 10);
    }

    // ============ Reset Tests ============

    [Fact]
    public void Reset_ShouldClearState()
    {
        var vwap = new Vwap();

        for (int i = 0; i < 100; i++)
        {
            vwap.Update(_bars[i]);
        }
        Assert.True(vwap.IsHot);

        vwap.Reset();

        Assert.False(vwap.IsHot);
        Assert.Equal(default, vwap.Last);
    }

    // ============ Period Reset Tests ============

    [Fact]
    public void Update_WithPeriod_ShouldResetAtPeriodBoundary()
    {
        var vwap = new Vwap(5);
        var results = new List<double>();

        // Create bars with consistent price/volume
        for (int i = 0; i < 10; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 100, 100, 100, 100, 1000);
            results.Add(vwap.Update(bar).Value);
        }

        // All values should be 100 since price is constant
        foreach (var value in results)
        {
            Assert.Equal(100.0, value, 10);
        }
    }

    [Fact]
    public void Update_PeriodReset_ShouldClearCumulativeSums()
    {
        var vwap = new Vwap(3);

        // Bars 0-2: price=10, VWAP=10
        for (int i = 0; i < 3; i++)
        {
            vwap.Update(new TBar(DateTime.UtcNow.AddMinutes(i), 10, 10, 10, 10, 100));
        }
        var beforeReset = vwap.Last.Value;
        Assert.Equal(10.0, beforeReset, 10);

        // Bar 3: Reset happens, price=20, VWAP should be 20
        var result = vwap.Update(new TBar(DateTime.UtcNow.AddMinutes(3), 20, 20, 20, 20, 100));
        Assert.Equal(20.0, result.Value, 10);
    }

    // ============ NaN/Infinity Handling ============

    [Fact]
    public void Update_NaN_ShouldUseLastValidValue()
    {
        var vwap = new Vwap();

        // First bar establishes valid values
        var bar1 = new TBar(DateTime.UtcNow, 10, 15, 8, 12, 1000);
        vwap.Update(bar1);
        _ = vwap.Last.Value; // establish first valid value

        // Second bar with NaN should use last valid
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);
        var result = vwap.Update(bar2);

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_Infinity_ShouldUseLastValidValue()
    {
        var vwap = new Vwap();

        var bar1 = new TBar(DateTime.UtcNow, 10, 15, 8, 12, 1000);
        vwap.Update(bar1);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);
        var result = vwap.Update(bar2);

        Assert.True(double.IsFinite(result.Value));
    }

    // ============ TValue Input Tests ============

    [Fact]
    public void Update_TValue_ShouldWork()
    {
        var vwap = new Vwap();
        var input = new TValue(DateTime.UtcNow, 100.0);
        var result = vwap.Update(input);

        // With TValue, it creates synthetic bar with price as OHLC and volume=1
        Assert.Equal(100.0, result.Value, 10);
    }

    [Fact]
    public void Update_TValue_MultipleInputs()
    {
        var vwap = new Vwap();

        // TValue input assumes volume=1 for all
        // VWAP = (100*1 + 200*1) / 2 = 150
        vwap.Update(new TValue(DateTime.UtcNow, 100.0));
        var result = vwap.Update(new TValue(DateTime.UtcNow.AddMinutes(1), 200.0));

        Assert.Equal(150.0, result.Value, 10);
    }

    // ============ Batch/Series Tests ============

    [Fact]
    public void Update_TBarSeries_ShouldReturnTSeries()
    {
        var vwap = new Vwap();
        var result = vwap.Update(_bars);

        Assert.NotNull(result);
        Assert.Equal(_bars.Count, result.Count);
    }

    [Fact]
    public void Calculate_Static_ShouldReturnTSeries()
    {
        var result = Vwap.Batch(_bars);

        Assert.NotNull(result);
        Assert.Equal(_bars.Count, result.Count);
    }

    [Fact]
    public void Calculate_Static_WithPeriod_ShouldWork()
    {
        var result = Vwap.Batch(_bars, 100);

        Assert.NotNull(result);
        Assert.Equal(_bars.Count, result.Count);
    }

    // ============ Span API Tests ============

    [Fact]
    public void Calculate_Span_ShouldMatchBatch()
    {
        var batchResult = Vwap.Batch(_bars);

        var high = _bars.High.Values.ToArray();
        var low = _bars.Low.Values.ToArray();
        var close = _bars.Close.Values.ToArray();
        var volume = _bars.Volume.Values.ToArray();
        var spanOutput = new double[_bars.Count];

        Vwap.Batch(high, low, close, volume, spanOutput);

        for (int i = 0; i < _bars.Count; i++)
        {
            Assert.Equal(batchResult.Values[i], spanOutput[i], 12);
        }
    }

    [Fact]
    public void Calculate_Span_MismatchedLengths_ShouldThrow()
    {
        var high = new double[100];
        var low = new double[99]; // Mismatched
        var close = new double[100];
        var volume = new double[100];
        var output = new double[100];

        Assert.Throws<ArgumentException>(() => Vwap.Batch(high, low, close, volume, output));
    }

    [Fact]
    public void Calculate_Span_OutputLengthMismatch_ShouldThrow()
    {
        var high = new double[100];
        var low = new double[100];
        var close = new double[100];
        var volume = new double[100];
        var output = new double[50]; // Mismatched

        Assert.Throws<ArgumentException>(() => Vwap.Batch(high, low, close, volume, output));
    }

    [Fact]
    public void Calculate_Span_NegativePeriod_ShouldThrow()
    {
        var high = new double[100];
        var low = new double[100];
        var close = new double[100];
        var volume = new double[100];
        var output = new double[100];

        Assert.Throws<ArgumentException>(() => Vwap.Batch(high, low, close, volume, output, -1));
    }

    // ============ Event Tests ============

    [Fact]
    public void Pub_ShouldFireOnUpdate()
    {
        var vwap = new Vwap();
        int eventCount = 0;

        vwap.Pub += (object? sender, in TValueEventArgs args) => eventCount++;

        vwap.Update(_bars[0]);
        vwap.Update(_bars[1]);

        Assert.Equal(2, eventCount);
    }

    // ============ Streaming/Batch Consistency ============

    [Fact]
    public void Streaming_ShouldMatchBatch()
    {
        // Streaming
        var vwap = new Vwap();
        var streamingResults = new List<double>();
        foreach (var bar in _bars)
        {
            streamingResults.Add(vwap.Update(bar).Value);
        }

        // Batch
        var batchResult = Vwap.Batch(_bars);

        // Compare last 100 values
        for (int i = _bars.Count - 100; i < _bars.Count; i++)
        {
            Assert.Equal(batchResult.Values[i], streamingResults[i], 10);
        }
    }
}
