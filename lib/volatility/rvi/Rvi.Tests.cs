// RVI Unit Tests

using Xunit;

namespace QuanTAlib.Tests;

public class RviTests
{
    private readonly GBM _gbm;
    private const int DefaultStdevLength = 10;
    private const int DefaultRmaLength = 14;
    private const double Tolerance = 1e-10;

    public RviTests()
    {
        _gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
    }

    private TBarSeries GenerateBars(int count)
    {
        _gbm.Reset(DateTime.UtcNow.Ticks);
        return _gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    private static TSeries GeneratePriceSeries(int count, int seed = 42)
    {
        var gbm = new GBM(seed: seed);
        var bars = gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var t = new List<long>(count);
        var v = new List<double>(count);
        for (int i = 0; i < count; i++)
        {
            t.Add(bars[i].Time);
            v.Add(bars[i].Close);
        }
        return new TSeries(t, v);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_DefaultParameters_SetsCorrectValues()
    {
        var rvi = new Rvi();
        Assert.Equal(DefaultStdevLength, rvi.StdevLength);
        Assert.Equal(DefaultRmaLength, rvi.RmaLength);
        Assert.Equal($"Rvi({DefaultStdevLength},{DefaultRmaLength})", rvi.Name);
    }

    [Fact]
    public void Constructor_CustomParameters_SetsCorrectValues()
    {
        var rvi = new Rvi(stdevLength: 20, rmaLength: 21);
        Assert.Equal(20, rvi.StdevLength);
        Assert.Equal(21, rvi.RmaLength);
        Assert.Equal("Rvi(20,21)", rvi.Name);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-5)]
    public void Constructor_InvalidStdevLength_ThrowsArgumentException(int stdevLength)
    {
        var ex = Assert.Throws<ArgumentException>(() => new Rvi(stdevLength: stdevLength));
        Assert.Equal("stdevLength", ex.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_InvalidRmaLength_ThrowsArgumentException(int rmaLength)
    {
        var ex = Assert.Throws<ArgumentException>(() => new Rvi(stdevLength: 10, rmaLength: rmaLength));
        Assert.Equal("rmaLength", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithSource_SubscribesToEvents()
    {
        var source = new TSeries();
        var rvi = new Rvi(source, stdevLength: 10, rmaLength: 14);
        source.Add(new TValue(DateTime.UtcNow, 100.0));
        Assert.NotEqual(default, rvi.Last);
    }

    #endregion

    #region Basic Calculation Tests

    [Fact]
    public void Update_FirstValue_ReturnsNeutral()
    {
        var rvi = new Rvi();
        var result = rvi.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(50.0, result.Value, Tolerance);
    }

    [Fact]
    public void Update_ReturnsValidTValue()
    {
        var rvi = new Rvi();
        var time = DateTime.UtcNow;
        rvi.Update(new TValue(time.AddSeconds(-1), 100.0));
        var result = rvi.Update(new TValue(time, 101.0));
        Assert.Equal(time.Ticks, result.Time);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_WithTBar_UsesClosePrice()
    {
        var rvi = new Rvi();
        var bar = new TBar(DateTime.UtcNow, 98, 102, 97, 100, 1000);
        var result = rvi.Update(bar);
        Assert.Equal(50.0, result.Value, Tolerance); // First value
    }

    [Fact]
    public void Update_OutputRangeIsZeroToHundred()
    {
        var rvi = new Rvi(stdevLength: 5, rmaLength: 5);
        var bars = GenerateBars(500);

        for (int i = 0; i < 500; i++)
        {
            var result = rvi.Update(new TValue(bars[i].Time, bars[i].Close));
            Assert.InRange(result.Value, 0.0, 100.0);
        }
    }

    [Fact]
    public void Update_ConsistentUpTrend_ProducesHighValues()
    {
        var rvi = new Rvi(stdevLength: 5, rmaLength: 10);

        // Consistent up moves
        double price = 100.0;
        for (int i = 0; i < 50; i++)
        {
            price += 1.0; // Always up
            rvi.Update(new TValue(DateTime.UtcNow.AddSeconds(i), price));
        }

        // Should be above 50 (bullish)
        Assert.True(rvi.Last.Value > 50.0);
    }

    [Fact]
    public void Update_ConsistentDownTrend_ProducesLowValues()
    {
        var rvi = new Rvi(stdevLength: 5, rmaLength: 10);

        // Consistent down moves
        double price = 200.0;
        for (int i = 0; i < 50; i++)
        {
            price -= 1.0; // Always down
            rvi.Update(new TValue(DateTime.UtcNow.AddSeconds(i), price));
        }

        // Should be below 50 (bearish)
        Assert.True(rvi.Last.Value < 50.0);
    }

    [Fact]
    public void Update_NoChange_StaysNeutral()
    {
        var rvi = new Rvi(stdevLength: 5, rmaLength: 10);

        // Constant price - no direction
        for (int i = 0; i < 50; i++)
        {
            rvi.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
        }

        // Should approach neutral (50)
        Assert.InRange(rvi.Last.Value, 40.0, 60.0);
    }

    #endregion

    #region IsHot and WarmupPeriod Tests

    [Fact]
    public void IsHot_BeforeWarmup_ReturnsFalse()
    {
        var rvi = new Rvi(stdevLength: 10, rmaLength: 14);

        for (int i = 0; i < 9; i++)
        {
            rvi.Update(new TValue(DateTime.UtcNow, 100.0 + i));
            Assert.False(rvi.IsHot);
        }
    }

    [Fact]
    public void IsHot_AfterWarmup_ReturnsTrue()
    {
        var rvi = new Rvi(stdevLength: 10, rmaLength: 14);

        for (int i = 0; i < 10; i++)
        {
            rvi.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }

        Assert.True(rvi.IsHot);
    }

    [Fact]
    public void WarmupPeriod_EqualsStdevLengthPlusRmaLength()
    {
        var rvi = new Rvi(stdevLength: 10, rmaLength: 14);
        Assert.Equal(24, rvi.WarmupPeriod);
    }

    #endregion

    #region State and Bar Correction Tests

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var rvi = new Rvi();
        var time = DateTime.UtcNow;

        rvi.Update(new TValue(time.AddSeconds(-2), 100.0), isNew: true);
        rvi.Update(new TValue(time.AddSeconds(-1), 101.0), isNew: true);
        var val1 = rvi.Update(new TValue(time, 102.0), isNew: true);

        rvi.Update(new TValue(time.AddSeconds(-1), 101.0), isNew: true);
        var val2 = rvi.Update(new TValue(time.AddSeconds(1), 102.0), isNew: true);

        // Different sequence should produce different result
        Assert.NotEqual(val1.Value, val2.Value, Tolerance);
    }

    [Fact]
    public void Update_IsNewFalse_RollsBackState()
    {
        var rvi = new Rvi();
        var time = DateTime.UtcNow;

        // Build up some history
        for (int i = 0; i < 20; i++)
        {
            rvi.Update(new TValue(time.AddSeconds(i), 100.0 + i * 0.1), isNew: true);
        }

        _ = rvi.Last; // Capture state before update

        // New bar
        var result1 = rvi.Update(new TValue(time.AddSeconds(20), 105.0), isNew: true);

        // Update same bar with different value - should rollback
        var result2 = rvi.Update(new TValue(time.AddSeconds(20), 106.0), isNew: false);

        // Different input should produce different result
        Assert.NotEqual(result1.Value, result2.Value);
    }

    [Fact]
    public void Update_IterativeCorrections_RestoreState()
    {
        var rvi = new Rvi(stdevLength: 5, rmaLength: 10);
        var time = DateTime.UtcNow;

        // Build history
        for (int i = 0; i < 30; i++)
        {
            rvi.Update(new TValue(time.AddSeconds(i), 100.0 + i * 0.5), isNew: true);
        }

        // Start a new bar
        var newBarValue = rvi.Update(new TValue(time.AddSeconds(30), 120.0), isNew: true);

        // Multiple corrections
        _ = rvi.Update(new TValue(time.AddSeconds(30), 121.0), isNew: false);
        _ = rvi.Update(new TValue(time.AddSeconds(30), 122.0), isNew: false);
        var correction3 = rvi.Update(new TValue(time.AddSeconds(30), 120.0), isNew: false);

        // Going back to original value should restore original result
        Assert.Equal(newBarValue.Value, correction3.Value, Tolerance);
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ClearsState()
    {
        var rvi = new Rvi();

        for (int i = 0; i < 50; i++)
        {
            rvi.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }

        Assert.True(rvi.IsHot);

        rvi.Reset();

        Assert.False(rvi.IsHot);
        Assert.Equal(default, rvi.Last);
    }

    [Fact]
    public void Reset_AllowsReuseOfIndicator()
    {
        var rvi = new Rvi();

        // First run
        for (int i = 0; i < 30; i++)
        {
            rvi.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }
        var firstResult = rvi.Last;

        rvi.Reset();

        // Second run with same data
        for (int i = 0; i < 30; i++)
        {
            rvi.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }
        var secondResult = rvi.Last;

        Assert.Equal(firstResult.Value, secondResult.Value, Tolerance);
    }

    #endregion

    #region NaN and Infinity Handling Tests

    [Fact]
    public void Update_NaNInput_UsesLastValidValue()
    {
        var rvi = new Rvi();

        for (int i = 0; i < 20; i++)
        {
            rvi.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }
        var validValue = rvi.Last;

        var nanResult = rvi.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.Equal(validValue.Value, nanResult.Value, Tolerance);
    }

    [Fact]
    public void Update_InfinityInput_UsesLastValidValue()
    {
        var rvi = new Rvi();

        for (int i = 0; i < 20; i++)
        {
            rvi.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }
        var validValue = rvi.Last;

        var infResult = rvi.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));

        Assert.Equal(validValue.Value, infResult.Value, Tolerance);
    }

    [Fact]
    public void Update_NegativeInfinityInput_UsesLastValidValue()
    {
        var rvi = new Rvi();

        for (int i = 0; i < 20; i++)
        {
            rvi.Update(new TValue(DateTime.UtcNow, 100.0 + i));
        }
        var validValue = rvi.Last;

        var negInfResult = rvi.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));

        Assert.Equal(validValue.Value, negInfResult.Value, Tolerance);
    }

    [Fact]
    public void Batch_WithNaN_ProducesSafeOutput()
    {
        double[] prices = [100.0, 101.0, double.NaN, 103.0, 104.0, 105.0, 106.0, 107.0, 108.0, 109.0, 110.0];
        double[] output = new double[prices.Length];

        Rvi.Batch(prices, output, stdevLength: 5, rmaLength: 5);

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
        const int dataLen = 200;
        var bars = GenerateBars(dataLen);

        var prices = new double[dataLen];
        var times = new long[dataLen];

        for (int i = 0; i < dataLen; i++)
        {
            prices[i] = bars[i].Close;
            times[i] = bars[i].Time;
        }

        // Mode 1: Streaming
        var rvi1 = new Rvi(stdevLength: 10, rmaLength: 14);
        for (int i = 0; i < dataLen; i++)
        {
            rvi1.Update(new TValue(times[i], prices[i]), isNew: true);
        }

        // Mode 2: Batch via TSeries
        var tSeries = new TSeries(new List<long>(times), new List<double>(prices));
        var batchResult = Rvi.Batch(tSeries, stdevLength: 10, rmaLength: 14);

        // Mode 3: Span-based
        double[] spanOutput = new double[dataLen];
        Rvi.Batch(prices, spanOutput, stdevLength: 10, rmaLength: 14);

        // Mode 4: Event-driven
        var sourceSeries = new TSeries();
        var rviEvent = new Rvi(sourceSeries, stdevLength: 10, rmaLength: 14);
        for (int i = 0; i < dataLen; i++)
        {
            sourceSeries.Add(new TValue(times[i], prices[i]));
        }

        // Compare last 100 values
        int compareStart = dataLen - 100;
        for (int i = compareStart; i < dataLen; i++)
        {
            double batch = batchResult[i].Value;
            double span = spanOutput[i];

            // Batch and Span should match exactly
            Assert.Equal(batch, span, Tolerance);
        }

        // Final values should match
        Assert.Equal(rvi1.Last.Value, batchResult[dataLen - 1].Value, 1e-8);
        Assert.Equal(rvi1.Last.Value, spanOutput[dataLen - 1], 1e-8);
        Assert.Equal(rvi1.Last.Value, rviEvent.Last.Value, 1e-8);
    }

    #endregion

    #region Span API Tests

    [Fact]
    public void Batch_ValidatesOutputLength()
    {
        double[] prices = [100.0, 101.0, 102.0, 103.0, 104.0];
        double[] output = new double[3]; // Too short

        var ex = Assert.Throws<ArgumentException>(() => Rvi.Batch(prices, output));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_ValidatesStdevLength()
    {
        double[] prices = [100.0, 101.0, 102.0];
        double[] output = new double[3];

        var ex = Assert.Throws<ArgumentException>(() => Rvi.Batch(prices, output, stdevLength: 1));
        Assert.Equal("stdevLength", ex.ParamName);
    }

    [Fact]
    public void Batch_ValidatesRmaLength()
    {
        double[] prices = [100.0, 101.0, 102.0];
        double[] output = new double[3];

        var ex = Assert.Throws<ArgumentException>(() => Rvi.Batch(prices, output, stdevLength: 2, rmaLength: 0));
        Assert.Equal("rmaLength", ex.ParamName);
    }

    [Fact]
    public void Batch_EmptyInput_ProducesNoOutput()
    {
        double[] prices = [];
        double[] output = [];

        Rvi.Batch(prices, output);
        // Should not throw, and output remains empty
        Assert.Empty(output);
    }

    [Fact]
    public void Batch_MatchesStreamingMode()
    {
        const int dataLen = 100;
        var bars = GenerateBars(dataLen);

        var prices = new double[dataLen];
        for (int i = 0; i < dataLen; i++)
        {
            prices[i] = bars[i].Close;
        }

        // Streaming
        var rvi = new Rvi(stdevLength: 10, rmaLength: 14);
        for (int i = 0; i < dataLen; i++)
        {
            rvi.Update(new TValue(bars[i].Time, prices[i]));
        }

        // Batch
        double[] batchOutput = new double[dataLen];
        Rvi.Batch(prices, batchOutput, stdevLength: 10, rmaLength: 14);

        // Compare final value
        Assert.Equal(rvi.Last.Value, batchOutput[dataLen - 1], 1e-8);
    }

    [Fact]
    public void Batch_LargeDataset_NoStackOverflow()
    {
        const int dataLen = 10000;
        var bars = new GBM(seed: 42).Fetch(dataLen, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] prices = bars.CloseValues.ToArray();
        double[] output = new double[dataLen];

        Rvi.Batch(prices, output, stdevLength: 10, rmaLength: 14);

        // Verify all outputs are valid
        for (int i = 0; i < dataLen; i++)
        {
            Assert.True(double.IsFinite(output[i]));
            Assert.InRange(output[i], 0.0, 100.0);
        }
    }

    #endregion

    #region Chainability Tests

    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var rvi = new Rvi();
        int eventCount = 0;

        rvi.Pub += (object? sender, in TValueEventArgs args) => eventCount++;

        rvi.Update(new TValue(DateTime.UtcNow, 100.0));
        rvi.Update(new TValue(DateTime.UtcNow, 101.0));
        rvi.Update(new TValue(DateTime.UtcNow, 102.0));

        Assert.Equal(3, eventCount);
    }

    [Fact]
    public void EventChaining_Works()
    {
        var sourceSeries = new TSeries();
        var rvi = new Rvi(sourceSeries, stdevLength: 5, rmaLength: 10);

        var results = new List<double>();
        rvi.Pub += (object? sender, in TValueEventArgs args) => results.Add(args.Value.Value);

        for (int i = 0; i < 30; i++)
        {
            sourceSeries.Add(new TValue(DateTime.UtcNow, 100.0 + i));
        }

        Assert.Equal(30, results.Count);
        Assert.All(results.ToArray(), r => Assert.InRange(r, 0.0, 100.0));
    }

    #endregion

    #region TSeries and TBarSeries Tests

    [Fact]
    public void Update_TSeries_ReturnsCorrectLength()
    {
        var rvi = new Rvi();
        var source = new TSeries();

        for (int i = 0; i < 50; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
        }

        var result = rvi.Update(source);

        Assert.Equal(50, result.Count);
    }

    [Fact]
    public void Update_TBarSeries_ReturnsCorrectLength()
    {
        var rvi = new Rvi();
        var source = new TBarSeries();

        for (int i = 0; i < 50; i++)
        {
            var time = DateTime.UtcNow.AddSeconds(i);
            double price = 100.0 + i;
            source.Add(new TBar(time, price - 1, price + 1, price - 2, price, 1000));
        }

        var result = rvi.Update(source);

        Assert.Equal(50, result.Count);
    }

    [Fact]
    public void Calculate_Static_TSeries_Works()
    {
        var source = new TSeries();
        for (int i = 0; i < 50; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i * 0.5));
        }

        var result = Rvi.Batch(source, stdevLength: 10, rmaLength: 14);

        Assert.Equal(50, result.Count);
        // Allow small floating-point tolerance beyond [0,100]
        Assert.All(result.Values.ToArray(), v => Assert.InRange(v, -1e-9, 100.0 + 1e-9));
    }

    [Fact]
    public void Calculate_Static_TBarSeries_Works()
    {
        var source = new TBarSeries();
        for (int i = 0; i < 50; i++)
        {
            var time = DateTime.UtcNow.AddSeconds(i);
            double price = 100.0 + i * 0.5;
            source.Add(new TBar(time, price - 1, price + 1, price - 2, price, 1000));
        }

        var result = Rvi.Batch(source, stdevLength: 10, rmaLength: 14);

        Assert.Equal(50, result.Count);
    }

    #endregion

    #region Prime Tests

    [Fact]
    public void Prime_SetsInitialState()
    {
        var rvi = new Rvi(stdevLength: 5, rmaLength: 10);
        double[] warmupData = [100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112, 113, 114];

        rvi.Prime(warmupData);

        Assert.True(rvi.IsHot);
        Assert.True(rvi.Last.Value > 0);
    }

    #endregion
}