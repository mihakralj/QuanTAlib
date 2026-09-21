namespace QuanTAlib;

public class AgcTests
{
    // Helper: generate a sine wave that oscillates around zero
    private static TSeries MakeSineWave(int count, double amplitude = 1.0, double period = 20.0)
    {
        var series = new TSeries();
        DateTime t = DateTime.UtcNow;
        for (int i = 0; i < count; i++)
        {
            double val = amplitude * Math.Sin(2.0 * Math.PI * i / period);
            series.Add(new TValue(t.AddMinutes(i), val));
        }
        return series;
    }

    // --- A) Constructor Validation ---

    [Fact]
    public void Constructor_ValidatesDecay_TooLow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Agc(decay: 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Agc(decay: -0.5));
    }

    [Fact]
    public void Constructor_ValidatesDecay_TooHigh()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Agc(decay: 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Agc(decay: 1.5));
    }

    [Fact]
    public void Constructor_SetsName()
    {
        var ind = new Agc(0.991);
        Assert.Equal("AGC(0.991)", ind.Name);
    }

    [Fact]
    public void Constructor_SetsWarmupPeriod()
    {
        var ind = new Agc(0.991);
        Assert.Equal(1, ind.WarmupPeriod);
    }

    [Fact]
    public void Constructor_DefaultParameters()
    {
        var ind = new Agc();
        Assert.Equal(0.991, ind.Decay);
    }

    // --- B) Basic Calculation ---

    [Fact]
    public void Calc_ReturnsFiniteValue()
    {
        var ind = new Agc();
        // Feed an oscillating value (not raw price!)
        var result = ind.Update(new TValue(DateTime.UtcNow, 0.5));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Calc_PropertiesAccessible()
    {
        var ind = new Agc();
        ind.Update(new TValue(DateTime.UtcNow, 0.5));
        Assert.True(double.IsFinite(ind.Last.Value));
        Assert.True(ind.IsHot);
        Assert.Equal("AGC(0.991)", ind.Name);
        _ = ind.IsNew;
    }

    [Fact]
    public void SineInput_OutputBounded()
    {
        // A pure sine wave fed through AGC should produce output in [-1, +1]
        var ind = new Agc(0.991);
        var sine = MakeSineWave(500);
        foreach (var item in sine)
        {
            var result = ind.Update(item);
            Assert.True(result.Value >= -1.0001 && result.Value <= 1.0001,
                $"AGC output {result.Value} exceeds [-1, +1] bounds");
        }
    }

    [Fact]
    public void ConstantInput_ReturnsOne()
    {
        // Constant positive input → peak = val → output = val/val = 1.0
        var ind = new Agc(0.991);
        double lastVal = 0;
        for (int i = 0; i < 200; i++)
        {
            lastVal = ind.Update(new TValue(DateTime.UtcNow, 5.0)).Value;
        }
        Assert.Equal(1.0, lastVal, 1e-6);
    }

    [Fact]
    public void ZeroInput_ReturnsZero()
    {
        // Zero input → output = 0 / peak = 0
        var ind = new Agc(0.991);
        ind.Update(new TValue(DateTime.UtcNow, 1.0)); // prime with non-zero
        double val = ind.Update(new TValue(DateTime.UtcNow, 0.0)).Value;
        Assert.Equal(0.0, val, 1e-10);
    }

    // --- C) State + Bar Correction ---

    [Fact]
    public void Calc_IsNew_AcceptsParameter()
    {
        var ind = new Agc();
        var sine = MakeSineWave(20);
        foreach (var item in sine)
        {
            ind.Update(item);
        }
        double val1 = ind.Last.Value;

        ind.Update(new TValue(DateTime.UtcNow, 0.75), isNew: false);
        double val2 = ind.Last.Value;
        Assert.NotEqual(val1, val2);
    }

    [Fact]
    public void Calc_IsNew_False_UpdatesValue()
    {
        var ind = new Agc();
        ind.Update(new TValue(DateTime.UtcNow, 0.5), isNew: true);
        ind.Update(new TValue(DateTime.UtcNow, 0.8), isNew: true);
        double val1 = ind.Last.Value;

        ind.Update(new TValue(DateTime.UtcNow, 0.3), isNew: false);
        double val2 = ind.Last.Value;
        Assert.NotEqual(val1, val2);
    }

    [Fact]
    public void IterativeCorrections_RestoreToOriginalState()
    {
        var ind = new Agc();
        var sine = MakeSineWave(50);

        for (int i = 0; i < sine.Count; i++)
        {
            ind.Update(sine[i]);
        }
        double originalValue = ind.Last.Value;

        // Feed corrections with isNew=false
        ind.Update(new TValue(DateTime.UtcNow, 0.1), isNew: false);
        ind.Update(new TValue(DateTime.UtcNow, 0.9), isNew: false);
        ind.Update(new TValue(DateTime.UtcNow, -0.5), isNew: false);

        // Restore with original last value
        ind.Update(sine[^1], isNew: false);
        double restoredValue = ind.Last.Value;

        Assert.Equal(originalValue, restoredValue, 10);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var ind = new Agc();
        var sine = MakeSineWave(50);
        foreach (var item in sine)
        {
            ind.Update(item);
        }

        ind.Reset();

        var ind2 = new Agc();
        var result1 = ind.Update(new TValue(DateTime.UtcNow, 0.5));
        var result2 = ind2.Update(new TValue(DateTime.UtcNow, 0.5));
        Assert.Equal(result2.Value, result1.Value, 10);
    }

    // --- D) Warmup/Convergence ---

    [Fact]
    public void IsHot_TrueAfterFirstUpdate()
    {
        var ind = new Agc();
        Assert.False(ind.IsHot); // No data yet
        ind.Update(new TValue(DateTime.UtcNow, 0.5));
        Assert.True(ind.IsHot); // One bar is enough
    }

    // --- E) Robustness ---

    [Fact]
    public void NaN_Input_UsesLastValidValue()
    {
        var ind = new Agc();
        ind.Update(new TValue(DateTime.UtcNow, 0.5));
        ind.Update(new TValue(DateTime.UtcNow, 0.8));

        var result = ind.Update(new TValue(DateTime.UtcNow, double.NaN));
        Assert.True(double.IsFinite(result.Value));
    }

    [Fact]
    public void Infinity_Input_UsesLastValidValue()
    {
        var ind = new Agc();
        ind.Update(new TValue(DateTime.UtcNow, 0.5));
        ind.Update(new TValue(DateTime.UtcNow, 0.8));

        var result = ind.Update(new TValue(DateTime.UtcNow, double.PositiveInfinity));
        Assert.True(double.IsFinite(result.Value));

        var result2 = ind.Update(new TValue(DateTime.UtcNow, double.NegativeInfinity));
        Assert.True(double.IsFinite(result2.Value));
    }

    [Fact]
    public void MultipleNaN_ContinuesWithLastValid()
    {
        var ind = new Agc();
        ind.Update(new TValue(DateTime.UtcNow, 0.5));
        ind.Update(new TValue(DateTime.UtcNow, 0.8));

        for (int i = 0; i < 10; i++)
        {
            var result = ind.Update(new TValue(DateTime.UtcNow, double.NaN));
            Assert.True(double.IsFinite(result.Value));
        }
    }

    [Fact]
    public void BatchCalc_HandlesNaN()
    {
        double[] input = [0.5, 0.8, double.NaN, -0.3, double.NaN, 0.6];
        double[] output = new double[input.Length];

        Agc.Batch(input, output, 0.991);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"Output[{i}] should be finite");
        }
    }

    // --- F) Consistency ---

    [Fact]
    public void AllModes_ProduceSameResult()
    {
        const double decay = 0.991;
        var sine = MakeSineWave(200);

        // 1. Span Mode
        double[] spanOutput = new double[sine.Count];
        Agc.Batch(sine.Values.ToArray(), spanOutput, decay);

        // 2. TSeries Batch Mode
        var agcBatch = new Agc(decay);
        var batchResult = agcBatch.Update(sine);

        // 3. Streaming Mode
        var agcStream = new Agc(decay);
        var streamResults = new List<double>();
        foreach (var item in sine)
        {
            streamResults.Add(agcStream.Update(item).Value);
        }

        // 4. Eventing Mode
        var pubSource = new TSeries();
        var agcEvent = new Agc(pubSource, decay);
        for (int i = 0; i < sine.Count; i++)
        {
            pubSource.Add(sine[i]);
        }

        // Assert all modes match
        for (int i = 0; i < sine.Count; i++)
        {
            Assert.Equal(spanOutput[i], batchResult[i].Value, 1e-9);
            Assert.Equal(spanOutput[i], streamResults[i], 1e-9);
        }
        Assert.Equal(spanOutput[^1], agcEvent.Last.Value, 1e-9);
    }

    // --- G) Span API ---

    [Fact]
    public void SpanCalc_ValidatesLength()
    {
        double[] source = new double[10];
        double[] output = new double[5]; // Mismatched!

        Assert.Throws<ArgumentException>(() => Agc.Batch(source, output));
    }

    [Fact]
    public void SpanCalc_SineInput_OutputBounded()
    {
        double[] input = new double[500];
        for (int i = 0; i < input.Length; i++)
        {
            input[i] = Math.Sin(2.0 * Math.PI * i / 20.0);
        }
        double[] output = new double[500];

        Agc.Batch(input, output, 0.991);

        for (int i = 0; i < output.Length; i++)
        {
            Assert.True(output[i] >= -1.0001 && output[i] <= 1.0001,
                $"Output[{i}] = {output[i]} exceeds [-1, +1] bounds");
        }
    }

    [Fact]
    public void SpanCalc_MatchesTSeriesCalc()
    {
        var sine = MakeSineWave(200);

        // Span
        double[] spanOutput = new double[sine.Count];
        Agc.Batch(sine.Values.ToArray(), spanOutput, 0.991);

        // TSeries
        var ind = new Agc(0.991);
        var tseriesResult = ind.Update(sine);

        for (int i = 0; i < sine.Count; i++)
        {
            Assert.Equal(spanOutput[i], tseriesResult[i].Value, 1e-9);
        }
    }

    // --- H) Chainability ---

    [Fact]
    public void Pub_FiresOnUpdate()
    {
        var ind = new Agc();
        int fireCount = 0;
        ind.Pub += (object? _, in TValueEventArgs _) => fireCount++;

        ind.Update(new TValue(DateTime.UtcNow, 0.5));
        ind.Update(new TValue(DateTime.UtcNow, 0.8));

        Assert.Equal(2, fireCount);
    }

    [Fact]
    public void EventChaining_Works()
    {
        var source = new TSeries();
        var ind = new Agc(source);

        source.Add(new TValue(DateTime.UtcNow, 0.5));
        source.Add(new TValue(DateTime.UtcNow, 0.8));

        Assert.True(double.IsFinite(ind.Last.Value));
    }

    // --- Additional ---

    [Fact]
    public void DifferentDecays_ProduceDifferentResults()
    {
        var sine = MakeSineWave(200);

        var ind1 = new Agc(0.991);
        var ind2 = new Agc(0.95);

        foreach (var item in sine)
        {
            ind1.Update(item);
            ind2.Update(item);
        }

        Assert.NotEqual(ind1.Last.Value, ind2.Last.Value);
    }

    [Fact]
    public void LargeDataset_DoesNotThrow()
    {
        double[] input = new double[10000];
        for (int i = 0; i < input.Length; i++)
        {
            input[i] = Math.Sin(2.0 * Math.PI * i / 20.0);
        }
        double[] output = new double[input.Length];

        Agc.Batch(input, output, 0.991);

        Assert.True(double.IsFinite(output[^1]));
    }

    [Fact]
    public void NegativeInput_ProducesNegativeOutput()
    {
        var ind = new Agc();
        ind.Update(new TValue(DateTime.UtcNow, 1.0)); // prime peak
        double val = ind.Update(new TValue(DateTime.UtcNow, -0.5)).Value;
        Assert.True(val < 0, $"Negative input should produce negative output, got {val}");
    }

    [Fact]
    public void Dispose_UnsubscribesFromSource()
    {
        var source = new TSeries();
        var ind = new Agc(source);

        source.Add(new TValue(DateTime.UtcNow, 0.5));
        Assert.True(double.IsFinite(ind.Last.Value));

        ind.Dispose();

        // After dispose, further adds should not affect ind
        double lastBefore = ind.Last.Value;
        source.Add(new TValue(DateTime.UtcNow, 999.0));
        Assert.Equal(lastBefore, ind.Last.Value, 10);
    }
}
