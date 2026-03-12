using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public sealed class LtmaValidationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public LtmaValidationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _ = disposing;
    }

    [Fact]
    public void CrossValidate_Dema_Batch()
    {
        // LTMA with same formula as DEMA (2·EMA1 - EMA2) must produce identical results
        int[] periods = { 5, 10, 14, 20, 50 };
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        var source = new TSeries();
        for (int i = 0; i < 500; i++)
        {
            var bar = gbm.Next(isNew: true);
            source.Add(new TValue(bar.Time, bar.Close));
        }

        foreach (var period in periods)
        {
            var ltmaBatch = Ltma.Batch(source, period);
            var demaBatch = Dema.Batch(source, period);

            for (int i = 0; i < source.Count; i++)
            {
                Assert.Equal(demaBatch[i].Value, ltmaBatch[i].Value, 1e-9);
            }

            _output.WriteLine($"Period {period}: LTMA matches DEMA for {source.Count} bars.");
        }
    }

    [Fact]
    public void CrossValidate_Dema_Streaming()
    {
        const int period = 14;
        var ltma = new Ltma(period);
        var dema = new Dema(period);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);

        for (int i = 0; i < 500; i++)
        {
            var bar = gbm.Next(isNew: true);
            var tVal = new TValue(bar.Time, bar.Close);

            var ltmaVal = ltma.Update(tVal);
            var demaVal = dema.Update(tVal);

            Assert.Equal(demaVal.Value, ltmaVal.Value, 1e-9);
        }

        _output.WriteLine($"Streaming: LTMA matches DEMA for 500 bars at period {period}.");
    }

    [Fact]
    public void CrossValidate_Dema_Span()
    {
        const int period = 14;
        const int count = 500;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);

        var values = new double[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = gbm.Next(isNew: true).Close;
        }

        var ltmaOutput = new double[count];
        var demaOutput = new double[count];
        Ltma.Batch(values, ltmaOutput, period);
        Dema.Batch(values, demaOutput, period);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(demaOutput[i], ltmaOutput[i], 1e-9);
        }

        _output.WriteLine($"Span: LTMA matches DEMA for {count} bars at period {period}.");
    }

    [Fact]
    public void Streaming_Matches_Batch()
    {
        const int period = 14;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        var source = new TSeries();
        for (int i = 0; i < 300; i++)
        {
            var bar = gbm.Next(isNew: true);
            source.Add(new TValue(bar.Time, bar.Close));
        }

        // Streaming
        var streaming = new Ltma(period);
        var streamResults = new double[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            streamResults[i] = streaming.Update(source[i]).Value;
        }

        // Batch
        var batchResults = Ltma.Batch(source, period);

        double maxDiff = 0;
        for (int i = 0; i < source.Count; i++)
        {
            double diff = Math.Abs(streamResults[i] - batchResults[i].Value);
            if (diff > maxDiff)
            {
                maxDiff = diff;
            }
            Assert.Equal(streamResults[i], batchResults[i].Value, 1e-9);
        }

        _output.WriteLine($"Streaming vs Batch max diff: {maxDiff:E3}");
    }

    [Fact]
    public void ConstantInput_ConvergesToConstant()
    {
        const int period = 10;
        const double constant = 42.0;
        var ltma = new Ltma(period);

        double lastVal = double.NaN;
        for (int i = 0; i < 500; i++)
        {
            lastVal = ltma.Update(new TValue(DateTime.UtcNow.AddSeconds(i), constant)).Value;
        }

        // LTMA of constant = constant (slope = 0, so 2*C - C = C)
        Assert.Equal(constant, lastVal, 1e-6);
        _output.WriteLine($"Constant input {constant}: converged to {lastVal}");
    }

    [Fact]
    public void MultiplePeriods_AllFinite()
    {
        int[] periods = { 1, 2, 3, 5, 10, 14, 20, 50, 100 };

        foreach (var period in periods)
        {
            var ltma = new Ltma(period);
            var localGbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);

            for (int i = 0; i < 200; i++)
            {
                var bar = localGbm.Next(isNew: true);
                var result = ltma.Update(new TValue(bar.Time, bar.Close));
                Assert.True(double.IsFinite(result.Value), $"Period {period}, bar {i}: NaN/Inf detected");
            }
        }
    }

    [Fact]
    public void ManualFormula_2EMA1_minus_EMA2()
    {
        // Verify LTMA = 2·EMA1 - EMA2 using raw EMA composition
        const int period = 10;
        var ema1 = new Ema(period);
        var ema2 = new Ema(period);
        var ltma = new Ltma(period);
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);

        for (int i = 0; i < 200; i++)
        {
            var bar = gbm.Next(isNew: true);
            var tVal = new TValue(bar.Time, bar.Close);

            var ltmaVal = ltma.Update(tVal);
            var e1 = ema1.Update(tVal);
            var e2 = ema2.Update(e1);
            double expected = (2.0 * e1.Value) - e2.Value;

            Assert.Equal(expected, ltmaVal.Value, 1e-9);
        }

        _output.WriteLine("Manual formula 2·EMA1 - EMA2 verified for 200 bars.");
    }
}
