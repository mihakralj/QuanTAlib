namespace QuanTAlib.Tests;

public class EthermTests
{
    // ============== A) Constructor & Parameter Validation ==============

    [Fact]
    public void Constructor_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Etherm(0));
        Assert.Throws<ArgumentException>(() => new Etherm(-1));
        Assert.Throws<ArgumentException>(() => new Etherm(-100));

        var etherm = new Etherm(22);
        Assert.NotNull(etherm);
    }

    [Fact]
    public void Constructor_DefaultPeriod_Is22()
    {
        var etherm = new Etherm();
        Assert.Contains("22", etherm.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_Period1_Works()
    {
        var etherm = new Etherm(1);
        Assert.NotNull(etherm);
        Assert.Contains("1", etherm.Name, StringComparison.Ordinal);
    }

    // ============== B) Basic Calculation ==============

    [Fact]
    public void BasicCalculation_DoesNotCrash()
    {
        var etherm = new Etherm(22);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            etherm.Update(bar);
        }

        Assert.True(double.IsFinite(etherm.Last.Value));
    }

    [Fact]
    public void Calc_ReturnsValue()
    {
        var etherm = new Etherm(22);
        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);

        Assert.Equal(0, etherm.Last.Value);

        TValue result = etherm.Update(bar);

        // First bar temperature = 0 (no previous bar)
        Assert.Equal(0.0, result.Value, 1e-10);
        Assert.Equal(result.Value, etherm.Last.Value);
    }

    [Fact]
    public void FirstValue_ReturnsZero()
    {
        var etherm = new Etherm(22);
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000);

        TValue result = etherm.Update(bar);

        // First bar: no previous bar to compare, temperature = 0
        Assert.Equal(0.0, result.Value, 1e-10);
    }

    [Fact]
    public void SecondBar_ReturnsRangeExtension()
    {
        var etherm = new Etherm(22);

        var bar1 = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000);
        etherm.Update(bar1);

        // Bar2: H=115, L=85 → highDiff=|115-110|=5, lowDiff=|90-85|=5
        // NOT inside bar (115 > 110), temp = max(5, 5) = 5
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 100, 115, 85, 105, 1000);
        TValue result = etherm.Update(bar2);

        Assert.Equal(5.0, result.Value, 1e-10);
    }

    [Fact]
    public void InsideBar_ReturnsZero()
    {
        var etherm = new Etherm(22);

        // Bar1: H=110, L=90
        var bar1 = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000);
        etherm.Update(bar1);

        // Bar2: H=105, L=95 → inside bar (105 < 110 AND 95 > 90) → temp = 0
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 100, 105, 95, 102, 1000);
        TValue result = etherm.Update(bar2);

        Assert.Equal(0.0, result.Value, 1e-10);
    }

    [Fact]
    public void Properties_Accessible()
    {
        var etherm = new Etherm(22);

        Assert.Equal(0, etherm.Last.Value);
        Assert.False(etherm.IsHot);
        Assert.Contains("Etherm", etherm.Name, StringComparison.Ordinal);
        Assert.True(etherm.WarmupPeriod > 0);

        var bar = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        etherm.Update(bar);

        // After first bar, signal EMA should have a value
        Assert.True(double.IsFinite(etherm.Signal));
    }

    // ============== C) State Management & Bar Correction ==============

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var etherm = new Etherm(22);

        var bar1 = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        etherm.Update(bar1, isNew: true);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 102, 110, 100, 108, 1000);
        etherm.Update(bar2, isNew: true);

        // Second bar should produce a range extension value
        Assert.True(etherm.Last.Value >= 0);
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var etherm = new Etherm(22);

        var bar1 = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000);
        etherm.Update(bar1, isNew: true);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 105, 115, 85, 108, 1000);
        etherm.Update(bar2, isNew: true);
        double beforeUpdate = etherm.Last.Value;

        // Modify bar2 with wider range
        var bar2Modified = new TBar(DateTime.UtcNow.AddMinutes(1), 105, 130, 70, 108, 1000);
        etherm.Update(bar2Modified, isNew: false);
        double afterUpdate = etherm.Last.Value;

        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void IsNew_Consistency()
    {
        var etherm = new Etherm(22);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed first 99
        for (int i = 0; i < 99; i++)
        {
            etherm.Update(bars[i]);
        }

        // Update with 100th bar (isNew=true)
        etherm.Update(bars[99], true);

        // Update with modified 100th bar (isNew=false)
        var modifiedBar = new TBar(bars[99].Time, bars[99].Open, bars[99].High + 10.0, bars[99].Low - 10.0, bars[99].Close, bars[99].Volume);
        double val2 = etherm.Update(modifiedBar, false).Value;

        // Create new instance and feed up to modified
        var etherm2 = new Etherm(22);
        for (int i = 0; i < 99; i++)
        {
            etherm2.Update(bars[i]);
        }
        double val3 = etherm2.Update(modifiedBar, true).Value;

        Assert.Equal(val3, val2, 1e-9);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var etherm = new Etherm(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);
        var bars = gbm.Fetch(20, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed 10 new values
        TBar tenthBar = default;
        for (int i = 0; i < 10; i++)
        {
            tenthBar = bars[i];
            etherm.Update(tenthBar, isNew: true);
        }

        // Remember state after 10 values
        double stateAfterTen = etherm.Last.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 10; i < 19; i++)
        {
            etherm.Update(bars[i], isNew: false);
        }

        // Feed the remembered 10th bar again with isNew=false
        TValue finalResult = etherm.Update(tenthBar, isNew: false);

        // State should match the original state after 10 values
        Assert.Equal(stateAfterTen, finalResult.Value, 1e-10);
    }

    [Fact]
    public void Reset_Works()
    {
        var etherm = new Etherm(22);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            etherm.Update(bar);
        }

        Assert.True(etherm.Signal != 0 || etherm.Last.Value >= 0);

        etherm.Reset();
        Assert.Equal(0, etherm.Last.Value);
        Assert.False(etherm.IsHot);
        Assert.Equal(0, etherm.Signal);

        // After reset, should accept new values
        etherm.Update(bars[0]);
        Assert.True(double.IsFinite(etherm.Last.Value));
    }

    // ============== D) Warmup & Convergence ==============

    [Fact]
    public void IsHot_BecomesTrueAfterWarmup()
    {
        var etherm = new Etherm(5);

        Assert.False(etherm.IsHot);

        int steps = 0;
        var baseTime = DateTime.UtcNow;
        while (!etherm.IsHot && steps < 100)
        {
            var bar = new TBar(baseTime.AddMinutes(steps), 100 + steps, 110 + steps, 90 + steps, 100 + steps, 1000);
            etherm.Update(bar);
            steps++;
        }

        Assert.True(etherm.IsHot);
        Assert.True(steps > 0);
    }

    [Fact]
    public void WarmupPeriod_IsPositive()
    {
        var etherm = new Etherm(22);
        Assert.True(etherm.WarmupPeriod > 0);

        var etherm2 = new Etherm(50);
        Assert.True(etherm2.WarmupPeriod > 0);
    }

    // ============== E) NaN/Infinity Handling ==============

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var etherm = new Etherm(5);

        var bar1 = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        etherm.Update(bar1);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 102, 110, 98, 108, 1000);
        etherm.Update(bar2);

        // Feed bar with NaN high
        var barWithNaN = new TBar(DateTime.UtcNow.AddMinutes(2), double.NaN, double.NaN, 100, 112, 1000);
        var resultAfterNaN = etherm.Update(barWithNaN);

        Assert.True(double.IsFinite(resultAfterNaN.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var etherm = new Etherm(5);

        var bar1 = new TBar(DateTime.UtcNow, 100, 105, 95, 102, 1000);
        etherm.Update(bar1);

        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 102, 110, 98, 108, 1000);
        etherm.Update(bar2);

        // Feed bar with Infinity
        var barWithInf = new TBar(DateTime.UtcNow.AddMinutes(2), 108, double.PositiveInfinity, 100, 112, 1000);
        var resultAfterInf = etherm.Update(barWithInf);

        Assert.True(double.IsFinite(resultAfterInf.Value));
    }

    [Fact]
    public void BatchNaN_Safe()
    {
        var etherm = new Etherm(5);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Feed some normal bars first
        for (int i = 0; i < 10; i++)
        {
            etherm.Update(bars[i]);
        }

        // Feed several NaN bars
        for (int i = 0; i < 5; i++)
        {
            var nanBar = new TBar(DateTime.UtcNow.AddMinutes(100 + i), double.NaN, double.NaN, double.NaN, double.NaN, 0);
            var result = etherm.Update(nanBar);
            Assert.True(double.IsFinite(result.Value));
        }

        // Resume normal bars
        for (int i = 10; i < 20; i++)
        {
            var result = etherm.Update(bars[i]);
            Assert.True(double.IsFinite(result.Value));
        }
    }

    // ============== F) Consistency Tests ==============

    [Fact]
    public void BatchCalc_MatchesIterativeCalc()
    {
        var ethermIterative = new Etherm(14);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Calculate iteratively
        var iterativeResults = new TSeries();
        foreach (var bar in bars)
        {
            iterativeResults.Add(ethermIterative.Update(bar));
        }

        // Calculate batch
        var batchResults = Etherm.Batch(bars, 14);

        // Compare
        Assert.Equal(iterativeResults.Count, batchResults.Count);
        for (int i = 0; i < iterativeResults.Count; i++)
        {
            Assert.Equal(iterativeResults[i].Value, batchResults[i].Value, 1e-10);
        }
    }

    [Fact]
    public void TBarSeries_Update_MatchesStreaming()
    {
        var etherm1 = new Etherm(14);
        var etherm2 = new Etherm(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Streaming
        foreach (var bar in bars)
        {
            etherm1.Update(bar);
        }

        // Batch
        etherm2.Update(bars);

        Assert.Equal(etherm1.Last.Value, etherm2.Last.Value, 1e-10);
    }

    [Fact]
    public void SpanBatch_MatchesStreaming()
    {
        var etherm = new Etherm(14);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);
        var bars = gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Streaming
        var streamResults = new double[100];
        for (int i = 0; i < 100; i++)
        {
            streamResults[i] = etherm.Update(bars[i]).Value;
        }

        // Span batch
        double[] highs = new double[100];
        double[] lows = new double[100];
        for (int i = 0; i < 100; i++)
        {
            highs[i] = bars[i].High;
            lows[i] = bars[i].Low;
        }

        double[] spanResults = new double[100];
        Etherm.Batch(highs, lows, spanResults, 14);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(streamResults[i], spanResults[i], 1e-10);
        }
    }

    [Fact]
    public void EventBased_MatchesStreaming()
    {
        var etherm1 = new Etherm(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // Collect event-based results
        var eventResults = new List<double>();
        etherm1.Pub += (object? _, in TValueEventArgs e) => eventResults.Add(e.Value.Value);

        foreach (var bar in bars)
        {
            etherm1.Update(bar);
        }

        // Collect streaming results
        var etherm2 = new Etherm(14);
        var streamResults = new List<double>();

        foreach (var bar in bars)
        {
            streamResults.Add(etherm2.Update(bar).Value);
        }

        Assert.Equal(streamResults.Count, eventResults.Count);
        for (int i = 0; i < streamResults.Count; i++)
        {
            Assert.Equal(streamResults[i], eventResults[i], 1e-10);
        }
    }

    // ============== G) Span API Tests ==============

    [Fact]
    public void SpanBatch_ValidatesLengths()
    {
        double[] high = new double[10];
        double[] low = new double[5]; // mismatched
        double[] output = new double[10];

        Assert.Throws<ArgumentException>(() => Etherm.Batch(high, low, output));
    }

    [Fact]
    public void SpanBatch_ValidatesOutputLength()
    {
        double[] high = new double[10];
        double[] low = new double[10];
        double[] output = new double[5]; // too small

        Assert.Throws<ArgumentException>(() => Etherm.Batch(high, low, output));
    }

    [Fact]
    public void SpanBatch_ValidatesPeriod()
    {
        double[] high = new double[10];
        double[] low = new double[10];
        double[] output = new double[10];

        Assert.Throws<ArgumentException>(() => Etherm.Batch(high, low, output, period: 0));
        Assert.Throws<ArgumentException>(() => Etherm.Batch(high, low, output, period: -1));
    }

    [Fact]
    public void SpanBatch_EmptyInput_NoOp()
    {
        double[] high = Array.Empty<double>();
        double[] low = Array.Empty<double>();
        double[] output = Array.Empty<double>();

        // Should not throw
        var ex = Record.Exception(() => Etherm.Batch(high, low, output));
        Assert.Null(ex);
    }

    [Fact]
    public void SpanBatch_NaN_HandledGracefully()
    {
        double[] high = { 100, 110, double.NaN, 115, 120 };
        double[] low = { 90, 85, double.NaN, 88, 92 };
        double[] output = new double[5];

        Etherm.Batch(high, low, output);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"Output[{i}] should be finite but was {output[i]}");
        }
    }

    // ============== H) Chainability ==============

    [Fact]
    public void Chainability_Works()
    {
        var etherm = new Etherm(14);
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var result = etherm.Update(bars);
        Assert.Equal(50, result.Count);
        Assert.Equal(etherm.Last.Value, result.Last.Value);
    }

    [Fact]
    public void PubEvent_Fires()
    {
        var etherm = new Etherm(14);
        int eventCount = 0;
        etherm.Pub += (object? _, in TValueEventArgs _) => eventCount++;

        var gbm = new GBM();
        var bars = gbm.Fetch(10, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            etherm.Update(bar);
        }

        Assert.Equal(10, eventCount);
    }

    [Fact]
    public void Chaining_ViaConstructor_Works()
    {
        // Create a source indicator (e.g., TR)
        var tr = new Tr();
        // Subscribe Etherm to TR's events (TValue-based chain)
        var etherm = new Etherm(tr, 14);

        var gbm = new GBM();
        var bars = gbm.Fetch(30, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        // When TR updates, the chained etherm should also update
        foreach (var bar in bars)
        {
            tr.Update(bar);
        }

        Assert.True(double.IsFinite(etherm.Last.Value));
    }

    // ============== ETHERM-Specific Tests ==============

    [Fact]
    public void Signal_IsEmaOfTemperature()
    {
        var etherm = new Etherm(5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.5);
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        foreach (var bar in bars)
        {
            etherm.Update(bar);
        }

        // Signal should be finite and non-negative after warmup
        Assert.True(double.IsFinite(etherm.Signal));
        Assert.True(etherm.Signal >= 0);
    }

    [Fact]
    public void FlatBars_ZeroTemperature()
    {
        var etherm = new Etherm(5);

        for (int i = 0; i < 10; i++)
        {
            var bar = new TBar(DateTime.UtcNow.AddMinutes(i), 100, 100, 100, 100, 1000);
            etherm.Update(bar);
        }

        // Flat bars: no range extension, temperature = 0
        Assert.Equal(0.0, etherm.Last.Value, 1e-10);
    }

    [Fact]
    public void HighDiff_DominatesWhenLarger()
    {
        var etherm = new Etherm(22);

        // Bar1: H=100, L=90
        var bar1 = new TBar(DateTime.UtcNow, 95, 100, 90, 95, 1000);
        etherm.Update(bar1);

        // Bar2: H=120, L=89 → highDiff=|120-100|=20, lowDiff=|90-89|=1
        // Not inside bar (120 > 100), temp = max(20, 1) = 20
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 105, 120, 89, 110, 1000);
        TValue result = etherm.Update(bar2);

        Assert.Equal(20.0, result.Value, 1e-10);
    }

    [Fact]
    public void LowDiff_DominatesWhenLarger()
    {
        var etherm = new Etherm(22);

        // Bar1: H=100, L=90
        var bar1 = new TBar(DateTime.UtcNow, 95, 100, 90, 95, 1000);
        etherm.Update(bar1);

        // Bar2: H=101, L=70 → highDiff=|101-100|=1, lowDiff=|90-70|=20
        // Not inside bar (101 > 100), temp = max(1, 20) = 20
        var bar2 = new TBar(DateTime.UtcNow.AddMinutes(1), 85, 101, 70, 80, 1000);
        TValue result = etherm.Update(bar2);

        Assert.Equal(20.0, result.Value, 1e-10);
    }

    [Fact]
    public void StaticBatch_Works()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var results = Etherm.Batch(bars, 14);

        Assert.Equal(50, results.Count);
        Assert.True(double.IsFinite(results.Last.Value));
    }

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var gbm = new GBM();
        var bars = gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        var (results, indicator) = Etherm.Calculate(bars, 14);

        Assert.Equal(50, results.Count);
        Assert.NotNull(indicator);
        Assert.True(double.IsFinite(indicator.Last.Value));
        Assert.True(double.IsFinite(indicator.Signal));
    }

    [Fact]
    public void SingleBar_ReturnsZero()
    {
        var etherm = new Etherm(14);
        var bar = new TBar(DateTime.UtcNow, 100, 110, 90, 105, 1000);

        var result = etherm.Update(bar);

        // First bar temperature = 0
        Assert.Equal(0.0, result.Value, 1e-10);
    }
}
