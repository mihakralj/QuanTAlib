namespace QuanTAlib.Tests;

public class VhfTests
{
    // ============== A) Constructor & Parameter Validation ==============

    [Fact]
    public void Constructor_ValidatesPeriod_Zero()
    {
        Assert.Throws<ArgumentException>(() => new Vhf(0));
    }

    [Fact]
    public void Constructor_ValidatesPeriod_One()
    {
        Assert.Throws<ArgumentException>(() => new Vhf(1));
    }

    [Fact]
    public void Constructor_ValidatesPeriod_Negative()
    {
        Assert.Throws<ArgumentException>(() => new Vhf(-5));
    }

    [Fact]
    public void Constructor_DefaultPeriod_Works()
    {
        var vhf = new Vhf();
        Assert.Contains("28", vhf.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_CustomPeriod_Works()
    {
        var vhf = new Vhf(14);
        Assert.Contains("14", vhf.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_Period2_Works()
    {
        var vhf = new Vhf(2);
        Assert.NotNull(vhf);
        Assert.Equal(3, vhf.WarmupPeriod);
    }

    // ============== B) Basic Calculation ==============

    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var vhf = new Vhf(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            vhf.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(double.IsFinite(vhf.Last.Value));
    }

    [Fact]
    public void Calc_ReturnsValue()
    {
        var vhf = new Vhf(5);

        Assert.Equal(0, vhf.Last.Value);

        var result = vhf.Update(new TValue(DateTime.UtcNow, 100));

        Assert.True(double.IsFinite(result.Value));
        Assert.Equal(result.Value, vhf.Last.Value);
    }

    [Fact]
    public void Properties_Accessible()
    {
        var vhf = new Vhf(28);

        Assert.Equal(0, vhf.Last.Value);
        Assert.False(vhf.IsHot);
        Assert.Contains("Vhf", vhf.Name, StringComparison.Ordinal);
        Assert.True(vhf.WarmupPeriod > 0);
        Assert.Equal(29, vhf.WarmupPeriod);
    }

    [Fact]
    public void ConstantPrice_ReturnsZeroAfterWarmup()
    {
        var vhf = new Vhf(5);

        for (int i = 0; i < 20; i++)
        {
            vhf.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100));
        }

        Assert.True(vhf.IsHot);
        Assert.Equal(0.0, vhf.Last.Value, 1e-10);
    }

    [Fact]
    public void OutputAlwaysNonNegative()
    {
        var vhf = new Vhf(10);
        var gbm = new GBM(startPrice: 100.0, mu: -0.5, sigma: 1.0);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            var result = vhf.Update(new TValue(bar.Time, bar.Close));
            Assert.True(result.Value >= 0, $"VHF must be non-negative, got {result.Value}");
        }
    }

    [Fact]
    public void MonotonicIncrease_ProducesHighVhf()
    {
        var vhf = new Vhf(5);
        var baseTime = DateTime.UtcNow;

        // Feed monotonically increasing prices: each bar +1
        // VHF = (high-low) / sum(|changes|) = (5) / (5*1) = 1.0
        for (int i = 0; i < 20; i++)
        {
            vhf.Update(new TValue(baseTime.AddMinutes(i), 100 + i));
        }

        Assert.True(vhf.IsHot);
        // For monotonic increase, VHF should be exactly 1.0
        Assert.Equal(1.0, vhf.Last.Value, 1e-10);
    }

    // ============== C) State Management & Bar Correction ==============

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var vhf = new Vhf(5);

        vhf.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        vhf.Update(new TValue(DateTime.UtcNow.AddMinutes(1), 105), isNew: true);

        Assert.True(vhf.Last.Value >= 0);
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var vhf = new Vhf(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        var bars = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed 10 bars to get past warmup
        for (int i = 0; i < 10; i++)
        {
            vhf.Update(new TValue(bars[i].Time, bars[i].Close), isNew: true);
        }

        double beforeUpdate = vhf.Last.Value;

        // Correct with a very different value
        vhf.Update(new TValue(bars[9].Time, bars[9].Close * 2), isNew: false);
        double afterUpdate = vhf.Last.Value;

        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var vhf = new Vhf(5);
        var gbm = new GBM();
        var bars = gbm.Fetch(30, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed first 14
        for (int i = 0; i < 14; i++)
        {
            vhf.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        // Feed 15th bar (isNew=true)
        vhf.Update(new TValue(bars[14].Time, bars[14].Close), true);

        // Correct with modified value (isNew=false)
        double modifiedClose = bars[14].Close + 50.0;
        double val2 = vhf.Update(new TValue(bars[14].Time, modifiedClose), false).Value;

        // Create new instance and feed up to modified
        var vhf2 = new Vhf(5);
        for (int i = 0; i < 14; i++)
        {
            vhf2.Update(new TValue(bars[i].Time, bars[i].Close));
        }
        double val3 = vhf2.Update(new TValue(bars[14].Time, modifiedClose), true).Value;

        Assert.Equal(val3, val2, 1e-9);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var vhf = new Vhf(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);
        var bars = gbm.Fetch(30, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed 10 new values
        TValue tenthValue = default;
        for (int i = 0; i < 10; i++)
        {
            tenthValue = new TValue(bars[i].Time, bars[i].Close);
            vhf.Update(tenthValue, isNew: true);
        }

        // Remember state after 10 values
        double stateAfter10 = vhf.Last.Value;

        // Generate corrections with isNew=false (different values)
        for (int i = 10; i < 20; i++)
        {
            vhf.Update(new TValue(bars[i].Time, bars[i].Close), isNew: false);
        }

        // Feed the remembered 10th value again with isNew=false
        TValue finalResult = vhf.Update(tenthValue, isNew: false);

        // State should match the original state after 10 values
        Assert.Equal(stateAfter10, finalResult.Value, 1e-10);
    }

    [Fact]
    public void Reset_Works()
    {
        var vhf = new Vhf(5);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            vhf.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(vhf.IsHot);

        vhf.Reset();
        Assert.Equal(0, vhf.Last.Value);
        Assert.False(vhf.IsHot);

        // After reset, should accept new values
        vhf.Update(new TValue(bars[0].Time, bars[0].Close));
        Assert.True(double.IsFinite(vhf.Last.Value));
    }

    // ============== D) Warmup & Convergence ==============

    [Fact]
    public void IsHot_BecomesTrueWhenBufferFull()
    {
        var vhf = new Vhf(5);

        Assert.False(vhf.IsHot);

        var baseTime = DateTime.UtcNow;
        // Need period+1 = 6 values for IsHot
        for (int i = 0; i < 5; i++)
        {
            vhf.Update(new TValue(baseTime.AddMinutes(i), 100 + i));
            Assert.False(vhf.IsHot);
        }

        // 6th value should make it hot (close buffer size = period+1 = 6)
        vhf.Update(new TValue(baseTime.AddMinutes(5), 105));
        Assert.True(vhf.IsHot);
    }

    [Fact]
    public void IsHot_IsPeriodDependent()
    {
        var vhf28 = new Vhf(28);
        var vhf5 = new Vhf(5);

        Assert.Equal(29, vhf28.WarmupPeriod);
        Assert.Equal(6, vhf5.WarmupPeriod);
    }

    // ============== E) NaN/Infinity Handling ==============

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var vhf = new Vhf(5);

        for (int i = 0; i < 10; i++)
        {
            vhf.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100 + i));
        }

        // Feed NaN
        var resultAfterNaN = vhf.Update(new TValue(DateTime.UtcNow.AddMinutes(10), double.NaN));

        Assert.True(double.IsFinite(resultAfterNaN.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var vhf = new Vhf(5);

        for (int i = 0; i < 10; i++)
        {
            vhf.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100 + i));
        }

        var resultAfterInf = vhf.Update(new TValue(DateTime.UtcNow.AddMinutes(10), double.PositiveInfinity));
        Assert.True(double.IsFinite(resultAfterInf.Value));

        var resultAfterNegInf = vhf.Update(new TValue(DateTime.UtcNow.AddMinutes(11), double.NegativeInfinity));
        Assert.True(double.IsFinite(resultAfterNegInf.Value));
    }

    [Fact]
    public void MultipleNaN_ContinuesWithLastValid()
    {
        var vhf = new Vhf(5);

        for (int i = 0; i < 10; i++)
        {
            vhf.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100 + i));
        }

        // Feed several NaN values
        for (int i = 0; i < 5; i++)
        {
            var result = vhf.Update(new TValue(DateTime.UtcNow.AddMinutes(10 + i), double.NaN));
            Assert.True(double.IsFinite(result.Value));
        }
    }

    [Fact]
    public void BatchNaN_Safe()
    {
        var vhf = new Vhf(5);
        var gbm = new GBM();
        var bars = gbm.Fetch(30, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed normal values
        for (int i = 0; i < 10; i++)
        {
            vhf.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        // Feed NaN values
        for (int i = 0; i < 5; i++)
        {
            var result = vhf.Update(new TValue(DateTime.UtcNow.AddHours(i + 1), double.NaN));
            Assert.True(double.IsFinite(result.Value));
        }

        // Resume normal
        for (int i = 10; i < 20; i++)
        {
            var result = vhf.Update(new TValue(bars[i].Time, bars[i].Close));
            Assert.True(double.IsFinite(result.Value));
        }
    }

    // ============== F) Consistency Tests ==============

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        var vhfIterative = new Vhf(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // Iterative
        var iterativeResults = new TSeries();
        foreach (var tv in series)
        {
            iterativeResults.Add(vhfIterative.Update(tv));
        }

        // Batch
        var batchResults = Vhf.Batch(series, 10);

        Assert.Equal(iterativeResults.Count, batchResults.Count);
        for (int i = 0; i < iterativeResults.Count; i++)
        {
            Assert.Equal(iterativeResults[i].Value, batchResults[i].Value, 1e-10);
        }
    }

    [Fact]
    public void TSeries_Update_MatchesStreaming()
    {
        var vhf1 = new Vhf(10);
        var vhf2 = new Vhf(10);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // Streaming
        foreach (var tv in series)
        {
            vhf1.Update(tv);
        }

        // Batch via Update(TSeries)
        vhf2.Update(series);

        Assert.Equal(vhf1.Last.Value, vhf2.Last.Value, 1e-10);
    }

    [Fact]
    public void SpanBatch_MatchesStreaming()
    {
        var vhf = new Vhf(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // Streaming
        var streamResults = new double[100];
        for (int i = 0; i < 100; i++)
        {
            streamResults[i] = vhf.Update(series[i]).Value;
        }

        // Span batch
        var values = series.Values.ToArray();
        var spanResults = new double[100];
        Vhf.Batch(values, spanResults, 10);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(streamResults[i], spanResults[i], 1e-10);
        }
    }

    [Fact]
    public void EventBased_MatchesStreaming()
    {
        var vhf1 = new Vhf(10);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // Collect event-based results
        var eventResults = new List<double>();
        vhf1.Pub += (object? _, in TValueEventArgs e) => eventResults.Add(e.Value.Value);

        foreach (var tv in series)
        {
            vhf1.Update(tv);
        }

        // Collect streaming results
        var vhf2 = new Vhf(10);
        var streamResults = new List<double>();

        foreach (var tv in series)
        {
            streamResults.Add(vhf2.Update(tv).Value);
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
        int period = 10;
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch
        var batchSeries = Vhf.Batch(series, period);
        double expected = batchSeries.Last.Value;

        // 2. Span
        var values = series.Values.ToArray();
        var spanOutput = new double[values.Length];
        Vhf.Batch(values, spanOutput, period);
        double spanResult = spanOutput[^1];

        // 3. Streaming
        var streamingInd = new Vhf(period);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing
        var pubSource = new TSeries();
        var eventingInd = new Vhf(pubSource, period);
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

        Assert.Throws<ArgumentException>(() => Vhf.Batch(source, output, 5));
    }

    [Fact]
    public void SpanBatch_ValidatesPeriod()
    {
        double[] source = new double[10];
        double[] output = new double[10];

        var ex = Assert.Throws<ArgumentException>(() => Vhf.Batch(source, output, 1));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void SpanBatch_ValidatesPeriod_Zero()
    {
        double[] source = new double[10];
        double[] output = new double[10];

        var ex = Assert.Throws<ArgumentException>(() => Vhf.Batch(source, output, 0));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void SpanBatch_EmptyInput_NoOp()
    {
        double[] source = Array.Empty<double>();
        double[] output = Array.Empty<double>();

        var ex = Record.Exception(() => Vhf.Batch(source, output, 5));
        Assert.Null(ex);
    }

    [Fact]
    public void SpanBatch_NaN_HandledGracefully()
    {
        double[] source = { 100, 101, double.NaN, 103, 104, 105, 106, 107, 108, 109, 110 };
        double[] output = new double[source.Length];

        Vhf.Batch(source, output, 5);

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
        var tsResults = Vhf.Batch(series, 10);

        // Span path
        var values = series.Values.ToArray();
        var spanOutput = new double[values.Length];
        Vhf.Batch(values, spanOutput, 10);

        for (int i = 0; i < values.Length; i++)
        {
            Assert.Equal(tsResults[i].Value, spanOutput[i], 1e-10);
        }
    }

    // ============== H) Chainability ==============

    [Fact]
    public void Chainability_Works()
    {
        var vhf = new Vhf(10);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var result = vhf.Update(series);
        Assert.Equal(50, result.Count);
        Assert.Equal(vhf.Last.Value, result.Last.Value);
    }

    [Fact]
    public void PubEvent_Fires()
    {
        var vhf = new Vhf(5);
        int eventCount = 0;
        vhf.Pub += (object? _, in TValueEventArgs _) => eventCount++;

        for (int i = 0; i < 15; i++)
        {
            vhf.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100 + i));
        }

        Assert.Equal(15, eventCount);
    }

    [Fact]
    public void Chaining_ViaConstructor_Works()
    {
        // Create a source SMA
        var sma = new Sma(5);
        var vhf = new Vhf(sma, 10);

        var gbm = new GBM();
        var bars = gbm.Fetch(30, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // When SMA updates, chained VHF should also update
        foreach (var tv in series)
        {
            sma.Update(tv);
        }

        Assert.True(double.IsFinite(vhf.Last.Value));
    }

    // ============== VHF-Specific Tests ==============

    [Fact]
    public void StaticBatch_Works()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var results = Vhf.Batch(series, 28);

        Assert.Equal(100, results.Count);
        Assert.True(double.IsFinite(results.Last.Value));
    }

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var (results, indicator) = Vhf.Calculate(series, 10);

        Assert.Equal(100, results.Count);
        Assert.NotNull(indicator);
        Assert.True(double.IsFinite(indicator.Last.Value));
        Assert.True(indicator.IsHot);
    }
}
