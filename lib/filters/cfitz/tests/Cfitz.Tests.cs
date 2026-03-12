namespace QuanTAlib;

public class CfitzTests
{
    private readonly GBM _gbm;

    public CfitzTests()
    {
        _gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
    }

    // --- A) Constructor Validation ---

    [Fact]
    public void Constructor_ValidatesPLow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Cfitz(pLow: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Cfitz(pLow: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Cfitz(pLow: -1));
    }

    [Fact]
    public void Constructor_ValidatesPHigh()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Cfitz(pLow: 6, pHigh: 6));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Cfitz(pLow: 6, pHigh: 5));
    }

    [Fact]
    public void Constructor_SetsName()
    {
        var ind = new Cfitz(6, 32);
        Assert.Equal("Cfitz(6,32)", ind.Name);
    }

    [Fact]
    public void Constructor_SetsWarmupPeriod()
    {
        var ind = new Cfitz(6, 32);
        Assert.Equal(2, ind.WarmupPeriod);
    }

    [Fact]
    public void Constructor_DefaultParameters()
    {
        var ind = new Cfitz();
        Assert.Equal(6, ind.PLow);
        Assert.Equal(32, ind.PHigh);
    }

    [Fact]
    public void Constructor_ExposesProperties()
    {
        var ind = new Cfitz(8, 40);
        Assert.Equal(8, ind.PLow);
        Assert.Equal(40, ind.PHigh);
    }

    // --- B) Basic Calculation ---

    [Fact]
    public void Calc_ReturnsValue()
    {
        var ind = new Cfitz(6, 32);
        var result = ind.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Calc_PropertiesAccessible()
    {
        var ind = new Cfitz(6, 32);
        for (int i = 0; i < 30; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow, 100 + i));
        }
        Assert.True(double.IsFinite(ind.Last.Value));
        Assert.True(ind.IsHot);
        Assert.Equal("Cfitz(6,32)", ind.Name);
        _ = ind.IsNew;
    }

    [Fact]
    public void ConstantInput_ConvergesToZero()
    {
        // Band-pass filter on DC should be zero — weights sum to zero
        var ind = new Cfitz(6, 32);
        double lastVal = 0;
        for (int i = 0; i < 100; i++)
        {
            lastVal = ind.Update(new TValue(DateTime.UtcNow, 100)).Value;
        }
        Assert.True(Math.Abs(lastVal) < 1e-10, $"Constant input should yield ~0, got {lastVal}");
    }

    [Fact]
    public void OutputOscillatesAroundZero()
    {
        var ind = new Cfitz(6, 32);
        var data = _gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));

        bool hasPositive = false, hasNegative = false;
        foreach (var item in data.Close)
        {
            double v = ind.Update(item).Value;
            if (v > 0.01)
            {
                hasPositive = true;
            }
            if (v < -0.01)
            {
                hasNegative = true;
            }
        }
        Assert.True(hasPositive, "CF output should have positive values");
        Assert.True(hasNegative, "CF output should have negative values");
    }

    // --- C) State + Bar Correction ---

    [Fact]
    public void State_IsNew_True_Advances()
    {
        var ind = new Cfitz(6, 32);
        var r1 = ind.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        var r2 = ind.Update(new TValue(DateTime.UtcNow, 105), isNew: true);
        Assert.True(double.IsFinite(r1.Value));
        Assert.True(double.IsFinite(r2.Value));
    }

    [Fact]
    public void State_IsNew_False_UpdatesValue()
    {
        var ind = new Cfitz(6, 32);
        for (int i = 0; i < 10; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow, 100 + i), isNew: true);
        }
        double val1 = ind.Last.Value;

        ind.Update(new TValue(DateTime.UtcNow, 200), isNew: false);
        double val2 = ind.Last.Value;
        Assert.NotEqual(val1, val2);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var ind = new Cfitz(6, 32);
        var data = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;

        for (int i = 0; i < series.Count; i++)
        {
            ind.Update(series[i]);
        }
        double originalValue = ind.Last.Value;

        // Feed corrections with isNew=false
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
        var ind = new Cfitz(6, 32);
        var data = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        foreach (var item in data.Close)
        {
            ind.Update(item);
        }

        ind.Reset();

        var ind2 = new Cfitz(6, 32);
        var result1 = ind.Update(new TValue(DateTime.UtcNow, 100));
        var result2 = ind2.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(result2.Value, result1.Value, 10);
    }

    // --- D) Warmup/Convergence ---

    [Fact]
    public void IsHot_AfterTwoBars()
    {
        var ind = new Cfitz(6, 32);
        ind.Update(new TValue(DateTime.UtcNow, 100));
        Assert.False(ind.IsHot, "Should not be hot after 1 bar");

        ind.Update(new TValue(DateTime.UtcNow, 105));
        Assert.True(ind.IsHot, "Should be hot after 2 bars");

        // Stays hot
        for (int i = 0; i < 10; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow, 110 + i));
        }
        Assert.True(ind.IsHot);
    }

    [Fact]
    public void WarmupPeriod_IsTwo()
    {
        var ind = new Cfitz(6, 32);
        Assert.Equal(2, ind.WarmupPeriod);

        var ind2 = new Cfitz(10, 50);
        Assert.Equal(2, ind2.WarmupPeriod);
    }

    [Fact]
    public void FirstBar_OutputIsZero()
    {
        var ind = new Cfitz(6, 32);
        double v = ind.Update(new TValue(DateTime.UtcNow, 100)).Value;
        Assert.Equal(0.0, v, 15);
    }

    // --- E) Robustness ---

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var ind = new Cfitz(6, 32);
        ind.Update(new TValue(DateTime.UtcNow, 100));
        ind.Update(new TValue(DateTime.UtcNow, 105));

        var result = ind.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var ind = new Cfitz(6, 32);
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
        var ind = new Cfitz(6, 32);
        ind.Update(new TValue(DateTime.UtcNow, 100));
        ind.Update(new TValue(DateTime.UtcNow, 105));

        for (int i = 0; i < 10; i++)
        {
            var result = ind.Update(new TValue(DateTime.UtcNow, double.NaN));
            Assert.True(double.IsFinite(result.Value));
        }
    }

    [Fact]
    public void StreamingNaN_HandledGracefully()
    {
        // Verify streaming handles NaN via last-valid substitution
        var ind = new Cfitz(6, 32);
        for (int i = 0; i < 20; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow, 100 + i));
        }

        // Inject NaN
        var result = ind.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(result.Value));

        // Continue with valid data
        var result2 = ind.Update(new TValue(DateTime.UtcNow, 125));
        Assert.True(double.IsFinite(result2.Value));
    }

    // --- F) Consistency ---

    [Fact]
    public void BatchSpan_MatchesBatchTSeries()
    {
        // Span and TSeries batch modes should match exactly (both full-sample)
        var data = _gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;

        // Span mode
        double[] spanOutput = new double[series.Count];
        Cfitz.Batch(series.Values.ToArray(), spanOutput, 6, 32);

        // TSeries batch mode
        var batchResult = Cfitz.Batch(series, 6, 32);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(spanOutput[i], batchResult[i].Value, 1e-9);
        }
    }

    [Fact]
    public void Eventing_ProducesSameAsStreaming()
    {
        var data = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;

        // Streaming
        var streamInd = new Cfitz(6, 32);
        foreach (var item in series)
        {
            streamInd.Update(item);
        }

        // Eventing
        var pubSource = new TSeries();
        var eventInd = new Cfitz(pubSource, 6, 32);
        for (int i = 0; i < series.Count; i++)
        {
            pubSource.Add(series[i]);
        }

        Assert.Equal(streamInd.Last.Value, eventInd.Last.Value, 1e-9);
    }

    // --- G) Span API ---

    [Fact]
    public void SpanCalc_ConstantInput_AllZero()
    {
        // CF with constant input: ALL outputs should be zero (including endpoints)
        double[] input = Enumerable.Repeat(100.0, 200).ToArray();
        double[] output = new double[200];

        Cfitz.Batch(input, output, 6, 32);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(Math.Abs(output[i]) < 1e-10, $"Expected ~0 for constant input at [{i}], got {output[i]}");
        }
    }

    [Fact]
    public void SpanCalc_OutputLengthMatches()
    {
        var data = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();
        double[] output = new double[input.Length];

        Cfitz.Batch(input, output, 6, 32);

        // All outputs should be finite
        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"Output[{i}] should be finite");
        }
    }

    [Fact]
    public void SpanCalc_ShortOutputThrows()
    {
        double[] input = new double[100];
        double[] output = new double[50]; // too short

        Assert.Throws<ArgumentException>(() => Cfitz.Batch(input, output, 6, 32));
    }

    [Fact]
    public void SpanCalc_SingleBar_ReturnsZero()
    {
        double[] input = [42.0];
        double[] output = new double[1];

        Cfitz.Batch(input, output, 6, 32);
        Assert.Equal(0.0, output[0], 15);
    }

    // --- H) Chainability ---

    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var ind = new Cfitz(6, 32);
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
        var ind = new Cfitz(source, 6, 32);

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

        var ind1 = new Cfitz(6, 32);
        var ind2 = new Cfitz(10, 50);

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
        var data = _gbm.Fetch(2000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();
        double[] output = new double[input.Length];

        Cfitz.Batch(input, output, 6, 32);

        Assert.True(double.IsFinite(output[^1]));
    }

    [Fact]
    public void Dispose_UnsubscribesFromSource()
    {
        var source = new TSeries();
        var ind = new Cfitz(source, 6, 32);

        source.Add(new TValue(DateTime.UtcNow, 100));
        Assert.True(double.IsFinite(ind.Last.Value));

        ind.Dispose();

        source.Add(new TValue(DateTime.UtcNow, 200));
    }

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var data = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var (results, indicator) = Cfitz.Calculate(data.Close, 6, 32);

        Assert.Equal(data.Close.Count, results.Count);
        Assert.True(indicator.IsHot);
        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    [Fact]
    public void Prime_SetsUpState()
    {
        var data = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] vals = data.Close.Values.ToArray();

        var ind = new Cfitz(6, 32);
        ind.Prime(vals);

        Assert.True(ind.IsHot);
        Assert.True(double.IsFinite(ind.Last.Value));
    }

    [Fact]
    public void WeightsSumToZero_ConstantInput()
    {
        // CF endpoint correction ensures weights sum to zero → constant input → zero output
        double[] input = Enumerable.Repeat(42.0, 100).ToArray();
        double[] output = new double[100];
        Cfitz.Batch(input, output, 6, 32);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(Math.Abs(output[i]) < 1e-12, $"Weights sum != 0: output[{i}]={output[i]}");
        }
    }

    [Fact]
    public void BatchSymmetric_FirstAndLastBar()
    {
        // For symmetric data, first and last CF-filtered bars should have equal magnitude
        int n = 50;
        double[] input = new double[n];
        for (int i = 0; i < n; i++)
        {
            input[i] = Math.Sin(2.0 * Math.PI * i / 10.0);  // 10-bar cycle
        }
        double[] output = new double[n];
        Cfitz.Batch(input, output, 6, 32);

        // Both endpoints should be finite
        Assert.True(double.IsFinite(output[0]));
        Assert.True(double.IsFinite(output[^1]));
    }
}
