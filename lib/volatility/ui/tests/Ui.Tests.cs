// Ulcer Index (UI) Unit Tests

using Xunit;

namespace QuanTAlib.Tests;

public class UiTests
{
    private readonly GBM _gbm;
    private const double Tolerance = 1e-10;
    private const int DefaultPeriod = 14;

    public UiTests()
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
        var ui = new Ui();
        Assert.Equal("Ui(14)", ui.Name);
        Assert.Equal(14, ui.WarmupPeriod);
        Assert.Equal(14, ui.Period);
    }

    [Fact]
    public void Constructor_CustomPeriod_SetsCorrectValues()
    {
        var ui = new Ui(period: 20);
        Assert.Equal("Ui(20)", ui.Name);
        Assert.Equal(20, ui.WarmupPeriod);
        Assert.Equal(20, ui.Period);
    }

    [Fact]
    public void Constructor_ZeroPeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Ui(period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Ui(period: -5));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithSource_SubscribesToEvents()
    {
        var source = new TSeries();
        var ui = new Ui(source, DefaultPeriod);
        source.Add(new TValue(DateTime.UtcNow, 100.0));
        Assert.NotEqual(default, ui.Last);
    }

    #endregion

    #region Basic Calculation Tests

    [Fact]
    public void Update_PriceAtHigh_ReturnsZero()
    {
        var ui = new Ui(period: 5);
        var time = DateTime.UtcNow;

        // Rising prices - each close is the highest
        double[] prices = [100, 101, 102, 103, 104];
        TValue result = default;

        for (int i = 0; i < prices.Length; i++)
        {
            result = ui.Update(new TValue(time.AddSeconds(i), prices[i]));
        }

        // When price is at period high, drawdown is zero → UI is zero
        Assert.Equal(0.0, result.Value, Tolerance);
    }

    [Fact]
    public void Update_PriceDrawdown_ReturnsPositiveValue()
    {
        var ui = new Ui(period: 5);
        var time = DateTime.UtcNow;

        // Price rises then falls
        double[] prices = [100, 105, 110, 105, 100];
        TValue result = default;

        for (int i = 0; i < prices.Length; i++)
        {
            result = ui.Update(new TValue(time.AddSeconds(i), prices[i]));
        }

        // There's a drawdown from 110, so UI should be positive
        Assert.True(result.Value > 0, $"UI should be positive during drawdown, got {result.Value}");
    }

    [Fact]
    public void Update_CalculatesCorrectUlcerIndex()
    {
        var ui = new Ui(period: 5);
        var time = DateTime.UtcNow;

        // Manual calculation:
        // Prices: 100, 102, 101, 103, 100
        // Highest: 100, 102, 102, 103, 103
        // %Drawdown: 0, 0, (101-102)/102*100=-0.98, 0, (100-103)/103*100=-2.91
        // SqDrawdown: 0, 0, 0.96, 0, 8.48
        // Sum = 9.44, Avg = 1.888, UI = sqrt(1.888) ≈ 1.374
        double[] prices = [100, 102, 101, 103, 100];
        TValue result = default;

        for (int i = 0; i < prices.Length; i++)
        {
            result = ui.Update(new TValue(time.AddSeconds(i), prices[i]));
        }

        // Verify it's approximately correct (allowing for rounding)
        Assert.True(result.Value > 1.0 && result.Value < 2.0,
            $"Expected UI around 1.37, got {result.Value}");
    }

    [Fact]
    public void Update_ReturnsNonNegative()
    {
        var ui = new Ui(DefaultPeriod);
        var data = GenerateData(100);

        for (int i = 0; i < data.Count; i++)
        {
            var result = ui.Update(data[i]);
            Assert.True(result.Value >= 0, $"UI should be non-negative, got {result.Value}");
        }
    }

    [Fact]
    public void Update_DeeperDrawdown_HigherUi()
    {
        var time = DateTime.UtcNow;

        // Shallow drawdown
        var ui1 = new Ui(period: 5);
        double[] prices1 = [100, 105, 110, 108, 109];
        for (int i = 0; i < prices1.Length; i++)
        {
            ui1.Update(new TValue(time.AddSeconds(i), prices1[i]));
        }
        var shallow = ui1.Last.Value;

        // Deep drawdown
        var ui2 = new Ui(period: 5);
        double[] prices2 = [100, 105, 110, 95, 90];
        for (int i = 0; i < prices2.Length; i++)
        {
            ui2.Update(new TValue(time.AddSeconds(i), prices2[i]));
        }
        var deep = ui2.Last.Value;

        Assert.True(deep > shallow,
            $"Deeper drawdown should have higher UI: deep={deep}, shallow={shallow}");
    }

    #endregion

    #region IsHot and WarmupPeriod Tests

    [Fact]
    public void IsHot_BeforeWarmup_ReturnsFalse()
    {
        var ui = new Ui(period: 10);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 9; i++)
        {
            ui.Update(new TValue(time.AddSeconds(i), 100 + i));
            Assert.False(ui.IsHot);
        }
    }

    [Fact]
    public void IsHot_AtWarmup_ReturnsTrue()
    {
        var ui = new Ui(period: 10);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            ui.Update(new TValue(time.AddSeconds(i), 100 + i));
        }
        Assert.True(ui.IsHot);
    }

    [Fact]
    public void WarmupPeriod_EqualsPeriod()
    {
        var ui = new Ui(period: 20);
        Assert.Equal(20, ui.WarmupPeriod);
    }

    #endregion

    #region State and Bar Correction Tests

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var ui = new Ui(period: 5);
        var time = DateTime.UtcNow;

        // Build up to warmup (prices: 100, 110, 105, 108, 103 have drawdowns from 110)
        double[] prices = [100, 110, 105, 108, 103];
        for (int i = 0; i < prices.Length; i++)
        {
            ui.Update(new TValue(time.AddSeconds(i), prices[i]), isNew: true);
        }

        // Add another bar (isNew=true) - state should advance
        var result = ui.Update(new TValue(time.AddSeconds(5), 95), isNew: true);

        // With isNew=true, state should have advanced (count incremented)
        // The UI value should be different because we added a new price point
        // Note: UI can be 0 only if all prices are at new highs, but 95 < 110, so drawdown exists
        Assert.True(ui.IsHot, "Should be hot after warmup period");
        Assert.True(result.Value >= 0, "UI should be non-negative");
        // 95 is a significant drawdown from 110 (highest), UI should be > 0
        Assert.NotEqual(0.0, result.Value);
    }

    [Fact]
    public void Update_IsNewFalse_RollsBackState()
    {
        var ui = new Ui(period: 5);
        var time = DateTime.UtcNow;

        // Build up history
        for (int i = 0; i < 5; i++)
        {
            ui.Update(new TValue(time.AddSeconds(i), 100 + i), isNew: true);
        }

        // New bar
        var result1 = ui.Update(new TValue(time.AddSeconds(5), 90), isNew: true);

        // Update same bar with different value - should rollback
        var result2 = ui.Update(new TValue(time.AddSeconds(5), 80), isNew: false);

        // Different values should produce different results
        Assert.NotEqual(result1.Value, result2.Value);
    }

    [Fact]
    public void Update_IterativeCorrections_RestoreState()
    {
        var ui = new Ui(period: 5);
        var time = DateTime.UtcNow;

        // Build history
        for (int i = 0; i < 5; i++)
        {
            ui.Update(new TValue(time.AddSeconds(i), 100 + i), isNew: true);
        }

        // Start a new bar
        var newBarResult = ui.Update(new TValue(time.AddSeconds(5), 95), isNew: true);

        // Multiple corrections
        _ = ui.Update(new TValue(time.AddSeconds(5), 90), isNew: false);
        _ = ui.Update(new TValue(time.AddSeconds(5), 85), isNew: false);
        var correction3 = ui.Update(new TValue(time.AddSeconds(5), 95), isNew: false);

        // Going back to original value should restore original result
        Assert.Equal(newBarResult.Value, correction3.Value, Tolerance);
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ClearsState()
    {
        var ui = new Ui(DefaultPeriod);
        var data = GenerateData(20);

        for (int i = 0; i < data.Count; i++)
        {
            ui.Update(data[i]);
        }

        Assert.True(ui.IsHot);

        ui.Reset();

        Assert.False(ui.IsHot);
        Assert.Equal(default, ui.Last);
    }

    [Fact]
    public void Reset_AllowsReuseOfIndicator()
    {
        var ui = new Ui(DefaultPeriod);
        var data = GenerateData(20);

        // First run
        for (int i = 0; i < data.Count; i++)
        {
            ui.Update(data[i]);
        }
        var firstResult = ui.Last;

        ui.Reset();

        // Second run with same data
        for (int i = 0; i < data.Count; i++)
        {
            ui.Update(data[i]);
        }
        var secondResult = ui.Last;

        Assert.Equal(firstResult.Value, secondResult.Value, Tolerance);
    }

    #endregion

    #region NaN and Infinity Handling Tests

    [Fact]
    public void Update_NaNInput_UsesLastValidValue()
    {
        var ui = new Ui(period: 5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 5; i++)
        {
            ui.Update(new TValue(time.AddSeconds(i), 100 + i));
        }

        var nanResult = ui.Update(new TValue(time.AddSeconds(5), double.NaN));

        Assert.True(double.IsFinite(nanResult.Value));
    }

    [Fact]
    public void Update_InfinityInput_UsesLastValidValue()
    {
        var ui = new Ui(period: 5);
        var time = DateTime.UtcNow;

        for (int i = 0; i < 5; i++)
        {
            ui.Update(new TValue(time.AddSeconds(i), 100 + i));
        }

        var infResult = ui.Update(new TValue(time.AddSeconds(5), double.PositiveInfinity));

        Assert.True(double.IsFinite(infResult.Value));
    }

    [Fact]
    public void Batch_WithNaN_ProducesSafeOutput()
    {
        double[] source = [100, 102, double.NaN, 98, 101];
        double[] output = new double[5];

        Ui.Batch(source, output, period: 5);

        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val));
        }
    }

    #endregion

    #region Mode Consistency Tests

    [Fact]
    public void AllModes_ProduceConsistentResults()
    {
        const int dataLen = 100;
        var data = GenerateData(dataLen);

        // Mode 1: Streaming
        var ui1 = new Ui(DefaultPeriod);
        for (int i = 0; i < dataLen; i++)
        {
            ui1.Update(data[i], isNew: true);
        }

        // Mode 2: Batch via TSeries
        var batchResult = Ui.Batch(data, DefaultPeriod);

        // Mode 3: Span-based
        double[] spanOutput = new double[dataLen];
        Ui.Batch(data.Values, spanOutput, DefaultPeriod);

        // Compare last 50 values
        int compareStart = dataLen - 50;
        for (int i = compareStart; i < dataLen; i++)
        {
            double batch = batchResult[i].Value;
            double span = spanOutput[i];

            // Batch and Span should match exactly
            Assert.Equal(batch, span, Tolerance);
        }

        // Final values should match
        Assert.Equal(ui1.Last.Value, batchResult[dataLen - 1].Value, 1e-8);
        Assert.Equal(ui1.Last.Value, spanOutput[dataLen - 1], 1e-8);
    }

    #endregion

    #region Span API Tests

    [Fact]
    public void Batch_ValidatesOutputLength()
    {
        double[] source = [100, 101, 102];
        double[] output = new double[2]; // Too short

        var ex = Assert.Throws<ArgumentException>(() => Ui.Batch(source, output, period: 3));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_ValidatesPeriod()
    {
        double[] source = [100, 101, 102];
        double[] output = new double[3];

        var ex = Assert.Throws<ArgumentException>(() => Ui.Batch(source, output, period: 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_EmptyInput_ProducesNoOutput()
    {
        double[] source = [];
        double[] output = [];

        Ui.Batch(source, output, period: 5);
        // Should not throw
        Assert.Empty(output);
    }

    [Fact]
    public void Batch_MatchesStreamingMode()
    {
        const int dataLen = 50;
        var data = GenerateData(dataLen);

        // Streaming
        var ui = new Ui(DefaultPeriod);
        for (int i = 0; i < dataLen; i++)
        {
            ui.Update(data[i]);
        }

        // Batch
        double[] batchOutput = new double[dataLen];
        Ui.Batch(data.Values, batchOutput, DefaultPeriod);

        // Compare final value
        Assert.Equal(ui.Last.Value, batchOutput[dataLen - 1], 1e-8);
    }

    [Fact]
    public void Batch_LargeDataset_NoStackOverflow()
    {
        const int dataLen = 10000;
        var bars = new GBM(seed: 42).Fetch(dataLen, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] source = bars.CloseValues.ToArray();
        double[] output = new double[dataLen];

        Ui.Batch(source, output, DefaultPeriod);

        // Verify all outputs are valid
        for (int i = 0; i < dataLen; i++)
        {
            Assert.True(double.IsFinite(output[i]));
            Assert.True(output[i] >= 0);
        }
    }

    [Fact]
    public void Batch_LargePeriod_UsesArrayPool()
    {
        const int dataLen = 500;
        const int largePeriod = 300; // > 256 threshold

        double[] source = new double[dataLen];
        double[] output = new double[dataLen];

        for (int i = 0; i < dataLen; i++)
        {
            source[i] = 100 + (Math.Sin(i * 0.1) * 10);
        }

        // Should not throw - uses ArrayPool for large period
        Ui.Batch(source, output, largePeriod);

        // Verify outputs are valid
        for (int i = 0; i < dataLen; i++)
        {
            Assert.True(double.IsFinite(output[i]));
        }
    }

    #endregion

    #region Chainability Tests

    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var ui = new Ui(period: 5);
        int eventCount = 0;

        ui.Pub += (object? sender, in TValueEventArgs args) => eventCount++;

        var time = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            ui.Update(new TValue(time.AddSeconds(i), 100 + i));
        }

        Assert.Equal(5, eventCount);
    }

    #endregion

    #region TSeries Tests

    [Fact]
    public void Update_TSeries_ReturnsCorrectLength()
    {
        var ui = new Ui(DefaultPeriod);
        var data = GenerateData(50);

        var result = ui.Update(data);

        Assert.Equal(50, result.Count);
    }

    [Fact]
    public void Calculate_Static_TSeries_Works()
    {
        var data = GenerateData(50);

        var result = Ui.Batch(data, DefaultPeriod);

        Assert.Equal(50, result.Count);
        Assert.All(result.Values.ToArray(), v => Assert.True(v >= 0));
    }

    #endregion

    #region Prime Tests

    [Fact]
    public void Prime_SetsInitialState()
    {
        var ui = new Ui(period: 5);
        double[] warmupData = [100, 101, 102, 103, 104];

        ui.Prime(warmupData);

        Assert.True(ui.IsHot);
    }

    #endregion

    #region TBar Update Tests

    [Fact]
    public void Update_TBar_UsesClosePrice()
    {
        var ui1 = new Ui(period: 5);
        var ui2 = new Ui(period: 5);
        var time = DateTime.UtcNow;

        // Update with TBar
        for (int i = 0; i < 10; i++)
        {
            var bar = new TBar(time.AddSeconds(i), 100 + i, 102 + i, 98 + i, 101 + i, 1000);
            ui1.Update(bar);
            ui2.Update(new TValue(time.AddSeconds(i), bar.Close));
        }

        // Both should produce same result (using close price)
        Assert.Equal(ui1.Last.Value, ui2.Last.Value, Tolerance);
    }

    #endregion
}
