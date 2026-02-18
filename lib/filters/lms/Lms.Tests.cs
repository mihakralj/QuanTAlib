namespace QuanTAlib;

public class LmsTests
{
    private readonly GBM _gbm;

    public LmsTests()
    {
        _gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
    }

    // --- A) Constructor Validation ---

    [Fact]
    public void Constructor_ValidatesOrder_TooSmall()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Lms(order: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Lms(order: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Lms(order: -1));
    }

    [Fact]
    public void Constructor_ValidatesMu_TooSmall()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Lms(order: 4, mu: 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Lms(order: 4, mu: -0.1));
    }

    [Fact]
    public void Constructor_ValidatesMu_TooLarge()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Lms(order: 4, mu: 2.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Lms(order: 4, mu: 5.0));
    }

    [Fact]
    public void Constructor_SetsName()
    {
        var ind = new Lms(16, 0.50);
        Assert.Equal("LMS(16,0.50)", ind.Name);
    }

    [Fact]
    public void Constructor_SetsWarmupPeriod()
    {
        var ind = new Lms(16, 0.50);
        Assert.Equal(17, ind.WarmupPeriod); // order + 1
    }

    [Fact]
    public void Constructor_DefaultParameters()
    {
        var ind = new Lms();
        Assert.Equal(16, ind.Order);
        Assert.Equal(0.5, ind.Mu);
    }

    [Fact]
    public void Constructor_ExposesProperties()
    {
        var ind = new Lms(8, 0.3);
        Assert.Equal(8, ind.Order);
        Assert.Equal(0.3, ind.Mu, 1e-15);
    }

    // --- B) Basic Calculation ---

    [Fact]
    public void Calc_ReturnsValue()
    {
        var ind = new Lms(4, 0.5);
        var result = ind.Update(new TValue(DateTime.UtcNow, 100));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Calc_PropertiesAccessible()
    {
        var ind = new Lms(4, 0.5);
        for (int i = 0; i < 10; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow, 100 + i));
        }
        Assert.True(double.IsFinite(ind.Last.Value));
        Assert.True(ind.IsHot);
        Assert.Equal("LMS(4,0.50)", ind.Name);
        _ = ind.IsNew;
    }

    [Fact]
    public void Calc_PassthroughDuringWarmup()
    {
        // During warmup (count <= order), output should equal input
        var ind = new Lms(4, 0.5);
        for (int i = 0; i < 4; i++)
        {
            double val = 100 + i;
            var result = ind.Update(new TValue(DateTime.UtcNow, val));
            Assert.Equal(val, result.Value, 1e-10);
        }
    }

    [Fact]
    public void Calc_AdaptiveFilter_FollowsPrice()
    {
        // LMS is an overlay (price-following) filter — output should track input
        var ind = new Lms(8, 0.5);
        var data = _gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        double lastInput = 0;
        double lastOutput = 0;

        foreach (var item in data.Close)
        {
            lastOutput = ind.Update(item).Value;
            lastInput = item.Value;
        }

        // After adaptation, output should be in the neighborhood of input
        double relError = Math.Abs(lastOutput - lastInput) / Math.Abs(lastInput);
        Assert.True(relError < 0.5, $"LMS output should track price, relative error = {relError:P2}");
    }

    // --- C) State + Bar Correction ---

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        // During warmup (passthrough), isNew=false with different value gives different output
        var ind = new Lms(4, 0.5);
        ind.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        ind.Update(new TValue(DateTime.UtcNow, 105), isNew: true);
        double val1 = ind.Last.Value;

        // In passthrough mode (count <= order), output = val, so different val = different output
        ind.Update(new TValue(DateTime.UtcNow, 110), isNew: false);
        double val2 = ind.Last.Value;
        Assert.NotEqual(val1, val2);
    }

    [Fact]
    public void Calc_IsNew_False_RollsBackAndRecomputes()
    {
        // isNew=false should roll back state and recompute with new value
        var ind = new Lms(4, 0.5);
        for (int i = 0; i < 6; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow, 100 + i), isNew: true);
        }

        // Correction with isNew=false
        var corrected = ind.Update(new TValue(DateTime.UtcNow, 200), isNew: false);
        Assert.True(double.IsFinite(corrected.Value), "Correction should produce finite output");
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var ind = new Lms(4, 0.5);
        var data = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;

        for (int i = 0; i < series.Count; i++)
        {
            ind.Update(series[i]);
        }
        double originalValue = ind.Last.Value;

        // Two sequential isNew=false corrections should produce consistent results
        // (each correction restores state and recomputes)
        var correction1 = ind.Update(new TValue(DateTime.UtcNow, 200), isNew: false);
        Assert.True(double.IsFinite(correction1.Value));

        var correction2 = ind.Update(new TValue(DateTime.UtcNow, 300), isNew: false);
        Assert.True(double.IsFinite(correction2.Value));

        // Replaying the same correction value should produce the same result (deterministic)
        var correction2b = ind.Update(new TValue(DateTime.UtcNow, 300), isNew: false);
        Assert.Equal(correction2.Value, correction2b.Value, 10);

        // Replaying original value should produce original prediction
        // (weights and buffer are restored from snapshot each time)
        ind.Update(series[^1], isNew: false);
        double restoredValue = ind.Last.Value;
        Assert.Equal(originalValue, restoredValue, 10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var ind = new Lms(4, 0.5);
        var data = _gbm.Fetch(50, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        foreach (var item in data.Close)
        {
            ind.Update(item);
        }

        ind.Reset();

        var ind2 = new Lms(4, 0.5);
        var result1 = ind.Update(new TValue(DateTime.UtcNow, 100));
        var result2 = ind2.Update(new TValue(DateTime.UtcNow, 100));
        Assert.Equal(result2.Value, result1.Value, 10);
    }

    // --- D) Warmup/Convergence ---

    [Fact]
    public void IsHot_AfterEnoughBars()
    {
        var ind = new Lms(4, 0.5);
        // Need count > order = 4, so 5 bars
        for (int i = 0; i < 4; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow, 100 + i));
            Assert.False(ind.IsHot, $"Should not be hot at count={i + 1}");
        }

        ind.Update(new TValue(DateTime.UtcNow, 104));
        Assert.True(ind.IsHot, "Should be hot after order+1 bars");

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
        var ind = new Lms(4, 0.5);
        for (int i = 0; i < 6; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow, 100 + i));
        }

        var result = ind.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var ind = new Lms(4, 0.5);
        for (int i = 0; i < 6; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow, 100 + i));
        }

        var result = ind.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(result.Value));

        var result2 = ind.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(result2.Value));
    }

    [Fact]
    public void MultipleNaN_ContinuesWithLastValid()
    {
        var ind = new Lms(4, 0.5);
        for (int i = 0; i < 6; i++)
        {
            ind.Update(new TValue(DateTime.UtcNow, 100 + i));
        }

        for (int i = 0; i < 10; i++)
        {
            var result = ind.Update(new TValue(DateTime.UtcNow, double.NaN));
            Assert.True(double.IsFinite(result.Value));
        }
    }

    [Fact]
    public void BatchCalc_HandlesNaN()
    {
        double[] input = [100, 105, double.NaN, 110, double.NaN, 115, 120, 125, 130, 135];
        double[] output = new double[input.Length];

        Lms.Batch(input, output, 4, 0.5);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"Output[{i}] should be finite");
        }
    }

    // --- F) Consistency ---

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        const int order = 8;
        const double mu = 0.5;
        var data = _gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;

        // 1. Span Mode
        double[] spanOutput = new double[series.Count];
        Lms.Batch(series.Values.ToArray(), spanOutput, order, mu);

        // 2. TSeries Batch Mode
        var batchInd = new Lms(order, mu);
        var batchResult = batchInd.Update(series);

        // 3. Streaming Mode
        var streamInd = new Lms(order, mu);
        var streamResults = new List<double>();
        foreach (var item in series)
        {
            streamResults.Add(streamInd.Update(item).Value);
        }

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var eventInd = new Lms(pubSource, order, mu);
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

        Assert.Throws<ArgumentException>(() => Lms.Batch(source, output));
    }

    [Fact]
    public void SpanCalc_ValidatesOrder()
    {
        double[] source = new double[10];
        double[] output = new double[10];

        Assert.Throws<ArgumentOutOfRangeException>(() => Lms.Batch(source, output, order: 1));
    }

    [Fact]
    public void SpanCalc_ValidatesMu()
    {
        double[] source = new double[10];
        double[] output = new double[10];

        Assert.Throws<ArgumentOutOfRangeException>(() => Lms.Batch(source, output, order: 4, mu: 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Lms.Batch(source, output, order: 4, mu: 2.0));
    }

    [Fact]
    public void SpanCalc_MatchesTSeriesCalc()
    {
        var data = _gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = data.Close;

        // Span
        double[] spanOutput = new double[series.Count];
        Lms.Batch(series.Values.ToArray(), spanOutput, 8, 0.5);

        // TSeries
        var ind = new Lms(8, 0.5);
        var tseriesResult = ind.Update(series);

        for (int i = 0; i < series.Count; i++)
        {
            Assert.Equal(spanOutput[i], tseriesResult[i].Value, 1e-9);
        }
    }

    [Fact]
    public void SpanCalc_NaN_Safe()
    {
        double[] input = new double[50];
        for (int i = 0; i < 50; i++)
        {
            input[i] = i % 7 == 0 ? double.NaN : 100.0 + Math.Sin(i * 0.1);
        }
        double[] output = new double[50];

        Lms.Batch(input, output, 4, 0.5);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"Output[{i}] should be finite with NaN input");
        }
    }

    // --- H) Chainability ---

    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var ind = new Lms(4, 0.5);
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
        var ind = new Lms(source, 4, 0.5);

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

        var ind1 = new Lms(8, 0.5);
        var ind2 = new Lms(16, 0.3);

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

        Lms.Batch(input, output, 16, 0.5);

        Assert.True(double.IsFinite(output[^1]));
    }
}
