namespace QuanTAlib;

public class PmaTests
{
    // === A) Constructor validation ===

    [Fact]
    public void Constructor_InvalidPeriod_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Pma(0));
        Assert.Throws<ArgumentException>(() => new Pma(-1));
    }

    [Fact]
    public void Constructor_ValidPeriod_SetsProperties()
    {
        var pma = new Pma(7);
        Assert.Equal("Pma(7)", pma.Name);
        Assert.Equal(13, pma.WarmupPeriod); // (7*2)-1
        Assert.False(pma.IsHot);
    }

    // === B) Basic calculation ===

    [Fact]
    public void Update_ValidInput_CalculatesCorrectly()
    {
        // PMA(3) of [1, 2, 3, 4, 5]
        // WMA(3): [1, 1.666, 2.333, 3.333, 4.333]
        // WMA(WMA(3)): [1, 1.444, 1.888, 2.629, 3.518]
        // PMA = 2*WMA1 - WMA2
        // [1]: 2*1 - 1 = 1
        // [2]: 2*1.666 - 1.444 = 1.888
        // [3]: 2*2.333 - 1.888 = 2.777

        var pma = new Pma(3);

        var v1 = pma.Update(new TValue(DateTime.UtcNow, 1)).Value;
        var v2 = pma.Update(new TValue(DateTime.UtcNow, 2)).Value;
        var v3 = pma.Update(new TValue(DateTime.UtcNow, 3)).Value;

        Assert.Equal(1.0, v1, 6);
        Assert.Equal(1.888888, v2, 5);
        Assert.Equal(2.777777, v3, 5);
    }

    [Fact]
    public void Update_ReturnsCorrectTrigger()
    {
        // Trigger = (4*WMA1 - WMA2) / 3
        // [1]: (4*1 - 1) / 3 = 1.0
        // [2]: (4*1.666 - 1.444) / 3 = 5.222/3 = 1.740
        // [3]: (4*2.333 - 1.888) / 3 = 7.444/3 = 2.481

        var pma = new Pma(3);

        pma.Update(new TValue(DateTime.UtcNow, 1));
        double t1 = pma.Trigger.Value;

        pma.Update(new TValue(DateTime.UtcNow, 2));
        double t2 = pma.Trigger.Value;

        pma.Update(new TValue(DateTime.UtcNow, 3));
        double t3 = pma.Trigger.Value;

        Assert.Equal(1.0, t1, 6);
        Assert.Equal(1.740740, t2, 4);
        Assert.Equal(2.481481, t3, 4);
    }

    [Fact]
    public void Update_LastAndTriggerHaveTimestamps()
    {
        var pma = new Pma(3);
        var time = DateTime.UtcNow;
        pma.Update(new TValue(time, 100));

        Assert.Equal(time.Ticks, pma.Last.Time);
        Assert.Equal(time.Ticks, pma.Trigger.Time);
    }

    // === C) State + bar correction ===

    [Fact]
    public void Update_IsNewFalse_CorrectsValue()
    {
        var pma = new Pma(3);

        pma.Update(new TValue(DateTime.UtcNow, 1));
        pma.Update(new TValue(DateTime.UtcNow, 2));

        var v3 = pma.Update(new TValue(DateTime.UtcNow, 3), isNew: true).Value;
        var v3_corrected = pma.Update(new TValue(DateTime.UtcNow, 4), isNew: false).Value;

        // Sequence [1, 2, 4]:
        // WMA(3): [1, 1.666, 2.833]
        // WMA(WMA(3)): [1, 1.444, 2.138]
        // PMA: 2*2.833 - 2.138 = 3.527

        Assert.Equal(2.777777, v3, 5);
        Assert.Equal(3.527777, v3_corrected, 5);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var pma = new Pma(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            pma.Update(tenthInput, isNew: true);
        }

        double valueAfterTen = pma.Last.Value;
        double triggerAfterTen = pma.Trigger.Value;

        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            pma.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        TValue finalValue = pma.Update(tenthInput, isNew: false);

        Assert.Equal(valueAfterTen, finalValue.Value, 1e-9);
        Assert.Equal(triggerAfterTen, pma.Trigger.Value, 1e-9);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var pma = new Pma(3);
        pma.Update(new TValue(DateTime.UtcNow, 100));
        pma.Update(new TValue(DateTime.UtcNow, 110));

        pma.Reset();

        Assert.False(pma.IsHot);
        var v1 = pma.Update(new TValue(DateTime.UtcNow, 1)).Value;
        Assert.Equal(1.0, v1);
    }

    // === D) Warmup/convergence ===

    [Fact]
    public void WarmupPeriod_AndIsHot_Agree()
    {
        int period = 5;
        var pma = new Pma(period);
        Assert.Equal(9, pma.WarmupPeriod); // (5*2)-1

        for (int i = 0; i < 8; i++)
        {
            pma.Update(new TValue(DateTime.UtcNow, i + 1));
            Assert.False(pma.IsHot, $"Should not be hot after {i + 1} samples");
        }

        pma.Update(new TValue(DateTime.UtcNow, 9));
        Assert.True(pma.IsHot, "Should be hot after 9 samples (WarmupPeriod)");
    }

    // === E) Robustness ===

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var pma = new Pma(5);
        pma.Update(new TValue(DateTime.UtcNow, 100));
        pma.Update(new TValue(DateTime.UtcNow, 110));

        var resultAfterNaN = pma.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(resultAfterNaN.Value));
        Assert.True(double.IsFinite(pma.Trigger.Value));
        Assert.NotEqual(0, resultAfterNaN.Value);
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var pma = new Pma(5);
        pma.Update(new TValue(DateTime.UtcNow, 100));
        pma.Update(new TValue(DateTime.UtcNow, 110));

        var result = pma.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));

        Assert.True(double.IsFinite(result.Value));
        Assert.True(double.IsFinite(pma.Trigger.Value));
    }

    // === F) Consistency — Batch == Streaming == Span == Eventing ===

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        int period = 7;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch Mode
        var batchSeries = Pma.Batch(series, period);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var tValues = series.Values.ToArray();
        var spanOutput = new double[tValues.Length];
        Pma.Batch(new ReadOnlySpan<double>(tValues), spanOutput, period);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Pma(period);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Pma(pubSource, period);
        for (int i = 0; i < series.Count; i++)
        {
            pubSource.Add(series[i]);
        }
        double eventingResult = eventingInd.Last.Value;

        Assert.Equal(expected, spanResult, precision: 9);
        Assert.Equal(expected, streamingResult, precision: 9);
        Assert.Equal(expected, eventingResult, precision: 9);
    }

    // === G) Span API tests ===

    [Fact]
    public void SpanCalc_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] output = new double[5];
        double[] wrongSizeOutput = new double[3];

        Assert.Throws<ArgumentException>(() => Pma.Batch(source.AsSpan(), output.AsSpan(), 0));
        Assert.Throws<ArgumentException>(() => Pma.Batch(source.AsSpan(), wrongSizeOutput.AsSpan(), 3));
    }

    [Fact]
    public void SpanCalc_HandlesNaN()
    {
        double[] source = [100, 110, double.NaN, 120, 130];
        double[] output = new double[5];

        Pma.Batch(source.AsSpan(), output.AsSpan(), 3);

        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val));
        }
    }

    [Fact]
    public void SpanCalc_DualOutput_ValidatesInput()
    {
        double[] source = [1, 2, 3, 4, 5];
        double[] pmaOut = new double[5];
        double[] trigOut = new double[5];
        double[] wrongSize = new double[3];

        Assert.Throws<ArgumentException>(() => Pma.Batch(source.AsSpan(), wrongSize.AsSpan(), trigOut.AsSpan(), 3));
        Assert.Throws<ArgumentException>(() => Pma.Batch(source.AsSpan(), pmaOut.AsSpan(), wrongSize.AsSpan(), 3));
        Assert.Throws<ArgumentException>(() => Pma.Batch(source.AsSpan(), pmaOut.AsSpan(), trigOut.AsSpan(), 0));
    }

    [Fact]
    public void SpanCalc_DualOutput_MatchesStreaming()
    {
        int period = 5;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 77);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        double[] src = series.Values.ToArray();
        double[] pmaOut = new double[src.Length];
        double[] trigOut = new double[src.Length];

        Pma.Batch(src.AsSpan(), pmaOut.AsSpan(), trigOut.AsSpan(), period);

        var streaming = new Pma(period);
        for (int i = 0; i < src.Length; i++)
        {
            streaming.Update(series[i]);
        }

        Assert.Equal(streaming.Last.Value, pmaOut[^1], 1e-9);
        Assert.Equal(streaming.Trigger.Value, trigOut[^1], 1e-9);
    }

    [Fact]
    public void SpanCalc_LargeData_DoesNotStackOverflow()
    {
        int len = 5000;
        double[] source = new double[len];
        double[] output = new double[len];

        for (int i = 0; i < len; i++)
        {
            source[i] = 100 + (i % 50);
        }

        Pma.Batch(source.AsSpan(), output.AsSpan(), 14);

        Assert.True(double.IsFinite(output[^1]));
    }

    // === H) Chainability ===

    [Fact]
    public void Pub_Fires_OnUpdate()
    {
        var pma = new Pma(3);
        int fireCount = 0;
        pma.Pub += (object? sender, in TValueEventArgs args) => fireCount++;

        pma.Update(new TValue(DateTime.UtcNow, 100));
        pma.Update(new TValue(DateTime.UtcNow, 110));

        Assert.Equal(2, fireCount);
    }

    [Fact]
    public void EventBased_Chaining_Works()
    {
        var source = new TSeries();
        var pma = new Pma(source, 5);

        source.Add(new TValue(DateTime.UtcNow, 100));
        source.Add(new TValue(DateTime.UtcNow, 110));
        source.Add(new TValue(DateTime.UtcNow, 120));

        Assert.True(double.IsFinite(pma.Last.Value));
        Assert.True(double.IsFinite(pma.Trigger.Value));
    }

    [Fact]
    public void StaticCalculate_MatchesInstance()
    {
        const int period = 10;
        int count = 100;
        var source = new TSeries();
        var pma = new Pma(period);

        for (int i = 0; i < count; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddMinutes(i), i));
            pma.Update(source.Last);
        }

        var staticResult = Pma.Batch(source, period);

        Assert.Equal(source.Count, staticResult.Count);
        Assert.Equal(pma.Last.Value, staticResult.Last.Value, 8);
    }
}
