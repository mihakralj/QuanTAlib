namespace QuanTAlib;

public class NyqmaTests
{
    // === A) Constructor validation ===

    [Fact]
    public void Constructor_InvalidPeriod_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Nyqma(0));
        Assert.Throws<ArgumentException>(() => new Nyqma(2));
        Assert.Throws<ArgumentException>(() => new Nyqma(-5));
    }

    [Fact]
    public void Constructor_ValidPeriod_SetsProperties()
    {
        var nyqma = new Nyqma(89, 21);
        Assert.Equal("Nyqma(89,21)", nyqma.Name);
        Assert.Equal(89 + 21 - 1, nyqma.WarmupPeriod); // period + nyquistPeriod - 1
        Assert.False(nyqma.IsHot);
    }

    [Fact]
    public void Constructor_NyquistPeriodClamped()
    {
        // nyquistPeriod > period/2 should be clamped
        var nyqma = new Nyqma(10, 100);
        Assert.Equal("Nyqma(10,5)", nyqma.Name); // clamped to floor(10/2) = 5
    }

    [Fact]
    public void Constructor_NyquistPeriodClampedToMinOne()
    {
        var nyqma = new Nyqma(10, 0);
        Assert.Equal("Nyqma(10,1)", nyqma.Name); // clamped to minimum 1
    }

    [Fact]
    public void Constructor_DefaultParameters()
    {
        var nyqma = new Nyqma();
        Assert.Equal("Nyqma(89,21)", nyqma.Name);
    }

    // === B) Basic calculation ===

    [Fact]
    public void Update_ValidInput_CalculatesCorrectly()
    {
        // NYQMA(5, 2): period=5, nyquistPeriod=2
        // alpha = 2/(5-2) = 2/3
        // WMA(5) of [1,2,3,4,5]:
        //   [1]: 1
        //   [2]: (1+4)/3 = 5/3 = 1.666...
        //   [3]: (1+4+9)/6 = 14/6 = 2.333...
        //   [4]: (1+4+9+16)/10 = 30/10 = 3.0     (not full window yet, 4 bars)
        //   [5]: (1+4+9+16+25)/15 = 55/15 = 3.666...

        var nyqma = new Nyqma(5, 2);

        var v1 = nyqma.Update(new TValue(DateTime.UtcNow, 1)).Value;
        var v2 = nyqma.Update(new TValue(DateTime.UtcNow, 2)).Value;
        var v3 = nyqma.Update(new TValue(DateTime.UtcNow, 3)).Value;

        // First value: WMA1=1, WMA2(WMA1)=1 → (1+2/3)*1 - 2/3*1 = 1
        Assert.Equal(1.0, v1, 6);
        Assert.True(double.IsFinite(v2));
        Assert.True(double.IsFinite(v3));
    }

    [Fact]
    public void Update_ConstantInput_ConvergesToConstant()
    {
        var nyqma = new Nyqma(10, 5);
        double constant = 42.0;

        double last = 0;
        for (int i = 0; i < 100; i++)
        {
            last = nyqma.Update(new TValue(DateTime.UtcNow, constant)).Value;
        }

        Assert.Equal(constant, last, 1e-9);
    }

    [Fact]
    public void Update_ReturnsCorrectTime()
    {
        var nyqma = new Nyqma(5, 2);
        var time = DateTime.UtcNow;
        nyqma.Update(new TValue(time, 100));

        Assert.Equal(time.Ticks, nyqma.Last.Time);
    }

    // === C) State + bar correction ===

    [Fact]
    public void Update_IsNewFalse_CorrectsValue()
    {
        var nyqma = new Nyqma(5, 2);

        nyqma.Update(new TValue(DateTime.UtcNow, 1));
        nyqma.Update(new TValue(DateTime.UtcNow, 2));

        var v3 = nyqma.Update(new TValue(DateTime.UtcNow, 3), isNew: true).Value;
        var v3_corrected = nyqma.Update(new TValue(DateTime.UtcNow, 4), isNew: false).Value;

        // After correction with 4 instead of 3, the sequence is [1, 2, 4]
        Assert.NotEqual(v3, v3_corrected);
        Assert.True(double.IsFinite(v3_corrected));
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var nyqma = new Nyqma(10, 5);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            nyqma.Update(tenthInput, isNew: true);
        }

        double valueAfterTen = nyqma.Last.Value;

        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            nyqma.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        TValue finalValue = nyqma.Update(tenthInput, isNew: false);

        Assert.Equal(valueAfterTen, finalValue.Value, 1e-9);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var nyqma = new Nyqma(5, 2);
        nyqma.Update(new TValue(DateTime.UtcNow, 100));
        nyqma.Update(new TValue(DateTime.UtcNow, 110));

        nyqma.Reset();

        Assert.False(nyqma.IsHot);
        var v1 = nyqma.Update(new TValue(DateTime.UtcNow, 1)).Value;
        Assert.Equal(1.0, v1, 1e-9);
    }

    // === D) Warmup/convergence ===

    [Fact]
    public void WarmupPeriod_AndIsHot_Agree()
    {
        int period = 5;
        int nyquistPeriod = 2;
        var nyqma = new Nyqma(period, nyquistPeriod);
        Assert.Equal(6, nyqma.WarmupPeriod); // 5 + 2 - 1 = 6

        for (int i = 0; i < 5; i++)
        {
            nyqma.Update(new TValue(DateTime.UtcNow, i + 1));
            Assert.False(nyqma.IsHot, $"Should not be hot after {i + 1} samples");
        }

        nyqma.Update(new TValue(DateTime.UtcNow, 6));
        Assert.True(nyqma.IsHot, "Should be hot after 6 samples (WarmupPeriod)");
    }

    // === E) Robustness ===

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var nyqma = new Nyqma(5, 2);
        nyqma.Update(new TValue(DateTime.UtcNow, 100));
        nyqma.Update(new TValue(DateTime.UtcNow, 110));

        var resultAfterNaN = nyqma.Update(new TValue(DateTime.UtcNow, double.NaN));

        Assert.True(double.IsFinite(resultAfterNaN.Value));
        Assert.NotEqual(0, resultAfterNaN.Value);
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var nyqma = new Nyqma(5, 2);
        nyqma.Update(new TValue(DateTime.UtcNow, 100));
        nyqma.Update(new TValue(DateTime.UtcNow, 110));

        var result = nyqma.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));

        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void BatchNaN_ProducesFiniteOutput()
    {
        double[] source = [100, 110, double.NaN, 120, 130, 140, 150];
        double[] output = new double[7];

        Nyqma.Batch(source.AsSpan(), output.AsSpan(), 5, 2);

        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val));
        }
    }

    // === F) Consistency — Batch == Streaming == Span == Eventing ===

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        int period = 10;
        int nyquistPeriod = 4;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        // 1. Batch Mode
        var batchSeries = Nyqma.Batch(series, period, nyquistPeriod);
        double expected = batchSeries.Last.Value;

        // 2. Span Mode
        var tValues = series.Values.ToArray();
        var spanOutput = new double[tValues.Length];
        Nyqma.Batch(new ReadOnlySpan<double>(tValues), spanOutput, period, nyquistPeriod);
        double spanResult = spanOutput[^1];

        // 3. Streaming Mode
        var streamingInd = new Nyqma(period, nyquistPeriod);
        for (int i = 0; i < series.Count; i++)
        {
            streamingInd.Update(series[i]);
        }
        double streamingResult = streamingInd.Last.Value;

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventingInd = new Nyqma(pubSource, period, nyquistPeriod);
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

        Assert.Throws<ArgumentException>(() => Nyqma.Batch(source.AsSpan(), output.AsSpan(), 0, 2));
        Assert.Throws<ArgumentException>(() => Nyqma.Batch(source.AsSpan(), wrongSizeOutput.AsSpan(), 5, 2));
    }

    [Fact]
    public void SpanCalc_HandlesNaN()
    {
        double[] source = [100, 110, double.NaN, 120, 130];
        double[] output = new double[5];

        Nyqma.Batch(source.AsSpan(), output.AsSpan(), 3, 1);

        foreach (var val in output)
        {
            Assert.True(double.IsFinite(val));
        }
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

        Nyqma.Batch(source.AsSpan(), output.AsSpan(), 14, 5);

        Assert.True(double.IsFinite(output[^1]));
    }

    [Fact]
    public void SpanCalc_MatchesTSeries()
    {
        int period = 7;
        int nyquistPeriod = 3;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;

        var batchResult = Nyqma.Batch(series, period, nyquistPeriod);

        double[] src = series.Values.ToArray();
        double[] spanOutput = new double[src.Length];
        Nyqma.Batch(src.AsSpan(), spanOutput.AsSpan(), period, nyquistPeriod);

        Assert.Equal(batchResult.Last.Value, spanOutput[^1], 1e-9);
    }

    // === H) Chainability ===

    [Fact]
    public void Pub_Fires_OnUpdate()
    {
        var nyqma = new Nyqma(5, 2);
        int fireCount = 0;
        nyqma.Pub += (object? sender, in TValueEventArgs args) => fireCount++;

        nyqma.Update(new TValue(DateTime.UtcNow, 100));
        nyqma.Update(new TValue(DateTime.UtcNow, 110));

        Assert.Equal(2, fireCount);
    }

    [Fact]
    public void EventBased_Chaining_Works()
    {
        var source = new TSeries();
        var nyqma = new Nyqma(source, 5, 2);

        source.Add(new TValue(DateTime.UtcNow, 100));
        source.Add(new TValue(DateTime.UtcNow, 110));
        source.Add(new TValue(DateTime.UtcNow, 120));

        Assert.True(double.IsFinite(nyqma.Last.Value));
    }

    [Fact]
    public void StaticCalculate_MatchesInstance()
    {
        const int period = 10;
        const int nyquistPeriod = 4;
        int count = 100;
        var source = new TSeries();
        var nyqma = new Nyqma(period, nyquistPeriod);

        for (int i = 0; i < count; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddMinutes(i), i));
            nyqma.Update(source.Last);
        }

        var staticResult = Nyqma.Batch(source, period, nyquistPeriod);

        Assert.Equal(source.Count, staticResult.Count);
        Assert.Equal(nyqma.Last.Value, staticResult.Last.Value, 8);
    }

    [Fact]
    public void Dispose_UnsubscribesFromSource()
    {
        var source = new TSeries();
        var nyqma = new Nyqma(source, 5, 2);

        source.Add(new TValue(DateTime.UtcNow, 100));
        Assert.True(double.IsFinite(nyqma.Last.Value));

        nyqma.Dispose();

        // After dispose, adding to source should not crash
        source.Add(new TValue(DateTime.UtcNow, 200));
    }
}
