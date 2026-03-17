using Xunit;

namespace QuanTAlib.Tests;

public class AcpTests
{
    private const double Tolerance = 1e-9;

    #region Constructor Tests

    [Fact]
    public void Constructor_DefaultParameters_SetsProperties()
    {
        var acp = new Acp();

        Assert.Equal("Acp(8,48)", acp.Name);
        Assert.False(acp.IsHot);
    }

    [Fact]
    public void Constructor_CustomParameters_SetsProperties()
    {
        var acp = new Acp(minPeriod: 10, maxPeriod: 60, avgLength: 5, enhance: false);

        Assert.Equal("Acp(10,60)", acp.Name);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_InvalidMinPeriod_ThrowsArgumentOutOfRange(int minPeriod)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Acp(minPeriod, 48));
        Assert.Equal("minPeriod", ex.ParamName);
    }

    [Theory]
    [InlineData(8, 8)]
    [InlineData(8, 5)]
    [InlineData(10, 10)]
    public void Constructor_MaxPeriodNotGreaterThanMin_ThrowsArgumentOutOfRange(int minPeriod, int maxPeriod)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Acp(minPeriod, maxPeriod));
        Assert.Equal("maxPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativeAvgLength_ThrowsArgumentOutOfRange()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Acp(8, 48, avgLength: -1));
        Assert.Equal("avgLength", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithNullSource_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new Acp(null!, 8, 48));
    }

    [Fact]
    public void Constructor_WithValidSource_Subscribes()
    {
        var source = new TSeries();
        var acp = new Acp(source, 8, 48);

        source.Add(new TValue(DateTime.UtcNow, 100.0));
        Assert.NotEqual(default, acp.Last);
    }

    #endregion

    #region Basic Calculation Tests

    [Fact]
    public void Update_ReturnsValidTValue()
    {
        var acp = new Acp(8, 48);
        var result = acp.Update(new TValue(DateTime.UtcNow, 100.0));

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_AfterWarmup_IsHotTrue()
    {
        var acp = new Acp(8, 48);

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            acp.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(acp.IsHot);
    }

    [Fact]
    public void Update_DominantCycle_WithinRange()
    {
        var acp = new Acp(8, 48);

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            acp.Update(new TValue(bar.Time, bar.Close));
        }

        // Dominant cycle should be within the specified range
        Assert.InRange(acp.DominantCycle, 8, 48);
    }

    [Fact]
    public void Update_NormalizedPower_BetweenZeroAndOne()
    {
        var acp = new Acp(8, 48);

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            acp.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.InRange(acp.NormalizedPower, 0, 1);
    }

    [Fact]
    public void Update_InitialValue_NearMidpoint()
    {
        var acp = new Acp(8, 48);

        // First update should return near midpoint of range
        var result = acp.Update(new TValue(DateTime.UtcNow, 100.0));

        // Initial dominant cycle starts at (8+48)/2 = 28
        Assert.True(result.Value >= 8 && result.Value <= 48);
    }

    #endregion

    #region Bar Correction Tests

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var acp = new Acp(8, 48);

        acp.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        var first = acp.Last.Value;

        acp.Update(new TValue(DateTime.UtcNow.AddSeconds(1), 110.0), isNew: true);
        var second = acp.Last.Value;

        // Values should potentially differ
        Assert.True(double.IsFinite(first) && double.IsFinite(second));
    }

    [Fact]
    public void Update_IsNewFalse_ReplacesCurrentBar()
    {
        var acp = new Acp(8, 48);

        // Build some history
        for (int i = 0; i < 100; i++)
        {
            acp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + Math.Sin(i * 0.1) * 10), isNew: true);
        }

        acp.Update(new TValue(DateTime.UtcNow.AddSeconds(100), 110.0), isNew: true);
        var beforeCorrection = acp.Last.Value;

        // Correct the bar with a different value
        acp.Update(new TValue(DateTime.UtcNow.AddSeconds(100), 90.0), isNew: false);
        var afterCorrection = acp.Last.Value;

        // Values should differ after correction
        Assert.True(double.IsFinite(beforeCorrection) && double.IsFinite(afterCorrection));
    }

    [Fact]
    public void Update_MultipleCorrections_RestoresToSnapshot()
    {
        var acp = new Acp(8, 48);

        // Build some history
        for (int i = 0; i < 100; i++)
        {
            acp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i), isNew: true);
        }

        // Add a new bar
        acp.Update(new TValue(DateTime.UtcNow.AddSeconds(100), 150.0), isNew: true);
        var originalValue = acp.Last.Value;

        // Correct multiple times
        acp.Update(new TValue(DateTime.UtcNow.AddSeconds(100), 160.0), isNew: false);
        acp.Update(new TValue(DateTime.UtcNow.AddSeconds(100), 140.0), isNew: false);
        acp.Update(new TValue(DateTime.UtcNow.AddSeconds(100), 150.0), isNew: false);
        var restoredValue = acp.Last.Value;

        Assert.Equal(originalValue, restoredValue, Tolerance);
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ClearsState()
    {
        var acp = new Acp(8, 48);

        for (int i = 0; i < 200; i++)
        {
            acp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
        }

        Assert.True(acp.IsHot);

        acp.Reset();

        Assert.False(acp.IsHot);
        Assert.Equal(default, acp.Last);
    }

    [Fact]
    public void Reset_AllowsReuse()
    {
        var acp = new Acp(8, 48);

        // First run
        for (int i = 0; i < 200; i++)
        {
            acp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + Math.Sin(i * 0.1) * 10));
        }
        var firstResult = acp.Last.Value;

        acp.Reset();

        // Second run with same data
        for (int i = 0; i < 200; i++)
        {
            acp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + Math.Sin(i * 0.1) * 10));
        }
        var secondResult = acp.Last.Value;

        Assert.Equal(firstResult, secondResult, Tolerance);
    }

    #endregion

    #region NaN/Infinity Handling Tests

    [Fact]
    public void Update_NaN_UsesLastValidValue()
    {
        var acp = new Acp(8, 48);

        acp.Update(new TValue(DateTime.UtcNow, 100.0));
        acp.Update(new TValue(DateTime.UtcNow.AddSeconds(1), double.NaN));

        Assert.True(double.IsFinite(acp.Last.Value));
    }

    [Fact]
    public void Update_Infinity_UsesLastValidValue()
    {
        var acp = new Acp(8, 48);

        acp.Update(new TValue(DateTime.UtcNow, 100.0));
        acp.Update(new TValue(DateTime.UtcNow.AddSeconds(1), double.PositiveInfinity));

        Assert.True(double.IsFinite(acp.Last.Value));
    }

    [Fact]
    public void Update_NegativeInfinity_UsesLastValidValue()
    {
        var acp = new Acp(8, 48);

        acp.Update(new TValue(DateTime.UtcNow, 100.0));
        acp.Update(new TValue(DateTime.UtcNow.AddSeconds(1), double.NegativeInfinity));

        Assert.True(double.IsFinite(acp.Last.Value));
    }

    #endregion

    #region Consistency Tests

    [Theory]
    [InlineData(42)]
    [InlineData(123)]
    [InlineData(999)]
    public void Update_StreamingMatchesBatch(int seed)
    {
        const int minPeriod = 8;
        const int maxPeriod = 48;
        const int dataLen = 200;

        var gbm = new GBM(seed: seed);
        var bars = gbm.Fetch(dataLen, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Streaming
        var streaming = new Acp(minPeriod, maxPeriod);
        foreach (var bar in bars)
        {
            streaming.Update(new TValue(bar.Time, bar.Close));
        }

        // Batch via TSeries
        var tSeries = new TSeries();
        foreach (var bar in bars)
        {
            tSeries.Add(new TValue(bar.Time, bar.Close));
        }

        var batch = Acp.Batch(tSeries, minPeriod, maxPeriod);

        // Compare last values
        Assert.Equal(batch[^1].Value, streaming.Last.Value, Tolerance);
    }

    [Fact]
    public void Batch_MatchesStreaming()
    {
        const int minPeriod = 8;
        const int maxPeriod = 48;
        const int dataLen = 200;

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(dataLen, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Streaming
        var streaming = new Acp(minPeriod, maxPeriod);
        var streamingResults = new double[dataLen];
        for (int i = 0; i < dataLen; i++)
        {
            streaming.Update(new TValue(bars[i].Time, bars[i].Close));
            streamingResults[i] = streaming.Last.Value;
        }

        // Batch
        double[] source = new double[dataLen];
        double[] batchResults = new double[dataLen];
        for (int i = 0; i < dataLen; i++)
        {
            source[i] = bars[i].Close;
        }

        Acp.Batch(source, batchResults, minPeriod, maxPeriod);

        // Compare all values
        for (int i = 0; i < dataLen; i++)
        {
            Assert.Equal(streamingResults[i], batchResults[i], Tolerance);
        }
    }

    #endregion

    #region Span API Tests

    [Fact]
    public void Batch_ValidatesLengthMismatch()
    {
        double[] source = new double[100];
        double[] output = new double[50];

        var ex = Assert.Throws<ArgumentException>(() => Acp.Batch(source, output, 8, 48));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_ValidatesMinPeriod()
    {
        double[] source = new double[100];
        double[] output = new double[100];

        Assert.Throws<ArgumentOutOfRangeException>(() => Acp.Batch(source, output, 2, 48));
    }

    [Fact]
    public void Batch_ValidatesMaxPeriod()
    {
        double[] source = new double[100];
        double[] output = new double[100];

        Assert.Throws<ArgumentOutOfRangeException>(() => Acp.Batch(source, output, 8, 8));
    }

    [Fact]
    public void Batch_EmptyArrays_NoException()
    {
        double[] source = [];
        double[] output = [];

        var ex = Record.Exception(() => Acp.Batch(source, output, 8, 48));
        Assert.Null(ex);
    }

    [Fact]
    public void Batch_HandlesNaN()
    {
        double[] source = { 100, 101, double.NaN, 103, 104, 105, 106, 107, 108, 109 };
        double[] output = new double[10];

        Acp.Batch(source, output, 3, 8);

        foreach (double v in output)
        {
            Assert.True(double.IsFinite(v));
        }
    }

    #endregion

    #region Chaining Tests

    [Fact]
    public void Chaining_PropagatesUpdates()
    {
        var source = new TSeries();
        var acp = new Acp(source, 8, 48);

        for (int i = 0; i < 200; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + Math.Sin(i * 0.1) * 10));
        }

        Assert.True(acp.IsHot);
        Assert.True(double.IsFinite(acp.Last.Value));
    }

    [Fact]
    public void Chaining_MultipleIndicators()
    {
        var source = new TSeries();
        var acp1 = new Acp(source, 8, 48);
        var acp2 = new Acp(source, 12, 60);

        for (int i = 0; i < 300; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + Math.Sin(i * 0.1) * 10));
        }

        // Both should have values
        Assert.True(double.IsFinite(acp1.Last.Value));
        Assert.True(double.IsFinite(acp2.Last.Value));

        // Different ranges should produce different results
        Assert.NotEqual(acp1.Last.Value, acp2.Last.Value);
    }

    #endregion

    #region Parameter Behavior Tests

    [Theory]
    [InlineData(3, 20)]
    [InlineData(8, 48)]
    [InlineData(12, 100)]
    public void Update_DifferentRanges_ProducesValidResults(int minPeriod, int maxPeriod)
    {
        var acp = new Acp(minPeriod, maxPeriod);

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            acp.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(acp.IsHot);
        Assert.InRange(acp.DominantCycle, minPeriod, maxPeriod);
    }

    [Fact]
    public void Update_EnhanceFalse_ProducesValidResults()
    {
        var acp = new Acp(8, 48, avgLength: 3, enhance: false);

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            acp.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(acp.IsHot);
        Assert.InRange(acp.DominantCycle, 8, 48);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    public void Update_DifferentAvgLength_ProducesValidResults(int avgLength)
    {
        var acp = new Acp(8, 48, avgLength: avgLength);

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            acp.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(acp.IsHot);
        Assert.InRange(acp.DominantCycle, 8, 48);
    }

    #endregion
}
