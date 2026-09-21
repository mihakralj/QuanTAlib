using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public sealed class NwValidationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly TBarSeries _bars;
    private bool _disposed;

    public NwValidationTests(ITestOutputHelper output)
    {
        _output = output;
        var gbm = new GBM(startPrice: 100, mu: 0.05, sigma: 0.5, seed: 42);
        _bars = gbm.Fetch(5000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Batch_TSeries_MatchesSpan()
    {
        int period = 20;
        double bw = 5.0;
        var data = _bars.Close;
        var batchTs = Nw.Batch(data, period, bw);

        double[] spanOut = new double[data.Count];
        Nw.Batch(data.Values, spanOut.AsSpan(), period, bw);

        for (int i = 0; i < data.Count; i++)
        {
            Assert.Equal(batchTs[i].Value, spanOut[i], 12);
        }

        _output.WriteLine($"Batch TSeries vs Span: {data.Count} bars matched at 12-digit precision.");
    }

    [Fact]
    public void Calculate_ReturnsCorrectCount()
    {
        int period = 20;
        double bw = 5.0;
        var data = _bars.Close;
        var (results, indicator) = Nw.Calculate(data, period, bw);

        Assert.Equal(data.Count, results.Count);
        Assert.True(indicator.IsHot);
        _output.WriteLine($"Calculate: {results.Count} results, indicator is hot.");
    }

    [Fact]
    public void ConstantInput_ReturnsConstant()
    {
        int period = 20;
        double bw = 5.0;
        double constVal = 42.0;
        double[] src = new double[100];
        double[] dst = new double[100];
        Array.Fill(src, constVal);
        Nw.Batch(src, dst, period, bw);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(constVal, dst[i], 10);
        }
        _output.WriteLine("Constant input: all outputs match constant value.");
    }

    [Fact]
    public void LargeBandwidth_ApproachesSMA()
    {
        // With very large h, all weights are nearly equal → approaches SMA
        int period = 10;
        double bw = 1000.0;
        var data = _bars.Close;
        double[] nwOut = new double[data.Count];
        Nw.Batch(data.Values, nwOut.AsSpan(), period, bw);

        // Compare with manual SMA for bars ≥ period
        int startBar = period;
        for (int t = startBar; t < Math.Min(data.Count, startBar + 100); t++)
        {
            double smaSum = 0;
            for (int i = 0; i < period; i++)
            {
                smaSum += data.Values[t - i];
            }
            double sma = smaSum / period;
            Assert.Equal(sma, nwOut[t], 2); // Within 2 decimal places
        }
        _output.WriteLine($"Large bandwidth (h={bw}): NW ≈ SMA within 2 decimals.");
    }

    [Fact]
    public void Streaming_MatchesBatch()
    {
        int period = 20;
        double bw = 5.0;
        var data = _bars.Close;

        // Batch
        double[] batchOut = new double[data.Count];
        Nw.Batch(data.Values, batchOut.AsSpan(), period, bw);

        // Streaming
        var nw = new Nw(period, bw);
        double[] streamOut = new double[data.Count];
        for (int i = 0; i < data.Count; i++)
        {
            streamOut[i] = nw.Update(new TValue(data.Times[i], data.Values[i])).Value;
        }

        for (int i = 0; i < data.Count; i++)
        {
            Assert.Equal(batchOut[i], streamOut[i], 10);
        }
        _output.WriteLine($"Streaming vs Batch: {data.Count} bars match at 10-digit precision.");
    }

    [Fact]
    public void DifferentBandwidths_OrderedSmoothing()
    {
        var data = _bars.Close;
        double[] out2 = new double[data.Count];
        double[] out20 = new double[data.Count];
        Nw.Batch(data.Values, out2.AsSpan(), 30, 2.0);
        Nw.Batch(data.Values, out20.AsSpan(), 30, 20.0);

        // Wider bandwidth should produce smoother output (lower variance in diffs)
        double var2 = 0, var20 = 0;
        int n = data.Count - 1;
        for (int i = 1; i < data.Count; i++)
        {
            double d2 = out2[i] - out2[i - 1];
            double d20 = out20[i] - out20[i - 1];
            var2 += d2 * d2;
            var20 += d20 * d20;
        }
        var2 /= n;
        var20 /= n;

        Assert.True(var20 < var2, $"Wider bandwidth should be smoother: var(h=20)={var20:E4} < var(h=2)={var2:E4}");
        _output.WriteLine($"Bandwidth ordering: var(h=2)={var2:E4} > var(h=20)={var20:E4}");
    }

    [Fact]
    public void Verify_Manual_Calc()
    {
        // Manual NW calculation for small dataset
        double[] src = { 10.0, 20.0, 30.0, 40.0, 50.0 };
        double[] dst = new double[5];
        int period = 3;
        double h = 1.0;
        Nw.Batch(src, dst, period, h);

        // Bar 0: only src[0], w0=1.0 → result = 10.0
        Assert.Equal(10.0, dst[0], 10);

        // Bar 1: src[1] with w0=1.0, src[0] with w1=exp(-1/(2*1))=exp(-0.5)
        double w0 = 1.0;
        double w1 = Math.Exp(-0.5);
        double expected1 = ((w0 * 20.0) + (w1 * 10.0)) / (w0 + w1);
        Assert.Equal(expected1, dst[1], 10);

        // Bar 2: src[2] w0=1, src[1] w1=exp(-0.5), src[0] w2=exp(-4/2)=exp(-2)
        double w2 = Math.Exp(-2.0);
        double expected2 = ((w0 * 30.0) + (w1 * 20.0) + (w2 * 10.0)) / (w0 + w1 + w2);
        Assert.Equal(expected2, dst[2], 10);

        _output.WriteLine($"Manual calc: bar0={dst[0]:F6}, bar1={dst[1]:F6} (expect {expected1:F6}), bar2={dst[2]:F6} (expect {expected2:F6})");
    }
}
