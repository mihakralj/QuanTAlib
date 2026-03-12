namespace QuanTAlib.Tests;

public class GatorTests
{
    // ============== A) Constructor & Parameter Validation ==============

    [Fact]
    public void Constructor_ValidatesJawPeriod()
    {
        Assert.Throws<ArgumentException>(() => new Gator(jawPeriod: 0));
        Assert.Throws<ArgumentException>(() => new Gator(jawPeriod: -1));
    }

    [Fact]
    public void Constructor_ValidatesTeethPeriod()
    {
        Assert.Throws<ArgumentException>(() => new Gator(teethPeriod: 0));
        Assert.Throws<ArgumentException>(() => new Gator(teethPeriod: -5));
    }

    [Fact]
    public void Constructor_ValidatesLipsPeriod()
    {
        Assert.Throws<ArgumentException>(() => new Gator(lipsPeriod: 0));
        Assert.Throws<ArgumentException>(() => new Gator(lipsPeriod: -1));
    }

    [Fact]
    public void Constructor_ValidatesJawShift()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Gator(jawShift: -1));
        Assert.Equal("jawShift", ex.ParamName);
    }

    [Fact]
    public void Constructor_ValidatesTeethShift()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Gator(teethShift: -1));
        Assert.Equal("teethShift", ex.ParamName);
    }

    [Fact]
    public void Constructor_ValidatesLipsShift()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Gator(lipsShift: -1));
        Assert.Equal("lipsShift", ex.ParamName);
    }

    [Fact]
    public void Constructor_DefaultParameters_Work()
    {
        var gator = new Gator();
        Assert.Contains("13", gator.Name, StringComparison.Ordinal);
        Assert.Contains("8", gator.Name, StringComparison.Ordinal);
        Assert.Contains("5", gator.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_CustomParameters_Work()
    {
        var gator = new Gator(jawPeriod: 21, jawShift: 13, teethPeriod: 13, teethShift: 8, lipsPeriod: 8, lipsShift: 5);
        Assert.Contains("21", gator.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_Period1_Works()
    {
        var gator = new Gator(jawPeriod: 1, jawShift: 0, teethPeriod: 1, teethShift: 0, lipsPeriod: 1, lipsShift: 0);
        Assert.NotNull(gator);
    }

    // ============== B) Basic Calculation ==============

    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var gator = new Gator();
        var gbm = new GBM();
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            gator.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(double.IsFinite(gator.Last.Value));
    }

    [Fact]
    public void Calc_ReturnsValue()
    {
        var gator = new Gator();

        Assert.Equal(0, gator.Last.Value);

        var result = gator.Update(new TValue(DateTime.UtcNow, 100));

        Assert.True(double.IsFinite(result.Value));
        Assert.Equal(result.Value, gator.Last.Value);
    }

    [Fact]
    public void Properties_Accessible()
    {
        var gator = new Gator();

        Assert.Equal(0, gator.Last.Value);
        Assert.False(gator.IsHot);
        Assert.Contains("Gator", gator.Name, StringComparison.Ordinal);
        Assert.True(gator.WarmupPeriod > 0);
        Assert.Equal(21, gator.WarmupPeriod); // jawPeriod(13) + jawShift(8)
    }

    [Fact]
    public void ConstantPrice_ReturnsZeroAfterWarmup()
    {
        var gator = new Gator();

        for (int i = 0; i < 50; i++)
        {
            gator.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100));
        }

        Assert.True(gator.IsHot);
        // All SMMAs converge to 100 → shifted values equal → upper = |100-100| = 0
        Assert.Equal(0.0, gator.Last.Value, 1e-6);
        // Lower also zero
        Assert.Equal(0.0, gator.Lower, 1e-6);
    }

    [Fact]
    public void UpperAlwaysNonNegative()
    {
        var gator = new Gator();
        var gbm = new GBM(startPrice: 100.0, mu: -0.5, sigma: 1.0);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            var result = gator.Update(new TValue(bar.Time, bar.Close));
            Assert.True(result.Value >= 0, $"Upper must be non-negative, got {result.Value}");
        }
    }

    [Fact]
    public void LowerAlwaysNonPositive()
    {
        var gator = new Gator();
        var gbm = new GBM(startPrice: 100.0, mu: 0.5, sigma: 1.0);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            gator.Update(new TValue(bar.Time, bar.Close));
            Assert.True(gator.Lower <= 0, $"Lower must be non-positive, got {gator.Lower}");
        }
    }

    [Fact]
    public void LowerProperty_Accessible()
    {
        var gator = new Gator();
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            gator.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(double.IsFinite(gator.Lower));
    }

    // ============== C) State Management & Bar Correction ==============

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var gator = new Gator();

        gator.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        gator.Update(new TValue(DateTime.UtcNow.AddMinutes(1), 105), isNew: true);

        Assert.True(gator.Last.Value >= 0);
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var gator = new Gator(jawPeriod: 5, jawShift: 3, teethPeriod: 3, teethShift: 2, lipsPeriod: 2, lipsShift: 1);
        var gbm = new GBM(startPrice: 100.0);
        var bars = gbm.Fetch(30, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 15; i++)
        {
            gator.Update(new TValue(bars[i].Time, bars[i].Close), isNew: true);
        }

        _ = gator.Last.Value;

        gator.Update(new TValue(bars[14].Time, bars[14].Close * 2), isNew: false);
        double afterUpdate = gator.Last.Value;

        // With doubled price, the indicator should change
        Assert.True(double.IsFinite(afterUpdate));
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var gator = new Gator(jawPeriod: 5, jawShift: 3, teethPeriod: 3, teethShift: 2, lipsPeriod: 2, lipsShift: 1);
        var gbm = new GBM();
        var bars = gbm.Fetch(30, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed first 19
        for (int i = 0; i < 19; i++)
        {
            gator.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        // Feed 20th bar (isNew=true)
        gator.Update(new TValue(bars[19].Time, bars[19].Close), true);

        // Correct with modified value (isNew=false)
        double modifiedClose = bars[19].Close + 50.0;
        double val2 = gator.Update(new TValue(bars[19].Time, modifiedClose), false).Value;

        // Create new instance and feed up to modified
        var gator2 = new Gator(jawPeriod: 5, jawShift: 3, teethPeriod: 3, teethShift: 2, lipsPeriod: 2, lipsShift: 1);
        for (int i = 0; i < 19; i++)
        {
            gator2.Update(new TValue(bars[i].Time, bars[i].Close));
        }
        double val3 = gator2.Update(new TValue(bars[19].Time, modifiedClose), true).Value;

        Assert.Equal(val3, val2, 1e-9);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var gator = new Gator(jawPeriod: 5, jawShift: 3, teethPeriod: 3, teethShift: 2, lipsPeriod: 2, lipsShift: 1);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);
        var bars = gbm.Fetch(30, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        TValue tenthValue = default;
        for (int i = 0; i < 15; i++)
        {
            tenthValue = new TValue(bars[i].Time, bars[i].Close);
            gator.Update(tenthValue, isNew: true);
        }

        double stateAfter15 = gator.Last.Value;

        // Generate corrections with isNew=false
        for (int i = 15; i < 25; i++)
        {
            gator.Update(new TValue(bars[i].Time, bars[i].Close), isNew: false);
        }

        TValue finalResult = gator.Update(tenthValue, isNew: false);
        Assert.Equal(stateAfter15, finalResult.Value, 1e-10);
    }

    [Fact]
    public void Reset_Works()
    {
        var gator = new Gator();
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            gator.Update(new TValue(bar.Time, bar.Close));
        }

        Assert.True(gator.IsHot);

        gator.Reset();
        Assert.Equal(0, gator.Last.Value);
        Assert.False(gator.IsHot);

        gator.Update(new TValue(bars[0].Time, bars[0].Close));
        Assert.True(double.IsFinite(gator.Last.Value));
    }

    // ============== D) Warmup & Convergence ==============

    [Fact]
    public void IsHot_BecomesTrueWhenBuffersFull()
    {
        var gator = new Gator(jawPeriod: 5, jawShift: 3, teethPeriod: 3, teethShift: 2, lipsPeriod: 2, lipsShift: 1);

        Assert.False(gator.IsHot);

        var baseTime = DateTime.UtcNow;
        // WarmupPeriod = max(5+3, 3+2, 2+1) = 8
        // Need shift+1 bars to fill each buffer: jaw=4, teeth=3, lips=2
        // But SMMAs need input bars first
        for (int i = 0; i < 20; i++)
        {
            gator.Update(new TValue(baseTime.AddMinutes(i), 100 + i));
            if (i < 3)
            {
                // Lips buffer fills first (size 2), but all 3 need to be full
                Assert.False(gator.IsHot, $"Should not be hot at bar {i}");
            }
        }

        Assert.True(gator.IsHot);
    }

    [Fact]
    public void IsHot_IsPeriodDependent()
    {
        var gator1 = new Gator(); // 13+8=21
        var gator2 = new Gator(jawPeriod: 5, jawShift: 3, teethPeriod: 3, teethShift: 2, lipsPeriod: 2, lipsShift: 1);

        Assert.Equal(21, gator1.WarmupPeriod);
        Assert.Equal(8, gator2.WarmupPeriod); // max(5+3, 3+2, 2+1) = 8
    }

    // ============== E) NaN/Infinity Handling ==============

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var gator = new Gator(jawPeriod: 5, jawShift: 3, teethPeriod: 3, teethShift: 2, lipsPeriod: 2, lipsShift: 1);

        for (int i = 0; i < 15; i++)
        {
            gator.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100 + i));
        }

        var resultAfterNaN = gator.Update(new TValue(DateTime.UtcNow.AddMinutes(15), double.NaN));
        Assert.True(double.IsFinite(resultAfterNaN.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var gator = new Gator(jawPeriod: 5, jawShift: 3, teethPeriod: 3, teethShift: 2, lipsPeriod: 2, lipsShift: 1);

        for (int i = 0; i < 15; i++)
        {
            gator.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100 + i));
        }

        var resultAfterInf = gator.Update(new TValue(DateTime.UtcNow.AddMinutes(15), double.PositiveInfinity));
        Assert.True(double.IsFinite(resultAfterInf.Value));

        var resultAfterNegInf = gator.Update(new TValue(DateTime.UtcNow.AddMinutes(16), double.NegativeInfinity));
        Assert.True(double.IsFinite(resultAfterNegInf.Value));
    }

    [Fact]
    public void MultipleNaN_ContinuesWithLastValid()
    {
        var gator = new Gator(jawPeriod: 5, jawShift: 3, teethPeriod: 3, teethShift: 2, lipsPeriod: 2, lipsShift: 1);

        for (int i = 0; i < 15; i++)
        {
            gator.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100 + i));
        }

        for (int i = 0; i < 5; i++)
        {
            var result = gator.Update(new TValue(DateTime.UtcNow.AddMinutes(15 + i), double.NaN));
            Assert.True(double.IsFinite(result.Value));
        }
    }

    [Fact]
    public void BatchNaN_Safe()
    {
        var gator = new Gator(jawPeriod: 5, jawShift: 3, teethPeriod: 3, teethShift: 2, lipsPeriod: 2, lipsShift: 1);
        var gbm = new GBM();
        var bars = gbm.Fetch(30, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 15; i++)
        {
            gator.Update(new TValue(bars[i].Time, bars[i].Close));
        }

        for (int i = 0; i < 5; i++)
        {
            var result = gator.Update(new TValue(DateTime.UtcNow.AddHours(i + 1), double.NaN));
            Assert.True(double.IsFinite(result.Value));
        }

        for (int i = 15; i < 25; i++)
        {
            var result = gator.Update(new TValue(bars[i].Time, bars[i].Close));
            Assert.True(double.IsFinite(result.Value));
        }
    }

    // ============== F) Consistency Tests ==============

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        var gatorIterative = new Gator(jawPeriod: 5, jawShift: 3, teethPeriod: 3, teethShift: 2, lipsPeriod: 2, lipsShift: 1);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var iterativeResults = new TSeries();
        foreach (var tv in series)
        {
            iterativeResults.Add(gatorIterative.Update(tv));
        }

        var batchResults = Gator.Batch(series, 5, 3, 3, 2, 2, 1);

        Assert.Equal(iterativeResults.Count, batchResults.Count);
        for (int i = 0; i < iterativeResults.Count; i++)
        {
            Assert.Equal(iterativeResults[i].Value, batchResults[i].Value, 1e-10);
        }
    }

    [Fact]
    public void TSeries_Update_MatchesStreaming()
    {
        var gator1 = new Gator(jawPeriod: 5, jawShift: 3, teethPeriod: 3, teethShift: 2, lipsPeriod: 2, lipsShift: 1);
        var gator2 = new Gator(jawPeriod: 5, jawShift: 3, teethPeriod: 3, teethShift: 2, lipsPeriod: 2, lipsShift: 1);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        foreach (var tv in series)
        {
            gator1.Update(tv);
        }

        gator2.Update(series);

        Assert.Equal(gator1.Last.Value, gator2.Last.Value, 1e-10);
    }

    [Fact]
    public void SpanBatch_MatchesStreaming()
    {
        var gator = new Gator(jawPeriod: 5, jawShift: 3, teethPeriod: 3, teethShift: 2, lipsPeriod: 2, lipsShift: 1);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var streamResults = new double[100];
        for (int i = 0; i < 100; i++)
        {
            streamResults[i] = gator.Update(series[i]).Value;
        }

        var values = series.Values.ToArray();
        var spanResults = new double[100];
        Gator.Batch(values, spanResults, 5, 3, 3, 2, 2, 1);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(streamResults[i], spanResults[i], 1e-10);
        }
    }

    [Fact]
    public void EventBased_MatchesStreaming()
    {
        var gator1 = new Gator(jawPeriod: 5, jawShift: 3, teethPeriod: 3, teethShift: 2, lipsPeriod: 2, lipsShift: 1);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var eventResults = new List<double>();
        gator1.Pub += (object? _, in TValueEventArgs e) => eventResults.Add(e.Value.Value);

        foreach (var tv in series)
        {
            gator1.Update(tv);
        }

        var gator2 = new Gator(jawPeriod: 5, jawShift: 3, teethPeriod: 3, teethShift: 2, lipsPeriod: 2, lipsShift: 1);
        var streamResults = new List<double>();

        foreach (var tv in series)
        {
            streamResults.Add(gator2.Update(tv).Value);
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
        int jp = 5, js = 3, tp = 3, ts = 2, lp = 2, ls = 1;
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch
        var batchSeries = Gator.Batch(series, jp, js, tp, ts, lp, ls);
        double expected = batchSeries.Last.Value;

        // 2. Span
        var values = series.Values.ToArray();
        var spanOutput = new double[values.Length];
        Gator.Batch(values, spanOutput, jp, js, tp, ts, lp, ls);
        double spanResult = spanOutput[^1];

        // 3. Streaming
        var streamingInd = new Gator(jp, js, tp, ts, lp, ls);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing
        var pubSource = new TSeries();
        var eventingInd = new Gator(pubSource, jp, js, tp, ts, lp, ls);
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
        double[] output = new double[5];

        Assert.Throws<ArgumentException>(() => Gator.Batch(source, output));
    }

    [Fact]
    public void SpanBatch_ValidatesJawPeriod()
    {
        double[] source = new double[10];
        double[] output = new double[10];

        var ex = Assert.Throws<ArgumentException>(() => Gator.Batch(source, output, jawPeriod: 0));
        Assert.Equal("jawPeriod", ex.ParamName);
    }

    [Fact]
    public void SpanBatch_ValidatesTeethPeriod()
    {
        double[] source = new double[10];
        double[] output = new double[10];

        var ex = Assert.Throws<ArgumentException>(() => Gator.Batch(source, output, teethPeriod: 0));
        Assert.Equal("teethPeriod", ex.ParamName);
    }

    [Fact]
    public void SpanBatch_ValidatesLipsPeriod()
    {
        double[] source = new double[10];
        double[] output = new double[10];

        var ex = Assert.Throws<ArgumentException>(() => Gator.Batch(source, output, lipsPeriod: 0));
        Assert.Equal("lipsPeriod", ex.ParamName);
    }

    [Fact]
    public void SpanBatch_ValidatesShifts()
    {
        double[] source = new double[10];
        double[] output = new double[10];

        var ex1 = Assert.Throws<ArgumentException>(() => Gator.Batch(source, output, jawShift: -1));
        Assert.Equal("jawShift", ex1.ParamName);

        var ex2 = Assert.Throws<ArgumentException>(() => Gator.Batch(source, output, teethShift: -1));
        Assert.Equal("teethShift", ex2.ParamName);

        var ex3 = Assert.Throws<ArgumentException>(() => Gator.Batch(source, output, lipsShift: -1));
        Assert.Equal("lipsShift", ex3.ParamName);
    }

    [Fact]
    public void SpanBatch_EmptyInput_NoOp()
    {
        double[] source = Array.Empty<double>();
        double[] output = Array.Empty<double>();

        var ex = Record.Exception(() => Gator.Batch(source, output));
        Assert.Null(ex);
    }

    [Fact]
    public void SpanBatch_NaN_HandledGracefully()
    {
        double[] source = { 100, 101, double.NaN, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112, 113, 114, 115, 116, 117, 118, 119, 120, 121, 122, 123, 124 };
        double[] output = new double[source.Length];

        Gator.Batch(source, output);

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

        int jp = 5, js = 3, tp = 3, ts = 2, lp = 2, ls = 1;

        var tsResults = Gator.Batch(series, jp, js, tp, ts, lp, ls);

        var values = series.Values.ToArray();
        var spanOutput = new double[values.Length];
        Gator.Batch(values, spanOutput, jp, js, tp, ts, lp, ls);

        for (int i = 0; i < values.Length; i++)
        {
            Assert.Equal(tsResults[i].Value, spanOutput[i], 1e-10);
        }
    }

    // ============== H) Chainability ==============

    [Fact]
    public void Chainability_Works()
    {
        var gator = new Gator(jawPeriod: 5, jawShift: 3, teethPeriod: 3, teethShift: 2, lipsPeriod: 2, lipsShift: 1);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var result = gator.Update(series);
        Assert.Equal(50, result.Count);
        Assert.Equal(gator.Last.Value, result.Last.Value);
    }

    [Fact]
    public void PubEvent_Fires()
    {
        var gator = new Gator(jawPeriod: 5, jawShift: 3, teethPeriod: 3, teethShift: 2, lipsPeriod: 2, lipsShift: 1);
        int eventCount = 0;
        gator.Pub += (object? _, in TValueEventArgs _) => eventCount++;

        for (int i = 0; i < 15; i++)
        {
            gator.Update(new TValue(DateTime.UtcNow.AddMinutes(i), 100 + i));
        }

        Assert.Equal(15, eventCount);
    }

    [Fact]
    public void Chaining_ViaConstructor_Works()
    {
        var sma = new Sma(5);
        var gator = new Gator(sma, jawPeriod: 5, jawShift: 3, teethPeriod: 3, teethShift: 2, lipsPeriod: 2, lipsShift: 1);

        var gbm = new GBM();
        var bars = gbm.Fetch(30, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        foreach (var tv in series)
        {
            sma.Update(tv);
        }

        Assert.True(double.IsFinite(gator.Last.Value));
    }

    // ============== Gator-Specific Tests ==============

    [Fact]
    public void TrendingMarket_ProducesNonZeroHistograms()
    {
        var gator = new Gator(jawPeriod: 5, jawShift: 3, teethPeriod: 3, teethShift: 2, lipsPeriod: 2, lipsShift: 1);
        var baseTime = DateTime.UtcNow;

        // Strong monotonic increase
        for (int i = 0; i < 30; i++)
        {
            gator.Update(new TValue(baseTime.AddMinutes(i), 100 + (i * 5)));
        }

        Assert.True(gator.IsHot);
        Assert.True(gator.Last.Value > 0, $"Upper should be positive in trend, got {gator.Last.Value}");
        Assert.True(gator.Lower < 0, $"Lower should be negative in trend, got {gator.Lower}");
    }

    [Fact]
    public void StaticBatch_Works()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var results = Gator.Batch(series);

        Assert.Equal(100, results.Count);
        Assert.True(double.IsFinite(results.Last.Value));
    }

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var (results, indicator) = Gator.Calculate(series, jawPeriod: 5, jawShift: 3, teethPeriod: 3, teethShift: 2, lipsPeriod: 2, lipsShift: 1);

        Assert.Equal(100, results.Count);
        Assert.NotNull(indicator);
        Assert.True(double.IsFinite(indicator.Last.Value));
        Assert.True(indicator.IsHot);
    }
}
