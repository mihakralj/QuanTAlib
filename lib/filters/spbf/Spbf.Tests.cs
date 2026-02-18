namespace QuanTAlib;

public class SpbfTests
{
    private readonly GBM _gbm;

    public SpbfTests()
    {
        _gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
    }

    // --- A) Constructor Validation ---

    [Fact]
    public void Constructor_ValidatesShortPeriod()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Spbf(shortPeriod: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Spbf(shortPeriod: -1));
    }

    [Fact]
    public void Constructor_ValidatesLongPeriod()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Spbf(longPeriod: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Spbf(longPeriod: -1));
    }

    [Fact]
    public void Constructor_ValidatesRmsPeriod()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Spbf(rmsPeriod: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Spbf(rmsPeriod: -1));
    }

    [Fact]
    public void Constructor_SetsName()
    {
        var ind = new Spbf(40, 60, 50);
        Assert.Equal("SPBF(40,60,50)", ind.Name);
    }

    [Fact]
    public void Constructor_SetsWarmupPeriod()
    {
        var ind = new Spbf(40, 60, 50);
        Assert.Equal(60, ind.WarmupPeriod); // max(longPeriod, rmsPeriod)
    }

    [Fact]
    public void Constructor_DefaultParameters()
    {
        var ind = new Spbf();
        Assert.Equal(40, ind.ShortPeriod);
        Assert.Equal(60, ind.LongPeriod);
        Assert.Equal(50, ind.RmsPeriod);
    }

    // --- B) Basic Calculation ---

    [Fact]
    public void Calc_ReturnsValue()
    {
        var ind = new Spbf(40, 60, 50);
        var result = ind.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Calc_PropertiesAccessible()
    {
        var ind = new Spbf(40, 60, 50);
        ind.Update(new TValue(DateTime.UtcNow, 100));
        ind.Update(new TValue(DateTime.UtcNow, 105));
        ind.Update(new TValue(DateTime.UtcNow, 103));
        Assert.True(double.IsFinite(ind.Last.Value));
        Assert.True(ind.IsHot);
        Assert.Equal("SPBF(40,60,50)", ind.Name);
        Assert.True(double.IsFinite(ind.Rms));
        _ = ind.IsNew;
    }

    [Fact]
    public void ConstantInput_ConvergesToZero()
    {
        // Bandpass filter on DC input → output should converge to zero
        var ind = new Spbf(40, 60, 50);
        double lastVal = 0;
        for (int i = 0; i < 500; i++)
        {
            lastVal = ind.Update(new TValue(DateTime.UtcNow, 100)).Value;
        }
        Assert.True(Math.Abs(lastVal) < 1e-6, $"Constant input should yield ~0, got {lastVal}");
    }

    [Fact]
    public void Rms_IsNonNegative()
    {
        var ind = new Spbf(40, 60, 50);
        var data = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        foreach (var item in data.Close)
        {
            ind.Update(item);
        }
        Assert.True(ind.Rms >= 0, $"RMS should be non-negative, got {ind.Rms}");
    }

    // --- C) State + Bar Correction ---

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var ind = new Spbf(40, 60, 50);
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
        var ind = new Spbf(20, 30, 10);
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
        var ind = new Spbf(20, 30, 10);
        var data = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;

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
        var ind = new Spbf(20, 30, 10);
        var data = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        foreach (var item in data.Close)
        {
            ind.Update(item);
        }

        ind.Reset();

        var ind2 = new Spbf(20, 30, 10);
        var result1 = ind.Update(new TValue(DateTime.UtcNow, 100));
        var result2 = ind2.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(result2.Value, result1.Value, 10);
    }

    // --- D) Warmup/Convergence ---

    [Fact]
    public void IsHot_AfterTwoBars()
    {
        // SPBF.IsHot => Count >= 2
        var ind = new Spbf(20, 30, 10);
        ind.Update(new TValue(DateTime.UtcNow, 100));
        Assert.False(ind.IsHot); // Count == 1 after first isNew=true

        ind.Update(new TValue(DateTime.UtcNow, 105));
        Assert.True(ind.IsHot); // Count == 2

        // Stays true after more data
        for (int i = 0; i < 50; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow, 100 + i));
        }
        Assert.True(ind.IsHot);
    }

    // --- E) Robustness ---

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var ind = new Spbf(20, 30, 10);
        ind.Update(new TValue(DateTime.UtcNow, 100));
        ind.Update(new TValue(DateTime.UtcNow, 105));

        var result = ind.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var ind = new Spbf(20, 30, 10);
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
        var ind = new Spbf(20, 30, 10);
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

        Spbf.Batch(input, output, 20, 30, 10);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"Output[{i}] should be finite");
        }
    }

    // --- F) Consistency ---

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        const int shortP = 40, longP = 60, rmsP = 50;
        var data = _gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;

        // 1. Span Mode
        double[] spanOutput = new double[series.Count];
        Spbf.Batch(series.Values.ToArray(), spanOutput, shortP, longP, rmsP);

        // 2. TSeries Batch Mode
        var batchInd = new Spbf(shortP, longP, rmsP);
        var batchResult = batchInd.Update(series);

        // 3. Streaming Mode
        var streamInd = new Spbf(shortP, longP, rmsP);
        var streamResults = new List<double>();
        foreach (var item in series)
        {
            streamResults.Add(streamInd.Update(item).Value);
        }

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventInd = new Spbf(pubSource, shortP, longP, rmsP);
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
        Assert.Equal(spanOutput[^1], eventInd.Last.Value, 1e-9);
    }

    // --- G) Span API ---

    [Fact]
    public void SpanCalc_ValidatesLength()
    {
        double[] source = new double[10];
        double[] output = new double[5]; // Mismatched!

        Assert.Throws<ArgumentException>(() => Spbf.Batch(source, output));
    }

    [Fact]
    public void SpanCalc_ConstantInput_ConvergesToZero()
    {
        double[] input = Enumerable.Repeat(100.0, 500).ToArray();
        double[] output = new double[500];

        Spbf.Batch(input, output, 40, 60, 50);

        Assert.True(Math.Abs(output[^1]) < 1e-6, $"Expected ~0 for constant input, got {output[^1]}");
    }

    [Fact]
    public void SpanCalc_MatchesTSeriesCalc()
    {
        var data = _gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;

        // Span
        double[] spanOutput = new double[series.Count];
        Spbf.Batch(series.Values.ToArray(), spanOutput, 40, 60, 50);

        // TSeries
        var ind = new Spbf(40, 60, 50);
        var tseriesResult = ind.Update(series);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(spanOutput[i], tseriesResult[i].Value, 1e-9);
        }
    }

    [Fact]
    public void BatchWithRms_ValidatesLength()
    {
        double[] source = new double[10];
        double[] pb = new double[5];
        double[] rms = new double[10];

        Assert.Throws<ArgumentException>(() => Spbf.BatchWithRms(source, pb, rms));
    }

    [Fact]
    public void BatchWithRms_ProducesValidOutput()
    {
        var data = _gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();
        double[] pb = new double[input.Length];
        double[] rms = new double[input.Length];

        Spbf.BatchWithRms(input, pb, rms, 40, 60, 50);

        for (int i = 0; i < input.Length; i++)
        {
            Assert.True(double.IsFinite(pb[i]), $"PB[{i}] should be finite");
            Assert.True(double.IsFinite(rms[i]) && rms[i] >= 0, $"RMS[{i}] should be finite and non-negative");
        }
    }

    // --- H) Chainability ---

    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var ind = new Spbf(20, 30, 10);
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
        var ind = new Spbf(source, 20, 30, 10);

        source.Add(new TValue(DateTime.UtcNow, 100));
        source.Add(new TValue(DateTime.UtcNow, 105));

        Assert.True(double.IsFinite(ind.Last.Value));
    }

    // --- Additional ---

    [Fact]
    public void DifferentParameters_ProduceDifferentResults()
    {
        var data = _gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;

        var ind1 = new Spbf(40, 60, 50);
        var ind2 = new Spbf(20, 30, 25);

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

        Spbf.Batch(input, output, 40, 60, 50);

        Assert.True(double.IsFinite(output[^1]));
    }
}
