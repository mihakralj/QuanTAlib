namespace QuanTAlib;

public class RoofingTests
{
    private readonly GBM _gbm;

    public RoofingTests()
    {
        _gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
    }

    // --- A) Constructor Validation ---

    [Fact]
    public void Constructor_ValidatesHpLength()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Roofing(hpLength: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Roofing(hpLength: -1));
    }

    [Fact]
    public void Constructor_ValidatesSsLength()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Roofing(ssLength: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Roofing(ssLength: -1));
    }

    [Fact]
    public void Constructor_SetsName()
    {
        var ind = new Roofing(48, 10);
        Assert.Equal("ROOFING(48,10)", ind.Name);
    }

    [Fact]
    public void Constructor_SetsWarmupPeriod()
    {
        var ind = new Roofing(48, 10);
        Assert.Equal(48, ind.WarmupPeriod);
    }

    [Fact]
    public void Constructor_DefaultParameters()
    {
        var ind = new Roofing();
        Assert.Equal(48, ind.HpLength);
        Assert.Equal(10, ind.SsLength);
    }

    // --- B) Basic Calculation ---

    [Fact]
    public void Calc_ReturnsValue()
    {
        var ind = new Roofing(48, 10);
        var result = ind.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Calc_PropertiesAccessible()
    {
        var ind = new Roofing(48, 10);
        ind.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(double.IsFinite(ind.Last.Value));
        Assert.True(ind.IsHot); // Roof2 defaults to 0.0 (finite) → IsHot true after first finite update
        Assert.Equal("ROOFING(48,10)", ind.Name);
        _ = ind.IsNew;
    }

    [Fact]
    public void ConstantInput_ConvergesToZero()
    {
        // Bandpass filter applied to DC input → output should converge to zero
        var ind = new Roofing(48, 10);
        double lastVal = 0;
        for (int i = 0; i < 500; i++)
        {
            lastVal = ind.Update(new TValue(DateTime.UtcNow, 100)).Value;
        }
        Assert.True(Math.Abs(lastVal) < 1e-6, $"Constant input should yield ~0, got {lastVal}");
    }

    // --- C) State + Bar Correction ---

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var ind = new Roofing(48, 10);
        ind.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        ind.Update(new TValue(DateTime.UtcNow, 105), isNew: true);
        double val1 = ind.Last.Value;

        ind.Update(new TValue(DateTime.UtcNow, 110), isNew: false);
        double val2 = ind.Last.Value;
        Assert.NotEqual(val1, val2);
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var ind = new Roofing(20, 5);
        ind.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        ind.Update(new TValue(DateTime.UtcNow, 105), isNew: true);
        double val1 = ind.Last.Value;

        ind.Update(new TValue(DateTime.UtcNow, 110), isNew: false);
        double val2 = ind.Last.Value;
        Assert.NotEqual(val1, val2);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var ind = new Roofing(20, 5);
        var data = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;

        // Feed N values
        for (int i = 0; i < series.Count; i++)
        {
            ind.Update(series[i]);
        }
        double originalValue = ind.Last.Value;

        // Feed M corrections with isNew=false
        ind.Update(new TValue(DateTime.UtcNow, 200), isNew: false);
        ind.Update(new TValue(DateTime.UtcNow, 300), isNew: false);
        ind.Update(new TValue(DateTime.UtcNow, 400), isNew: false);

        // Restore with original last value
        ind.Update(series[^1], isNew: false);
        double restoredValue = ind.Last.Value;

        Assert.Equal(originalValue, restoredValue, 10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var ind = new Roofing(20, 5);
        var data = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        foreach (var item in data.Close)
        {
            ind.Update(item);
        }

        ind.Reset();

        var ind2 = new Roofing(20, 5);
        var result1 = ind.Update(new TValue(DateTime.UtcNow, 100));
        var result2 = ind2.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(result2.Value, result1.Value, 10);
    }

    // --- D) Warmup/Convergence ---

    [Fact]
    public void IsHot_TrueAfterFirstUpdate()
    {
        // Roofing.IsHot => double.IsFinite(_state.Roof2)
        // Roof2 defaults to 0.0 (finite), so IsHot is true immediately after first finite update
        var ind = new Roofing(20, 5);
        ind.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(ind.IsHot);

        // Stays true after more data
        for (int i = 0; i < 20; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow, 100 + i));
        }
        Assert.True(ind.IsHot);
    }

    // --- E) Robustness ---

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var ind = new Roofing(20, 5);
        ind.Update(new TValue(DateTime.UtcNow, 100));
        ind.Update(new TValue(DateTime.UtcNow, 105));

        var result = ind.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var ind = new Roofing(20, 5);
        ind.Update(new TValue(DateTime.UtcNow, 100));
        ind.Update(new TValue(DateTime.UtcNow, 105));

        var result = ind.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(result.Value));

        var result2 = ind.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(result2.Value));
    }

    [Fact]
    public void MultipleNaN_ContinuesWithLastValid()
    {
        var ind = new Roofing(20, 5);
        ind.Update(new TValue(DateTime.UtcNow, 100));
        ind.Update(new TValue(DateTime.UtcNow, 105));

        for (int i = 0; i < 10; i++)
        {
            var result = ind.Update(new TValue(DateTime.UtcNow, double.NaN));
            Assert.True(double.IsFinite(result.Value));
        }
    }

    [Fact]
    public void BatchCalc_HandlesNaN()
    {
        double[] input = [100, 105, double.NaN, 110, double.NaN, 115];
        double[] output = new double[input.Length];

        Roofing.Batch(input, output, 20, 5);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"Output[{i}] should be finite");
        }
    }

    // --- F) Consistency ---

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        const int hpLength = 48;
        const int ssLength = 10;
        var data = _gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;

        // 1. Span Mode
        double[] spanOutput = new double[series.Count];
        Roofing.Batch(series.Values.ToArray(), spanOutput, hpLength, ssLength);

        // 2. TSeries Batch Mode
        var roofBatch = new Roofing(hpLength, ssLength);
        var batchResult = roofBatch.Update(series);

        // 3. Streaming Mode
        var roofStream = new Roofing(hpLength, ssLength);
        var streamResults = new List<double>();
        foreach (var item in series)
        {
            streamResults.Add(roofStream.Update(item).Value);
        }

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var roofEvent = new Roofing(pubSource, hpLength, ssLength);
        for (int i = 0; i < series.Count; i++)
        {
            pubSource.Add(series[i]);
        }

        // Assert all modes match
        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(spanOutput[i], batchResult[i].Value, 1e-9);
            Assert.Equal(spanOutput[i], streamResults[i], 1e-9);
        }
        Assert.Equal(spanOutput[^1], roofEvent.Last.Value, 1e-9);
    }

    // --- G) Span API ---

    [Fact]
    public void SpanCalc_ValidatesLength()
    {
        double[] source = new double[10];
        double[] output = new double[5]; // Mismatched!

        Assert.Throws<ArgumentException>(() => Roofing.Batch(source, output));
    }

    [Fact]
    public void SpanCalc_ConstantInput_ConvergesToZero()
    {
        double[] input = Enumerable.Repeat(100.0, 500).ToArray();
        double[] output = new double[500];

        Roofing.Batch(input, output, 48, 10);

        Assert.True(Math.Abs(output[^1]) < 1e-6, $"Expected ~0 for constant input, got {output[^1]}");
    }

    [Fact]
    public void SpanCalc_MatchesTSeriesCalc()
    {
        var data = _gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;

        // Span
        double[] spanOutput = new double[series.Count];
        Roofing.Batch(series.Values.ToArray(), spanOutput, 48, 10);

        // TSeries
        var ind = new Roofing(48, 10);
        var tseriesResult = ind.Update(series);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(spanOutput[i], tseriesResult[i].Value, 1e-9);
        }
    }

    // --- H) Chainability ---

    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var ind = new Roofing(20, 5);
        int fireCount = 0;
        ind.Pub += (object? _, in TValueEventArgs _) => fireCount++;

        ind.Update(new TValue(DateTime.UtcNow, 100));
        ind.Update(new TValue(DateTime.UtcNow, 105));

        Assert.Equal(2, fireCount);
    }

    [Fact]
    public void EventChaining_Works()
    {
        var source = new TSeries();
        var ind = new Roofing(source, 20, 5);

        source.Add(new TValue(DateTime.UtcNow, 100));
        source.Add(new TValue(DateTime.UtcNow, 105));

        Assert.True(double.IsFinite(ind.Last.Value));
    }

    // --- Additional: Different Parameters ---

    [Fact]
    public void DifferentParameters_ProduceDifferentResults()
    {
        var data = _gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;

        var ind1 = new Roofing(48, 10);
        var ind2 = new Roofing(20, 5);

        foreach (var item in series)
        {
            ind1.Update(item);
            ind2.Update(item);
        }

        Assert.NotEqual(ind1.Last.Value, ind2.Last.Value);
    }

    [Fact]
    public void LargeDataset_DoesNotThrow()
    {
        var data = _gbm.Fetch(10000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;

        double[] input = series.Values.ToArray();
        double[] output = new double[input.Length];

        Roofing.Batch(input, output, 48, 10);

        Assert.True(double.IsFinite(output[^1]));
    }
}
