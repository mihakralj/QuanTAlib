namespace QuanTAlib.Tests;

public class QuantileTests
{
    [Fact]
    public void Constructor_ValidParameters_NoThrow()
    {
        var q = new Quantile(10, 0.25);
        Assert.Equal("Quantile(10,0.25)", q.Name);
    }

    [Fact]
    public void Constructor_PeriodZero_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Quantile(0, 0.5));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativeQuantile_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Quantile(10, -0.01));
        Assert.Equal("quantileLevel", ex.ParamName);
    }

    [Fact]
    public void Constructor_QuantileOver1_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Quantile(10, 1.01));
        Assert.Equal("quantileLevel", ex.ParamName);
    }

    [Fact]
    public void Quantile50_MatchesMedian_OddPeriod()
    {
        // {1, 2, 3, 4, 5} → median = 3
        // rank = 0.5 * (5-1) = 2.0 → sorted[2] = 3
        var q = new Quantile(5, 0.5);
        for (int i = 1; i <= 5; i++)
        {
            q.Update(new TValue(DateTime.UtcNow, i));
        }
        Assert.Equal(3.0, q.Last.Value);
    }

    [Fact]
    public void Quantile50_MatchesMedian_EvenPeriod()
    {
        // {1, 2, 3, 4} → rank = 0.5 * (4-1) = 1.5
        // sorted[1]=2, sorted[2]=3 → 2 + 0.5*(3-2) = 2.5
        var q = new Quantile(4, 0.5);
        for (int i = 1; i <= 4; i++)
        {
            q.Update(new TValue(DateTime.UtcNow, i));
        }
        Assert.Equal(2.5, q.Last.Value);
    }

    [Fact]
    public void Quantile0_ReturnsMinimum()
    {
        var q = new Quantile(5, 0.0);
        q.Update(new TValue(DateTime.UtcNow, 10));
        q.Update(new TValue(DateTime.UtcNow, 20));
        q.Update(new TValue(DateTime.UtcNow, 5));
        q.Update(new TValue(DateTime.UtcNow, 30));
        q.Update(new TValue(DateTime.UtcNow, 15));
        Assert.Equal(5.0, q.Last.Value);
    }

    [Fact]
    public void Quantile1_ReturnsMaximum()
    {
        var q = new Quantile(5, 1.0);
        q.Update(new TValue(DateTime.UtcNow, 10));
        q.Update(new TValue(DateTime.UtcNow, 20));
        q.Update(new TValue(DateTime.UtcNow, 5));
        q.Update(new TValue(DateTime.UtcNow, 30));
        q.Update(new TValue(DateTime.UtcNow, 15));
        Assert.Equal(30.0, q.Last.Value);
    }

    [Fact]
    public void Quantile25_LinearInterpolation()
    {
        // {1, 2, 3, 4, 5} sorted → rank = 0.25 * (5-1) = 1.0 → sorted[1] = 2
        var q = new Quantile(5, 0.25);
        for (int i = 1; i <= 5; i++)
        {
            q.Update(new TValue(DateTime.UtcNow, i));
        }
        Assert.Equal(2.0, q.Last.Value);
    }

    [Fact]
    public void Quantile75_LinearInterpolation()
    {
        // {1, 2, 3, 4, 5} sorted → rank = 0.75 * (5-1) = 3.0 → sorted[3] = 4
        var q = new Quantile(5, 0.75);
        for (int i = 1; i <= 5; i++)
        {
            q.Update(new TValue(DateTime.UtcNow, i));
        }
        Assert.Equal(4.0, q.Last.Value);
    }

    [Fact]
    public void SingleValue_ReturnsItself()
    {
        var q = new Quantile(1, 0.5);
        q.Update(new TValue(DateTime.UtcNow, 42.0));
        Assert.Equal(42.0, q.Last.Value);
    }

    [Fact]
    public void IsHot_FlipsAtPeriod()
    {
        var q = new Quantile(5, 0.5);
        for (int i = 1; i <= 4; i++)
        {
            q.Update(new TValue(DateTime.UtcNow, i * 10));
            Assert.False(q.IsHot);
        }
        q.Update(new TValue(DateTime.UtcNow, 50));
        Assert.True(q.IsHot);
    }

    [Fact]
    public void Update_IsNewFalse_CorrectsBar()
    {
        var q = new Quantile(5, 0.5);
        // {1, 2, 3, 4, 5} → q50 = 3
        for (int i = 1; i <= 5; i++)
        {
            q.Update(new TValue(DateTime.UtcNow, i));
        }
        Assert.Equal(3.0, q.Last.Value);

        // Correct last bar to 1 → {1, 2, 3, 4, 1} sorted {1,1,2,3,4} → rank=2 → sorted[2]=2
        q.Update(new TValue(DateTime.UtcNow, 1), isNew: false);
        Assert.Equal(2.0, q.Last.Value);
    }

    [Fact]
    public void BarCorrection_RestoreToOriginal()
    {
        var q = new Quantile(5, 0.5);
        // {10, 20, 30, 40, 50} → q50: rank=2 → 30
        q.Update(new TValue(DateTime.UtcNow, 10));
        q.Update(new TValue(DateTime.UtcNow, 20));
        q.Update(new TValue(DateTime.UtcNow, 30));
        q.Update(new TValue(DateTime.UtcNow, 40));
        q.Update(new TValue(DateTime.UtcNow, 50));
        double original = q.Last.Value;
        Assert.Equal(30.0, original);

        // Correct to 5 → {10, 20, 30, 40, 5} sorted {5,10,20,30,40} → q50=20
        q.Update(new TValue(DateTime.UtcNow, 5), isNew: false);
        Assert.NotEqual(original, q.Last.Value);
        Assert.Equal(20.0, q.Last.Value);

        // Correct back to 50
        var result = q.Update(new TValue(DateTime.UtcNow, 50), isNew: false);
        Assert.Equal(original, result.Value);
    }

    [Fact]
    public void NaN_Input_UsesLastValid()
    {
        var q = new Quantile(3, 0.5);
        q.Update(new TValue(DateTime.UtcNow, 10));
        q.Update(new TValue(DateTime.UtcNow, 20));
        q.Update(new TValue(DateTime.UtcNow, 30));

        // NaN should substitute last valid (30) → buffer gets {20, 30, 30} after sliding
        q.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(q.Last.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValid()
    {
        var q = new Quantile(3, 0.5);
        q.Update(new TValue(DateTime.UtcNow, 10));
        q.Update(new TValue(DateTime.UtcNow, 20));
        q.Update(new TValue(DateTime.UtcNow, 30));

        q.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(q.Last.Value));
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var q = new Quantile(5, 0.5);
        for (int i = 1; i <= 10; i++)
        {
            q.Update(new TValue(DateTime.UtcNow, i));
        }
        Assert.True(q.IsHot);

        q.Reset();
        Assert.False(q.IsHot);
        Assert.Equal(default, q.Last);
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
        double quantileLevel = 0.25;

        // Streaming
        var indicator = new Quantile(period, quantileLevel);
        var streamingResults = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streamingResults[i] = indicator.Update(new TValue(source.Times[i], source.Values[i])).Value;
        }

        // Batch
        var batchSeries = Quantile.Batch(source, period, quantileLevel);

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
        double quantileLevel = 0.75;

        // Streaming
        var indicator = new Quantile(period, quantileLevel);
        var streamingResults = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streamingResults[i] = indicator.Update(new TValue(source.Times[i], source.Values[i])).Value;
        }

        // Span batch
        var spanOutput = new double[source.Count];
        Quantile.Batch(source.Values, spanOutput.AsSpan(), period, quantileLevel);

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
            Quantile.Batch(source.AsSpan(), output.AsSpan(), 5, 0.5));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void SpanBatch_PeriodZero_Throws()
    {
        var source = new double[] { 1, 2, 3 };
        var output = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Quantile.Batch(source.AsSpan(), output.AsSpan(), 0, 0.5));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void SpanBatch_QuantileOutOfRange_Throws()
    {
        var source = new double[] { 1, 2, 3 };
        var output = new double[3];
        var ex = Assert.Throws<ArgumentException>(() =>
            Quantile.Batch(source.AsSpan(), output.AsSpan(), 3, 1.01));
        Assert.Equal("quantileLevel", ex.ParamName);
    }

    [Fact]
    public void SpanBatch_EmptyInput_NoException()
    {
        Span<double> source = [];
        Span<double> output = [];
        Quantile.Batch(source, output, 5, 0.5);
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
        Quantile.Batch(source.AsSpan(), output.AsSpan(), 50, 0.5);
        Assert.True(double.IsFinite(output[^1]));
    }

    [Fact]
    public void Chaining_PubEventFires()
    {
        var q = new Quantile(5, 0.5);
        int eventCount = 0;
        q.Pub += (object? _, in TValueEventArgs e) => eventCount++;

        for (int i = 1; i <= 10; i++)
        {
            q.Update(new TValue(DateTime.UtcNow, i));
        }
        Assert.Equal(10, eventCount);
    }

    [Fact]
    public void ConstantValues_ReturnsConstant()
    {
        var q = new Quantile(5, 0.25);
        for (int i = 0; i < 10; i++)
        {
            q.Update(new TValue(DateTime.UtcNow, 42.0));
        }
        Assert.Equal(42.0, q.Last.Value);
    }

    [Fact]
    public void SlidingWindow_CorrectlyDropsOldest()
    {
        var q = new Quantile(3, 0.5);
        // {100} → 100
        q.Update(new TValue(DateTime.UtcNow, 100));
        // {100, 200} → rank=0.5 → 100 + 0.5*100 = 150
        q.Update(new TValue(DateTime.UtcNow, 200));
        // {100, 200, 300} → rank=1 → 200
        q.Update(new TValue(DateTime.UtcNow, 300));
        Assert.Equal(200.0, q.Last.Value);

        // {200, 300, 400} → rank=1 → 300
        q.Update(new TValue(DateTime.UtcNow, 400));
        Assert.Equal(300.0, q.Last.Value);
    }

    [Fact]
    public void FractionalInterpolation()
    {
        // {1, 2, 3, 4, 5, 6, 7, 8, 9, 10} → q=0.33
        // rank = 0.33 * 9 = 2.97 → lo=2, hi=3
        // sorted[2]=3, sorted[3]=4 → 3 + 0.97*(4-3) = 3.97
        var q = new Quantile(10, 0.33);
        for (int i = 1; i <= 10; i++)
        {
            q.Update(new TValue(DateTime.UtcNow, i));
        }
        Assert.Equal(3.97, q.Last.Value, precision: 10);
    }
}
