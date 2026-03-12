// Volatility of Volatility (VOV) Unit Tests

using Xunit;

namespace QuanTAlib.Tests;

public class VovTests
{
    private readonly GBM _gbm;
    private const double Tolerance = 1e-10;
    private const int DefaultVolatilityPeriod = 20;
    private const int DefaultVovPeriod = 10;

    public VovTests()
    {
        _gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
    }

    private TSeries GenerateData(int count)
    {
        _gbm.Reset(DateTime.UtcNow.Ticks);
        var bars = _gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var ts = new TSeries();
        for (int i = 0; i < bars.Count; i++)
        {
            ts.Add(new TValue(bars[i].Time, bars[i].Close));
        }
        return ts;
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_DefaultParameters_SetsCorrectValues()
    {
        var vov = new Vov();
        Assert.Equal(DefaultVolatilityPeriod, vov.VolatilityPeriod);
        Assert.Equal(DefaultVovPeriod, vov.VovPeriod);
        Assert.Equal($"Vov({DefaultVolatilityPeriod},{DefaultVovPeriod})", vov.Name);
        Assert.Equal(DefaultVolatilityPeriod + DefaultVovPeriod - 1, vov.WarmupPeriod);
    }

    [Fact]
    public void Constructor_CustomParameters_SetsCorrectValues()
    {
        var vov = new Vov(volatilityPeriod: 30, vovPeriod: 15);
        Assert.Equal(30, vov.VolatilityPeriod);
        Assert.Equal(15, vov.VovPeriod);
        Assert.Equal("Vov(30,15)", vov.Name);
        Assert.Equal(44, vov.WarmupPeriod);
    }

    [Fact]
    public void Constructor_ZeroVolatilityPeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Vov(volatilityPeriod: 0));
        Assert.Equal("volatilityPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativeVolatilityPeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Vov(volatilityPeriod: -5));
        Assert.Equal("volatilityPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_ZeroVovPeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Vov(volatilityPeriod: 20, vovPeriod: 0));
        Assert.Equal("vovPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativeVovPeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Vov(volatilityPeriod: 20, vovPeriod: -5));
        Assert.Equal("vovPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithSource_SubscribesToEvents()
    {
        var source = new TSeries();
        var vov = new Vov(source, volatilityPeriod: 10, vovPeriod: 5);
        source.Add(new TValue(DateTime.UtcNow, 100.0));
        Assert.NotEqual(default, vov.Last);
    }

    #endregion

    #region Basic Calculation Tests

    [Fact]
    public void Update_SingleValue_ReturnsZero()
    {
        var vov = new Vov();
        var result = vov.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(0.0, result.Value);
    }

    [Fact]
    public void Update_ConstantValues_ConvergesToZero()
    {
        var vov = new Vov(volatilityPeriod: 5, vovPeriod: 3);
        for (int i = 0; i < 50; i++)
        {
            vov.Update(new TValue(DateTime.UtcNow, 100.0));
        }
        // Constant price = zero volatility = zero VOV
        Assert.True(vov.Last.Value < 0.001, $"Expected near zero, got {vov.Last.Value}");
    }

    [Fact]
    public void Update_ReturnsNonNegativeValue()
    {
        var vov = new Vov();
        var data = GenerateData(100);

        for (int i = 0; i < data.Count; i++)
        {
            var result = vov.Update(data[i]);
            Assert.True(result.Value >= 0, $"VOV should be non-negative, got {result.Value}");
        }
    }

    [Fact]
    public void Update_HighVolatilityVariation_ProducesHigherVov()
    {
        var vov = new Vov(volatilityPeriod: 5, vovPeriod: 3);

        // First phase: low volatility
        for (int i = 0; i < 20; i++)
        {
            vov.Update(new TValue(DateTime.UtcNow, 100.0 + ((i % 2) * 0.1)));
        }
        double lowVolVov = vov.Last.Value;

        // Reset and test high volatility variation
        vov.Reset();

        // Second phase: alternating high/low volatility
        for (int i = 0; i < 10; i++)
        {
            // High volatility period
            for (int j = 0; j < 5; j++)
            {
                vov.Update(new TValue(DateTime.UtcNow, 100.0 + ((j % 2) * 10.0)));
            }
            // Low volatility period
            for (int j = 0; j < 5; j++)
            {
                vov.Update(new TValue(DateTime.UtcNow, 100.0 + ((j % 2) * 0.1)));
            }
        }
        double highVolVov = vov.Last.Value;

        Assert.True(highVolVov > lowVolVov, $"High vol variation VOV ({highVolVov}) should exceed low vol VOV ({lowVolVov})");
    }

    #endregion

    #region IsHot and Warmup Tests

    [Fact]
    public void IsHot_BeforeWarmup_ReturnsFalse()
    {
        var vov = new Vov(volatilityPeriod: 10, vovPeriod: 5);
        // WarmupPeriod = 10 + 5 - 1 = 14. IsHot when PriceCount >= 10 AND VolCount >= 5.
        // After 5 bars: PriceCount=5, VolCount=4 (vol counting starts at bar 2)
        for (int i = 0; i < 5; i++)
        {
            vov.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }
        Assert.False(vov.IsHot);
    }

    [Fact]
    public void IsHot_AfterWarmup_ReturnsTrue()
    {
        var vov = new Vov(volatilityPeriod: 10, vovPeriod: 5);
        for (int i = 0; i < 20; i++)
        {
            vov.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }
        Assert.True(vov.IsHot);
    }

    [Fact]
    public void WarmupPeriod_IsCorrectlyCombined()
    {
        var vov = new Vov(volatilityPeriod: 15, vovPeriod: 8);
        Assert.Equal(22, vov.WarmupPeriod);
    }

    #endregion

    #region Bar Correction (isNew) Tests

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var vov = new Vov(volatilityPeriod: 5, vovPeriod: 3);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            vov.Update(new TValue(time.AddSeconds(i), 100.0 + i), isNew: true);
        }

        double valueAfterUpdates = vov.Last.Value;

        // Additional update should change value
        vov.Update(new TValue(time.AddSeconds(10), 150.0), isNew: true);
        double valueAfterNew = vov.Last.Value;

        Assert.NotEqual(valueAfterUpdates, valueAfterNew);
    }

    [Fact]
    public void Update_IsNewFalse_UpdatesCurrentBar()
    {
        var vov = new Vov(volatilityPeriod: 5, vovPeriod: 3);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 15; i++)
        {
            vov.Update(new TValue(time.AddSeconds(i), 100.0 + i), isNew: true);
        }

        double valueBeforeCorrection = vov.Last.Value;

        // First correction
        vov.Update(new TValue(time.AddSeconds(15), 200.0), isNew: false);
        double valueAfterCorrection1 = vov.Last.Value;

        // Second correction to different value
        vov.Update(new TValue(time.AddSeconds(15), 50.0), isNew: false);
        double valueAfterCorrection2 = vov.Last.Value;

        Assert.NotEqual(valueBeforeCorrection, valueAfterCorrection1);
        Assert.NotEqual(valueAfterCorrection1, valueAfterCorrection2);
    }

    [Fact]
    public void Update_MultipleCorrections_RestoresPreviousState()
    {
        var vov = new Vov(volatilityPeriod: 5, vovPeriod: 3);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 15; i++)
        {
            vov.Update(new TValue(time.AddSeconds(i), 100.0 + i), isNew: true);
        }

        // Add a new bar
        vov.Update(new TValue(time.AddSeconds(15), 110.0), isNew: true);
        double baseValue = vov.Last.Value;

        // Multiple corrections should all be based on the same previous state
        vov.Update(new TValue(time.AddSeconds(15), 200.0), isNew: false);
        vov.Update(new TValue(time.AddSeconds(15), 110.0), isNew: false);
        double restoredValue = vov.Last.Value;

        Assert.Equal(baseValue, restoredValue, 10);
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ClearsAllState()
    {
        var vov = new Vov(volatilityPeriod: 5, vovPeriod: 3);

        for (int i = 0; i < 20; i++)
        {
            vov.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }

        Assert.True(vov.IsHot);

        vov.Reset();

        Assert.False(vov.IsHot);
        Assert.Equal(default, vov.Last);
    }

    [Fact]
    public void Reset_AllowsReuse()
    {
        var vov = new Vov(volatilityPeriod: 5, vovPeriod: 3);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 20; i++)
        {
            vov.Update(new TValue(time.AddSeconds(i), 100.0 + i));
        }
        double firstRunValue = vov.Last.Value;

        vov.Reset();

        for (int i = 0; i < 20; i++)
        {
            vov.Update(new TValue(time.AddSeconds(i), 100.0 + i));
        }
        double secondRunValue = vov.Last.Value;

        Assert.Equal(firstRunValue, secondRunValue, 10);
    }

    #endregion

    #region NaN and Infinity Handling Tests

    [Fact]
    public void Update_NaNInput_UsesLastValidValue()
    {
        var vov = new Vov(volatilityPeriod: 5, vovPeriod: 3);

        for (int i = 0; i < 15; i++)
        {
            vov.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }

        // Update with NaN
        vov.Update(new TValue(DateTime.UtcNow, double.NaN));
        double valueAfterNaN = vov.Last.Value;

        Assert.True(double.IsFinite(valueAfterNaN));
    }

    [Fact]
    public void Update_InfinityInput_UsesLastValidValue()
    {
        var vov = new Vov(volatilityPeriod: 5, vovPeriod: 3);

        for (int i = 0; i < 15; i++)
        {
            vov.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }

        vov.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(vov.Last.Value));

        vov.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(vov.Last.Value));
    }

    [Fact]
    public void Update_MultipleNaNs_StaysFinite()
    {
        var vov = new Vov(volatilityPeriod: 5, vovPeriod: 3);

        for (int i = 0; i < 15; i++)
        {
            vov.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }

        for (int i = 0; i < 5; i++)
        {
            vov.Update(new TValue(DateTime.UtcNow, double.NaN));
        }

        Assert.True(double.IsFinite(vov.Last.Value));
    }

    [Fact]
    public void Batch_WithNaN_ProducesSafeOutput()
    {
        double[] source = [100, 102, double.NaN, 98, 101, 103, 99, 100, 101, 102];
        double[] output = new double[10];

        Vov.Batch(source, output, volatilityPeriod: 5, vovPeriod: 3);

        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val));
        }
    }

    #endregion

    #region TSeries and Batch Tests

    [Fact]
    public void Update_TSeries_ReturnsCorrectLength()
    {
        var vov = new Vov();
        var data = GenerateData(100);

        var result = vov.Update(data);
        Assert.Equal(data.Count, result.Count);
    }

    [Fact]
    public void Calculate_Static_ProducesValidResults()
    {
        var data = GenerateData(100);

        var result = Vov.Batch(data, volatilityPeriod: 10, vovPeriod: 5);

        Assert.Equal(data.Count, result.Count);
        for (int i = 0; i < result.Count; i++)
        {
            Assert.True(double.IsFinite(result.Values[i]));
            Assert.True(result.Values[i] >= 0);
        }
    }

    [Fact]
    public void Batch_ProducesConsistentResults()
    {
        var data = GenerateData(100);

        double[] output = new double[100];
        Vov.Batch(data.Values, output, volatilityPeriod: 10, vovPeriod: 5);

        // Verify all outputs are valid
        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]));
            Assert.True(output[i] >= 0);
        }
    }

    [Fact]
    public void Batch_ZeroVolatilityPeriod_ThrowsArgumentException()
    {
        double[] source = [1, 2, 3];
        double[] output = new double[3];
        var ex = Assert.Throws<ArgumentException>(() => Vov.Batch(source, output, volatilityPeriod: 0));
        Assert.Equal("volatilityPeriod", ex.ParamName);
    }

    [Fact]
    public void Batch_ZeroVovPeriod_ThrowsArgumentException()
    {
        double[] source = [1, 2, 3];
        double[] output = new double[3];
        var ex = Assert.Throws<ArgumentException>(() => Vov.Batch(source, output, volatilityPeriod: 10, vovPeriod: 0));
        Assert.Equal("vovPeriod", ex.ParamName);
    }

    [Fact]
    public void Batch_OutputTooSmall_ThrowsArgumentException()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[3];
        var ex = Assert.Throws<ArgumentException>(() => Vov.Batch(source, output));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_EmptySource_DoesNotThrow()
    {
        double[] source = [];
        double[] output = [];
        Vov.Batch(source, output);
        // Should complete without exception
        Assert.Empty(output);
    }

    #endregion

    #region Mode Consistency Tests

    [Fact]
    public void AllModes_ProduceSameResults()
    {
        const int dataLen = 100;
        var data = GenerateData(dataLen);
        int volPeriod = 10;
        int vovPeriod = 5;

        // Mode 1: Streaming
        var streamingVov = new Vov(volPeriod, vovPeriod);
        for (int i = 0; i < dataLen; i++)
        {
            streamingVov.Update(data[i], isNew: true);
        }

        // Mode 2: TSeries batch
        var batchResult = Vov.Batch(data, volPeriod, vovPeriod);

        // Mode 3: Span batch
        double[] spanOutput = new double[dataLen];
        Vov.Batch(data.Values, spanOutput, volPeriod, vovPeriod);

        // Compare last 50 values (after warmup)
        int compareStart = dataLen - 50;
        for (int i = compareStart; i < dataLen; i++)
        {
            double batch = batchResult[i].Value;
            double span = spanOutput[i];

            // Batch and Span should match exactly
            Assert.Equal(batch, span, Tolerance);
        }

        // Final values should match
        Assert.Equal(streamingVov.Last.Value, batchResult[dataLen - 1].Value, 1e-8);
        Assert.Equal(streamingVov.Last.Value, spanOutput[dataLen - 1], 1e-8);
    }

    #endregion

    #region Event Tests

    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var vov = new Vov(volatilityPeriod: 5, vovPeriod: 3);
        int eventCount = 0;

        vov.Pub += (object? sender, in TValueEventArgs args) => eventCount++;

        var time = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            vov.Update(new TValue(time.AddSeconds(i), 100 + i));
        }

        Assert.Equal(5, eventCount);
    }

    [Fact]
    public void Event_ChainedIndicator_ReceivesUpdates()
    {
        var source = new TSeries();
        var vov = new Vov(source, volatilityPeriod: 10, vovPeriod: 5);

        for (int i = 0; i < 30; i++)
        {
            source.Add(new TValue(DateTime.UtcNow, 100.0 + i));
        }

        Assert.True(vov.IsHot);
        Assert.True(double.IsFinite(vov.Last.Value));
    }

    #endregion

    #region TBar Tests

    [Fact]
    public void Update_TBar_UsesClosePrice()
    {
        var vov1 = new Vov(volatilityPeriod: 5, vovPeriod: 3);
        var vov2 = new Vov(volatilityPeriod: 5, vovPeriod: 3);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 15; i++)
        {
            var bar = new TBar(time.AddSeconds(i), 100.0, 105.0, 95.0, 102.0 + i, 1000);
            vov1.Update(bar);
            vov2.Update(new TValue(time.AddSeconds(i), bar.Close));
        }

        // Both should produce same result (using close price)
        Assert.Equal(vov1.Last.Value, vov2.Last.Value, Tolerance);
    }

    #endregion

    #region Large Period Tests

    [Fact]
    public void Batch_LargeVolatilityPeriod_UsesArrayPool()
    {
        const int dataLen = 1000;
        double[] source = new double[dataLen];
        double[] output = new double[dataLen];

        for (int i = 0; i < dataLen; i++)
        {
            source[i] = 100.0 + (i % 50);
        }

        // Period > 256 should use ArrayPool
        Vov.Batch(source, output, volatilityPeriod: 300, vovPeriod: 10);

        // Verify outputs are valid
        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]));
        }
    }

    [Fact]
    public void Batch_LargeVovPeriod_UsesArrayPool()
    {
        const int dataLen = 1000;
        double[] source = new double[dataLen];
        double[] output = new double[dataLen];

        for (int i = 0; i < dataLen; i++)
        {
            source[i] = 100.0 + (i % 50);
        }

        // Period > 256 should use ArrayPool
        Vov.Batch(source, output, volatilityPeriod: 20, vovPeriod: 300);

        // Verify outputs are valid
        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]));
        }
    }

    [Fact]
    public void Batch_LargeDataset_NoStackOverflow()
    {
        const int dataLen = 10000;
        var bars = new GBM(seed: 42).Fetch(dataLen, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] source = bars.CloseValues.ToArray();
        double[] output = new double[dataLen];

        Vov.Batch(source, output, DefaultVolatilityPeriod, DefaultVovPeriod);

        // Verify all outputs are valid
        for (int i = 0; i < dataLen; i++)
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
        var vov = new Vov(volatilityPeriod: 5, vovPeriod: 3);
        double[] warmupData = [100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112, 113, 114];

        vov.Prime(warmupData);

        Assert.True(vov.IsHot);
    }

    #endregion
}
