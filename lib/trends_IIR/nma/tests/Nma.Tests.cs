using Xunit;

namespace QuanTAlib.Tests;

public class NmaTests
{
    private const int DefaultPeriod = 40;
    private const double Tolerance = 1e-10;
    private const long Seed = 12345;
    private static readonly TimeSpan Step = TimeSpan.FromMinutes(1);

    private static TSeries GetTestSeries(int count = 500)
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(count, Seed, Step);
        return bars.Close;
    }

    // ── A) Constructor validation ──────────────────────────────────────

    [Fact]
    public void Constructor_PeriodZero_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Nma(0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_PeriodNegative_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Nma(-1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_PeriodOne_Valid()
    {
        var nma = new Nma(1);
        Assert.Equal("Nma(1)", nma.Name);
    }

    [Fact]
    public void Constructor_ValidPeriod_SetsName()
    {
        var nma = new Nma(DefaultPeriod);
        Assert.Equal($"Nma({DefaultPeriod})", nma.Name);
    }

    [Fact]
    public void Constructor_ValidPeriod_SetsWarmupPeriod()
    {
        var nma = new Nma(DefaultPeriod);
        Assert.Equal(DefaultPeriod, nma.WarmupPeriod);
    }

    // ── B) Basic calculation ───────────────────────────────────────────

    [Fact]
    public void Update_FirstBar_ReturnsPrice()
    {
        var nma = new Nma(DefaultPeriod);
        var result = nma.Update(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(100.0, result.Value);
    }

    [Fact]
    public void Update_ReturnsFiniteValues()
    {
        var nma = new Nma(DefaultPeriod);
        var series = GetTestSeries();
        foreach (var tv in series)
        {
            var result = nma.Update(tv);
            Assert.True(double.IsFinite(result.Value), $"Non-finite at {tv.Time}");
        }
    }

    [Fact]
    public void Update_Last_MatchesReturnValue()
    {
        var nma = new Nma(DefaultPeriod);
        var series = GetTestSeries(100);
        foreach (var tv in series)
        {
            var result = nma.Update(tv);
            Assert.Equal(result.Value, nma.Last.Value);
        }
    }

    // ── C) State + bar correction ──────────────────────────────────────

    [Fact]
    public void Update_IsNewTrue_AdvancesState()
    {
        var nma = new Nma(DefaultPeriod);
        var series = GetTestSeries(50);

        for (int i = 0; i < series.Count; i++)
        {
            nma.Update(series[i], isNew: true);
        }

        Assert.True(nma.IsHot);
    }

    [Fact]
    public void Update_IsNewFalse_CorrectionRestores()
    {
        var nma = new Nma(DefaultPeriod);
        var series = GetTestSeries(100);

        // Process 98 bars
        for (int i = 0; i < 98; i++)
        {
            nma.Update(series[i]);
        }

        // Correction path: isNew=true then multiple isNew=false
        nma.Update(new TValue(series[98].Time, series[98].Value), true);
        nma.Update(new TValue(series[98].Time, series[98].Value + 0.5), false);
        nma.Update(new TValue(series[98].Time, series[98].Value + 1.0), false);
        var corrected = nma.Update(new TValue(series[98].Time, series[98].Value + 1.5), false);

        // Clean path: same data in fresh indicator
        var nma2 = new Nma(DefaultPeriod);
        for (int i = 0; i < 98; i++)
        {
            nma2.Update(series[i]);
        }
        var expected = nma2.Update(new TValue(series[98].Time, series[98].Value + 1.5), true);

        Assert.Equal(expected.Value, corrected.Value, 1e-9);
    }

    [Fact]
    public void Update_IterativeCorrections_RestoresExactly()
    {
        var nma = new Nma(DefaultPeriod);
        var series = GetTestSeries(80);

        for (int i = 0; i < series.Count - 1; i++)
        {
            nma.Update(series[i]);
        }

        // Apply new bar then 5 corrections, final correction to target value
        nma.Update(series[^1]);
        for (int c = 0; c < 5; c++)
        {
            nma.Update(new TValue(series[^1].Time, series[^1].Value * (1.0 + (c * 0.01))), isNew: false);
        }
        var corrected = nma.Update(new TValue(series[^1].Time, series[^1].Value + 2.0), isNew: false);

        // Clean path
        var nma2 = new Nma(DefaultPeriod);
        for (int i = 0; i < series.Count - 1; i++)
        {
            nma2.Update(series[i]);
        }
        var expected = nma2.Update(new TValue(series[^1].Time, series[^1].Value + 2.0), true);

        Assert.Equal(expected.Value, corrected.Value, 1e-9);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var nma = new Nma(DefaultPeriod);
        var series = GetTestSeries(100);
        foreach (var tv in series)
        {
            nma.Update(tv);
        }

        nma.Reset();
        Assert.False(nma.IsHot);
        Assert.Equal(0, nma.Last.Value);
    }

    // ── D) Warmup/convergence ──────────────────────────────────────────

    [Fact]
    public void IsHot_FlipsAtPeriod()
    {
        var nma = new Nma(DefaultPeriod);
        for (int i = 0; i < DefaultPeriod; i++)
        {
            var hot = nma.IsHot;
            nma.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0 + i));
            if (i < DefaultPeriod - 1)
            {
                Assert.False(hot);
            }
        }
        Assert.True(nma.IsHot);
    }

    // ── E) Robustness ──────────────────────────────────────────────────

    [Fact]
    public void Update_NaN_UsesLastValid()
    {
        var nma = new Nma(DefaultPeriod);
        var series = GetTestSeries(60);

        for (int i = 0; i < 50; i++)
        {
            nma.Update(series[i]);
        }
        _ = nma.Last.Value;

        nma.Update(new TValue(DateTime.UtcNow, double.NaN));
        double afterNaN = nma.Last.Value;

        Assert.True(double.IsFinite(afterNaN));
    }

    [Fact]
    public void Update_Infinity_UsesLastValid()
    {
        var nma = new Nma(DefaultPeriod);
        var series = GetTestSeries(60);

        for (int i = 0; i < 50; i++)
        {
            nma.Update(series[i]);
        }

        nma.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(nma.Last.Value));
    }

    [Fact]
    public void Update_BatchNaN_AllFinite()
    {
        var nma = new Nma(DefaultPeriod);
        var series = GetTestSeries(100);

        for (int i = 0; i < series.Count; i++)
        {
            // Inject NaN every 10th bar after warmup
            if (i > DefaultPeriod && i % 10 == 0)
            {
                nma.Update(new TValue(series[i].Time, double.NaN));
            }
            else
            {
                nma.Update(series[i]);
            }
            Assert.True(double.IsFinite(nma.Last.Value));
        }
    }

    // ── F) Consistency (4 modes) ───────────────────────────────────────

    [Fact]
    public void TSeries_MatchesStreaming()
    {
        var series = GetTestSeries(200);

        // Streaming
        var streaming = new Nma(DefaultPeriod);
        var streamResults = new double[series.Count];
        for (int i = 0; i < series.Count; i++)
        {
            streamResults[i] = streaming.Update(series[i]).Value;
        }

        // Batch via TSeries
        var batchResults = Nma.Batch(series, DefaultPeriod);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(streamResults[i], batchResults.Values[i], 1e-7);
        }
    }

    [Fact]
    public void Batch_Span_MatchesStreaming()
    {
        var series = GetTestSeries(200);

        // Streaming
        var streaming = new Nma(DefaultPeriod);
        var streamResults = new double[series.Count];
        for (int i = 0; i < series.Count; i++)
        {
            streamResults[i] = streaming.Update(series[i]).Value;
        }

        // Span batch
        var output = new double[series.Count];
        Nma.Batch(series.Values, output, DefaultPeriod);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(streamResults[i], output[i], 1e-7);
        }
    }

    [Fact]
    public void EventDriven_MatchesStreaming()
    {
        var series = GetTestSeries(200);

        // Streaming
        var streaming = new Nma(DefaultPeriod);
        var streamResults = new double[series.Count];
        for (int i = 0; i < series.Count; i++)
        {
            streamResults[i] = streaming.Update(series[i]).Value;
        }

        // Event-driven
        var source = new TSeries();
        var eventNma = new Nma(source, DefaultPeriod);
        var eventResults = new double[series.Count];
        for (int i = 0; i < series.Count; i++)
        {
            source.Add(series[i]);
            eventResults[i] = eventNma.Last.Value;
        }

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(streamResults[i], eventResults[i], 1e-10);
        }
    }

    // ── G) Span API tests ──────────────────────────────────────────────

    [Fact]
    public void Batch_Span_MismatchedLengths_Throws()
    {
        var src = new double[10];
        var output = new double[5];
        var ex = Assert.Throws<ArgumentException>(() => Nma.Batch(src, output, DefaultPeriod));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_InvalidPeriod_Throws()
    {
        var src = new double[10];
        var output = new double[10];
        var ex = Assert.Throws<ArgumentException>(() => Nma.Batch(src, output, 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Batch_Span_Empty_NoOp()
    {
        var src = ReadOnlySpan<double>.Empty;
        var output = Span<double>.Empty;
        Nma.Batch(src, output, DefaultPeriod);
        Assert.True(true); // S2699 - verifying no exception is the assertion
    }

    [Fact]
    public void Batch_Span_HandlesNaN()
    {
        var src = new double[] { 100, 101, double.NaN, 103, 104 };
        var output = new double[5];
        Nma.Batch(src, output, 3);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]));
        }
    }

    // ── H) Chainability ────────────────────────────────────────────────

    [Fact]
    public void PubSub_FiresEvents()
    {
        var source = new TSeries();
        var nma = new Nma(source, DefaultPeriod);
        int eventCount = 0;
        nma.Pub += (object? _, in TValueEventArgs e) => eventCount++;

        for (int i = 0; i < 10; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddMinutes(i), 100.0 + i));
        }

        Assert.Equal(10, eventCount);
    }

    [Fact]
    public void Dispose_UnsubscribesFromSource()
    {
        var source = new TSeries();
        var nma = new Nma(source, DefaultPeriod);
        nma.Dispose();

        // Adding to source should not affect disposed nma
        source.Add(new TValue(DateTime.UtcNow, 100.0));
        Assert.Equal(0, nma.Last.Value);
    }

    // ── Additional behavior tests ──────────────────────────────────────

    [Fact]
    public void ConstantInput_ConvergesToConstant()
    {
        var nma = new Nma(DefaultPeriod);
        double constant = 50.0;

        for (int i = 0; i < 200; i++)
        {
            nma.Update(new TValue(DateTime.UtcNow.AddMinutes(i), constant));
        }

        Assert.Equal(constant, nma.Last.Value, 1e-6);
    }

    [Fact]
    public void MonotonicInput_TracksTrend()
    {
        var nma = new Nma(14);
        double lastNma = 0;

        for (int i = 0; i < 100; i++)
        {
            double price = 100.0 + i;
            lastNma = nma.Update(new TValue(DateTime.UtcNow.AddMinutes(i), price)).Value;
        }

        // NMA should be between first and last price in a monotonic series
        Assert.True(lastNma > 100.0);
        Assert.True(lastNma < 200.0);
    }

    [Fact]
    public void Ratio_BoundedZeroOne()
    {
        // The ratio should conceptually be in [0,1] range
        // We verify indirectly: NMA should always be between min and max of input
        var nma = new Nma(DefaultPeriod);
        var series = GetTestSeries(200);
        double minPrice = double.MaxValue;
        double maxPrice = double.MinValue;

        for (int i = 0; i < series.Count; i++)
        {
            nma.Update(series[i]);
            if (series[i].Value < minPrice)
            {
                minPrice = series[i].Value;
            }
            if (series[i].Value > maxPrice)
            {
                maxPrice = series[i].Value;
            }
        }

        // NMA value should be within the range of input data (with some tolerance)
        Assert.True(nma.Last.Value >= minPrice * 0.99);
        Assert.True(nma.Last.Value <= maxPrice * 1.01);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(14)]
    [InlineData(40)]
    [InlineData(100)]
    public void DifferentPeriods_AllValid(int period)
    {
        var nma = new Nma(period);
        var series = GetTestSeries(200);
        foreach (var tv in series)
        {
            var result = nma.Update(tv);
            Assert.True(double.IsFinite(result.Value));
        }
    }

    [Fact]
    public void Calculate_ReturnsBothResultsAndIndicator()
    {
        var series = GetTestSeries(100);
        var (results, indicator) = Nma.Calculate(series, DefaultPeriod);

        Assert.Equal(series.Count, results.Count);
        Assert.True(indicator.IsHot);
    }

    [Fact]
    public void Prime_SetsState()
    {
        var series = GetTestSeries(100);
        var nma = new Nma(DefaultPeriod);
        nma.Prime(series.Values);

        Assert.True(nma.IsHot);
        Assert.True(double.IsFinite(nma.Last.Value));
    }
}
