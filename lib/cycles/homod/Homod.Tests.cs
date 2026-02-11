using Xunit;

namespace QuanTAlib.Tests;

public class HomodTests
{
    private const double Tolerance = 1e-9;

    #region Constructor Tests

    [Fact]
    public void Constructor_DefaultParameters_SetsProperties()
    {
        var homod = new Homod();

        Assert.Equal("Homod(6,50)", homod.Name);
        Assert.Equal(100, homod.WarmupPeriod); // maxPeriod * 2
        Assert.False(homod.IsHot);
    }

    [Fact]
    public void Constructor_CustomParameters_SetsProperties()
    {
        var homod = new Homod(minPeriod: 8, maxPeriod: 40);

        Assert.Equal("Homod(8,40)", homod.Name);
        Assert.Equal(80, homod.WarmupPeriod);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-5)]
    public void Constructor_InvalidMinPeriod_ThrowsArgumentOutOfRange(double minPeriod)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Homod(minPeriod, 50));
        Assert.Equal("minPeriod", ex.ParamName);
    }

    [Theory]
    [InlineData(10, 10)]
    [InlineData(10, 5)]
    [InlineData(20, 15)]
    public void Constructor_MaxPeriodNotGreaterThanMin_ThrowsArgumentOutOfRange(double minPeriod, double maxPeriod)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Homod(minPeriod, maxPeriod));
        Assert.Equal("maxPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithNullSource_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new Homod(null!, 6, 50));
    }

    [Fact]
    public void Constructor_WithValidSource_Subscribes()
    {
        var source = new TSeries();
        var homod = new Homod(source, 6, 50);

        source.Add(new TValue(DateTime.UtcNow, 100.0));
        Assert.NotEqual(default, homod.Last);
    }

    #endregion

    #region Basic Calculation Tests

    [Fact]
    public void Update_ReturnsValidTValue()
    {
        var homod = new Homod(6, 50);
        var result = homod.Update(new TValue(DateTime.UtcNow, 100.0));

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_AfterWarmup_IsHotTrue()
    {
        var homod = new Homod(6, 50);

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            homod.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(homod.IsHot);
    }

    [Fact]
    public void Update_DominantCycle_WithinRange()
    {
        var homod = new Homod(6, 50);

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            homod.Update(new TValue(bar.Time, bar.Close));
        }

        // Dominant cycle should be within the specified range
        Assert.InRange(homod.DominantCycle, 6, 50);
    }

    [Fact]
    public void Update_InitialValue_NearMidpoint()
    {
        var homod = new Homod(6, 50);

        // First update should return near initial period (15)
        var result = homod.Update(new TValue(DateTime.UtcNow, 100.0));

        Assert.True(result.Value >= 6 && result.Value <= 50);
    }

    #endregion

    #region Bar Correction Tests

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var homod = new Homod(6, 50);

        homod.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        var first = homod.Last.Value;

        homod.Update(new TValue(DateTime.UtcNow.AddSeconds(1), 110.0), isNew: true);
        var second = homod.Last.Value;

        Assert.True(double.IsFinite(first) && double.IsFinite(second));
    }

    [Fact]
    public void Update_IsNewFalse_ReplacesCurrentBar()
    {
        var homod = new Homod(6, 50);

        // Build some history
        for (int i = 0; i < 100; i++)
        {
            homod.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + Math.Sin(i * 0.1) * 10), isNew: true);
        }

        homod.Update(new TValue(DateTime.UtcNow.AddSeconds(100), 110.0), isNew: true);
        var beforeCorrection = homod.Last.Value;

        // Correct the bar with a different value
        homod.Update(new TValue(DateTime.UtcNow.AddSeconds(100), 90.0), isNew: false);
        var afterCorrection = homod.Last.Value;

        Assert.True(double.IsFinite(beforeCorrection) && double.IsFinite(afterCorrection));
    }

    [Fact]
    public void Update_MultipleCorrections_RestoresToSnapshot()
    {
        var homod = new Homod(6, 50);

        // Build some history
        for (int i = 0; i < 100; i++)
        {
            homod.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i), isNew: true);
        }

        // Add a new bar
        homod.Update(new TValue(DateTime.UtcNow.AddSeconds(100), 150.0), isNew: true);
        var originalValue = homod.Last.Value;

        // Correct multiple times
        homod.Update(new TValue(DateTime.UtcNow.AddSeconds(100), 160.0), isNew: false);
        homod.Update(new TValue(DateTime.UtcNow.AddSeconds(100), 140.0), isNew: false);
        homod.Update(new TValue(DateTime.UtcNow.AddSeconds(100), 150.0), isNew: false);
        var restoredValue = homod.Last.Value;

        Assert.Equal(originalValue, restoredValue, Tolerance);
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ClearsState()
    {
        var homod = new Homod(6, 50);

        for (int i = 0; i < 200; i++)
        {
            homod.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
        }

        Assert.True(homod.IsHot);

        homod.Reset();

        Assert.False(homod.IsHot);
        Assert.Equal(default, homod.Last);
    }

    [Fact]
    public void Reset_AllowsReuse()
    {
        var homod = new Homod(6, 50);

        // First run
        for (int i = 0; i < 200; i++)
        {
            homod.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + Math.Sin(i * 0.1) * 10));
        }
        var firstResult = homod.Last.Value;

        homod.Reset();

        // Second run with same data
        for (int i = 0; i < 200; i++)
        {
            homod.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + Math.Sin(i * 0.1) * 10));
        }
        var secondResult = homod.Last.Value;

        Assert.Equal(firstResult, secondResult, Tolerance);
    }

    #endregion

    #region NaN/Infinity Handling Tests

    [Fact]
    public void Update_NaN_UsesLastValidValue()
    {
        var homod = new Homod(6, 50);

        homod.Update(new TValue(DateTime.UtcNow, 100.0));
        homod.Update(new TValue(DateTime.UtcNow.AddSeconds(1), double.NaN));

        Assert.True(double.IsFinite(homod.Last.Value));
    }

    [Fact]
    public void Update_Infinity_UsesLastValidValue()
    {
        var homod = new Homod(6, 50);

        homod.Update(new TValue(DateTime.UtcNow, 100.0));
        homod.Update(new TValue(DateTime.UtcNow.AddSeconds(1), double.PositiveInfinity));

        Assert.True(double.IsFinite(homod.Last.Value));
    }

    [Fact]
    public void Update_NegativeInfinity_UsesLastValidValue()
    {
        var homod = new Homod(6, 50);

        homod.Update(new TValue(DateTime.UtcNow, 100.0));
        homod.Update(new TValue(DateTime.UtcNow.AddSeconds(1), double.NegativeInfinity));

        Assert.True(double.IsFinite(homod.Last.Value));
    }

    #endregion

    #region Consistency Tests

    [Theory]
    [InlineData(42)]
    [InlineData(123)]
    [InlineData(999)]
    public void Update_StreamingMatchesBatch(int seed)
    {
        const double minPeriod = 6;
        const double maxPeriod = 50;
        const int dataLen = 200;

        var gbm = new GBM(seed: seed);
        var bars = gbm.Fetch(dataLen, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Streaming
        var streaming = new Homod(minPeriod, maxPeriod);
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

        var batch = Homod.Batch(tSeries, minPeriod, maxPeriod);

        // Compare last values
        Assert.Equal(batch[^1].Value, streaming.Last.Value, Tolerance);
    }

    [Fact]
    public void Batch_MatchesStreaming()
    {
        const double minPeriod = 6;
        const double maxPeriod = 50;
        const int dataLen = 200;

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(dataLen, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Streaming
        var streaming = new Homod(minPeriod, maxPeriod);
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

        Homod.Batch(source, batchResults, minPeriod, maxPeriod);

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

        var ex = Assert.Throws<ArgumentException>(() => Homod.Batch(source, output, 6, 50));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_ValidatesMinPeriod()
    {
        double[] source = new double[100];
        double[] output = new double[100];

        Assert.Throws<ArgumentOutOfRangeException>(() => Homod.Batch(source, output, 0, 50));
    }

    [Fact]
    public void Batch_ValidatesMaxPeriod()
    {
        double[] source = new double[100];
        double[] output = new double[100];

        Assert.Throws<ArgumentOutOfRangeException>(() => Homod.Batch(source, output, 10, 10));
    }

    [Fact]
    public void Batch_EmptyArrays_NoException()
    {
        double[] source = [];
        double[] output = [];

        var ex = Record.Exception(() => Homod.Batch(source, output, 6, 50));
        Assert.Null(ex);
    }

    [Fact]
    public void Batch_HandlesNaN()
    {
        double[] source = { 100, 101, double.NaN, 103, 104, 105, 106, 107, 108, 109 };
        double[] output = new double[10];

        Homod.Batch(source, output, 3, 8);

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
        var homod = new Homod(source, 6, 50);

        for (int i = 0; i < 200; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + Math.Sin(i * 0.1) * 10));
        }

        Assert.True(homod.IsHot);
        Assert.True(double.IsFinite(homod.Last.Value));
    }

    [Fact]
    public void Chaining_MultipleIndicators()
    {
        var source = new TSeries();
        var homod1 = new Homod(source, 6, 50);
        var homod2 = new Homod(source, 8, 60);

        for (int i = 0; i < 300; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + Math.Sin(i * 0.1) * 10));
        }

        // Both should have values
        Assert.True(double.IsFinite(homod1.Last.Value));
        Assert.True(double.IsFinite(homod2.Last.Value));

        // Different ranges should produce different results
        Assert.NotEqual(homod1.Last.Value, homod2.Last.Value);
    }

    #endregion

    #region Parameter Behavior Tests

    [Theory]
    [InlineData(3, 20)]
    [InlineData(6, 50)]
    [InlineData(10, 100)]
    public void Update_DifferentRanges_ProducesValidResults(double minPeriod, double maxPeriod)
    {
        var homod = new Homod(minPeriod, maxPeriod);

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            homod.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(homod.IsHot);
        Assert.InRange(homod.DominantCycle, minPeriod, maxPeriod);
    }

    #endregion

    #region Prime Tests

    [Fact]
    public void Prime_WarmupIndicator()
    {
        var homod = new Homod(6, 50);

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double[] primeData = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            primeData[i] = bars[i].Close;
        }

        homod.Prime(primeData);

        Assert.True(homod.IsHot);
    }

    #endregion
}