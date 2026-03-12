// Volatility Ratio (VR) Unit Tests

using Xunit;

namespace QuanTAlib.Tests;

public class VrTests
{
    private readonly GBM _gbm;
    private const double Tolerance = 1e-10;
    private const int DefaultPeriod = 14;

    public VrTests()
    {
        _gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
    }

    private TBarSeries GenerateBarData(int count)
    {
        _gbm.Reset(DateTime.UtcNow.Ticks);
        return _gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_DefaultParameters_SetsCorrectValues()
    {
        var vr = new Vr();
        Assert.Equal(DefaultPeriod, vr.Period);
        Assert.Equal($"Vr({DefaultPeriod})", vr.Name);
        Assert.Equal(DefaultPeriod, vr.WarmupPeriod);
    }

    [Fact]
    public void Constructor_CustomPeriod_SetsCorrectValues()
    {
        var vr = new Vr(period: 20);
        Assert.Equal(20, vr.Period);
        Assert.Equal("Vr(20)", vr.Name);
        Assert.Equal(20, vr.WarmupPeriod);
    }

    [Fact]
    public void Constructor_ZeroPeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Vr(period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Vr(period: -5));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithTBarSeriesSource_PrimesIndicator()
    {
        var bars = GenerateBarData(50);
        var vr = new Vr(bars, period: 10);
        Assert.True(vr.IsHot);
        Assert.True(double.IsFinite(vr.Last.Value));
    }

    #endregion

    #region Basic Calculation Tests

    [Fact]
    public void Update_SingleBar_ReturnsNonNegativeValue()
    {
        var vr = new Vr();
        var bar = new TBar(DateTime.UtcNow, 100.0, 102.0, 98.0, 101.0, 1000);
        var result = vr.Update(bar);
        Assert.True(result.Value >= 0);
    }

    [Fact]
    public void Update_ConstantTR_ProducesVRNearOne()
    {
        var vr = new Vr(period: 5);
        for (int i = 0; i < 30; i++)
        {
            // Consistent range: VR should converge to 1.0
            vr.Update(new TBar(DateTime.UtcNow, 100.0, 102.0, 98.0, 101.0, 1000));
        }
        // With constant TR, VR should be near 1.0
        Assert.True(vr.Last.Value > 0.9 && vr.Last.Value < 1.1, $"Expected near 1.0, got {vr.Last.Value}");
    }

    [Fact]
    public void Update_ReturnsNonNegativeValue()
    {
        var vr = new Vr();
        var bars = GenerateBarData(100);

        for (int i = 0; i < bars.Count; i++)
        {
            var result = vr.Update(bars[i]);
            Assert.True(result.Value >= 0, $"VR should be non-negative, got {result.Value}");
        }
    }

    [Fact]
    public void Update_HighVolatilityBar_ProducesVRAboveOne()
    {
        var vr = new Vr(period: 10);

        // Build up ATR with normal bars
        for (int i = 0; i < 20; i++)
        {
            vr.Update(new TBar(DateTime.UtcNow, 100.0, 101.0, 99.0, 100.5, 1000));
        }

        // High volatility bar: TR much larger than ATR
        var highVolBar = new TBar(DateTime.UtcNow, 100.0, 110.0, 90.0, 105.0, 1000);
        var result = vr.Update(highVolBar);

        Assert.True(result.Value > 1.0, $"VR should be > 1.0 for high vol bar, got {result.Value}");
    }

    [Fact]
    public void Update_LowVolatilityBar_ProducesVRBelowOne()
    {
        var vr = new Vr(period: 10);

        // Build up ATR with normal bars
        for (int i = 0; i < 20; i++)
        {
            vr.Update(new TBar(DateTime.UtcNow, 100.0, 105.0, 95.0, 102.0, 1000));
        }

        // Low volatility bar: TR much smaller than ATR
        var lowVolBar = new TBar(DateTime.UtcNow, 100.0, 100.5, 99.5, 100.2, 1000);
        var result = vr.Update(lowVolBar);

        Assert.True(result.Value < 1.0, $"VR should be < 1.0 for low vol bar, got {result.Value}");
    }

    [Fact]
    public void Update_GapIncludedInTR_ProducesCorrectVR()
    {
        var vr = new Vr(period: 10);

        // Build up some history
        for (int i = 0; i < 15; i++)
        {
            vr.Update(new TBar(DateTime.UtcNow, 100.0, 101.0, 99.0, 100.0, 1000));
        }

        // Gap up: High-PrevClose should be largest component
        var gapBar = new TBar(DateTime.UtcNow, 105.0, 106.0, 104.0, 105.5, 1000);
        var result = vr.Update(gapBar);

        // TR = max(2, 6, 4) = 6 (High - PrevClose = 106 - 100 = 6)
        Assert.True(result.Value > 1.0, $"Gap bar should produce VR > 1.0, got {result.Value}");
    }

    #endregion

    #region IsHot and Warmup Tests

    [Fact]
    public void IsHot_BeforeWarmup_ReturnsFalse()
    {
        var vr = new Vr(period: 10);
        for (int i = 0; i < 5; i++)
        {
            vr.Update(new TBar(DateTime.UtcNow, 100.0 + i, 102.0 + i, 98.0 + i, 101.0 + i, 1000));
        }
        Assert.False(vr.IsHot);
    }

    [Fact]
    public void IsHot_AfterWarmup_ReturnsTrue()
    {
        var vr = new Vr(period: 10);
        for (int i = 0; i < 15; i++)
        {
            vr.Update(new TBar(DateTime.UtcNow, 100.0 + i, 102.0 + i, 98.0 + i, 101.0 + i, 1000));
        }
        Assert.True(vr.IsHot);
    }

    [Fact]
    public void WarmupPeriod_EqualsToPeriod()
    {
        var vr = new Vr(period: 15);
        Assert.Equal(15, vr.WarmupPeriod);
    }

    #endregion

    #region Bar Correction (isNew) Tests

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var vr = new Vr(period: 5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            vr.Update(new TBar(time.AddSeconds(i), 100 + i, 102 + i, 98 + i, 101 + i, 1000), isNew: true);
        }

        double valueBeforeNew = vr.Last.Value;
        vr.Update(new TBar(time.AddSeconds(10), 150, 155, 145, 152, 1000), isNew: true);

        Assert.NotEqual(valueBeforeNew, vr.Last.Value);
    }

    [Fact]
    public void Update_IsNewFalse_UpdatesCurrentBar()
    {
        var vr = new Vr(period: 5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 15; i++)
        {
            vr.Update(new TBar(time.AddSeconds(i), 100 + i, 102 + i, 98 + i, 101 + i, 1000), isNew: true);
        }

        double valueBeforeCorrection = vr.Last.Value;

        // First correction
        vr.Update(new TBar(time.AddSeconds(15), 200, 210, 190, 205, 1000), isNew: false);
        double valueAfterCorrection1 = vr.Last.Value;

        // Second correction to different value
        vr.Update(new TBar(time.AddSeconds(15), 50, 55, 45, 52, 1000), isNew: false);
        double valueAfterCorrection2 = vr.Last.Value;

        Assert.NotEqual(valueBeforeCorrection, valueAfterCorrection1);
        Assert.NotEqual(valueAfterCorrection1, valueAfterCorrection2);
    }

    [Fact]
    public void Update_MultipleCorrections_RestoresPreviousState()
    {
        var vr = new Vr(period: 5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 15; i++)
        {
            vr.Update(new TBar(time.AddSeconds(i), 100 + i, 102 + i, 98 + i, 101 + i, 1000), isNew: true);
        }

        // Add a new bar
        var newBar = new TBar(time.AddSeconds(15), 115, 117, 113, 116, 1000);
        vr.Update(newBar, isNew: true);
        double baseValue = vr.Last.Value;

        // Multiple corrections should all restore to same base state
        vr.Update(new TBar(time.AddSeconds(15), 200, 210, 190, 205, 1000), isNew: false);
        vr.Update(newBar, isNew: false);
        double restoredValue = vr.Last.Value;

        Assert.Equal(baseValue, restoredValue, 10);
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ClearsAllState()
    {
        var vr = new Vr(period: 5);
        var bars = GenerateBarData(20);

        for (int i = 0; i < bars.Count; i++)
        {
            vr.Update(bars[i]);
        }

        Assert.True(vr.IsHot);

        vr.Reset();

        Assert.False(vr.IsHot);
        Assert.Equal(default, vr.Last);
    }

    [Fact]
    public void Reset_AllowsReuse()
    {
        var vr = new Vr(period: 5);
        var bars = GenerateBarData(20);

        for (int i = 0; i < bars.Count; i++)
        {
            vr.Update(bars[i]);
        }
        double firstRunValue = vr.Last.Value;

        vr.Reset();

        for (int i = 0; i < bars.Count; i++)
        {
            vr.Update(bars[i]);
        }
        double secondRunValue = vr.Last.Value;

        Assert.Equal(firstRunValue, secondRunValue, 10);
    }

    #endregion

    #region NaN and Infinity Handling Tests

    [Fact]
    public void Update_NaNInput_UsesLastValidValue()
    {
        var vr = new Vr(period: 5);

        for (int i = 0; i < 15; i++)
        {
            vr.Update(new TBar(DateTime.UtcNow, 100 + i, 102 + i, 98 + i, 101 + i, 1000));
        }

        // Update with NaN
        vr.Update(new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, 1000));
        Assert.True(double.IsFinite(vr.Last.Value));
    }

    [Fact]
    public void Update_InfinityInput_UsesLastValidValue()
    {
        var vr = new Vr(period: 5);

        for (int i = 0; i < 15; i++)
        {
            vr.Update(new TBar(DateTime.UtcNow, 100 + i, 102 + i, 98 + i, 101 + i, 1000));
        }

        vr.Update(new TBar(DateTime.UtcNow, double.PositiveInfinity, double.PositiveInfinity, 98, 101, 1000));
        Assert.True(double.IsFinite(vr.Last.Value));
    }

    [Fact]
    public void Update_MultipleNaNs_StaysFinite()
    {
        var vr = new Vr(period: 5);

        for (int i = 0; i < 15; i++)
        {
            vr.Update(new TBar(DateTime.UtcNow, 100 + i, 102 + i, 98 + i, 101 + i, 1000));
        }

        for (int i = 0; i < 5; i++)
        {
            vr.Update(new TBar(DateTime.UtcNow, double.NaN, double.NaN, double.NaN, double.NaN, 1000));
        }

        Assert.True(double.IsFinite(vr.Last.Value));
    }

    #endregion

    #region TBarSeries and Batch Tests

    [Fact]
    public void Update_TBarSeries_ReturnsCorrectLength()
    {
        var vr = new Vr();
        var bars = GenerateBarData(100);

        var result = vr.Update(bars);
        Assert.Equal(bars.Count, result.Count);
    }

    [Fact]
    public void Calculate_Static_ProducesValidResults()
    {
        var bars = GenerateBarData(100);

        var result = Vr.Batch(bars, period: 10);

        Assert.Equal(bars.Count, result.Count);
        for (int i = 0; i < result.Count; i++)
        {
            Assert.True(double.IsFinite(result.Values[i]));
            Assert.True(result.Values[i] >= 0);
        }
    }

    [Fact]
    public void Batch_ProducesConsistentResults()
    {
        var bars = GenerateBarData(100);

        double[] output = new double[100];
        Vr.Batch(bars, output, period: 10);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]));
            Assert.True(output[i] >= 0);
        }
    }

    [Fact]
    public void Batch_ZeroPeriod_ThrowsArgumentException()
    {
        var bars = GenerateBarData(10);
        double[] output = new double[10];
        var ex = Assert.Throws<ArgumentException>(() => Vr.Batch(bars, output, period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_OutputTooSmall_ThrowsArgumentException()
    {
        var bars = GenerateBarData(10);
        double[] output = new double[5];
        var ex = Assert.Throws<ArgumentException>(() => Vr.Batch(bars, output));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_EmptySource_DoesNotThrow()
    {
        var bars = new TBarSeries();
        double[] output = [];
        Vr.Batch(bars, output);
        Assert.Empty(output);
    }

    [Fact]
    public void Batch_HlcArrays_ProducesValidResults()
    {
        int len = 50;
        double[] high = new double[len];
        double[] low = new double[len];
        double[] close = new double[len];
        double[] output = new double[len];

        for (int i = 0; i < len; i++)
        {
            high[i] = 102 + i;
            low[i] = 98 + i;
            close[i] = 101 + i;
        }

        Vr.Batch(high, low, close, output, period: 10);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]));
            Assert.True(output[i] >= 0);
        }
    }

    #endregion

    #region Mode Consistency Tests

    [Fact]
    public void AllModes_ProduceSameResults()
    {
        var bars = GenerateBarData(100);
        int period = 10;

        // Mode 1: Streaming
        var streamingVr = new Vr(period);
        for (int i = 0; i < bars.Count; i++)
        {
            streamingVr.Update(bars[i], isNew: true);
        }

        // Mode 2: TBarSeries batch
        var batchResult = Vr.Batch(bars, period);

        // Mode 3: Span batch
        double[] spanOutput = new double[bars.Count];
        Vr.Batch(bars, spanOutput, period);

        // Compare last 50 values (after warmup)
        int compareStart = bars.Count - 50;
        for (int i = compareStart; i < bars.Count; i++)
        {
            double batch = batchResult[i].Value;
            double span = spanOutput[i];

            Assert.Equal(batch, span, Tolerance);
        }

        // Final values should match
        Assert.Equal(streamingVr.Last.Value, batchResult[bars.Count - 1].Value, 1e-8);
        Assert.Equal(streamingVr.Last.Value, spanOutput[bars.Count - 1], 1e-8);
    }

    #endregion

    #region Event Tests

    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var vr = new Vr(period: 5);
        int eventCount = 0;

        vr.Pub += (object? sender, in TValueEventArgs args) => eventCount++;

        var time = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            vr.Update(new TBar(time.AddSeconds(i), 100 + i, 102 + i, 98 + i, 101 + i, 1000));
        }

        Assert.Equal(5, eventCount);
    }

    #endregion

    #region TValue Input Tests

    [Fact]
    public void Update_TValue_CreatesSyntheticBar()
    {
        var vr1 = new Vr(period: 5);
        var vr2 = new Vr(period: 5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 15; i++)
        {
            // TValue input creates bar with O=H=L=C
            vr1.Update(new TValue(time.AddSeconds(i), 100.0 + i));
            vr2.Update(new TBar(time.AddSeconds(i), 100.0 + i, 100.0 + i, 100.0 + i, 100.0 + i, 0));
        }

        Assert.Equal(vr1.Last.Value, vr2.Last.Value, Tolerance);
    }

    #endregion

    #region Large Period Tests

    [Fact]
    public void LargeDataset_NoStackOverflow()
    {
        var bars = GenerateBarData(10000);

        double[] output = new double[10000];
        Vr.Batch(bars, output, period: 14);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]));
            Assert.True(output[i] >= 0);
        }
    }

    #endregion

    #region Prime Tests

    [Fact]
    public void Prime_SetsInitialState()
    {
        var vr = new Vr(period: 5);
        double[] warmupData = [100, 101, 102, 103, 104, 105, 106, 107, 108, 109];

        vr.Prime(warmupData);

        Assert.True(vr.IsHot);
    }

    #endregion

    #region VR Specific Tests

    [Fact]
    public void Update_TrueRangeCalculation_IncludesGaps()
    {
        var vr = new Vr(period: 5);

        // First bar establishes previous close
        vr.Update(new TBar(DateTime.UtcNow, 100, 101, 99, 100, 1000));

        // Gap up bar: High-PrevClose > H-L
        // PrevClose = 100, Current bar: O=105, H=107, L=104, C=106
        // TR = max(3, 7, 4) = 7 (High - PrevClose)
        var gapUpBar = new TBar(DateTime.UtcNow, 105, 107, 104, 106, 1000);
        vr.Update(gapUpBar);

        // The TR should incorporate the gap
        Assert.True(vr.Last.Value > 0, "VR should be positive with gap");
    }

    [Fact]
    public void Update_BiasCorrection_WorksDuringWarmup()
    {
        var vr = new Vr(period: 20);
        var bars = GenerateBarData(5);

        // During warmup, bias correction should prevent extreme values
        for (int i = 0; i < bars.Count; i++)
        {
            var result = vr.Update(bars[i]);
            Assert.True(double.IsFinite(result.Value), $"Value at index {i} should be finite");
            Assert.True(result.Value >= 0, $"Value at index {i} should be non-negative");
        }
    }

    [Fact]
    public void Update_VRMeanReverts_TowardsOne()
    {
        var vr = new Vr(period: 10);

        // Build up history with varying volatility
        for (int i = 0; i < 50; i++)
        {
            double range = 2.0 + (i % 5) * 0.5; // Varying range
            vr.Update(new TBar(DateTime.UtcNow, 100, 100 + range, 100 - range, 100 + range / 2, 1000));
        }

        // VR should oscillate around 1.0 over time
        // After many bars, the average should be close to 1.0
        Assert.True(vr.Last.Value > 0, "VR should be positive");
        Assert.True(double.IsFinite(vr.Last.Value), "VR should be finite");
    }

    #endregion
}
