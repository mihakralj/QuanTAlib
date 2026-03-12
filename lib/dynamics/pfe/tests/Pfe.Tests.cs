namespace QuanTAlib.Tests;

public class PfeTests
{
    // ============== A) Constructor & Parameter Validation ==============

    [Fact]
    public void Constructor_ValidatesPeriodTooSmall()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Pfe(1, 5));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_ValidatesPeriodZero()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Pfe(0, 5));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_ValidatesPeriodNegative()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Pfe(-5, 5));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void Constructor_ValidatesSmoothPeriodZero()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Pfe(10, 0));
        Assert.Equal("smoothPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_ValidatesSmoothPeriodNegative()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Pfe(10, -1));
        Assert.Equal("smoothPeriod", ex.ParamName);
    }

    [Fact]
    public void Constructor_DefaultParameters_Work()
    {
        var pfe = new Pfe();
        Assert.Contains("10", pfe.Name, StringComparison.Ordinal);
        Assert.Contains("5", pfe.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_CustomParameters_Work()
    {
        var pfe = new Pfe(20, 8);
        Assert.Contains("20", pfe.Name, StringComparison.Ordinal);
        Assert.Contains("8", pfe.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_MinimumPeriods_Work()
    {
        var pfe = new Pfe(2, 1);
        Assert.NotNull(pfe);
    }

    // ============== B) Basic Calculation ==============

    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var pfe = new Pfe(10, 5);
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            pfe.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(double.IsFinite(pfe.Last.Value));
    }

    [Fact]
    public void Calc_ReturnsValue()
    {
        var pfe = new Pfe(5, 3);

        Assert.Equal(0, pfe.Last.Value);

        var result = pfe.Update(new TValue(DateTime.UtcNow, 100));

        Assert.True(double.IsFinite(result.Value));
        Assert.Equal(result.Value, pfe.Last.Value);
    }

    [Fact]
    public void Properties_Accessible()
    {
        var pfe = new Pfe(10, 5);

        Assert.Equal(0, pfe.Last.Value);
        Assert.False(pfe.IsHot);
        Assert.Contains("Pfe", pfe.Name, StringComparison.Ordinal);
        Assert.True(pfe.WarmupPeriod > 0);
        Assert.Equal(11, pfe.WarmupPeriod);
    }

    [Fact]
    public void ConstantPrice_ReturnsHundredAfterWarmup()
    {
        // Constant price: priceDiff=0, straightLine=sqrt(0+period^2)=period
        // fractalPath = period*sqrt(1) = period, efficiency = 100%
        // Sign convention: priceDiff >= 0 → positive, so PFE = +100
        var pfe = new Pfe(5, 3);

        for (int i = 0; i < 30; i++)
        {
            pfe.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100));
        }

        Assert.Equal(100.0, pfe.Last.Value, 1e-4);
    }

    [Fact]
    public void OutputBounded_WhenHot()
    {
        // Raw PFE is always in [-100, +100]. EMA warmup bias compensation
        // (c = 1/(1-e)) can overshoot up to ~5% when IsHot first fires
        // (E <= 0.05 → c ≈ 1.053). Values converge to [-100, +100] as e→0.
        var pfe = new Pfe(10, 5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.5, sigma: 1.0);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            var result = pfe.Update(new TValue(bar.Time, bar.Close));
            if (pfe.IsHot)
            {
                Assert.True(result.Value >= -106 && result.Value <= 106,
                    $"PFE must be approximately in [-100, +100] when hot, got {result.Value}");
            }
        }
    }

    // ============== C) State Management & Bar Correction ==============

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var pfe = new Pfe(5, 3);

        pfe.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        pfe.Update(new TValue(DateTime.UtcNow.AddMinutes(1), 105), isNew: true);

        Assert.True(double.IsFinite(pfe.Last.Value));
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var pfe = new Pfe(5, 3);
        var gbm = new GBM(startPrice: 100.0);
        var bars = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed past warmup
        for (int i = 0; i < 15; i++)
        {
            pfe.Update(new TValue(bars[i].Time, bars[i].Close), isNew: true);
        }

        double beforeUpdate = pfe.Last.Value;

        // Correct with a very different value
        pfe.Update(new TValue(bars[14].Time, bars[14].Close * 2), isNew: false);
        double afterUpdate = pfe.Last.Value;

        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var pfe = new Pfe(5, 3);
        var gbm = new GBM();
        var bars = gbm.Fetch(30, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed first 14
        for (int i = 0; i < 14; i++)
        {
            pfe.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        // Feed 15th bar (isNew=true)
        pfe.Update(new TValue(bars[14].Time, bars[14].Close), true);

        // Correct with modified value (isNew=false)
        double modifiedClose = bars[14].Close + 50.0;
        double val2 = pfe.Update(new TValue(bars[14].Time, modifiedClose), false).Value;

        // Create new instance and feed up to modified
        var pfe2 = new Pfe(5, 3);
        for (int i = 0; i < 14; i++)
        {
            pfe2.Update(new TValue(bars[i].Time, bars[i].Close));
        }
        double val3 = pfe2.Update(new TValue(bars[14].Time, modifiedClose), true).Value;

        Assert.Equal(val3, val2, 1e-9);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var pfe = new Pfe(5, 3);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);
        var bars = gbm.Fetch(30, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed 15 new values
        TValue fifteenthValue = default;
        for (int i = 0; i < 15; i++)
        {
            fifteenthValue = new TValue(bars[i].Time, bars[i].Close);
            pfe.Update(fifteenthValue, isNew: true);
        }

        // Remember state after 15 values
        double stateAfter15 = pfe.Last.Value;

        // Generate corrections with isNew=false (different values)
        for (int i = 15; i < 25; i++)
        {
            pfe.Update(new TValue(bars[i].Time, bars[i].Close), isNew: false);
        }

        // Feed the remembered 15th value again with isNew=false
        TValue finalResult = pfe.Update(fifteenthValue, isNew: false);

        // State should match the original state after 15 values
        Assert.Equal(stateAfter15, finalResult.Value, 1e-10);
    }

    [Fact]
    public void Reset_Works()
    {
        var pfe = new Pfe(5, 3);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            pfe.Update(new TValue(bar.Time, bar.Close));
        }

        pfe.Reset();
        Assert.Equal(0, pfe.Last.Value);
        Assert.False(pfe.IsHot);

        // After reset, should accept new values
        pfe.Update(new TValue(bars[0].Time, bars[0].Close));
        Assert.True(double.IsFinite(pfe.Last.Value));
    }

    // ============== D) Warmup & Convergence ==============

    [Fact]
    public void IsHot_BecomesTrueAfterEnoughData()
    {
        var pfe = new Pfe(5, 3);

        Assert.False(pfe.IsHot);

        var baseTime = DateTime.UtcNow;
        // Feed period+1 = 6 bars to get first raw PFE, then EMA needs more for IsHot
        for (int i = 0; i < 50; i++)
        {
            pfe.Update(new TValue(baseTime.AddMinutes(i), 100 + i));
        }

        Assert.True(pfe.IsHot);
    }

    [Fact]
    public void IsHot_IsPeriodDependent()
    {
        var pfe10_5 = new Pfe(10, 5);
        var pfe5_3 = new Pfe(5, 3);

        Assert.Equal(11, pfe10_5.WarmupPeriod);
        Assert.Equal(6, pfe5_3.WarmupPeriod);
    }

    // ============== E) NaN/Infinity Handling ==============

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var pfe = new Pfe(5, 3);

        for (int i = 0; i < 15; i++)
        {
            pfe.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100 + i));
        }

        // Feed NaN
        var resultAfterNaN = pfe.Update(new TValue(DateTime.UtcNow.AddMinutes(15), double.NaN));

        Assert.True(double.IsFinite(resultAfterNaN.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var pfe = new Pfe(5, 3);

        for (int i = 0; i < 15; i++)
        {
            pfe.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100 + i));
        }

        var resultAfterInf = pfe.Update(new TValue(DateTime.UtcNow.AddMinutes(15), double.PositiveInfinity));
        Assert.True(double.IsFinite(resultAfterInf.Value));

        var resultAfterNegInf = pfe.Update(new TValue(DateTime.UtcNow.AddMinutes(16), double.NegativeInfinity));
        Assert.True(double.IsFinite(resultAfterNegInf.Value));
    }

    [Fact]
    public void MultipleNaN_ContinuesWithLastValid()
    {
        var pfe = new Pfe(5, 3);

        for (int i = 0; i < 15; i++)
        {
            pfe.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100 + i));
        }

        // Feed several NaN values
        for (int i = 0; i < 5; i++)
        {
            var result = pfe.Update(new TValue(DateTime.UtcNow.AddMinutes(15 + i), double.NaN));
            Assert.True(double.IsFinite(result.Value));
        }
    }

    [Fact]
    public void BatchNaN_Safe()
    {
        var pfe = new Pfe(5, 3);
        var gbm = new GBM();
        var bars = gbm.Fetch(30, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed normal values
        for (int i = 0; i < 15; i++)
        {
            pfe.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        // Feed NaN values
        for (int i = 0; i < 5; i++)
        {
            var result = pfe.Update(new TValue(DateTime.UtcNow.AddHours(i + 1), double.NaN));
            Assert.True(double.IsFinite(result.Value));
        }

        // Resume normal
        for (int i = 15; i < 25; i++)
        {
            var result = pfe.Update(new TValue(bars[i].Time, bars[i].Close));
            Assert.True(double.IsFinite(result.Value));
        }
    }

    // ============== F) Consistency Tests ==============

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        var pfeIterative = new Pfe(5, 3);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // Iterative
        var iterativeResults = new TSeries();
        foreach (var tv in series)
        {
            iterativeResults.Add(pfeIterative.Update(tv));
        }

        // Batch
        var batchResults = Pfe.Batch(series, 5, 3);

        Assert.Equal(iterativeResults.Count, batchResults.Count);
        for (int i = 0; i < iterativeResults.Count; i++)
        {
            Assert.Equal(iterativeResults[i].Value, batchResults[i].Value, 1e-10);
        }
    }

    [Fact]
    public void TSeries_Update_MatchesStreaming()
    {
        var pfe1 = new Pfe(5, 3);
        var pfe2 = new Pfe(5, 3);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // Streaming
        foreach (var tv in series)
        {
            pfe1.Update(tv);
        }

        // Batch via Update(TSeries)
        pfe2.Update(series);

        Assert.Equal(pfe1.Last.Value, pfe2.Last.Value, 1e-10);
    }

    [Fact]
    public void SpanBatch_MatchesStreaming()
    {
        var pfe = new Pfe(5, 3);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // Streaming
        var streamResults = new double[100];
        for (int i = 0; i < 100; i++)
        {
            streamResults[i] = pfe.Update(series[i]).Value;
        }

        // Span batch
        var values = series.Values.ToArray();
        var spanResults = new double[100];
        Pfe.Batch(values, spanResults, 5, 3);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(streamResults[i], spanResults[i], 1e-10);
        }
    }

    [Fact]
    public void EventBased_MatchesStreaming()
    {
        var pfe1 = new Pfe(5, 3);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // Collect event-based results
        var eventResults = new List<double>();
        pfe1.Pub += (object? _, in TValueEventArgs e) => eventResults.Add(e.Value.Value);

        foreach (var tv in series)
        {
            pfe1.Update(tv);
        }

        // Collect streaming results
        var pfe2 = new Pfe(5, 3);
        var streamResults = new List<double>();

        foreach (var tv in series)
        {
            streamResults.Add(pfe2.Update(tv).Value);
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
        int period = 5;
        int smooth = 3;
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch
        var batchSeries = Pfe.Batch(series, period, smooth);
        double expected = batchSeries.Last.Value;

        // 2. Span
        var values = series.Values.ToArray();
        var spanOutput = new double[values.Length];
        Pfe.Batch(values, spanOutput, period, smooth);
        double spanResult = spanOutput[^1];

        // 3. Streaming
        var streamingInd = new Pfe(period, smooth);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing
        var pubSource = new TSeries();
        var eventingInd = new Pfe(pubSource, period, smooth);
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

        Assert.Throws<ArgumentException>(() => Pfe.Batch(source, output, 5, 3));
    }

    [Fact]
    public void SpanBatch_ValidatesPeriod()
    {
        double[] source = new double[10];
        double[] output = new double[10];

        var ex = Assert.Throws<ArgumentException>(() => Pfe.Batch(source, output, 1, 5));
        Assert.Equal("period", ex.ParamName);
    }

    [Fact]
    public void SpanBatch_ValidatesSmoothPeriod()
    {
        double[] source = new double[10];
        double[] output = new double[10];

        var ex = Assert.Throws<ArgumentException>(() => Pfe.Batch(source, output, 10, 0));
        Assert.Equal("smoothPeriod", ex.ParamName);
    }

    [Fact]
    public void SpanBatch_EmptyInput_NoOp()
    {
        double[] source = Array.Empty<double>();
        double[] output = Array.Empty<double>();

        var ex = Record.Exception(() => Pfe.Batch(source, output, 5, 3));
        Assert.Null(ex);
    }

    [Fact]
    public void SpanBatch_NaN_HandledGracefully()
    {
        double[] source = { 100, 101, double.NaN, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112 };
        double[] output = new double[source.Length];

        Pfe.Batch(source, output, 5, 3);

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
        var tsResults = Pfe.Batch(series, 5, 3);

        // Span path
        var values = series.Values.ToArray();
        var spanOutput = new double[values.Length];
        Pfe.Batch(values, spanOutput, 5, 3);

        for (int i = 0; i < values.Length; i++)
        {
            Assert.Equal(tsResults[i].Value, spanOutput[i], 1e-10);
        }
    }

    // ============== H) Chainability ==============

    [Fact]
    public void Chainability_Works()
    {
        var pfe = new Pfe(5, 3);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var result = pfe.Update(series);
        Assert.Equal(50, result.Count);
        Assert.Equal(pfe.Last.Value, result.Last.Value);
    }

    [Fact]
    public void PubEvent_Fires()
    {
        var pfe = new Pfe(5, 3);
        int eventCount = 0;
        pfe.Pub += (object? _, in TValueEventArgs _) => eventCount++;

        for (int i = 0; i < 15; i++)
        {
            pfe.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100 + i));
        }

        Assert.Equal(15, eventCount);
    }

    [Fact]
    public void Chaining_ViaConstructor_Works()
    {
        // Create a source SMA
        var sma = new Sma(5);
        var pfe = new Pfe(sma, 5, 3);

        var gbm = new GBM();
        var bars = gbm.Fetch(30, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // When SMA updates, chained PFE should also update
        foreach (var tv in series)
        {
            sma.Update(tv);
        }

        Assert.True(double.IsFinite(pfe.Last.Value));
    }

    // ============== PFE-Specific Tests ==============

    [Fact]
    public void MonotonicIncrease_ProducesPositivePfe()
    {
        var pfe = new Pfe(5, 3);
        var baseTime = DateTime.UtcNow;

        // Feed strictly increasing prices (equal steps)
        for (int i = 0; i < 30; i++)
        {
            pfe.Update(new TValue(baseTime.AddMinutes(i), 100 + i));
        }

        Assert.True(pfe.Last.Value > 0, $"PFE should be positive for uptrend, got {pfe.Last.Value}");
    }

    [Fact]
    public void MonotonicDecrease_ProducesNegativePfe()
    {
        var pfe = new Pfe(5, 3);
        var baseTime = DateTime.UtcNow;

        // Feed strictly decreasing prices
        for (int i = 0; i < 30; i++)
        {
            pfe.Update(new TValue(baseTime.AddMinutes(i), 200 - i));
        }

        Assert.True(pfe.Last.Value < 0, $"PFE should be negative for downtrend, got {pfe.Last.Value}");
    }

    [Fact]
    public void StaticBatch_Works()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var results = Pfe.Batch(series, 10, 5);

        Assert.Equal(100, results.Count);
        Assert.True(double.IsFinite(results.Last.Value));
    }

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var (results, indicator) = Pfe.Calculate(series, 5, 3);

        Assert.Equal(100, results.Count);
        Assert.NotNull(indicator);
        Assert.True(double.IsFinite(indicator.Last.Value));
    }
}
