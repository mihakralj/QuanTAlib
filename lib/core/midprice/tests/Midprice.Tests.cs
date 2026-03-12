// Midprice Unit Tests

using Xunit;

namespace QuanTAlib.Tests;

public class MidpriceTests
{
    private readonly GBM _gbm;
    private const double Tolerance = 1e-10;

    public MidpriceTests()
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
    public void Constructor_ValidPeriod_SetsCorrectValues()
    {
        var indicator = new Midprice(14);
        Assert.Equal("Midprice(14)", indicator.Name);
        Assert.Equal(14, indicator.WarmupPeriod);
    }

    [Fact]
    public void Constructor_InvalidPeriod_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Midprice(0));
        Assert.Throws<ArgumentException>(() => new Midprice(-1));
    }

    [Fact]
    public void Constructor_Period1_IsValid()
    {
        var indicator = new Midprice(1);
        Assert.Equal("Midprice(1)", indicator.Name);
        Assert.Equal(1, indicator.WarmupPeriod);
    }

    [Fact]
    public void Constructor_WithSource_SubscribesToEvents()
    {
        var source = new TSeries();
        var indicator = new Midprice(source, 5);
        source.Add(new TValue(DateTime.UtcNow, 100.0));
        Assert.NotEqual(default, indicator.Last);
    }

    #endregion

    #region Basic Calculation Tests

    [Fact]
    public void Update_SingleBar_ReturnsMidpointOfHL()
    {
        var indicator = new Midprice(1);
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000);
        var result = indicator.Update(bar);
        // Period=1: highest high = 110, lowest low = 90
        // (110 + 90) / 2 = 100
        Assert.Equal(100.0, result.Value, Tolerance);
    }

    [Fact]
    public void Update_ThreeBars_UsesRollingWindow()
    {
        var indicator = new Midprice(3);
        var time = DateTime.UtcNow;

        indicator.Update(new TBar(time, 100, 105, 95, 102, 1000), isNew: true);
        indicator.Update(new TBar(time.AddMinutes(1), 101, 110, 93, 108, 1000), isNew: true);
        var result = indicator.Update(new TBar(time.AddMinutes(2), 106, 108, 98, 104, 1000), isNew: true);

        // Highest high over 3 bars: max(105, 110, 108) = 110
        // Lowest low over 3 bars: min(95, 93, 98) = 93
        // Midprice = (110 + 93) / 2 = 101.5
        Assert.Equal(101.5, result.Value, Tolerance);
    }

    [Fact]
    public void Update_TValue_UsesSameValueForBothChannels()
    {
        var indicator = new Midprice(3);
        var time = DateTime.UtcNow;

        indicator.Update(new TValue(time, 100), isNew: true);
        indicator.Update(new TValue(time.AddMinutes(1), 110), isNew: true);
        var result = indicator.Update(new TValue(time.AddMinutes(2), 105), isNew: true);

        // With TValue, H=L=value, so highest = 110, lowest = 100
        // Midprice = (110 + 100) / 2 = 105
        Assert.Equal(105.0, result.Value, Tolerance);
    }

    #endregion

    #region Warmup Tests

    [Fact]
    public void IsHot_BeforeWarmup_ReturnsFalse()
    {
        var indicator = new Midprice(5);
        Assert.False(indicator.IsHot);

        for (int i = 0; i < 4; i++)
        {
            indicator.Update(new TBar(DateTime.UtcNow.AddMinutes(i), 100, 110, 90, 105, 1000));
            Assert.False(indicator.IsHot);
        }
    }

    [Fact]
    public void IsHot_AtWarmup_ReturnsTrue()
    {
        var indicator = new Midprice(5);
        for (int i = 0; i < 5; i++)
        {
            indicator.Update(new TBar(DateTime.UtcNow.AddMinutes(i), 100, 110, 90, 105, 1000));
        }
        Assert.True(indicator.IsHot);
    }

    #endregion

    #region State and Bar Correction Tests

    [Fact]
    public void Update_IsNewFalse_RestoresPreviousState()
    {
        var indicator = new Midprice(3);
        var time = DateTime.UtcNow;

        indicator.Update(new TBar(time, 100, 105, 95, 102, 1000), isNew: true);
        indicator.Update(new TBar(time.AddMinutes(1), 101, 110, 93, 108, 1000), isNew: true);

        // New bar
        indicator.Update(new TBar(time.AddMinutes(2), 106, 108, 98, 104, 1000), isNew: true);

        // Correction on third bar
        var corrected = indicator.Update(new TBar(time.AddMinutes(2), 106, 120, 80, 104, 1000), isNew: false);

        // Highest high: max(105, 110, 120) = 120
        // Lowest low: min(95, 93, 80) = 80
        // Midprice = (120 + 80) / 2 = 100
        Assert.Equal(100.0, corrected.Value, Tolerance);
    }

    [Fact]
    public void Update_MultipleIsNewFalse_ProducesIdempotentResults()
    {
        var indicator = new Midprice(3);
        var time = DateTime.UtcNow;

        indicator.Update(new TBar(time, 100, 105, 95, 102, 1000), isNew: true);
        indicator.Update(new TBar(time.AddMinutes(1), 101, 110, 93, 108, 1000), isNew: true);

        var bar = new TBar(time.AddMinutes(2), 106, 108, 98, 104, 1000);
        var result1 = indicator.Update(bar, isNew: false);
        var result2 = indicator.Update(bar, isNew: false);
        var result3 = indicator.Update(bar, isNew: false);

        Assert.Equal(result1.Value, result2.Value, Tolerance);
        Assert.Equal(result2.Value, result3.Value, Tolerance);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var indicator = new Midprice(5);
        for (int i = 0; i < 10; i++)
        {
            indicator.Update(new TBar(DateTime.UtcNow.AddMinutes(i), 100, 110, 90, 105, 1000));
        }
        Assert.True(indicator.IsHot);

        indicator.Reset();
        Assert.False(indicator.IsHot);
        Assert.Equal(default, indicator.Last);
    }

    #endregion

    #region Consistency Tests (All Modes)

    [Fact]
    public void AllModes_ProduceConsistentResults()
    {
        int period = 14;
        var bars = GenerateBars(100);

        // Mode 1: Streaming
        var streaming = new Midprice(period);
        double[] streamingResults = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            streamingResults[i] = streaming.Update(bars[i], isNew: true).Value;
        }

        // Mode 2: Batch (TBarSeries)
        var batchResult = Midprice.Batch(bars, period);

        // Mode 3: Span batch
        double[] spanOutput = new double[bars.Count];
        Midprice.Batch(bars.HighValues, bars.LowValues, spanOutput, period);

        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(streamingResults[i], batchResult.Values[i], Tolerance);
            Assert.Equal(streamingResults[i], spanOutput[i], Tolerance);
        }
    }

    #endregion

    #region Batch Validation Tests

    [Fact]
    public void Batch_MismatchedLengths_ThrowsArgumentException()
    {
        double[] high = new double[10];
        double[] low = new double[5]; // mismatched
        double[] output = new double[10];

        var ex = Assert.Throws<ArgumentException>(() => Midprice.Batch(high, low, output, 5));
        Assert.Equal("low", ex.ParamName);
    }

    [Fact]
    public void Batch_OutputTooShort_ThrowsArgumentException()
    {
        double[] high = new double[10];
        double[] low = new double[10];
        double[] output = new double[5]; // too short

        var ex = Assert.Throws<ArgumentException>(() => Midprice.Batch(high, low, output, 5));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_InvalidPeriod_ThrowsArgumentException()
    {
        double[] high = new double[10];
        double[] low = new double[10];
        double[] output = new double[10];

        var ex = Assert.Throws<ArgumentException>(() => Midprice.Batch(high, low, output, 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_EmptyInput_NoOutput()
    {
        var bars = new TBarSeries();
        var result = Midprice.Batch(bars, 5);
        Assert.Empty(result);
    }

    [Fact]
    public void Batch_LargeDataset_NoStackOverflow()
    {
        var bars = GenerateBars(10_000);
        double[] output = new double[bars.Count];
        Midprice.Batch(bars.HighValues, bars.LowValues, output, 14);
        Assert.True(double.IsFinite(output[^1]));
    }

    #endregion

    #region Event Chaining Tests

    [Fact]
    public void Pub_EventFires_OnUpdate()
    {
        var indicator = new Midprice(5);
        bool fired = false;
        indicator.Pub += (object? sender, in TValueEventArgs args) => fired = true;

        indicator.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000));
        Assert.True(fired);
    }

    [Fact]
    public void Calculate_Static_ReturnsResultsAndIndicator()
    {
        var bars = GenerateBars(50);
        var (results, ind) = Midprice.Calculate(bars, 14);
        Assert.Equal(bars.Count, results.Count);
        Assert.True(ind.IsHot);
    }

    #endregion
}
