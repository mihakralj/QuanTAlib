using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public class HendValidationTests(ITestOutputHelper output)
{
    private readonly ValidationTestData _testData = new();
    private readonly ITestOutputHelper _output = output;
    private const int DefaultPeriod = 7;

    // ── Batch vs Streaming consistency ──────────────────────────────────

    [Fact]
    public void BatchVsStreaming_Match()
    {
        var source = new TSeries();
        var gbm = new GBM(startPrice: 100, seed: 42);
        const int count = 100;

        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next();
            source.Add(bar.C);
        }

        // Streaming
        var hend = new Hend(DefaultPeriod);
        var streaming = new double[count];
        for (int i = 0; i < count; i++)
        {
            streaming[i] = hend.Update(source[i]).Value;
        }

        // Batch
        var batchResult = Hend.Batch(source, DefaultPeriod);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(streaming[i], batchResult[i].Value, 1e-10);
        }
    }

    // ── Span vs Streaming consistency ──────────────────────────────────

    [Fact]
    public void SpanVsStreaming_Match()
    {
        var source = new TSeries();
        var gbm = new GBM(startPrice: 100, seed: 42);
        const int count = 100;

        for (int i = 0; i < count; i++)
        {
            var bar = gbm.Next();
            source.Add(bar.C);
        }

        // Streaming
        var hend = new Hend(DefaultPeriod);
        var streaming = new double[count];
        for (int i = 0; i < count; i++)
        {
            streaming[i] = hend.Update(source[i]).Value;
        }

        // Span
        double[] spanOutput = new double[count];
        Hend.Batch(source.Values, spanOutput, DefaultPeriod);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(streaming[i], spanOutput[i], 1e-10);
        }
    }

    // ── Polynomial exact-fit validation ────────────────────────────────

    [Fact]
    public void LinearPolynomial_ExactFit()
    {
        // Henderson preserves linear trends at the CENTER of the window.
        // For period=7, half=3, output at bar N = polynomial at bar N-3.
        int half = (DefaultPeriod - 1) / 2;
        var hend = new Hend(DefaultPeriod);
        const int total = 50;
        const double a = 5.0, b = 3.0;

        for (int i = 0; i < total; i++)
        {
            double val = a + b * i;
            hend.Update(new TValue(DateTime.UtcNow.AddSeconds(i), val));
        }

        int centerIdx = total - 1 - half;
        double expected = a + b * centerIdx;
        _output.WriteLine($"Linear: expected={expected}, actual={hend.Last.Value}");
        Assert.Equal(expected, hend.Last.Value, 1e-6);
    }

    [Fact]
    public void QuadraticPolynomial_ExactFit()
    {
        int half = (DefaultPeriod - 1) / 2;
        var hend = new Hend(DefaultPeriod);
        const int total = 50;
        const double a = 2.0, b = 1.5, c = 0.3;

        for (int i = 0; i < total; i++)
        {
            double val = a + b * i + c * i * i;
            hend.Update(new TValue(DateTime.UtcNow.AddSeconds(i), val));
        }

        int centerIdx = total - 1 - half;
        double expected = a + b * centerIdx + c * centerIdx * centerIdx;
        _output.WriteLine($"Quadratic: expected={expected}, actual={hend.Last.Value}");
        Assert.Equal(expected, hend.Last.Value, 0.1);
    }

    [Fact]
    public void CubicPolynomial_ExactFit()
    {
        int half = (DefaultPeriod - 1) / 2;
        var hend = new Hend(DefaultPeriod);
        const int total = 50;
        const double a = 1.0, b = 0.5, c = 0.1, d = 0.005;

        for (int i = 0; i < total; i++)
        {
            double val = a + b * i + c * i * i + d * i * i * i;
            hend.Update(new TValue(DateTime.UtcNow.AddSeconds(i), val));
        }

        int centerIdx = total - 1 - half;
        double expected = a + b * centerIdx + c * centerIdx * centerIdx + d * centerIdx * centerIdx * centerIdx;
        _output.WriteLine($"Cubic: expected={expected}, actual={hend.Last.Value}");
        Assert.Equal(expected, hend.Last.Value, 1.0);
    }

    // ── Calculate returns hot indicator ─────────────────────────────────

    [Fact]
    public void Calculate_ReturnsHotIndicator()
    {
        var source = new TSeries();
        var gbm = new GBM(startPrice: 100, seed: 42);

        for (int i = 0; i < 50; i++)
        {
            var bar = gbm.Next();
            source.Add(bar.C);
        }

        var (results, indicator) = Hend.Calculate(source, DefaultPeriod);

        Assert.True(indicator.IsHot);
        Assert.Equal(50, results.Count);
    }
}
