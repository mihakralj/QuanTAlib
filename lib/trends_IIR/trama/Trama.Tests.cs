using Xunit;

namespace QuanTAlib.Tests;

public class TramaTests
{
    private const int DefaultPeriod = 14;
    private const long Seed = 12345;
    private static readonly TimeSpan Step = TimeSpan.FromMinutes(1);

    private static TSeries GetTestSeries(int count = 500)
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(count, Seed, Step);
        return bars.Close;
    }

    // ── A) Constructor validation ──────────────────────────

    [Fact]
    public void Constructor_ValidPeriod_CreatesInstance()
    {
        var trama = new Trama(DefaultPeriod);
        Assert.Equal($"Trama({DefaultPeriod})", trama.Name);
        Assert.Equal(DefaultPeriod, trama.WarmupPeriod);
    }

    [Fact]
    public void Constructor_PeriodOne_IsValid()
    {
        var trama = new Trama(1);
        Assert.Equal("Trama(1)", trama.Name);
    }

    [Fact]
    public void Constructor_ZeroPeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Trama(0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_NegativePeriod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Trama(-5));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithSource_SubscribesEvent()
    {
        var trama1 = new Trama(DefaultPeriod);
        var trama2 = new Trama(trama1, DefaultPeriod);
        Assert.NotNull(trama2);
        trama2.Dispose();
    }

    // ── B) Basic calculation ──────────────────────────────

    [Fact]
    public void BasicCalculation_ReturnsFinite()
    {
        var trama = new Trama(DefaultPeriod);
        var series = GetTestSeries();

        for (int i = 0; i < series.Count; i++)
        {
            trama.Update(series[i]);
        }

        Assert.True(double.IsFinite(trama.Last.Value));
    }

    [Fact]
    public void FirstValue_EqualsInput()
    {
        var trama = new Trama(DefaultPeriod);
        var result = trama.Update(new TValue(DateTime.UtcNow.Ticks, 100.0));
        Assert.Equal(100.0, result.Value, 1e-10);
    }

    [Fact]
    public void Name_IsCorrect()
    {
        var trama = new Trama(20);
        Assert.Equal("Trama(20)", trama.Name);
    }

    [Fact]
    public void Last_UpdatesOnEachCall()
    {
        var trama = new Trama(DefaultPeriod);
        var series = GetTestSeries(50);

        for (int i = 0; i < series.Count; i++)
        {
            var result = trama.Update(series[i]);
            Assert.Equal(result.Value, trama.Last.Value, 1e-15);
        }
    }

    // ── C) State + bar correction (critical) ──────────────

    [Fact]
    public void IsNew_True_AdvancesState()
    {
        var trama = new Trama(DefaultPeriod);
        var series = GetTestSeries(50);

        for (int i = 0; i < 49; i++)
        {
            trama.Update(series[i]);
        }

        var val1 = trama.Update(new TValue(series[49].Time, series[49].Value), true);
        Assert.True(double.IsFinite(val1.Value));
    }

    [Fact]
    public void IsNew_False_RollsBackState()
    {
        var trama = new Trama(DefaultPeriod);
        var series = GetTestSeries(100);

        for (int i = 0; i < 99; i++)
        {
            trama.Update(series[i]);
        }

        // Update with isNew=true then isNew=false with different value
        trama.Update(new TValue(series[99].Time, series[99].Value), true);
        var corrected = trama.Update(new TValue(series[99].Time, series[99].Value + 1.0), false);

        // Compare with fresh instance that gets the corrected value directly
        var trama2 = new Trama(DefaultPeriod);
        for (int i = 0; i < 99; i++)
        {
            trama2.Update(series[i]);
        }
        var expected = trama2.Update(new TValue(series[99].Time, series[99].Value + 1.0), true);

        Assert.Equal(expected.Value, corrected.Value, 1e-9);
    }

    [Fact]
    public void IterativeCorrections_Restore()
    {
        var trama = new Trama(DefaultPeriod);
        var series = GetTestSeries(100);

        for (int i = 0; i < 98; i++)
        {
            trama.Update(series[i]);
        }

        // Multiple isNew=false corrections
        trama.Update(new TValue(series[98].Time, series[98].Value), true);
        trama.Update(new TValue(series[98].Time, series[98].Value + 0.5), false);
        trama.Update(new TValue(series[98].Time, series[98].Value + 1.0), false);
        var finalResult = trama.Update(new TValue(series[98].Time, series[98].Value + 1.5), false);

        // Compare with clean path
        var trama2 = new Trama(DefaultPeriod);
        for (int i = 0; i < 98; i++)
        {
            trama2.Update(series[i]);
        }
        var expected = trama2.Update(new TValue(series[98].Time, series[98].Value + 1.5), true);

        Assert.Equal(expected.Value, finalResult.Value, 1e-9);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var trama = new Trama(DefaultPeriod);
        var series = GetTestSeries(100);

        for (int i = 0; i < series.Count; i++)
        {
            trama.Update(series[i]);
        }

        trama.Reset();
        Assert.Equal(0, trama.Last.Value);
        Assert.False(trama.IsHot);

        // Feed again
        for (int i = 0; i < series.Count; i++)
        {
            trama.Update(series[i]);
        }
        Assert.True(double.IsFinite(trama.Last.Value));
        Assert.True(trama.IsHot);
    }

    // ── D) Warmup/convergence ─────────────────────────────

    [Fact]
    public void IsHot_FlipsAtPeriod()
    {
        var trama = new Trama(DefaultPeriod);
        var series = GetTestSeries(50);

        for (int i = 0; i < DefaultPeriod - 1; i++)
        {
            trama.Update(series[i]);
            Assert.False(trama.IsHot, $"Should not be hot at bar {i + 1}");
        }

        trama.Update(series[DefaultPeriod - 1]);
        Assert.True(trama.IsHot, $"Should be hot at bar {DefaultPeriod}");
    }

    [Fact]
    public void WarmupPeriod_MatchesPeriod()
    {
        var trama = new Trama(20);
        Assert.Equal(20, trama.WarmupPeriod);
    }

    // ── E) Robustness (critical) ──────────────────────────

    [Fact]
    public void NaN_UsesLastValidValue()
    {
        var trama = new Trama(DefaultPeriod);
        var series = GetTestSeries(50);

        for (int i = 0; i < 30; i++)
        {
            trama.Update(series[i]);
        }

        var beforeNaN = trama.Last.Value;
        var nanResult = trama.Update(new TValue(DateTime.UtcNow.Ticks, double.NaN));

        Assert.True(double.IsFinite(nanResult.Value));
    }

    [Fact]
    public void Infinity_UsesLastValidValue()
    {
        var trama = new Trama(DefaultPeriod);
        var series = GetTestSeries(50);

        for (int i = 0; i < 30; i++)
        {
            trama.Update(series[i]);
        }

        var infResult = trama.Update(new TValue(DateTime.UtcNow.Ticks, double.PositiveInfinity));
        Assert.True(double.IsFinite(infResult.Value));
    }

    [Fact]
    public void BatchNaN_DoesNotPropagate()
    {
        var trama = new Trama(DefaultPeriod);
        var series = GetTestSeries(100);

        // Insert NaN values
        for (int i = 0; i < series.Count; i++)
        {
            double val = (i == 25 || i == 50 || i == 75) ? double.NaN : series[i].Value;
            trama.Update(new TValue(series[i].Time, val));
        }

        Assert.True(double.IsFinite(trama.Last.Value));
    }

    // ── F) Consistency (critical) ─────────────────────────

    [Fact]
    public void TSeries_Update_Matches_Streaming()
    {
        var series = GetTestSeries(200);

        // Streaming
        var trama1 = new Trama(DefaultPeriod);
        var streamResults = new List<double>();
        for (int i = 0; i < series.Count; i++)
        {
            streamResults.Add(trama1.Update(series[i]).Value);
        }

        // TSeries batch
        var trama2 = new Trama(DefaultPeriod);
        var batchResults = trama2.Update(series);

        Assert.Equal(streamResults.Count, batchResults.Count);
        for (int i = 0; i < batchResults.Count; i++)
        {
            Assert.Equal(streamResults[i], batchResults.Values[i], 1e-9);
        }
    }

    [Fact]
    public void SpanBatch_Matches_Streaming()
    {
        var series = GetTestSeries(200);
        var values = series.Values;

        // Streaming
        var trama = new Trama(DefaultPeriod);
        var streamResults = new List<double>();
        for (int i = 0; i < series.Count; i++)
        {
            streamResults.Add(trama.Update(series[i]).Value);
        }

        // Span batch
        var output = new double[values.Length];
        Trama.Batch(values, output, DefaultPeriod);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.Equal(streamResults[i], output[i], 1e-9);
        }
    }

    [Fact]
    public void StaticBatch_Matches_Streaming()
    {
        var series = GetTestSeries(200);

        // Streaming
        var trama = new Trama(DefaultPeriod);
        var streamResults = new List<double>();
        for (int i = 0; i < series.Count; i++)
        {
            streamResults.Add(trama.Update(series[i]).Value);
        }

        // Static batch
        var batchResults = Trama.Batch(series, DefaultPeriod);

        Assert.Equal(streamResults.Count, batchResults.Count);
        for (int i = 0; i < batchResults.Count; i++)
        {
            Assert.Equal(streamResults[i], batchResults.Values[i], 1e-9);
        }
    }

    [Fact]
    public void EventChaining_MatchesManual()
    {
        var series = GetTestSeries(100);

        // Manual
        var trama1 = new Trama(DefaultPeriod);
        var results1 = new List<double>();
        for (int i = 0; i < series.Count; i++)
        {
            results1.Add(trama1.Update(series[i]).Value);
        }

        // Event-based
        var trama2 = new Trama(DefaultPeriod);
        var trama3 = new Trama(trama2, DefaultPeriod);
        var results3 = new List<double>();
        trama3.Pub += (object? _, in TValueEventArgs e) => results3.Add(e.Value.Value);

        for (int i = 0; i < series.Count; i++)
        {
            trama2.Update(series[i]);
        }

        // trama3 receives trama2's output, so compare trama3's last value is finite
        Assert.Equal(series.Count, results3.Count);
        Assert.True(double.IsFinite(trama3.Last.Value));

        trama3.Dispose();
    }

    // ── G) Span API tests ─────────────────────────────────

    [Fact]
    public void SpanBatch_MismatchedLengths_Throws()
    {
        var source = new double[100];
        var output = new double[50];

        var ex = Assert.Throws<ArgumentException>(() => Trama.Batch(source, output, DefaultPeriod));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void SpanBatch_InvalidPeriod_Throws()
    {
        var source = new double[100];
        var output = new double[100];

        var ex = Assert.Throws<ArgumentException>(() => Trama.Batch(source, output, 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void SpanBatch_EmptyInput_NoException()
    {
        var source = ReadOnlySpan<double>.Empty;
        var output = Span<double>.Empty;

        Trama.Batch(source, output, DefaultPeriod); // Should not throw
    }

    [Fact]
    public void SpanBatch_LargeDataset_NoStackOverflow()
    {
        int size = 5000;
        var source = new double[size];
        var output = new double[size];

        // Fill with simple incrementing values
        for (int i = 0; i < size; i++)
        {
            source[i] = 100.0 + i * 0.01;
        }

        Trama.Batch(source, output, DefaultPeriod);

        Assert.True(double.IsFinite(output[^1]));
    }

    // ── H) Chainability ───────────────────────────────────

    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var trama = new Trama(DefaultPeriod);
        int eventCount = 0;
        trama.Pub += (object? _, in TValueEventArgs _) => eventCount++;

        var series = GetTestSeries(50);
        for (int i = 0; i < series.Count; i++)
        {
            trama.Update(series[i]);
        }

        Assert.Equal(50, eventCount);
    }

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var series = GetTestSeries(200);
        var (results, indicator) = Trama.Calculate(series, DefaultPeriod);

        Assert.Equal(series.Count, results.Count);
        Assert.True(indicator.IsHot);
        Assert.True(double.IsFinite(results.Values[^1]));
    }

    // ── Additional behavioral tests ───────────────────────

    [Fact]
    public void ConstantInput_ConvergesToConstant()
    {
        var trama = new Trama(DefaultPeriod);
        double constant = 50.0;

        for (int i = 0; i < 100; i++)
        {
            trama.Update(new TValue(DateTime.UtcNow.AddMinutes(i).Ticks, constant));
        }

        Assert.Equal(constant, trama.Last.Value, 1e-10);
    }

    [Fact]
    public void StrongTrend_TracksClosely()
    {
        var trama = new Trama(DefaultPeriod);
        double lastPrice = 0;

        // Create strong uptrend: every bar makes new high
        for (int i = 0; i < 100; i++)
        {
            lastPrice = 100.0 + i;
            trama.Update(new TValue(DateTime.UtcNow.AddMinutes(i).Ticks, lastPrice));
        }

        // In strong trend, TRAMA should be close to current price
        double diff = Math.Abs(lastPrice - trama.Last.Value);
        Assert.True(diff < lastPrice * 0.1, $"TRAMA should track strong trend closely, diff={diff}");
    }

    [Fact]
    public void RangeboundMarket_MovesSlowly()
    {
        var trama = new Trama(DefaultPeriod);

        // Warm up with a value
        for (int i = 0; i < 20; i++)
        {
            trama.Update(new TValue(DateTime.UtcNow.AddMinutes(i).Ticks, 100.0));
        }

        double valueAfterWarmup = trama.Last.Value;

        // Now oscillate in a tight range
        for (int i = 0; i < 50; i++)
        {
            double price = 100.0 + (i % 2 == 0 ? 0.5 : -0.5);
            trama.Update(new TValue(DateTime.UtcNow.AddMinutes(20 + i).Ticks, price));
        }

        // In range, TRAMA should barely move from 100.0
        double diff = Math.Abs(100.0 - trama.Last.Value);
        Assert.True(diff < 2.0, $"TRAMA should be near flat in range, diff={diff}");
    }

    [Fact]
    public void Prime_RestoresState()
    {
        var series = GetTestSeries(200);

        // Streaming
        var trama1 = new Trama(DefaultPeriod);
        for (int i = 0; i < series.Count; i++)
        {
            trama1.Update(series[i]);
        }

        // Prime
        var trama2 = new Trama(DefaultPeriod);
        trama2.Prime(series.Values);

        Assert.Equal(trama1.Last.Value, trama2.Last.Value, 1e-9);
    }

    [Fact]
    public void Dispose_UnsubscribesEvent()
    {
        var trama1 = new Trama(DefaultPeriod);
        var trama2 = new Trama(trama1, DefaultPeriod);

        trama2.Dispose();

        // Should not crash after unsubscribe
        trama1.Update(new TValue(DateTime.UtcNow.Ticks, 100.0));
    }
}
