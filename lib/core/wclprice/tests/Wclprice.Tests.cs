// Wclprice Unit Tests

using Xunit;

namespace QuanTAlib.Tests;

public class WclpriceTests
{
    private readonly GBM _gbm;
    private const double Tolerance = 1e-10;

    public WclpriceTests()
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
        var indicator = new Wclprice();
        Assert.Equal("Wclprice", indicator.Name);
        Assert.Equal(1, indicator.WarmupPeriod);
    }

    [Fact]
    public void Constructor_WithSource_SubscribesToEvents()
    {
        var source = new TSeries();
        var indicator = new Wclprice(source);
        source.Add(new TValue(DateTime.UtcNow, 100.0));
        Assert.NotEqual(default, indicator.Last);
    }

    #endregion

    #region Basic Calculation Tests

    [Fact]
    public void Update_Bar_ReturnsHLCC4()
    {
        var indicator = new Wclprice();
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000);
        var result = indicator.Update(bar);
        // (110 + 90 + 2*105) / 4 = 410/4 = 102.5
        Assert.Equal(102.5, result.Value, Tolerance);
    }

    [Fact]
    public void Update_Bar_MatchesTBarHLCC4()
    {
        var indicator = new Wclprice();
        var bar = new TBar(DateTime.UtcNow, 50, 60, 40, 55, 500);
        var result = indicator.Update(bar);
        Assert.Equal(bar.HLCC4, result.Value, Tolerance);
    }

    [Fact]
    public void Update_TValue_ReturnsIdentity()
    {
        var indicator = new Wclprice();
        var result = indicator.Update(new TValue(DateTime.UtcNow, 42.0));
        Assert.Equal(42.0, result.Value, Tolerance);
    }

    [Fact]
    public void Update_Bar_UsesFMA()
    {
        // Verify FMA computation: close*0.5 + (high+low)*0.25
        var indicator = new Wclprice();
        var bar = new TBar(DateTime.UtcNow, 100, 200, 50, 150, 1000);
        var result = indicator.Update(bar);
        // FMA: 150*0.5 + (200+50)*0.25 = 75 + 62.5 = 137.5
        // Standard: (200+50+2*150)/4 = 550/4 = 137.5
        Assert.Equal(137.5, result.Value, Tolerance);
    }

    #endregion

    #region State and Bar Correction Tests

    [Fact]
    public void IsHot_AfterFirstBar_ReturnsTrue()
    {
        var indicator = new Wclprice();
        Assert.False(indicator.IsHot);
        indicator.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000));
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void Update_IsNewFalse_RestoresPreviousState()
    {
        var indicator = new Wclprice();
        var time = DateTime.UtcNow;

        indicator.Update(new TBar(time, 100, 110, 90, 105, 1000), isNew: true);
        indicator.Update(new TBar(time.AddMinutes(1), 105, 115, 95, 110, 1000), isNew: true);

        var corrected = indicator.Update(new TBar(time.AddMinutes(1), 106, 120, 80, 111, 1000), isNew: false);
        // FMA: 111*0.5 + (120+80)*0.25 = 55.5 + 50 = 105.5
        double expected = Math.FusedMultiplyAdd(111.0, 0.5, (120.0 + 80.0) * 0.25);
        Assert.Equal(expected, corrected.Value, Tolerance);
    }

    [Fact]
    public void Update_MultipleIsNewFalse_ProducesIdempotentResults()
    {
        var indicator = new Wclprice();
        var time = DateTime.UtcNow;

        indicator.Update(new TBar(time, 100, 110, 90, 105, 1000), isNew: true);

        var bar = new TBar(time.AddMinutes(1), 105, 115, 95, 110, 1000);
        var result1 = indicator.Update(bar, isNew: false);
        var result2 = indicator.Update(bar, isNew: false);
        var result3 = indicator.Update(bar, isNew: false);

        Assert.Equal(result1.Value, result2.Value, Tolerance);
        Assert.Equal(result2.Value, result3.Value, Tolerance);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var indicator = new Wclprice();
        indicator.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000));
        Assert.True(indicator.IsHot);

        indicator.Reset();
        Assert.False(indicator.IsHot);
        Assert.Equal(default, indicator.Last);
    }

    #endregion

    #region NaN/Infinity Robustness Tests

    [Fact]
    public void Update_NaN_UsesLastValidValue()
    {
        var indicator = new Wclprice();
        var time = DateTime.UtcNow;

        indicator.Update(new TBar(time, 100, 110, 90, 105, 1000), isNew: true);
        double validResult = indicator.Last.Value;

        var nanBar = new TBar(time.AddMinutes(1), double.NaN, double.NaN, double.NaN, double.NaN, 1000);
        var result = indicator.Update(nanBar, isNew: true);
        Assert.True(double.IsFinite(result.Value));
        Assert.Equal(validResult, result.Value, Tolerance);
    }

    #endregion

    #region Consistency Tests (All Modes)

    [Fact]
    public void AllModes_ProduceConsistentResults()
    {
        var bars = GenerateBars(100);

        // Mode 1: Streaming
        var streaming = new Wclprice();
        double[] streamingResults = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            streamingResults[i] = streaming.Update(bars[i], isNew: true).Value;
        }

        // Mode 2: Batch (TBarSeries)
        var batchResult = Wclprice.Batch(bars);

        // Mode 3: Span batch
        double[] spanOutput = new double[bars.Count];
        Wclprice.Batch(bars.HighValues, bars.LowValues, bars.CloseValues, spanOutput);

        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(streamingResults[i], batchResult.Values[i], Tolerance);
            Assert.Equal(streamingResults[i], spanOutput[i], Tolerance);
        }
    }

    [Fact]
    public void AllBars_MatchTBarHLCC4()
    {
        var bars = GenerateBars(50);
        var indicator = new Wclprice();

        for (int i = 0; i < bars.Count; i++)
        {
            var result = indicator.Update(bars[i], isNew: true);
            Assert.Equal(bars[i].HLCC4, result.Value, Tolerance);
        }
    }

    #endregion

    #region Batch Validation Tests

    [Fact]
    public void Batch_MismatchedLengths_ThrowsArgumentException()
    {
        double[] high = new double[10];
        double[] low = new double[5]; // mismatched
        double[] close = new double[10];
        double[] output = new double[10];

        var ex = Assert.Throws<ArgumentException>(() => Wclprice.Batch(high, low, close, output));
        Assert.Equal("low", ex.ParamName);
    }

    [Fact]
    public void Batch_OutputTooShort_ThrowsArgumentException()
    {
        double[] high = new double[10];
        double[] low = new double[10];
        double[] close = new double[10];
        double[] output = new double[5]; // too short

        var ex = Assert.Throws<ArgumentException>(() => Wclprice.Batch(high, low, close, output));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_EmptyInput_NoOutput()
    {
        var bars = new TBarSeries();
        var result = Wclprice.Batch(bars);
        Assert.Empty(result);
    }

    [Fact]
    public void Batch_LargeDataset_NoStackOverflow()
    {
        var bars = GenerateBars(10_000);
        double[] output = new double[bars.Count];
        Wclprice.Batch(bars.HighValues, bars.LowValues, bars.CloseValues, output);
        Assert.True(double.IsFinite(output[^1]));
    }

    #endregion

    #region Event Chaining Tests

    [Fact]
    public void Pub_EventFires_OnUpdate()
    {
        var indicator = new Wclprice();
        bool fired = false;
        indicator.Pub += (object? sender, in TValueEventArgs args) => fired = true;

        indicator.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000));
        Assert.True(fired);
    }

    [Fact]
    public void Calculate_Static_ReturnsResultsAndIndicator()
    {
        var bars = GenerateBars(50);
        var (results, ind) = Wclprice.Calculate(bars);
        Assert.Equal(bars.Count, results.Count);
        Assert.True(ind.IsHot);
    }

    #endregion
}
