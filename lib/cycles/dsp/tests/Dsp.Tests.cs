using Xunit;

namespace QuanTAlib.Tests;

public class DspTests
{
    private const double Tolerance = 1e-9;

    #region Constructor Tests

    [Fact]
    public void Constructor_ValidPeriod_SetsProperties()
    {
        var dsp = new Dsp(40);

        Assert.Equal("Dsp(40)", dsp.Name);
        Assert.False(dsp.IsHot);
    }

    [Fact]
    public void Constructor_MinimumPeriod_Works()
    {
        var dsp = new Dsp(4);

        Assert.Equal("Dsp(4)", dsp.Name);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(3)]
    public void Constructor_InvalidPeriod_ThrowsArgumentOutOfRange(int period)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Dsp(period));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithNullSource_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new Dsp(null!, 40));
    }

    [Fact]
    public void Constructor_WithValidSource_Subscribes()
    {
        var source = new TSeries();
        var dsp = new Dsp(source, 40);

        source.Add(new TValue(DateTime.UtcNow, 100.0));
        Assert.NotEqual(default, dsp.Last);
    }

    #endregion

    #region Basic Calculation Tests

    [Fact]
    public void Update_ReturnsValidTValue()
    {
        var dsp = new Dsp(40);
        var result = dsp.Update(new TValue(DateTime.UtcNow, 100.0));

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_AfterWarmup_IsHotTrue()
    {
        var dsp = new Dsp(8); // Small period for faster warmup

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            dsp.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(dsp.IsHot);
    }

    [Fact]
    public void Update_ConstantSeries_DspIsZero()
    {
        // For a constant series, both EMAs converge to the same value
        // so DSP = fast - slow = 0
        var dsp = new Dsp(40);

        for (int i = 0; i < 500; i++)
        {
            dsp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
        }

        Assert.Equal(0.0, dsp.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_Uptrend_DspPositive()
    {
        // Fast EMA reacts more quickly to rising prices, so DSP > 0
        var dsp = new Dsp(20);

        for (int i = 0; i < 100; i++)
        {
            double price = 100.0 + (i * 1.0);
            dsp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), price));
        }

        Assert.True(dsp.Last.Value > 0, $"Uptrend should produce positive DSP, got {dsp.Last.Value}");
    }

    [Fact]
    public void Update_Downtrend_DspNegative()
    {
        // Fast EMA reacts more quickly to falling prices, so DSP < 0
        var dsp = new Dsp(20);

        for (int i = 0; i < 100; i++)
        {
            double price = 200.0 - (i * 1.0);
            dsp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), price));
        }

        Assert.True(dsp.Last.Value < 0, $"Downtrend should produce negative DSP, got {dsp.Last.Value}");
    }

    #endregion

    #region Bar Correction Tests

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var dsp = new Dsp(20);

        dsp.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        var first = dsp.Last.Value;

        dsp.Update(new TValue(DateTime.UtcNow.AddSeconds(1), 110.0), isNew: true);
        var second = dsp.Last.Value;

        // Values should be different after processing different prices
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Update_IsNewFalse_ReplacesCurrentBar()
    {
        var dsp = new Dsp(20);

        dsp.Update(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        dsp.Update(new TValue(DateTime.UtcNow.AddSeconds(1), 110.0), isNew: true);
        var beforeCorrection = dsp.Last.Value;

        // Correct the bar with a different value
        dsp.Update(new TValue(DateTime.UtcNow.AddSeconds(1), 90.0), isNew: false);
        var afterCorrection = dsp.Last.Value;

        Assert.NotEqual(beforeCorrection, afterCorrection);
    }

    [Fact]
    public void Update_MultipleCorrections_RestoresToSnapshot()
    {
        var dsp = new Dsp(20);

        // Build some history
        for (int i = 0; i < 30; i++)
        {
            dsp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i), isNew: true);
        }

        // Add a new bar
        dsp.Update(new TValue(DateTime.UtcNow.AddSeconds(30), 150.0), isNew: true);
        var originalValue = dsp.Last.Value;

        // Correct multiple times
        dsp.Update(new TValue(DateTime.UtcNow.AddSeconds(30), 160.0), isNew: false);
        dsp.Update(new TValue(DateTime.UtcNow.AddSeconds(30), 140.0), isNew: false);
        dsp.Update(new TValue(DateTime.UtcNow.AddSeconds(30), 150.0), isNew: false);
        var restoredValue = dsp.Last.Value;

        Assert.Equal(originalValue, restoredValue, Tolerance);
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ClearsState()
    {
        var dsp = new Dsp(20);

        for (int i = 0; i < 50; i++)
        {
            dsp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
        }

        Assert.True(dsp.IsHot);

        dsp.Reset();

        Assert.False(dsp.IsHot);
        Assert.Equal(default, dsp.Last);
    }

    [Fact]
    public void Reset_AllowsReuse()
    {
        var dsp = new Dsp(20);

        // First run
        for (int i = 0; i < 50; i++)
        {
            dsp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
        }
        var firstResult = dsp.Last.Value;

        dsp.Reset();

        // Second run with same data
        for (int i = 0; i < 50; i++)
        {
            dsp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
        }
        var secondResult = dsp.Last.Value;

        Assert.Equal(firstResult, secondResult, Tolerance);
    }

    #endregion

    #region NaN/Infinity Handling Tests

    [Fact]
    public void Update_NaN_UsesLastValidValue()
    {
        var dsp = new Dsp(20);

        dsp.Update(new TValue(DateTime.UtcNow, 100.0));
        dsp.Update(new TValue(DateTime.UtcNow.AddSeconds(1), double.NaN));
        var afterNaN = dsp.Last.Value;

        Assert.True(double.IsFinite(afterNaN));
    }

    [Fact]
    public void Update_Infinity_UsesLastValidValue()
    {
        var dsp = new Dsp(20);

        dsp.Update(new TValue(DateTime.UtcNow, 100.0));
        dsp.Update(new TValue(DateTime.UtcNow.AddSeconds(1), double.PositiveInfinity));

        Assert.True(double.IsFinite(dsp.Last.Value));
    }

    [Fact]
    public void Update_NegativeInfinity_UsesLastValidValue()
    {
        var dsp = new Dsp(20);

        dsp.Update(new TValue(DateTime.UtcNow, 100.0));
        dsp.Update(new TValue(DateTime.UtcNow.AddSeconds(1), double.NegativeInfinity));

        Assert.True(double.IsFinite(dsp.Last.Value));
    }

    #endregion

    #region Consistency Tests

    [Theory]
    [InlineData(42)]
    [InlineData(123)]
    [InlineData(999)]
    public void Update_StreamingMatchesBatch(int seed)
    {
        const int period = 40;
        const int dataLen = 100;

        var gbm = new GBM(seed: seed);
        var bars = gbm.Fetch(dataLen, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Streaming
        var streaming = new Dsp(period);
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

        var batch = Dsp.Batch(tSeries, period);

        // Compare last values
        Assert.Equal(batch[^1].Value, streaming.Last.Value, Tolerance);
    }

    [Fact]
    public void Batch_MatchesStreaming()
    {
        const int period = 20;
        const int dataLen = 200;

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(dataLen, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Streaming
        var streaming = new Dsp(period);
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

        Dsp.Batch(source, batchResults, period);

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

        var ex = Assert.Throws<ArgumentException>(() => Dsp.Batch(source, output, 20));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_ValidatesPeriod()
    {
        double[] source = new double[100];
        double[] output = new double[100];

        Assert.Throws<ArgumentOutOfRangeException>(() => Dsp.Batch(source, output, 3));
    }

    [Fact]
    public void Batch_EmptyArrays_NoException()
    {
        double[] source = [];
        double[] output = [];

        var ex = Record.Exception(() => Dsp.Batch(source, output, 20));
        Assert.Null(ex);
    }

    [Fact]
    public void Batch_HandlesNaN()
    {
        double[] source = { 100, 101, double.NaN, 103, 104 };
        double[] output = new double[5];

        Dsp.Batch(source, output, 4);

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
        var dsp = new Dsp(source, 20);

        for (int i = 0; i < 50; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
        }

        Assert.True(dsp.IsHot);
        Assert.True(double.IsFinite(dsp.Last.Value));
    }

    [Fact]
    public void Chaining_MultipleIndicators()
    {
        var source = new TSeries();
        var dsp1 = new Dsp(source, 20);
        var dsp2 = new Dsp(source, 40);

        for (int i = 0; i < 100; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + (Math.Sin(i * 0.1) * 10)));
        }

        // Both should have values
        Assert.True(double.IsFinite(dsp1.Last.Value));
        Assert.True(double.IsFinite(dsp2.Last.Value));

        // Different periods should produce different results
        Assert.NotEqual(dsp1.Last.Value, dsp2.Last.Value);
    }

    #endregion

    #region Period Behavior Tests

    [Theory]
    [InlineData(4)]
    [InlineData(20)]
    [InlineData(40)]
    [InlineData(100)]
    public void Update_DifferentPeriods_ProducesValidResults(int period)
    {
        var dsp = new Dsp(period);

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            dsp.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(dsp.IsHot);
        Assert.True(double.IsFinite(dsp.Last.Value));
    }

    #endregion
}
