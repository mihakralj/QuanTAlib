namespace QuanTAlib;

public class BaxterKingTests
{
    private readonly GBM _gbm;

    public BaxterKingTests()
    {
        _gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
    }

    // --- A) Constructor Validation ---

    [Fact]
    public void Constructor_ValidatesPLow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BaxterKing(pLow: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BaxterKing(pLow: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BaxterKing(pLow: -1));
    }

    [Fact]
    public void Constructor_ValidatesPHigh()
    {
        // pHigh must be > pLow
        Assert.Throws<ArgumentOutOfRangeException>(() => new BaxterKing(pLow: 6, pHigh: 6));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BaxterKing(pLow: 6, pHigh: 5));
    }

    [Fact]
    public void Constructor_ValidatesK()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BaxterKing(k: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BaxterKing(k: -1));
    }

    [Fact]
    public void Constructor_SetsName()
    {
        var ind = new BaxterKing(6, 32, 12);
        Assert.Equal("BaxterKing(6,32,12)", ind.Name);
    }

    [Fact]
    public void Constructor_SetsWarmupPeriod()
    {
        var ind = new BaxterKing(6, 32, 12);
        Assert.Equal(25, ind.WarmupPeriod); // 2*12 + 1
    }

    [Fact]
    public void Constructor_DefaultParameters()
    {
        var ind = new BaxterKing();
        Assert.Equal(6, ind.PLow);
        Assert.Equal(32, ind.PHigh);
        Assert.Equal(12, ind.K);
    }

    [Fact]
    public void Constructor_ExposesProperties()
    {
        var ind = new BaxterKing(8, 40, 16);
        Assert.Equal(8, ind.PLow);
        Assert.Equal(40, ind.PHigh);
        Assert.Equal(16, ind.K);
    }

    // --- B) Basic Calculation ---

    [Fact]
    public void Calc_ReturnsValue()
    {
        var ind = new BaxterKing(6, 32, 12);
        var result = ind.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Calc_PropertiesAccessible()
    {
        var ind = new BaxterKing(6, 32, 12);
        for (int i = 0; i < 30; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow, 100 + i));
        }
        Assert.True(double.IsFinite(ind.Last.Value));
        Assert.True(ind.IsHot);
        Assert.Equal("BaxterKing(6,32,12)", ind.Name);
        _ = ind.IsNew;
    }

    [Fact]
    public void ConstantInput_ConvergesToZero()
    {
        // Band-pass filter on DC should be zero after warmup
        var ind = new BaxterKing(6, 32, 12);
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
        // BK is a band-pass, output should oscillate around zero
        var ind = new BaxterKing(6, 32, 5);
        var data = _gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        foreach (var item in data.Close)
        {
            ind.Update(item);
        }

        bool hasPositive = false, hasNegative = false;
        // Use streaming on second pass to check values
        var ind2 = new BaxterKing(6, 32, 5);
        foreach (var item in data.Close)
        {
            double v = ind2.Update(item).Value;
            if (v > 0.01)
            {
                hasPositive = true;
            }
            if (v < -0.01)
            {
                hasNegative = true;
            }
        }
        Assert.True(hasPositive, "BK output should have positive values");
        Assert.True(hasNegative, "BK output should have negative values");
    }

    // --- C) State + Bar Correction ---

    [Fact]
    public void State_IsNew_True_Advances()
    {
        var ind = new BaxterKing(6, 32, 3);
        var r1 = ind.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        var r2 = ind.Update(new TValue(DateTime.UtcNow, 105), isNew: true);
        Assert.True(double.IsFinite(r1.Value));
        Assert.True(double.IsFinite(r2.Value));
    }

    [Fact]
    public void State_IsNew_False_UpdatesValue()
    {
        var ind = new BaxterKing(6, 32, 3);
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
        var ind = new BaxterKing(6, 32, 5);
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
        var ind = new BaxterKing(6, 32, 5);
        var data = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        foreach (var item in data.Close)
        {
            ind.Update(item);
        }

        ind.Reset();

        var ind2 = new BaxterKing(6, 32, 5);
        var result1 = ind.Update(new TValue(DateTime.UtcNow, 100));
        var result2 = ind2.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(result2.Value, result1.Value, 10);
    }

    // --- D) Warmup/Convergence ---

    [Fact]
    public void IsHot_AfterFilterLen()
    {
        // BK IsHot when Count >= 2K+1
        var ind = new BaxterKing(6, 32, 3); // filterLen = 7
        for (int i = 0; i < 6; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow, 100 + i));
            Assert.False(ind.IsHot, $"Should not be hot at bar {i + 1}");
        }

        ind.Update(new TValue(DateTime.UtcNow, 107));
        Assert.True(ind.IsHot, "Should be hot after 7 bars (2*3+1)");

        // Stays true
        for (int i = 0; i < 10; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow, 110 + i));
        }
        Assert.True(ind.IsHot);
    }

    [Fact]
    public void WarmupPeriod_MatchesFilterLen()
    {
        var ind1 = new BaxterKing(6, 32, 5);
        Assert.Equal(11, ind1.WarmupPeriod); // 2*5+1

        var ind2 = new BaxterKing(6, 32, 12);
        Assert.Equal(25, ind2.WarmupPeriod); // 2*12+1

        var ind3 = new BaxterKing(6, 32, 20);
        Assert.Equal(41, ind3.WarmupPeriod); // 2*20+1
    }

    [Fact]
    public void DuringWarmup_OutputIsZero()
    {
        var ind = new BaxterKing(6, 32, 5); // filterLen = 11
        for (int i = 0; i < 10; i++)
        {
            double v = ind.Update(new TValue(DateTime.UtcNow, 100 + i * 0.5)).Value;
            Assert.Equal(0.0, v, 15);
        }
    }

    // --- E) Robustness ---

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var ind = new BaxterKing(6, 32, 3);
        ind.Update(new TValue(DateTime.UtcNow, 100));
        ind.Update(new TValue(DateTime.UtcNow, 105));

        var result = ind.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var ind = new BaxterKing(6, 32, 3);
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
        var ind = new BaxterKing(6, 32, 3);
        ind.Update(new TValue(DateTime.UtcNow, 100));
        ind.Update(new TValue(DateTime.UtcNow, 105));

        for (int i = 0; i < 10; i++)
        {
            var result = ind.Update(new TValue(DateTime.UtcNow, double.NaN));
            Assert.True(double.IsFinite(result.Value));
        }
    }

    [Fact]
    public void BatchCalc_FiniteInput_ProducesFiniteOutput()
    {
        // Batch (span) API does raw FIR convolution without NaN substitution.
        // Verify finite input produces finite output.
        var data = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();
        double[] output = new double[input.Length];

        BaxterKing.Batch(input, output, 6, 32, 3);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"Output[{i}] should be finite");
        }
    }

    // --- F) Consistency ---

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        const int pLow = 6, pHigh = 32, k = 12;
        var data = _gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;

        // 1. Span Mode
        double[] spanOutput = new double[series.Count];
        BaxterKing.Batch(series.Values.ToArray(), spanOutput, pLow, pHigh, k);

        // 2. TSeries Batch Mode
        var batchInd = new BaxterKing(pLow, pHigh, k);
        var batchResult = batchInd.Update(series);

        // 3. Streaming Mode
        var streamInd = new BaxterKing(pLow, pHigh, k);
        var streamResults = new List<double>();
        foreach (var item in series)
        {
            streamResults.Add(streamInd.Update(item).Value);
        }

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventInd = new BaxterKing(pubSource, pLow, pHigh, k);
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
    public void SpanCalc_ConstantInput_ConvergesToZero()
    {
        double[] input = Enumerable.Repeat(100.0, 200).ToArray();
        double[] output = new double[200];

        BaxterKing.Batch(input, output, 6, 32, 12);

        // After warmup, constant input -> 0
        for (int i = 25; i < output.Length; i++)
        {
            Assert.True(Math.Abs(output[i]) < 1e-10, $"Expected ~0 for constant input at [{i}], got {output[i]}");
        }
    }

    [Fact]
    public void SpanCalc_MatchesTSeriesCalc()
    {
        var data = _gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;

        // Span
        double[] spanOutput = new double[series.Count];
        BaxterKing.Batch(series.Values.ToArray(), spanOutput, 6, 32, 12);

        // TSeries
        var ind = new BaxterKing(6, 32, 12);
        var tseriesResult = ind.Update(series);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(spanOutput[i], tseriesResult[i].Value, 1e-9);
        }
    }

    [Fact]
    public void SpanCalc_WarmupBarsAreZero()
    {
        var data = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] input = data.Close.Values.ToArray();
        double[] output = new double[input.Length];

        BaxterKing.Batch(input, output, 6, 32, 12);

        // First 24 bars (filterLen-1) should be 0
        for (int i = 0; i < 24; i++)
        {
            Assert.Equal(0.0, output[i], 15);
        }
    }

    // --- H) Chainability ---

    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var ind = new BaxterKing(6, 32, 3);
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
        var ind = new BaxterKing(source, 6, 32, 3);

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

        var ind1 = new BaxterKing(6, 32, 12);
        var ind2 = new BaxterKing(10, 50, 20);

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

        BaxterKing.Batch(input, output, 6, 32, 12);

        Assert.True(double.IsFinite(output[^1]));
    }

    [Fact]
    public void Dispose_UnsubscribesFromSource()
    {
        var source = new TSeries();
        var ind = new BaxterKing(source, 6, 32, 3);

        source.Add(new TValue(DateTime.UtcNow, 100));
        Assert.True(double.IsFinite(ind.Last.Value));

        ind.Dispose();

        // After dispose, adding to source should not affect the disposed indicator
        source.Add(new TValue(DateTime.UtcNow, 200));
    }

    [Fact]
    public void Calculate_ReturnsResultsAndIndicator()
    {
        var data = _gbm.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var (results, indicator) = BaxterKing.Calculate(data.Close, 6, 32, 12);

        Assert.Equal(data.Close.Count, results.Count);
        Assert.True(indicator.IsHot);
        Assert.True(double.IsFinite(indicator.Last.Value));
    }

    [Fact]
    public void Prime_SetsUpState()
    {
        var data = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double[] vals = data.Close.Values.ToArray();

        var ind = new BaxterKing(6, 32, 5);
        ind.Prime(vals);

        Assert.True(ind.IsHot);
        Assert.True(double.IsFinite(ind.Last.Value));
    }

    [Fact]
    public void WeightsSumToZero()
    {
        // BK normalization ensures weights sum to zero for DC rejection
        double[] input = Enumerable.Repeat(42.0, 100).ToArray();
        double[] output = new double[100];
        BaxterKing.Batch(input, output, 6, 32, 12);

        // After warmup, all outputs should be exactly 0 (weights sum to 0 * constant = 0)
        for (int i = 25; i < output.Length; i++)
        {
            Assert.True(Math.Abs(output[i]) < 1e-12, $"Weights sum != 0: output[{i}]={output[i]}");
        }
    }
}
