using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public sealed class ModfValidationTests : IDisposable
{
    private readonly TBarSeries _bars;

    public ModfValidationTests(ITestOutputHelper _)
    {
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.5, seed: 42);
        _bars = gbm.Fetch(5000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    public void Dispose() { Dispose(true); }
    private void Dispose(bool _) { /* nothing to release */ }

    [Fact]
    public void BatchStreaming_Match()
    {
        int period = 14;
        var data = _bars.Close;
        var batch = Modf.Batch(data, period);

        var streaming = new Modf(period);
        for (int i = 0; i < data.Count; i++)
        {
            double sv = streaming.Update(data[i]).Value;
            Assert.Equal(sv, batch[i].Value, 10);
        }
    }

    [Fact]
    public void SpanStreaming_Match()
    {
        int period = 14;
        var data = _bars.Close;
        double[] spanOut = new double[data.Count];
        Modf.Batch(data.Values, spanOut, period);

        var streaming = new Modf(period);
        for (int i = 0; i < data.Count; i++)
        {
            double sv = streaming.Update(data[i]).Value;
            Assert.Equal(sv, spanOut[i], 10);
        }
    }

    [Fact]
    public void DifferentPeriods_ProduceDifferentResults()
    {
        var data = _bars.Close;
        double[] out10 = new double[data.Count];
        double[] out30 = new double[data.Count];
        Modf.Batch(data.Values, out10, 10);
        Modf.Batch(data.Values, out30, 30);

        bool anyDifferent = false;
        for (int i = 50; i < data.Count; i++)
        {
            if (Math.Abs(out10[i] - out30[i]) > 1e-8) { anyDifferent = true; break; }
        }
        Assert.True(anyDifferent);
    }

    [Fact]
    public void ConstantInput_ConvergesToConstant()
    {
        var modf = new Modf(14);
        for (int i = 0; i < 200; i++)
        {
            modf.Update(new TValue(DateTime.UtcNow, 50.0));
        }
        Assert.Equal(50.0, modf.Last.Value, 8);
    }

    [Fact]
    public void Calculate_ReturnsHotIndicator()
    {
        var data = _bars.Close;
        var (results, indicator) = Modf.Calculate(data, 14);
        Assert.True(indicator.IsHot);
        Assert.Equal(data.Count, results.Count);
    }

    [Fact]
    public void BarCorrection_Consistency()
    {
        int period = 14;
        var data = _bars.Close;

        var modf1 = new Modf(period);
        var modf2 = new Modf(period);

        // modf1: stream normally
        for (int i = 0; i < data.Count; i++)
        {
            modf1.Update(data[i]);
        }

        // modf2: stream with corrections on each bar
        for (int i = 0; i < data.Count; i++)
        {
            modf2.Update(new TValue(data[i].Time, data[i].Value * 1.01), isNew: true);
            modf2.Update(data[i], isNew: false);
        }

        Assert.Equal(modf1.Last.Value, modf2.Last.Value, 10);
    }

    [Fact]
    public void DifferentBetas_ProduceDifferentResults()
    {
        var data = _bars.Close;
        double[] outLow = new double[data.Count];
        double[] outHigh = new double[data.Count];
        Modf.Batch(data.Values, outLow, 14, beta: 0.2);
        Modf.Batch(data.Values, outHigh, 14, beta: 0.9);

        bool anyDifferent = false;
        for (int i = 20; i < data.Count; i++)
        {
            if (Math.Abs(outLow[i] - outHigh[i]) > 1e-8) { anyDifferent = true; break; }
        }
        Assert.True(anyDifferent);
    }

    [Fact]
    public void Feedback_ProducesDifferentResults()
    {
        var data = _bars.Close;
        double[] outNoFb = new double[data.Count];
        double[] outFb = new double[data.Count];
        Modf.Batch(data.Values, outNoFb, 14, feedback: false);
        Modf.Batch(data.Values, outFb, 14, feedback: true, fbWeight: 0.5);

        bool anyDifferent = false;
        for (int i = 20; i < data.Count; i++)
        {
            if (Math.Abs(outNoFb[i] - outFb[i]) > 1e-8) { anyDifferent = true; break; }
        }
        Assert.True(anyDifferent);
    }
}
