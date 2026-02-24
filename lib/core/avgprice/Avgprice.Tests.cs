// Avgprice Unit Tests

using Xunit;

namespace QuanTAlib.Tests;

public class AvgpriceTests
{
    private readonly GBM _gbm;
    private const double Tolerance = 1e-10;

    public AvgpriceTests()
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
        var indicator = new Avgprice();
        Assert.Equal("Avgprice", indicator.Name);
        Assert.Equal(1, indicator.WarmupPeriod);
    }

    [Fact]
    public void Constructor_WithSource_SubscribesToEvents()
    {
        var source = new TSeries();
        var indicator = new Avgprice(source);
        source.Add(new TValue(DateTime.UtcNow, 100.0));
        Assert.NotEqual(default, indicator.Last);
    }

    #endregion

    #region Basic Calculation Tests

    [Fact]
    public void Update_Bar_ReturnsOHLC4()
    {
        var indicator = new Avgprice();
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000);
        var result = indicator.Update(bar);
        // (100 + 110 + 90 + 105) / 4 = 101.25
        Assert.Equal(101.25, result.Value, Tolerance);
    }

    [Fact]
    public void Update_Bar_MatchesTBarOHLC4()
    {
        var indicator = new Avgprice();
        var bar = new TBar(DateTime.UtcNow, 50, 60, 40, 55, 500);
        var result = indicator.Update(bar);
        Assert.Equal(bar.OHLC4, result.Value, Tolerance);
    }

    [Fact]
    public void Update_TValue_ReturnsIdentity()
    {
        var indicator = new Avgprice();
        var result = indicator.Update(new TValue(DateTime.UtcNow, 42.0));
        Assert.Equal(42.0, result.Value, Tolerance);
    }

    #endregion

    #region State and Bar Correction Tests

    [Fact]
    public void IsHot_AfterFirstBar_ReturnsTrue()
    {
        var indicator = new Avgprice();
        Assert.False(indicator.IsHot);
        indicator.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000));
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void Update_IsNewFalse_RestoresPreviousState()
    {
        var indicator = new Avgprice();
        var time = DateTime.UtcNow;

        // First bar
        indicator.Update(new TBar(time, 100, 110, 90, 105, 1000), isNew: true);
        _ = indicator.Last.Value;

        // Second bar (new)
        indicator.Update(new TBar(time.AddMinutes(1), 105, 115, 95, 110, 1000), isNew: true);

        // Correction on second bar — should produce same result as a fresh update
        var corrected = indicator.Update(new TBar(time.AddMinutes(1), 106, 116, 96, 111, 1000), isNew: false);
        double expected = (106 + 116 + 96 + 111) * 0.25;
        Assert.Equal(expected, corrected.Value, Tolerance);
    }

    [Fact]
    public void Update_MultipleIsNewFalse_ProducesIdempotentResults()
    {
        var indicator = new Avgprice();
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
        var indicator = new Avgprice();
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
        var indicator = new Avgprice();
        var time = DateTime.UtcNow;

        // Valid bar first
        indicator.Update(new TBar(time, 100, 110, 90, 105, 1000), isNew: true);
        double validResult = indicator.Last.Value;

        // NaN bar — should substitute last valid values
        var nanBar = new TBar(time.AddMinutes(1), double.NaN, double.NaN, double.NaN, double.NaN, 1000);
        var result = indicator.Update(nanBar, isNew: true);
        Assert.True(double.IsFinite(result.Value));
        Assert.Equal(validResult, result.Value, Tolerance);
    }

    [Fact]
    public void Update_Infinity_UsesLastValidValue()
    {
        var indicator = new Avgprice();
        var time = DateTime.UtcNow;

        indicator.Update(new TBar(time, 100, 110, 90, 105, 1000), isNew: true);
        _ = indicator.Last.Value;

        var infBar = new TBar(time.AddMinutes(1), double.PositiveInfinity, double.NegativeInfinity, double.NaN, double.PositiveInfinity, 1000);
        var result = indicator.Update(infBar, isNew: true);
        Assert.True(double.IsFinite(result.Value));
    }

    #endregion

    #region Consistency Tests (All Modes)

    [Fact]
    public void AllModes_ProduceConsistentResults()
    {
        var bars = GenerateBars(100);

        // Mode 1: Streaming
        var streaming = new Avgprice();
        double[] streamingResults = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            streamingResults[i] = streaming.Update(bars[i], isNew: true).Value;
        }

        // Mode 2: Batch (TBarSeries)
        var batchResult = Avgprice.Batch(bars);

        // Mode 3: Span batch
        double[] spanOutput = new double[bars.Count];
        Avgprice.Batch(bars.OpenValues, bars.HighValues, bars.LowValues, bars.CloseValues, spanOutput);

        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(streamingResults[i], batchResult.Values[i], Tolerance);
            Assert.Equal(streamingResults[i], spanOutput[i], Tolerance);
        }
    }

    [Fact]
    public void AllBars_MatchTBarOHLC4()
    {
        var bars = GenerateBars(50);
        var indicator = new Avgprice();

        for (int i = 0; i < bars.Count; i++)
        {
            var result = indicator.Update(bars[i], isNew: true);
            Assert.Equal(bars[i].OHLC4, result.Value, Tolerance);
        }
    }

    #endregion

    #region Batch Validation Tests

    [Fact]
    public void Batch_MismatchedLengths_ThrowsArgumentException()
    {
        double[] open = new double[10];
        double[] high = new double[10];
        double[] low = new double[5]; // mismatched
        double[] close = new double[10];
        double[] output = new double[10];

        var ex = Assert.Throws<ArgumentException>(() => Avgprice.Batch(open, high, low, close, output));
        Assert.Equal("high", ex.ParamName);
    }

    [Fact]
    public void Batch_OutputTooShort_ThrowsArgumentException()
    {
        double[] open = new double[10];
        double[] high = new double[10];
        double[] low = new double[10];
        double[] close = new double[10];
        double[] output = new double[5]; // too short

        var ex = Assert.Throws<ArgumentException>(() => Avgprice.Batch(open, high, low, close, output));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_EmptyInput_NoOutput()
    {
        var bars = new TBarSeries();
        var result = Avgprice.Batch(bars);
        Assert.Empty(result);
    }

    [Fact]
    public void Batch_LargeDataset_NoStackOverflow()
    {
        var bars = GenerateBars(10_000);
        double[] output = new double[bars.Count];
        Avgprice.Batch(bars.OpenValues, bars.HighValues, bars.LowValues, bars.CloseValues, output);
        Assert.True(double.IsFinite(output[^1]));
    }

    #endregion

    #region Event Chaining Tests

    [Fact]
    public void Pub_EventFires_OnUpdate()
    {
        var indicator = new Avgprice();
        bool fired = false;
        indicator.Pub += (object? sender, in TValueEventArgs args) => fired = true;

        indicator.Update(new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000));
        Assert.True(fired);
    }

    [Fact]
    public void Calculate_Static_ReturnsResultsAndIndicator()
    {
        var bars = GenerateBars(50);
        var (results, ind) = Avgprice.Calculate(bars);
        Assert.Equal(bars.Count, results.Count);
        Assert.True(ind.IsHot);
    }

    #endregion
}
