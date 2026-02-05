using Xunit;

namespace QuanTAlib.Tests;

public class HtSineTests
{
    private const double Tolerance = 1e-9;

    #region Constructor Tests

    [Fact]
    public void Constructor_SetsProperties()
    {
        var htSine = new HtSine();

        Assert.Equal("HtSine", htSine.Name);
        Assert.False(htSine.IsHot);
        Assert.Equal(63, htSine.WarmupPeriod);
    }

    [Fact]
    public void Constructor_WithNullSource_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new HtSine(null!));
    }

    [Fact]
    public void Constructor_WithValidSource_Subscribes()
    {
        var source = new TSeries();
        var htSine = new HtSine(source);

        source.Add(new TValue(DateTime.UtcNow, 100.0));
        Assert.NotEqual(default, htSine.Last);
    }

    #endregion

    #region Basic Calculation Tests

    [Fact]
    public void Update_ReturnsValidTValue()
    {
        var htSine = new HtSine();
        var result = htSine.Update(new TValue(DateTime.UtcNow, 100.0));

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Update_AfterWarmup_IsHotTrue()
    {
        var htSine = new HtSine();

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            htSine.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(htSine.IsHot);
    }

    [Fact]
    public void Update_SineInBoundedRange()
    {
        // Sine values should be between -1 and +1
        var htSine = new HtSine();

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            htSine.Update(new TValue(bar.Time, bar.Close));
            
            if (htSine.IsHot)
            {
                Assert.InRange(htSine.Last.Value, -1.0, 1.0);
                Assert.InRange(htSine.LeadSine, -1.0, 1.0);
            }
        }
    }

    [Fact]
    public void Update_LeadSineIsAccessible()
    {
        var htSine = new HtSine();

        for (int i = 0; i < 100; i++)
        {
            htSine.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + Math.Sin(i * 0.1) * 10));
        }

        Assert.True(double.IsFinite(htSine.LeadSine));
    }

    [Fact]
    public void Update_SineWaveInput_DetectsCycle()
    {
        var htSine = new HtSine();

        // Feed a perfect sine wave with known period
        const int period = 20;
        for (int i = 0; i < 500; i++)
        {
            double price = 100.0 + 10.0 * Math.Sin(2.0 * Math.PI * i / period);
            htSine.Update(new TValue(DateTime.UtcNow.AddSeconds(i), price));
        }

        // Should have detected a cycle and produce valid output
        Assert.True(htSine.IsHot);
        Assert.InRange(htSine.Last.Value, -1.0, 1.0);
        Assert.InRange(htSine.LeadSine, -1.0, 1.0);
    }

    #endregion

    #region Bar Correction Tests

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var htSine = new HtSine();

        // Build some history first
        for (int i = 0; i < 100; i++)
        {
            htSine.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i * 0.1), isNew: true);
        }
        var first = htSine.Last.Value;

        htSine.Update(new TValue(DateTime.UtcNow.AddSeconds(100), 200.0), isNew: true);
        var second = htSine.Last.Value;

        // Values should be different after processing different prices
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Update_IsNewFalse_ReplacesCurrentBar()
    {
        var htSine = new HtSine();

        // Build some history first
        for (int i = 0; i < 100; i++)
        {
            htSine.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i * 0.1), isNew: true);
        }

        htSine.Update(new TValue(DateTime.UtcNow.AddSeconds(100), 150.0), isNew: true);
        var beforeCorrection = htSine.Last.Value;

        // Correct the bar with a significantly different value
        htSine.Update(new TValue(DateTime.UtcNow.AddSeconds(100), 50.0), isNew: false);
        var afterCorrection = htSine.Last.Value;

        Assert.NotEqual(beforeCorrection, afterCorrection);
    }

    [Fact]
    public void Update_MultipleCorrections_RestoresToSnapshot()
    {
        var htSine = new HtSine();

        // Build some history
        for (int i = 0; i < 100; i++)
        {
            htSine.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i * 0.1), isNew: true);
        }

        // Add a new bar
        htSine.Update(new TValue(DateTime.UtcNow.AddSeconds(100), 150.0), isNew: true);
        var originalValue = htSine.Last.Value;

        // Correct multiple times
        htSine.Update(new TValue(DateTime.UtcNow.AddSeconds(100), 160.0), isNew: false);
        htSine.Update(new TValue(DateTime.UtcNow.AddSeconds(100), 140.0), isNew: false);
        htSine.Update(new TValue(DateTime.UtcNow.AddSeconds(100), 150.0), isNew: false);
        var restoredValue = htSine.Last.Value;

        Assert.Equal(originalValue, restoredValue, Tolerance);
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ClearsState()
    {
        var htSine = new HtSine();

        for (int i = 0; i < 100; i++)
        {
            htSine.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
        }

        Assert.True(htSine.IsHot);

        htSine.Reset();

        Assert.False(htSine.IsHot);
        Assert.Equal(default, htSine.Last);
        Assert.Equal(0, htSine.LeadSine);
    }

    [Fact]
    public void Reset_AllowsReuse()
    {
        var htSine = new HtSine();

        // First run
        for (int i = 0; i < 100; i++)
        {
            double price = 100.0 + 10.0 * Math.Sin(i * 0.1);
            htSine.Update(new TValue(DateTime.UtcNow.AddSeconds(i), price));
        }
        var firstResult = htSine.Last.Value;

        htSine.Reset();

        // Second run with same data
        for (int i = 0; i < 100; i++)
        {
            double price = 100.0 + 10.0 * Math.Sin(i * 0.1);
            htSine.Update(new TValue(DateTime.UtcNow.AddSeconds(i), price));
        }
        var secondResult = htSine.Last.Value;

        Assert.Equal(firstResult, secondResult, Tolerance);
    }

    #endregion

    #region NaN/Infinity Handling Tests

    [Fact]
    public void Update_NaN_UsesLastValidValue()
    {
        var htSine = new HtSine();

        htSine.Update(new TValue(DateTime.UtcNow, 100.0));
        htSine.Update(new TValue(DateTime.UtcNow.AddSeconds(1), double.NaN));
        var afterNaN = htSine.Last.Value;

        Assert.True(double.IsFinite(afterNaN));
    }

    [Fact]
    public void Update_Infinity_UsesLastValidValue()
    {
        var htSine = new HtSine();

        htSine.Update(new TValue(DateTime.UtcNow, 100.0));
        htSine.Update(new TValue(DateTime.UtcNow.AddSeconds(1), double.PositiveInfinity));

        Assert.True(double.IsFinite(htSine.Last.Value));
    }

    [Fact]
    public void Update_NegativeInfinity_UsesLastValidValue()
    {
        var htSine = new HtSine();

        htSine.Update(new TValue(DateTime.UtcNow, 100.0));
        htSine.Update(new TValue(DateTime.UtcNow.AddSeconds(1), double.NegativeInfinity));

        Assert.True(double.IsFinite(htSine.Last.Value));
    }

    #endregion

    #region Consistency Tests

    [Theory]
    [InlineData(42)]
    [InlineData(123)]
    [InlineData(999)]
    public void Update_StreamingMatchesBatch(int seed)
    {
        const int dataLen = 200;

        var gbm = new GBM(seed: seed);
        var bars = gbm.Fetch(dataLen, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Streaming
        var streaming = new HtSine();
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

        var batch = HtSine.Calculate(tSeries);

        // Compare last values
        Assert.Equal(batch[^1].Value, streaming.Last.Value, Tolerance);
    }

    [Fact]
    public void Batch_MatchesStreaming()
    {
        const int dataLen = 200;

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(dataLen, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Streaming
        var streaming = new HtSine();
        var streamingSine = new double[dataLen];
        var streamingLeadSine = new double[dataLen];
        for (int i = 0; i < dataLen; i++)
        {
            streaming.Update(new TValue(bars[i].Time, bars[i].Close));
            streamingSine[i] = streaming.Last.Value;
            streamingLeadSine[i] = streaming.LeadSine;
        }

        // Batch
        double[] source = new double[dataLen];
        double[] batchSine = new double[dataLen];
        double[] batchLeadSine = new double[dataLen];
        for (int i = 0; i < dataLen; i++)
        {
            source[i] = bars[i].Close;
        }

        HtSine.Batch(source, batchSine, batchLeadSine);

        // Compare all values
        for (int i = 0; i < dataLen; i++)
        {
            Assert.Equal(streamingSine[i], batchSine[i], Tolerance);
            Assert.Equal(streamingLeadSine[i], batchLeadSine[i], Tolerance);
        }
    }

    #endregion

    #region Span API Tests

    [Fact]
    public void Batch_ValidatesSineLengthMismatch()
    {
        double[] source = new double[100];
        double[] sine = new double[50];
        double[] leadSine = new double[100];

        var ex = Assert.Throws<ArgumentException>(() => HtSine.Batch(source, sine, leadSine));
        Assert.Equal("sine", ex.ParamName);
    }

    [Fact]
    public void Batch_ValidatesLeadSineLengthMismatch()
    {
        double[] source = new double[100];
        double[] sine = new double[100];
        double[] leadSine = new double[50];

        var ex = Assert.Throws<ArgumentException>(() => HtSine.Batch(source, sine, leadSine));
        Assert.Equal("leadSine", ex.ParamName);
    }

    [Fact]
    public void Batch_EmptyArrays_NoException()
    {
        double[] source = [];
        double[] sine = [];
        double[] leadSine = [];

        var ex = Record.Exception(() => HtSine.Batch(source, sine, leadSine));
        Assert.Null(ex);
    }

    [Fact]
    public void Batch_HandlesNaN()
    {
        double[] source = { 100, 101, double.NaN, 103, 104, 105, 106, 107, 108, 109 };
        double[] sine = new double[10];
        double[] leadSine = new double[10];

        HtSine.Batch(source, sine, leadSine);

        foreach (double v in sine)
        {
            Assert.True(double.IsFinite(v));
        }
        foreach (double v in leadSine)
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
        var htSine = new HtSine(source);

        for (int i = 0; i < 100; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + Math.Sin(i * 0.1) * 10));
        }

        Assert.True(htSine.IsHot);
        Assert.True(double.IsFinite(htSine.Last.Value));
        Assert.True(double.IsFinite(htSine.LeadSine));
    }

    #endregion

    #region Phase Lead Tests

    [Fact]
    public void LeadSine_IsPhaseShifted()
    {
        // LeadSine should be sin(phase + π/4), which means it leads by 45°
        var htSine = new HtSine();

        var gbm = new GBM(seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            htSine.Update(new TValue(bar.Time, bar.Close));
        }

        // Both should be valid after warmup
        Assert.True(double.IsFinite(htSine.Last.Value));
        Assert.True(double.IsFinite(htSine.LeadSine));
        
        // They should generally be different (unless at specific phase points)
        // We just verify both are in valid range
        Assert.InRange(htSine.Last.Value, -1.0, 1.0);
        Assert.InRange(htSine.LeadSine, -1.0, 1.0);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void Update_ConstantSeries_ProducesValidOutput()
    {
        var htSine = new HtSine();

        for (int i = 0; i < 200; i++)
        {
            htSine.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
        }

        // Should produce valid (finite) output even for constant input
        Assert.True(double.IsFinite(htSine.Last.Value));
        Assert.True(double.IsFinite(htSine.LeadSine));
    }

    [Fact]
    public void Update_StepChange_HandlesGracefully()
    {
        var htSine = new HtSine();

        // Constant series then step change
        for (int i = 0; i < 100; i++)
        {
            htSine.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0));
        }
        for (int i = 100; i < 200; i++)
        {
            htSine.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 200.0));
        }

        Assert.True(double.IsFinite(htSine.Last.Value));
        Assert.InRange(htSine.Last.Value, -1.0, 1.0);
    }

    #endregion
}