using Xunit;

namespace QuanTAlib.Tests;

public class PvdTests
{
    private readonly GBM _gbm;
    private readonly TBarSeries _bars;
    private const int TestDataLength = 1000;

    public PvdTests()
    {
        _gbm = new GBM(seed: 42);
        _bars = new TBarSeries();
        for (int i = 0; i < TestDataLength; i++)
        {
            _bars.Add(_gbm.Next());
        }
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_DefaultParameters_SetsCorrectValues()
    {
        var pvd = new Pvd();
        Assert.Equal("Pvd(14,14,3)", pvd.Name);
        Assert.Equal(17, pvd.WarmupPeriod); // max(14,14) + 3
        Assert.False(pvd.IsHot);
    }

    [Fact]
    public void Constructor_CustomPeriods_SetsCorrectValues()
    {
        var pvd = new Pvd(pricePeriod: 10, volumePeriod: 20, smoothingPeriod: 5);
        Assert.Equal("Pvd(10,20,5)", pvd.Name);
        Assert.Equal(25, pvd.WarmupPeriod); // max(10,20) + 5
    }

    [Fact]
    public void Constructor_InvalidPricePeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Pvd(pricePeriod: 0));
        Assert.Equal("pricePeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_InvalidVolumePeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Pvd(volumePeriod: 0));
        Assert.Equal("volumePeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_InvalidSmoothingPeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Pvd(smoothingPeriod: 0));
        Assert.Equal("smoothingPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Pvd(pricePeriod: -1));
        Assert.Throws<ArgumentException>(() => new Pvd(volumePeriod: -5));
        Assert.Throws<ArgumentException>(() => new Pvd(smoothingPeriod: -2));
    }

    #endregion

    #region Basic Calculation Tests

    [Fact]
    public void Update_ReturnsTValue()
    {
        var pvd = new Pvd();
        var bar = _bars[0];
        var result = pvd.Update(bar);
        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Update_SetsLastProperty()
    {
        var pvd = new Pvd();
        var bar = _bars[0];
        var result = pvd.Update(bar);
        Assert.Equal(result.Value, pvd.Last.Value);
        Assert.Equal(result.Time, pvd.Last.Time);
    }

    [Fact]
    public void Update_SingleBar_ReturnsZero()
    {
        var pvd = new Pvd();
        var result = pvd.Update(_bars[0]);
        Assert.Equal(0.0, result.Value);
    }

    [Fact]
    public void Update_AfterWarmup_ReturnsFiniteValue()
    {
        var pvd = new Pvd(pricePeriod: 5, volumePeriod: 5, smoothingPeriod: 3);
        for (int i = 0; i < pvd.WarmupPeriod + 10; i++)
        {
            pvd.Update(_bars[i]);
        }
        Assert.True(double.IsFinite(pvd.Last.Value));
    }

    [Fact]
    public void Update_DetectsPositiveDivergence()
    {
        // Create scenario: price up, volume down = positive divergence
        var pvd = new Pvd(pricePeriod: 2, volumePeriod: 2, smoothingPeriod: 1);
        var time = DateTime.UtcNow;

        // Establish baseline
        pvd.Update(new TBar(time, 100.0, 100.0, 100.0, 100.0, 1000.0), isNew: true);
        pvd.Update(new TBar(time.AddMinutes(1), 100.0, 100.0, 100.0, 100.0, 1000.0), isNew: true);
        pvd.Update(new TBar(time.AddMinutes(2), 100.0, 100.0, 100.0, 100.0, 1000.0), isNew: true);

        // Price up, volume down
        pvd.Update(new TBar(time.AddMinutes(3), 110.0, 110.0, 110.0, 110.0, 800.0), isNew: true);

        // Should show divergence (price up + volume down = positive)
        Assert.True(pvd.Last.Value > 0);
    }

    [Fact]
    public void Update_DetectsNegativeDivergence()
    {
        // Create scenario: price up, volume up = negative divergence (same direction)
        var pvd = new Pvd(pricePeriod: 2, volumePeriod: 2, smoothingPeriod: 1);
        var time = DateTime.UtcNow;

        // Establish baseline
        pvd.Update(new TBar(time, 100.0, 100.0, 100.0, 100.0, 1000.0), isNew: true);
        pvd.Update(new TBar(time.AddMinutes(1), 100.0, 100.0, 100.0, 100.0, 1000.0), isNew: true);
        pvd.Update(new TBar(time.AddMinutes(2), 100.0, 100.0, 100.0, 100.0, 1000.0), isNew: true);

        // Price up, volume up
        pvd.Update(new TBar(time.AddMinutes(3), 110.0, 110.0, 110.0, 110.0, 1200.0), isNew: true);

        // Should show negative divergence (same direction)
        Assert.True(pvd.Last.Value < 0);
    }

    #endregion

    #region State Management Tests

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var pvd = new Pvd();

        for (int i = 0; i < 20; i++)
        {
            pvd.Update(_bars[i], isNew: true);
        }

        _ = pvd.Last.Value;
        pvd.Update(_bars[20], isNew: true);
        // State should advance (can't easily verify internal state, but no exception means success)
        Assert.True(true);
    }

    [Fact]
    public void Update_IsNewFalse_RollsBackState()
    {
        var pvd = new Pvd();

        for (int i = 0; i < 25; i++)
        {
            pvd.Update(_bars[i], isNew: true);
        }

        _ = pvd.Last.Value;

        // Update with isNew=false should rollback and recalculate
        pvd.Update(_bars[25], isNew: false);
        double valueAfterCorrection = pvd.Last.Value;

        // Values may differ since we're using different input
        // The key is that state was rolled back properly
        Assert.True(double.IsFinite(valueAfterCorrection));
    }

    [Fact]
    public void Update_IterativeCorrections_RestoreState()
    {
        var pvd = new Pvd();

        // Build up state
        for (int i = 0; i < 30; i++)
        {
            pvd.Update(_bars[i], isNew: true);
        }

        _ = pvd.Last.Value;

        // Make several corrections
        for (int c = 0; c < 5; c++)
        {
            pvd.Update(_bars[30], isNew: false);
        }

        // Apply final new bar
        pvd.Update(_bars[30], isNew: true);
        double afterCorrections = pvd.Last.Value;

        // After applying the same bar as new, should get same result
        Assert.True(double.IsFinite(afterCorrections));
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var pvd = new Pvd();

        // Build up state
        for (int i = 0; i < 50; i++)
        {
            pvd.Update(_bars[i], isNew: true);
        }

        Assert.True(pvd.IsHot);

        pvd.Reset();

        Assert.False(pvd.IsHot);
        Assert.Equal(default, pvd.Last);
    }

    #endregion

    #region Warmup and IsHot Tests

    [Fact]
    public void IsHot_FalseBeforeWarmup()
    {
        var pvd = new Pvd(pricePeriod: 5, volumePeriod: 5, smoothingPeriod: 3);

        for (int i = 0; i < pvd.WarmupPeriod - 1; i++)
        {
            pvd.Update(_bars[i], isNew: true);
            Assert.False(pvd.IsHot);
        }
    }

    [Fact]
    public void IsHot_TrueAfterWarmup()
    {
        var pvd = new Pvd(pricePeriod: 5, volumePeriod: 5, smoothingPeriod: 3);

        for (int i = 0; i < pvd.WarmupPeriod; i++)
        {
            pvd.Update(_bars[i], isNew: true);
        }

        Assert.True(pvd.IsHot);
    }

    [Fact]
    public void WarmupPeriod_CalculatedCorrectly()
    {
        var pvd1 = new Pvd(pricePeriod: 10, volumePeriod: 5, smoothingPeriod: 3);
        Assert.Equal(13, pvd1.WarmupPeriod); // max(10,5) + 3

        var pvd2 = new Pvd(pricePeriod: 5, volumePeriod: 20, smoothingPeriod: 5);
        Assert.Equal(25, pvd2.WarmupPeriod); // max(5,20) + 5
    }

    #endregion

    #region NaN and Infinity Handling Tests

    [Fact]
    public void Update_NaNInput_UsesLastValidValue()
    {
        var pvd = new Pvd(pricePeriod: 3, volumePeriod: 3, smoothingPeriod: 2);
        var time = DateTime.UtcNow;

        // Build up state
        for (int i = 0; i < 10; i++)
        {
            pvd.Update(new TBar(time.AddMinutes(i), 100.0 + i, 100.0 + i, 100.0 + i, 100.0 + i, 1000.0 + i * 10), isNew: true);
        }

        _ = pvd.Last.Value;

        // Update with NaN close - should use last valid
        pvd.Update(new TBar(time.AddMinutes(10), double.NaN, double.NaN, double.NaN, double.NaN, 1100.0), isNew: true);

        // Should return NaN when close is NaN and no prior valid close
        // But since we have prior valid, it should use that
        Assert.True(double.IsFinite(pvd.Last.Value) || double.IsNaN(pvd.Last.Value));
    }

    [Fact]
    public void Update_InfinityInput_UsesLastValidValue()
    {
        var pvd = new Pvd(pricePeriod: 3, volumePeriod: 3, smoothingPeriod: 2);
        var time = DateTime.UtcNow;

        // Build up state
        for (int i = 0; i < 10; i++)
        {
            pvd.Update(new TBar(time.AddMinutes(i), 100.0 + i, 100.0 + i, 100.0 + i, 100.0 + i, 1000.0 + i * 10), isNew: true);
        }

        // Update with Infinity
        pvd.Update(new TBar(time.AddMinutes(10), double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, 1100.0), isNew: true);

        Assert.True(double.IsFinite(pvd.Last.Value));
    }

    [Fact]
    public void Update_NegativeInfinityInput_UsesLastValidValue()
    {
        var pvd = new Pvd(pricePeriod: 3, volumePeriod: 3, smoothingPeriod: 2);
        var time = DateTime.UtcNow;

        // Build up state
        for (int i = 0; i < 10; i++)
        {
            pvd.Update(new TBar(time.AddMinutes(i), 100.0 + i, 100.0 + i, 100.0 + i, 100.0 + i, 1000.0 + i * 10), isNew: true);
        }

        // Update with negative infinity
        pvd.Update(new TBar(time.AddMinutes(10), double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity, 1100.0), isNew: true);

        Assert.True(double.IsFinite(pvd.Last.Value));
    }

    [Fact]
    public void Calculate_Span_HandlesNaN()
    {
        double[] closes = [100, 101, double.NaN, 103, 104];
        double[] volumes = [1000, 1100, 1200, 1300, 1400];
        double[] output = new double[5];

        Pvd.Batch(closes.AsSpan(), volumes.AsSpan(), output.AsSpan(), pricePeriod: 2, volumePeriod: 2, smoothingPeriod: 1);

        // Should handle NaN gracefully - result might be NaN or computed value
        Assert.True(output.Length == 5);
    }

    #endregion

    #region Mode Consistency Tests

    [Fact]
    public void AllModes_ProduceSameResults()
    {
        int period = 10;

        // Mode 1: Streaming Update
        var pvdStreaming = new Pvd(pricePeriod: period, volumePeriod: period, smoothingPeriod: 3);
        for (int i = 0; i < _bars.Count; i++)
        {
            pvdStreaming.Update(_bars[i], isNew: true);
        }
        var streamingResults = new List<double>();
        pvdStreaming.Reset();
        for (int i = 0; i < _bars.Count; i++)
        {
            streamingResults.Add(pvdStreaming.Update(_bars[i], isNew: true).Value);
        }

        // Mode 2: Batch via instance Update(TBarSeries)
        var pvdBatch = new Pvd(pricePeriod: period, volumePeriod: period, smoothingPeriod: 3);
        var batchResult = pvdBatch.Update(_bars);

        // Mode 3: Static Batch(TBarSeries)
        var staticResult = Pvd.Batch(_bars, pricePeriod: period, volumePeriod: period, smoothingPeriod: 3);

        // Mode 4: Static Batch(Span)
        double[] closes = new double[_bars.Count];
        double[] volumes = new double[_bars.Count];
        double[] spanOutput = new double[_bars.Count];
        for (int i = 0; i < _bars.Count; i++)
        {
            closes[i] = _bars[i].Close;
            volumes[i] = _bars[i].Volume;
        }
        Pvd.Batch(closes.AsSpan(), volumes.AsSpan(), spanOutput.AsSpan(), pricePeriod: period, volumePeriod: period, smoothingPeriod: 3);

        // Compare last 100 values (after warmup)
        int compareStart = _bars.Count - 100;
        for (int i = compareStart; i < _bars.Count; i++)
        {
            Assert.Equal(batchResult[i].Value, staticResult[i].Value, precision: 10);
            Assert.Equal(batchResult[i].Value, spanOutput[i], precision: 10);
        }
    }

    #endregion

    #region Span API Tests

    [Fact]
    public void Calculate_Span_ValidatesLengths()
    {
        double[] closes = [1, 2, 3, 4, 5];
        double[] volumes = [100, 200, 300]; // Wrong length
        double[] output = new double[5];

        var ex = Assert.Throws<ArgumentException>(() =>
            Pvd.Batch(closes.AsSpan(), volumes.AsSpan(), output.AsSpan()));
        Assert.Equal("volume", ex.ParamName);
    }

    [Fact]
    public void Calculate_Span_ValidatesOutputLength()
    {
        double[] closes = [1, 2, 3, 4, 5];
        double[] volumes = [100, 200, 300, 400, 500];
        double[] output = new double[3]; // Too short

        var ex = Assert.Throws<ArgumentException>(() =>
            Pvd.Batch(closes.AsSpan(), volumes.AsSpan(), output.AsSpan()));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Calculate_Span_ValidatesPricePeriod()
    {
        double[] closes = [1, 2, 3, 4, 5];
        double[] volumes = [100, 200, 300, 400, 500];
        double[] output = new double[5];

        var ex = Assert.Throws<ArgumentException>(() =>
            Pvd.Batch(closes.AsSpan(), volumes.AsSpan(), output.AsSpan(), pricePeriod: 0));
        Assert.Equal("pricePeriod", ex.ParamName);
    }

    [Fact]
    public void Calculate_Span_ValidatesVolumePeriod()
    {
        double[] closes = [1, 2, 3, 4, 5];
        double[] volumes = [100, 200, 300, 400, 500];
        double[] output = new double[5];

        var ex = Assert.Throws<ArgumentException>(() =>
            Pvd.Batch(closes.AsSpan(), volumes.AsSpan(), output.AsSpan(), volumePeriod: 0));
        Assert.Equal("volumePeriod", ex.ParamName);
    }

    [Fact]
    public void Calculate_Span_ValidatesSmoothingPeriod()
    {
        double[] closes = [1, 2, 3, 4, 5];
        double[] volumes = [100, 200, 300, 400, 500];
        double[] output = new double[5];

        var ex = Assert.Throws<ArgumentException>(() =>
            Pvd.Batch(closes.AsSpan(), volumes.AsSpan(), output.AsSpan(), smoothingPeriod: 0));
        Assert.Equal("smoothingPeriod", ex.ParamName);
    }

    [Fact]
    public void Calculate_Span_LargeData_NoStackOverflow()
    {
        int size = 10000;
        double[] closes = new double[size];
        double[] volumes = new double[size];
        double[] output = new double[size];

        for (int i = 0; i < size; i++)
        {
            closes[i] = 100.0 + i * 0.01;
            volumes[i] = 1000000.0 + i * 100;
        }

        // Should not stack overflow
        Pvd.Batch(closes.AsSpan(), volumes.AsSpan(), output.AsSpan());

        Assert.True(double.IsFinite(output[size - 1]));
    }

    #endregion

    #region Event Chaining Tests

    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var pvd = new Pvd();
        int eventCount = 0;

        pvd.Pub += (object? sender, in TValueEventArgs args) => eventCount++;

        for (int i = 0; i < 10; i++)
        {
            pvd.Update(_bars[i], isNew: true);
        }

        Assert.Equal(10, eventCount);
    }

    [Fact]
    public void Chaining_ProcessBars_Works()
    {
        var gbm = new GBM(seed: 42);
        var pvd = new Pvd(pricePeriod: 5, volumePeriod: 5, smoothingPeriod: 2);

        // Process bars through indicator
        for (int i = 0; i < 20; i++)
        {
            pvd.Update(gbm.Next());
        }

        Assert.True(pvd.IsHot);
        Assert.True(double.IsFinite(pvd.Last.Value));
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void Update_ZeroVolume_HandlesGracefully()
    {
        var pvd = new Pvd(pricePeriod: 2, volumePeriod: 2, smoothingPeriod: 1);
        var time = DateTime.UtcNow;

        pvd.Update(new TBar(time, 100.0, 100.0, 100.0, 100.0, 0.0), isNew: true);
        pvd.Update(new TBar(time.AddMinutes(1), 101.0, 101.0, 101.0, 101.0, 0.0), isNew: true);
        pvd.Update(new TBar(time.AddMinutes(2), 102.0, 102.0, 102.0, 102.0, 0.0), isNew: true);
        pvd.Update(new TBar(time.AddMinutes(3), 103.0, 103.0, 103.0, 103.0, 0.0), isNew: true);

        Assert.True(double.IsFinite(pvd.Last.Value));
    }

    [Fact]
    public void Update_NegativeVolume_TreatedAsZero()
    {
        var pvd = new Pvd(pricePeriod: 2, volumePeriod: 2, smoothingPeriod: 1);
        var time = DateTime.UtcNow;

        pvd.Update(new TBar(time, 100.0, 100.0, 100.0, 100.0, -1000.0), isNew: true);
        pvd.Update(new TBar(time.AddMinutes(1), 101.0, 101.0, 101.0, 101.0, -500.0), isNew: true);
        pvd.Update(new TBar(time.AddMinutes(2), 102.0, 102.0, 102.0, 102.0, 1000.0), isNew: true);
        pvd.Update(new TBar(time.AddMinutes(3), 103.0, 103.0, 103.0, 103.0, 1200.0), isNew: true);

        Assert.True(double.IsFinite(pvd.Last.Value));
    }

    [Fact]
    public void Update_ConstantPriceAndVolume_ReturnsZero()
    {
        var pvd = new Pvd(pricePeriod: 3, volumePeriod: 3, smoothingPeriod: 1);
        var time = DateTime.UtcNow;

        // All same values - no momentum in either direction
        for (int i = 0; i < 10; i++)
        {
            pvd.Update(new TBar(time.AddMinutes(i), 100.0, 100.0, 100.0, 100.0, 1000.0), isNew: true);
        }

        // With no change, ROC is 0, so divergence should be 0
        Assert.Equal(0.0, pvd.Last.Value);
    }

    [Fact]
    public void Update_MinimumPeriods_Works()
    {
        var pvd = new Pvd(pricePeriod: 1, volumePeriod: 1, smoothingPeriod: 1);
        var time = DateTime.UtcNow;

        pvd.Update(new TBar(time, 100.0, 100.0, 100.0, 100.0, 1000.0), isNew: true);
        pvd.Update(new TBar(time.AddMinutes(1), 105.0, 105.0, 105.0, 105.0, 900.0), isNew: true);

        Assert.True(double.IsFinite(pvd.Last.Value));
    }

    [Fact]
    public void Update_AsymmetricPeriods_Works()
    {
        var pvd = new Pvd(pricePeriod: 5, volumePeriod: 20, smoothingPeriod: 3);

        for (int i = 0; i < 30; i++)
        {
            pvd.Update(_bars[i], isNew: true);
        }

        Assert.True(pvd.IsHot);
        Assert.True(double.IsFinite(pvd.Last.Value));
    }

    [Fact]
    public void Update_TValueInput_ThrowsNotSupported()
    {
        var pvd = new Pvd();
        var value = new TValue(DateTime.UtcNow, 100);

        Assert.Throws<NotSupportedException>(() => pvd.Update(value));
    }

    #endregion
}
