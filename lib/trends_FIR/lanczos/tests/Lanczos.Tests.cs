namespace QuanTAlib.Tests;

public class LanczosTests
{
    private const int DefaultPeriod = 14;
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
        var ex = Assert.Throws<ArgumentException>(() => new Lanczos(period));
        Assert.Equal("period", ex.ParamName);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(14)]
    [InlineData(100)]
    public void Constructor_ValidPeriod_Succeeds(int period)
    {
        var lanczos = new Lanczos(period);
        Assert.Contains(period.ToString(System.Globalization.CultureInfo.InvariantCulture), lanczos.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_DefaultName()
    {
        var lanczos = new Lanczos(14);
        Assert.Equal("Lanczos(14)", lanczos.Name);
    }

    [Fact]
    public void Constructor_NullSource_Throws()
    {
        Assert.Throws<NullReferenceException>(() => new Lanczos(null!, DefaultPeriod));
    }

    // ── B) Basic calculation ───────────────────────────────────────────

    [Fact]
    public void Update_ReturnsTValue()
    {
        var lanczos = new Lanczos(DefaultPeriod);
        var result = lanczos.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.IsType<TValue>(result);
    }

    [Fact]
    public void Last_IsAccessible()
    {
        var lanczos = new Lanczos(DefaultPeriod);
        lanczos.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.True(double.IsFinite(lanczos.Last.Value));
    }

    [Fact]
    public void Name_IsCorrect()
    {
        var lanczos = new Lanczos(20);
        Assert.Equal("Lanczos(20)", lanczos.Name);
    }

    [Fact]
    public void Update_ReturnsFiniteValue()
    {
        var lanczos = new Lanczos(DefaultPeriod);
        foreach (var tv in _data)
        {
            var result = lanczos.Update(tv);
            Assert.True(double.IsFinite(result.Value));
        }
    }

    // ── C) State + bar correction ──────────────────────────────────────

    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var lanczos = new Lanczos(5);
        var now = DateTime.UtcNow;
        lanczos.Update(new TValue(now, 10.0), isNew: true);
        lanczos.Update(new TValue(now.AddMinutes(1), 20.0), isNew: true);
        Assert.True(double.IsFinite(lanczos.Last.Value));
    }

    [Fact]
    public void IsNew_False_DoesNotAdvanceBuffer()
    {
        // The Lanczos sinc window has zero weight at the newest bar position
        // (since sinc(1)=0 at k=period-1), so we test that isNew=false does not
        // advance the buffer by verifying state is preserved after correction.
        var lanczos = new Lanczos(7);
        var src = MakeSeries(20);

        for (int i = 0; i < src.Count; i++)
        {
            lanczos.Update(src[i], isNew: true);
        }

        double original = lanczos.Last.Value;

        // Multiple corrections should not change the final result when
        // we restore the original value
        lanczos.Update(new TValue(src[src.Count - 1].Time, 500.0), isNew: false);
        lanczos.Update(new TValue(src[src.Count - 1].Time, src[src.Count - 1].Value), isNew: false);

        Assert.Equal(original, lanczos.Last.Value, Epsilon);
    }

    [Fact]
    public void IterativeCorrections_Restore()
    {
        var lanczos = new Lanczos(14);
        var src = MakeSeries(30);

        for (int i = 0; i < src.Count; i++)
        {
            lanczos.Update(src[i], isNew: true);
        }

        double original = lanczos.Last.Value;

        for (int c = 0; c < 5; c++)
        {
            lanczos.Update(new TValue(src[src.Count - 1].Time, 200.0 + c), isNew: false);
        }

        // Restore original value
        lanczos.Update(new TValue(src[src.Count - 1].Time, src[src.Count - 1].Value), isNew: false);
        Assert.Equal(original, lanczos.Last.Value, Epsilon);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var lanczos = new Lanczos(DefaultPeriod);
        foreach (var tv in _data)
        {
            lanczos.Update(tv);
        }

        lanczos.Reset();
        Assert.False(lanczos.IsHot);
    }

    // ── D) Warmup/convergence ──────────────────────────────────────────

    [Fact]
    public void IsHot_FlipsAtPeriod()
    {
        var lanczos = new Lanczos(5);
        for (int i = 0; i < 4; i++)
        {
            lanczos.Update(new TValue(DateTime.UtcNow, 100.0 + i));
            Assert.False(lanczos.IsHot);
        }
        lanczos.Update(new TValue(DateTime.UtcNow, 105.0));
        Assert.True(lanczos.IsHot);
    }

    [Fact]
    public void WarmupPeriod_MatchesPeriod()
    {
        var lanczos = new Lanczos(10);
        Assert.Equal(10, lanczos.WarmupPeriod);
    }

    // ── E) Robustness ──────────────────────────────────────────────────

    [Fact]
    public void NaN_UsesLastValidValue()
    {
        var lanczos = new Lanczos(5);
        for (int i = 0; i < 5; i++)
        {
            lanczos.Update(new TValue(DateTime.UtcNow, 100.0));
        }

        lanczos.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(lanczos.Last.Value));
    }

    [Fact]
    public void Infinity_UsesLastValidValue()
    {
        var lanczos = new Lanczos(5);
        for (int i = 0; i < 5; i++)
        {
            lanczos.Update(new TValue(DateTime.UtcNow, 100.0));
        }

        lanczos.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(lanczos.Last.Value));
    }

    [Fact]
    public void BatchNaN_Safe()
    {
        var lanczos = new Lanczos(5);
        var src = MakeSeries(50);
        var result = lanczos.Update(src);
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
        var src = MakeSeries(100);

        // Streaming
        var streaming = new Lanczos(period);
        var streamResults = new double[src.Count];
        for (int i = 0; i < src.Count; i++)
        {
            streamResults[i] = streaming.Update(src[i]).Value;
        }

        // Batch (TSeries)
        var batchResults = Lanczos.Batch(src, period);

        // Span
        var spanOutput = new double[src.Count];
        Lanczos.Batch(src.Values, spanOutput, period);

        // Event-based
        var publisher = new TSeries();
        var eventLanczos = new Lanczos(publisher, period);
        var eventResults = new double[src.Count];
        for (int i = 0; i < src.Count; i++)
        {
            publisher.Add(src[i], isNew: true);
            eventResults[i] = eventLanczos.Last.Value;
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
        var ex = Assert.Throws<ArgumentException>(() => Lanczos.Batch(src, output, 5));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_PeriodTooSmall_Throws()
    {
        var src = new double[10];
        var output = new double[10];
        var ex = Assert.Throws<ArgumentException>(() => Lanczos.Batch(src, output, 1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_EmptyInput_NoOp()
    {
        var src = ReadOnlySpan<double>.Empty;
        var output = Span<double>.Empty;
        Lanczos.Batch(src, output, 5);
        Assert.True(true);
    }

    // ── H) Chainability ────────────────────────────────────────────────

    [Fact]
    public void Pub_Fires()
    {
        var lanczos = new Lanczos(5);
        int count = 0;
        lanczos.Pub += (object? _, in TValueEventArgs e) => count++;
        lanczos.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(1, count);
    }

    [Fact]
    public void EventBased_Chaining()
    {
        var source = new TSeries();
        using var lanczos = new Lanczos(source, 5);

        source.Add(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        Assert.True(double.IsFinite(lanczos.Last.Value));
    }

    [Fact]
    public void Dispose_UnsubscribesFromSource()
    {
        var source = new TSeries();
        var lanczos = new Lanczos(source, 5);
        lanczos.Dispose();

        source.Add(new TValue(DateTime.UtcNow, 100.0), isNew: true);
        Assert.Equal(default, lanczos.Last);
    }

    [Fact]
    public void Dispose_Idempotent()
    {
        var lanczos = new Lanczos(5);
        lanczos.Dispose();
        lanczos.Dispose();
        Assert.True(true);
    }

    // ── I) Lanczos-specific: sinc properties ───────────────────────────

    [Fact]
    public void ConstantInput_ReturnsConstant()
    {
        var lanczos = new Lanczos(7);
        for (int i = 0; i < 20; i++)
        {
            lanczos.Update(new TValue(DateTime.UtcNow, 42.0));
        }
        Assert.Equal(42.0, lanczos.Last.Value, 1e-10);
    }

    [Fact]
    public void Period2_ReducesToSma()
    {
        // For period=2: k=0 -> x = -1, k=1 -> x = 1
        // sinc(-1) = sinc(1) = 0, both weights zero -> normalization makes them equal -> SMA(2)
        int period = 2;
        var lanczos = new Lanczos(period);
        var sma = new Sma(period);

        var src = MakeSeries(50);
        for (int i = 0; i < src.Count; i++)
        {
            lanczos.Update(src[i]);
            sma.Update(src[i]);
        }

        Assert.Equal(sma.Last.Value, lanczos.Last.Value, 1e-8);
    }

    [Fact]
    public void LargerPeriod_SmoothsMore()
    {
        var src = MakeSeries(200);

        var smallPeriod = new Lanczos(5);
        var largePeriod = new Lanczos(20);

        double sumDiffSmall = 0;
        double sumDiffLarge = 0;
        int countSmall = 0;
        int countLarge = 0;

        for (int i = 0; i < src.Count; i++)
        {
            double raw = src[i].Value;
            smallPeriod.Update(src[i]);
            largePeriod.Update(src[i]);

            if (smallPeriod.IsHot)
            {
                sumDiffSmall += Math.Abs(raw - smallPeriod.Last.Value);
                countSmall++;
            }
            if (largePeriod.IsHot)
            {
                sumDiffLarge += Math.Abs(raw - largePeriod.Last.Value);
                countLarge++;
            }
        }

        double avgDiffSmall = sumDiffSmall / countSmall;
        double avgDiffLarge = sumDiffLarge / countLarge;

        // Larger period should smooth more (larger avg deviation from raw)
        Assert.True(avgDiffLarge > avgDiffSmall);
    }

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var (results, indicator) = Lanczos.Calculate(_data, 14);
        Assert.Equal(_data.Count, results.Count);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void Prime_SetsState()
    {
        var lanczos = new Lanczos(5);
        var src = MakeSeries(20);
        lanczos.Prime(src.Values);
        Assert.True(lanczos.IsHot);
    }
}
