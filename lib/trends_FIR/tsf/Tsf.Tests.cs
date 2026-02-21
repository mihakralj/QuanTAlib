namespace QuanTAlib.Tests;

public class TsfTests
{
    private static TSeries MakeSeries(int count = 500)
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        return gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
    }

    // ── A) Constructor validation ──────────────────────────────────────

    [Fact]
    public void Constructor_InvalidPeriod_ThrowsArgumentException()
    {
        var ex0 = Assert.Throws<ArgumentException>(() => new Tsf(0));
        Assert.Equal("period", ex0.ParamName);

        var exNeg = Assert.Throws<ArgumentException>(() => new Tsf(-1));
        Assert.Equal("period", exNeg.ParamName);
    }

    [Fact]
    public void Constructor_ValidParameters_SetsProperties()
    {
        var tsf = new Tsf(14);
        Assert.Equal("Tsf(14)", tsf.Name);
        Assert.False(tsf.IsHot);
        Assert.Equal(14, tsf.WarmupPeriod);
    }

    [Fact]
    public void Constructor_NullSource_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new Tsf(null!, 14));
    }

    // ── B) Basic calculation ───────────────────────────────────────────

    [Fact]
    public void Update_SingleValue_ReturnsSameValue()
    {
        var tsf = new Tsf(14);
        var result = tsf.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(100, result.Value);
    }

    [Fact]
    public void Update_Last_IsAccessible()
    {
        var tsf = new Tsf(5);
        var series = MakeSeries(20);
        foreach (var item in series)
        {
            tsf.Update(item);
        }
        Assert.True(double.IsFinite(tsf.Last.Value));
        Assert.True(tsf.IsHot);
        Assert.Contains("Tsf", tsf.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Update_LinearTrend_ReturnsNextValue()
    {
        // For a perfect linear trend y = x,
        // TSF should return x+1 (one step forecast) after warmup
        const int period = 10;
        var tsf = new Tsf(period);

        for (int i = 0; i < period * 2; i++)
        {
            var result = tsf.Update(new TValue(DateTime.UtcNow, i));
            if (i >= period)
            {
                // TSF forecasts one step ahead: should be i+1
                Assert.Equal(i + 1, result.Value, 1e-9);
            }
        }
    }

    [Fact]
    public void Update_ConstantValue_ReturnsSameValue()
    {
        const int period = 10;
        var tsf = new Tsf(period);
        const double value = 123.45;

        for (int i = 0; i < period * 2; i++)
        {
            var result = tsf.Update(new TValue(DateTime.UtcNow, value));
            Assert.Equal(value, result.Value, 1e-9);
        }
    }

    [Fact]
    public void Update_LinearSlope_ForecastsCorrectly()
    {
        // y = 2x + 5
        // At bar i, the next bar's value should be 2*(i+1) + 5
        const int period = 8;
        var tsf = new Tsf(period);

        for (int i = 0; i < 30; i++)
        {
            double y = 2.0 * i + 5.0;
            var result = tsf.Update(new TValue(DateTime.UtcNow, y));

            if (i >= period)
            {
                double expected = 2.0 * (i + 1) + 5.0;
                Assert.Equal(expected, result.Value, 1e-9);
            }
        }
    }

    // ── C) State + bar correction ──────────────────────────────────────

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var tsf = new Tsf(5);
        var result = tsf.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var tsf = new Tsf(5);
        var series = MakeSeries(20);

        foreach (var item in series)
        {
            tsf.Update(item, isNew: true);
        }

        double valueBefore = tsf.Last.Value;
        tsf.Update(new TValue(DateTime.UtcNow, series[^1].Value * 1.1), isNew: false);
        double valueAfter = tsf.Last.Value;

        Assert.NotEqual(valueBefore, valueAfter);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var tsf = new Tsf(10);
        var series = MakeSeries(50);

        // Feed N values
        for (int i = 0; i < 30; i++)
        {
            tsf.Update(series[i], isNew: true);
        }
        double expectedValue = tsf.Last.Value;

        // Feed M corrections with isNew: false
        for (int j = 0; j < 5; j++)
        {
            tsf.Update(new TValue(DateTime.UtcNow, 999.0 + j), isNew: false);
        }

        // Restore original value
        tsf.Update(series[29], isNew: false);
        Assert.Equal(expectedValue, tsf.Last.Value, 1e-6);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var tsf = new Tsf(10);
        var series = MakeSeries(50);
        foreach (var item in series)
        {
            tsf.Update(item);
        }

        Assert.True(tsf.IsHot);
        tsf.Reset();
        Assert.False(tsf.IsHot);

        // Re-feed same data should produce identical results
        var tsf2 = new Tsf(10);
        foreach (var item in series)
        {
            tsf.Update(item);
            tsf2.Update(item);
        }
        Assert.Equal(tsf2.Last.Value, tsf.Last.Value, 1e-12);
    }

    // ── D) Warmup/convergence ──────────────────────────────────────────

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        var tsf = new Tsf(10);
        for (int i = 0; i < 9; i++)
        {
            tsf.Update(new TValue(DateTime.UtcNow, i));
            Assert.False(tsf.IsHot);
        }
        tsf.Update(new TValue(DateTime.UtcNow, 9));
        Assert.True(tsf.IsHot);
    }

    [Fact]
    public void IsHot_IsPeriodDependent()
    {
        foreach (int period in new[] { 5, 10, 20, 50 })
        {
            var tsf = new Tsf(period);
            for (int i = 0; i < period - 1; i++)
            {
                tsf.Update(new TValue(DateTime.UtcNow, i));
                Assert.False(tsf.IsHot);
            }
            tsf.Update(new TValue(DateTime.UtcNow, period - 1));
            Assert.True(tsf.IsHot);
        }
    }

    // ── E) Robustness (NaN/Infinity) ───────────────────────────────────

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var tsf = new Tsf(5);
        var series = MakeSeries(20);

        for (int i = 0; i < 10; i++)
        {
            tsf.Update(series[i]);
        }

        _ = tsf.Last.Value;
        tsf.Update(new TValue(DateTime.UtcNow, double.NaN), isNew: true);
        Assert.True(double.IsFinite(tsf.Last.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var tsf = new Tsf(5);
        var series = MakeSeries(20);

        for (int i = 0; i < 10; i++)
        {
            tsf.Update(series[i]);
        }

        tsf.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity), isNew: true);
        Assert.True(double.IsFinite(tsf.Last.Value));

        tsf.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity), isNew: true);
        Assert.True(double.IsFinite(tsf.Last.Value));
    }

    [Fact]
    public void MultipleNaN_ContinuesWithLastValid()
    {
        var tsf = new Tsf(5);
        var series = MakeSeries(20);

        for (int i = 0; i < 10; i++)
        {
            tsf.Update(series[i]);
        }

        for (int j = 0; j < 5; j++)
        {
            tsf.Update(new TValue(DateTime.UtcNow, double.NaN), isNew: true);
            Assert.True(double.IsFinite(tsf.Last.Value));
        }
    }

    [Fact]
    public void BatchCalc_HandlesNaN()
    {
        double[] input = { 1, 2, 3, double.NaN, 5, 6, 7, 8, 9, 10 };
        double[] output = new double[input.Length];
        Tsf.Batch(input.AsSpan(), output.AsSpan(), 5);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]));
        }
    }

    // ── F) Consistency (all 4 modes match) ─────────────────────────────

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        int period = 14;
        var series = MakeSeries(500);

        // 1. Batch (TSeries)
        var batchResult = Tsf.Batch(series, period);

        // 2. Span
        double[] spanOutput = new double[series.Count];
        Tsf.Batch(series.Values, spanOutput.AsSpan(), period);

        // 3. Streaming
        var streamTsf = new Tsf(period);
        var streamResults = new List<double>();
        foreach (var item in series)
        {
            streamResults.Add(streamTsf.Update(item).Value);
        }

        // 4. Eventing
        var pubSource = new TSeries();
        var eventTsf = new Tsf(pubSource, period);
        foreach (var item in series)
        {
            pubSource.Add(item);
        }

        // Compare last values
        double batchLast = batchResult.Values[^1];
        double spanLast = spanOutput[^1];
        double streamLast = streamResults[^1];
        double eventLast = eventTsf.Last.Value;

        Assert.Equal(batchLast, spanLast, 1e-9);
        Assert.Equal(batchLast, streamLast, 1e-9);
        Assert.Equal(batchLast, eventLast, 1e-9);
    }

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        int period = 10;
        var series = MakeSeries(200);

        // Batch
        var batchResult = Tsf.Batch(series, period);

        // Iterative
        var tsf = new Tsf(period);
        TSeries streamResult = tsf.Update(series);

        int compareCount = Math.Min(100, series.Count);
        int start = series.Count - compareCount;

        for (int i = start; i < series.Count; i++)
        {
            Assert.Equal(batchResult.Values[i], streamResult.Values[i], 1e-9);
        }
    }

    // ── G) Span API tests ──────────────────────────────────────────────

    [Fact]
    public void SpanCalc_ValidatesInput_LengthMismatch()
    {
        double[] input = { 1, 2, 3, 4, 5 };
        double[] output = new double[3];

        var ex = Assert.Throws<ArgumentException>(() =>
            Tsf.Batch(input.AsSpan(), output.AsSpan(), 3));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void SpanCalc_ValidatesInput_InvalidPeriod()
    {
        double[] input = { 1, 2, 3, 4, 5 };
        double[] output = new double[5];

        var ex = Assert.Throws<ArgumentException>(() =>
            Tsf.Batch(input.AsSpan(), output.AsSpan(), 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void SpanCalc_MatchesTSeriesCalc()
    {
        int period = 20;
        var series = MakeSeries(500);

        var batchResult = Tsf.Batch(series, period);
        double[] spanOutput = new double[series.Count];
        Tsf.Batch(series.Values, spanOutput.AsSpan(), period);

        int compareCount = 100;
        int start = series.Count - compareCount;
        for (int i = start; i < series.Count; i++)
        {
            Assert.Equal(batchResult.Values[i], spanOutput[i], 1e-9);
        }
    }

    [Fact]
    public void SpanCalc_EmptyInput_NoException()
    {
        double[] input = Array.Empty<double>();
        double[] output = Array.Empty<double>();
        Tsf.Batch(input.AsSpan(), output.AsSpan(), 5);
        Assert.Empty(output);
    }

    [Fact]
    public void SpanCalc_LargeDataset_NoStackOverflow()
    {
        int size = 10_000;
        double[] input = new double[size];
        double[] output = new double[size];

        var gbm = new GBM(100, 0.05, 0.2, seed: 99);
        var series = gbm.Fetch(size, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;

        for (int i = 0; i < size; i++)
        {
            input[i] = series.Values[i];
        }

        Tsf.Batch(input.AsSpan(), output.AsSpan(), 300);

        Assert.True(double.IsFinite(output[^1]));
    }

    // ── H) Chainability ────────────────────────────────────────────────

    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var tsf = new Tsf(5);
        int fireCount = 0;
        tsf.Pub += (object? _, in TValueEventArgs _) => fireCount++;

        var series = MakeSeries(20);
        foreach (var item in series)
        {
            tsf.Update(item);
        }

        Assert.Equal(series.Count, fireCount);
    }

    [Fact]
    public void EventChaining_Works()
    {
        int period = 5;
        var source = new TSeries();
        var tsf = new Tsf(source, period);

        var series = MakeSeries(50);
        foreach (var item in series)
        {
            source.Add(item);
        }

        Assert.True(tsf.IsHot);
        Assert.True(double.IsFinite(tsf.Last.Value));
    }

    // ── TSF-specific tests ─────────────────────────────────────────────

    [Fact]
    public void TSF_EqualsLSMA_PlusSlope()
    {
        // TSF = LSMA(offset=0) + slope
        // Which is the same as LSMA(offset=1)?
        // Yes: LSMA uses result = b - m * offset
        // LSMA(offset=1) = b - m*1 = b - m = TSF
        const int period = 14;
        var series = MakeSeries(500);

        var lsma = new Lsma(period, offset: 1);
        var tsf = new Tsf(period);

        for (int i = 0; i < series.Count; i++)
        {
            var lsmaResult = lsma.Update(series[i]);
            var tsfResult = tsf.Update(series[i]);

            Assert.Equal(lsmaResult.Value, tsfResult.Value, 1e-9);
        }
    }

    [Fact]
    public void Calculate_ReturnsBothResultsAndIndicator()
    {
        var series = MakeSeries(100);
        var (results, indicator) = Tsf.Calculate(series, 10);

        Assert.True(results.Count > 0);
        Assert.True(indicator.IsHot);
        Assert.Equal(results[^1].Value, indicator.Last.Value);
    }
}
