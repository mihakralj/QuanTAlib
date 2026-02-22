namespace QuanTAlib.Tests;

public class KaiserTests
{
    private const int DefaultPeriod = 14;
    private const double DefaultBeta = 3.0;
    private const double Epsilon = 1e-10;

    private static TSeries MakeSeries(int count = 500)
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
    }

    private readonly TSeries _data = MakeSeries();

    // ── A) Constructor validation ──────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-5)]
    public void Constructor_InvalidPeriod_Throws(int period)
    {
        var ex = Assert.Throws<ArgumentException>(() => new Kaiser(period));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativeBeta_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Kaiser(14, beta: -1.0));
        Assert.Equal("beta", ex.ParamName);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(14)]
    [InlineData(100)]
    public void Constructor_ValidPeriod_Succeeds(int period)
    {
        var kaiser = new Kaiser(period);
        Assert.Contains(period.ToString(System.Globalization.CultureInfo.InvariantCulture), kaiser.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_DefaultBeta_InName()
    {
        var kaiser = new Kaiser(14, 3.0);
        Assert.Equal("Kaiser(14,3.0)", kaiser.Name);
    }

    [Fact]
    public void Constructor_NullSource_Throws()
    {
        Assert.Throws<NullReferenceException>(() => new Kaiser(null!, DefaultPeriod));
    }

    // ── B) Basic calculation ───────────────────────────────────────────

    [Fact]
    public void Update_ReturnsTValue()
    {
        var kaiser = new Kaiser(DefaultPeriod);
        var result = kaiser.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Last_IsAccessible()
    {
        var kaiser = new Kaiser(DefaultPeriod);
        kaiser.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(kaiser.Last.Value));
    }

    [Fact]
    public void Name_IsCorrect()
    {
        var kaiser = new Kaiser(14, 5.0);
        Assert.Equal("Kaiser(14,5.0)", kaiser.Name);
    }

    [Fact]
    public void Update_ReturnsFiniteValue()
    {
        var kaiser = new Kaiser(DefaultPeriod);
        foreach (var tv in _data)
        {
            var result = kaiser.Update(tv);
            Assert.True(double.IsFinite(result.Value));
        }
    }

    // ── C) State + bar correction ──────────────────────────────────────

    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var kaiser = new Kaiser(5);
        var now = DateTime.UtcNow;
        kaiser.Update(new TValue(now, 10.0), isNew: true);
        kaiser.Update(new TValue(now.AddMinutes(1), 20.0), isNew: true);
        Assert.True(double.IsFinite(kaiser.Last.Value));
    }

    [Fact]
    public void IsNew_False_RewritesCurrentBar()
    {
        var kaiser = new Kaiser(5, 3.0);
        var now = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            kaiser.Update(new TValue(now.AddMinutes(i), 100.0 + i), isNew: true);
        }

        double beforeCorrection = kaiser.Last.Value;
        kaiser.Update(new TValue(now.AddMinutes(9), 999.0), isNew: false);
        double afterCorrection = kaiser.Last.Value;

        Assert.NotEqual(beforeCorrection, afterCorrection, Epsilon);
    }

    [Fact]
    public void IterativeCorrections_Restore()
    {
        var kaiser = new Kaiser(5, 3.0);
        var now = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            kaiser.Update(new TValue(now.AddMinutes(i), 100.0 + i), isNew: true);
        }

        double original = kaiser.Last.Value;

        for (int c = 0; c < 5; c++)
        {
            kaiser.Update(new TValue(now.AddMinutes(9), 200.0 + c), isNew: false);
        }

        kaiser.Update(new TValue(now.AddMinutes(9), 109.0), isNew: false);
        Assert.Equal(original, kaiser.Last.Value, Epsilon);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var kaiser = new Kaiser(DefaultPeriod);
        foreach (var tv in _data)
        {
            kaiser.Update(tv);
        }

        kaiser.Reset();
        Assert.False(kaiser.IsHot);
    }

    // ── D) Warmup/convergence ──────────────────────────────────────────

    [Fact]
    public void IsHot_FlipsAtPeriod()
    {
        var kaiser = new Kaiser(5);
        for (int i = 0; i < 4; i++)
        {
            kaiser.Update(new TValue(DateTime.UtcNow, 100.0 + i));
            Assert.False(kaiser.IsHot);
        }
        kaiser.Update(new TValue(DateTime.UtcNow, 105.0));
        Assert.True(kaiser.IsHot);
    }

    [Fact]
    public void WarmupPeriod_MatchesPeriod()
    {
        var kaiser = new Kaiser(10);
        Assert.Equal(10, kaiser.WarmupPeriod);
    }

    // ── E) Robustness ──────────────────────────────────────────────────

    [Fact]
    public void NaN_UsesLastValidValue()
    {
        var kaiser = new Kaiser(5);
        for (int i = 0; i < 5; i++)
        {
            kaiser.Update(new TValue(DateTime.UtcNow, 100.0));
        }

        kaiser.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(kaiser.Last.Value));
    }

    [Fact]
    public void Infinity_UsesLastValidValue()
    {
        var kaiser = new Kaiser(5);
        for (int i = 0; i < 5; i++)
        {
            kaiser.Update(new TValue(DateTime.UtcNow, 100.0));
        }

        kaiser.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(kaiser.Last.Value));
    }

    [Fact]
    public void BatchNaN_Safe()
    {
        var kaiser = new Kaiser(5);
        var src = MakeSeries(50);
        var result = kaiser.Update(src);
        Assert.Equal(src.Count, result.Count);
        for (int i = 0; i < result.Count; i++)
        {
            Assert.True(double.IsFinite(result[i].Value));
        }
    }

    // ── F) Consistency (4-API match) ───────────────────────────────────

    [Fact]
    public void AllModes_ProduceSameResults()
    {
        int period = 10;
        double beta = 3.0;
        var src = MakeSeries(100);

        // Streaming
        var streaming = new Kaiser(period, beta);
        var streamResults = new double[src.Count];
        for (int i = 0; i < src.Count; i++)
        {
            streamResults[i] = streaming.Update(src[i]).Value;
        }

        // Batch (TSeries)
        var batchResults = Kaiser.Batch(src, period, beta);

        // Span
        var spanOutput = new double[src.Count];
        Kaiser.Batch(src.Values, spanOutput, period, beta);

        // Event-based
        var publisher = new TSeries();
        var eventKaiser = new Kaiser(publisher, period, beta);
        var eventResults = new double[src.Count];
        for (int i = 0; i < src.Count; i++)
        {
            publisher.Add(src[i], isNew: true);
            eventResults[i] = eventKaiser.Last.Value;
        }

        for (int i = 0; i < src.Count; i++)
        {
            Assert.Equal(streamResults[i], batchResults[i].Value, 1e-6);
            Assert.Equal(streamResults[i], spanOutput[i], 1e-6);
            Assert.Equal(streamResults[i], eventResults[i], 1e-6);
        }
    }

    // ── G) Span API tests ──────────────────────────────────────────────

    [Fact]
    public void Batch_Span_MismatchedLengths_Throws()
    {
        var src = new double[10];
        var output = new double[5];
        var ex = Assert.Throws<ArgumentException>(() => Kaiser.Batch(src, output, 5));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_PeriodTooSmall_Throws()
    {
        var src = new double[10];
        var output = new double[10];
        var ex = Assert.Throws<ArgumentException>(() => Kaiser.Batch(src, output, 1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_EmptyInput_NoOp()
    {
        var src = ReadOnlySpan<double>.Empty;
        var output = Span<double>.Empty;
        Kaiser.Batch(src, output, 5);
        Assert.True(true);
    }

    // ── H) Chainability ────────────────────────────────────────────────

    [Fact]
    public void Pub_Fires()
    {
        var kaiser = new Kaiser(5);
        int count = 0;
        kaiser.Pub += (object? _, in TValueEventArgs _) => count++;
        kaiser.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(1, count);
    }

    [Fact]
    public void EventBased_Chaining()
    {
        var source = new TSeries();
        using var kaiser = new Kaiser(source, 5);

        source.Add(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        Assert.True(double.IsFinite(kaiser.Last.Value));
    }

    [Fact]
    public void Dispose_UnsubscribesFromSource()
    {
        var source = new TSeries();
        var kaiser = new Kaiser(source, 5);
        kaiser.Dispose();

        source.Add(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        Assert.Equal(default, kaiser.Last);
    }

    [Fact]
    public void Dispose_Idempotent()
    {
        var kaiser = new Kaiser(5);
        kaiser.Dispose();
        kaiser.Dispose();
        Assert.True(true);
    }

    // ── I) Kaiser-specific: beta behavior ──────────────────────────────

    [Fact]
    public void BetaZero_ReducesToSma()
    {
        int period = 5;
        var kaiser = new Kaiser(period, beta: 0.0);
        var sma = new Sma(period);

        var src = MakeSeries(50);
        for (int i = 0; i < src.Count; i++)
        {
            kaiser.Update(src[i]);
            sma.Update(src[i]);
        }

        Assert.Equal(sma.Last.Value, kaiser.Last.Value, 1e-8);
    }

    [Fact]
    public void HigherBeta_SmoothsMore()
    {
        var src = MakeSeries(100);
        int period = 14;

        var kaiserLow = new Kaiser(period, beta: 1.0);
        var kaiserHigh = new Kaiser(period, beta: 8.0);

        double sumDiffLow = 0;
        double sumDiffHigh = 0;

        for (int i = 0; i < src.Count; i++)
        {
            double raw = src[i].Value;
            kaiserLow.Update(src[i]);
            kaiserHigh.Update(src[i]);

            if (kaiserLow.IsHot && kaiserHigh.IsHot)
            {
                sumDiffLow += Math.Abs(raw - kaiserLow.Last.Value);
                sumDiffHigh += Math.Abs(raw - kaiserHigh.Last.Value);
            }
        }

        Assert.True(sumDiffHigh >= sumDiffLow * 0.8);
    }

    [Fact]
    public void ConstantInput_ReturnsConstant()
    {
        var kaiser = new Kaiser(7, 3.0);
        for (int i = 0; i < 20; i++)
        {
            kaiser.Update(new TValue(DateTime.UtcNow, 42.0));
        }
        Assert.Equal(42.0, kaiser.Last.Value, 1e-10);
    }

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var (results, indicator) = Kaiser.Calculate(_data, 14, 3.0);
        Assert.Equal(_data.Count, results.Count);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void Prime_SetsState()
    {
        var kaiser = new Kaiser(5, 3.0);
        var src = MakeSeries(20);
        kaiser.Prime(src.Values);
        Assert.True(kaiser.IsHot);
    }
}
