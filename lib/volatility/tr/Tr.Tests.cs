// TR Unit Tests

using Xunit;

namespace QuanTAlib.Tests;

public class TrTests
{
    private readonly GBM _gbm;
    private const double Tolerance = 1e-10;

    public TrTests()
    {
        _gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
    }

    private TBarSeries GenerateBars(int count)
    {
        _gbm.Reset(DateTime.UtcNow.Ticks);
        return _gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_DefaultParameters_SetsCorrectValues()
    {
        var tr = new Tr();
        Assert.Equal("Tr", tr.Name);
        Assert.Equal(1, tr.WarmupPeriod);
    }

    [Fact]
    public void Constructor_WithSource_SubscribesToEvents()
    {
        var source = new TSeries();
        var tr = new Tr(source);
        source.Add(new TValue(DateTime.UtcNow, 100.0));
        Assert.NotEqual(default, tr.Last);
    }

    #endregion

    #region Basic Calculation Tests

    [Fact]
    public void Update_FirstBar_ReturnsHighMinusLow()
    {
        var tr = new Tr();
        var bar = new TBar(DateTime.UtcNow, 100, 105, 98, 102, 1000);
        var result = tr.Update(bar);
        // First bar: TR = High - Low = 105 - 98 = 7
        Assert.Equal(7.0, result.Value, Tolerance);
    }

    [Fact]
    public void Update_SecondBar_CalculatesTrueRange()
    {
        var tr = new Tr();
        var time = DateTime.UtcNow;

        // First bar: Close = 100
        tr.Update(new TBar(time.AddSeconds(-1), 99, 101, 97, 100, 1000));

        // Second bar: H=105, L=98, prevClose=100
        // TR1 = 105 - 98 = 7
        // TR2 = |105 - 100| = 5
        // TR3 = |98 - 100| = 2
        // TR = max(7, 5, 2) = 7
        var result = tr.Update(new TBar(time, 100, 105, 98, 103, 1000));
        Assert.Equal(7.0, result.Value, Tolerance);
    }

    [Fact]
    public void Update_GapUp_UsesPrevClose()
    {
        var tr = new Tr();
        var time = DateTime.UtcNow;

        // First bar: Close = 100
        tr.Update(new TBar(time.AddSeconds(-1), 99, 101, 97, 100, 1000));

        // Gap up bar: H=115, L=110, prevClose=100
        // TR1 = 115 - 110 = 5
        // TR2 = |115 - 100| = 15
        // TR3 = |110 - 100| = 10
        // TR = max(5, 15, 10) = 15
        var result = tr.Update(new TBar(time, 112, 115, 110, 113, 1000));
        Assert.Equal(15.0, result.Value, Tolerance);
    }

    [Fact]
    public void Update_GapDown_UsesPrevClose()
    {
        var tr = new Tr();
        var time = DateTime.UtcNow;

        // First bar: Close = 100
        tr.Update(new TBar(time.AddSeconds(-1), 99, 101, 97, 100, 1000));

        // Gap down bar: H=90, L=85, prevClose=100
        // TR1 = 90 - 85 = 5
        // TR2 = |90 - 100| = 10
        // TR3 = |85 - 100| = 15
        // TR = max(5, 10, 15) = 15
        var result = tr.Update(new TBar(time, 88, 90, 85, 87, 1000));
        Assert.Equal(15.0, result.Value, Tolerance);
    }

    [Fact]
    public void Update_ReturnsNonNegative()
    {
        var tr = new Tr();
        var bars = GenerateBars(100);

        for (int i = 0; i < bars.Count; i++)
        {
            var result = tr.Update(bars[i]);
            Assert.True(result.Value >= 0, $"TR should be non-negative, got {result.Value}");
        }
    }

    [Fact]
    public void Update_WithTValue_ReturnsZeroRange()
    {
        var tr = new Tr();
        // When using TValue, H=L=C, so range is always 0 for first bar
        var result = tr.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(0.0, result.Value, Tolerance);
    }

    #endregion

    #region IsHot and WarmupPeriod Tests

    [Fact]
    public void IsHot_AfterFirstBar_ReturnsTrue()
    {
        var tr = new Tr();
        Assert.False(tr.IsHot);

        tr.Update(new TBar(DateTime.UtcNow, 99, 101, 97, 100, 1000));
        Assert.True(tr.IsHot);
    }

    [Fact]
    public void WarmupPeriod_EqualsOne()
    {
        var tr = new Tr();
        Assert.Equal(1, tr.WarmupPeriod);
    }

    #endregion

    #region State and Bar Correction Tests

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var tr = new Tr();
        var time = DateTime.UtcNow;

        tr.Update(new TBar(time.AddSeconds(-2), 99, 101, 97, 100, 1000), isNew: true);
        var val1 = tr.Update(new TBar(time.AddSeconds(-1), 100, 105, 98, 103, 1000), isNew: true);

        // New sequence with different previous close
        var tr2 = new Tr();
        tr2.Update(new TBar(time.AddSeconds(-2), 99, 101, 97, 95, 1000), isNew: true);
        var val2 = tr2.Update(new TBar(time.AddSeconds(-1), 100, 105, 98, 103, 1000), isNew: true);

        // Different previous close should produce different TR
        Assert.NotEqual(val1.Value, val2.Value, Tolerance);
    }

    [Fact]
    public void Update_IsNewFalse_RollsBackState()
    {
        var tr = new Tr();
        var time = DateTime.UtcNow;

        // Build up history
        for (int i = 0; i < 5; i++)
        {
            var bar = GenerateBars(1)[0];
            tr.Update(bar, isNew: true);
        }

        var lastBar = GenerateBars(1)[0];

        // New bar
        var result1 = tr.Update(new TBar(time, lastBar.Open, 110, 90, 100, 1000), isNew: true);

        // Update same bar with different values - should rollback
        var result2 = tr.Update(new TBar(time, lastBar.Open, 120, 80, 100, 1000), isNew: false);

        // Different range should produce different result
        Assert.NotEqual(result1.Value, result2.Value);
    }

    [Fact]
    public void Update_IterativeCorrections_RestoreState()
    {
        var tr = new Tr();
        var time = DateTime.UtcNow;

        // Build history
        tr.Update(new TBar(time.AddSeconds(-1), 99, 101, 97, 100, 1000), isNew: true);

        // Start a new bar
        var newBarResult = tr.Update(new TBar(time, 100, 110, 95, 105, 1000), isNew: true);

        // Multiple corrections
        _ = tr.Update(new TBar(time, 100, 115, 90, 105, 1000), isNew: false);
        _ = tr.Update(new TBar(time, 100, 120, 85, 105, 1000), isNew: false);
        var correction3 = tr.Update(new TBar(time, 100, 110, 95, 105, 1000), isNew: false);

        // Going back to original values should restore original result
        Assert.Equal(newBarResult.Value, correction3.Value, Tolerance);
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ClearsState()
    {
        var tr = new Tr();

        var bars = GenerateBars(10);
        for (int i = 0; i < bars.Count; i++)
        {
            tr.Update(bars[i]);
        }

        Assert.True(tr.IsHot);

        tr.Reset();

        Assert.False(tr.IsHot);
        Assert.Equal(default, tr.Last);
    }

    [Fact]
    public void Reset_AllowsReuseOfIndicator()
    {
        var tr = new Tr();
        var bars = GenerateBars(10);

        // First run
        for (int i = 0; i < bars.Count; i++)
        {
            tr.Update(bars[i]);
        }
        var firstResult = tr.Last;

        tr.Reset();

        // Second run with same data
        for (int i = 0; i < bars.Count; i++)
        {
            tr.Update(bars[i]);
        }
        var secondResult = tr.Last;

        Assert.Equal(firstResult.Value, secondResult.Value, Tolerance);
    }

    #endregion

    #region NaN and Infinity Handling Tests

    [Fact]
    public void Update_NaNHigh_UsesLastValidValue()
    {
        var tr = new Tr();

        tr.Update(new TBar(DateTime.UtcNow.AddSeconds(-1), 99, 101, 97, 100, 1000));
        _ = tr.Update(new TBar(DateTime.UtcNow, 100, 110, 95, 105, 1000));

        var nanResult = tr.Update(new TBar(DateTime.UtcNow.AddSeconds(1), 100, double.NaN, 90, 95, 1000));

        Assert.True(double.IsFinite(nanResult.Value));
    }

    [Fact]
    public void Update_NaNLow_UsesLastValidValue()
    {
        var tr = new Tr();

        tr.Update(new TBar(DateTime.UtcNow.AddSeconds(-1), 99, 101, 97, 100, 1000));
        tr.Update(new TBar(DateTime.UtcNow, 100, 110, 95, 105, 1000));

        var nanResult = tr.Update(new TBar(DateTime.UtcNow.AddSeconds(1), 100, 115, double.NaN, 112, 1000));

        Assert.True(double.IsFinite(nanResult.Value));
    }

    [Fact]
    public void Update_InfinityInput_UsesLastValidValue()
    {
        var tr = new Tr();

        tr.Update(new TBar(DateTime.UtcNow.AddSeconds(-1), 99, 101, 97, 100, 1000));
        tr.Update(new TBar(DateTime.UtcNow, 100, 110, 95, 105, 1000));

        var infResult = tr.Update(new TBar(DateTime.UtcNow.AddSeconds(1), 100, double.PositiveInfinity, 90, 95, 1000));

        Assert.True(double.IsFinite(infResult.Value));
    }

    [Fact]
    public void Batch_WithNaN_ProducesSafeOutput()
    {
        double[] highs = [101, 110, double.NaN, 108, 115];
        double[] lows = [97, 95, 92, 90, 100];
        double[] closes = [100, 105, 95, 102, 110];
        double[] output = new double[5];

        Tr.Batch(highs, lows, closes, output);

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
        var bars = GenerateBars(dataLen);

        // Mode 1: Streaming
        var tr1 = new Tr();
        for (int i = 0; i < dataLen; i++)
        {
            tr1.Update(bars[i], isNew: true);
        }

        // Mode 2: Batch via TBarSeries
        var batchResult = Tr.Batch(bars);

        // Mode 3: Span-based
        double[] highs = new double[dataLen];
        double[] lows = new double[dataLen];
        double[] closes = new double[dataLen];
        double[] spanOutput = new double[dataLen];

        for (int i = 0; i < dataLen; i++)
        {
            highs[i] = bars[i].High;
            lows[i] = bars[i].Low;
            closes[i] = bars[i].Close;
        }

        Tr.Batch(highs, lows, closes, spanOutput);

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
        Assert.Equal(tr1.Last.Value, batchResult[dataLen - 1].Value, 1e-8);
        Assert.Equal(tr1.Last.Value, spanOutput[dataLen - 1], 1e-8);
    }

    #endregion

    #region Span API Tests

    [Fact]
    public void Batch_ValidatesOutputLength()
    {
        double[] highs = [101, 102, 103];
        double[] lows = [99, 98, 97];
        double[] closes = [100, 101, 102];
        double[] output = new double[2]; // Too short

        var ex = Assert.Throws<ArgumentException>(() => Tr.Batch(highs, lows, closes, output));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_ValidatesInputLengths()
    {
        double[] highs = [101, 102, 103];
        double[] lows = [99, 98]; // Wrong length
        double[] closes = [100, 101, 102];
        double[] output = new double[3];

        var ex = Assert.Throws<ArgumentException>(() => Tr.Batch(highs, lows, closes, output));
        Assert.Equal("low", ex.ParamName);
    }

    [Fact]
    public void Batch_EmptyInput_ProducesNoOutput()
    {
        double[] highs = [];
        double[] lows = [];
        double[] closes = [];
        double[] output = [];

        Tr.Batch(highs, lows, closes, output);
        // Should not throw
        Assert.Empty(output);
    }

    [Fact]
    public void Batch_MatchesStreamingMode()
    {
        const int dataLen = 50;
        var bars = GenerateBars(dataLen);

        double[] highs = new double[dataLen];
        double[] lows = new double[dataLen];
        double[] closes = new double[dataLen];

        for (int i = 0; i < dataLen; i++)
        {
            highs[i] = bars[i].High;
            lows[i] = bars[i].Low;
            closes[i] = bars[i].Close;
        }

        // Streaming
        var tr = new Tr();
        for (int i = 0; i < dataLen; i++)
        {
            tr.Update(bars[i]);
        }

        // Batch
        double[] batchOutput = new double[dataLen];
        Tr.Batch(highs, lows, closes, batchOutput);

        // Compare final value
        Assert.Equal(tr.Last.Value, batchOutput[dataLen - 1], 1e-8);
    }

    [Fact]
    public void Batch_LargeDataset_NoStackOverflow()
    {
        const int dataLen = 10000;
        double[] highs = new double[dataLen];
        double[] lows = new double[dataLen];
        double[] closes = new double[dataLen];
        double[] output = new double[dataLen];

        // Fill with realistic data
        double price = 100.0;
        var rng = new Random(42);
        for (int i = 0; i < dataLen; i++)
        {
            double volatility = 0.02;
            double high = price * (1 + rng.NextDouble() * volatility);
            double low = price * (1 - rng.NextDouble() * volatility);
            double close = low + rng.NextDouble() * (high - low);

            highs[i] = high;
            lows[i] = low;
            closes[i] = close;
            price = close;
        }

        Tr.Batch(highs, lows, closes, output);

        // Verify all outputs are valid
        for (int i = 0; i < dataLen; i++)
        {
            Assert.True(double.IsFinite(output[i]));
            Assert.True(output[i] >= 0);
        }
    }

    #endregion

    #region Chainability Tests

    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var tr = new Tr();
        int eventCount = 0;

        tr.Pub += (object? sender, in TValueEventArgs args) => eventCount++;

        tr.Update(new TBar(DateTime.UtcNow.AddSeconds(0), 99, 101, 97, 100, 1000));
        tr.Update(new TBar(DateTime.UtcNow.AddSeconds(1), 100, 105, 98, 103, 1000));
        tr.Update(new TBar(DateTime.UtcNow.AddSeconds(2), 102, 108, 100, 106, 1000));

        Assert.Equal(3, eventCount);
    }

    #endregion

    #region TBarSeries Tests

    [Fact]
    public void Update_TBarSeries_ReturnsCorrectLength()
    {
        var tr = new Tr();
        var bars = GenerateBars(50);

        var result = tr.Update(bars);

        Assert.Equal(50, result.Count);
    }

    [Fact]
    public void Calculate_Static_TBarSeries_Works()
    {
        var bars = GenerateBars(50);

        var result = Tr.Batch(bars);

        Assert.Equal(50, result.Count);
        Assert.All(result.Values.ToArray(), v => Assert.True(v >= 0));
    }

    #endregion

    #region Prime Tests

    [Fact]
    public void Prime_SetsInitialState()
    {
        var tr = new Tr();
        double[] warmupData = [100, 101, 102, 103, 104];

        tr.Prime(warmupData);

        Assert.True(tr.IsHot);
    }

    #endregion
}