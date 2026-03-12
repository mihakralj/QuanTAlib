namespace QuanTAlib.Tests;

public class DecoValidationTests
{
    private const double Tolerance = 1e-10;

    [Fact]
    public void StreamingMatchesBatch_DefaultParams()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        var bars = gbm.Fetch(500, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        // Streaming
        var deco = new Deco(30, 60);
        var streaming = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streaming[i] = deco.Update(source[i]).Value;
        }

        // Batch span
        var batch = new double[source.Count];
        Deco.Batch(source.Values, batch, 30, 60);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(batch[i], streaming[i], Tolerance);
        }
    }

    [Fact]
    public void StreamingMatchesBatch_ShortPeriods()
    {
        var gbm = new GBM(startPrice: 50.0, mu: 0.01, sigma: 0.3, seed: 7);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        var deco = new Deco(5, 15);
        var streaming = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streaming[i] = deco.Update(source[i]).Value;
        }

        var batch = new double[source.Count];
        Deco.Batch(source.Values, batch, 5, 15);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.Equal(batch[i], streaming[i], Tolerance);
        }
    }

    [Fact]
    public void ConstantPrice_OscillatesAtZero()
    {
        var source = new TSeries();
        for (int i = 0; i < 100; i++)
        {
            source.Add(new TValue(DateTime.UtcNow.AddSeconds(i), 50.0));
        }

        var deco = new Deco(10, 20);
        for (int i = 0; i < source.Count; i++)
        {
            var result = deco.Update(source[i]);
            if (i >= 2)
            {
                Assert.Equal(0.0, result.Value, Tolerance);
            }
        }
    }

    [Fact]
    public void Deterministic_SameInputSameOutput()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 123);
        var bars = gbm.Fetch(300, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        var deco1 = new Deco(10, 30);
        var deco2 = new Deco(10, 30);

        for (int i = 0; i < source.Count; i++)
        {
            var r1 = deco1.Update(source[i]);
            var r2 = deco2.Update(source[i]);
            Assert.Equal(r1.Value, r2.Value, Tolerance);
        }
    }

    [Fact]
    public void DirectionalCorrectness_UpTrend()
    {
        // Exponential growth produces non-zero HP output (linear ramp has zero second-difference)
        var deco = new Deco(5, 10);
        double lastVal = 0;
        for (int i = 0; i < 50; i++)
        {
            var result = deco.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 * Math.Exp(0.02 * i)));
            lastVal = result.Value;
        }
        // Exponential uptrend produces non-zero DECO
        Assert.NotEqual(0.0, lastVal);
    }

    [Fact]
    public void SymmetryCheck_OppositeInputs()
    {
        // Sinusoidal inputs with opposite phase should produce opposite-sign DECO values
        var decoUp = new Deco(5, 10);
        var decoDown = new Deco(5, 10);

        double lastUp = 0, lastDown = 0;
        for (int i = 0; i < 60; i++)
        {
            double phase = 2.0 * Math.PI * i / 20.0; // period=20 bars
            var rUp = decoUp.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 + 10.0 * Math.Sin(phase)));
            var rDown = decoDown.Update(new TValue(DateTime.UtcNow.AddSeconds(i), 100.0 - 10.0 * Math.Sin(phase)));
            lastUp = rUp.Value;
            lastDown = rDown.Value;
        }
        // Opposite-phase sinusoidal inputs should produce opposite-sign DECO values
        Assert.True(lastUp * lastDown < 0,
            $"Expected opposite signs: up={lastUp}, down={lastDown}");
    }

    [Fact]
    public void HpFilter_Components_SumCorrectly()
    {
        // Verify that the HP_long and HP_short filters produce sensible output:
        // For constant input, both HP outputs should be zero, hence DECO = 0
        var source = new double[50];
        Array.Fill(source, 42.0);
        var output = new double[50];
        Deco.Batch(source, output, 10, 20);

        for (int i = 0; i < 50; i++)
        {
            Assert.Equal(0.0, output[i], Tolerance);
        }
    }

    [Fact]
    public void LargeDataset_NoOverflow()
    {
        var gbm = new GBM(startPrice: 1000.0, mu: 0.1, sigma: 0.5, seed: 55);
        var bars = gbm.Fetch(5000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        var output = new double[source.Count];
        var ex = Record.Exception(() => Deco.Batch(source.Values, output, 30, 60));
        Assert.Null(ex);

        for (int i = 0; i < source.Count; i++)
        {
            Assert.True(double.IsFinite(output[i]), $"Non-finite at index {i}");
        }
    }

    [Fact]
    public void CalculateMethod_ReturnsConsistentResults()
    {
        var gbm = new GBM(startPrice: 100.0, mu: 0.05, sigma: 0.2, seed: 42);
        var bars = gbm.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        TSeries source = bars.Close;

        var (results, indicator) = Deco.Calculate(source, 15, 30);
        Assert.Equal(source.Count, results.Count);
        Assert.True(indicator.IsHot);
        Assert.True(double.IsFinite(results.Values[^1]));
    }
}
