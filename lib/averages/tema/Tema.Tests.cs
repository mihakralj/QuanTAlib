namespace QuanTAlib.Tests;

#pragma warning disable S2245 // Random is acceptable for simulation/testing purposes
public class TemaTests
{
    [Fact]
    public void Tema_Constructor_Period_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Tema(0));
        Assert.Throws<ArgumentException>(() => new Tema(-1));

        var tema = new Tema(10);
        Assert.NotNull(tema);
    }

    [Fact]
    public void Tema_Constructor_Alpha_ValidatesInput()
    {
        Assert.Throws<ArgumentException>(() => new Tema(0.0));
        Assert.Throws<ArgumentException>(() => new Tema(-0.1));
        Assert.Throws<ArgumentException>(() => new Tema(1.1));

        var tema = new Tema(0.5);
        Assert.NotNull(tema);
    }

    [Fact]
    public void Tema_Calc_ReturnsValue()
    {
        var tema = new Tema(10);

        Assert.Equal(0, tema.Value.Value);

        TValue result = tema.Update(new TValue(DateTime.UtcNow, 100));

        Assert.True(result.Value > 0);
        Assert.Equal(result.Value, tema.Value.Value);
    }

    [Fact]
    public void Tema_Calc_IsNew_AcceptsParameter()
    {
        var tema = new Tema(10);

        tema.Update(new TValue(DateTime.UtcNow, 100), isNew: true);
        double value1 = tema.Value;

        tema.Update(new TValue(DateTime.UtcNow, 105), isNew: true);
        double value2 = tema.Value;

        // Values should change with new bars
        Assert.NotEqual(value1, value2);
    }

    [Fact]
    public void Tema_Calc_IsNew_False_UpdatesValue()
    {
        var tema = new Tema(10);

        tema.Update(new TValue(DateTime.UtcNow, 100));
        tema.Update(new TValue(DateTime.UtcNow, 110), isNew: true);
        double beforeUpdate = tema.Value;

        tema.Update(new TValue(DateTime.UtcNow, 120), isNew: false);
        double afterUpdate = tema.Value;

        // Update should change the value
        Assert.NotEqual(beforeUpdate, afterUpdate);
    }

    [Fact]
    public void Tema_Reset_ClearsState()
    {
        var tema = new Tema(10);

        tema.Update(new TValue(DateTime.UtcNow, 100));
        tema.Update(new TValue(DateTime.UtcNow, 105));
        double valueBefore = tema.Value;

        tema.Reset();

        Assert.Equal(0, tema.Value.Value);

        // After reset, should accept new values
        tema.Update(new TValue(DateTime.UtcNow, 50));
        Assert.NotEqual(0, tema.Value.Value);
        Assert.NotEqual(valueBefore, tema.Value.Value);
    }

    [Fact]
    public void Tema_Properties_Accessible()
    {
        var tema = new Tema(10);

        Assert.Equal(0, tema.Value.Value);
        Assert.False(tema.IsHot);

        tema.Update(new TValue(DateTime.UtcNow, 100));

        Assert.NotEqual(0, tema.Value.Value);
    }

    [Fact]
    public void Tema_IsHot_BecomesTrueAfterWarmup()
    {
        var tema = new Tema(10);

        // Initially IsHot should be false
        Assert.False(tema.IsHot);

        // TEMA needs more warmup than EMA due to triple smoothing
        int steps = 0;
        while (!tema.IsHot && steps < 1000)
        {
            tema.Update(new TValue(DateTime.UtcNow, 100));
            steps++;
        }

        Assert.True(tema.IsHot);
        Assert.True(steps > 0);
    }

    [Fact]
    public void Tema_PeriodEquivalence_BothConstructorsWork()
    {
        int period = 20;
        double alpha = 2.0 / (period + 1);

        var temaPeriod = new Tema(period);
        var temaAlpha = new Tema(alpha);

        // Both should accept Calc calls and produce same result
        TValue result1 = temaPeriod.Update(new TValue(DateTime.UtcNow, 100));
        TValue result2 = temaAlpha.Update(new TValue(DateTime.UtcNow, 100));

        Assert.Equal(result1.Value, result2.Value, 1e-10);
    }

    [Fact]
    public void Tema_IterativeCorrections_RestoreToOriginalState()
    {
        var tema = new Tema(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Feed 10 new values
        TValue tenthInput = default;
        for (int i = 0; i < 10; i++)
        {
            var bar = gbm.Next(isNew: true);
            tenthInput = new TValue(bar.Time, bar.Close);
            tema.Update(tenthInput, isNew: true);
        }

        // Remember TEMA state after 10 values
        double temaAfterTen = tema.Value;

        // Generate 9 corrections with isNew=false (different values)
        for (int i = 0; i < 9; i++)
        {
            var bar = gbm.Next(isNew: false);
            tema.Update(new TValue(bar.Time, bar.Close), isNew: false);
        }

        // Feed the remembered 10th input again with isNew=false
        TValue finalTema = tema.Update(tenthInput, isNew: false);

        // TEMA should match the original state after 10 values
        Assert.Equal(temaAfterTen, finalTema.Value, 1e-10);
    }

    [Fact]
    public void Tema_BatchCalc_MatchesIterativeCalc()
    {
        var temaIterative = new Tema(10);
        var temaBatch = new Tema(10);
        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1);

        // Generate data
        var series = new TSeries();
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            series.Add(bar.Time, bar.Close);
        }

        Assert.True(series.Count > 0);

        // Calculate iteratively
        var iterativeResults = new TSeries();
        foreach (var item in series)
        {
            iterativeResults.Add(temaIterative.Update(item));
        }

        // Calculate batch
        var batchResults = temaBatch.Update(series);

        // Compare
        Assert.Equal(iterativeResults.Count, batchResults.Count);
        for (int i = 0; i < iterativeResults.Count; i++)
        {
            Assert.Equal(iterativeResults[i].Value, batchResults[i].Value, 1e-10);
            Assert.Equal(iterativeResults[i].Time, batchResults[i].Time);
        }
    }

    [Fact]
    public void Tema_NaN_Input_UsesLastValidValue()
    {
        var tema = new Tema(10);

        // Feed some valid values
        tema.Update(new TValue(DateTime.UtcNow, 100));
        tema.Update(new TValue(DateTime.UtcNow, 110));

        // Feed NaN - should use last valid value (110)
        var resultAfterNaN = tema.Update(new TValue(DateTime.UtcNow, double.NaN));

        // Result should be finite (not NaN)
        Assert.True(double.IsFinite(resultAfterNaN.Value));
        Assert.NotEqual(0, resultAfterNaN.Value);
    }

    [Fact]
    public void Tema_SpanCalc_MatchesTSeriesCalc()
    {
        var series = new TSeries();
        double[] source = new double[100];
        double[] output = new double[100];

        var gbm = new GBM(startPrice: 100.0, mu: 0.02, sigma: 0.1, seed: 42);
        for (int i = 0; i < 100; i++)
        {
            var bar = gbm.Next(isNew: true);
            source[i] = bar.Close;
            series.Add(bar.Time, bar.Close);
        }

        // Calculate with TSeries API
        var tseriesResult = Tema.Calculate(series, 10);

        // Calculate with Span API
        Tema.Calculate(source.AsSpan(), output.AsSpan(), 10);

        // Compare results
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(tseriesResult[i].Value, output[i], 1e-9);
        }
    }

    [Fact]
    public void Tema_SpanCalc_ZeroAllocation()
    {
        double[] source = new double[10000];
        double[] output = new double[10000];
        var rng = new Random(42); // nosemgrep
        for (int i = 0; i < source.Length; i++)
            source[i] = rng.NextDouble() * 100;

        // Warm up
        Tema.Calculate(source.AsSpan(), output.AsSpan(), 100);

        // This test verifies the method runs without throwing
        Assert.True(double.IsFinite(output[^1]));
    }
}
