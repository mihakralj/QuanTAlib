using Xunit;

namespace QuanTAlib.Tests;

public class EbswTests
{
    private const double Tolerance = 1e-9;

    #region Constructor Tests

    [Fact]
    public void Constructor_ValidParameters_SetsProperties()
    {
        var ebsw = new Ebsw(40, 10);

        Assert.Equal("Ebsw(40,10)", ebsw.Name);
        Assert.False(ebsw.IsHot);
    }

    [Fact]
    public void Constructor_DefaultParameters_Works()
    {
        var ebsw = new Ebsw();

        Assert.Equal("Ebsw(40,10)", ebsw.Name);
    }

    [Fact]
    public void Constructor_MinimumParameters_Works()
    {
        var ebsw = new Ebsw(1, 1);

        Assert.Equal("Ebsw(1,1)", ebsw.Name);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    public void Constructor_InvalidHpLength_ThrowsArgumentOutOfRange(int hpLength, int ssfLength)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Ebsw(hpLength, ssfLength));
        Assert.Equal("hpLength", ex.ParamName);
    }

    [Theory]
    [InlineData(40, 0)]
    [InlineData(40, -1)]
    public void Constructor_InvalidSsfLength_ThrowsArgumentOutOfRange(int hpLength, int ssfLength)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Ebsw(hpLength, ssfLength));
        Assert.Equal("ssfLength", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithNullSource_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new Ebsw(null!, 40, 10));
    }

    [Fact]
    public void Constructor_WithValidSource_Subscribes()
    {
        var source = new TSeries();
        var ebsw = new Ebsw(source, 40, 10);

        source.Add(new TValue(DateTime.UtcNow, 100.0));
        Assert.NotEqual(default, ebsw.Last);
    }

    #endregion

    #region Basic Calculation Tests

    [Fact]
    public void Update_ReturnsValidTValue()
    {
        var ebsw = new Ebsw(40, 10);
        var result = ebsw.Update(new TValue(DateTime.UtcNow, 100.0));

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_AfterWarmup_IsHotTrue()
    {
        var ebsw = new Ebsw(10, 5);

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            ebsw.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(ebsw.IsHot);
    }

    [Fact]
    public void Update_OutputBoundedBetweenMinusOneAndOne()
    {
        var ebsw = new Ebsw(40, 10);

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            ebsw.Update(new TValue(bar.Time, bar.Close));
            Assert.InRange(ebsw.Last.Value, -1.0, 1.0);
        }
    }

    [Fact]
    public void Update_ConstantSeries_ProducesBoundedOutput()
    {
        // For a constant series, the high-pass filter removes the DC component,
        // making filt → 0. However, the AGC (wave/sqrt(pwr)) normalizes any
        // non-zero signal. Due to floating-point precision, very small filt values
        // produce ratios approaching ±1, not 0. This is mathematically correct.
        var ebsw = new Ebsw(40, 10);

        for (int i = 0; i < 500; i++)
        {
            ebsw.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
        }

        // Output should still be bounded [-1, +1]
        Assert.InRange(ebsw.Last.Value, -1.0, 1.0);
    }

    [Fact]
    public void Update_SinusoidalInput_DetectsCycles()
    {
        var ebsw = new Ebsw(40, 10);
        double frequency = 2.0 * Math.PI / 20.0; // 20-bar cycle

        for (int i = 0; i < 200; i++)
        {
            double price = 100.0 + 10.0 * Math.Sin(i * frequency);
            ebsw.Update(new TValue(DateTime.UtcNow.AddSeconds(i), price));
        }

        // Output should show cyclic behavior with values approaching extremes
        Assert.True(ebsw.IsHot);
        Assert.InRange(ebsw.Last.Value, -1.0, 1.0);
    }

    #endregion

    #region Bar Correction Tests

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var ebsw = new Ebsw(20, 10);

        ebsw.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        var first = ebsw.Last.Value;

        ebsw.Update(new TValue(DateTime.UtcNow.AddSeconds(1), 110.0), isNew: true);
        var second = ebsw.Last.Value;

        // Values should differ after processing different prices
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Update_IsNewFalse_ReplacesCurrentBar()
    {
        var ebsw = new Ebsw(20, 10);

        ebsw.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        ebsw.Update(new TValue(DateTime.UtcNow.AddSeconds(1), 110.0), isNew: true);
        var beforeCorrection = ebsw.Last.Value;

        // Correct the bar with a different value
        ebsw.Update(new TValue(DateTime.UtcNow.AddSeconds(1), 90.0), isNew: false);
        var afterCorrection = ebsw.Last.Value;

        Assert.NotEqual(beforeCorrection, afterCorrection);
    }

    [Fact]
    public void Update_MultipleCorrections_RestoresToSnapshot()
    {
        var ebsw = new Ebsw(20, 10);

        // Build some history
        for (int i = 0; i < 50; i++)
        {
            ebsw.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i), isNew: true);
        }

        // Add a new bar
        ebsw.Update(new TValue(DateTime.UtcNow.AddSeconds(50), 150.0), isNew: true);
        var originalValue = ebsw.Last.Value;

        // Correct multiple times
        ebsw.Update(new TValue(DateTime.UtcNow.AddSeconds(50), 160.0), isNew: false);
        ebsw.Update(new TValue(DateTime.UtcNow.AddSeconds(50), 140.0), isNew: false);
        ebsw.Update(new TValue(DateTime.UtcNow.AddSeconds(50), 150.0), isNew: false);
        var restoredValue = ebsw.Last.Value;

        Assert.Equal(originalValue, restoredValue, Tolerance);
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ClearsState()
    {
        var ebsw = new Ebsw(20, 10);

        for (int i = 0; i < 100; i++)
        {
            ebsw.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
        }

        Assert.True(ebsw.IsHot);

        ebsw.Reset();

        Assert.False(ebsw.IsHot);
        Assert.Equal(default, ebsw.Last);
    }

    [Fact]
    public void Reset_AllowsReuse()
    {
        var ebsw = new Ebsw(20, 10);

        // First run
        for (int i = 0; i < 100; i++)
        {
            ebsw.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + Math.Sin(i * 0.1) * 10));
        }
        var firstResult = ebsw.Last.Value;

        ebsw.Reset();

        // Second run with same data
        for (int i = 0; i < 100; i++)
        {
            ebsw.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + Math.Sin(i * 0.1) * 10));
        }
        var secondResult = ebsw.Last.Value;

        Assert.Equal(firstResult, secondResult, Tolerance);
    }

    #endregion

    #region NaN/Infinity Handling Tests

    [Fact]
    public void Update_NaN_UsesLastValidValue()
    {
        var ebsw = new Ebsw(20, 10);

        ebsw.Update(new TValue(DateTime.UtcNow, 100.0));
        ebsw.Update(new TValue(DateTime.UtcNow.AddSeconds(1), double.NaN));
        var afterNaN = ebsw.Last.Value;

        Assert.True(double.IsFinite(afterNaN));
    }

    [Fact]
    public void Update_Infinity_UsesLastValidValue()
    {
        var ebsw = new Ebsw(20, 10);

        ebsw.Update(new TValue(DateTime.UtcNow, 100.0));
        ebsw.Update(new TValue(DateTime.UtcNow.AddSeconds(1), double.PositiveInfinity));

        Assert.True(double.IsFinite(ebsw.Last.Value));
    }

    [Fact]
    public void Update_NegativeInfinity_UsesLastValidValue()
    {
        var ebsw = new Ebsw(20, 10);

        ebsw.Update(new TValue(DateTime.UtcNow, 100.0));
        ebsw.Update(new TValue(DateTime.UtcNow.AddSeconds(1), double.NegativeInfinity));

        Assert.True(double.IsFinite(ebsw.Last.Value));
    }

    #endregion

    #region Consistency Tests

    [Theory]
    [InlineData(42)]
    [InlineData(123)]
    [InlineData(999)]
    public void Update_StreamingMatchesBatch(int seed)
    {
        const int hpLength = 40;
        const int ssfLength = 10;
        const int dataLen = 100;

        var gbm = new GBM(seed: seed);
        var bars = gbm.Fetch(dataLen, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Streaming
        var streaming = new Ebsw(hpLength, ssfLength);
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

        var batch = Ebsw.Calculate(tSeries, hpLength, ssfLength);

        // Compare last values
        Assert.Equal(batch[^1].Value, streaming.Last.Value, Tolerance);
    }

    [Fact]
    public void Batch_MatchesStreaming()
    {
        const int hpLength = 20;
        const int ssfLength = 8;
        const int dataLen = 200;

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(dataLen, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Streaming
        var streaming = new Ebsw(hpLength, ssfLength);
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

        Ebsw.Batch(source, batchResults, hpLength, ssfLength);

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

        var ex = Assert.Throws<ArgumentException>(() => Ebsw.Batch(source, output, 40, 10));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_ValidatesHpLength()
    {
        double[] source = new double[100];
        double[] output = new double[100];

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Ebsw.Batch(source, output, 0, 10));
        Assert.Equal("hpLength", ex.ParamName);
    }

    [Fact]
    public void Batch_ValidatesSsfLength()
    {
        double[] source = new double[100];
        double[] output = new double[100];

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Ebsw.Batch(source, output, 40, 0));
        Assert.Equal("ssfLength", ex.ParamName);
    }

    [Fact]
    public void Batch_EmptyArrays_NoException()
    {
        double[] source = [];
        double[] output = [];

        var ex = Record.Exception(() => Ebsw.Batch(source, output, 40, 10));
        Assert.Null(ex);
    }

    [Fact]
    public void Batch_HandlesNaN()
    {
        double[] source = { 100, 101, double.NaN, 103, 104 };
        double[] output = new double[5];

        Ebsw.Batch(source, output, 5, 2);

        foreach (double v in output)
        {
            Assert.True(double.IsFinite(v));
        }
    }

    [Fact]
    public void Batch_HpLengthFour_ThrowsArgumentOutOfRange()
    {
        double[] source = new double[100];
        double[] output = new double[100];

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Ebsw.Batch(source, output, 4, 10));
        Assert.Equal("hpLength", ex.ParamName);
        Assert.Contains("cos(2π/hpLength) is zero", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_HpLengthFour_ThrowsArgumentOutOfRange()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Ebsw(4, 10));
        Assert.Equal("hpLength", ex.ParamName);
        Assert.Contains("cos(2π/hpLength) is zero", ex.Message, StringComparison.Ordinal);
    }

    #endregion

    #region Chaining Tests

    [Fact]
    public void Chaining_PropagatesUpdates()
    {
        var source = new TSeries();
        var ebsw = new Ebsw(source, 20, 10);

        for (int i = 0; i < 100; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + Math.Sin(i * 0.1) * 10));
        }

        Assert.True(ebsw.IsHot);
        Assert.True(double.IsFinite(ebsw.Last.Value));
    }

    [Fact]
    public void Chaining_MultipleIndicators()
    {
        var source = new TSeries();
        var ebsw1 = new Ebsw(source, 20, 10);
        var ebsw2 = new Ebsw(source, 40, 5);

        for (int i = 0; i < 200; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + Math.Sin(i * 0.1) * 10));
        }

        // Both should have values
        Assert.True(double.IsFinite(ebsw1.Last.Value));
        Assert.True(double.IsFinite(ebsw2.Last.Value));

        // Different parameters should produce different results
        Assert.NotEqual(ebsw1.Last.Value, ebsw2.Last.Value);
    }

    #endregion

    #region Parameter Behavior Tests

    [Theory]
    [InlineData(10, 5)]
    [InlineData(20, 10)]
    [InlineData(40, 10)]
    [InlineData(100, 20)]
    public void Update_DifferentParameters_ProducesValidResults(int hpLength, int ssfLength)
    {
        var ebsw = new Ebsw(hpLength, ssfLength);

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            ebsw.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(ebsw.IsHot);
        Assert.True(double.IsFinite(ebsw.Last.Value));
        Assert.InRange(ebsw.Last.Value, -1.0, 1.0);
    }

    #endregion
}