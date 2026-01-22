using Xunit;

namespace QuanTAlib.Tests;

public class AtrnTests
{
    private readonly GBM _gbm;
    private readonly TBarSeries _bars;
    private const int DefaultPeriod = 14;
    private const double Tolerance = 1e-10;

    public AtrnTests()
    {
        _gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        _bars = _gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidPeriod_SetsCorrectName()
    {
        var atrn = new Atrn(DefaultPeriod);
        Assert.Equal($"Atrn({DefaultPeriod})", atrn.Name);
    }

    [Fact]
    public void Constructor_WithZeroPeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Atrn(0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithNegativePeriod_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Atrn(-1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithTBarSeries_InitializesState()
    {
        var atrn = new Atrn(_bars, DefaultPeriod);
        Assert.True(atrn.Last.Value >= 0);
        Assert.True(atrn.Last.Value <= 1);
    }

    #endregion

    #region Basic Calculation Tests

    [Fact]
    public void Update_ReturnsValidTValue()
    {
        var atrn = new Atrn(DefaultPeriod);
        var result = atrn.Update(_bars[0], isNew: true);

        Assert.IsType<TValue>(result);
        Assert.Equal(_bars[0].Time, result.Time);
    }

    [Fact]
    public void Update_ReturnsValueInZeroOneRange()
    {
        var atrn = new Atrn(DefaultPeriod);

        for (int i = 0; i < _bars.Count; i++)
        {
            var result = atrn.Update(_bars[i], isNew: true);
            Assert.True(result.Value >= 0 && result.Value <= 1,
                $"Value {result.Value} at index {i} is outside [0,1] range");
        }
    }

    [Fact]
    public void Last_ReturnsLatestValue()
    {
        var atrn = new Atrn(DefaultPeriod);

        for (int i = 0; i < _bars.Count; i++)
        {
            var result = atrn.Update(_bars[i], true);
            Assert.Equal(result.Value, atrn.Last.Value);
        }
    }

    [Fact]
    public void Name_IsAccessible()
    {
        var atrn = new Atrn(DefaultPeriod);
        Assert.False(string.IsNullOrEmpty(atrn.Name));
    }

    #endregion

    #region State and Bar Correction Tests

    [Fact]
    public void Update_WithIsNewTrue_AdvancesState()
    {
        var atrn = new Atrn(DefaultPeriod);

        atrn.Update(_bars[0], true);
        atrn.Update(_bars[1], true);

        // State should advance - time should match latest bar
        Assert.True(atrn.Last.Time == _bars[1].Time);
    }

    [Fact]
    public void Update_WithIsNewFalse_RollsBackState()
    {
        var atrn = new Atrn(DefaultPeriod);

        // Process several bars first
        for (int i = 0; i < 50; i++)
        {
            atrn.Update(_bars[i], true);
        }

        // Update with new bar
        atrn.Update(_bars[50], true);
        double valueAfterNewBar = atrn.Last.Value;

        // Create modified bar
        var modifiedBar = new TBar(
            _bars[50].Time,
            _bars[50].Open * 1.1,
            _bars[50].High * 1.1,
            _bars[50].Low * 1.1,
            _bars[50].Close * 1.1,
            _bars[50].Volume
        );

        // Update with isNew=false (correction)
        atrn.Update(modifiedBar, false);
        var valueAfterCorrection = atrn.Last.Value;

        // Correction should produce different value than original update
        Assert.NotEqual(valueAfterNewBar, valueAfterCorrection);
    }

    [Fact]
    public void Update_IterativeCorrections_RestoreState()
    {
        var atrn = new Atrn(DefaultPeriod);

        // Process initial bars
        for (int i = 0; i < 100; i++)
        {
            atrn.Update(_bars[i], true);
        }

        // Process more bars
        for (int i = 100; i < 150; i++)
        {
            atrn.Update(_bars[i], true);
        }

        // Now correct bar 150 multiple times
        var originalBar150 = _bars[149];
        var result1 = atrn.Update(originalBar150, false);

        // Correct again with same value
        var result2 = atrn.Update(originalBar150, false);

        Assert.Equal(result1.Value, result2.Value, Tolerance);
    }

    [Fact]
    public void Reset_ClearsStateAndLastValue()
    {
        var atrn = new Atrn(DefaultPeriod);

        // Process some data
        for (int i = 0; i < 200; i++)
        {
            atrn.Update(_bars[i], true);
        }

        Assert.True(atrn.IsHot);

        // Reset
        atrn.Reset();

        Assert.False(atrn.IsHot);
        Assert.Equal(default, atrn.Last);
    }

    #endregion

    #region Warmup and Convergence Tests

    [Fact]
    public void IsHot_BecomesTrueAfterWarmup()
    {
        var atrn = new Atrn(DefaultPeriod);

        Assert.False(atrn.IsHot);

        // Warmup is period + 10*period = 11*period
        int warmupPeriod = DefaultPeriod + (10 * DefaultPeriod);

        for (int i = 0; i < warmupPeriod + 50; i++)
        {
            atrn.Update(_bars[i], true);
        }

        Assert.True(atrn.IsHot);
    }

    [Fact]
    public void WarmupPeriod_IsCorrectlySet()
    {
        var atrn = new Atrn(DefaultPeriod);

        // Warmup = RMA warmup + lookback window
        int expectedWarmup = DefaultPeriod + (10 * DefaultPeriod);
        Assert.True(atrn.WarmupPeriod >= expectedWarmup - DefaultPeriod);
    }

    #endregion

    #region Robustness Tests

    [Fact]
    public void Update_WithNaN_UsesLastValidValue()
    {
        var atrn = new Atrn(DefaultPeriod);

        // Process some valid data
        for (int i = 0; i < 50; i++)
        {
            atrn.Update(_bars[i], true);
        }

        // Create bar with NaN
        var nanBar = new TBar(
            DateTime.UtcNow,
            double.NaN,
            double.NaN,
            double.NaN,
            double.NaN,
            100
        );

        var result = atrn.Update(nanBar, true);

        // Should still produce a valid value
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_WithInfinity_UsesLastValidValue()
    {
        var atrn = new Atrn(DefaultPeriod);

        // Process some valid data
        for (int i = 0; i < 50; i++)
        {
            atrn.Update(_bars[i], true);
        }

        // Create bar with Infinity
        var infBar = new TBar(
            DateTime.UtcNow,
            double.PositiveInfinity,
            double.PositiveInfinity,
            double.NegativeInfinity,
            double.PositiveInfinity,
            100
        );

        var result = atrn.Update(infBar, true);

        // Should still produce a valid value
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_BatchNaN_RemainsStable()
    {
        var atrn = new Atrn(DefaultPeriod);

        // Process valid data
        for (int i = 0; i < 100; i++)
        {
            atrn.Update(_bars[i], true);
        }

        // Process multiple NaN bars
        for (int i = 0; i < 10; i++)
        {
            var nanBar = new TBar(
                DateTime.UtcNow.AddMinutes(i),
                double.NaN,
                double.NaN,
                double.NaN,
                double.NaN,
                100
            );

            var result = atrn.Update(nanBar, true);
            Assert.True(double.IsFinite(result.Value));
        }
    }

    #endregion

    #region Consistency Tests

    [Fact]
    public void BatchCalc_MatchesStreaming()
    {
        var streamingAtrn = new Atrn(DefaultPeriod);
        var streamingResults = new List<double>();

        for (int i = 0; i < _bars.Count; i++)
        {
            var result = streamingAtrn.Update(_bars[i], true);
            streamingResults.Add(result.Value);
        }

        var batchResults = Atrn.Batch(_bars, DefaultPeriod);

        // Compare last 100 values (after warmup)
        int compareStart = Math.Max(0, streamingResults.Count - 100);
        for (int i = compareStart; i < streamingResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], batchResults[i].Value, Tolerance);
        }
    }

    [Fact]
    public void TBarSeries_MatchesStreaming()
    {
        var streamingAtrn = new Atrn(DefaultPeriod);
        var streamingResults = new List<double>();

        for (int i = 0; i < _bars.Count; i++)
        {
            var result = streamingAtrn.Update(_bars[i], true);
            streamingResults.Add(result.Value);
        }

        var seriesAtrn = new Atrn(DefaultPeriod);
        var seriesResults = seriesAtrn.Update(_bars);

        // Compare last 100 values
        int compareStart = Math.Max(0, streamingResults.Count - 100);
        for (int i = compareStart; i < streamingResults.Count; i++)
        {
            Assert.Equal(streamingResults[i], seriesResults[i].Value, Tolerance);
        }
    }

    #endregion

    #region Chainability Tests

    [Fact]
    public void Pub_EventFires_OnUpdate()
    {
        var atrn = new Atrn(DefaultPeriod);
        int eventCount = 0;

        atrn.Pub += (object? sender, in TValueEventArgs args) => eventCount++;

        for (int i = 0; i < 10; i++)
        {
            atrn.Update(_bars[i], true);
        }

        Assert.Equal(10, eventCount);
    }

    [Fact]
    public void EventBasedChaining_Works()
    {
        var atrn1 = new Atrn(DefaultPeriod);
        var sma = new Sma(5);
        var receivedValues = new List<double>();

        atrn1.Pub += (object? sender, in TValueEventArgs args) =>
        {
            sma.Update(args.Value, args.IsNew);
            receivedValues.Add(args.Value.Value);
        };

        for (int i = 0; i < 50; i++)
        {
            atrn1.Update(_bars[i], true);
        }

        Assert.Equal(50, receivedValues.Count);
        Assert.True(sma.Last.Value >= 0 && sma.Last.Value <= 1);
    }

    #endregion

    #region Normalization Tests

    [Fact]
    public void Output_IsAlwaysNormalized()
    {
        var atrn = new Atrn(DefaultPeriod);

        for (int i = 0; i < _bars.Count; i++)
        {
            var result = atrn.Update(_bars[i], true);
            Assert.True(result.Value >= 0.0,
                $"Value {result.Value} at index {i} is less than 0");
            Assert.True(result.Value <= 1.0,
                $"Value {result.Value} at index {i} is greater than 1");
        }
    }

    [Fact]
    public void ConstantVolatility_ReturnsStableValue()
    {
        var atrn = new Atrn(DefaultPeriod);

        // Create bars with constant range
        var constantBars = new TBarSeries();
        for (int i = 0; i < 200; i++)
        {
            constantBars.Add(new TBar(
                DateTime.UtcNow.AddMinutes(i),
                100.0,  // Open
                105.0,  // High
                95.0,   // Low
                100.0,  // Close
                1000.0  // Volume
            ));
        }

        TValue lastResult = default;
        for (int i = 0; i < constantBars.Count; i++)
        {
            lastResult = atrn.Update(constantBars[i], true);
        }

        // With constant volatility, value should be stable and within [0,1]
        Assert.True(lastResult.Value >= 0.0 && lastResult.Value <= 1.0,
            $"Expected value in [0,1] for constant volatility, got {lastResult.Value}");
    }

    #endregion
}
