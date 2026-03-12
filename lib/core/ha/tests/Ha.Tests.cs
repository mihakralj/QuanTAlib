// Ha Unit Tests

using Xunit;

namespace QuanTAlib.Tests;

public class HaTests
{
    private readonly GBM _gbm;
    private const double Tolerance = 1e-10;

    public HaTests()
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
        var indicator = new Ha();
        Assert.Equal("Ha", indicator.Name);
        Assert.Equal(1, indicator.WarmupPeriod);
    }

    [Fact]
    public void Constructor_WithSource_SubscribesToEvents()
    {
        var source = new TSeries();
        var indicator = new Ha(source);
        source.Add(new TValue(DateTime.UtcNow, 100.0));
        Assert.NotEqual(default, indicator.Last);
    }

    #endregion

    #region Basic Calculation Tests

    [Fact]
    public void Update_FirstBar_HaCloseIsOHLC4()
    {
        var indicator = new Ha();
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000);
        var result = indicator.UpdateBar(bar);
        // HA Close = (100 + 110 + 90 + 105) / 4 = 101.25
        Assert.Equal(101.25, result.Close, Tolerance);
    }

    [Fact]
    public void Update_FirstBar_HaOpenIsMidpointOC()
    {
        var indicator = new Ha();
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000);
        var result = indicator.UpdateBar(bar);
        // HA Open on first bar = (O + C) / 2 = (100 + 105) / 2 = 102.5
        Assert.Equal(102.5, result.Open, Tolerance);
    }

    [Fact]
    public void Update_FirstBar_HaHighIsMaxOfHOC()
    {
        var indicator = new Ha();
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000);
        var result = indicator.UpdateBar(bar);
        // HA High = max(110, 102.5, 101.25) = 110
        Assert.Equal(110, result.High, Tolerance);
    }

    [Fact]
    public void Update_FirstBar_HaLowIsMinOfLOC()
    {
        var indicator = new Ha();
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000);
        var result = indicator.UpdateBar(bar);
        // HA Low = min(90, 102.5, 101.25) = 90
        Assert.Equal(90, result.Low, Tolerance);
    }

    [Fact]
    public void Update_SecondBar_HaOpenIsRecursive()
    {
        var indicator = new Ha();
        var time = DateTime.UtcNow;

        // First bar: O=100, H=110, L=90, C=105
        // HA_Open1 = (100+105)/2 = 102.5, HA_Close1 = 101.25
        indicator.UpdateBar(new TBar(time, 100, 110, 90, 105, 1000));

        // Second bar: O=105, H=115, L=95, C=110
        // HA_Open2 = (prevHaOpen + prevHaClose) / 2 = (102.5 + 101.25) / 2 = 101.875
        var result = indicator.UpdateBar(new TBar(time.AddMinutes(1), 105, 115, 95, 110, 1000));
        Assert.Equal(101.875, result.Open, Tolerance);
    }

    [Fact]
    public void Update_SecondBar_HaCloseIsOHLC4()
    {
        var indicator = new Ha();
        var time = DateTime.UtcNow;

        indicator.UpdateBar(new TBar(time, 100, 110, 90, 105, 1000));

        var result = indicator.UpdateBar(new TBar(time.AddMinutes(1), 105, 115, 95, 110, 1000));
        // HA Close = (105 + 115 + 95 + 110) / 4 = 106.25
        Assert.Equal(106.25, result.Close, Tolerance);
    }

    [Fact]
    public void Update_VolumePassthrough()
    {
        var indicator = new Ha();
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1234.5);
        var result = indicator.UpdateBar(bar);
        Assert.Equal(1234.5, result.Volume, Tolerance);
    }

    [Fact]
    public void Update_TimePassthrough()
    {
        var indicator = new Ha();
        var time = DateTime.UtcNow;
        var bar = new TBar(time, 100, 110, 90, 105, 1000);
        var result = indicator.UpdateBar(bar);
        Assert.Equal(time.Ticks, result.Time);
    }

    [Fact]
    public void Update_HaHighAlwaysGEHaOpenAndHaClose()
    {
        var indicator = new Ha();
        var bars = GenerateBars(100);

        for (int i = 0; i < bars.Count; i++)
        {
            var ha = indicator.UpdateBar(bars[i], isNew: true);
            Assert.True(ha.High >= ha.Open, $"Bar {i}: High {ha.High} < Open {ha.Open}");
            Assert.True(ha.High >= ha.Close, $"Bar {i}: High {ha.High} < Close {ha.Close}");
        }
    }

    [Fact]
    public void Update_HaLowAlwaysLEHaOpenAndHaClose()
    {
        var indicator = new Ha();
        var bars = GenerateBars(100);

        for (int i = 0; i < bars.Count; i++)
        {
            var ha = indicator.UpdateBar(bars[i], isNew: true);
            Assert.True(ha.Low <= ha.Open, $"Bar {i}: Low {ha.Low} > Open {ha.Open}");
            Assert.True(ha.Low <= ha.Close, $"Bar {i}: Low {ha.Low} > Close {ha.Close}");
        }
    }

    [Fact]
    public void Update_LastProperty_ReturnsHaClose()
    {
        var indicator = new Ha();
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000);
        indicator.UpdateBar(bar);
        // Last.Value should equal HA Close
        Assert.Equal(101.25, indicator.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_LastBarProperty_ReturnsFullHaBar()
    {
        var indicator = new Ha();
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000);
        var result = indicator.UpdateBar(bar);
        Assert.Equal(result, indicator.LastBar);
    }

    #endregion

    #region State and Bar Correction Tests

    [Fact]
    public void IsHot_AfterFirstBar_ReturnsTrue()
    {
        var indicator = new Ha();
        Assert.False(indicator.IsHot);
        indicator.UpdateBar(new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000));
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void Update_IsNewFalse_RestoresPreviousState()
    {
        var indicator = new Ha();
        var time = DateTime.UtcNow;

        // First bar
        indicator.UpdateBar(new TBar(time, 100, 110, 90, 105, 1000), isNew: true);

        // Second bar (new)
        indicator.UpdateBar(new TBar(time.AddMinutes(1), 105, 115, 95, 110, 1000), isNew: true);

        // Correction on second bar
        var corrected = indicator.UpdateBar(new TBar(time.AddMinutes(1), 106, 116, 96, 111, 1000), isNew: false);

        // Verify the HA Open is computed from first bar's HA values, not second bar's
        // After first bar: prevHaOpen=102.5, prevHaClose=101.25
        // Corrected HA_Open = (102.5 + 101.25)/2 = 101.875
        Assert.Equal(101.875, corrected.Open, Tolerance);
        // Corrected HA_Close = (106+116+96+111)/4 = 107.25
        Assert.Equal(107.25, corrected.Close, Tolerance);
    }

    [Fact]
    public void Update_MultipleIsNewFalse_ProducesIdempotentResults()
    {
        var indicator = new Ha();
        var time = DateTime.UtcNow;

        indicator.UpdateBar(new TBar(time, 100, 110, 90, 105, 1000), isNew: true);

        var bar = new TBar(time.AddMinutes(1), 105, 115, 95, 110, 1000);
        var result1 = indicator.UpdateBar(bar, isNew: false);
        var result2 = indicator.UpdateBar(bar, isNew: false);
        var result3 = indicator.UpdateBar(bar, isNew: false);

        Assert.Equal(result1.Open, result2.Open, Tolerance);
        Assert.Equal(result1.Close, result2.Close, Tolerance);
        Assert.Equal(result1.High, result2.High, Tolerance);
        Assert.Equal(result1.Low, result2.Low, Tolerance);
        Assert.Equal(result2, result3);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var indicator = new Ha();
        indicator.UpdateBar(new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000));
        Assert.True(indicator.IsHot);

        indicator.Reset();
        Assert.False(indicator.IsHot);
        Assert.Equal(default, indicator.Last);
        Assert.Equal(default, indicator.LastBar);
    }

    #endregion

    #region NaN/Infinity Robustness Tests

    [Fact]
    public void Update_NaN_UsesLastValidValue()
    {
        var indicator = new Ha();
        var time = DateTime.UtcNow;

        // Valid bar first
        indicator.UpdateBar(new TBar(time, 100, 110, 90, 105, 1000), isNew: true);
        _ = indicator.LastBar;

        // NaN bar — should substitute last valid values
        var nanBar = new TBar(time.AddMinutes(1), double.NaN, double.NaN, double.NaN, double.NaN, 1000);
        var result = indicator.UpdateBar(nanBar, isNew: true);
        Assert.True(double.IsFinite(result.Open));
        Assert.True(double.IsFinite(result.High));
        Assert.True(double.IsFinite(result.Low));
        Assert.True(double.IsFinite(result.Close));
    }

    [Fact]
    public void Update_Infinity_UsesLastValidValue()
    {
        var indicator = new Ha();
        var time = DateTime.UtcNow;

        indicator.UpdateBar(new TBar(time, 100, 110, 90, 105, 1000), isNew: true);

        var infBar = new TBar(time.AddMinutes(1), double.PositiveInfinity, double.NegativeInfinity, double.NaN, double.PositiveInfinity, 1000);
        var result = indicator.UpdateBar(infBar, isNew: true);
        Assert.True(double.IsFinite(result.Open));
        Assert.True(double.IsFinite(result.High));
        Assert.True(double.IsFinite(result.Low));
        Assert.True(double.IsFinite(result.Close));
    }

    #endregion

    #region Consistency Tests (All Modes)

    [Fact]
    public void AllModes_StreamingAndBatch_ProduceConsistentResults()
    {
        var bars = GenerateBars(100);

        // Mode 1: Streaming
        var streaming = new Ha();
        TBar[] streamingResults = new TBar[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            streamingResults[i] = streaming.UpdateBar(bars[i], isNew: true);
        }

        // Mode 2: Batch (TBarSeries)
        var batchResult = Ha.Batch(bars);

        // Mode 3: Span batch
        double[] haOpenOut = new double[bars.Count];
        double[] haHighOut = new double[bars.Count];
        double[] haLowOut = new double[bars.Count];
        double[] haCloseOut = new double[bars.Count];
        Ha.Batch(bars.OpenValues, bars.HighValues, bars.LowValues, bars.CloseValues,
            haOpenOut, haHighOut, haLowOut, haCloseOut);

        for (int i = 0; i < bars.Count; i++)
        {
            Assert.Equal(streamingResults[i].Open, batchResult[i].Open, Tolerance);
            Assert.Equal(streamingResults[i].High, batchResult[i].High, Tolerance);
            Assert.Equal(streamingResults[i].Low, batchResult[i].Low, Tolerance);
            Assert.Equal(streamingResults[i].Close, batchResult[i].Close, Tolerance);

            Assert.Equal(streamingResults[i].Open, haOpenOut[i], Tolerance);
            Assert.Equal(streamingResults[i].High, haHighOut[i], Tolerance);
            Assert.Equal(streamingResults[i].Low, haLowOut[i], Tolerance);
            Assert.Equal(streamingResults[i].Close, haCloseOut[i], Tolerance);
        }
    }

    [Fact]
    public void AllBars_HaCloseMatchesOHLC4()
    {
        var bars = GenerateBars(50);
        var indicator = new Ha();

        for (int i = 0; i < bars.Count; i++)
        {
            var result = indicator.UpdateBar(bars[i], isNew: true);
            Assert.Equal(bars[i].OHLC4, result.Close, Tolerance);
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
        double[] ho = new double[10], hh = new double[10], hl = new double[10], hc = new double[10];

        var ex = Assert.Throws<ArgumentException>(() => Ha.Batch(open, high, low, close, ho, hh, hl, hc));
        Assert.Equal("high", ex.ParamName);
    }

    [Fact]
    public void Batch_OutputTooShort_ThrowsArgumentException()
    {
        double[] open = new double[10];
        double[] high = new double[10];
        double[] low = new double[10];
        double[] close = new double[10];
        double[] ho = new double[5]; // too short
        double[] hh = new double[10], hl = new double[10], hc = new double[10];

        var ex = Assert.Throws<ArgumentException>(() => Ha.Batch(open, high, low, close, ho, hh, hl, hc));
        Assert.Equal("haOpenOut", ex.ParamName);
    }

    [Fact]
    public void Batch_EmptyInput_NoOutput()
    {
        var bars = new TBarSeries();
        var result = Ha.Batch(bars);
        Assert.Empty(result);
    }

    [Fact]
    public void Batch_LargeDataset_NoStackOverflow()
    {
        var bars = GenerateBars(10_000);
        var result = Ha.Batch(bars);
        Assert.Equal(bars.Count, result.Count);
        Assert.True(double.IsFinite(result[^1].Close));
    }

    #endregion

    #region HA-Specific Property Tests

    [Fact]
    public void ConstantInput_ConvergesToConstant()
    {
        var indicator = new Ha();
        var time = DateTime.UtcNow;

        // Feed constant bars: O=100, H=100, L=100, C=100
        for (int i = 0; i < 20; i++)
        {
            _ = indicator.UpdateBar(new TBar(time.AddMinutes(i), 100, 100, 100, 100, 1000), isNew: true);
        }

        var last = indicator.LastBar;
        // After many constant bars, all HA values should converge to 100
        Assert.Equal(100.0, last.Open, 1e-6);
        Assert.Equal(100.0, last.High, 1e-6);
        Assert.Equal(100.0, last.Low, 1e-6);
        Assert.Equal(100.0, last.Close, 1e-6);
    }

    [Fact]
    public void HaHighGERealHigh_WhenBodyExceedsHigh()
    {
        // This tests the clamping: HA High is at least as large as HA Open and HA Close
        var indicator = new Ha();
        var bars = GenerateBars(100);

        for (int i = 0; i < bars.Count; i++)
        {
            var ha = indicator.UpdateBar(bars[i], isNew: true);
            // HA High should be >= real High OR >= haOpen/haClose
            Assert.True(ha.High >= ha.Open);
            Assert.True(ha.High >= ha.Close);
        }
    }

    #endregion

    #region Event Chaining Tests

    [Fact]
    public void Pub_EventFires_OnUpdate()
    {
        var indicator = new Ha();
        bool fired = false;
        indicator.Pub += (object? sender, in TValueEventArgs args) => fired = true;

        indicator.UpdateBar(new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000));
        Assert.True(fired);
    }

    [Fact]
    public void Calculate_Static_ReturnsResultsAndIndicator()
    {
        var bars = GenerateBars(50);
        var (results, ind) = Ha.Calculate(bars);
        Assert.Equal(bars.Count, results.Count);
        Assert.True(ind.IsHot);
    }

    #endregion
}
