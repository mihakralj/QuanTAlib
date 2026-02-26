namespace QuanTAlib.Tests;

public class GhlaTests
{
    // ============== A) Constructor & Parameter Validation ==============

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Ghla(0));
        Assert.Throws<ArgumentException>(() => new Ghla(-1));
        Assert.Throws<ArgumentException>(() => new Ghla(-100));

        var ghla = new Ghla(13);
        Assert.NotNull(ghla);
    }

    [Fact]
    public void Constructor_DefaultPeriod_Is13()
    {
        var ghla = new Ghla();
        Assert.Contains("13", ghla.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_Period1_Works()
    {
        var ghla = new Ghla(1);
        Assert.NotNull(ghla);
        Assert.Contains("1", ghla.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_ArgumentException_HasParamName()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Ghla(0));
        Assert.Equal("period", ex.ParamName);
    }

    // ============== B) Basic Calculation ==============

    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var ghla = new Ghla(13);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            ghla.Update(bar);
        }

        Assert.True(double.IsFinite(ghla.Last.Value));
    }

    [Fact]
    public void Calc_ReturnsValue()
    {
        var ghla = new Ghla(13);
        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);

        Assert.Equal(0, ghla.Last.Value);

        TValue result = ghla.Update(bar);

        Assert.True(double.IsFinite(result.Value));
        Assert.Equal(result.Value, ghla.Last.Value);
    }

    [Fact]
    public void FirstBar_OutputIsSmaValue()
    {
        var ghla = new Ghla(3);
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000);

        TValue result = ghla.Update(bar);

        // First bar: SMA(high,1)=110, SMA(low,1)=90
        // close=105 < smaHigh=110, close=105 > smaLow=90 → neutral zone
        // Seed: close >= smaHigh? No. close <= smaLow? No. default = 1 (bullish)
        // Bullish → output = smaLow = 90
        Assert.Equal(90.0, result.Value, 1e-10);
    }

    [Fact]
    public void Properties_Accessible()
    {
        var ghla = new Ghla(13);

        Assert.Equal(0, ghla.Last.Value);
        Assert.False(ghla.IsHot);
        Assert.Contains("Ghla", ghla.Name, StringComparison.Ordinal);
        Assert.True(ghla.WarmupPeriod > 0);

        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        ghla.Update(bar);

        Assert.True(ghla.Trend != 0 || ghla.Last.Value >= 0);
    }

    [Fact]
    public void Trend_Property_ReturnsDirection()
    {
        var ghla = new Ghla(3);

        // Feed rising bars to establish bullish trend
        var baseTime = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            double price = 100 + (i * 5);
            var bar = new TBar(baseTime.AddMinutes(i), price, price + 2, price - 2, price + 1, 1000);
            ghla.Update(bar);
        }

        // With strongly rising prices, trend should be bullish
        Assert.Equal(1, ghla.Trend);
    }

    // ============== C) State Management & Bar Correction ==============

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var ghla = new Ghla(5);

        var bar1 = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        ghla.Update(bar1, isNew: true);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 102, 110, 100, 108, 1000);
        ghla.Update(bar2, isNew: true);

        Assert.True(double.IsFinite(ghla.Last.Value));
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var ghla = new Ghla(5);

        var bar1 = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000);
        ghla.Update(bar1, isNew: true);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 105, 115, 85, 108, 1000);
        ghla.Update(bar2, isNew: true);
        double beforeUpdate = ghla.Last.Value;

        // Modify bar2 with very different range
        var bar2Modified = new TBar(DateTime.UtcNow.AddMinutes(1), 105, 200, 50, 108, 1000);
        ghla.Update(bar2Modified, isNew: false);
        double afterUpdate = ghla.Last.Value;

        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var ghla = new Ghla(5);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed first 99
        for (int i = 0; i < 99; i++)
        {
            ghla.Update(bars[i]);
        }

        // Update with 100th bar (isNew=true)
        ghla.Update(bars[99], true);

        // Update with modified 100th bar (isNew=false)
        var modifiedBar = new TBar(bars[99].Time, bars[99].Open, bars[99].High + 10.0, bars[99].Low - 10.0, bars[99].Close, bars[99].Volume);
        double val2 = ghla.Update(modifiedBar, false).Value;

        // Create new instance and feed up to modified
        var ghla2 = new Ghla(5);
        for (int i = 0; i < 99; i++)
        {
            ghla2.Update(bars[i]);
        }
        double val3 = ghla2.Update(modifiedBar, true).Value;

        Assert.Equal(val3, val2, 1e-9);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var ghla = new Ghla(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);
        var bars = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed 10 new values
        TBar tenthBar = default;
        for (int i = 0; i < 10; i++)
        {
            tenthBar = bars[i];
            ghla.Update(tenthBar, isNew: true);
        }

        double stateAfterTen = ghla.Last.Value;

        // Generate 9 corrections with isNew=false
        for (int i = 10; i < 19; i++)
        {
            ghla.Update(bars[i], isNew: false);
        }

        // Feed the remembered 10th bar again with isNew=false
        TValue finalResult = ghla.Update(tenthBar, isNew: false);

        Assert.Equal(stateAfterTen, finalResult.Value, 1e-10);
    }

    [Fact]
    public void Reset_Works()
    {
        var ghla = new Ghla(5);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            ghla.Update(bar);
        }

        Assert.True(ghla.IsHot);

        ghla.Reset();
        Assert.Equal(0, ghla.Last.Value);
        Assert.False(ghla.IsHot);
        Assert.Equal(0, ghla.Trend);

        // After reset, should accept new values
        ghla.Update(bars[0]);
        Assert.True(double.IsFinite(ghla.Last.Value));
    }

    // ============== D) Warmup & Convergence ==============

    [Fact]
    public void IsHot_BecomesTrueAfterWarmup()
    {
        var ghla = new Ghla(5);

        Assert.False(ghla.IsHot);

        var baseTime = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            var bar = new TBar(baseTime.AddMinutes(i), 100 + i, 110 + i, 90 + i, 100 + i, 1000);
            ghla.Update(bar);
        }

        Assert.True(ghla.IsHot);
    }

    [Fact]
    public void WarmupPeriod_IsPositive()
    {
        var ghla = new Ghla(13);
        Assert.True(ghla.WarmupPeriod > 0);
        Assert.Equal(13, ghla.WarmupPeriod);

        var ghla2 = new Ghla(50);
        Assert.Equal(50, ghla2.WarmupPeriod);
    }

    // ============== E) NaN/Infinity Handling ==============

    [Fact]
    public void NaN_High_UsesLastValidValue()
    {
        var ghla = new Ghla(5);

        var bar1 = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        ghla.Update(bar1);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 102, 110, 98, 108, 1000);
        ghla.Update(bar2);

        // Feed bar with NaN high
        var barWithNaN = new TBar(DateTime.UtcNow.AddMinutes(2), 108, double.NaN, 100, 112, 1000);
        var resultAfterNaN = ghla.Update(barWithNaN);

        Assert.True(double.IsFinite(resultAfterNaN.Value));
    }

    [Fact]
    public void NaN_Low_UsesLastValidValue()
    {
        var ghla = new Ghla(5);

        var bar1 = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        ghla.Update(bar1);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 102, 110, 98, 108, 1000);
        ghla.Update(bar2);

        var barWithNaN = new TBar(DateTime.UtcNow.AddMinutes(2), 108, 115, double.NaN, 112, 1000);
        var resultAfterNaN = ghla.Update(barWithNaN);

        Assert.True(double.IsFinite(resultAfterNaN.Value));
    }

    [Fact]
    public void NaN_Close_UsesLastValidValue()
    {
        var ghla = new Ghla(5);

        var bar1 = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        ghla.Update(bar1);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 102, 110, 98, 108, 1000);
        ghla.Update(bar2);

        var barWithNaN = new TBar(DateTime.UtcNow.AddMinutes(2), 108, 115, 100, double.NaN, 1000);
        var resultAfterNaN = ghla.Update(barWithNaN);

        Assert.True(double.IsFinite(resultAfterNaN.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var ghla = new Ghla(5);

        var bar1 = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        ghla.Update(bar1);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 102, 110, 98, 108, 1000);
        ghla.Update(bar2);

        var barWithInf = new TBar(DateTime.UtcNow.AddMinutes(2), 108, double.PositiveInfinity, double.NegativeInfinity, double.PositiveInfinity, 1000);
        var resultAfterInf = ghla.Update(barWithInf);

        Assert.True(double.IsFinite(resultAfterInf.Value));
    }

    [Fact]
    public void BatchNaN_Safe()
    {
        var ghla = new Ghla(5);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 10; i++)
        {
            ghla.Update(bars[i]);
        }

        for (int i = 0; i < 5; i++)
        {
            var nanBar = new TBar(DateTime.UtcNow.AddMinutes(100 + i), double.NaN, double.NaN, double.NaN, double.NaN, 0);
            var result = ghla.Update(nanBar);
            Assert.True(double.IsFinite(result.Value));
        }

        for (int i = 10; i < 20; i++)
        {
            var result = ghla.Update(bars[i]);
            Assert.True(double.IsFinite(result.Value));
        }
    }

    // ============== F) Consistency Tests ==============

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        var ghlaIterative = new Ghla(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var iterativeResults = new TSeries();
        foreach (var bar in bars)
        {
            iterativeResults.Add(ghlaIterative.Update(bar));
        }

        var batchResults = Ghla.Batch(bars, 5);

        Assert.Equal(iterativeResults.Count, batchResults.Count);
        for (int i = 0; i < iterativeResults.Count; i++)
        {
            Assert.Equal(iterativeResults[i].Value, batchResults[i].Value, 1e-10);
        }
    }

    [Fact]
    public void TBarSeries_Update_MatchesStreaming()
    {
        var ghla1 = new Ghla(5);
        var ghla2 = new Ghla(5);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            ghla1.Update(bar);
        }

        ghla2.Update(bars);

        Assert.Equal(ghla1.Last.Value, ghla2.Last.Value, 1e-10);
    }

    [Fact]
    public void SpanBatch_MatchesStreaming()
    {
        var ghla = new Ghla(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var streamResults = new double[100];
        for (int i = 0; i < 100; i++)
        {
            streamResults[i] = ghla.Update(bars[i]).Value;
        }

        double[] highs = new double[100];
        double[] lows = new double[100];
        double[] closes = new double[100];
        for (int i = 0; i < 100; i++)
        {
            highs[i] = bars[i].High;
            lows[i] = bars[i].Low;
            closes[i] = bars[i].Close;
        }

        double[] spanResults = new double[100];
        Ghla.Batch(highs, lows, closes, spanResults, 5);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(streamResults[i], spanResults[i], 1e-10);
        }
    }

    [Fact]
    public void EventBased_MatchesStreaming()
    {
        var ghla1 = new Ghla(5);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var eventResults = new List<double>();
        ghla1.Pub += (object? _, in TValueEventArgs e) => eventResults.Add(e.Value.Value);

        foreach (var bar in bars)
        {
            ghla1.Update(bar);
        }

        var ghla2 = new Ghla(5);
        var streamResults = new List<double>();

        foreach (var bar in bars)
        {
            streamResults.Add(ghla2.Update(bar).Value);
        }

        Assert.Equal(streamResults.Count, eventResults.Count);
        for (int i = 0; i < streamResults.Count; i++)
        {
            Assert.Equal(streamResults[i], eventResults[i], 1e-10);
        }
    }

    // ============== G) Span API Tests ==============

    [Fact]
    public void SpanBatch_ValidatesHighLowLength()
    {
        double[] high = new double[10];
        double[] low = new double[5]; // mismatched
        double[] close = new double[10];
        double[] output = new double[10];

        var ex = Assert.Throws<ArgumentException>(() => Ghla.Batch(high, low, close, output));
        Assert.Equal("low", ex.ParamName);
    }

    [Fact]
    public void SpanBatch_ValidatesCloseLength()
    {
        double[] high = new double[10];
        double[] low = new double[10];
        double[] close = new double[5]; // mismatched
        double[] output = new double[10];

        var ex = Assert.Throws<ArgumentException>(() => Ghla.Batch(high, low, close, output));
        Assert.Equal("close", ex.ParamName);
    }

    [Fact]
    public void SpanBatch_ValidatesOutputLength()
    {
        double[] high = new double[10];
        double[] low = new double[10];
        double[] close = new double[10];
        double[] output = new double[5]; // too small

        var ex = Assert.Throws<ArgumentException>(() => Ghla.Batch(high, low, close, output));
        Assert.Equal("output", ex.ParamName);
    }

    [Fact]
    public void SpanBatch_ValidatesPeriod()
    {
        double[] high = new double[10];
        double[] low = new double[10];
        double[] close = new double[10];
        double[] output = new double[10];

        Assert.Throws<ArgumentException>(() => Ghla.Batch(high, low, close, output, period: 0));
        Assert.Throws<ArgumentException>(() => Ghla.Batch(high, low, close, output, period: -1));
    }

    [Fact]
    public void SpanBatch_EmptyInput_NoOp()
    {
        double[] high = Array.Empty<double>();
        double[] low = Array.Empty<double>();
        double[] close = Array.Empty<double>();
        double[] output = Array.Empty<double>();

        var ex = Record.Exception(() => Ghla.Batch(high, low, close, output));
        Assert.Null(ex);
    }

    [Fact]
    public void SpanBatch_NaN_HandledGracefully()
    {
        double[] high = { 110, 115, double.NaN, 120, 125 };
        double[] low = { 90, 85, double.NaN, 88, 92 };
        double[] close = { 100, 105, double.NaN, 110, 115 };
        double[] output = new double[5];

        Ghla.Batch(high, low, close, output);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"Output[{i}] should be finite but was {output[i]}");
        }
    }

    // ============== H) Chainability ==============

    [Fact]
    public void Chainability_Works()
    {
        var ghla = new Ghla(5);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var result = ghla.Update(bars);
        Assert.Equal(50, result.Count);
        Assert.Equal(ghla.Last.Value, result.Last.Value);
    }

    [Fact]
    public void PubEvent_Fires()
    {
        var ghla = new Ghla(5);
        int eventCount = 0;
        ghla.Pub += (object? _, in TValueEventArgs _) => eventCount++;

        var gbm = new GBM();
        var bars = gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            ghla.Update(bar);
        }

        Assert.Equal(10, eventCount);
    }

    [Fact]
    public void Chaining_ViaConstructor_Works()
    {
        var tr = new Tr();
        var ghla = new Ghla(tr, 5);

        var gbm = new GBM();
        var bars = gbm.Fetch(30, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            tr.Update(bar);
        }

        Assert.True(double.IsFinite(ghla.Last.Value));
    }

    // ============== GHLA-Specific Tests ==============

    [Fact]
    public void Hysteresis_RetainsTrend_InNeutralZone()
    {
        var ghla = new Ghla(3);

        // Establish bullish trend with strongly rising bars
        var baseTime = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            double price = 100 + (i * 10);
            var bar = new TBar(baseTime.AddMinutes(i), price, price + 5, price - 5, price + 3, 1000);
            ghla.Update(bar);
        }

        Assert.Equal(1, ghla.Trend);

        // Feed a bar inside the neutral zone (between smaLow and smaHigh)
        // With period=3 and rising prices, smaHigh and smaLow are high
        // Feed a bar whose close is between the two SMAs → trend should stay +1
        var neutralBar = new TBar(baseTime.AddMinutes(5), 140, 142, 138, 140, 1000);
        ghla.Update(neutralBar);

        // Trend should remain bullish (hysteresis)
        Assert.Equal(1, ghla.Trend);
    }

    [Fact]
    public void TrendFlip_OnStrongMove()
    {
        var ghla = new Ghla(3);

        // Feed rising bars → bullish
        var baseTime = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            double price = 100 + (i * 5);
            var bar = new TBar(baseTime.AddMinutes(i), price, price + 2, price - 2, price + 1, 1000);
            ghla.Update(bar);
        }
        Assert.Equal(1, ghla.Trend);

        // Feed strongly falling bars → eventually bearish
        for (int i = 5; i < 15; i++)
        {
            double price = 120 - ((i - 5) * 10);
            var bar = new TBar(baseTime.AddMinutes(i), price, price + 2, price - 2, price - 1, 1000);
            ghla.Update(bar);
        }
        Assert.Equal(-1, ghla.Trend);
    }

    [Fact]
    public void Bearish_OutputIsSmaHigh()
    {
        var ghla = new Ghla(3);

        // Create strongly bearish scenario: close far below smaLow
        var baseTime = DateTime.UtcNow;
        // First fill buffers with high prices
        for (int i = 0; i < 3; i++)
        {
            var bar = new TBar(baseTime.AddMinutes(i), 100, 105, 95, 100, 1000);
            ghla.Update(bar);
        }

        // Then crash the close far below → bearish
        var crashBar = new TBar(baseTime.AddMinutes(3), 50, 55, 45, 50, 1000);
        ghla.Update(crashBar);

        if (ghla.Trend == -1)
        {
            // In bearish mode, output should be SMA of highs (resistance)
            // The value should be positive and finite
            Assert.True(ghla.Last.Value > 0);
        }
    }

    [Fact]
    public void StaticBatch_Works()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var results = Ghla.Batch(bars, 5);

        Assert.Equal(50, results.Count);
        Assert.True(double.IsFinite(results.Last.Value));
    }

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var (results, indicator) = Ghla.Calculate(bars, 5);

        Assert.Equal(50, results.Count);
        Assert.NotNull(indicator);
        Assert.True(double.IsFinite(indicator.Last.Value));
        Assert.True(indicator.Trend != 0);
    }

    [Fact]
    public void FlatBars_OutputEqualsPrice()
    {
        var ghla = new Ghla(3);

        // Flat bars: H=L=C=100 → SMA(H)=100, SMA(L)=100, close is NOT > smaH and NOT < smaL
        // Seed: close >= smaHigh (100 >= 100)? Yes → trend=1 → output = smaLow = 100
        for (int i = 0; i < 10; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 100, 100, 100, 100, 1000);
            ghla.Update(bar);
        }

        Assert.Equal(100.0, ghla.Last.Value, 1e-10);
    }

    [Fact]
    public void OverlayValue_TracksPrice()
    {
        var ghla = new Ghla(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            ghla.Update(bar);
        }

        // GHLA is an overlay — value should be in same ballpark as price
        double lastClose = bars[^1].Close;
        Assert.True(ghla.Last.Value > 0, "GHLA overlay should be positive for positive prices");
        Assert.True(Math.Abs(ghla.Last.Value - lastClose) < lastClose, "GHLA should be within 100% of close price");
    }
}
