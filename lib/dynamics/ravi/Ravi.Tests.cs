namespace QuanTAlib.Tests;

public class RaviTests
{
    // ============== A) Constructor & Parameter Validation ==============

    [Fact]
    public void Constructor_ValidatesShortPeriod()
    {
        Assert.Throws<ArgumentException>(() => new Ravi(0, 65));
        Assert.Throws<ArgumentException>(() => new Ravi(-1, 65));
        Assert.Throws<ArgumentException>(() => new Ravi(-100, 65));
    }

    [Fact]
    public void Constructor_ValidatesLongPeriod()
    {
        Assert.Throws<ArgumentException>(() => new Ravi(7, 0));
        Assert.Throws<ArgumentException>(() => new Ravi(7, -1));
    }

    [Fact]
    public void Constructor_ValidatesShortLessThanLong()
    {
        Assert.Throws<ArgumentException>(() => new Ravi(10, 10));
        Assert.Throws<ArgumentException>(() => new Ravi(20, 10));
    }

    [Fact]
    public void Constructor_DefaultPeriods_Work()
    {
        var ravi = new Ravi();
        Assert.Contains("7", ravi.Name, StringComparison.Ordinal);
        Assert.Contains("65", ravi.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_CustomPeriods_Work()
    {
        var ravi = new Ravi(5, 50);
        Assert.Contains("5", ravi.Name, StringComparison.Ordinal);
        Assert.Contains("50", ravi.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_Period1Short_Works()
    {
        var ravi = new Ravi(1, 2);
        Assert.NotNull(ravi);
    }

    // ============== B) Basic Calculation ==============

    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var ravi = new Ravi(7, 65);
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            ravi.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(double.IsFinite(ravi.Last.Value));
    }

    [Fact]
    public void Calc_ReturnsValue()
    {
        var ravi = new Ravi(3, 10);

        Assert.Equal(0, ravi.Last.Value);

        var result = ravi.Update(new TValue(DateTime.UtcNow, 100));

        Assert.True(double.IsFinite(result.Value));
        Assert.Equal(result.Value, ravi.Last.Value);
    }

    [Fact]
    public void Properties_Accessible()
    {
        var ravi = new Ravi(7, 65);

        Assert.Equal(0, ravi.Last.Value);
        Assert.False(ravi.IsHot);
        Assert.Contains("Ravi", ravi.Name, StringComparison.Ordinal);
        Assert.True(ravi.WarmupPeriod > 0);
        Assert.Equal(65, ravi.WarmupPeriod);
    }

    [Fact]
    public void ConstantPrice_ReturnsZeroAfterWarmup()
    {
        var ravi = new Ravi(3, 10);

        for (int i = 0; i < 20; i++)
        {
            ravi.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100));
        }

        Assert.True(ravi.IsHot);
        Assert.Equal(0.0, ravi.Last.Value, 1e-10);
    }

    [Fact]
    public void OutputAlwaysNonNegative()
    {
        var ravi = new Ravi(3, 10);
        var gbm = new GBM(startPrice: 100.0, mu: -0.5, sigma: 1.0);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            var result = ravi.Update(new TValue(bar.Time, bar.Close));
            Assert.True(result.Value >= 0, $"RAVI must be non-negative, got {result.Value}");
        }
    }

    // ============== C) State Management & Bar Correction ==============

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var ravi = new Ravi(3, 10);

        ravi.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        ravi.Update(new TValue(DateTime.UtcNow.AddMinutes(1), 105), isNew: true);

        Assert.True(ravi.Last.Value >= 0);
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var ravi = new Ravi(3, 10);
        var gbm = new GBM(startPrice: 100.0);
        var bars = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed 15 bars to get past warmup
        for (int i = 0; i < 15; i++)
        {
            ravi.Update(new TValue(bars[i].Time, bars[i].Close), isNew: true);
        }

        double beforeUpdate = ravi.Last.Value;

        // Correct with a very different value
        ravi.Update(new TValue(bars[14].Time, bars[14].Close * 2), isNew: false);
        double afterUpdate = ravi.Last.Value;

        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var ravi = new Ravi(3, 10);
        var gbm = new GBM();
        var bars = gbm.Fetch(30, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed first 19
        for (int i = 0; i < 19; i++)
        {
            ravi.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        // Feed 20th bar (isNew=true)
        ravi.Update(new TValue(bars[19].Time, bars[19].Close), true);

        // Correct with modified value (isNew=false)
        double modifiedClose = bars[19].Close + 50.0;
        double val2 = ravi.Update(new TValue(bars[19].Time, modifiedClose), false).Value;

        // Create new instance and feed up to modified
        var ravi2 = new Ravi(3, 10);
        for (int i = 0; i < 19; i++)
        {
            ravi2.Update(new TValue(bars[i].Time, bars[i].Close));
        }
        double val3 = ravi2.Update(new TValue(bars[19].Time, modifiedClose), true).Value;

        Assert.Equal(val3, val2, 1e-9);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var ravi = new Ravi(3, 10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);
        var bars = gbm.Fetch(30, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed 15 new values
        TValue tenthValue = default;
        for (int i = 0; i < 15; i++)
        {
            tenthValue = new TValue(bars[i].Time, bars[i].Close);
            ravi.Update(tenthValue, isNew: true);
        }

        // Remember state after 15 values
        double stateAfter15 = ravi.Last.Value;

        // Generate corrections with isNew=false (different values)
        for (int i = 15; i < 25; i++)
        {
            ravi.Update(new TValue(bars[i].Time, bars[i].Close), isNew: false);
        }

        // Feed the remembered 15th value again with isNew=false
        TValue finalResult = ravi.Update(tenthValue, isNew: false);

        // State should match the original state after 15 values
        Assert.Equal(stateAfter15, finalResult.Value, 1e-10);
    }

    [Fact]
    public void Reset_Works()
    {
        var ravi = new Ravi(3, 10);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            ravi.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(ravi.IsHot);

        ravi.Reset();
        Assert.Equal(0, ravi.Last.Value);
        Assert.False(ravi.IsHot);

        // After reset, should accept new values
        ravi.Update(new TValue(bars[0].Time, bars[0].Close));
        Assert.True(double.IsFinite(ravi.Last.Value));
    }

    // ============== D) Warmup & Convergence ==============

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        var ravi = new Ravi(3, 10);

        Assert.False(ravi.IsHot);

        var baseTime = DateTime.UtcNow;
        for (int i = 0; i < 9; i++)
        {
            ravi.Update(new TValue(baseTime.AddMinutes(i), 100 + i));
            Assert.False(ravi.IsHot);
        }

        // 10th value should make it hot (long period = 10)
        ravi.Update(new TValue(baseTime.AddMinutes(9), 109));
        Assert.True(ravi.IsHot);
    }

    [Fact]
    public void IsHot_IsPeriodDependent()
    {
        var ravi7_65 = new Ravi(7, 65);
        var ravi3_10 = new Ravi(3, 10);

        Assert.Equal(65, ravi7_65.WarmupPeriod);
        Assert.Equal(10, ravi3_10.WarmupPeriod);
    }

    // ============== E) NaN/Infinity Handling ==============

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var ravi = new Ravi(3, 10);

        for (int i = 0; i < 12; i++)
        {
            ravi.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100 + i));
        }

        // Feed NaN
        var resultAfterNaN = ravi.Update(new TValue(DateTime.UtcNow.AddMinutes(12), double.NaN));

        Assert.True(double.IsFinite(resultAfterNaN.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var ravi = new Ravi(3, 10);

        for (int i = 0; i < 12; i++)
        {
            ravi.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100 + i));
        }

        var resultAfterInf = ravi.Update(new TValue(DateTime.UtcNow.AddMinutes(12), double.PositiveInfinity));
        Assert.True(double.IsFinite(resultAfterInf.Value));

        var resultAfterNegInf = ravi.Update(new TValue(DateTime.UtcNow.AddMinutes(13), double.NegativeInfinity));
        Assert.True(double.IsFinite(resultAfterNegInf.Value));
    }

    [Fact]
    public void MultipleNaN_ContinuesWithLastValid()
    {
        var ravi = new Ravi(3, 10);

        for (int i = 0; i < 12; i++)
        {
            ravi.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100 + i));
        }

        // Feed several NaN values
        for (int i = 0; i < 5; i++)
        {
            var result = ravi.Update(new TValue(DateTime.UtcNow.AddMinutes(12 + i), double.NaN));
            Assert.True(double.IsFinite(result.Value));
        }
    }

    [Fact]
    public void BatchNaN_Safe()
    {
        var ravi = new Ravi(3, 10);
        var gbm = new GBM();
        var bars = gbm.Fetch(30, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed normal values
        for (int i = 0; i < 15; i++)
        {
            ravi.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        // Feed NaN values
        for (int i = 0; i < 5; i++)
        {
            var result = ravi.Update(new TValue(DateTime.UtcNow.AddHours(i + 1), double.NaN));
            Assert.True(double.IsFinite(result.Value));
        }

        // Resume normal
        for (int i = 15; i < 25; i++)
        {
            var result = ravi.Update(new TValue(bars[i].Time, bars[i].Close));
            Assert.True(double.IsFinite(result.Value));
        }
    }

    // ============== F) Consistency Tests ==============

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        var raviIterative = new Ravi(5, 20);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // Iterative
        var iterativeResults = new TSeries();
        foreach (var tv in series)
        {
            iterativeResults.Add(raviIterative.Update(tv));
        }

        // Batch
        var batchResults = Ravi.Batch(series, 5, 20);

        Assert.Equal(iterativeResults.Count, batchResults.Count);
        for (int i = 0; i < iterativeResults.Count; i++)
        {
            Assert.Equal(iterativeResults[i].Value, batchResults[i].Value, 1e-10);
        }
    }

    [Fact]
    public void TSeries_Update_MatchesStreaming()
    {
        var ravi1 = new Ravi(5, 20);
        var ravi2 = new Ravi(5, 20);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // Streaming
        foreach (var tv in series)
        {
            ravi1.Update(tv);
        }

        // Batch via Update(TSeries)
        ravi2.Update(series);

        Assert.Equal(ravi1.Last.Value, ravi2.Last.Value, 1e-10);
    }

    [Fact]
    public void SpanBatch_MatchesStreaming()
    {
        var ravi = new Ravi(5, 20);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // Streaming
        var streamResults = new double[100];
        for (int i = 0; i < 100; i++)
        {
            streamResults[i] = ravi.Update(series[i]).Value;
        }

        // Span batch
        var values = series.Values.ToArray();
        var spanResults = new double[100];
        Ravi.Batch(values, spanResults, 5, 20);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(streamResults[i], spanResults[i], 1e-10);
        }
    }

    [Fact]
    public void EventBased_MatchesStreaming()
    {
        var ravi1 = new Ravi(5, 20);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // Collect event-based results
        var eventResults = new List<double>();
        ravi1.Pub += (object? _, in TValueEventArgs e) => eventResults.Add(e.Value.Value);

        foreach (var tv in series)
        {
            ravi1.Update(tv);
        }

        // Collect streaming results
        var ravi2 = new Ravi(5, 20);
        var streamResults = new List<double>();

        foreach (var tv in series)
        {
            streamResults.Add(ravi2.Update(tv).Value);
        }

        Assert.Equal(streamResults.Count, eventResults.Count);
        for (int i = 0; i < streamResults.Count; i++)
        {
            Assert.Equal(streamResults[i], eventResults[i], 1e-10);
        }
    }

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        int shortP = 5;
        int longP = 20;
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch
        var batchSeries = Ravi.Batch(series, shortP, longP);
        double expected = batchSeries.Last.Value;

        // 2. Span
        var values = series.Values.ToArray();
        var spanOutput = new double[values.Length];
        Ravi.Batch(values, spanOutput, shortP, longP);
        double spanResult = spanOutput[^1];

        // 3. Streaming
        var streamingInd = new Ravi(shortP, longP);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing
        var pubSource = new TSeries();
        var eventingInd = new Ravi(pubSource, shortP, longP);
        for (int i = 0; i < series.Count; i++)
        {
            pubSource.Add(series[i]);
        }
        double eventingResult = eventingInd.Last.Value;

        Assert.Equal(expected, spanResult, 1e-9);
        Assert.Equal(expected, streamingResult, 1e-9);
        Assert.Equal(expected, eventingResult, 1e-9);
    }

    // ============== G) Span API Tests ==============

    [Fact]
    public void SpanBatch_ValidatesLengths()
    {
        double[] source = new double[10];
        double[] output = new double[5]; // too small

        Assert.Throws<ArgumentException>(() => Ravi.Batch(source, output, 3, 10));
    }

    [Fact]
    public void SpanBatch_ValidatesShortPeriod()
    {
        double[] source = new double[10];
        double[] output = new double[10];

        var ex = Assert.Throws<ArgumentException>(() => Ravi.Batch(source, output, 0, 10));
        Assert.Equal("shortPeriod", ex.ParamName);
    }

    [Fact]
    public void SpanBatch_ValidatesLongPeriod()
    {
        double[] source = new double[10];
        double[] output = new double[10];

        var ex = Assert.Throws<ArgumentException>(() => Ravi.Batch(source, output, 3, 0));
        Assert.Equal("longPeriod", ex.ParamName);
    }

    [Fact]
    public void SpanBatch_ValidatesShortLessThanLong()
    {
        double[] source = new double[10];
        double[] output = new double[10];

        var ex = Assert.Throws<ArgumentException>(() => Ravi.Batch(source, output, 10, 5));
        Assert.Equal("shortPeriod", ex.ParamName);
    }

    [Fact]
    public void SpanBatch_EmptyInput_NoOp()
    {
        double[] source = Array.Empty<double>();
        double[] output = Array.Empty<double>();

        var ex = Record.Exception(() => Ravi.Batch(source, output, 3, 10));
        Assert.Null(ex);
    }

    [Fact]
    public void SpanBatch_NaN_HandledGracefully()
    {
        double[] source = { 100, 101, double.NaN, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112 };
        double[] output = new double[source.Length];

        Ravi.Batch(source, output, 3, 10);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"Output[{i}] should be finite but was {output[i]}");
        }
    }

    [Fact]
    public void SpanBatch_MatchesTSeriesCalc()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // TSeries path
        var tsResults = Ravi.Batch(series, 5, 20);

        // Span path
        var values = series.Values.ToArray();
        var spanOutput = new double[values.Length];
        Ravi.Batch(values, spanOutput, 5, 20);

        for (int i = 0; i < values.Length; i++)
        {
            Assert.Equal(tsResults[i].Value, spanOutput[i], 1e-10);
        }
    }

    // ============== H) Chainability ==============

    [Fact]
    public void Chainability_Works()
    {
        var ravi = new Ravi(5, 20);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var result = ravi.Update(series);
        Assert.Equal(50, result.Count);
        Assert.Equal(ravi.Last.Value, result.Last.Value);
    }

    [Fact]
    public void PubEvent_Fires()
    {
        var ravi = new Ravi(3, 10);
        int eventCount = 0;
        ravi.Pub += (object? _, in TValueEventArgs _) => eventCount++;

        for (int i = 0; i < 15; i++)
        {
            ravi.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100 + i));
        }

        Assert.Equal(15, eventCount);
    }

    [Fact]
    public void Chaining_ViaConstructor_Works()
    {
        // Create a source SMA
        var sma = new Sma(5);
        var ravi = new Ravi(sma, 3, 10);

        var gbm = new GBM();
        var bars = gbm.Fetch(30, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // When SMA updates, chained RAVI should also update
        foreach (var tv in series)
        {
            sma.Update(tv);
        }

        Assert.True(double.IsFinite(ravi.Last.Value));
    }

    // ============== RAVI-Specific Tests ==============

    [Fact]
    public void MonotonicallyIncreasing_ProducesPositiveRavi()
    {
        var ravi = new Ravi(3, 10);
        var baseTime = DateTime.UtcNow;

        // Feed monotonically increasing prices
        for (int i = 0; i < 20; i++)
        {
            ravi.Update(new TValue(baseTime.AddMinutes(i), 100 + i));
        }

        Assert.True(ravi.IsHot);
        Assert.True(ravi.Last.Value > 0, $"RAVI should be positive for trending market, got {ravi.Last.Value}");
    }

    [Fact]
    public void StaticBatch_Works()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var results = Ravi.Batch(series, 7, 65);

        Assert.Equal(100, results.Count);
        Assert.True(double.IsFinite(results.Last.Value));
    }

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var (results, indicator) = Ravi.Calculate(series, 5, 20);

        Assert.Equal(100, results.Count);
        Assert.NotNull(indicator);
        Assert.True(double.IsFinite(indicator.Last.Value));
        Assert.True(indicator.IsHot);
    }
}
