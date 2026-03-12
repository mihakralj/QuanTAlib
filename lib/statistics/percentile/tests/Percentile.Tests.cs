namespace QuanTAlib.Tests;

public class PercentileTests
{
    [Fact]
    public void Constructor_ValidParameters_NoThrow()
    {
        var p = new Percentile(10, 25.0);
        Assert.Equal("Percentile(10,25)", p.Name);
    }

    [Fact]
    public void Constructor_PeriodZero_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Percentile(0, 50.0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePercent_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Percentile(10, -1.0));
        Assert.Equal("percent", ex.ParamName);
    }

    [Fact]
    public void Constructor_PercentOver100_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Percentile(10, 101.0));
        Assert.Equal("percent", ex.ParamName);
    }

    [Fact]
    public void Percentile50_MatchesMedian_OddPeriod()
    {
        // {1, 2, 3, 4, 5} → median = 3
        // rank = (50/100)*(5-1) = 2.0 → sorted[2] = 3
        var p = new Percentile(5, 50.0);
        for (int i = 1; i <= 5; i++)
        {
            p.Update(new TValue(DateTime.UtcNow, i));
        }
        Assert.Equal(3.0, p.Last.Value);
    }

    [Fact]
    public void Percentile50_MatchesMedian_EvenPeriod()
    {
        // {1, 2, 3, 4} → rank = (50/100)*(4-1) = 1.5
        // sorted[1]=2, sorted[2]=3 → 2 + 0.5*(3-2) = 2.5
        var p = new Percentile(4, 50.0);
        for (int i = 1; i <= 4; i++)
        {
            p.Update(new TValue(DateTime.UtcNow, i));
        }
        Assert.Equal(2.5, p.Last.Value);
    }

    [Fact]
    public void Percentile0_ReturnsMinimum()
    {
        var p = new Percentile(5, 0.0);
        p.Update(new TValue(DateTime.UtcNow, 10));
        p.Update(new TValue(DateTime.UtcNow, 20));
        p.Update(new TValue(DateTime.UtcNow, 5));
        p.Update(new TValue(DateTime.UtcNow, 30));
        p.Update(new TValue(DateTime.UtcNow, 15));
        Assert.Equal(5.0, p.Last.Value);
    }

    [Fact]
    public void Percentile100_ReturnsMaximum()
    {
        var p = new Percentile(5, 100.0);
        p.Update(new TValue(DateTime.UtcNow, 10));
        p.Update(new TValue(DateTime.UtcNow, 20));
        p.Update(new TValue(DateTime.UtcNow, 5));
        p.Update(new TValue(DateTime.UtcNow, 30));
        p.Update(new TValue(DateTime.UtcNow, 15));
        Assert.Equal(30.0, p.Last.Value);
    }

    [Fact]
    public void Percentile25_LinearInterpolation()
    {
        // {1, 2, 3, 4, 5} sorted → rank = (25/100)*(5-1) = 1.0 → sorted[1] = 2
        var p = new Percentile(5, 25.0);
        for (int i = 1; i <= 5; i++)
        {
            p.Update(new TValue(DateTime.UtcNow, i));
        }
        Assert.Equal(2.0, p.Last.Value);
    }

    [Fact]
    public void Percentile75_LinearInterpolation()
    {
        // {1, 2, 3, 4, 5} sorted → rank = (75/100)*(5-1) = 3.0 → sorted[3] = 4
        var p = new Percentile(5, 75.0);
        for (int i = 1; i <= 5; i++)
        {
            p.Update(new TValue(DateTime.UtcNow, i));
        }
        Assert.Equal(4.0, p.Last.Value);
    }

    [Fact]
    public void SingleValue_ReturnsItself()
    {
        var p = new Percentile(1, 50.0);
        p.Update(new TValue(DateTime.UtcNow, 42.0));
        Assert.Equal(42.0, p.Last.Value);
    }

    [Fact]
    public void IsHot_FlipsAtPeriod()
    {
        var p = new Percentile(5, 50.0);
        for (int i = 1; i <= 4; i++)
        {
            p.Update(new TValue(DateTime.UtcNow, i * 10));
            Assert.False(p.IsHot);
        }
        p.Update(new TValue(DateTime.UtcNow, 50));
        Assert.True(p.IsHot);
    }

    [Fact]
    public void Update_IsNewFalse_CorrectsBar()
    {
        var p = new Percentile(5, 50.0);
        // {1, 2, 3, 4, 5} → p50 = 3
        for (int i = 1; i <= 5; i++)
        {
            p.Update(new TValue(DateTime.UtcNow, i));
        }
        Assert.Equal(3.0, p.Last.Value);

        // Correct last bar to 1 → {1, 2, 3, 4, 1} sorted {1,1,2,3,4} → rank=2 → sorted[2]=2
        p.Update(new TValue(DateTime.UtcNow, 1), isNew: false);
        Assert.Equal(2.0, p.Last.Value);
    }

    [Fact]
    public void BarCorrection_RestoreToOriginal()
    {
        var p = new Percentile(5, 50.0);
        // {10, 20, 30, 40, 50} → p50: rank=2 → 30
        p.Update(new TValue(DateTime.UtcNow, 10));
        p.Update(new TValue(DateTime.UtcNow, 20));
        p.Update(new TValue(DateTime.UtcNow, 30));
        p.Update(new TValue(DateTime.UtcNow, 40));
        p.Update(new TValue(DateTime.UtcNow, 50));
        double original = p.Last.Value;
        Assert.Equal(30.0, original);

        // Correct to 5 → {10, 20, 30, 40, 5} sorted {5,10,20,30,40} → p50=20
        p.Update(new TValue(DateTime.UtcNow, 5), isNew: false);
        Assert.NotEqual(original, p.Last.Value);
        Assert.Equal(20.0, p.Last.Value);

        // Correct back to 50
        var result = p.Update(new TValue(DateTime.UtcNow, 50), isNew: false);
        Assert.Equal(original, result.Value);
    }

    [Fact]
    public void NaN_Input_UsesLastValid()
    {
        var p = new Percentile(3, 50.0);
        p.Update(new TValue(DateTime.UtcNow, 10));
        p.Update(new TValue(DateTime.UtcNow, 20));
        p.Update(new TValue(DateTime.UtcNow, 30));

        // NaN should substitute last valid (30) → buffer gets {20, 30, 30} after sliding
        p.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(p.Last.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValid()
    {
        var p = new Percentile(3, 50.0);
        p.Update(new TValue(DateTime.UtcNow, 10));
        p.Update(new TValue(DateTime.UtcNow, 20));
        p.Update(new TValue(DateTime.UtcNow, 30));

        p.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(p.Last.Value));
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var p = new Percentile(5, 50.0);
        for (int i = 1; i <= 10; i++)
        {
            p.Update(new TValue(DateTime.UtcNow, i));
        }
        Assert.True(p.IsHot);

        p.Reset();
        Assert.False(p.IsHot);
        Assert.Equal(default, p.Last);
    }

    [Fact]
    public void BatchCalc_MatchesStreaming()
    {
        var rng = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var source = new TSeries();
        for (int i = 0; i < 100; i++)
        {
            source.Add(rng.Next());
        }
        int period = 14;
        double percent = 25.0;

        // Streaming
        var indicator = new Percentile(period, percent);
        var streamingResults = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streamingResults[i] = indicator.Update(new TValue(source.Times[i], source.Values[i])).Value;
        }

        // Batch
        var batchSeries = Percentile.Batch(source, period, percent);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(streamingResults[i], batchSeries.Values[i], precision: 10);
        }
    }

    [Fact]
    public void SpanBatch_MatchesStreaming()
    {
        var rng = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 241);
        var source = new TSeries();
        for (int i = 0; i < 100; i++)
        {
            source.Add(rng.Next());
        }
        int period = 14;
        double percent = 75.0;

        // Streaming
        var indicator = new Percentile(period, percent);
        var streamingResults = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streamingResults[i] = indicator.Update(new TValue(source.Times[i], source.Values[i])).Value;
        }

        // Span batch
        var spanOutput = new double[source.Count];
        Percentile.Batch(source.Values, spanOutput.AsSpan(), period, percent);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(streamingResults[i], spanOutput[i], precision: 10);
        }
    }

    [Fact]
    public void SpanBatch_LengthMismatch_Throws()
    {
        var source = new double[] { 1, 2, 3, 4, 5 };
        var output = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Percentile.Batch(source.AsSpan(), output.AsSpan(), 5, 50.0));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void SpanBatch_PeriodZero_Throws()
    {
        var source = new double[] { 1, 2, 3 };
        var output = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Percentile.Batch(source.AsSpan(), output.AsSpan(), 0, 50.0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void SpanBatch_PercentOutOfRange_Throws()
    {
        var source = new double[] { 1, 2, 3 };
        var output = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Percentile.Batch(source.AsSpan(), output.AsSpan(), 3, 101.0));
        Assert.Equal("percent", ex.ParamName);
    }

    [Fact]
    public void SpanBatch_EmptyInput_NoException()
    {
        Span<double> source = [];
        Span<double> output = [];
        Percentile.Batch(source, output, 5, 50.0);
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public void SpanBatch_LargeData_NoStackOverflow()
    {
        var rng = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 309);
        var source = new double[10_000];
        for (int i = 0; i < source.Length; i++)
        {
            source[i] = rng.Next().Close;
        }
        var output = new double[source.Length];
        Percentile.Batch(source.AsSpan(), output.AsSpan(), 50, 50.0);
        Assert.True(double.IsFinite(output[^1]));
    }

    [Fact]
    public void Chaining_PubEventFires()
    {
        var p = new Percentile(5, 50.0);
        int eventCount = 0;
        p.Pub += (object? _, in TValueEventArgs e) => eventCount++;

        for (int i = 1; i <= 10; i++)
        {
            p.Update(new TValue(DateTime.UtcNow, i));
        }
        Assert.Equal(10, eventCount);
    }

    [Fact]
    public void ConstantValues_ReturnsConstant()
    {
        var p = new Percentile(5, 25.0);
        for (int i = 0; i < 10; i++)
        {
            p.Update(new TValue(DateTime.UtcNow, 42.0));
        }
        Assert.Equal(42.0, p.Last.Value);
    }

    [Fact]
    public void SlidingWindow_CorrectlyDropsOldest()
    {
        var p = new Percentile(3, 50.0);
        // {100} → 100
        p.Update(new TValue(DateTime.UtcNow, 100));
        // {100, 200} → rank=0.5 → 100 + 0.5*100 = 150
        p.Update(new TValue(DateTime.UtcNow, 200));
        // {100, 200, 300} → rank=1 → 200
        p.Update(new TValue(DateTime.UtcNow, 300));
        Assert.Equal(200.0, p.Last.Value);

        // {200, 300, 400} → rank=1 → 300
        p.Update(new TValue(DateTime.UtcNow, 400));
        Assert.Equal(300.0, p.Last.Value);
    }
}
