using Xunit;

namespace QuanTAlib.Tests;

public class SsfdspTests
{
    private const double Tolerance = 1e-9;

    #region Constructor Tests

    [Fact]
    public void Constructor_ValidPeriod_SetsProperties()
    {
        var ssfdsp = new Ssfdsp(40);

        Assert.Equal("SsfDsp(40)", ssfdsp.Name);
        Assert.False(ssfdsp.IsHot);
    }

    [Fact]
    public void Constructor_MinimumPeriod_Works()
    {
        var ssfdsp = new Ssfdsp(4);

        Assert.Equal("SsfDsp(4)", ssfdsp.Name);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(3)]
    public void Constructor_InvalidPeriod_ThrowsArgumentOutOfRange(int period)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Ssfdsp(period));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithNullSource_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new Ssfdsp(null!, 40));
    }

    [Fact]
    public void Constructor_WithValidSource_Subscribes()
    {
        var source = new TSeries();
        var ssfdsp = new Ssfdsp(source, 40);

        source.Add(new TValue(DateTime.UtcNow, 100.0));
        Assert.NotEqual(default, ssfdsp.Last);
    }

    #endregion

    #region Basic Calculation Tests

    [Fact]
    public void Update_ReturnsValidTValue()
    {
        var ssfdsp = new Ssfdsp(40);
        var result = ssfdsp.Update(new TValue(DateTime.UtcNow, 100.0));

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_AfterWarmup_IsHotTrue()
    {
        var ssfdsp = new Ssfdsp(8); // Small period for faster warmup

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            ssfdsp.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(ssfdsp.IsHot);
    }

    [Fact]
    public void Update_ConstantSeries_SsfdspIsZero()
    {
        // For a constant series, both SSFs converge to the same value
        // so SSF-DSP = fast - slow = 0
        var ssfdsp = new Ssfdsp(40);

        for (int i = 0; i < 500; i++)
        {
            ssfdsp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
        }

        Assert.Equal(0.0, ssfdsp.Last.Value, Tolerance);
    }

    [Fact]
    public void Update_Uptrend_SsfdspPositive()
    {
        // Fast SSF reacts more quickly to rising prices, so SSF-DSP > 0
        var ssfdsp = new Ssfdsp(20);

        for (int i = 0; i < 100; i++)
        {
            double price = 100.0 + (i * 1.0);
            ssfdsp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), price));
        }

        Assert.True(ssfdsp.Last.Value > 0, $"Uptrend should produce positive SSF-DSP, got {ssfdsp.Last.Value}");
    }

    [Fact]
    public void Update_Downtrend_SsfdspNegative()
    {
        // Fast SSF reacts more quickly to falling prices, so SSF-DSP < 0
        var ssfdsp = new Ssfdsp(20);

        for (int i = 0; i < 100; i++)
        {
            double price = 200.0 - (i * 1.0);
            ssfdsp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), price));
        }

        Assert.True(ssfdsp.Last.Value < 0, $"Downtrend should produce negative SSF-DSP, got {ssfdsp.Last.Value}");
    }

    #endregion

    #region Bar Correction Tests

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var ssfdsp = new Ssfdsp(8);  // Use smaller period

        // Build some history first
        for (int i = 0; i < 20; i++)
        {
            ssfdsp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i), isNew: true);
        }
        var first = ssfdsp.Last.Value;

        ssfdsp.Update(new TValue(DateTime.UtcNow.AddSeconds(20), 150.0), isNew: true);
        var second = ssfdsp.Last.Value;

        // Values should be different after processing different prices
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Update_IsNewFalse_ReplacesCurrentBar()
    {
        var ssfdsp = new Ssfdsp(8);  // Use smaller period

        // Build some history first
        for (int i = 0; i < 20; i++)
        {
            ssfdsp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i), isNew: true);
        }

        ssfdsp.Update(new TValue(DateTime.UtcNow.AddSeconds(20), 150.0), isNew: true);
        var beforeCorrection = ssfdsp.Last.Value;

        // Correct the bar with a significantly different value
        ssfdsp.Update(new TValue(DateTime.UtcNow.AddSeconds(20), 50.0), isNew: false);
        var afterCorrection = ssfdsp.Last.Value;

        Assert.NotEqual(beforeCorrection, afterCorrection);
    }

    [Fact]
    public void Update_MultipleCorrections_RestoresToSnapshot()
    {
        var ssfdsp = new Ssfdsp(20);

        // Build some history
        for (int i = 0; i < 30; i++)
        {
            ssfdsp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i), isNew: true);
        }

        // Add a new bar
        ssfdsp.Update(new TValue(DateTime.UtcNow.AddSeconds(30), 150.0), isNew: true);
        var originalValue = ssfdsp.Last.Value;

        // Correct multiple times
        ssfdsp.Update(new TValue(DateTime.UtcNow.AddSeconds(30), 160.0), isNew: false);
        ssfdsp.Update(new TValue(DateTime.UtcNow.AddSeconds(30), 140.0), isNew: false);
        ssfdsp.Update(new TValue(DateTime.UtcNow.AddSeconds(30), 150.0), isNew: false);
        var restoredValue = ssfdsp.Last.Value;

        Assert.Equal(originalValue, restoredValue, Tolerance);
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ClearsState()
    {
        var ssfdsp = new Ssfdsp(20);

        for (int i = 0; i < 50; i++)
        {
            ssfdsp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
        }

        Assert.True(ssfdsp.IsHot);

        ssfdsp.Reset();

        Assert.False(ssfdsp.IsHot);
        Assert.Equal(default, ssfdsp.Last);
    }

    [Fact]
    public void Reset_AllowsReuse()
    {
        var ssfdsp = new Ssfdsp(20);

        // First run
        for (int i = 0; i < 50; i++)
        {
            ssfdsp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
        }
        var firstResult = ssfdsp.Last.Value;

        ssfdsp.Reset();

        // Second run with same data
        for (int i = 0; i < 50; i++)
        {
            ssfdsp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
        }
        var secondResult = ssfdsp.Last.Value;

        Assert.Equal(firstResult, secondResult, Tolerance);
    }

    #endregion

    #region NaN/Infinity Handling Tests

    [Fact]
    public void Update_NaN_UsesLastValidValue()
    {
        var ssfdsp = new Ssfdsp(20);

        ssfdsp.Update(new TValue(DateTime.UtcNow, 100.0));
        ssfdsp.Update(new TValue(DateTime.UtcNow.AddSeconds(1), double.NaN));
        var afterNaN = ssfdsp.Last.Value;

        Assert.True(double.IsFinite(afterNaN));
    }

    [Fact]
    public void Update_Infinity_UsesLastValidValue()
    {
        var ssfdsp = new Ssfdsp(20);

        ssfdsp.Update(new TValue(DateTime.UtcNow, 100.0));
        ssfdsp.Update(new TValue(DateTime.UtcNow.AddSeconds(1), double.PositiveInfinity));

        Assert.True(double.IsFinite(ssfdsp.Last.Value));
    }

    [Fact]
    public void Update_NegativeInfinity_UsesLastValidValue()
    {
        var ssfdsp = new Ssfdsp(20);

        ssfdsp.Update(new TValue(DateTime.UtcNow, 100.0));
        ssfdsp.Update(new TValue(DateTime.UtcNow.AddSeconds(1), double.NegativeInfinity));

        Assert.True(double.IsFinite(ssfdsp.Last.Value));
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
        var streaming = new Ssfdsp(period);
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

        var batch = Ssfdsp.Batch(tSeries, period);

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
        var streaming = new Ssfdsp(period);
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

        Ssfdsp.Batch(source, batchResults, period);

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

        var ex = Assert.Throws<ArgumentException>(() => Ssfdsp.Batch(source, output, 20));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_ValidatesPeriod()
    {
        double[] source = new double[100];
        double[] output = new double[100];

        Assert.Throws<ArgumentOutOfRangeException>(() => Ssfdsp.Batch(source, output, 3));
    }

    [Fact]
    public void Batch_EmptyArrays_NoException()
    {
        double[] source = [];
        double[] output = [];

        var ex = Record.Exception(() => Ssfdsp.Batch(source, output, 20));
        Assert.Null(ex);
    }

    [Fact]
    public void Batch_HandlesNaN()
    {
        double[] source = { 100, 101, double.NaN, 103, 104 };
        double[] output = new double[5];

        Ssfdsp.Batch(source, output, 4);

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
        var ssfdsp = new Ssfdsp(source, 20);

        for (int i = 0; i < 50; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
        }

        Assert.True(ssfdsp.IsHot);
        Assert.True(double.IsFinite(ssfdsp.Last.Value));
    }

    [Fact]
    public void Chaining_MultipleIndicators()
    {
        var source = new TSeries();
        var ssfdsp1 = new Ssfdsp(source, 20);
        var ssfdsp2 = new Ssfdsp(source, 40);

        for (int i = 0; i < 100; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + (Math.Sin(i * 0.1) * 10)));
        }

        // Both should have values
        Assert.True(double.IsFinite(ssfdsp1.Last.Value));
        Assert.True(double.IsFinite(ssfdsp2.Last.Value));

        // Different periods should produce different results
        Assert.NotEqual(ssfdsp1.Last.Value, ssfdsp2.Last.Value);
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
        var ssfdsp = new Ssfdsp(period);

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            ssfdsp.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(ssfdsp.IsHot);
        Assert.True(double.IsFinite(ssfdsp.Last.Value));
    }

    #endregion

    #region Comparison with DSP Tests

    [Fact]
    public void SsfdspVsDsp_BothOscillateAroundZero()
    {
        // Both DSP and SSF-DSP should oscillate around zero for the same input
        var ssfdsp = new Ssfdsp(40);
        var dsp = new Dsp(40);

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        double ssfdspSum = 0, dspSum = 0;
        int count = 0;

        foreach (var bar in bars)
        {
            var input = new TValue(bar.Time, bar.Close);
            ssfdsp.Update(input);
            dsp.Update(input);

            if (ssfdsp.IsHot && dsp.IsHot)
            {
                ssfdspSum += ssfdsp.Last.Value;
                dspSum += dsp.Last.Value;
                count++;
            }
        }

        // Both should have mean close to zero (detrending property)
        double ssfdspMean = ssfdspSum / count;
        double dspMean = dspSum / count;

        // Mean should be relatively small compared to price range
        Assert.True(Math.Abs(ssfdspMean) < 5, $"SSF-DSP mean {ssfdspMean} should be close to zero");
        Assert.True(Math.Abs(dspMean) < 5, $"DSP mean {dspMean} should be close to zero");
    }

    #endregion
}
